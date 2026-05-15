namespace SoundFxEditor.Core.Models;

public class SynthChannel
{
    public int ChannelId { get; set; }        // 0=Pulse1, 1=Pulse2, 2=Triangle, 3=Noise
    public bool IsActive { get; set; }

    // Pitch
    public int InitialPeriod { get; set; }    // APU period value (0-2047 for pulse/triangle, 0-15 for noise)
    public int PeriodSweepRate { get; set; }  // signed, applied per frame (-5 to +5)
    public int SweepDuration { get; set; }    // frames before sweep stops

    // Volume
    public int InitialVolume { get; set; }    // 0-15
    public int VolumeDecayPerFrame { get; set; } // 0-15 (subtracted each frame)

    // Pulse-specific
    public int DutyCycle { get; set; }        // 0-3

    // Noise-specific
    public bool ShortMode { get; set; }       // LFSR short mode ($400E bit 7)

    // Timing
    public int DurationFrames { get; set; }
}
