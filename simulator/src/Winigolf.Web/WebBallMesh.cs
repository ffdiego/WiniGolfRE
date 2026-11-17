using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Winigolf.Game;

// One immutable mesh for the life of the browser context.  The desktop renderer
// remains unchanged; capture uses its original clipped-triangle path.
internal static class WebBallMesh
{
    private static Model model;
    private static bool ready;

    public static void Draw(Vector3 center, Quaternion rotation, Color tint)
    {
        if (!ready) { model = Create(); ready = true; }
        rotation = Quaternion.Normalize(rotation);
        var halfAngle = MathF.Acos(Math.Clamp(rotation.W, -1, 1));
        var sin = MathF.Sin(halfAngle);
        var axis = MathF.Abs(sin) < .00001f ? Vector3.UnitY : new Vector3(rotation.X, rotation.Y, rotation.Z) / sin;
        DrawModelEx(model, center, axis, halfAngle * 2 * 180 / MathF.PI, Vector3.One, tint);
    }

    private static unsafe Model Create()
    {
        const int rings = 24, slices = 48;
        const int triangles = (rings - 1) * slices * 2 * 2;
        var mesh = new Mesh(triangles * 3, triangles);
        mesh.AllocVertices(); mesh.AllocColors();
        var spots = Enumerable.Range(0, 28).Select(i =>
        {
            var y = 1 - 2 * (i + .5f) / 28;
            var r = MathF.Sqrt(1 - y * y);
            var angle = i * 2.39996323f;
            return new Vector3(r * MathF.Cos(angle), y, r * MathF.Sin(angle));
        }).ToArray();
        var lightDirection = Vector3.Normalize(new Vector3(-1, 2, 1));
        var vertex = 0;
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
            var light = .65f + .35f * MathF.Max(0, Vector3.Dot(normal, lightDirection));
            byte shade = (byte)(dark ? 33 : (int)(255 * light));
            void Vertex(Vector3 p)
            {
                mesh.Vertices[vertex * 3] = p.X * 4;
                mesh.Vertices[vertex * 3 + 1] = p.Y * 4;
                mesh.Vertices[vertex * 3 + 2] = p.Z * 4;
                mesh.Colors[vertex * 4] = shade; mesh.Colors[vertex * 4 + 1] = shade;
                mesh.Colors[vertex * 4 + 2] = shade; mesh.Colors[vertex * 4 + 3] = 255;
                vertex++;
            }
            Vertex(a); Vertex(b); Vertex(c);
            Vertex(c); Vertex(b); Vertex(a);
        }
        for (var ring = 0; ring < rings; ring++)
        for (var slice = 0; slice < slices; slice++)
        {
            var a = Normal(ring, slice); var b = Normal(ring + 1, slice);
            var c = Normal(ring + 1, slice + 1); var d = Normal(ring, slice + 1);
            if (ring > 0) Face(a, b, d);
            if (ring < rings - 1) Face(d, b, c);
        }
        UploadMesh(ref mesh, false);
        return LoadModelFromMesh(mesh);
    }
}
