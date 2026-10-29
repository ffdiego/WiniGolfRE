using Winigolf.Physics;

namespace Winigolf.Game;

// One visual transition for each ready-to-play state. Randomness never enters GameSession.
internal sealed class TurnPresentation(Random random)
{
    private Course? course;
    private ShotResult? shot;
    private bool started;

    public void Reset(SceneCamera camera)
    {
        course = null; shot = null; started = false; camera.CancelTurn();
    }

    public void Update(GameSession session, SceneCamera camera, double elapsed)
    {
        if (!ReferenceEquals(course, session.Course) || !ReferenceEquals(shot, session.Shot))
        {
            course = session.Course; shot = session.Shot; started = false; camera.CancelTurn();
        }
        if (!started && session.CanShoot)
        {
            started = true; camera.BeginTurn(random);
        }
        if (!session.Paused) camera.AdvanceTurn(elapsed);
    }
}
