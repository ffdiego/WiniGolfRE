using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using Winigolf.Physics;
using static Raylib_cs.Raylib;
using P3 = Winigolf.Physics.Vector3;
using Vector3 = System.Numerics.Vector3;

namespace Winigolf.Game;

internal static class GameChecks
{
    public static int Run(GameApp app, string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        void Check(bool success, string description)
        {
            if (!success) throw new InvalidOperationException(description);
            checks.Add(description);
        }
        var courses = app.Session.Courses;
        CameraChecks.Run(app, Check, directory);
        AudioChecks.Run(app, Check);
        var a = new GameSession(courses); var b = new GameSession(courses);
        a.Shoot(new P3(10, 0, 0)); b.Shoot(new P3(10, 0, 0));
        a.Advance(.01);
        Check(a.FrameIndex == 0 && Math.Abs(a.VisualPosition.X + 245) < 1e-8 && a.Ball.Position.X == -250,
            "Half-step interpolation changes presentation only, not authoritative state");
        a.Advance(.59); for (var n = 0; n < 60; n++) b.Advance(.01);
        Check(a.FrameIndex == b.FrameIndex && a.Ball == b.Ball, "Render timer chunking does not change authoritative frame");
        a.Paused = true; a.Advance(99);
        Check(a.FrameIndex == 30, "Pause freezes replay time");
        a.Paused = false; a.Advance(99);
        Check(a.Outcome == EndReason.Captured && a.Strokes == 1 && a.Scores[1] == 1 && !a.CanShoot,
            "Capture ends playable course, counts stroke and records score");
        Check(a.CaptureProgress == 1 && a.VisualPosition == courses[0].Target && a.Ball == a.Shot!.FinalState,
            "Capture animation reaches hole center without changing authoritative result");
        a.Advance(99); Check(a.Strokes == 1 && a.Scores.Count == 1, "Terminal animation never adds another score");
        a.Select(1); Check(a.Index == 1 && a.Strokes == 0 && a.Scores.Count == 1, "Course progress resets ball and preserves completed scores");
        a.SetPolicy(CollisionPolicy.ReferenceOrder);
        Check(a.Scores.Count == 0 && a.Shot is null, "Changing physics resets course and scores");
        a.Select(0);
        for (var n = 0; n < 7; n++) { a.Shoot(new P3(.01, 0, 0)); a.Advance(1); }
        Check(a.Completed && a.Strokes == 9 && !a.CanShoot, "Stroke limit includes penalty and allows progression");
        a.Select(36); Check(a.Index == 0, "Course progression wraps safely after course 36");

        app.Select(0); app.Camera.TopDown = true; app.Camera.Yaw = 0; app.Draw(false);
        var origin = app.Session.Ball.Position;
        var p = app.Camera.Project(origin);
        var basis = app.Camera.Project(origin with { X = origin.X + 1 }) - p;
        var normal = app.Camera.Impulse(-25 * basis, origin, ShotMode.Mode1);
        var strong = app.Camera.Impulse(-25 * basis, origin, ShotMode.Mode2);
        Check(Math.Abs(normal.X - 1.25) < .003 && Math.Abs(normal.Y) < .003,
            "Top-down camera aim agrees with original precision formula");
        Check(Math.Abs(strong.X - 10) < .02, "Top-down camera strong aim agrees with original formula");
        Check(!app.StartAim(new Vector2(-100, -100), MouseButton.Left), "Click away from ball cannot start a shot");
        Check(app.StartAim(p, MouseButton.Left) && !app.FinishAim(p, MouseButton.Left) && app.Session.Shot is null,
            "Zero drag never counts as a stroke");
        app.StartAim(p, MouseButton.Left); app.CancelAim();
        Check(!app.FinishAim(p - 25 * basis, MouseButton.Left), "Esc or focus loss cancellation cannot launch a shot");
        Check(app.PrecisionGain == 1, "Playable client defaults to corrected original input gain without compensation");
        foreach (var gain in new[] { 1, 2, 3 })
        {
            app.SetPrecisionGain(gain); app.StartAim(p, MouseButton.Left); app.FinishAim(p - 25 * basis, MouseButton.Left);
            Check(Math.Abs(app.Session.Shot!.Frames[0].State.Velocity.X - 1.25 * gain) < .01,
                $"Actual left-button shot uses selected gain {gain} without changing camera reference formula");
            app.Select(0);
        }
        app.SetPrecisionGain(1);
        foreach (var length in new[] { 10, 50, 100, 200, 250 })
        {
            app.StartAim(p, MouseButton.Left); app.FinishAim(p - length * basis, MouseButton.Left);
            var expectedVelocity = .01 * length * Math.Sqrt(length);
            Check(Math.Abs(app.Session.Shot!.Frames[0].State.Velocity.X - expectedVelocity) < .01,
                $"Actual left-button drag {length} follows independently decoded L^1.5 curve");
            app.Select(0);
        }
        var rightMax = app.Camera.Impulse(-50 * basis, origin, ShotMode.Mode2);
        Check(Math.Abs(rightMax.X - 28.284271247461902) < .03, "Strong mode uses corrected multiplication at its maximum length");
        Check(app.StartAim(p, MouseButton.Right) && !app.FinishAim(p - 25 * basis, MouseButton.Left),
            "Wrong mouse release does not fire a right-button shot");
        Check(app.FinishAim(p - 25 * basis, MouseButton.Right), "Right mouse pull launches strong shot through shared input path");
        Check(Math.Abs(app.Session.Shot!.Frames[0].State.Velocity.X - 10) < .02,
            "Left-button sensitivity adjustment never changes right-button strength");
        app.Session.Advance(99);
        Check(app.Session.Outcome == EndReason.Captured, "Actual camera-compensated input captures on Basic");
        app.Draw(false); app.Draw(false); app.Capture(Path.Combine(directory, "basic-captured.png"));

        app.Select(0); app.Camera.TopDown = false; app.Camera.Yaw = 0; app.Draw(false);
        p = app.Camera.Project(origin); basis = app.Camera.Project(origin with { X = origin.X + 1 }) - p;
        var tilted = app.Camera.Impulse(new Vector2(0, 25 * basis.Length()), origin, ShotMode.Mode1);
        var expected = Aim.FromDrag(0, 25, ShotMode.Mode1);
        Check(Math.Abs(tilted.Y - expected.Y) < .01 && Math.Abs(tilted.X) < .01,
            "Inclined orthographic aim matches original minus-60-degree compensation");
        foreach (var yaw in new[] { 0f, .7f, 1.57f, 3f, 4.7f })
        {
            app.Camera.Yaw = yaw; app.Draw(false);
            p = app.Camera.Project(origin); basis = app.Camera.Project(origin with { X = origin.X + 1 }) - p;
            var impulse = app.Camera.Impulse(-25 * basis, origin, ShotMode.Mode1);
            Check(impulse.X > 0 && Math.Abs(impulse.Y) < .01 && impulse.IsFinite,
                $"Orthographic aim remains directed along world X at yaw {yaw}");
        }

        // The corrected input exposes substantially higher velocities than the old curve.
        // Exercise that expanded range on the three representative courses, not just L=25
        // (where the old and corrected formulas happen to coincide).
        foreach (var index in new[] { 0, 2, 24 })
        foreach (var mode in new[] { ShotMode.Mode1, ShotMode.Mode2 })
        foreach (var length in new[] { 1, 25, 50, 100, 250 })
        for (var direction = 0; direction < 8; direction++)
        {
            var angle = direction * Math.PI / 4;
            var impulse = Aim.FromDrag(length * Math.Cos(angle), length * Math.Sin(angle), mode);
            var course = courses[index];
            var result = Simulator.Run(course, Simulator.AtStart(course, impulse), 6000, policy: CollisionPolicy.SurfaceTraversal);
            Check(result.Reason is not (EndReason.SimulationLimitReached or EndReason.GeometryResolutionFailed) &&
                result.Frames.All(f => f.State.Position.IsFinite && course.Contains(f.State.SurfaceId, f.State.Position)),
                $"Corrected input envelope: course {course.Id}, mode {(int)mode}, length {length}, direction {direction}");
        }

        for (var index = 0; index < courses.Length; index++)
        {
            app.Select(index);
            var expectedWalls = courses[index].Surfaces.SelectMany(s => s.VertexIds.Select((id, edge) =>
                    (A: Math.Min(id, s.VertexIds[(edge + 1) % s.VertexIds.Count]), B: Math.Max(id, s.VertexIds[(edge + 1) % s.VertexIds.Count]), Neighbor: s.Neighbors[edge])))
                .Where(e => e.Neighbor == 0).Select(e => (e.A, e.B)).Distinct().Count();
            Check(app.Scene.WallCount == expectedWalls && app.Scene.TriangleCount > 0,
                $"Course {index + 1:00} builds all unique walls including internal walls");
            foreach (var topDown in new[] { false, true })
            {
                app.Camera.TopDown = topDown; app.Camera.Yaw = -.35f;
                app.Draw(false); app.Draw(false);
                Check(app.Camera.Value.Projection == CameraProjection.Orthographic,
                    $"Course {index + 1:00} always uses orthographic projection, topDown={topDown}");
                var viewDirection = Vector3.Normalize(app.Camera.Value.Target - app.Camera.Value.Position);
                Check(courses[index].Vertices.Values.All(vertex =>
                {
                    var point = SceneCamera.World(vertex);
                    var screen = app.Camera.Project(vertex);
                    var depth = Vector3.Dot(point - app.Camera.Value.Position, viewDirection);
                    return depth > 1 && depth < 20000 && screen.X >= 20 && screen.X <= app.Camera.Width - 20
                        && screen.Y >= 20 && screen.Y <= app.Camera.Height - 20;
                }), $"Course {index + 1:00} vertices stay inside viewport and clipping planes in {(topDown ? "top-down" : "inclined orthographic")} view");
                var image = LoadImageFromScreen(); var pixels = 0;
                try
                {
                    for (var x = 28; x < 930; x += 8)
                        for (var y = 145; y < 750; y += 8)
                        {
                            var color = GetImageColor(image, x, y);
                            if (color.G - color.R > 35 && color.G - color.B > 25) pixels++;
                        }
                }
                finally { UnloadImage(image); }
                Check(pixels > 50, $"Course {index + 1:00} renders real green polygon pixels in {(topDown ? "top-down" : "inclined orthographic")} view");
                if (index is 0 or 2 or 24)
                    app.Capture(Path.Combine(directory, $"{index + 1:00}-{(topDown ? "top" : "orthographic")}.png"));
            }
        }
        File.WriteAllText(Path.Combine(directory, "checks.json"), JsonSerializer.Serialize(new
        {
            passed = checks.Count, failed = 0, checks,
            renderer = "Raylib-cs 8.1.0 / raylib 6.0; real hidden OpenGL window and framebuffer readback",
            audioDeviceReady = app.Audio.Ready, loadedSoundCount = app.Audio.LoadedNames.Count,
            limitation = "Not measurements against original Winigolf; GPU frame rate and cross-machine determinism not certified."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
