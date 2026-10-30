using Winigolf.Physics;
using P3 = Winigolf.Physics.Vector3;

namespace Winigolf.Game;

internal static class CaptureMotionChecks
{
    public static void Run(Course course, Action<bool, string> check)
    {
        var surface = course.Surfaces.First(s => course.Contains(s.Id, course.Target));
        var centered = new BallState(course.Target, new P3(1, 0, 0), surface.Id, 0);
        var initial = CaptureMotion.Sample(course, centered, 0);
        var early = CaptureMotion.Sample(course, centered, .01);
        check(Math.Abs(initial.X - course.Target.X) < 1e-5 && Math.Abs(initial.Z + course.Target.Y) < 1e-5,
            "Cup animation starts at the captured position without horizontal teleportation");
        check(early.X > initial.X, "Cup animation initially preserves incoming horizontal direction");
        var reverse = CaptureMotion.Sample(course, centered with { Velocity = new P3(-1, 0, 0) }, .01);
        check(reverse.X < initial.X, "Opposite incoming velocity produces opposite cup entry motion");
        var slow = CaptureMotion.Sample(course, centered with { Velocity = new P3(.2, 0, 0) }, .01);
        check(early.X - initial.X > slow.X - initial.X, "Entry speed affects cup animation travel");
        foreach (var x in new[] { -7.9, 0, 7.9 })
        foreach (var y in new[] { -7.9, 0, 7.9 })
        foreach (var vx in new[] { -1.99, 0, 1.99 })
        foreach (var vy in new[] { -1.99, 0, 1.99 })
        {
            var state = centered with { Position = course.Target with { X = course.Target.X + x, Y = course.Target.Y + y }, Velocity = new P3(vx, vy, 0) };
            var start = CaptureMotion.Sample(course, state, 0);
            check(Math.Abs(start.X - state.Position.X) < 1e-4 && Math.Abs(start.Z + state.Position.Y) < 1e-4,
                $"Legacy square capture starts without snap: {x}/{y}/{vx}/{vy}");
            var finish = CaptureMotion.Sample(course, state, .8);
            check(float.IsFinite(finish.X) && float.IsFinite(finish.Y) && float.IsFinite(finish.Z) &&
                finish.Y + CaptureMotion.BallRadius < course.Target.Z + .36,
                $"Legacy square capture is hidden before turn changes: {x}/{y}/{vx}/{vy}");
        }
        var source = centered;
        var first = CaptureMotion.Sample(course, source, .2);
        _ = CaptureMotion.Sample(course, source, .5);
        check(first == CaptureMotion.Sample(course, source, .2) && source == centered,
            "Cup presentation is reproducible, seekable and leaves authoritative state untouched");
    }
}
