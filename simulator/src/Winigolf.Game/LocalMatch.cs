using Raylib_cs;
using Winigolf.Physics;

namespace Winigolf.Game;

internal sealed record LocalPlayer(string Name, Color Color, GameSession Session)
{
    public int Total => Session.Scores.Values.Sum() + (Session.Scores.ContainsKey(Session.Course.Id) ? 0 : Session.Strokes);
}

internal sealed class LocalMatch
{
    public static readonly Color[] Palette = [new(245, 211, 98, 255), new(112, 192, 255, 255),
        new(249, 139, 151, 255), new(164, 225, 154, 255), new(195, 159, 246, 255), new(255, 173, 108, 255)];
    public IReadOnlyList<LocalPlayer> Players { get; }
    public int ActiveIndex { get; private set; }
    public LocalPlayer Active => Players[ActiveIndex];
    public bool CourseCompleted => Players.All(p => p.Session.Completed);
    private ShotResult? observedShot;
    private bool pending;
    private double endWaitElapsed;
    private readonly GameConfig config;
    public bool WaitingToPass => pending && Players.Count > 1 && Active.Session.AtEnd && !CourseCompleted &&
        Players.Where((_, i) => i != ActiveIndex).Any(p => !p.Session.Completed);

    public LocalMatch(Course[] courses, IEnumerable<string> names, GameConfig? config = null)
    {
        this.config = config ?? new GameConfig(); this.config.Validate();
        var entries = names.Select(n => n.Trim()).ToArray();
        if (entries.Length is < 1 or > 6 || entries.Any(string.IsNullOrWhiteSpace) || entries.Any(n => n.Length > 18))
            throw new ArgumentException("Informe de um a seis nomes com até 18 caracteres.");
        Players = entries.Select((name, i) => new LocalPlayer(name, Palette[i], new GameSession(courses))).ToArray();
    }

    public void Select(int index)
    {
        foreach (var player in Players)
        {
            player.Session.Select(index);
            player.Session.Scores.Remove(player.Session.Course.Id);
        }
        ActiveIndex = 0; observedShot = null; pending = false; endWaitElapsed = 0;
    }

    public void SetPolicy(CollisionPolicy policy)
    {
        foreach (var player in Players) player.Session.SetPolicy(policy);
        ActiveIndex = 0; observedShot = null; pending = false; endWaitElapsed = 0;
    }

    public void Advance(double elapsed)
    {
        var session = Active.Session;
        if (!ReferenceEquals(observedShot, session.Shot))
        { observedShot = session.Shot; pending = session.Shot is not null; endWaitElapsed = 0; }
        session.Advance(elapsed);
        if (!pending || session.Paused || !session.AtEnd || Players.Count == 1 || CourseCompleted) return;
        endWaitElapsed += elapsed;
        var endDelay = session.Outcome == EndReason.Captured
            ? config.DelayAfterCapture(session.Strokes, session.Course.Par) : .5;
        if (endWaitElapsed < endDelay) return;
        for (var offset = 1; offset <= Players.Count; offset++)
        {
            var next = (ActiveIndex + offset) % Players.Count;
            if (Players[next].Session.Completed) continue;
            ActiveIndex = next; observedShot = Active.Session.Shot; pending = false; endWaitElapsed = 0;
            return;
        }
    }
}
