using Winigolf.Physics;

namespace Winigolf.Viewer;

// Playback is deliberately separate from physics. All frames come from the shared engine.
internal sealed class LabSession(Course course)
{
    public Course Course { get; private set; } = course;
    public CollisionPolicy Policy { get; private set; }
    public ShotResult? Shot { get; private set; }
    public ShotResult? Comparison { get; private set; }
    public int Cursor { get; private set; }
    public bool Paused { get; private set; }
    private BallState? origin;
    private int priorStrokes;
    private double accumulator;
    public BallState Ball => Shot?.Frames[Cursor].State ?? Simulator.AtStart(Course, default);
    public bool AtEnd => Shot is not null && Cursor == Shot.Frames.Count - 1;
    public int Strokes => Shot is null ? 0 : AtEnd ? Shot.StrokeCount : priorStrokes + 1;
    public bool IsInside => Course.Contains(Ball.SurfaceId, Ball.Position);
    public bool CanShoot => Shot is null || AtEnd && (Shot.Reason is EndReason.Stopped or EndReason.BounceLimitReset) && Strokes < 7 && IsInside;
    public EndReason? Outcome => AtEnd ? Shot!.Reason : Shot is null ? null : EndReason.Moving;

    public void Reset(Course? course = null)
    {
        if (course is not null) Course = course;
        Shot = null; Comparison = null; origin = null; Cursor = 0;
        priorStrokes = 0; accumulator = 0; Paused = false;
    }

    public void Shoot(Vector3 velocity)
    {
        if (!CanShoot) throw new InvalidOperationException("Conclua a reprodução ou reinicie o campo.");
        var strokes = Strokes;
        var start = Ball with { Velocity = velocity, BounceCount = 0 };
        SetShot(start, strokes);
    }

    private void SetShot(BallState start, int strokes)
    {
        // Bound pathological shots; a limit is reported as a diagnostic outcome.
        var shot = Simulator.Run(Course, start, 6000, strokes, Policy);
        origin = start; priorStrokes = strokes; Shot = shot;
        Comparison = null; Replay();
    }

    public void SetPolicy(CollisionPolicy policy)
    {
        if (Policy == policy) return;
        Policy = policy;
        // Compare the SAME initial state and stroke number, even midway through playback.
        if (origin is not null) SetShot(origin, priorStrokes);
    }

    public ShotResult? GetComparison()
    {
        if (origin is null) return null;
        return Comparison ??= Simulator.Run(Course, origin, 6000, priorStrokes,
            Policy == CollisionPolicy.ReferenceOrder ? CollisionPolicy.SurfaceTraversal : CollisionPolicy.ReferenceOrder);
    }

    public void Replay()
    {
        Cursor = 0; accumulator = 0; Paused = false;
    }

    public void TogglePause() { if (Shot is not null) Paused = !Paused; }

    public void Seek(int frame)
    {
        if (Shot is null) return;
        Cursor = Math.Clamp(frame, 0, Shot.Frames.Count - 1);
        accumulator = 0; Paused = true;
    }

    public void StepOne() => Seek(Cursor + 1);

    public void Advance(double elapsed)
    {
        if (!double.IsFinite(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (Paused || Shot is null || AtEnd) return;
        accumulator += elapsed;
        // Small tolerance prevents binary representation from losing a frame at exact boundaries.
        var count = (int)Math.Min(Shot.Frames.Count - 1 - Cursor, Math.Floor((accumulator + 1e-10) / Simulator.StepSeconds));
        Cursor += count; accumulator -= count * Simulator.StepSeconds;
        if (AtEnd) accumulator = 0;
    }

    public void Demo()
    {
        Reset();
        if (Course.Id == 25)
            SetShot(new BallState(new Vector3(0, 201, 10), new Vector3(0, -2, 0), 4), 0);
        else
            Shoot(Course.Id == 5 ? new Vector3(-15, 1.83697019872103e-15, 0) : new Vector3(10, 0, 0));
    }
}
