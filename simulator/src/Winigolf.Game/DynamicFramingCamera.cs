using System.Numerics;
using Winigolf.Physics;
using P3 = Winigolf.Physics.Vector3;

namespace Winigolf.Game;

// TV framing keeps the active ball and the cup in the usable part of the screen.
// It is deliberately separate from SceneCamera's local-play tracking policy.
internal sealed class DynamicFramingCamera : SceneCamera
{
    private float desiredZoom = 1;
    private Course? course;

    public override void Fit(Course currentCourse, int width, int height)
    {
        if (!ReferenceEquals(course, currentCourse))
        {
            course = currentCourse;
            desiredZoom = 1;
        }
        base.Fit(currentCourse, width, height);
    }

    public override void TrackFrame(P3 ball, P3 hole, double elapsed)
    {
        if (Width <= 0 || Height <= 0 || elapsed <= 0 || !double.IsFinite(elapsed)) return;

        var ballPoint = Project(ball with { Z = ball.Z + CaptureMotion.BallRadius });
        var holePoint = Project(hole);
        var safe = SafeArea;
        var usableWidth = Math.Max(1, safe.Width * Math.Clamp(Options.DynamicFrameOccupancy, .4f, .95f));
        var usableHeight = Math.Max(1, safe.Height * Math.Clamp(Options.DynamicFrameOccupancy, .4f, .95f));
        var separation = Vector2.Abs(ballPoint - holePoint);

        // Zoom is proportional to the projected separation in an orthographic view.
        // Keep both points inside the safe rectangle while using most of its area.
        var factor = 3f;
        if (separation.X > 1) factor = Math.Min(factor, usableWidth / separation.X);
        if (separation.Y > 1) factor = Math.Min(factor, usableHeight / separation.Y);
        var minZoom = Math.Max(.1f, Options.DynamicZoomMin);
        var maxZoom = Math.Max(minZoom, Options.DynamicZoomMax);
        desiredZoom = Math.Clamp(Zoom * factor, minZoom, maxZoom);

        // Place the pair's midpoint at the center of the safe area, which is left of the TV scoreboard.
        var safeCenter = new Vector2(safe.X + safe.Width / 2, safe.Y + safe.Height / 2);
        var pairCenter = (ballPoint + holePoint) / 2;
        var pan = pairCenter - safeCenter;
        var panAlpha = (float)(1 - Math.Exp(-Math.Max(0, Options.DynamicPanSpeed) * elapsed));
        PanFocus(pan * panAlpha);
    }

    public override void AdvanceZoom(double elapsed)
    {
        if (!double.IsFinite(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
        var zoomingOut = desiredZoom < Zoom;
        var speed = zoomingOut ? Options.DynamicZoomOutSpeed : Options.DynamicZoomInSpeed;
        var amount = (float)(1 - Math.Exp(-Math.Max(.01f, speed) * elapsed));
        var next = Zoom + (desiredZoom - Zoom) * amount;
        if (Math.Abs(desiredZoom - next) < .0001f) next = desiredZoom;
        Zoom = next;
    }
}
