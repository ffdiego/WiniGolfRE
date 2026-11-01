using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Winigolf.Game;

internal static class GolfBallIcon
{
    public static void ApplyToWindow()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "icon", "winigolf-ball.png");
        if (!File.Exists(path)) return;
        var image = LoadImage(path);
        if (image.Width <= 0 || image.Height <= 0) return;
        SetWindowIcon(image);
        UnloadImage(image);
    }
}
