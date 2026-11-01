namespace Winigolf.Game;

// Presentation defaults. Pass another instance to GameApp to change these choices.
internal sealed record CameraOptions
{
    public bool CenterOnBallAtTurnStart { get; init; } = true;
    public bool AllowTopDown { get; init; } = false;
    public bool AllowManualRotation { get; init; } = false;
    public bool AutoRotateBetweenShots { get; init; } = true;
    public bool KeepBallInFrame { get; init; } = true;
    public bool DynamicBallAndHoleFraming { get; init; }
    public float EdgeMargin { get; init; } = .15f;
    public float FollowSpeed { get; init; } = 8;
    public float DynamicPanSpeed { get; init; } = 5;
    public float DynamicZoomOutSpeed { get; init; } = 10;
    public float DynamicZoomInSpeed { get; init; } = 2;
    public float DynamicZoomMin { get; init; } = .55f;
    public float DynamicZoomMax { get; init; } = 3;
    public float DynamicFrameOccupancy { get; init; } = .78f;
}
