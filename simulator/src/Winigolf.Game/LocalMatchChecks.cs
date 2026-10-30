using Winigolf.Physics;

namespace Winigolf.Game;

internal static class LocalMatchChecks
{
    public static void Run(Course[] courses, Action<bool, string> check)
    {
        var match = new LocalMatch(courses, [" Ana ", "Bruno", "Carla"]);
        check(match.Players[0].Name == "Ana" && match.Players.Select(p => p.Color).Distinct().Count() == 3,
            "Local roster trims names and assigns unique ball colors");
        match.Active.Session.Shoot(new(1, 0, 0));
        match.Advance(.01);
        check(match.ActiveIndex == 0, "Turn stays with active player while the ball is moving");
        match.Advance(20);
        var firstBall = match.Players[0].Session.Ball;
        check(match.ActiveIndex == 1 && match.Active.Session.Strokes == 0 && match.Players[0].Session.Strokes == 1,
            "Stopped shot passes turn and keeps each player's stroke count");
        match.Advance(20);
        check(match.ActiveIndex == 1, "Idle player never loses a turn without shooting");
        match.Active.Session.Shoot(new(2, 0, 0)); match.Advance(20);
        check(match.ActiveIndex == 2 && match.Players[0].Session.Ball == firstBall,
            "Inactive ball position survives other players' shots");
        match.Active.Session.Shoot(new(1, 0, 0)); match.Advance(20);
        check(match.ActiveIndex == 0 && match.Active.Session.Ball == firstBall,
            "Round-robin returns to the saved ball position");
        match.Select(0);
        match.Active.Session.Shoot(new(10, 0, 0));
        match.Advance(2.3);
        check(match.ActiveIndex == 0 && match.Active.Session.Outcome == EndReason.Captured,
            "Captured player keeps presentation turn while the ball drops");
        match.Active.Session.Paused = true; match.Advance(20);
        check(match.ActiveIndex == 0, "Pause prevents switching players during capture");
        match.Active.Session.Paused = false; match.Advance(20);
        check(match.ActiveIndex == 1 && match.Players[0].Total == 1 && match.Players[0].Session.Scores[1] == 1,
            "Capture records a single score before passing the turn");
        match.Active.Session.Shoot(new(10, 0, 0)); match.Advance(20);
        match.Active.Session.Shoot(new(1, 0, 0)); match.Advance(20);
        check(match.ActiveIndex == 2 && !match.CourseCompleted,
            "Completed players are skipped until the remaining player finishes");
        match.Select(0);
        for (var i = 0; i < 3; i++) { match.Active.Session.Shoot(new(10, 0, 0)); match.Advance(20); }
        check(match.CourseCompleted && match.Players.All(p => p.Total == 1),
            "Course finishes only after everyone completes it, with independent totals");
        match.Select(1);
        check(match.ActiveIndex == 0 && match.Players.All(p => p.Session.Index == 1 && p.Session.Strokes == 0 && p.Total == 1),
            "Next course resets all positions and preserves previous scores");
        match.SetPolicy(CollisionPolicy.ReferenceOrder);
        check(match.Players.All(p => p.Session.Scores.Count == 0 && p.Session.Policy == CollisionPolicy.ReferenceOrder),
            "Changing physics clears the scores for every player");
        var rejected = false;
        try { _ = new LocalMatch(courses, [" "]); } catch (ArgumentException) { rejected = true; }
        check(rejected, "Roster rejects empty player names");
    }
}
