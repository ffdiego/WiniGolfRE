using System.Text.Json;
using Winigolf.Physics;

namespace Winigolf.Viewer;

internal static class ViewerChecks
{
    private sealed class MouseView(LabSession session) : CourseView(session)
    {
        public void Down(Point p, MouseButtons button = MouseButtons.Left) => OnMouseDown(new MouseEventArgs(button, 1, p.X, p.Y, 0));
        public void MoveTo(Point p) => OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, p.X, p.Y, 0));
        public void Up(Point p, MouseButtons button = MouseButtons.Left) => OnMouseUp(new MouseEventArgs(button, 1, p.X, p.Y, 0));
    }

    public static int Run(Course[] courses, string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            checks.Add(description);
        }
        var basic = courses[0];
        var a = new LabSession(basic); var b = new LabSession(basic);
        a.Shoot(new Vector3(10, 0, 0)); b.Shoot(new Vector3(10, 0, 0));
        a.Advance(.6); for (var n = 0; n < 60; n++) b.Advance(.01);
        Check(a.Cursor == 30 && a.Cursor == b.Cursor && a.Ball == b.Ball, "Playback invariant under timer chunking");
        a.TogglePause(); var before = a.Ball; a.Advance(100);
        Check(a.Cursor == 30 && a.Ball == before, "Pause never advances simulation frames");
        a.StepOne(); Check(a.Cursor == 31 && a.Paused, "Single step advances exactly one frame and stays paused");
        a.Seek(int.MaxValue);
        Check(a.AtEnd && a.Outcome == EndReason.Captured && !a.CanShoot && a.Strokes == 1, "Captured ball cannot take another shot");
        var result = a.Shot;
        a.Replay(); a.Advance(1000); a.Replay(); a.Advance(1000);
        Check(a.Strokes == 1 && a.Shot == result, "Replay does not add strokes or recompute physics");
        a.Reset(); a.Shoot(new Vector3(1, 0, 0)); a.Advance(1000);
        Check(a.CanShoot && a.Strokes == 1, "Stopped ball allows next stroke");
        a.Shoot(new Vector3(1, 0, 0)); a.Advance(1000);
        Check(a.Strokes == 2, "A new stroke increments score once");
        a.Replay(); a.Advance(1000);
        Check(a.Strokes == 2, "Replaying a later stroke preserves previous strokes");
        var priorOrigin = a.Shot!.Frames[0].State;
        a.SetPolicy(CollisionPolicy.SurfaceTraversal);
        Check(a.Cursor == 0 && a.Shot!.Frames[0].State == priorOrigin && a.Strokes == 2,
            "Policy switch preserves shot origin and stroke number");
        Check(a.GetComparison()!.Frames[0].State == priorOrigin, "Overlay compares the identical initial state");
        a.Reset();
        for (var i = 0; i < 7; i++) { a.Shoot(new Vector3(.01, 0, 0)); a.Advance(100); }
        Check(a.Strokes == 9 && a.Outcome == EndReason.StrokeLimitReached && !a.CanShoot,
            "Seven unsuccessful shots apply penalty and block another shot");

        using (var mouse = new MouseView(new LabSession(basic)) { Size = new Size(900, 760) })
        {
            mouse.CreateControl();
            using var bitmap = new Bitmap(mouse.Width, mouse.Height);
            mouse.DrawToBitmap(bitmap, mouse.ClientRectangle);
            var start = Point.Round(mouse.BallScreen);
            mouse.Down(new Point(2, 2)); mouse.Up(new Point(3, 3));
            Check(mouse.Session.Shot is null, "Click outside ball cannot launch shot");
            mouse.Down(start); mouse.Up(start);
            Check(mouse.Session.Shot is null, "Zero-length drag cannot launch shot");
            mouse.Down(start); mouse.MoveTo(new Point(start.X - 25, start.Y)); mouse.CancelAim();
            mouse.Up(new Point(start.X - 25, start.Y));
            Check(mouse.Session.Shot is null, "Canceled drag cannot launch shot");
            mouse.Down(start); mouse.MoveTo(new Point(start.X - 25, start.Y)); mouse.Up(new Point(start.X - 25, start.Y));
            Check(mouse.Session.Shot is not null && mouse.Session.Shot.Frames[0].State.Velocity.X > 0,
                "Mouse pull to left launches ball to right through shared aim formula");
            var firstVelocity = mouse.Session.Shot!.Frames[0].State.Velocity;
            Check(mouse.ActiveMode == ShotMode.Mode1, "Left button selects precision mode rather than strong mode");
            mouse.Session.Reset(); mouse.Down(start, MouseButtons.Right);
            mouse.Up(new Point(start.X - 25, start.Y));
            Check(mouse.IsAiming && mouse.Session.Shot is null, "Release of other button does not launch a shot");
            mouse.Up(new Point(start.X - 25, start.Y), MouseButtons.Right);
            Check(mouse.ActiveMode == ShotMode.Mode2 &&
                Math.Abs(mouse.Session.Shot!.Frames[0].State.Velocity.X - firstVelocity.X * 8) < 1e-10,
                "Right button selects strong mode, eight times the unsaturated precision impulse");
            mouse.Session.Reset(); mouse.Size = new Size(1690, 1330);
            using var large = new Bitmap(mouse.Width, mouse.Height);
            mouse.DrawToBitmap(large, mouse.ClientRectangle);
            start = Point.Round(mouse.BallScreen);
            // Both canvas dimensions available for geometry have doubled, so scale doubles.
            mouse.Down(start); mouse.Up(new Point(start.X - 50, start.Y));
            Check(mouse.Session.Shot!.Frames[0].State.Velocity == firstVelocity, "Equivalent world-space drag is invariant under canvas resizing");
            mouse.Session.Reset(); mouse.Tilted = true;
            mouse.DrawToBitmap(large, mouse.ClientRectangle); start = Point.Round(mouse.BallScreen);
            mouse.Down(start); mouse.Up(new Point(start.X, start.Y + 25));
            Check(mouse.Session.Shot!.Frames[0].State.Velocity.Y > 0 && mouse.Session.Shot.Frames[0].State.Velocity.X == 0,
                "Inclined camera converts downward pull to positive world Y");
            mouse.Session.Reset(); mouse.Down(start); mouse.Size = new Size(900, 760);
            mouse.Up(new Point(start.X - 50, start.Y));
            Check(mouse.Session.Shot is null, "Resizing during a drag cancels it instead of changing its force");
            mouse.Tilted = false; mouse.DrawToBitmap(bitmap, mouse.ClientRectangle);
            start = Point.Round(mouse.BallScreen);
            var pixels = (int)Math.Round(mouse.Project(basic.Start with { X = basic.Start.X + 25 }).X - mouse.BallScreen.X);
            foreach (var policy in new[] { CollisionPolicy.ReferenceOrder, CollisionPolicy.SurfaceTraversal })
            {
                mouse.Session.Reset(); mouse.Session.SetPolicy(policy);
                mouse.Down(start, MouseButtons.Right); mouse.Up(new Point(start.X - pixels, start.Y), MouseButtons.Right);
                mouse.Session.Advance(1000);
                Check(mouse.Session.Outcome == EndReason.Captured && !mouse.BallVisible,
                    $"Actual mouse shot on Basic captures and hides ball under {policy}");
                mouse.DrawToBitmap(bitmap, mouse.ClientRectangle);
                Check(mouse.Session.Ball == mouse.Session.Shot!.FinalState, "Capture presentation never changes physical position or velocity");
                mouse.Session.Seek(mouse.Session.Cursor - 1);
                Check(mouse.BallVisible, "Seeking before capture restores ball visibility");
                mouse.Session.Replay(); Check(mouse.BallVisible, "Replay starts with a visible ball after capture");
            }
        }

        using (var form = new LabForm(courses, animate: false))
        {
            // Native ComboBox text needs a visible HWND for WM_PRINT; keep the test
            // window transparent, offscreen and absent from the taskbar.
            form.Opacity = 0; form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000);
            form.Show(); Application.DoEvents();
            static void Prepare(Control control)
            {
                _ = control.Handle;
                control.PerformLayout();
                foreach (Control child in control.Controls) Prepare(child);
            }
            Prepare(form);
            using var image = new Bitmap(form.Width, form.Height);
            for (var index = 0; index < courses.Length; index++)
            {
                form.SelectCourse(index);
                foreach (var tilted in new[] { false, true })
                {
                    form.View.Tilted = tilted;
                    form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                    using var canvas = new Bitmap(form.View.Width, form.View.Height);
                    form.View.DrawToBitmap(canvas, form.View.ClientRectangle);
                    var fieldPixels = 0;
                    for (var x = 0; x < canvas.Width; x += 8)
                        for (var y = 0; y < canvas.Height; y += 8)
                        {
                            var pixel = canvas.GetPixel(x, y);
                            if (pixel.G - pixel.R > 35 && pixel.G - pixel.B > 20) fieldPixels++;
                        }
                    Check(fieldPixels > 100 && float.IsFinite(form.View.BallScreen.X) && float.IsFinite(form.View.BallScreen.Y),
                        $"Course {index + 1:00} renders {(tilted ? "tilted" : "top-down")} including valid ball projection");
                }
            }
            form.SelectCourse(24); form.View.Tilted = false; form.Session.Demo();
            form.Session.Seek(int.MaxValue); form.SetOverlay(true); form.RefreshState();
            form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
            image.Save(Path.Combine(directory, "splitt.png"));
            form.SelectCourse(4); form.Session.Demo(); form.Session.Seek(int.MaxValue); form.RefreshState();
            form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(Path.Combine(directory, "plateau-reference.png"));
            form.SelectPolicy(CollisionPolicy.SurfaceTraversal); form.Session.Seek(int.MaxValue); form.RefreshState();
            form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(Path.Combine(directory, "plateau-traversal.png"));
            form.SelectCourse(0); form.SetOverlay(false); form.Session.Demo(); form.Session.Seek(int.MaxValue); form.RefreshState();
            form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(Path.Combine(directory, "basic-captured.png"));
        }
        File.WriteAllText(Path.Combine(directory, "checks.json"), JsonSerializer.Serialize(new
        {
            passed = checks.Count, failed = 0, checks,
            scope = "Viewer interaction, playback, offscreen rendering. Not dynamic validation against original Winigolf."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
