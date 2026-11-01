using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Winigolf.Game;

internal sealed record OnlineParticipant(string Id, string Name, int Seat, string Color);
internal sealed record OnlinePlayerState(string Id, string Name, string Color, double X, double Y, double Z,
    int Surface, int Strokes, int Total, bool Completed);
internal sealed record OnlineMatchSnapshot(int Revision, int CourseId, string CourseName, string CourseHash,
    string PhysicsVersion, int ActiveIndex, string ActivePlayerId, bool CanShoot, double CameraYawDegrees,
    IReadOnlyList<OnlinePlayerState> Players);

internal abstract record OnlineCommand;
internal sealed record OnlineStart(IReadOnlyList<OnlineParticipant> Players) : OnlineCommand;
internal sealed record OnlineAim(string PlayerId, int Revision, int AngleDegrees, int StrengthPercent) : OnlineCommand;
internal sealed record OnlineShot(string PlayerId, string ShotId, int Revision, int AngleDegrees, int StrengthPercent) : OnlineCommand;

// The HTTP/WebSocket tasks only validate and enqueue input. The Raylib thread owns and mutates the match.
internal sealed class OnlineHost : IDisposable
{
    private static readonly string[] Palette = ["#F5D362", "#70C0FF", "#F98B97", "#A4E19A", "#C39FF6", "#FFAD6C"];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly object gate = new();
    private readonly List<Peer> peers = [];
    private readonly ConcurrentQueue<OnlineCommand> commands = new();
    private readonly GameConfig config;
    private WebApplication? application;
    private OnlineMatchSnapshot? snapshot;
    private string? firstPlayerId;
    private string? firstPlayerName;
    private bool started;
    private bool shotPending;
    private bool disposed;
    private string? lastShotId;
    private object? lastShotResult;

    public OnlineHost(GameConfig config)
    {
        this.config = config;
        config.Validate();
        if (!config.OnlineMode) throw new InvalidOperationException("Online host is disabled by configuration.");
    }

    public string JoinUrl { get; private set; } = "http://127.0.0.1:5188/";
    public bool Started { get { lock (gate) return started; } }
    public int ConnectedCount { get { lock (gate) return peers.Count(p => p.Player is not null && p.Socket.State == WebSocketState.Open); } }
    public string[] LobbyNames { get { lock (gate) return peers.Where(p => p.Player is not null).Select(p => p.Player!.Name).ToArray(); } }

    public void Start()
    {
        if (application is not null) throw new InvalidOperationException("The online host is already running.");
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://0.0.0.0:{config.Port}");
        var app = builder.Build();
        app.UseWebSockets();
        var pagePath = Path.Combine(AppContext.BaseDirectory, "web", "index.html");
        app.MapGet("/", async context =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(pagePath, context.RequestAborted);
        });
        app.Map("/ws", HandleSocketAsync);
        app.StartAsync().GetAwaiter().GetResult();
        application = app;
        JoinUrl = $"http://{GetLanAddress()}:{config.Port}/";
    }

    public bool TryDequeue(out OnlineCommand command) => commands.TryDequeue(out command!);

    public void PublishSnapshot(OnlineMatchSnapshot value)
    {
        lock (gate)
        {
            snapshot = value;
            if (value.CanShoot) shotPending = false;
            BroadcastLocked(new { type = "state", state = value });
        }
    }

    public void RejectShot(string playerId, string shotId, string reason)
    {
        lock (gate)
        {
            shotPending = false;
            var peer = FindPeer(playerId);
            peer?.Enqueue(new { type = "shotRejected", shotId, reason });
            if (snapshot is not null) peer?.Enqueue(new { type = "state", state = snapshot });
        }
    }

    public void ConfirmShot(string playerId, string shotId, int angle, int strength, int steps, string outcome,
        double x, double y, double z, int surface, int strokes, int penalty)
    {
        lock (gate)
        {
            var result = new { type = "shotResult", shotId, playerId, angleDegrees = angle, strengthPercent = strength,
                steps, outcome, finalPosition = new { x, y, z, surface }, strokes, penalty };
            lastShotId = shotId; lastShotResult = result;
            BroadcastLocked(result);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { application?.StopAsync().GetAwaiter().GetResult(); }
        finally { application?.DisposeAsync().AsTask().GetAwaiter().GetResult(); application = null; }
    }

    private async Task HandleSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var peer = new Peer(socket);
        lock (gate) peers.Add(peer);
        var sender = SendLoopAsync(peer, context.RequestAborted);
        try { await ReceiveLoopAsync(peer, context.RequestAborted); }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            lock (gate)
            {
                peers.Remove(peer);
                peer.Outgoing.Writer.TryComplete();
                if (!started && peer.Player is not null) BroadcastLobbyLocked();
            }
            try { if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived) await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
            catch (WebSocketException) { }
            try { await sender; }
            catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException) { }
        }
    }

    private async Task ReceiveLoopAsync(Peer peer, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        using var message = new MemoryStream();
        while (!cancellationToken.IsCancellationRequested && peer.Socket.State == WebSocketState.Open)
        {
            var part = await peer.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (part.MessageType == WebSocketMessageType.Close) return;
            if (part.MessageType != WebSocketMessageType.Text) { peer.Enqueue(new { type = "error", message = "Only text messages are supported." }); continue; }
            if (message.Length + part.Count > 16_384) { peer.Enqueue(new { type = "error", message = "Message too large." }); message.SetLength(0); continue; }
            message.Write(buffer, 0, part.Count);
            if (!part.EndOfMessage) continue;
            try { using var document = JsonDocument.Parse(message.ToArray()); HandleMessage(peer, document.RootElement); }
            catch (JsonException) { peer.Enqueue(new { type = "error", message = "Invalid JSON." }); }
            finally { message.SetLength(0); }
        }
    }

    private void HandleMessage(Peer peer, JsonElement json)
    {
        if (!json.TryGetProperty("type", out var typeValue)) return;
        var type = typeValue.GetString();
        lock (gate)
        {
            switch (type)
            {
                case "join": JoinLocked(peer, ReadString(json, "name", 18)); break;
                case "start": StartLocked(peer); break;
                case "aim": AimLocked(peer, json); break;
                case "shot": ShotLocked(peer, json); break;
            }
        }
    }

    private void JoinLocked(Peer peer, string name)
    {
        name = name.Trim();
        if (peer.Player is not null) { peer.Enqueue(new { type = "error", message = "Already joined." }); return; }
        if (started) { peer.Enqueue(new { type = "error", message = "This match has already started." }); return; }
        if (name.Length is < 1 or > 18) { peer.Enqueue(new { type = "error", message = "Name must contain 1 to 18 characters." }); return; }
        var seat = peers.Count(p => p.Player is not null);
        if (seat >= Palette.Length) { peer.Enqueue(new { type = "error", message = "The room is full (6 players)." }); return; }
        var player = new OnlineParticipant(Guid.NewGuid().ToString("N")[..10], name, seat, Palette[seat]);
        peer.Player = player;
        if (firstPlayerId is null) { firstPlayerId = player.Id; firstPlayerName = player.Name; }
        peer.Enqueue(new { type = "joined", playerId = player.Id, player.Name, player.Color, seat, isHost = player.Id == firstPlayerId });
        BroadcastLobbyLocked();
    }

    private void StartLocked(Peer peer)
    {
        if (peer.Player is null) { peer.Enqueue(new { type = "error", message = "Join the room first." }); return; }
        if (peer.Player.Id != firstPlayerId)
        { peer.Enqueue(new { type = "error", message = "Only the first player to join can start the match." }); return; }
        if (started) { peer.Enqueue(new { type = "error", message = "The match is already starting." }); return; }
        var players = peers.Where(p => p.Player is not null).Select(p => p.Player!).ToArray();
        if (players.Length == 0) return;
        started = true;
        commands.Enqueue(new OnlineStart(players));
        BroadcastLocked(new { type = "starting", players });
    }

    private void AimLocked(Peer peer, JsonElement json)
    {
        if (peer.Player is null || !TryInput(json, out var revision, out var angle, out var strength)) return;
        var now = Environment.TickCount64;
        if (now - peer.LastAimAt < 40) return;
        peer.LastAimAt = now;
        if (shotPending || snapshot is not { CanShoot: true } state || state.Revision != revision || state.ActivePlayerId != peer.Player.Id) return;
        var aim = new OnlineAim(peer.Player.Id, revision, angle, strength);
        commands.Enqueue(aim);
        BroadcastLocked(new { type = "aim", playerId = peer.Player.Id, angleDegrees = angle, strengthPercent = strength, revision });
    }

    private void ShotLocked(Peer peer, JsonElement json)
    {
        if (peer.Player is null) return;
        var shotId = ReadString(json, "shotId", 64);
        if (shotId.Length == 0) return;
        if (shotId == lastShotId && lastShotResult is not null) { peer.Enqueue(lastShotResult); return; }
        if (shotPending) { peer.Enqueue(new { type = "shotRejected", shotId, reason = "A shot is already being resolved." }); return; }
        if (!TryInput(json, out var revision, out var angle, out var strength) ||
            snapshot is not { CanShoot: true } state || state.Revision != revision || state.ActivePlayerId != peer.Player.Id)
        { peer.Enqueue(new { type = "shotRejected", shotId, reason = "It is not your turn or the state is out of date." }); return; }
        shotPending = true;
        commands.Enqueue(new OnlineShot(peer.Player.Id, shotId, revision, angle, strength));
    }

    private void BroadcastLobbyLocked()
    {
        var players = peers.Where(p => p.Player is not null).Select(p => p.Player!).ToArray();
        var hostConnected = firstPlayerId is not null && peers.Any(p => p.Player?.Id == firstPlayerId && p.Socket.State == WebSocketState.Open);
        BroadcastLocked(new { type = "lobby", players, started, hostPlayerId = firstPlayerId, hostPlayerName = firstPlayerName, hostConnected });
    }

    private void BroadcastLocked(object message)
    {
        var json = JsonSerializer.Serialize(message, JsonOptions);
        foreach (var peer in peers) peer.Outgoing.Writer.TryWrite(json);
    }

    private Peer? FindPeer(string playerId) => peers.FirstOrDefault(p => p.Player?.Id == playerId);

    private static bool TryInput(JsonElement json, out int revision, out int angle, out int strength)
    {
        revision = angle = strength = 0;
        return json.TryGetProperty("revision", out var r) && r.TryGetInt32(out revision) &&
            json.TryGetProperty("angleDegrees", out var a) && a.TryGetInt32(out angle) && angle is >= 0 and <= 359 &&
            json.TryGetProperty("strengthPercent", out var s) && s.TryGetInt32(out strength) && strength is >= 1 and <= 100;
    }

    private static string ReadString(JsonElement json, string name, int maxLength)
    {
        if (!json.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) return "";
        var result = value.GetString() ?? "";
        return result.Length <= maxLength ? result : result[..maxLength];
    }

    private static async Task SendLoopAsync(Peer peer, CancellationToken cancellationToken)
    {
        await foreach (var text in peer.Outgoing.Reader.ReadAllAsync(cancellationToken))
        {
            if (peer.Socket.State != WebSocketState.Open) break;
            var bytes = Encoding.UTF8.GetBytes(text);
            await peer.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }
    }

    private string GetLanAddress()
    {
        try
        {
            var address = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(u => u.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
            return address?.ToString() ?? "127.0.0.1";
        }
        catch { return "127.0.0.1"; }
    }

    private sealed class Peer(WebSocket socket)
    {
        public WebSocket Socket { get; } = socket;
        public OnlineParticipant? Player { get; set; }
        public long LastAimAt { get; set; }
        public Channel<string> Outgoing { get; } = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        public void Enqueue(object message) => Outgoing.Writer.TryWrite(JsonSerializer.Serialize(message, JsonOptions));
    }
}
