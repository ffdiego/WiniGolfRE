using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Winigolf.Game;

internal sealed class BallVisual
{
    private const float Radius = 4;
    private Quaternion rotation = Quaternion.Identity;
    private Vector3? previous;
    private static readonly Vector3[] spots = Enumerable.Range(0, 28).Select(i =>
    {
        var y = 1 - 2 * (i + .5f) / 28;
        var r = MathF.Sqrt(1 - y * y);
        var angle = i * 2.39996323f;
        return new Vector3(r * MathF.Cos(angle), y, r * MathF.Sin(angle));
    }).ToArray();

    public void Reset() { previous = null; rotation = Quaternion.Identity; }

    public void Draw(GameSession session, Vector3 target)
    {
        var p = SceneCamera.World(session.VisualPosition);
        var progress = (float)session.CaptureProgress;
        if (previous is { } old && progress == 0)
        {
            var delta = p - old;
            if (delta.LengthSquared() > .000001f)
            {
                var axis = Vector3.Cross(Vector3.UnitY, delta);
                if (axis.LengthSquared() > .000001f)
                    rotation = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), delta.Length() / Radius) * rotation);
            }
        }
        previous = p;
        // Settle above the opening, then fall at increasing speed. Radius stays constant.
        var fall = Math.Clamp((progress - .35f) / .65f, 0, 1);
        var center = p + Vector3.UnitY * (Radius - 10 * fall * fall);
        if (fall == 0)
            DrawCylinderEx(p + Vector3.UnitY * .12f, p + Vector3.UnitY * .2f, 5.5f, 5.5f, 24, new Color(30, 64, 50, 255));
        if (progress >= 1) return;
        const int rings = 24, slices = 48;
        Vector3 Normal(int ring, int slice)
        {
            var latitude = MathF.PI * ring / rings;
            var longitude = 2 * MathF.PI * slice / slices;
            return new(MathF.Sin(latitude) * MathF.Cos(longitude), MathF.Cos(latitude), MathF.Sin(latitude) * MathF.Sin(longitude));
        }
        void Face(Vector3 a, Vector3 b, Vector3 c)
        {
            var normal = Vector3.Normalize(a + b + c);
            var dark = spots.Any(spot => Vector3.Dot(normal, spot) > .987f);
            var worldNormal = Vector3.Transform(normal, rotation);
            var light = .65f + .35f * MathF.Max(0, Vector3.Dot(worldNormal, Vector3.Normalize(new Vector3(-1, 2, 1))));
            var shade = dark ? 32 : (int)(248 * light);
            var color = new Color(shade, shade, dark ? shade : (int)(235 * light), 255);
            var vertices = new List<Vector3> { center + Radius * Vector3.Transform(a, rotation), center + Radius * Vector3.Transform(b, rotation), center + Radius * Vector3.Transform(c, rotation) };
            if (fall > 0)
            {
                // Clip at the mouth: the green hides the portion below its surface.
                var clipped = new List<Vector3>();
                var plane = target.Y + .36f;
                for (var i = 0; i < vertices.Count; i++)
                {
                    var v = vertices[i]; var next = vertices[(i + 1) % vertices.Count];
                    if (v.Y >= plane) clipped.Add(v);
                    if ((v.Y >= plane) != (next.Y >= plane))
                        clipped.Add(Vector3.Lerp(v, next, (plane - v.Y) / (next.Y - v.Y)));
                }
                vertices = clipped;
            }
            for (var i = 1; i + 1 < vertices.Count; i++)
            {
                DrawTriangle3D(vertices[0], vertices[i], vertices[i + 1], color);
                DrawTriangle3D(vertices[i + 1], vertices[i], vertices[0], color);
            }
        }
        for (var ring = 0; ring < rings; ring++)
        for (var slice = 0; slice < slices; slice++)
        {
            var a = Normal(ring, slice); var b = Normal(ring + 1, slice);
            var c = Normal(ring + 1, slice + 1); var d = Normal(ring, slice + 1);
            if (ring > 0) Face(a, b, d);
            if (ring < rings - 1) Face(d, b, c);
        }
    }
}
