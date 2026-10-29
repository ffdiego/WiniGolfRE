using Winigolf.Physics;

namespace Winigolf.Game;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var courses = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "courses"), "*.json")
                .Select(Course.Load).OrderBy(c => c.Id).ToArray();
            if (courses.Length != 36) throw new InvalidDataException("São necessários os 36 campos extraídos.");
            var check = args.Length > 0 && args[0] == "--self-test";
            using var app = new GameApp(courses, hidden: check);
            if (check) return GameChecks.Run(app, args.Length > 1 ? args[1] : "artifacts/game");
            app.Run(); return 0;
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory("artifacts/game");
            File.WriteAllText("artifacts/game/error.txt", ex.ToString());
            return 1;
        }
    }
}
