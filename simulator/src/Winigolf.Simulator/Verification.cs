using Winigolf.Physics;
using Engine = Winigolf.Physics.Simulator;

namespace Winigolf.Simulator;

internal sealed record VerificationReport(IReadOnlyList<string> PhysicsVersions, int Total, int Passed, int Courses, IReadOnlyList<string> Failures);

internal static class Verification
{
    public static VerificationReport Run(Func<int, Course> load)
    {
        var failures = new List<string>(); var total = 0;
        void Check(string name, Action action)
        {
            total++;
            try { action(); } catch (Exception ex) { failures.Add($"{name}: {ex.Message}"); }
        }
        static void True(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
        static void Near(double actual, double expected, double tolerance = 1e-8)
        { if (!double.IsFinite(actual) || Math.Abs(actual - expected) > tolerance) throw new Exception($"Expected {expected:R}, got {actual:R}."); }
        static void Reject(Action action)
        { try { action(); } catch (ArgumentException) { return; } throw new Exception("Invalid input accepted."); }

        var courses = new List<Course>();
        for (var h = 1; h <= 36; h++) { var id = h; Check($"Load course {id}", () => courses.Add(load(id))); }
        string[] versions = [Engine.Version, Engine.VersionFor(CollisionPolicy.SurfaceTraversal)];
        if (courses.Count != 36) return new(versions, total, total - failures.Count, courses.Count, failures.AsReadOnly());
        var basic = courses[0]; var dip = courses[2]; var splitt = courses[24];
        Check("Course inventory", () => { True(courses.Sum(c => c.Surfaces.Count) == 265); True(courses.Sum(c => c.Vertices.Count) == 717); });
        Check("Unknown Splitt slot remains absent", () => True(splitt.UnassignedVertices.SequenceEqual([7]) && !splitt.Vertices.ContainsKey(7)));
        Check("Aim mode 1 (byte-interpreted x87)", () => { var v = Aim.FromDrag(-100, 0, ShotMode.Mode1); Near(v.X, 10); Near(v.Y, 0); });
        Check("Aim mode 2 (byte-interpreted x87)", () => Near(Aim.FromDrag(-50, 0, ShotMode.Mode2).X, 28.284271247461902));
        Check("Aim short drag", () => Near(Aim.FromDrag(-10, 0, ShotMode.Mode1).X, .31622776601683794));
        Check("Aim medium drag", () => Near(Aim.FromDrag(-50, 0, ShotMode.Mode1).X, 3.5355339059327378));
        Check("Aim maximum normal drag", () => Near(Aim.FromDrag(-250, 0, ShotMode.Mode1).X, 39.528470752104745));
        Check("Aim length exponent is 1.5", () => Near(Aim.FromDrag(-100, 0, ShotMode.Mode1).X / Aim.FromDrag(-25, 0, ShotMode.Mode1).X, 8));
        Check("Aim clamps length", () => Near(Aim.FromDrag(-1000, 0, ShotMode.Mode1).X, Aim.FromDrag(-250, 0, ShotMode.Mode1).X));
        Check("Aim vertical sign and projection", () => Near(Aim.FromDrag(0, 100, ShotMode.Mode1).Y, 20));
        Check("Aim rejects zero", () => Reject(() => Aim.FromDrag(0, 0, ShotMode.Mode1)));
        Check("Aim rejects singular camera", () => Reject(() => Aim.FromDrag(10, 0, ShotMode.Mode1, 0, 90)));
        Check("Aim rejects NaN", () => Reject(() => Aim.FromDrag(double.NaN, 0, ShotMode.Mode1)));
        Check("Flat displacement before damping", () =>
        { var r = Engine.Step(basic, Engine.AtStart(basic, new(10, 0, 0))); Near(r.State.Position.X, -240); Near(r.State.Velocity.X, 9.8); True(r.State.SurfaceId == 1); });
        Check("Passage", () =>
        { var r = Engine.Step(basic, new(new(99, 0, 10), new(2, 0, 0), 1)); Near(r.State.Position.X, 101); Near(r.State.Velocity.X, 1.96); True(r.State.SurfaceId == 2); });
        Check("Exact portal endpoint uses angular boundary", () =>
        { var r = Engine.Step(basic, new(new(98, 0, 10), new(2, 0, 0), 1)); Near(r.State.Position.X, 100); True(r.State.SurfaceId == 2); });
        Check("Wall restores full position", () =>
        { var r = Engine.Step(basic, new(new(-299, 0, 10), new(-2, 0, 0), 1)); Near(r.State.Position.X, -299); Near(r.State.Velocity.X, 1.96); True(r.State.BounceCount == 1); });
        Check("Oblique reflection preserves damped magnitude", () =>
        { var r = Engine.Step(basic, new(new(-299, 0, 10), new(-2, 1, 0), 1)); Near(r.State.Position.Y, 0); Near(r.State.Velocity.X, 1.96); Near(r.State.Velocity.Y, .98); });
        Check("Corner follows edge order", () =>
        { var r = Engine.Step(basic, new(new(-299, 49, 10), new(-2, 2, 0), 1)); Near(r.State.Velocity.X, -1.96); Near(r.State.Velocity.Y, -1.96); True(r.Events.Single().Edge == 1); });
        Check("Legacy pending passage survives later wall (documented diagnostic)", () =>
        { var r = Engine.Step(basic, new(new(99, -49, 10), new(2, -2, 0), 1)); True(r.State.SurfaceId == 2); Near(r.State.Position.X, 99); True(!basic.Contains(2, r.State.Position)); });
        Check("Dip descent and lagged height", () =>
        { var r = Engine.Step(dip, new(new(-125, 0, 50), new(2, 0, 0), 2)); Near(r.State.Position.X, -122.76470588235294); Near(r.State.Position.Z, 50); Near(r.State.Velocity.X, 2.1905882352941175); });
        Check("Dip ascent", () =>
        { var r = Engine.Step(dip, new(new(75, 0, 50), new(2, 0, 0), 4)); Near(r.State.Position.X, 76.76470588235294); Near(r.State.Velocity.X, 1.7294117647058822); });
        Check("Ramp reverses from zero instead of stopping", () =>
        { var r = Engine.Step(dip, new(new(75, 0, 50), default, 4)); True(r.State.Velocity.X < 0); True(Engine.Evaluate(dip, r.State) == EndReason.Moving); });
        Check("Splitt horizontal internal wall", () =>
        { var r = Engine.Step(splitt, new(new(0, 201, 10), new(0, -2, 0), 4)); Near(r.State.Position.Y, 201); Near(r.State.Velocity.Y, 1.96); True(r.State.SurfaceId == 4); });
        Check("Splitt vertical internal wall", () =>
        { var r = Engine.Step(splitt, new(new(99, 225, 10), new(2, 0, 0), 4)); Near(r.State.Position.X, 99); Near(r.State.Velocity.X, -1.96); True(r.State.SurfaceId == 4); });
        Check("Capture strict speed boundary", () => True(Engine.Evaluate(basic, new(basic.Target, new(2, 0, 0), 2)) == EndReason.Moving));
        Check("Capture strict distance boundary", () => True(Engine.Evaluate(basic, new(new(208, 0, 10), new(1, 0, 0), 2)) == EndReason.Moving));
        Check("Capture square rather than circle", () => True(Engine.Evaluate(basic, new(new(207, 7, 10), new(1, 1, 0), 2)) == EndReason.Captured));
        Check("Capture precedes stop", () => True(Engine.Evaluate(basic, new(basic.Target, default, 2)) == EndReason.Captured));
        Check("Flat low speed stop", () => True(Engine.Evaluate(basic, new(basic.Start, new(.049, .049, 0), 1)) == EndReason.Stopped));
        Check("Strict stopping threshold", () => True(Engine.Evaluate(basic, new(basic.Start, new(.05, 0, 0), 1)) == EndReason.Moving));
        Check("Bounce limit resets surface and position", () =>
        { var r = Engine.Step(basic, new(new(-299, 0, 10), new(-2, 0, 0), 1, 50)); True(r.Reset); True(r.State.Position == basic.Start); True(r.State.SurfaceId == basic.InitialSurface); True(r.State.Velocity == default); });
        Check("Seventh failed attempt becomes nine", () =>
        { var r = Engine.Run(basic, Engine.AtStart(basic, new(.01, 0, 0)), 10, 6); True(r.Reason == EndReason.StrokeLimitReached && r.StrokeCount == 9 && r.Penalty == 2); });
        Check("Seventh successful attempt has no penalty", () =>
        { var r = Engine.Run(basic, new(new(199, 0, 10), new(.1, 0, 0), 2), 10, 6); True(r.Reason == EndReason.Captured && r.StrokeCount == 7 && r.Penalty == 0); });
        Check("Step limit is explicit, not stop", () =>
        { var r = Engine.Run(basic, Engine.AtStart(basic, new(10, 0, 0)), 1); True(r.Reason == EndReason.SimulationLimitReached && r.Steps == 1 && r.Penalty == 0); });
        Check("Basic analytic full trajectory", () =>
        { var r = Engine.Run(basic, Engine.AtStart(basic, new(10, 0, 0)), 6000); True(r.Reason == EndReason.Captured && r.Steps == 107); Near(r.FinalState.Position.X, 192.434788659962, 1e-7); });
        Check("Repeatability on this runtime", () =>
        { var state = Engine.AtStart(dip, new(10, 0, 0)); var a = Engine.Run(dip, state, 6000); var b = Engine.Run(dip, state, 6000); True(a.Reason == b.Reason && a.Frames.Select(f => f.State).SequenceEqual(b.Frames.Select(f => f.State))); });
        Check("Reject outside start", () => Reject(() => Engine.Run(basic, new(new(10000, 0, 10), new(1, 0, 0), 1), 1)));
        Check("Reject zero shot", () => Reject(() => Engine.Run(basic, Engine.AtStart(basic, default), 1)));
        Check("Reject nonplanar velocity", () => Reject(() => Engine.Step(basic, Engine.AtStart(basic, new(1, 0, 1)))));
        const CollisionPolicy traversal = CollisionPolicy.SurfaceTraversal;
        Check("Traversal: rollback cancels pending surface", () =>
        { var r = Engine.Step(basic, new(new(99, -49, 10), new(2, -2, 0), 1), traversal); True(r.State.SurfaceId == 1 && basic.Contains(1, r.State.Position)); Near(r.State.Velocity.X, 1.96); Near(r.State.Velocity.Y, 1.96); });
        Check("Traversal: both walls at exact corner", () =>
        { var r = Engine.Step(basic, new(new(-299, 49, 10), new(-2, 2, 0), 1), traversal); Near(r.State.Velocity.X, 1.96); Near(r.State.Velocity.Y, -1.96); True(r.State.BounceCount == 2); });
        Check("Traversal: Splitt L remains solid", () =>
        { var r = Engine.Step(splitt, new(new(99, 225, 10), new(5, 0, 0), 4), traversal); True(r.State.SurfaceId == 4); Near(r.State.Position.X, 99); Near(r.State.Velocity.X, -4.9); });
        Check("Traversal: Splitt horizontal arm remains solid", () =>
        { var r = Engine.Step(splitt, new(new(0, 201, 10), new(0, -2, 0), 4), traversal); True(r.State.SurfaceId == 4); Near(r.State.Position.Y, 201); Near(r.State.Velocity.Y, 1.96); });
        Check("Traversal: actual Splitt exit remains open", () =>
        { var r = Engine.Step(splitt, new(new(99, 175, 10), new(5, 0, 0), 5), traversal); True(r.State.SurfaceId == 7 && !r.ResolutionFailed); Near(r.State.Position.X, 104); });
        Check("Traversal: three passages in one displacement", () =>
        { var r = Engine.Step(dip, new(new(-51, 0, 10.533333333333333), new(210, 0, 0), 2), traversal); True(r.State.SurfaceId == 5 && !r.ResolutionFailed); True(r.Events.Count(e => e.Kind == "PassageTraversed") == 3); Near(r.State.Position.X, 159.23529411764707); });
        Check("Traversal: neighbor wall rolls back original surface", () =>
        { var r = Engine.Step(basic, new(new(99, 0, 10), new(250, 0, 0), 1), traversal); True(r.State.SurfaceId == 1); Near(r.State.Position.X, 99); Near(r.State.Velocity.X, -245); True(r.Events.Any(e => e.Kind == "WallBounce" && e.SurfaceId == 2)); });
        Check("Traversal: Plateau portal precedes a remote edge extension", () =>
        { var r = Engine.Step(courses[4], new(new(-51, 195, 100), new(7, 0, 0), 1), traversal); True(r.State.SurfaceId == 2 && !r.ResolutionFailed); Near(r.State.Position.X, -44); Near(r.State.Velocity.X, 6.86); True(r.Events.All(e => e.Kind != "WallBounce")); });
        Check("Traversal: exact portal arrival then continued movement", () =>
        { var r = Engine.Step(basic, new(new(98, 0, 10), new(2, 0, 0), 1), traversal); True(r.State.SurfaceId == 2); var next = Engine.Step(basic, r.State, traversal); True(!next.ResolutionFailed && next.State.SurfaceId == 2); Near(next.State.Position.X, 101.96); });
        Check("Traversal: stationary portal has no artificial crossing", () =>
        { var r = Engine.Step(basic, new(new(100, 0, 10), default, 1), traversal); True(r.State.SurfaceId == 1 && r.Events.Count == 0); });
        Check("Traversal: flat integration unchanged", () =>
        { var state = Engine.AtStart(basic, new(10, 0, 0)); True(Engine.Step(basic, state).State == Engine.Step(basic, state, traversal).State); });
        Check("Traversal: ramp equation unchanged", () =>
        { var state = new BallState(new(-125, 0, 50), new(2, 0, 0), 2); True(Engine.Step(dip, state).State == Engine.Step(dip, state, traversal).State); });
        Check("Traversal: Basic analytic capture unchanged", () =>
        { var r = Engine.Run(basic, Engine.AtStart(basic, new(10, 0, 0)), 6000, policy: traversal); True(r.Reason == EndReason.Captured && r.Steps == 107); Near(r.FinalState.Position.X, 192.434788659962, 1e-7); });
        Check("Traversal: capture and seventh penalty preserved", () =>
        { var r = Engine.Run(basic, new(new(199, 0, 10), new(.1, 0, 0), 2), 10, 6, traversal); True(r.Reason == EndReason.Captured && r.Penalty == 0); var failed = Engine.Run(basic, Engine.AtStart(basic, new(.01, 0, 0)), 10, 6, traversal); True(failed.Reason == EndReason.StrokeLimitReached && failed.StrokeCount == 9); });
        Check("Traversal: bounce protection preserves valid state", () =>
        { var r = Engine.Step(basic, new(new(-299, 0, 10), new(-2, 0, 0), 1, 50), traversal); True(r.Reset && r.State.Position == basic.Start && r.State.SurfaceId == basic.InitialSurface); });
        Check("Traversal: reject inconsistent checkpoint", () =>
            Reject(() => Engine.Step(basic, new(new(99, 0, 10), new(1, 0, 0), 2), traversal)));
        Check("Reject unknown collision policy", () => Reject(() => Engine.Step(basic, Engine.AtStart(basic, new(1, 0, 0)), (CollisionPolicy)123)));
        Check("Traversal: repeatability on this runtime", () =>
        { var state = Engine.AtStart(splitt, new(10, 7, 0)); var a = Engine.Run(splitt, state, 6000, policy: traversal); var b = Engine.Run(splitt, state, 6000, policy: traversal); True(a.Reason == b.Reason && a.Frames.Select(f => f.State).SequenceEqual(b.Frames.Select(f => f.State))); });
        foreach (var policy in new[] { CollisionPolicy.ReferenceOrder, CollisionPolicy.SurfaceTraversal })
        {
            Check($"Serialized checkpoint resumes exactly on this runtime: {policy}", () =>
            {
                var course = courses[35];
                var result = Engine.Run(course, Engine.AtStart(course, new(10, 7, 0)), 6000, policy: policy);
                True(result.Frames.Count > 8);
                foreach (var fraction in new[] { .25, .5, .75 })
                {
                    var index = (int)((result.Frames.Count - 1) * fraction);
                    var checkpoint = System.Text.Json.JsonSerializer.Deserialize<BallState>(
                        System.Text.Json.JsonSerializer.Serialize(result.Frames[index].State, Export.JsonOptions), Export.JsonOptions)!;
                    True(checkpoint == result.Frames[index].State);
                    for (var i = index + 1; i < result.Frames.Count; i++)
                    {
                        var step = Engine.Step(course, checkpoint, policy);
                        checkpoint = step.State;
                        if (!step.Reset && !step.ResolutionFailed && Engine.Evaluate(course, checkpoint) == EndReason.Stopped)
                            checkpoint = checkpoint with { Velocity = default };
                        True(checkpoint == result.Frames[i].State, $"Checkpoint mismatch at step {i}");
                    }
                }
            });
        }
        return new(versions, total, total - failures.Count, courses.Count, failures.AsReadOnly());
    }
}
