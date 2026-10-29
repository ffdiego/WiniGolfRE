using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Winigolf.Physics;
using Winigolf.Simulator;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
try
{
    if (args.Length == 0 || args[0] is "help" or "--help")
    {
        Console.WriteLine("""
            Winigolf reference simulator (local console, no original executable required)
              verify [--data DIRECTORY]
              run --course ID (--velocity VX VY | --drag DX DY) --max-steps N [--output FILE.json]
                  [--position X Y Z --surface ID] [--mode 1|2] [--yaw DEG] [--tilt DEG] [--prior-strokes N]
              sweep --max-steps N [--output FILE.json] [--data DIRECTORY]
              audit [--output FILE.json] [--data DIRECTORY]  (experimental traversal geometry probes)
            run/sweep: optional --profile reference|traversal (default: reference).
            Run writes JSON trajectory plus CSV and SVG next to the requested output.
            """);
        return 0;
    }
    var options = new Options(args.Skip(1).ToArray());
    var data = options.Text("data", Path.Combine(AppContext.BaseDirectory, "courses"));
    CollisionPolicy Profile() => options.Text("profile", "reference") switch
    {
        "reference" => CollisionPolicy.ReferenceOrder,
        "traversal" => CollisionPolicy.SurfaceTraversal,
        _ => throw new ArgumentException("--profile must be reference or traversal.")
    };
    Course Load(int id)
    {
        if (id < 1 || id > 36) throw new ArgumentException("Course must be between 1 and 36.");
        return Course.Load(Path.Combine(data, $"{id:00}.json"));
    }
    switch (args[0])
    {
        case "verify":
            options.EnsureOnly("data", "output");
            var verification = Verification.Run(Load);
            if (options.Has("output")) Export.Json(options.Text("output"), verification);
            Console.WriteLine($"{verification.Passed}/{verification.Total} checks passed; {verification.Courses} courses loaded.");
            foreach (var failure in verification.Failures) Console.Error.WriteLine(failure);
            return verification.Failures.Count == 0 ? 0 : 1;
        case "run":
            options.EnsureOnly("data", "course", "velocity", "drag", "max-steps", "output", "position", "surface", "mode", "yaw", "tilt", "prior-strokes", "profile");
            var course = Load(options.Int("course"));
            if (options.Has("velocity") == options.Has("drag")) throw new ArgumentException("Supply either --velocity or --drag.");
            Vector3 velocity;
            if (options.Has("velocity"))
            {
                var v = options.Numbers("velocity", 2); velocity = new(v[0], v[1], 0);
            }
            else
            {
                var d = options.Numbers("drag", 2);
                velocity = Aim.FromDrag(d[0], d[1], (ShotMode)options.Int("mode", 1), options.Number("yaw", 0), options.Number("tilt", -60));
            }
            if (options.Has("position") != options.Has("surface")) throw new ArgumentException("Custom position requires --surface and vice versa.");
            var initial = Winigolf.Physics.Simulator.AtStart(course, velocity);
            if (options.Has("position"))
            {
                var p = options.Numbers("position", 3);
                initial = new(new(p[0], p[1], p[2]), velocity, options.Int("surface"));
            }
            var result = Winigolf.Physics.Simulator.Run(course, initial, options.Int("max-steps"), options.Int("prior-strokes", 0), Profile());
            if (options.Has("output")) Export.Trajectory(options.Text("output"), course, result);
            Console.WriteLine($"{course.Id:00} {course.Name}: {result.Reason}, {result.Steps} steps ({result.Steps * result.StepSeconds:F2} nominal seconds).");
            Console.WriteLine($"Position={result.FinalState.Position}; surface={result.FinalState.SurfaceId}; strokes={result.StrokeCount}; bounces={result.FinalState.BounceCount}.");
            return result.Reason is EndReason.SimulationLimitReached or EndReason.GeometryResolutionFailed ? 3 : 0;
        case "sweep":
            options.EnsureOnly("data", "max-steps", "output", "profile");
            var sweep = Sweep.Run(Load, options.Int("max-steps"), Profile());
            if (options.Has("output")) Export.Json(options.Text("output"), sweep);
            Console.WriteLine($"Sweep [{sweep.PhysicsVersion}]: {sweep.Shots} shots, {sweep.Errors.Count} execution errors, {sweep.ResolutionFailures} geometry failures, {sweep.LimitReached} step limits, {sweep.ShotsWithOutsideFrames} trajectories with surface-containment diagnostics.");
            return sweep.Errors.Count == 0 && sweep.ResolutionFailures == 0 ? 0 : 1;
        case "audit":
            options.EnsureOnly("data", "output");
            var audit = GeometryAudit.Run(Load);
            if (options.Has("output")) Export.Json(options.Text("output"), audit);
            Console.WriteLine($"Geometry audit [{audit.PhysicsVersion}]: {audit.Samples} probes, {audit.Failures} failures.");
            return audit.Failures == 0 ? 0 : 1;
        default: throw new ArgumentException("Unknown command. Use help.");
    }
}
catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException or JsonException or OverflowException)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

namespace Winigolf.Simulator
{
    internal sealed class Options
    {
        private readonly Dictionary<string, string[]> values = [];
        public Options(string[] args)
        {
            for (var i = 0; i < args.Length;)
            {
                if (!args[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Unexpected argument {args[i]}.");
                var key = args[i++][2..]; var start = i;
                while (i < args.Length && !args[i].StartsWith("--", StringComparison.Ordinal)) i++;
                if (!values.TryAdd(key, args[start..i]) || start == i) throw new ArgumentException($"Missing or duplicate --{key}.");
            }
        }
        public bool Has(string key) => values.ContainsKey(key);
        public string Text(string key, string? fallback = null) => values.TryGetValue(key, out var v)
            ? v.Length == 1 ? v[0] : throw new ArgumentException($"--{key} needs one value.")
            : fallback ?? throw new ArgumentException($"Missing --{key}.");
        public int Int(string key, int? fallback = null) => !Has(key) && fallback.HasValue ? fallback.Value
            : int.TryParse(Text(key), CultureInfo.InvariantCulture, out var n) ? n : throw new ArgumentException($"Invalid integer --{key}.");
        public double Number(string key, double fallback) => Has(key) ? Numbers(key, 1)[0] : fallback;
        public double[] Numbers(string key, int count)
        {
            if (!values.TryGetValue(key, out var raw) || raw.Length != count) throw new ArgumentException($"--{key} needs {count} values.");
            return raw.Select(s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n)
                ? n : throw new ArgumentException($"--{key} requires finite numbers using decimal point.")).ToArray();
        }
        public void EnsureOnly(params string[] allowed)
        {
            foreach (var key in values.Keys) if (!allowed.Contains(key)) throw new ArgumentException($"Unknown --{key}.");
        }
    }

    internal static class Export
    {
        internal static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
        public static void Json(string file, object value)
        {
            var absolute = Path.GetFullPath(file);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, JsonSerializer.Serialize(value, JsonOptions) + "\n");
        }
        public static void Trajectory(string file, Course course, ShotResult result)
        {
            if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Output must end in .json.");
            Json(file, result);
            using (var csv = new StreamWriter(Path.ChangeExtension(file, ".csv")))
            {
                csv.WriteLine("step,timeSeconds,x,y,z,vx,vy,surface,bounces,events");
                foreach (var frame in result.Frames)
                {
                    var s = frame.State;
                    csv.WriteLine(FormattableString.Invariant($"{frame.Step},{frame.Step * result.StepSeconds:R},{s.Position.X:R},{s.Position.Y:R},{s.Position.Z:R},{s.Velocity.X:R},{s.Velocity.Y:R},{s.SurfaceId},{s.BounceCount},{string.Join('|', frame.Events.Select(e => e.Kind))}"));
                }
            }
            Diagram.Write(Path.ChangeExtension(file, ".svg"), course, result);
            Console.WriteLine($"Written: {Path.GetFullPath(file)} (+ .csv, .svg)");
        }
    }
}
