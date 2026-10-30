using System.Numerics;
using Winigolf.Physics;
using Vector3 = System.Numerics.Vector3;

namespace Winigolf.Game;

// A constrained presentation model after the authoritative capture decision.
// Its spring keeps every legacy square-region capture visually committed to the cup.
internal static class CaptureMotion
{
    public const float BallRadius = 4;
    private const float CenteringSpring = 120;
    private const float HorizontalDamping = 12;
    private const float Gravity = 600;
    public static Vector3 Sample(Course course, BallState state, double elapsed)
    {
        var mouth = SceneCamera.World(course.Target) + Vector3.UnitY * .36f;
        var point = SceneCamera.World(state.Position);
        point.Y = (float)course.GetSurface(state.SurfaceId).Height(state.Position.X, state.Position.Y) + BallRadius;
        var velocity = SceneCamera.World(state.Velocity) / (float)Simulator.StepSeconds;
        velocity.Y = 0;
        var remaining = Math.Clamp(elapsed, 0, 1);
        while (remaining > 1e-9)
        {
            var dt = (float)Math.Min(remaining, 1.0 / 240);
            var radial = new Vector3(point.X - mouth.X, 0, point.Z - mouth.Z);
            velocity += (-CenteringSpring * radial - HorizontalDamping * new Vector3(velocity.X, 0, velocity.Z)) * dt;
            velocity.Y -= Gravity * dt;
            point += velocity * dt;
            radial = new(point.X - mouth.X, 0, point.Z - mouth.Z);
            var distance = radial.Length();
            // The sphere rolls over the lip until its full diameter fits the opening.
            if (distance > 4 && point.Y >= mouth.Y)
            {
                var support = mouth.Y + (distance >= 8 ? BallRadius : MathF.Sqrt(16 - (8 - distance) * (8 - distance)));
                if (point.Y < support) { point.Y = support; velocity.Y = 0; }
            }
            // Inside the cup, inelastic wall and floor contacts damp the remaining motion.
            if (point.Y < mouth.Y && distance > 4)
            {
                var normal = radial / distance;
                point.X = mouth.X + normal.X * 4; point.Z = mouth.Z + normal.Z * 4;
                var outward = Vector3.Dot(velocity, normal);
                if (outward > 0) velocity -= normal * (1.25f * outward);
            }
            if (point.Y < mouth.Y - 12)
            {
                point.Y = mouth.Y - 12;
                if (velocity.Y < 0) velocity.Y *= -.15f;
            }
            remaining -= dt;
        }
        return point;
    }
}
