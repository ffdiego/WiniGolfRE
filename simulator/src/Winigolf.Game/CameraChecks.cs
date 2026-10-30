using Raylib_cs;
using Winigolf.Physics;

namespace Winigolf.Game;

internal static class CameraChecks
{
    public static void Run(GameApp app, Action<bool, string> check, string directory)
    {
        var a = new SceneCamera(); var b = new SceneCamera();
        foreach (var course in app.Session.Courses)
        {
            a.Yaw = 0; a.Fit(course, 936, 620); var span = a.Value.FovY;
            for (var angle = 0; angle < 360; angle += 15)
            {
                a.Yaw = angle * MathF.PI / 180; a.Fit(course, 936, 620);
                check(Math.Abs(a.Value.FovY - span) < .0001f, "Orthographic scale stays fixed through rotation: " + course.Id + "/" + angle);
            }
        }
        a = new SceneCamera(); b = new SceneCamera();
        a.RequestZoom(2); b.RequestZoom(2); a.AdvanceZoom(.1);
        for (var i = 0; i < 10; i++) b.AdvanceZoom(.01);
        check(a.Zoom > 1 && a.Zoom < a.ZoomTarget && Math.Abs(a.Zoom - b.Zoom) < .000001f,
            "Wheel zoom transitions smoothly and independently of frame timing");
        a.AdvanceZoom(10);
        check(a.Zoom == a.ZoomTarget, "Wheel zoom settles at requested scale");
        a = new SceneCamera(); b = new SceneCamera();
        a.BeginTurn(new Random(123)); b.BeginTurn(new Random(123));
        check(a.IsTurning && Math.Abs(a.TurnDelta) >= 20 * MathF.PI / 180 - 1e-6 &&
            Math.Abs(a.TurnDelta) <= MathF.PI + 1e-6 && a.TurnDuration is >= .2 and <= 1.8,
            "Turn uses recovered 10..90 steps, two degrees per step and 20ms nominal timing");
        a.AdvanceTurn(.15); for (var n = 0; n < 15; n++) b.AdvanceTurn(.01);
        check(Math.Abs(a.Yaw - b.Yaw) < 1e-6, "Camera animation is independent of render frame timing");
        a.AdvanceTurn(10); b.AdvanceTurn(10);
        check(!a.IsTurning && a.Yaw == a.TurnDelta && a.Yaw == b.Yaw, "Camera reaches exact selected final angle");
        var signs = new HashSet<int>();
        for (var seed = 0; seed < 30; seed++) { a.BeginTurn(new Random(seed)); signs.Add(Math.Sign(a.TurnDelta)); }
        check(signs.Count == 2, "Random transitions include both rotation directions");

        var session = new GameSession(app.Session.Courses);
        var turns = new TurnPresentation(new Random(19));
        var camera = new SceneCamera();
        turns.Update(session, camera, 0);
        check(camera.IsTurning && session.Shot is null && session.Strokes == 0, "Initial camera preparation does not create a stroke");
        turns.Update(session, camera, 10); var settled = camera.Yaw;
        turns.Update(session, camera, 10);
        check(!camera.IsTurning && camera.Yaw == settled, "Ready state starts only one rotation, never one per render frame");
        session.Shoot(new(1, 0, 0)); var shot = session.Shot;
        turns.Update(session, camera, .01);
        check(!camera.IsTurning && camera.Yaw == settled, "Camera does not turn while ball is in motion");
        session.Advance(10); var state = session.Ball;
        turns.Update(session, camera, 0);
        check(camera.IsTurning && session.CanShoot && session.Shot == shot && session.Ball == state,
            "Stopped ball starts next-turn rotation without modifying physics");
        session.Paused = true; turns.Update(session, camera, 10);
        check(camera.Yaw == settled && camera.IsTurning, "Pause freezes automatic camera transition");
        session.Paused = false; turns.Update(session, camera, 10);
        check(!camera.IsTurning && session.Ball == state && session.Shot == shot && session.Strokes == 1,
            "Finishing rotation never moves ball or adds strokes");
        turns.Reset(camera); session.Select(0); session.Shoot(new(10, 0, 0)); session.Advance(99);
        turns.Update(session, camera, 0);
        check(session.Outcome == EndReason.Captured && !camera.IsTurning, "Capture animation is not interrupted by next-turn camera rotation");
        session.Select(0); camera.TopDown = true; turns.Reset(camera); turns.Update(session, camera, 0);
        check(!camera.IsTurning && session.CanShoot, "Top-down mode stays ready without waiting for invisible rotation");

        app.Select(0); app.Camera.TopDown = false; app.AdvancePresentation(0); app.Draw(false);
        var ball = app.Camera.Project(app.Session.VisualPosition);
        check(app.Camera.IsTurning && !app.StartAim(ball, MouseButton.Left), "Actual UI blocks aim during automatic rotation");
        app.Draw(false); app.Capture(Path.Combine(directory, "turn-start.png"));
        app.AdvancePresentation(app.Camera.TurnDuration / 2); app.Draw(false); app.Draw(false);
        app.Capture(Path.Combine(directory, "turn-middle.png"));
        app.AdvancePresentation(10); app.Draw(false);
        app.Draw(false); app.Capture(Path.Combine(directory, "turn-end.png"));
        ball = app.Camera.Project(app.Session.VisualPosition);
        check(!app.Camera.IsTurning && app.StartAim(ball, MouseButton.Left), "Actual UI accepts aim after camera settles");
        app.CancelAim(); app.Select(0);
    }
}
