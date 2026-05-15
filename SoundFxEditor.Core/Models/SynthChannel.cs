namespace SoundFxEditor.Core.Models;

public class SynthChannel
{
    public int ChannelId;        // 0=Pulse1, 1=Pulse2, 2=Triangle, 3=Noise
    public bool IsActive;

    // Pitch
    public int InitialPeriod;    // APU period value (0-2047 for pulse/triangle, 0-15 for noise)
    public int PeriodSweepRate;  // signed, applied per frame (-5 to +5)
    public int SweepDuration;    // frames before sweep stops

    // Volume
    public int InitialVolume;    // 0-15
    public int VolumeDecayPerFrame; // 0-15 (subtracted each frame)

    // Pulse-specific
    public int DutyCycle;        // 0-3

    // Noise-specific
    public bool ShortMode;       // LFSR short mode ($400E bit 7)

    // Timing
    public int DurationFrames;
}
