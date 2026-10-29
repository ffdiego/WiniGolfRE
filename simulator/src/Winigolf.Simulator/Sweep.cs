using Winigolf.Physics;
using Engine = Winigolf.Physics.Simulator;

namespace Winigolf.Simulator;

internal sealed record SweepShot(int CourseId, string CourseName, string CourseDataHash, double Vx, double Vy,
    EndReason Reason, int Steps, int Bounces, int OutsideFrames, int? FirstOutsideStep,
    double MaxOutsideDistance, IReadOnlyList<PhysicsEvent> FirstOutsideEvents, BallState? FirstOutsideState, BallState FinalState);
internal sealed record SweepReport(string PhysicsVersion, string Runtime, string Architecture, int MaxSteps,
    int Shots, int LimitReached, int ResolutionFailures, int ShotsWithOutsideFrames, IReadOnlyDictionary<string, int> EndReasons,
    IReadOnlyList<string> Errors, IReadOnlyList<SweepShot> Results);

internal static class Sweep
{
    public static SweepReport Run(Func<int, Course> load, int maxSteps, CollisionPolicy policy = CollisionPolicy.ReferenceOrder)
    {
        if (maxSteps < 1 || maxSteps > 1_000_000) throw new ArgumentOutOfRangeException(nameof(maxSteps));
        var rows = new List<SweepShot>(); var errors = new List<string>();
        foreach (var h in Enumerable.Range(1, 36))
        {
            var course = load(h);
            foreach (var speed in new[] { 2.5, 10.0, 15.0 })
            foreach (var angle in Enumerable.Range(0, 8).Select(i => i * 45.0))
            {
                var radians = angle * Math.PI / 180;
                var velocity = new Vector3(speed * Math.Cos(radians), speed * Math.Sin(radians), 0);
                try
                {
                    var result = Engine.Run(course, Engine.AtStart(course, velocity), maxSteps, policy: policy);
                    var outside = result.Frames.Where(f => !course.Contains(f.State.SurfaceId, f.State.Position)).ToArray();
                    var firstOutside = outside.FirstOrDefault();
                    rows.Add(new(course.Id, course.Name, course.DataHash, velocity.X, velocity.Y, result.Reason,
                        result.Steps, result.FinalState.BounceCount, outside.Length, firstOutside?.Step,
                        outside.Select(f => OutsideDistance(course, f.State)).DefaultIfEmpty(0).Max(),
                        firstOutside?.Events ?? Array.Empty<PhysicsEvent>(), firstOutside?.State, result.FinalState));
                }
                catch (Exception e) when (e is ArgumentException or OverflowException)
                { errors.Add($"Course {h}, speed {speed}, angle {angle}: {e.Message}"); }
            }
        }
        return new(Engine.VersionFor(policy), Environment.Version.ToString(), System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            maxSteps, rows.Count + errors.Count, rows.Count(r => r.Reason == EndReason.SimulationLimitReached),
            rows.Count(r => r.Reason == EndReason.GeometryResolutionFailed),
            rows.Count(r => r.OutsideFrames > 0), rows.GroupBy(r => r.Reason.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            errors.AsReadOnly(), rows.AsReadOnly());
    }

    private static double OutsideDistance(Course course, BallState state)
    {
        var surface = course.GetSurface(state.SurfaceId);
        double distance = 0;
        for (var i = 0; i < surface.VertexIds.Count; i++)
        {
            var a = course.Vertices[surface.VertexIds[i]];
            var b = course.Vertices[surface.VertexIds[(i + 1) % surface.VertexIds.Count]];
            var dx = b.X - a.X; var dy = b.Y - a.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length > 0) distance = Math.Max(distance, (dx * (state.Position.Y - a.Y) - dy * (state.Position.X - a.X)) / length);
        }
        return distance;
    }
}
