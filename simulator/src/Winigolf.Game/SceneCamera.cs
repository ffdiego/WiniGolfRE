using System.Numerics;
using Raylib_cs;
using Winigolf.Physics;
using P3 = Winigolf.Physics.Vector3;
using Vector3 = System.Numerics.Vector3;

namespace Winigolf.Game;

internal sealed class SceneCamera
{
    public CameraOptions Options { get; init; } = new();
    public Vector3? Focus { get; set; }
    public float RightOverlayWidth { get; init; }
    public float TopOverlayHeight { get; init; }
    public float BottomOverlayHeight { get; init; }
    public bool TopDown { get; set; }
    public float Yaw { get; set; }
    private float zoom = 1;
    public float Zoom { get => zoom; set { zoom = value; ZoomTarget = value; } }
    public float ZoomTarget { get; private set; } = 1;
    public void RequestZoom(float wheel) => ZoomTarget = Math.Clamp(ZoomTarget * MathF.Pow(1.15f, wheel), .5f, 3f);
    public void AdvanceZoom(double elapsed)
    {
        if (!double.IsFinite(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
        zoom += (ZoomTarget - zoom) * (float)(1 - Math.Exp(-12 * elapsed));
        if (Math.Abs(ZoomTarget - zoom) < .00001f) zoom = ZoomTarget;
    }
    public Camera3D Value { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool IsTurning { get; private set; }
    public double TurnDuration { get; private set; }
    public float TurnDelta { get; private set; }
    private float turnStart;
    private double turnElapsed;
    private Vector3? focusStart, focusDestination;

    public void BeginTurn(Random random, Vector3? destination = null, bool rotate = true)
    {
        var steps = random.Next(10, 91);
        TurnDelta = (random.Next(2) == 0 ? -1 : 1) * steps * 2 * MathF.PI / 180;
        TurnDuration = steps * .02;
        if (!rotate) { TurnDelta = 0; TurnDuration = .5; }
        focusStart = Focus ?? (Width > 0 ? Value.Target : destination);
        focusDestination = destination;
        if (Focus is null && destination is not null) Focus = focusStart;
        turnStart = Yaw; turnElapsed = 0;
        // Keep the optional map view steady; prepare the next inclined angle without delay.
        if (TopDown) { Yaw += TurnDelta; if (destination is not null) Focus = destination; IsTurning = false; }
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
        if (focusStart is { } start && focusDestination is { } finish)
            Focus = Vector3.Lerp(start, finish, (float)smooth);
        if (turnElapsed >= TurnDuration) IsTurning = false;
    }

    public void CancelTurn() => IsTurning = false;
    public static Vector3 World(P3 p) => new((float)p.X, (float)p.Z, -(float)p.Y);

    public void Fit(Course course, int width, int height)
    {
        Width = width; Height = height;
        var points = course.Vertices.Values.Select(World).ToArray();
        var min = points.Aggregate(Vector3.Min); var max = points.Aggregate(Vector3.Max);
        var courseCenter = (min + max) / 2;
        var target = Focus ?? courseCenter;
        var radius = points.Max(p => Vector3.Distance(p, courseCenter)) + 35;
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
        // The course bounding sphere fixes scale independently of yaw.
        camera.FovY /= Zoom;
        Value = camera;
    }

    public Vector2 Project(P3 p) => Raylib.GetWorldToScreenEx(World(p), Value, Width, Height);
    public Vector2 ProjectBallCenter(P3 position) => Project(position with { Z = position.Z + CaptureMotion.BallRadius });

    public void FollowBall(P3 position, double elapsed)
    {
        if (!Options.KeepBallInFrame || Width <= 0 || Height <= 0 || elapsed <= 0) return;
        var point = Raylib.GetWorldToScreenEx(World(position) + Vector3.UnitY * 4, Value, Width, Height);
        var margin = Math.Clamp(Options.EdgeMargin, 0, .45f);
        var rightLimit = Math.Min(Width * (1 - margin), Width - RightOverlayWidth - 24);
        var topLimit = Math.Max(Height * margin, TopOverlayHeight + 16);
        var bottomLimit = Math.Min(Height * (1 - margin), Height - BottomOverlayHeight - 16);
        var nearest = Vector2.Clamp(point, new(Width * margin, topLimit), new(rightLimit, bottomLimit));
        var excess = point - nearest;
        if (excess.LengthSquared() < .0001f) return;
        var movement = excess * (float)(1 - Math.Exp(-Options.FollowSpeed * elapsed));
        // Preserve visibility if a fast shot outruns the soft tracking at high zoom.
        var remaining = point - movement;
        movement += remaining - Vector2.Clamp(remaining,
            new(Width * .03f, Math.Max(Height * .03f, TopOverlayHeight)),
            new(Math.Min(Width * .97f, Width - RightOverlayWidth - 8), Math.Min(Height * .97f, Height - BottomOverlayHeight)));
        var forward = Vector3.Normalize(Value.Target - Value.Position);
        var right = Vector3.Normalize(Vector3.Cross(forward, Value.Up));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        var correction = (right * movement.X - up * movement.Y) * (Value.FovY / Height);
        Focus = (Focus ?? Value.Target) + correction;
    }

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

    // Convert the network's camera-relative angle and integer strength to the same
    // world pull that a local left-button drag would produce (2.5 units per percent).
    public Vector2 DragForAim(int angleDegrees, int strengthPercent, P3 ball)
    {
        if (angleDegrees is < 0 or > 359) throw new ArgumentOutOfRangeException(nameof(angleDegrees));
        if (strengthPercent is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(strengthPercent));
        var length = strengthPercent * 2.5;
        var radians = Yaw + Math.PI + angleDegrees * Math.PI / 180;
        var pullX = (float)(Math.Cos(radians) * length);
        var pullY = (float)(Math.Sin(radians) * length);
        var basis = Basis(ball);
        return basis.X * pullX + basis.Y * pullY;
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
