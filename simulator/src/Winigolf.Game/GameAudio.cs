using Raylib_cs;
using Winigolf.Physics;
using static Raylib_cs.Raylib;

namespace Winigolf.Game;

internal readonly record struct SoundCue(int Frame, string Name);

// Pure scheduling over immutable physics frames; muting never touches the simulation.
internal sealed class SoundTimeline
{
    public IReadOnlyList<SoundCue> Cues { get; private set; } = [];
    private ShotResult? shot;
    private int cursor;

    public void Start(Course course, ShotResult? result)
    {
        shot = result; cursor = 0;
        if (result is null) { Cues = []; return; }
        var cues = new List<SoundCue> { new(0, "hit") };
        var nearMiss = false;
        foreach (var frame in result.Frames.Skip(1))
        {
            if (frame.Events.Any(e => e.Kind == "WallBounce")) cues.Add(new(frame.Step, "bounce"));
            var state = frame.State;
            if (!nearMiss && Math.Abs(state.Position.X - course.Target.X) < 8 && Math.Abs(state.Position.Y - course.Target.Y) < 8 &&
                (Math.Abs(state.Velocity.X) >= 2 || Math.Abs(state.Velocity.Y) >= 2))
            {
                cues.Add(new(frame.Step, "uh")); nearMiss = true;
            }
        }
        if (result.Reason == EndReason.Captured)
            cues.Add(new(result.Steps, result.StrokeCount < course.Par ? "applause" : "clap"));
        if (result.Penalty > 0) cues.Add(new(result.Steps, "laugh"));
        Cues = cues.OrderBy(c => c.Frame).ToArray();
    }

    public IReadOnlyList<SoundCue> Drain(ShotResult? result, int frame)
    {
        if (!ReferenceEquals(result, shot)) throw new InvalidOperationException("Sound timeline belongs to another shot.");
        var ready = new List<SoundCue>();
        while (cursor < Cues.Count && Cues[cursor].Frame <= frame) ready.Add(Cues[cursor++]);
        return ready;
    }
}

internal sealed class GameAudio : IDisposable
{
    public static readonly string[] Names = ["hit", "bounce", "applause", "clap", "laugh", "uh"];
    public bool Ready { get; }
    public bool Muted { get; private set; }
    public IReadOnlyList<string> LoadedNames => sounds.Keys.ToArray();
    private readonly Dictionary<string, Sound> sounds = [];
    private readonly SoundTimeline timeline = new();
    private readonly List<Sound> pausedSounds = [];
    private ShotResult? current;
    private bool paused;
    private bool disposed;

    public GameAudio(bool silent)
    {
        Muted = silent;
        InitAudioDevice(); Ready = IsAudioDeviceReady();
        if (!Ready) return; // No audio device must never prevent gameplay.
        foreach (var name in Names)
        {
            var file = Path.Combine(AppContext.BaseDirectory, "audio", name + ".wav");
            if (!File.Exists(file)) continue;
            var sound = LoadSound(file);
            if (!IsSoundValid(sound)) continue;
            SetSoundVolume(sound, name is "hit" or "bounce" ? .65f : .4f);
            sounds.Add(name, sound);
        }
    }

    public void ToggleMute()
    {
        Muted = !Muted;
        if (Muted) Stop();
    }

    public void Update(GameSession session)
    {
        if (!ReferenceEquals(current, session.Shot))
        {
            Stop(); current = session.Shot; timeline.Start(session.Course, current);
        }
        if (paused != session.Paused)
        {
            paused = session.Paused;
            if (paused)
            {
                foreach (var sound in sounds.Values)
                    if (IsSoundPlaying(sound)) { PauseSound(sound); pausedSounds.Add(sound); }
            }
            else
            {
                if (!Muted) foreach (var sound in pausedSounds) ResumeSound(sound);
                pausedSounds.Clear();
            }
        }
        if (session.Paused) return;
        foreach (var cue in timeline.Drain(current, session.FrameIndex))
        {
            // Consume muted and stale events too: unmuting must not replay old impacts.
            if (Muted || !Ready || (session.Time - cue.Frame * .02 > .15)) continue;
            if (sounds.TryGetValue(cue.Name, out var sound)) PlaySound(sound);
        }
    }

    private void Stop()
    {
        foreach (var sound in sounds.Values) StopSound(sound);
        pausedSounds.Clear(); paused = false;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Stop(); foreach (var sound in sounds.Values) UnloadSound(sound);
        sounds.Clear(); if (Ready) CloseAudioDevice();
    }
}
