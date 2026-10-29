using System.Text.Json;

namespace Winigolf.Physics;

public readonly record struct Vector3(double X, double Y, double Z)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
}

public sealed class Surface
{
    public int Id { get; }
    public IReadOnlyList<int> VertexIds { get; }
    public IReadOnlyList<int> Neighbors { get; }
    public Vector3 A { get; }
    public double SlopeX { get; }
    public double SlopeY { get; }
    public double AccelerationX { get; }
    public double AccelerationY { get; }
    public bool IsFlat => SlopeX == 0 && SlopeY == 0;

    internal Surface(int id, int[] vertices, int[] neighbors, IReadOnlyDictionary<int, Vector3> points)
    {
        Id = id;
        VertexIds = Array.AsReadOnly(vertices);
        Neighbors = Array.AsReadOnly(neighbors);
        A = points[vertices[0]];
        var b = points[vertices[1]];
        var c = points[vertices[2]];
        var dx = b.X - A.X;
        var dy = c.Y - b.Y;
        var dzx = b.Z - A.Z;
        var dzy = c.Z - b.Z;
        if (dx <= 0 || dy >= 0) throw new InvalidDataException($"Surface {id}: unsupported vertex ordering.");
        SlopeX = dzx / dx;
        SlopeY = dzy / dy;
        AccelerationX = -0.5 * dzx / Math.Sqrt(dx * dx + dzx * dzx);
        AccelerationY = 0.5 * dzy / Math.Sqrt(dy * dy + dzy * dzy);
        double twiceArea = 0;
        for (var i = 0; i < vertices.Length; i++)
        {
            var p = points[vertices[i]];
            var q = points[vertices[(i + 1) % vertices.Length]];
            var r = points[vertices[(i + 2) % vertices.Length]];
            if (Math.Abs(Height(p.X, p.Y) - p.Z) > 1e-9 ||
                (q.X - p.X) * (r.Y - q.Y) - (q.Y - p.Y) * (r.X - q.X) > 0)
                throw new InvalidDataException($"Surface {id}: nonplanar or nonconvex geometry.");
            twiceArea += p.X * q.Y - q.X * p.Y;
        }
        if (twiceArea >= 0) throw new InvalidDataException($"Surface {id}: invalid winding or area.");
    }

    public double Height(double x, double y) => A.Z + SlopeX * (x - A.X) + SlopeY * (y - A.Y);
}

public sealed class Course
{
    public int Id { get; }
    public string Name { get; }
    public string DataHash { get; }
    public int Par { get; }
    public Vector3 Start { get; }
    public Vector3 Target { get; }
    public int InitialSurface { get; }
    public IReadOnlyDictionary<int, Vector3> Vertices { get; }
    public IReadOnlyList<int> UnassignedVertices { get; }
    public IReadOnlyList<Surface> Surfaces { get; }

    private Course(int id, string name, string hash, int par, Vector3 start, Vector3 target,
        int initialSurface, Dictionary<int, Vector3> vertices, List<int> unassigned, Surface[] surfaces)
    {
        Id = id; Name = name; DataHash = hash; Par = par; Start = start; Target = target;
        InitialSurface = initialSurface;
        Vertices = new System.Collections.ObjectModel.ReadOnlyDictionary<int, Vector3>(vertices);
        UnassignedVertices = unassigned.AsReadOnly();
        Surfaces = Array.AsReadOnly(surfaces);
    }

    public Surface GetSurface(int id) => id >= 1 && id <= Surfaces.Count
        ? Surfaces[id - 1] : throw new ArgumentOutOfRangeException(nameof(id), "Unknown surface.");

    // Diagnostic containment; collision classification deliberately uses the original angular test.
    public bool Contains(int surfaceId, Vector3 point, double tolerance = 1e-8)
    {
        var s = GetSurface(surfaceId);
        for (var i = 0; i < s.VertexIds.Count; i++)
        {
            var a = Vertices[s.VertexIds[i]];
            var b = Vertices[s.VertexIds[(i + 1) % s.VertexIds.Count]];
            var cross = (b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X);
            if (cross > tolerance) return false;
        }
        return true;
    }

    public static Course Load(string file)
    {
        var bytes = File.ReadAllBytes(file);
        using var doc = JsonDocument.Parse(bytes);
        var root = doc.RootElement;
        static int Int(JsonElement value) => value.GetProperty("value").GetInt32();
        static Vector3 Point(JsonElement array)
        {
            var a = array.EnumerateArray().Select(v => v.GetProperty("value").GetDouble()).ToArray();
            if (a.Length != 3) throw new InvalidDataException("A point must have three coordinates.");
            var p = new Vector3(a[0], a[1], a[2]);
            if (!p.IsFinite) throw new InvalidDataException("Nonfinite point.");
            return p;
        }
        var points = new Dictionary<int, Vector3>();
        var unknown = new List<int>();
        var allIds = new HashSet<int>();
        foreach (var v in root.GetProperty("vertices").EnumerateArray())
        {
            var id = v.GetProperty("id").GetInt32();
            if (id <= 0 || !allIds.Add(id)) throw new InvalidDataException("Duplicate or invalid vertex ID.");
            var values = new[] { "x", "y", "z" }.Select(k => v.GetProperty(k).GetProperty("value")).ToArray();
            if (values.Any(n => n.ValueKind == JsonValueKind.Null)) { unknown.Add(id); continue; }
            var p = new Vector3(values[0].GetDouble(), values[1].GetDouble(), values[2].GetDouble());
            if (!p.IsFinite) throw new InvalidDataException("Nonfinite vertex.");
            points.Add(id, p);
        }
        if (allIds.Count != Int(root.GetProperty("vertexCount"))) throw new InvalidDataException("Vertex count mismatch.");
        var surfaces = root.GetProperty("surfaces").EnumerateArray().Select(s =>
        {
            var ids = s.GetProperty("vertices").EnumerateArray().Select(Int).ToArray();
            var neighbors = s.GetProperty("neighbors").EnumerateArray().Select(Int).ToArray();
            if (ids.Length < 3 || ids.Length > 8 || ids.Length != neighbors.Length ||
                ids.Length != Int(s.GetProperty("vertexCount")) || ids.Any(i => !points.ContainsKey(i)))
                throw new InvalidDataException("Invalid or unassigned surface vertices.");
            return new Surface(s.GetProperty("id").GetInt32(), ids, neighbors, points);
        }).ToArray();
        if (surfaces.Length != Int(root.GetProperty("surfaceCount")) ||
            surfaces.Where((s, i) => s.Id != i + 1).Any()) throw new InvalidDataException("Surface ID/count mismatch.");
        foreach (var s in surfaces)
        for (var i = 0; i < s.VertexIds.Count; i++)
        {
            var n = s.Neighbors[i];
            if (n == 0) continue;
            if (n < 1 || n > surfaces.Length) throw new InvalidDataException("Unknown neighbor.");
            var other = surfaces[n - 1];
            var a = s.VertexIds[i]; var b = s.VertexIds[(i + 1) % s.VertexIds.Count];
            if (!Enumerable.Range(0, other.VertexIds.Count).Any(j => other.VertexIds[j] == b &&
                other.VertexIds[(j + 1) % other.VertexIds.Count] == a && other.Neighbors[j] == s.Id))
                throw new InvalidDataException("Nonreciprocal passage.");
        }
        var course = new Course(root.GetProperty("field").GetInt32(), root.GetProperty("name").GetString()!,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),
            Int(root.GetProperty("par")), Point(root.GetProperty("start")), Point(root.GetProperty("target")),
            Int(root.GetProperty("initialSurface")), points, unknown, surfaces);
        if (!course.Contains(course.InitialSurface, course.Start)) throw new InvalidDataException("Start outside initial surface.");
        return course;
    }
}
