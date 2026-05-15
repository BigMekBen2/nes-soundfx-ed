namespace SoundFxEditor.Core.Stamps;

using SoundFxEditor.Core.Models;

public static class SoundStamps
{
    public static SoundEffect Boing() => new()
    {
        Name = "Boing",
        Channels = MakeChannels(new SynthChannel
        {
            ChannelId = 0, IsActive = true,
            InitialPeriod = 400, PeriodSweepRate = -8, SweepDuration = 8,
            InitialVolume = 12, VolumeDecayPerFrame = 1,
            DutyCycle = 2, DurationFrames = 12
        })
    };

    public static SoundEffect Laser() => new()
    {
        Name = "Laser Shot",
        Channels = MakeChannels(new SynthChannel
        {
            ChannelId = 0, IsActive = true,
            InitialPeriod = 200, PeriodSweepRate = 6, SweepDuration = 5,
            InitialVolume = 15, VolumeDecayPerFrame = 2,
            DutyCycle = 1, DurationFrames = 8
        })
    };

    public static SoundEffect Gunshot() => new()
    {
        Name = "Gunshot",
        Channels = MakeChannels(new SynthChannel
        {
            ChannelId = 3, IsActive = true,
            InitialPeriod = 6, PeriodSweepRate = 1, SweepDuration = 20,
            InitialVolume = 15, VolumeDecayPerFrame = 0,
            DurationFrames = 30
        },
        new SynthChannel
        {
            ChannelId = 0, IsActive = true,
            InitialPeriod = 800, PeriodSweepRate = 15, SweepDuration = 6,
            InitialVolume = 8, VolumeDecayPerFrame = 2,
            DutyCycle = 0, DurationFrames = 8
        })
    };

    public static SoundEffect BreakingGlass() => new()
    {
        Name = "Breaking Glass",
        Channels = MakeChannels(new SynthChannel
        {
            ChannelId = 3, IsActive = true,
            InitialPeriod = 2, PeriodSweepRate = 1, SweepDuration = 10,
            InitialVolume = 15, VolumeDecayPerFrame = 1,
            ShortMode = true, DurationFrames = 18
        })
    };

    public static SoundEffect Explosion() => new()
    {
        Name = "Explosion",
        Channels = MakeChannels(
            new SynthChannel
            {
                ChannelId = 3, IsActive = true,
                InitialPeriod = 15, PeriodSweepRate = -1, SweepDuration = 20,
                InitialVolume = 15, VolumeDecayPerFrame = 0,
                DurationFrames = 36
            },
            new SynthChannel
            {
                ChannelId = 0, IsActive = true,
                InitialPeriod = 600, PeriodSweepRate = 10, SweepDuration = 10,
                InitialVolume = 10, VolumeDecayPerFrame = 1,
                DutyCycle = 0, DurationFrames = 12
            }
        )
    };

    private static SynthChannel[] MakeChannels(params SynthChannel[] active)
    {
        var channels = Enumerable.Range(0, 4).Select(i => new SynthChannel { ChannelId = i }).ToArray();
        foreach (var ch in active)
            channels[ch.ChannelId] = ch;
        return channels;
    }
}
