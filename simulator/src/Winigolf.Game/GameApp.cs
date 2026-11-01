using System.Numerics;
using Raylib_cs;
using Winigolf.Physics;
using static Raylib_cs.Raylib;

namespace Winigolf.Game;

internal sealed partial class GameApp : IDisposable
{
    public LocalMatch Match { get; private set; }
    public GameSession Session => Match.Active.Session;
    private bool setup;
    private readonly List<string> playerNames = ["Jogador 1", "Jogador 2"];
    private int editingName;
    public SceneCamera Camera { get; } = new() { Yaw = -.35f };
    public CourseScene Scene { get; } = new();
    public GameAudio Audio { get; }
    private readonly TurnPresentation turns = new(Random.Shared);
    public double PrecisionGain { get; private set; } = 1;
    private RenderTexture2D canvas;
    private Font font;
    private bool ownsFont;
    private bool picker;
    private bool aiming;
    private Vector2 anchor, endpoint;
    private MouseButton aimButton;
    private ShotMode aimMode;
    private Rectangle viewport;
    private bool disposed;
    private readonly Color background = new(14, 25, 34, 255);
    private readonly Color panel = new(24, 41, 51, 255);
    private readonly Color muted = new(146, 173, 181, 255);
    private readonly Color mint = new(142, 229, 190, 255);
    private readonly Color white = new(238, 244, 237, 255);
    private readonly OnlineHost? onlineHost;
    private readonly GameConfig config;
    private readonly List<string> onlinePlayerIds = [];
    private string? onlineQrUrl;
    private QrCodeSymbol? onlineQr;
    private OnlineAim? remoteAim;
    private bool remoteAimDisplayInitialized;
    private double remoteAimDisplayAngle;
    private double remoteAimDisplayStrength;
    private bool onlineStarted;
    private bool onlineSnapshotInitialized;
    private int onlineRevision;
    private int publishedActiveIndex = -1;
    private ShotResult? publishedShot;
    private bool publishedAtEnd;
    private bool publishedReady;
    private double courseAdvanceElapsed;
    private bool courseAdvanceTimerStarted;
    private double CurrentCourseAdvanceDelay => Session.Outcome == EndReason.Captured
        ? config.DelayAfterCapture(Session.Strokes, Session.Course.Par)
        : config.CourseAdvanceDelaySeconds;

    public GameApp(Course[] courses, bool hidden, CameraOptions? cameraOptions = null, OnlineHost? onlineHost = null,
        GameConfig? config = null)
    {
        this.onlineHost = onlineHost;
        this.config = config ?? new GameConfig(); this.config.Validate();
        var selectedCameraOptions = cameraOptions ?? new CameraOptions();
        Camera = this.config.DynamicBallAndHoleFraming
            ? new DynamicFramingCamera { Yaw = -.35f, Options = selectedCameraOptions,
                RightOverlayWidth = 310, TopOverlayHeight = 128, BottomOverlayHeight = 90 }
            : new SceneCamera { Yaw = -.35f, Options = selectedCameraOptions,
                RightOverlayWidth = 310, TopOverlayHeight = 128, BottomOverlayHeight = 90 };
        SetTraceLogLevel(TraceLogLevel.Warning);
        SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint |
            (hidden ? ConfigFlags.HiddenWindow : 0));
        InitWindow(1280, 820, "WiniGolf · primeira versão jogável");
        if (!IsWindowReady()) throw new InvalidOperationException("Não foi possível inicializar o contexto gráfico raylib.");
        GolfBallIcon.ApplyToWindow();
        // Coordinates retain the original game's units; some fields exceed the default far plane.
        Rlgl.SetClipPlanes(1, 20000);
        SetWindowMinSize(1024, 720); SetExitKey(KeyboardKey.Null); SetTargetFPS(hidden ? 0 : 120);
        Match = new LocalMatch(courses, ["Jogador 1"], this.config);
        setup = !hidden && onlineHost is null;
        Audio = new GameAudio(silent: hidden);
        var fontFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeuib.ttf");
        if (File.Exists(fontFile))
        {
            var glyphs = Enumerable.Range(32, 95).Concat(Enumerable.Range(160, 96)).ToArray();
            font = LoadFontEx(fontFile, 64, glyphs, glyphs.Length);
            ownsFont = true; SetTextureFilter(font.Texture, TextureFilter.Bilinear);
        }
        else font = GetFontDefault();
        Scene.Build(Session.Course); Resize();
        Camera.Focus = Camera.Options.CenterOnBallAtTurnStart ? SceneCamera.World(Session.VisualPosition) + System.Numerics.Vector3.UnitY * 4 : null;
    }

    public void Select(int index)
    {
        courseAdvanceElapsed = 0; courseAdvanceTimerStarted = false;
        CancelAim(); Match.Select(index); Scene.Build(Session.Course); Camera.Zoom = 1; picker = false;
        turns.Reset(Camera);
        Camera.Focus = Camera.Options.CenterOnBallAtTurnStart ? SceneCamera.World(Session.VisualPosition) + System.Numerics.Vector3.UnitY * 4 : null;
        Audio.Update(Session);
        Camera.Fit(Session.Course, (int)viewport.Width, (int)viewport.Height);
    }

    private void Resize()
    {
        var width = Math.Max(1, GetScreenWidth());
        var height = Math.Max(1, GetScreenHeight());
        if (canvas.Id != 0 && canvas.Texture.Width == width * 2 && canvas.Texture.Height == height * 2) return;
        CancelAim();
        if (canvas.Id != 0) UnloadRenderTexture(canvas);
        // Supersampling also smooths polygon edges inside the offscreen viewport.
        canvas = LoadRenderTexture(width * 2, height * 2);
        if (canvas.Id == 0) throw new InvalidOperationException("Falha ao criar a superfície de renderização.");
        SetTextureFilter(canvas.Texture, TextureFilter.Bilinear);
        viewport = new Rectangle(0, 0, width, height);
    }

    private Vector2 LocalMouse => GetMousePosition() - new Vector2(viewport.X, viewport.Y);

    public bool StartAim(Vector2 point, MouseButton button)
    {
        if (setup || Match.WaitingToPass || !Session.CanShoot || Camera.IsTurning || aiming || button is not (MouseButton.Left or MouseButton.Right)) return false;
        var projected = Camera.ProjectBallCenter(Session.VisualPosition);
        if (Vector2.DistanceSquared(point, projected) > 22 * 22) return false;
        aimButton = button; aimMode = button == MouseButton.Left ? ShotMode.Mode1 : ShotMode.Mode2;
        Camera.Zoom = Camera.Zoom; // Freeze scale while dragging the shot.
        anchor = endpoint = projected; aiming = true; return true;
    }

    public bool FinishAim(Vector2 point, MouseButton button)
    {
        if (!aiming || button != aimButton) return false;
        endpoint = point; aiming = false;
        var impulse = Camera.Impulse(endpoint - anchor, Session.Ball.Position, aimMode);
        if (aimMode == ShotMode.Mode1)
            impulse = impulse with { X = impulse.X * PrecisionGain, Y = impulse.Y * PrecisionGain };
        if (impulse.X == 0 && impulse.Y == 0) return false;
        Session.Shoot(impulse); Audio.Update(Session); return true;
    }

    public void CancelAim() => aiming = false;

    public void SetPrecisionGain(int gain)
    {
        if (gain is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(gain));
        CancelAim(); PrecisionGain = gain;
    }

    private void Input(double dt)
    {
        if (IsWindowResized()) Resize();
        if (onlineHost is not null) { Camera.Fit(Session.Course, (int)viewport.Width, (int)viewport.Height); return; }
        if (setup) { SetupInput(); return; }
        if (!IsWindowFocused()) CancelAim();
        if (IsKeyPressed(KeyboardKey.Escape)) { CancelAim(); picker = false; }
        if (IsKeyPressed(KeyboardKey.N) && Session.Outcome != EndReason.Moving) { OpenPlayerSetup(); return; }
        if (IsKeyPressed(KeyboardKey.M)) Audio.ToggleMute();
        if (IsKeyPressed(KeyboardKey.Tab)) { CancelAim(); picker = !picker; }
        if (picker) return;
        if (IsKeyPressed(KeyboardKey.R)) Select(Session.Index);
        if (IsKeyPressed(KeyboardKey.Space)) { CancelAim(); Session.Paused = !Session.Paused; }
        if (Camera.Options.AllowTopDown && IsKeyPressed(KeyboardKey.V)) { CancelAim(); Camera.CancelTurn(); Camera.TopDown = !Camera.TopDown; }
        if (IsKeyPressed(KeyboardKey.F2))
        {
            CancelAim(); Match.SetPolicy(Session.Policy == CollisionPolicy.SurfaceTraversal
                ? CollisionPolicy.ReferenceOrder : CollisionPolicy.SurfaceTraversal);
        }
        if (Camera.Options.AllowManualRotation && !Camera.TopDown && !Camera.IsTurning && (IsKeyDown(KeyboardKey.Q) || IsKeyDown(KeyboardKey.E)))
        {
            CancelAim(); Camera.Yaw += (IsKeyDown(KeyboardKey.Q) ? -1 : 1) * (float)dt;
        }
        var over = CheckCollisionPointRec(GetMousePosition(), viewport) && !OverHud(GetMousePosition());
        if (over && !aiming && !Camera.IsTurning)
        {
            var wheel = GetMouseWheelMove();
            Camera.RequestZoom(wheel);
            if (Camera.Options.AllowManualRotation && IsMouseButtonDown(MouseButton.Middle) && !Camera.TopDown) Camera.Yaw += GetMouseDelta().X * .007f;
        }
        Camera.Fit(Session.Course, (int)viewport.Width, (int)viewport.Height);
        if (over && IsMouseButtonPressed(MouseButton.Left)) StartAim(LocalMouse, MouseButton.Left);
        if (over && IsMouseButtonPressed(MouseButton.Right)) StartAim(LocalMouse, MouseButton.Right);
        if (aiming)
        {
            endpoint = LocalMouse;
            if (IsMouseButtonReleased(aimButton)) FinishAim(endpoint, aimButton);
        }
    }

    public void Run()
    {
        AdvancePresentation(0);
        while (!WindowShouldClose())
        {
            var dt = (double)GetFrameTime();
            if (onlineHost is not null) ProcessOnlineCommands();
            Input(dt);
            if (!setup)
            {
                var active = Match.ActiveIndex; Match.Advance(dt);
                if (active != Match.ActiveIndex) { CancelAim(); turns.Reset(Camera); }
                AdvancePresentation(dt); AdvanceRemoteAim(dt); Audio.Update(Session);
                AdvanceCourseTransition(dt);
                if (onlineStarted) UpdateOnlineSnapshot();
            }
            Draw(acceptUi: onlineHost is null);
        }
    }

    private void AdvanceCourseTransition(double elapsed)
    {
        if (!Match.CourseCompleted)
        {
            courseAdvanceElapsed = 0; courseAdvanceTimerStarted = false;
            return;
        }
        if (Session.Paused || !Session.AtEnd) return;

        // The final player's capture delay is the same one used between players,
        // including the longer below-par applause. Non-capture completion uses its own delay.
        var delay = Session.Outcome == EndReason.Captured
            ? config.DelayAfterCapture(Session.Strokes, Session.Course.Par)
            : config.CourseAdvanceDelaySeconds;
        if (!courseAdvanceTimerStarted) courseAdvanceTimerStarted = true;
        else courseAdvanceElapsed += elapsed;
        if (courseAdvanceElapsed < delay) return;

        var nextCourse = Session.Index + 1;
        Select(nextCourse);
        AdvancePresentation(0);
    }

    private void ProcessOnlineCommands()
    {
        if (onlineHost is null) return;
        while (onlineHost.TryDequeue(out var command))
        {
            switch (command)
            {
                case OnlineStart start when !onlineStarted:
                    ConfigurePlayers(start.Players.Select(p => p.Name));
                    onlinePlayerIds.Clear(); onlinePlayerIds.AddRange(start.Players.Select(p => p.Id));
                    onlineStarted = true; onlineRevision = 0; onlineSnapshotInitialized = false;
                    remoteAim = null; remoteAimDisplayInitialized = false; turns.Reset(Camera); AdvancePresentation(0);
                    UpdateOnlineSnapshot(force: true);
                    break;
                case OnlineAim aim when onlineStarted && Match.ActiveIndex < onlinePlayerIds.Count && onlinePlayerIds[Match.ActiveIndex] == aim.PlayerId:
                    if (!remoteAimDisplayInitialized || remoteAim?.PlayerId != aim.PlayerId)
                    {
                        remoteAimDisplayAngle = aim.AngleDegrees;
                        remoteAimDisplayStrength = aim.StrengthPercent;
                        remoteAimDisplayInitialized = true;
                    }
                    remoteAim = aim;
                    break;
                case OnlineShot shot:
                    ProcessOnlineShot(shot);
                    break;
            }
        }
    }

    private void ProcessOnlineShot(OnlineShot shot)
    {
        if (onlineHost is null || !onlineStarted || shot.Revision != onlineRevision ||
            Match.ActiveIndex >= onlinePlayerIds.Count || onlinePlayerIds[Match.ActiveIndex] != shot.PlayerId ||
            !Session.CanShoot || Camera.IsTurning)
        {
            onlineHost?.RejectShot(shot.PlayerId, shot.ShotId, "It is not your turn or the game is not ready.");
            return;
        }
        try
        {
            var drag = Camera.DragForAim(shot.AngleDegrees, shot.StrengthPercent, Session.Ball.Position);
            var impulse = Camera.Impulse(drag, Session.Ball.Position, ShotMode.Mode1);
            if (impulse.X == 0 && impulse.Y == 0) throw new InvalidOperationException("The shot input produced no impulse.");
            Session.Shoot(impulse); Audio.Update(Session); remoteAim = null; remoteAimDisplayInitialized = false;
            var result = Session.Shot!;
            onlineHost.ConfirmShot(shot.PlayerId, shot.ShotId, shot.AngleDegrees, shot.StrengthPercent,
                result.Steps, result.Reason.ToString(), result.FinalState.Position.X, result.FinalState.Position.Y,
                result.FinalState.Position.Z, result.FinalState.SurfaceId, result.StrokeCount, result.Penalty);
            UpdateOnlineSnapshot(force: true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            onlineHost.RejectShot(shot.PlayerId, shot.ShotId, ex.Message);
        }
    }

    private void UpdateOnlineSnapshot(bool force = false)
    {
        if (onlineHost is null || !onlineStarted || onlinePlayerIds.Count != Match.Players.Count) return;
        var active = Match.ActiveIndex;
        var shot = Session.Shot;
        var atEnd = Session.AtEnd;
        var ready = Session.CanShoot && !Camera.IsTurning && !Match.WaitingToPass;
        if (!force && onlineSnapshotInitialized && active == publishedActiveIndex &&
            ReferenceEquals(shot, publishedShot) && atEnd == publishedAtEnd && ready == publishedReady) return;
        onlineRevision++;
        var players = Match.Players.Select((p, i) =>
        {
            var ball = p.Session.Ball;
            return new OnlinePlayerState(onlinePlayerIds[i], p.Name, onlineHostColor(i), ball.Position.X, ball.Position.Y,
                ball.Position.Z, ball.SurfaceId, p.Session.Strokes, p.Total, p.Session.Completed);
        }).ToArray();
        onlineHost.PublishSnapshot(new OnlineMatchSnapshot(onlineRevision, Session.Course.Id, Session.Course.Name,
            Session.Course.DataHash, Simulator.VersionFor(Session.Policy), active, onlinePlayerIds[active], ready,
            Camera.Yaw * 180 / Math.PI, players));
        onlineSnapshotInitialized = true; publishedActiveIndex = active; publishedShot = shot;
        publishedAtEnd = atEnd; publishedReady = ready;
    }

    private static string onlineHostColor(int seat) => seat switch
    {
        0 => "#F5D362", 1 => "#70C0FF", 2 => "#F98B97", 3 => "#A4E19A", 4 => "#C39FF6", _ => "#FFAD6C"
    };

    public void AdvancePresentation(double elapsed)
    {
        if (!Match.WaitingToPass) turns.Update(Session, Camera, elapsed);
        if (!aiming) Camera.AdvanceZoom(elapsed);
    }

    private void AdvanceRemoteAim(double elapsed)
    {
        if (remoteAim is not { } target || !remoteAimDisplayInitialized || elapsed <= 0) return;
        var amount = 1 - Math.Exp(-config.RemoteAimSmoothingPerSecond * elapsed);
        var angleDelta = (target.AngleDegrees - remoteAimDisplayAngle + 540) % 360 - 180;
        remoteAimDisplayAngle = (remoteAimDisplayAngle + angleDelta * amount + 360) % 360;
        remoteAimDisplayStrength += (target.StrengthPercent - remoteAimDisplayStrength) * amount;
    }

    private void Text(string text, float x, float y, float size, Color color) => DrawTextEx(font, text, new Vector2(x, y), size, .3f, color);
    private void Box(Rectangle rect, Color color, float round = .12f) => DrawRectangleRounded(rect, round, 12, color);

    private bool Button(string label, Rectangle rect, bool active = false, bool enabled = true, bool modal = false)
    {
        var hover = enabled && (!picker || modal) && (!setup || modal) && CheckCollisionPointRec(GetMousePosition(), rect);
        Box(rect, active ? mint : hover ? new Color(49, 76, 85, 255) : new Color(33, 53, 64, 255));
        var size = 20f;
        while (size > 15 && MeasureTextEx(font, label, size, .3f).X > rect.Width - 20) size -= 1;
        var measured = MeasureTextEx(font, label, size, .3f);
        Text(label, rect.X + (rect.Width - measured.X) / 2, rect.Y + (rect.Height - size) / 2 - 1, size,
            !enabled ? muted : active ? background : white);
        return enabled && hover && IsMouseButtonPressed(MouseButton.Left);
    }

    public void Draw(bool acceptUi = true)
    {
        Resize(); Camera.Fit(Session.Course, (int)viewport.Width, (int)viewport.Height);
        BeginTextureMode(canvas); ClearBackground(background); Scene.Draw(Session, Camera, Match.Players); EndTextureMode();
        BeginDrawing(); ClearBackground(background);
        DrawTexturePro(canvas.Texture, new Rectangle(0, 0, canvas.Texture.Width, -canvas.Texture.Height), viewport, Vector2.Zero, 0, Color.White);
        DrawGhostNameLabels();
        Box(new Rectangle(16, 12, 410, 116), new Color(14, 25, 34, 220));
        Box(new Rectangle(16, GetScreenHeight() - 82, Math.Min(850, GetScreenWidth() - 32), 72), new Color(14, 25, 34, 220));
        Text("wini", 27, 18, 40, white); Text("golf", 112, 18, 40, mint);
        var course = Session.Course;
        Text($"{course.Id:00}  /  {course.Name}", 28, 91, 28, white);
        if (Button("Todas as pistas  [Tab]", new Rectangle(GetScreenWidth() - 294, 24, 270, 42)) && acceptUi) { CancelAim(); picker = true; }
        if (aiming)
        {
            var ball = Camera.ProjectBallCenter(Session.VisualPosition) + new Vector2(viewport.X, viewport.Y);
            var delta = endpoint - anchor;
            var limit = Camera.DragLimit(Session.Ball.Position, aimMode);
            var pull = delta.Length() > limit ? Vector2.Normalize(delta) * limit : delta;
            var color = Match.Active.Color;
            DrawLineEx(ball, ball + pull, aimMode == ShotMode.Mode1 ? 1.5f : 4, color);
            DrawCircleV(ball + pull, 4, color);
            var direction = Camera.Impulse(delta, Session.Ball.Position, aimMode);
            var angle = (int)Math.Round((Math.Atan2(direction.Y, direction.X) * 180 / Math.PI + 180 + 360) % 360,
                MidpointRounding.AwayFromZero) % 360;
            var force = Math.Clamp((int)Math.Round(delta.Length() / limit * 100, MidpointRounding.AwayFromZero), 0, 100);
            Text($"ÂNGULO {angle:000}°   ·   FORÇA {force:00}%", 28, GetScreenHeight() - 101, 20, color);
        }
        if (onlineStarted && remoteAim is { } remote && Match.ActiveIndex < onlinePlayerIds.Count &&
            remote.PlayerId == onlinePlayerIds[Match.ActiveIndex])
        {
            var ball = Camera.ProjectBallCenter(Session.VisualPosition) + new Vector2(viewport.X, viewport.Y);
            var pull = Camera.DragForAim(remoteAimDisplayAngle, remoteAimDisplayStrength, Session.Ball.Position);
            var color = Match.Active.Color;
            DrawLineEx(ball, ball + pull, 2.5f, color); DrawCircleV(ball + pull, 5, color);
            Text($"MIRA {(int)Math.Round(remoteAimDisplayAngle) % 360:000}°  ·  FORÇA {(int)Math.Round(remoteAimDisplayStrength):00}%", 28,
                GetScreenHeight() - 101, 20, color);
        }

        var sideX = GetScreenWidth() - 294;
        Box(new Rectangle(sideX, 86, 270, 260 + Match.Players.Count * 36), new Color(24, 41, 51, 225));
        DrawScoreboard(sideX);
        var message = Session.Outcome switch
        {
            EndReason.Captured => "EMBOCOU!",
            EndReason.StrokeLimitReached => "CAMPO CONCLUÍDO · LIMITE DE TACADAS",
            EndReason.Moving => Session.Paused ? "PAUSADO" : "BOLA EM JOGO",
            EndReason.GeometryResolutionFailed => "A SIMULAÇÃO NÃO RESOLVEU A GEOMETRIA. REINICIE O CAMPO.",
            EndReason.SimulationLimitReached => "LIMITE DA SIMULAÇÃO ATINGIDO. REINICIE O CAMPO.",
            _ when Session.Completed => "CAMPO CONCLUÍDO · LIMITE DE TACADAS",
            _ => Session.CanShoot ? Camera.IsTurning ? "PREPARANDO A TACADA…" : "ESCOLHA A DIREÇÃO." : "REINICIE O CAMPO PARA CONTINUAR."
        };
        if (Match.WaitingToPass) message = "PASSANDO A VEZ…";
        if (Match.CourseCompleted)
        {
            message = courseAdvanceTimerStarted
                ? $"PRÓXIMA PISTA EM {Math.Max(0, CurrentCourseAdvanceDelay - courseAdvanceElapsed):0.0} s"
                : Session.Outcome == EndReason.Captured ? "AGUARDE A BOLA AFUNDAR…" : "PISTA CONCLUÍDA!";
        }
        Text(message, 28, GetScreenHeight() - 66, 21, Session.Completed ? mint : white);
        Text(onlineHost is null ? "Puxe no sentido oposto à tacada  ·  Esc cancela  ·  Espaço pausa" :
            "Mire e confirme pelo celular  ·  a tela grande mostra a tacada", 28, GetScreenHeight() - 32, 18, muted);
        if (picker) DrawPicker(acceptUi);
        if (setup) DrawSetup(acceptUi);
        if (onlineHost is not null) DrawOnlineOverlay();
        EndDrawing();
    }

    private void DrawGhostNameLabels()
    {
        var placed = new List<Rectangle>();
        var protectedAreas = Match.Players
            .Where(p => p.Session.Outcome != EndReason.Captured)
            .Select(p => ScreenCircle(Camera.ProjectBallCenter(p.Session.VisualPosition), CaptureMotion.BallRadius, 5))
            .Append(ScreenCircle(Camera.Project(Session.Course.Target), 12, 5))
            .ToArray();
        var ghostPlayers = Match.Players.Where(p => !ReferenceEquals(p.Session, Session) &&
                p.Session.Outcome != EndReason.Captured && p.Session.Strokes > 0)
            .OrderByDescending(p => System.Numerics.Vector3.DistanceSquared(
                SceneCamera.World(p.Session.VisualPosition), Camera.Value.Position));
        foreach (var player in ghostPlayers)
        {
            var point = Camera.ProjectBallCenter(player.Session.VisualPosition);
            const float fontSize = 17;
            var label = ShortName(player.Name, 170, fontSize);
            var measured = MeasureTextEx(font, label, fontSize, .3f);
            var width = measured.X + 32; const float height = 27;
            var x = Math.Clamp(point.X - width / 2, 8, GetScreenWidth() - width - 8);
            var baseY = point.Y - 32;
            var color = player.Color; color.A = 225;
            var stackStep = height + 3;
            var maxRows = Math.Max(1, (int)(GetScreenHeight() / stackStep));
            for (var attempt = 0; attempt < maxRows; attempt++)
            {
                // Keep the first label above its ball. When labels collide, stack the next
                // one below; search above as a fallback near the bottom UI or screen edge.
                var row = attempt == 0 ? 0 : (attempt % 2 == 1 ? (attempt + 1) / 2 : -(attempt / 2));
                var rect = new Rectangle(x, baseY + row * stackStep, width, height);
                if (rect.Y < 4 || rect.Y + rect.Height > GetScreenHeight() - 4 ||
                    Overlaps(rect, new Rectangle(16, 12, 410, 116)) ||
                    Overlaps(rect, new Rectangle(GetScreenWidth() - 294, 86, 270, 260 + Match.Players.Count * 36)) ||
                    Overlaps(rect, new Rectangle(16, GetScreenHeight() - 82, Math.Min(850, GetScreenWidth() - 32), 72)) ||
                    protectedAreas.Any(area => Overlaps(rect, area)) ||
                    placed.Any(other => Overlaps(rect, other))) continue;
                var plate = new Color(9, 17, 22, 175);
                Box(rect, plate, .18f);
                DrawCircleV(new Vector2(rect.X + 12, rect.Y + rect.Height / 2), 4, color);
                Text(label, rect.X + 22, rect.Y + 4, fontSize, color);
                placed.Add(rect);
                break;
            }
        }
    }

    private Rectangle ScreenCircle(Vector2 center, float worldRadius, float padding)
    {
        var radius = worldRadius * GetScreenHeight() / Math.Max(1, Camera.Value.FovY) + padding;
        return new Rectangle(center.X - radius, center.Y - radius, radius * 2, radius * 2);
    }

    private static bool Overlaps(Rectangle a, Rectangle b) =>
        a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;

    private void DrawOnlineOverlay()
    {
        if (onlineStarted || onlineHost is null) return;
        if (onlineQrUrl != onlineHost.JoinUrl)
        {
            onlineQrUrl = onlineHost.JoinUrl;
            onlineQr = QrCode.Encode(onlineQrUrl);
        }
        var width = Math.Min(1000, GetScreenWidth() - 48);
        var height = Math.Min(500, GetScreenHeight() - 48);
        var left = (GetScreenWidth() - width) / 2f; var top = (GetScreenHeight() - height) / 2f;
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new Color(5, 12, 19, 218));
        Box(new Rectangle(left, top, width, height), panel, .05f);
        Text("SALA ONLINE", left + 28, top + 24, 34, white);
        Text("Escaneie o QR code com o celular para entrar na sala:", left + 28, top + 78, 20, muted);
        Text("OU ACESSE ESTE ENDEREÇO", left + 28, top + 126, 16, muted);
        Text(onlineHost.JoinUrl, left + 28, top + 152, 24, mint);
        Text($"JOGADORES  {onlineHost.ConnectedCount} / 6", left + 28, top + 218, 19, white);
        var names = onlineHost.LobbyNames;
        if (names.Length == 0) Text("Aguardando jogadores…", left + 28, top + 250, 19, muted);
        else
            for (var i = 0; i < names.Length; i++) Text(names[i], left + 28, top + 248 + i * 24, 17, muted);
        Text("O primeiro jogador a entrar poderá iniciar a partida pelo celular.", left + 28,
            top + Math.Max(292, 260 + names.Length * 24), 16, white);
        var qrSize = Math.Min(328, (int)height - 96);
        var qrBounds = new Rectangle(left + width - qrSize - 26, top + (height - qrSize) / 2, qrSize, qrSize);
        DrawQrCode(onlineQr!, qrBounds);
    }

    private static void DrawQrCode(QrCodeSymbol qr, Rectangle bounds)
    {
        var tile = new Color(238, 244, 237, 255);
        var ink = new Color(14, 25, 34, 255);
        DrawRectangleRounded(bounds, .06f, 12, tile);
        var quietZone = 4;
        var total = qr.Modules.GetLength(0) + quietZone * 2;
        var moduleSize = Math.Min(bounds.Width, bounds.Height) / total;
        for (var y = 0; y < qr.Modules.GetLength(0); y++)
        for (var x = 0; x < qr.Modules.GetLength(1); x++)
        {
            if (!qr.Modules[y, x]) continue;
            var left = bounds.X + (x + quietZone) * moduleSize;
            var top = bounds.Y + (y + quietZone) * moduleSize;
            var right = bounds.X + (x + quietZone + 1) * moduleSize;
            var bottom = bounds.Y + (y + quietZone + 1) * moduleSize;
            if (qr.FunctionModules[y, x])
            {
                DrawRectangle((int)MathF.Floor(left), (int)MathF.Floor(top),
                    (int)MathF.Ceiling(right) - (int)MathF.Floor(left),
                    (int)MathF.Ceiling(bottom) - (int)MathF.Floor(top), ink);
                continue;
            }

            var inset = moduleSize * .1f;
            DrawRectangleRounded(new Rectangle(left + inset, top + inset,
                right - left - inset * 2, bottom - top - inset * 2), .42f, 4, ink);
        }
    }

    private void DrawPicker(bool acceptUi)
    {
        DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new Color(5, 12, 19, 235));
        var left = (GetScreenWidth() - 900) / 2f; var top = (GetScreenHeight() - 610) / 2f;
        Box(new Rectangle(left, top, 900, 610), panel, .04f);
        Text("Escolha sua próxima pista", left + 25, top + 23, 30, white);
        Text("Resultados desta sessão aparecem ao lado do nome. Tab / Esc para voltar.", left + 25, top + 65, 18, muted);
        for (var i = 0; i < Session.Courses.Length; i++)
        {
            var course = Session.Courses[i];
            var label = $"{course.Id:00}  {course.Name}";
            if (Session.Scores.TryGetValue(course.Id, out var score)) label += $"   / {score}";
            var rect = new Rectangle(left + 25 + i % 4 * 215, top + 105 + i / 4 * 52, 205, 40);
            if (Button(label, rect, Session.Index == i, modal: true) && acceptUi) Select(i);
        }
    }

    public void Capture(string file)
    {
        var image = LoadImageFromScreen();
        try { if (!ExportImage(image, Path.GetFullPath(file))) throw new IOException("Falha ao salvar a captura."); }
        finally { UnloadImage(image); }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (canvas.Id != 0) UnloadRenderTexture(canvas);
        if (ownsFont) UnloadFont(font);
        Audio.Dispose();
        CloseWindow();
    }
}
