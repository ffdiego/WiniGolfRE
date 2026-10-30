using System.Numerics;
using Raylib_cs;
using Winigolf.Physics;
using static Raylib_cs.Raylib;
using P3 = Winigolf.Physics.Vector3;
using Vector3 = System.Numerics.Vector3;

namespace Winigolf.Game;

internal sealed class CourseScene
{
    private readonly record struct Triangle(Vector3 A, Vector3 B, Vector3 C, Color Color);
    private readonly List<Triangle> triangles = [];
    private readonly Dictionary<GameSession, BallVisual> ballVisuals = [];
    public int TriangleCount => triangles.Count;
    public int WallCount { get; private set; }
    private readonly List<(Vector3 A, Vector3 B)> seams = [];

    public void Build(Course course)
    {
        triangles.Clear(); seams.Clear(); WallCount = 0; ballVisuals.Clear();
        var floor = (float)course.Vertices.Values.Min(v => v.Z) - 24;
        var seenWalls = new HashSet<(int, int)>();
        foreach (var surface in course.Surfaces)
        {
            var points = surface.VertexIds.Select(id => SceneCamera.World(course.Vertices[id])).ToArray();
            var lift = Math.Clamp((int)surface.A.Z / 8, 0, 20);
            var green = new Color(92 + lift, 151 + lift, 113 + lift, 255);
            for (var i = 1; i < points.Length - 1; i++)
                triangles.Add(new(points[0], points[i + 1], points[i], green));
            for (var i = 0; i < points.Length; i++)
            {
                var a = points[i]; var b = points[(i + 1) % points.Length];
                if (surface.Neighbors[i] != 0)
                {
                    if (surface.Neighbors[i] > surface.Id) seams.Add((a, b));
                    continue;
                }
                var first = surface.VertexIds[i]; var second = surface.VertexIds[(i + 1) % points.Length];
                if (!seenWalls.Add((Math.Min(first, second), Math.Max(first, second)))) continue;
                WallCount++;
                Quad(a, b, new(b.X, floor, b.Z), new(a.X, floor, a.Z), new Color(47, 78, 68, 255));
                var along = Vector3.Normalize(b - a);
                var side = Vector3.Normalize(Vector3.Cross(along, Vector3.UnitY)) * 2;
                var up = Vector3.UnitY * 8;
                Quad(a - side, b - side, b - side + up, a - side + up, new Color(205, 197, 165, 255));
                Quad(b + side, a + side, a + side + up, b + side + up, new Color(175, 169, 143, 255));
                Quad(a - side + up, b - side + up, b + side + up, a + side + up, new Color(244, 232, 194, 255));
                Quad(a + side, a - side, a - side + up, a + side + up, new Color(219, 207, 174, 255));
                Quad(b - side, b + side, b + side + up, b - side + up, new Color(219, 207, 174, 255));
            }
        }
    }

    private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        // Two-sided, because an internal wall is visible from both neighboring regions.
        triangles.Add(new(a, b, c, color)); triangles.Add(new(a, c, d, color));
        triangles.Add(new(c, b, a, color)); triangles.Add(new(d, c, a, color));
    }

    public void Draw(GameSession session, SceneCamera camera, IReadOnlyList<LocalPlayer>? players = null)
    {
        BeginMode3D(camera.Value);
        foreach (var t in triangles) DrawTriangle3D(t.A, t.B, t.C, t.Color);
        foreach (var (a, b) in seams) DrawLine3D(a + Vector3.UnitY * .12f, b + Vector3.UnitY * .12f, new Color(151, 190, 148, 255));
        var target = SceneCamera.World(session.Course.Target);
        DrawCylinderEx(target + Vector3.UnitY * .15f, target + Vector3.UnitY * .35f, 8, 8, 32, new Color(12, 30, 31, 255));
        DrawCylinderEx(target, target + Vector3.UnitY * 38, .75f, .75f, 8, new Color(238, 234, 208, 255));
        var flagA = target + Vector3.UnitY * 38; var flagB = flagA + Vector3.UnitX * 20;
        var flagC = target + Vector3.UnitY * 24;
        var coral = new Color(244, 130, 106, 255);
        DrawTriangle3D(flagA, flagB, flagC, coral); DrawTriangle3D(flagC, flagB, flagA, coral);
        if (players is not null)
        {
            Rlgl.DisableDepthMask();
            foreach (var player in players.Where(p => !ReferenceEquals(p.Session, session) && p.Session.Outcome != EndReason.Captured)
                .OrderByDescending(p => Vector3.DistanceSquared(SceneCamera.World(p.Session.VisualPosition), camera.Value.Position)))
            {
                var center = SceneCamera.World(player.Session.VisualPosition) + Vector3.UnitY * 4;
                var color = player.Color; color.A = 90;
                DrawSphereEx(center, 4, 12, 16, color);
            }
            Rlgl.EnableDepthMask();
        }
        if (!ballVisuals.TryGetValue(session, out var ballVisual)) ballVisuals[session] = ballVisual = new();
        ballVisual.Draw(session, target, players?.FirstOrDefault(p => ReferenceEquals(p.Session, session))?.Color ?? new Color(248, 249, 233, 255));
        EndMode3D();
    }
}
