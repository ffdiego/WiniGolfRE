using Winigolf.Physics;
using V3 = Winigolf.Physics.Vector3;

namespace Winigolf.Game;

internal sealed class GameSession(Course[] courses)
{
    public Course[] Courses { get; } = courses;
    public int Index { get; private set; }
    public Course Course => Courses[Index];
    public CollisionPolicy Policy { get; private set; } = CollisionPolicy.SurfaceTraversal;
    public ShotResult? Shot { get; private set; }
    public double Time { get; private set; }
    public bool Paused { get; set; }
    public Dictionary<int, int> Scores { get; } = [];
    private int priorStrokes;
    public int FrameIndex => Shot is null ? 0 : Math.Min(Shot.Steps, (int)Math.Floor((Time + 1e-10) / Simulator.StepSeconds));
    public BallState Ball => Shot?.Frames[FrameIndex].State ?? Simulator.AtStart(Course, default);
    public bool AtEnd => Shot is not null && FrameIndex == Shot.Steps;
    public EndReason? Outcome => AtEnd ? Shot!.Reason : Shot is null ? null : EndReason.Moving;
    public int Strokes => Shot is null ? 0 : AtEnd ? Shot.StrokeCount : priorStrokes + 1;
    public double CaptureProgress => Outcome == EndReason.Captured ? Math.Clamp((Time - Shot!.Steps * .02) / .65, 0, 1) : 0;
    public bool CanShoot => Shot is null || AtEnd && (Outcome is EndReason.Stopped or EndReason.BounceLimitReset)
        && Strokes < 7 && Course.Contains(Ball.SurfaceId, Ball.Position);
    public bool Completed => Outcome is EndReason.Captured or EndReason.StrokeLimitReached || AtEnd && Strokes >= 7;

    public void Select(int index)
    {
        Index = (index % Courses.Length + Courses.Length) % Courses.Length;
        Shot = null; Time = 0; priorStrokes = 0; Paused = false;
    }

    public void SetPolicy(CollisionPolicy policy)
    {
        Policy = policy; Select(Index);
        // Scores obtained with different physics must never be mixed.
        Scores.Clear();
    }

    public void Shoot(V3 velocity)
    {
        if (!CanShoot) throw new InvalidOperationException("A bola não está pronta para uma tacada.");
        var count = Strokes;
        var initial = Ball with { Velocity = velocity, BounceCount = 0 };
        var result = Simulator.Run(Course, initial, 6000, count, Policy);
        Shot = result; priorStrokes = count; Time = 0; Paused = false;
    }

    public void Advance(double elapsed)
    {
        if (!double.IsFinite(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (Shot is null || Paused) return;
        Time = Math.Min(Shot.Steps * .02 + 1, Time + elapsed);
        if (Completed) Scores[Course.Id] = Strokes;
    }

    // Smooth presentation only: authoritative frames, surface IDs and velocities are untouched.
    public V3 VisualPosition
    {
        get
        {
            var current = Ball;
            if (Shot is null) return current.Position;
            if (AtEnd)
            {
                var t = Math.Clamp(CaptureProgress / .35, 0, 1);
                return Lerp(current.Position, Course.Target, t);
            }
            var next = Shot.Frames[FrameIndex + 1].State;
            var alpha = Math.Clamp(Time / .02 - FrameIndex, 0, 1);
            var p = Lerp(current.Position, next.Position, alpha);
            // Rest the drawn sphere on the ramp, rather than displaying the legacy z lag.
            var support = Course.Contains(current.SurfaceId, p) ? current.SurfaceId : next.SurfaceId;
            return p with { Z = Course.GetSurface(support).Height(p.X, p.Y) };
        }
    }

    private static V3 Lerp(V3 a, V3 b, double t) => new(a.X + (b.X - a.X) * t,
        a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
}
