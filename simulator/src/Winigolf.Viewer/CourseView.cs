using System.ComponentModel;
using System.Drawing.Drawing2D;
using Winigolf.Physics;

namespace Winigolf.Viewer;

internal class CourseView : Control
{
    public LabSession Session { get; }
    public ShotMode ActiveMode { get; private set; } = ShotMode.Mode1;
    [DefaultValue(false)] public bool Tilted { get; set; }
    [DefaultValue(true)] public bool Labels { get; set; } = true;
    [DefaultValue(false)] public bool Overlay { get; set; }
    [DefaultValue(false)] public bool FullTrail { get; set; }
    public bool IsAiming { get; private set; }
    public event Action? Changed;
    private PointF anchor, endpoint;
    private MouseButtons aimButton;
    private float scale = 1, centerX, centerY, middleX, middleY;
    private readonly Font titleFont = new("Segoe UI", 22, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font smallFont = new("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
    private readonly Font labelFont = new("Segoe UI", 11, FontStyle.Bold, GraphicsUnit.Pixel);
    public PointF BallScreen => Project(Session.Ball.Position);
    public bool BallVisible => Session.Outcome != EndReason.Captured;

    public CourseView(LabSession session)
    {
        Session = session; DoubleBuffered = true; ResizeRedraw = true;
        BackColor = Color.FromArgb(13, 25, 34); Cursor = Cursors.Cross;
        SetStyle(ControlStyles.Selectable, true); TabStop = true;
    }

    private static float ProjectY(Vector3 p, bool tilted) => (float)(tilted ? .5 * p.Y + Math.Sqrt(3) / 2 * p.Z : p.Y);
    private void Fit()
    {
        var vertices = Session.Course.Vertices.Values;
        var minX = vertices.Min(v => v.X); var maxX = vertices.Max(v => v.X);
        var minY = vertices.Min(v => ProjectY(v, Tilted)); var maxY = vertices.Max(v => ProjectY(v, Tilted));
        scale = (float)Math.Min(Math.Max(1, Width - 110) / Math.Max(1, maxX - minX),
            Math.Max(1, Height - 190) / Math.Max(1, maxY - minY));
        middleX = (float)((minX + maxX) / 2); middleY = (minY + maxY) / 2;
        centerX = Width / 2f; centerY = Height / 2f + 18;
    }

    public PointF Project(Vector3 point) => new(centerX + ((float)point.X - middleX) * scale,
        centerY - (ProjectY(point, Tilted) - middleY) * scale);

    public void CancelAim()
    {
        IsAiming = false; Capture = false; Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus(); Fit();
        if (e.Button is not (MouseButtons.Left or MouseButtons.Right) || !Session.CanShoot || IsAiming) return;
        var ball = BallScreen;
        if (Math.Pow(e.X - ball.X, 2) + Math.Pow(e.Y - ball.Y, 2) > 22 * 22) return;
        aimButton = e.Button;
        ActiveMode = e.Button == MouseButtons.Right ? ShotMode.Mode2 : ShotMode.Mode1;
        anchor = e.Location; endpoint = anchor; IsAiming = true; Capture = true; Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!IsAiming) return;
        endpoint = e.Location; Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!IsAiming || e.Button != aimButton) return;
        endpoint = e.Location;
        var dx = (endpoint.X - anchor.X) / scale;
        var dy = (endpoint.Y - anchor.Y) / scale;
        CancelAim();
        if (dx * dx + dy * dy < 1) return;
        Session.Shoot(Aim.FromDrag(dx, dy, ActiveMode, tiltDegrees: Tilted ? -60 : 0));
        Changed?.Invoke();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture) { IsAiming = false; Invalidate(); }
    }

    protected override void OnSizeChanged(EventArgs e) { CancelAim(); base.OnSizeChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); Fit();
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        using var grid = new Pen(Color.FromArgb(23, 40, 50));
        for (var x = 24; x < Width; x += 32) g.DrawLine(grid, x, 85, x, Height - 55);
        for (var y = 85; y < Height - 55; y += 32) g.DrawLine(grid, 24, y, Width - 24, y);
        using var white = new SolidBrush(Color.FromArgb(229, 238, 241));
        using var muted = new SolidBrush(Color.FromArgb(133, 160, 175));
        var course = Session.Course;
        g.DrawString($"{course.Id:00}  {course.Name}", titleFont, white, 26, 20);
        g.DrawString($"PAR {course.Par}     /     {course.Surfaces.Count} superfícies     /     {(Tilted ? "vista inclinada" : "vista de cima")}", smallFont, muted, 28, 53);

        var surfaces = course.Surfaces.OrderBy(s => Tilted
            ? s.VertexIds.Average(id => -.866 * course.Vertices[id].Y + .5 * course.Vertices[id].Z) : 0);
        foreach (var surface in surfaces)
        {
            var points = surface.VertexIds.Select(id => Project(course.Vertices[id])).ToArray();
            var brightness = Math.Clamp((int)(surface.A.Z * .25), -25, 30);
            using var fill = new SolidBrush(Color.FromArgb(102 + brightness, 153 + brightness, 119 + brightness));
            using var outline = new Pen(Color.FromArgb(160, 194, 157), 1);
            g.FillPolygon(fill, points); g.DrawPolygon(outline, points);
            if (surface.Id == Session.Ball.SurfaceId)
            {
                using var active = new Pen(Color.FromArgb(129, 228, 193), 2);
                g.DrawPolygon(active, points);
            }
        }
        foreach (var surface in course.Surfaces)
        {
            for (var edge = 0; edge < surface.VertexIds.Count; edge++)
            {
                var a = Project(course.Vertices[surface.VertexIds[edge]]);
                var b = Project(course.Vertices[surface.VertexIds[(edge + 1) % surface.VertexIds.Count]]);
                if (surface.Neighbors[edge] == 0)
                {
                    using var shade = new Pen(Color.FromArgb(38, 59, 57), 7) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    using var wall = new Pen(Color.FromArgb(235, 224, 188), 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    g.DrawLine(shade, a, b); g.DrawLine(wall, a, b);
                }
                else if (Labels && surface.Neighbors[edge] > surface.Id)
                {
                    using var portal = new Pen(Color.FromArgb(180, 235, 221, 147), 1) { DashStyle = DashStyle.Dash };
                    g.DrawLine(portal, a, b);
                }
            }
            if (Labels)
            {
                var points = surface.VertexIds.Select(id => Project(course.Vertices[id])).ToArray();
                var center = new PointF(points.Average(p => p.X), points.Average(p => p.Y));
                using var label = new SolidBrush(Color.FromArgb(225, 236, 232));
                g.DrawString($"S{surface.Id}", labelFont, label, center.X - 10, center.Y - 7);
            }
        }

        if (Overlay && Session.GetComparison() is { } other)
            DrawTrail(g, other.Frames, other.Frames.Count - 1, Color.FromArgb(240, 182, 86), true);
        if (Session.Shot is { } shot)
            DrawTrail(g, shot.Frames, FullTrail ? shot.Frames.Count - 1 : Session.Cursor, Color.FromArgb(97, 215, 237), false);

        var start = Project(course.Start); var target = Project(course.Target);
        using var startPen = new Pen(Color.FromArgb(210, 235, 223), 1);
        g.DrawEllipse(startPen, start.X - 8, start.Y - 8, 16, 16);
        using var hole = new SolidBrush(Color.FromArgb(9, 22, 26));
        var radius = Math.Max(4, 8 * scale);
        var verticalRadius = Tilted ? radius / 2 : radius;
        g.FillEllipse(hole, target.X - radius, target.Y - verticalRadius, 2 * radius, 2 * verticalRadius);
        if (Labels)
        {
            // The recovered capture test is an axis-aligned square, not a circle.
            var t = course.Target;
            using var capture = new Pen(Color.FromArgb(235, 224, 188), 1) { DashStyle = DashStyle.Dot };
            g.DrawPolygon(capture, new PointF[]
            {
                Project(t with { X = t.X - 8, Y = t.Y - 8 }), Project(t with { X = t.X - 8, Y = t.Y + 8 }),
                Project(t with { X = t.X + 8, Y = t.Y + 8 }), Project(t with { X = t.X + 8, Y = t.Y - 8 })
            });
        }
        using var flag = new SolidBrush(Color.FromArgb(250, 129, 112));
        using var pole = new Pen(Color.FromArgb(235, 224, 188), 2);
        g.DrawLine(pole, target.X, target.Y, target.X, target.Y - 28);
        g.FillPolygon(flag, new PointF[] { new(target.X, target.Y - 28), new(target.X + 17, target.Y - 23), new(target.X, target.Y - 17) });

        var ball = BallScreen;
        using var shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0));
        if (BallVisible)
        {
            g.FillEllipse(shadow, ball.X - 5, ball.Y - 2, 13, 10);
            g.FillEllipse(white, ball.X - 5, ball.Y - 5, 10, 10);
        }
        else
        {
            using var success = new SolidBrush(Color.FromArgb(129, 228, 193));
            using var ring = new Pen(Color.FromArgb(129, 228, 193), 3);
            g.DrawEllipse(ring, target.X - radius - 4, target.Y - verticalRadius - 4, 2 * radius + 8, 2 * verticalRadius + 8);
            g.DrawString("EMBOCOU!", titleFont, success, Width / 2f - 60, Height - 105);
        }
        if (IsAiming)
        {
            var dx = endpoint.X - anchor.X; var dy = endpoint.Y - anchor.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            var limit = (ActiveMode == ShotMode.Mode1 ? 250 : 50) * scale;
            var factor = length > limit ? (float)(limit / length) : 1;
            using var aim = new Pen(Color.FromArgb(250, 196, 106), ActiveMode == ShotMode.Mode1 ? 1 : 3) { DashStyle = DashStyle.Dash };
            g.DrawLine(aim, ball, new PointF(ball.X + dx * factor, ball.Y + dy * factor));
            using var arrow = new Pen(Color.FromArgb(250, 196, 106), ActiveMode == ShotMode.Mode1 ? 2 : 4) { EndCap = LineCap.ArrowAnchor };
            g.DrawLine(arrow, ball, new PointF(ball.X - dx * factor, ball.Y - dy * factor));
            g.DrawString($"{(ActiveMode == ShotMode.Mode1 ? "PRECISÃO" : "FORTE")} · PUXADA {Math.Min(100, length / limit * 100):0}%", smallFont, white, 28, Height - 72);
        }
        var message = Session.CanShoot ? "Puxe a bola: esquerdo = precisão · direito = forte · Esc cancela" :
            Session.AtEnd ? "Tacada concluída. Repita, compare os perfis ou reinicie." : "Espaço: pausar   ·   →: avançar um passo";
        g.DrawString(message, smallFont, muted, 28, Height - 42);
        g.DrawString($"X →   Y ↑     |     escala {scale:0.00}×", smallFont, muted, 28, Height - 24);
        if (Overlay) g.DrawString("Ciano: perfil atual    Âmbar: outro perfil (trajetória completa)", smallFont, white, 28, 78);
    }

    private void DrawTrail(Graphics g, IReadOnlyList<Frame> frames, int until, Color color, bool dashed)
    {
        if (until < 1) return;
        using var pen = new Pen(color, dashed ? 2 : 2.5f) { DashStyle = dashed ? DashStyle.Dash : DashStyle.Solid };
        var points = frames.Take(until + 1).Select(f => Project(f.State.Position)).ToArray();
        g.DrawLines(pen, points);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { titleFont.Dispose(); smallFont.Dispose(); labelFont.Dispose(); }
        base.Dispose(disposing);
    }
}
