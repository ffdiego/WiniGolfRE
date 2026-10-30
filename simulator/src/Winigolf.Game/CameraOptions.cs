namespace Winigolf.Game;

// Presentation defaults. Pass another instance to GameApp to change these choices.
internal sealed record CameraOptions
{
    public bool CenterOnBallAtTurnStart { get; init; } = true;
    public bool AllowTopDown { get; init; } = false;
    public bool AllowManualRotation { get; init; } = false;
    public bool AutoRotateBetweenShots { get; init; } = true;
    public bool KeepBallInFrame { get; init; } = true;
    public float EdgeMargin { get; init; } = .15f;
    public float FollowSpeed { get; init; } = 8;
}
