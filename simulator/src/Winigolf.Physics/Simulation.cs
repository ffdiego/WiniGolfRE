namespace Winigolf.Physics;

public enum ShotMode { Mode1 = 1, Mode2 = 2 }
public enum CollisionPolicy { ReferenceOrder, SurfaceTraversal }
public enum EndReason { Moving, Captured, Stopped, StrokeLimitReached, BounceLimitReset, SimulationLimitReached, GeometryResolutionFailed }
public sealed record BallState(Vector3 Position, Vector3 Velocity, int SurfaceId, int BounceCount = 0);
public sealed record PhysicsEvent(string Kind, int SurfaceId, int Edge, int Neighbor = 0);
public sealed record StepResult(BallState State, IReadOnlyList<PhysicsEvent> Events, bool Reset = false, bool ResolutionFailed = false);
public sealed record Frame(int Step, BallState State, IReadOnlyList<PhysicsEvent> Events);
public sealed record ShotResult(string PhysicsVersion, int CourseId, string CourseName, string CourseDataHash,
    double StepSeconds, EndReason Reason, int Steps, int StrokeCount, int Penalty,
    BallState FinalState, IReadOnlyList<Frame> Frames);

public static class Aim
{
    public const string Version = "drag-x87-corrected-2";
    // Continuous endpoint clamp: intentionally does not reproduce VCL pixel rounding.
    public static Vector3 FromDrag(double dx, double dy, ShotMode mode, double yawDegrees = 0, double tiltDegrees = -60)
    {
        if (!double.IsFinite(dx) || !double.IsFinite(dy) || !double.IsFinite(yawDegrees) || !double.IsFinite(tiltDegrees))
            throw new ArgumentException("Aim must be finite.");
        var (scale, limit) = mode switch
        {
            ShotMode.Mode1 => (0.1, 250.0), ShotMode.Mode2 => (0.8, 50.0),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (!(length > 0) || !double.IsFinite(length)) throw new ArgumentException("Drag must have finite nonzero length.");
        if (length > limit) { dx *= limit / length; dy *= limit / length; length = limit; }
        var cosTilt = Math.Cos(tiltDegrees * Math.PI / 180);
        if (Math.Abs(cosTilt) < 1e-8) throw new ArgumentException("Singular camera tilt.");
        var a = yawDegrees * Math.PI / 180;
        var u = scale * dx; var w = scale * dy / cosTilt;
        // 0041E1DD/0041E202 FMUL; 0041E1EC/0041E211 FMULP.
        // The 80-bit constant CDCCCCCCCCCCCCCCFB3F at 0041E648 is 0.1.
        var multiplier = 0.1 * Math.Sqrt(length);
        var velocity = new Vector3((-Math.Cos(a) * u + Math.Sin(a) * w) * multiplier,
            (Math.Sin(a) * u + Math.Cos(a) * w) * multiplier, 0);
        if (!velocity.IsFinite) throw new ArgumentException("Aim overflow.");
        return velocity;
    }
}

public static class Simulator
{
    public const string Version = "modern-reference-1";
    public const double StepSeconds = 0.02;
    public const int BounceLimit = 50;

    public static string VersionFor(CollisionPolicy policy) => policy switch
    {
        CollisionPolicy.ReferenceOrder => Version,
        CollisionPolicy.SurfaceTraversal => "surface-traversal-2",
        _ => throw new ArgumentOutOfRangeException(nameof(policy))
    };

    public static BallState AtStart(Course course, Vector3 velocity) => new(course.Start, velocity, course.InitialSurface);

    public static double Normalize180(double angle)
    {
        if (angle > 180) angle -= 360;
        if (angle < -180) angle += 360;
        return angle;
    }

    public static double Direction(Vector3 a, Vector3 p)
    {
        var dx = p.X - a.X; var dy = p.Y - a.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length == 0) return 0;
        if (!double.IsFinite(length)) throw new ArgumentException("Direction overflow.");
        // Clamp roundoff at the asin domain boundary, not collision comparisons.
        var angle = Math.Asin(Math.Clamp(dy / length, -1, 1)) * 180 / Math.PI;
        if (dx < 0) angle = 180 - angle;
        if (angle > 180) angle -= 360;
        return angle;
    }

    public static StepResult Step(Course course, BallState state, CollisionPolicy policy = CollisionPolicy.ReferenceOrder)
    {
        _ = VersionFor(policy);
        ValidateState(course, state);
        var surface = course.GetSurface(state.SurfaceId);
        var before = state.Position;
        var vx = state.Velocity.X + surface.AccelerationX;
        var vy = state.Velocity.Y + surface.AccelerationY;
        var position = new Vector3(before.X + vx, before.Y + vy, surface.Height(before.X, before.Y));
        vx *= 0.98; vy *= 0.98;
        var speed = Math.Sqrt(vx * vx + vy * vy);
        if (!position.IsFinite || !double.IsFinite(speed)) throw new ArgumentException("Physics overflow.");
        if (policy == CollisionPolicy.SurfaceTraversal)
            return SurfaceTraversal.Step(course, state, position, vx, vy);
        var alpha = Direction(default, new Vector3(vx, vy, 0));
        var pendingSurface = 0;
        var bounces = state.BounceCount;
        var reflected = false;
        var events = new List<PhysicsEvent>();
        for (var i = 0; i < surface.VertexIds.Count; i++)
        {
            var a = course.Vertices[surface.VertexIds[i]];
            var b = course.Vertices[surface.VertexIds[(i + 1) % surface.VertexIds.Count]];
            var edgeAngle = Direction(a, b);
            var delta = Normalize180(Direction(a, position) - edgeAngle);
            if (delta < 0) continue;
            var neighbor = surface.Neighbors[i];
            if (neighbor == 0)
            {
                bounces = checked(bounces + 1);
                alpha = Normalize180(2 * edgeAngle - alpha);
                position = before;
                reflected = true;
                events.Add(new("WallBounce", surface.Id, i + 1));
            }
            else
            {
                pendingSurface = neighbor;
                events.Add(new("PassageSelected", surface.Id, i + 1, neighbor));
            }
        }
        if (reflected)
        {
            vx = speed * Math.Cos(alpha * Math.PI / 180);
            vy = speed * Math.Sin(alpha * Math.PI / 180);
        }
        var next = new BallState(position, new Vector3(vx, vy, 0),
            pendingSurface == 0 ? state.SurfaceId : pendingSurface, bounces);
        if (bounces > BounceLimit)
        {
            next = new(course.Start, default, course.InitialSurface, bounces);
            events.Add(new("BounceLimitReset", next.SurfaceId, 0));
            return new(next, events.AsReadOnly(), true);
        }
        return new(next, events.AsReadOnly());
    }

    public static EndReason Evaluate(Course course, BallState state)
    {
        ValidateState(course, state);
        if (Math.Abs(state.Position.X - course.Target.X) < 8 && Math.Abs(state.Position.Y - course.Target.Y) < 8 &&
            Math.Abs(state.Velocity.X) < 2 && Math.Abs(state.Velocity.Y) < 2) return EndReason.Captured;
        if (course.GetSurface(state.SurfaceId).IsFlat && Math.Abs(state.Velocity.X) < 0.05 && Math.Abs(state.Velocity.Y) < 0.05)
            return EndReason.Stopped;
        return EndReason.Moving;
    }

    public static ShotResult Run(Course course, BallState initial, int maxSteps, int priorStrokes = 0,
        CollisionPolicy policy = CollisionPolicy.ReferenceOrder)
    {
        var version = VersionFor(policy);
        ValidateState(course, initial);
        if (initial.Velocity.X == 0 && initial.Velocity.Y == 0) throw new ArgumentException("Zero impulse is not a shot.");
        if (maxSteps < 1 || maxSteps > 1_000_000) throw new ArgumentOutOfRangeException(nameof(maxSteps));
        if (priorStrokes < 0 || priorStrokes > 6) throw new ArgumentOutOfRangeException(nameof(priorStrokes));
        if (!course.Contains(initial.SurfaceId, initial.Position)) throw new ArgumentException("Initial position outside selected surface.");
        var frames = new List<Frame> { new(0, initial with { BounceCount = 0 }, Array.Empty<PhysicsEvent>()) };
        var current = frames[0].State;
        var reason = EndReason.Moving;
        for (var step = 1; step <= maxSteps; step++)
        {
            var result = Step(course, current, policy);
            current = result.State;
            reason = result.ResolutionFailed ? EndReason.GeometryResolutionFailed
                : result.Reset ? EndReason.BounceLimitReset : Evaluate(course, current);
            if (reason == EndReason.Stopped) current = current with { Velocity = default };
            frames.Add(new(step, current, result.Events));
            if (reason != EndReason.Moving) break;
        }
        if (reason == EndReason.Moving) reason = EndReason.SimulationLimitReached;
        var strokes = priorStrokes + 1;
        var penalty = 0;
        if ((reason == EndReason.Stopped || reason == EndReason.BounceLimitReset) && strokes == 7)
        {
            strokes = 9; penalty = 2;
            if (reason == EndReason.Stopped) reason = EndReason.StrokeLimitReached;
        }
        return new(version, course.Id, course.Name, course.DataHash, StepSeconds, reason,
            frames.Count - 1, strokes, penalty, current, frames.AsReadOnly());
    }

    private static void ValidateState(Course course, BallState state)
    {
        if (!state.Position.IsFinite || !state.Velocity.IsFinite || state.Velocity.Z != 0 || state.BounceCount < 0)
            throw new ArgumentException("Finite planar state required (vz=0, bounceCount>=0).");
        _ = course.GetSurface(state.SurfaceId);
    }
}
