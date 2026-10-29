using System.Text.Json;
using Raylib_cs;
using Winigolf.Physics;

namespace Winigolf.Game;

internal static class AudioChecks
{
    public static void Run(GameApp app, Action<bool, string> check)
    {
        var course = app.Session.Courses[0];
        var timeline = new SoundTimeline();
        var capture = Simulator.Run(course, Simulator.AtStart(course, new(10, 0, 0)), 6000,
            policy: CollisionPolicy.SurfaceTraversal);
        timeline.Start(course, capture);
        check(timeline.Drain(capture, 0).Single().Name == "hit", "Shot sound fires once at frame zero");
        check(timeline.Drain(capture, 0).Count == 0, "Repeated render at same frame never repeats a sound");
        var final = timeline.Drain(capture, capture.Steps);
        check(final.Count(c => c.Name == "applause") == 1 && final.All(c => c.Frame <= capture.Steps),
            "Capture below par schedules original applause exactly once");
        check(timeline.Drain(capture, capture.Steps).Count == 0, "Completed shot never repeats applause");
        var atPar = capture with { StrokeCount = course.Par };
        timeline.Start(course, atPar);
        check(timeline.Cues.Any(c => c.Name == "clap") && timeline.Cues.All(c => c.Name != "applause"),
            "Capture at par schedules original clap instead of applause");
        var penalty = Simulator.Run(course, Simulator.AtStart(course, new(.01, 0, 0)), 6000, 6,
            CollisionPolicy.SurfaceTraversal);
        timeline.Start(course, penalty);
        check(timeline.Cues.Count(c => c.Name == "laugh") == 1, "Stroke penalty schedules original laugh once");
        var fast = Simulator.Run(course, Simulator.AtStart(course, new(14, 0, 0)), 6000,
            policy: CollisionPolicy.SurfaceTraversal);
        timeline.Start(course, fast);
        check(timeline.Cues.Count(c => c.Name == "uh") == 1, "Fast pass over hole schedules original reaction once per shot");
        check(timeline.Cues.Count(c => c.Name == "bounce") == fast.Frames.Count(f => f.Events.Any(e => e.Kind == "WallBounce")),
            "Bounce sounds match collision frames and never surface portals");
        var once = timeline.Drain(fast, fast.Steps).ToArray();
        timeline.Start(course, fast);
        var parts = new List<SoundCue>();
        for (var frame = 0; frame <= fast.Steps; frame++) parts.AddRange(timeline.Drain(fast, frame));
        check(once.SequenceEqual(parts), "Sound scheduling is independent of playback frame batching");
        timeline.Start(course, null);
        check(timeline.Drain(null, 0).Count == 0, "Changing course clears all queued sounds");

        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "audio", "manifest.json")));
        using (manifest)
        {
            foreach (var sound in manifest.RootElement.GetProperty("sounds").EnumerateArray())
            {
                var name = Path.GetFileName(sound.GetProperty("file").GetString()!);
                var file = Path.Combine(AppContext.BaseDirectory, "audio", name);
                var bytes = File.ReadAllBytes(file);
                var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
                check(sha == sound.GetProperty("sha256").GetString(), $"{name} is byte-identical to extracted PE resource");
                var wave = Raylib.LoadWave(file);
                try { check(Raylib.IsWaveValid(wave) && wave.FrameCount > 0, $"Raylib decodes original {name}"); }
                finally { Raylib.UnloadWave(wave); }
            }
        }
        if (app.Audio.Ready)
            check(app.Audio.LoadedNames.Count == 6, "Real audio device initializes and all six native sounds load");
        check(app.Audio.Muted, "Hidden integration tests keep audio silent");
        // Mute must not block, modify or replay simulation.
        app.Select(0); app.Session.Shoot(new(10, 0, 0)); app.Audio.Update(app.Session);
        var shot = app.Session.Shot;
        app.Audio.ToggleMute(); app.Audio.ToggleMute();
        app.Session.Paused = true; app.Audio.Update(app.Session);
        app.Session.Advance(10);
        check(app.Session.FrameIndex == 0 && app.Session.Shot == shot, "Mute and audio pause never advance or replace physics");
        app.Session.Paused = false; app.Session.Advance(99); app.Audio.Update(app.Session);
        check(app.Session.Outcome == EndReason.Captured && app.Session.Shot == shot, "Muted shot still captures normally");
        app.Select(0);
    }
}
