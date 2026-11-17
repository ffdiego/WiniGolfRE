using System.Numerics;
using System.Runtime.InteropServices.JavaScript;
using Raylib_cs;
using Winigolf.Physics;
using static Raylib_cs.Raylib;
using Vector3 = System.Numerics.Vector3;

namespace Winigolf.Game;

public static partial class Host
{
    private const int Width = 960, Height = 600;
    private static GameSession session = null!;
    private static readonly CourseScene scene = new();
    private static readonly SceneCamera camera = new DynamicFramingCamera { Yaw = -.35f };
    private static readonly TurnPresentation turns = new(Random.Shared);
    private static bool aiming;
    private static Vector2 anchor, endpoint;

    public static void Main()
    {
        InitWindow(Width, Height, "WiniGolf Web");
        if (!IsWindowReady()) throw new InvalidOperationException("Falha ao inicializar raylib.");
        Rlgl.SetClipPlanes(1, 20000);
        session = new GameSession([Course.Load("courses/01.json")]);
        scene.Build(session.Course);
        camera.Focus = SceneCamera.World(session.VisualPosition) + Vector3.UnitY * 4;
        camera.Fit(session.Course, Width, Height);
    }

    [JSExport]
    public static void Pointer(int kind, double x, double y)
    {
        var point = new Vector2((float)x, (float)y);
        if (kind == 0 && session.CanShoot && !camera.IsTurning)
        {
            aiming = true; anchor = endpoint = point;
        }
        else if (aiming && kind == 1) endpoint = point;
        else if (aiming && kind == 2)
        {
            aiming = false;
            var impulse = camera.Impulse(point - anchor, session.Ball.Position, ShotMode.Mode1);
            if (impulse.X != 0 || impulse.Y != 0) session.Shoot(impulse);
        }
        else if (kind == 3) aiming = false;
    }

    [JSExport]
    public static void Reset()
    {
        aiming = false; session.Select(0); scene.Build(session.Course); turns.Reset(camera);
        camera.Zoom = 1;
        camera.Focus = SceneCamera.World(session.VisualPosition) + Vector3.UnitY * 4;
    }

    [JSExport]
    public static void Frame(double elapsed)
    {
        elapsed = Math.Clamp(elapsed, 0, .1);
        session.Advance(elapsed);
        camera.AdvanceZoom(elapsed);
        camera.Fit(session.Course, Width, Height);
        turns.Update(session, camera, elapsed);
        camera.Fit(session.Course, Width, Height);
        BeginDrawing();
        ClearBackground(new Color(14, 25, 34, 255));
        scene.Draw(session, camera);
        if (aiming)
        {
            var ball = camera.ProjectBallCenter(session.VisualPosition);
            var delta = endpoint - anchor;
            var limit = camera.DragLimit(session.Ball.Position, ShotMode.Mode1);
            var pull = delta.Length() > limit ? Vector2.Normalize(delta) * limit : delta;
            DrawLineEx(ball, ball + pull, 3, Color.Yellow);
            DrawCircleV(ball + pull, 5, Color.Yellow);
            DrawText($"FORCA {Math.Clamp((int)(delta.Length() / limit * 100), 0, 100)}%", 16, Height - 32, 20, Color.White);
        }
        DrawText($"BASIC   TACADAS: {session.Strokes}", 16, 16, 20, Color.White);
        EndDrawing();
    }
}
