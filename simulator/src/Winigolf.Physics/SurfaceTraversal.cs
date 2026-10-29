namespace Winigolf.Physics;

// Experimental modernization, not a claim about the original executable.
// Trace a point along the attempted displacement through explicit portal edges only.
internal static class SurfaceTraversal
{
    private const double FractionTolerance = 1e-10;
    private const double DistanceTolerance = 1e-9;
    private readonly record struct Exit(int Edge, double Fraction, double Dx, double Dy, int Neighbor);

    public static StepResult Step(Course course, BallState original, Vector3 candidate, double vx, double vy)
    {
        if (!course.Contains(original.SurfaceId, original.Position))
            throw new ArgumentException("Surface traversal requires a position inside its selected surface.");
        var events = new List<PhysicsEvent>();
        var current = original.SurfaceId;
        double enteredAt = 0;
        var visits = new HashSet<(int Surface, int Edge)>();
        var limit = 4 * course.Surfaces.Count + 8;
        for (var iteration = 0; iteration < limit; iteration++)
        {
            var surface = course.GetSurface(current);
            var exits = new List<Exit>();
            for (var i = 0; i < surface.VertexIds.Count; i++)
            {
                var a = course.Vertices[surface.VertexIds[i]];
                var b = course.Vertices[surface.VertexIds[(i + 1) % surface.VertexIds.Count]];
                var dx = b.X - a.X; var dy = b.Y - a.Y;
                var length = Math.Sqrt(dx * dx + dy * dy);
                var startDistance = (dx * (original.Position.Y - a.Y) - dy * (original.Position.X - a.X)) / length;
                var endDistance = (dx * (candidate.Y - a.Y) - dy * (candidate.X - a.X)) / length;
                var change = endDistance - startDistance;
                // Only exits, including an endpoint exactly on a boundary. Incoming portal is ignored.
                if (change <= 0 || endDistance < -DistanceTolerance) continue;
                var fraction = -startDistance / change;
                if (fraction < enteredAt - FractionTolerance || fraction > 1 + FractionTolerance) continue;
                exits.Add(new(i, Math.Clamp(fraction, enteredAt, 1), dx, dy, surface.Neighbors[i]));
            }
            if (exits.Count == 0)
            {
                if (!course.Contains(current, candidate)) return Failed("UnresolvedEndpoint");
                return new(new(candidate, new(vx, vy, 0), current, original.BounceCount), events.AsReadOnly());
            }
            var firstTime = exits.Min(e => e.Fraction);
            var first = exits.Where(e => Math.Abs(e.Fraction - firstTime) <= FractionTolerance).OrderBy(e => e.Edge).ToArray();
            var walls = first.Where(e => e.Neighbor == 0).ToArray();
            if (walls.Length > 0)
            {
                var bounces = original.BounceCount;
                // Tied wall/portal: wall wins. Reflect all outward tied walls in edge order.
                foreach (var wall in walls)
                {
                    var nx = -wall.Dy; var ny = wall.Dx; // outward normal for clockwise polygons
                    var dot = vx * nx + vy * ny;
                    if (dot <= 0) continue;
                    var factor = 2 * dot / (nx * nx + ny * ny);
                    vx -= factor * nx; vy -= factor * ny;
                    bounces = checked(bounces + 1);
                    events.Add(new("WallBounce", current, wall.Edge + 1));
                }
                // Preserve full-step rollback like the reference, but roll back the surface too.
                events.Add(new("MovementRolledBack", original.SurfaceId, 0));
                var next = new BallState(original.Position, new(vx, vy, 0), original.SurfaceId, bounces);
                if (bounces > Simulator.BounceLimit)
                {
                    next = new(course.Start, default, course.InitialSurface, bounces);
                    events.Add(new("BounceLimitReset", next.SurfaceId, 0));
                    return new(next, events.AsReadOnly(), Reset: true);
                }
                return new(next, events.AsReadOnly());
            }
            var passage = first[0];
            if (!visits.Add((current, passage.Edge))) return Failed("RepeatedPortal");
            events.Add(new("PassageTraversed", current, passage.Edge + 1, passage.Neighbor));
            current = passage.Neighbor;
            enteredAt = firstTime;
        }
        return Failed("TraversalLimit");

        StepResult Failed(string kind)
        {
            events.Add(new(kind, current, 0));
            return new(original with { Velocity = new(vx, vy, 0) }, events.AsReadOnly(), ResolutionFailed: true);
        }
    }
}
