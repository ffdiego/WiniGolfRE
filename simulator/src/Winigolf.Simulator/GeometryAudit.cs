using Winigolf.Physics;
using Engine = Winigolf.Physics.Simulator;

namespace Winigolf.Simulator;

internal sealed record ProbeFailure(int CourseId, int SurfaceId, Vector3 Origin, Vector3 Velocity, string Detail, StepResult? Result);
internal sealed record GeometryAuditReport(string PhysicsVersion, string Runtime, int Samples, int Failures,
    IReadOnlyDictionary<int, string> CourseDataHashes, IReadOnlyList<ProbeFailure> FirstFailures);

internal static class GeometryAudit
{
    public static GeometryAuditReport Run(Func<int, Course> load)
    {
        var failures = new List<ProbeFailure>(); var failureCount = 0; var samples = 0;
        var hashes = new Dictionary<int, string>();
        foreach (var h in Enumerable.Range(1, 36))
        {
            var course = load(h); hashes.Add(h, course.DataHash);
            foreach (var surface in course.Surfaces)
            {
                var vertices = surface.VertexIds.Select(i => course.Vertices[i]).ToArray();
                var center = new Vector3(vertices.Average(p => p.X), vertices.Average(p => p.Y), 0);
                var origins = new List<Vector3> { center };
                for (var i = 0; i < vertices.Length; i++)
                {
                    var a = vertices[i]; var b = vertices[(i + 1) % vertices.Length];
                    origins.Add(Toward(a, center, .001));
                    origins.Add(Toward(new((a.X + b.X) / 2, (a.Y + b.Y) / 2, 0), center, 1e-8));
                }
                foreach (var xy in origins)
                foreach (var speed in new[] { .001, 2.5, 15.0, 30.0, 300.0 })
                foreach (var angle in Enumerable.Range(0, 16).Select(i => i * Math.PI / 8))
                {
                    var origin = xy with { Z = surface.Height(xy.X, xy.Y) };
                    var velocity = new Vector3(speed * Math.Cos(angle), speed * Math.Sin(angle), 0);
                    StepResult? result = null;
                    samples++;
                    try
                    {
                        result = Engine.Step(course, new(origin, velocity, surface.Id), CollisionPolicy.SurfaceTraversal);
                        if (result.ResolutionFailed) throw new ArgumentException("GeometryResolutionFailed");
                        if (!result.State.Position.IsFinite || !result.State.Velocity.IsFinite) throw new ArgumentException("Nonfinite result");
                        if (!course.Contains(result.State.SurfaceId, result.State.Position)) throw new ArgumentException("Outside selected surface");
                        var expectedSquared = Math.Pow((velocity.X + surface.AccelerationX) * .98, 2) + Math.Pow((velocity.Y + surface.AccelerationY) * .98, 2);
                        var actualSquared = result.State.Velocity.X * result.State.Velocity.X + result.State.Velocity.Y * result.State.Velocity.Y;
                        if (!result.Reset && Math.Abs(actualSquared - expectedSquared) > 1e-9 * Math.Max(1, expectedSquared))
                            throw new ArgumentException("Collision changed damped kinetic magnitude");
                    }
                    catch (Exception ex) when (ex is ArgumentException or OverflowException)
                    {
                        failureCount++;
                        if (failures.Count < 100) failures.Add(new(h, surface.Id, origin, velocity, ex.Message, result));
                    }
                }
            }
        }
        return new(Engine.VersionFor(CollisionPolicy.SurfaceTraversal), Environment.Version.ToString(), samples,
            failureCount, hashes, failures.AsReadOnly());
    }

    private static Vector3 Toward(Vector3 a, Vector3 b, double fraction)
        => new(a.X + (b.X - a.X) * fraction, a.Y + (b.Y - a.Y) * fraction, 0);
}
