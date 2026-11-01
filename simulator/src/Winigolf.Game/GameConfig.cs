namespace Winigolf.Game;

// Runtime settings are passed as one instance. This can later be populated from IConfiguration.
public sealed record GameConfig
{
    public bool OnlineMode { get; init; } = true;
    public bool DynamicBallAndHoleFraming { get; init; } = true;
    public int Port { get; init; } = 9000;
    public double CourseAdvanceDelaySeconds { get; init; } = 2;
    public double CaptureDelaySeconds { get; init; } = 2;
    public double UnderParCaptureDelaySeconds { get; init; } = 3;
    public double RemoteAimSmoothingPerSecond { get; init; } = 18;

    public double DelayAfterCapture(int strokes, int par) =>
        strokes < par ? UnderParCaptureDelaySeconds : CaptureDelaySeconds;

    internal void Validate()
    {
        if (OnlineMode && Port is < 1024 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1024 and 65535.");
        ValidateDelay(CourseAdvanceDelaySeconds, nameof(CourseAdvanceDelaySeconds));
        ValidateDelay(CaptureDelaySeconds, nameof(CaptureDelaySeconds));
        ValidateDelay(UnderParCaptureDelaySeconds, nameof(UnderParCaptureDelaySeconds));
        if (!double.IsFinite(RemoteAimSmoothingPerSecond) || RemoteAimSmoothingPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(RemoteAimSmoothingPerSecond), "Smoothing rate must be finite and positive.");
    }

    private static void ValidateDelay(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(name, "Delay must be finite and nonnegative.");
    }
}
