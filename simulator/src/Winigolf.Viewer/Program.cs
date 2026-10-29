using Winigolf.Physics;

namespace Winigolf.Viewer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            var courses = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "courses"), "*.json")
                .Select(Course.Load).OrderBy(c => c.Id).ToArray();
            if (courses.Length != 36) throw new InvalidDataException("Esperados os 36 campos extraídos.");
            if (args.Length > 0 && args[0] == "--self-test")
                return ViewerChecks.Run(courses, args.Length > 1 ? args[1] : "artifacts/viewer");
            Application.Run(new LabForm(courses));
            return 0;
        }
        catch (Exception ex)
        {
            if (args.Length > 0) File.WriteAllText("viewer-error.txt", ex.ToString());
            else MessageBox.Show(ex.Message, "WiniGolf Lab", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
