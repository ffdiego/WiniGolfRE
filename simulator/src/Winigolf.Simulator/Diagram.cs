using System.Security;
using System.Text;
using Winigolf.Physics;

namespace Winigolf.Simulator;

internal static class Diagram
{
    public static void Write(string file, Course course, ShotResult result)
    {
        var points = course.Vertices.Values.Concat(result.Frames.Select(f => f.State.Position)).ToArray();
        var minX = points.Min(p => p.X); var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y); var maxY = points.Max(p => p.Y);
        var scale = Math.Min(850 / Math.Max(1, maxX - minX), 460 / Math.Max(1, maxY - minY));
        double X(double x) => 480 + (x - (minX + maxX) / 2) * scale;
        double Y(double y) => 335 - (y - (minY + maxY) / 2) * scale;
        static string N(double n) => n.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        string Point(Vector3 p) => $"{N(X(p.X))},{N(Y(p.Y))}";
        var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 960 650\" role=\"img\" aria-label=\"Simulated trajectory, plan view\"><rect width=\"960\" height=\"650\" fill=\"#fafafa\"/>");
        svg.Append($"<g font-family=\"sans-serif\" fill=\"#17202a\"><text x=\"25\" y=\"30\" font-size=\"21\">{course.Id:00} — {SecurityElement.Escape(course.Name)}: {result.Reason}</text>");
        svg.Append($"<text x=\"25\" y=\"54\" font-size=\"13\">{result.Steps} steps · {N(result.Steps * result.StepSeconds)} nominal seconds · surface {result.FinalState.SurfaceId} · bounces {result.FinalState.BounceCount}</text>");
        svg.Append("<text x=\"25\" y=\"76\" font-size=\"12\">Derived simulator output; not a recording of the original. Plan view x/y, positive y upwards.</text></g>");
        foreach (var surface in course.Surfaces)
        {
            var z = surface.VertexIds.Average(i => course.Vertices[i].Z);
            svg.Append($"<polygon points=\"{string.Join(' ', surface.VertexIds.Select(i => Point(course.Vertices[i])))}\" fill=\"hsl(200,40%,{N(87 - Math.Clamp(z, 0, 160) * .23)}%)\" stroke=\"#334155\" stroke-width=\"1.5\"/>");
            for (var i = 0; i < surface.VertexIds.Count; i++)
                if (surface.Neighbors[i] > surface.Id)
                    svg.Append($"<polyline points=\"{Point(course.Vertices[surface.VertexIds[i]])} {Point(course.Vertices[surface.VertexIds[(i + 1) % surface.VertexIds.Count]])}\" fill=\"none\" stroke=\"#e5a21b\" stroke-width=\"2\" stroke-dasharray=\"5 4\"/>");
        }
        svg.Append($"<polyline points=\"{string.Join(' ', result.Frames.Select(f => Point(f.State.Position)))}\" fill=\"none\" stroke=\"#c02554\" stroke-width=\"2.5\"/>");
        void Marker(Vector3 point, string color, int radius)
            => svg.Append($"<circle cx=\"{N(X(point.X))}\" cy=\"{N(Y(point.Y))}\" r=\"{radius}\" fill=\"{color}\" stroke=\"white\" stroke-width=\"1.5\"/>");
        Marker(course.Target, "#111827", 6);
        Marker(result.Frames[0].State.Position, "#16803c", 5);
        Marker(result.FinalState.Position, "#c02554", 4);
        svg.Append("<g font-family=\"sans-serif\" font-size=\"13\"><text x=\"25\" y=\"615\">Green: shot origin. Black: target. Red: trajectory/end. Dashed: passages. Darker surfaces: higher.</text></g></svg>\n");
        File.WriteAllText(file, svg.ToString());
    }
}
