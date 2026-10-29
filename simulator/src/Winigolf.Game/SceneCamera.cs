using System.Numerics;
using Raylib_cs;
using Winigolf.Physics;
using P3 = Winigolf.Physics.Vector3;
using Vector3 = System.Numerics.Vector3;

namespace Winigolf.Game;

internal sealed class SceneCamera
{
    public bool TopDown { get; set; }
    public float Yaw { get; set; }
    public float Zoom { get; set; } = 1;
    public Camera3D Value { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool IsTurning { get; private set; }
    public double TurnDuration { get; private set; }
    public float TurnDelta { get; private set; }
    private float turnStart;
    private double turnElapsed;

    public void BeginTurn(Random random)
    {
        var steps = random.Next(10, 91);
        TurnDelta = (random.Next(2) == 0 ? -1 : 1) * steps * 2 * MathF.PI / 180;
        TurnDuration = steps * .02;
        turnStart = Yaw; turnElapsed = 0;
        // Keep the optional map view steady; prepare the next inclined angle without delay.
        if (TopDown) { Yaw += TurnDelta; IsTurning = false; }
        else IsTurning = true;
    }

    public void AdvanceTurn(double elapsed)
    {
        if (!double.IsFinite(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (!IsTurning) return;
        turnElapsed = Math.Min(TurnDuration, turnElapsed + elapsed);
        var t = turnElapsed / TurnDuration;
        var smooth = t * t * (3 - 2 * t);
        Yaw = turnStart + TurnDelta * (float)smooth;
        if (turnElapsed >= TurnDuration) IsTurning = false;
    }

    public void CancelTurn() => IsTurning = false;
    public static Vector3 World(P3 p) => new((float)p.X, (float)p.Z, -(float)p.Y);

    public void Fit(Course course, int width, int height)
    {
        Width = width; Height = height;
        var points = course.Vertices.Values.Select(World).ToArray();
        var min = points.Aggregate(Vector3.Min); var max = points.Aggregate(Vector3.Max);
        var target = (min + max) / 2;
        var radius = points.Max(p => Vector3.Distance(p, target)) + 35;
        var aspect = (float)width / height;
        // A bounding sphere makes framing safe at every yaw, including tall/narrow fields.
        var span = 2.2f * radius * Math.Max(1, 1 / aspect);
        var distance = span / (2 * MathF.Tan(22.5f * MathF.PI / 180));
        // Original c=-60 projects ground Y with factor .5: the viewing elevation is 30°.
        var direction = TopDown ? Vector3.UnitY : new Vector3(MathF.Sin(Yaw) * .8660254f, .5f, MathF.Cos(Yaw) * .8660254f);
        var camera = new Camera3D
        {
            Position = target + direction * distance, Target = target,
            Up = TopDown ? new Vector3(0, 0, -1) : Vector3.UnitY,
            FovY = span, Projection = CameraProjection.Orthographic
        };
        // Fit actual projected vertices, not just a bounding sphere. Long flat courses
        // would otherwise occupy a small fraction of the playable viewport.
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var projected = points.Select(p => Raylib.GetWorldToScreenEx(p, camera, width, height)).ToArray();
            var ratio = Math.Max(projected.Max(p => Math.Abs(p.X - width / 2f)) / Math.Max(1, width / 2f - 55),
                projected.Max(p => Math.Abs(p.Y - height / 2f)) / Math.Max(1, height / 2f - 55));
            if (Math.Abs(ratio - 1) < .002f) break;
            span *= ratio; camera.FovY = span;
        }
        camera.FovY /= Zoom;
        Value = camera;
    }

    public Vector2 Project(P3 p) => Raylib.GetWorldToScreenEx(World(p), Value, Width, Height);

    // Inverse projection on the ball's horizontal plane: original yaw/tilt compensation.
    // Camera state never enters the simulation of a shot already calculated.
    public P3 Impulse(Vector2 drag, P3 ball, ShotMode mode)
    {
        var basis = Basis(ball);
        var units = drag / basis.Scale;
        var length = units.Length();
        if (length < .5f) return default;
        var limit = mode == ShotMode.Mode1 ? 250f : 50f;
        var factor = Math.Min(1, limit / length);
        var worldPull = new Vector2((basis.Y.Y * drag.X - basis.Y.X * drag.Y) / basis.Determinant,
            (-basis.X.Y * drag.X + basis.X.X * drag.Y) / basis.Determinant) * factor;
        var gain = (mode == ShotMode.Mode1 ? .1 : .8) * .1 * Math.Sqrt(Math.Min(length, limit));
        return new P3(-worldPull.X * gain, -worldPull.Y * gain, 0);
    }

    public float DragLimit(P3 ball, ShotMode mode) => Basis(ball).Scale * (mode == ShotMode.Mode1 ? 250 : 50);

    private (Vector2 X, Vector2 Y, float Determinant, float Scale) Basis(P3 ball)
    {
        var p = Project(ball);
        var x = Project(ball with { X = ball.X + 1 }) - p;
        var y = Project(ball with { Y = ball.Y + 1 }) - p;
        var det = x.X * y.Y - y.X * x.Y;
        if (Math.Abs(det) < 1e-7) throw new InvalidOperationException("Vista singular para a mira.");
        var xx = x.LengthSquared(); var yy = y.LengthSquared(); var xy = Vector2.Dot(x, y);
        var scale = MathF.Sqrt((xx + yy + MathF.Sqrt((xx - yy) * (xx - yy) + 4 * xy * xy)) / 2);
        return (x, y, det, scale);
    }
}
