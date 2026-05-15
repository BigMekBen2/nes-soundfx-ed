using Xunit;
using SoundFxEditor.Core.Models;
using SoundFxEditor.Core.Stamps;
using SoundFxEditor.Core.Synthesis;
using SoundFxEditor.Core.Export;

public class SynthesizerTests
{
    [Fact]
    public void Pulse_ProducesNonZeroSamples()
    {
        var sound = SoundStamps.Boing();
        var samples = SoundSynthesizer.Synthesize(sound);
        Assert.True(samples.Any(s => s != 0f));
    }

    [Fact]
    public void Noise_ShortMode_DifferentFromLongMode()
    {
        // Use period index 2 (period=16 samples) to get enough LFSR clocks to diverge
        var soundLong = new SoundEffect();
        soundLong.Channels[3] = new SynthChannel
        {
            ChannelId = 3, IsActive = true,
            InitialPeriod = 2, InitialVolume = 15, VolumeDecayPerFrame = 0,
            ShortMode = false, DurationFrames = 4
        };
        var soundShort = new SoundEffect();
        soundShort.Channels[3] = new SynthChannel
        {
            ChannelId = 3, IsActive = true,
            InitialPeriod = 2, InitialVolume = 15, VolumeDecayPerFrame = 0,
            ShortMode = true, DurationFrames = 4
        };
        var samplesLong = SoundSynthesizer.Synthesize(soundLong);
        var samplesShort = SoundSynthesizer.Synthesize(soundShort);
        Assert.False(samplesLong.SequenceEqual(samplesShort));
    }

    [Fact]
    public void Volume_DecaysToZero()
    {
        var sound = new SoundEffect();
        sound.Channels[0] = new SynthChannel
        {
            ChannelId = 0, IsActive = true,
            InitialPeriod = 100, InitialVolume = 2, VolumeDecayPerFrame = 1,
            DutyCycle = 2, DurationFrames = 4
        };
        var samples = SoundSynthesizer.Synthesize(sound);
        // Last frame (frame 3) should have vol=max(0,2-3)=0, so all silence
        int lastFrameStart = 3 * (44100 / 60);
        var lastFrame = samples.Skip(lastFrameStart).ToArray();
        Assert.All(lastFrame, s => Assert.Equal(0f, s));
    }

    [Fact]
    public void WavExporter_ProducesValidRiffHeader()
    {
        var sound = SoundStamps.Boing();
        var samples = SoundSynthesizer.Synthesize(sound);
        using var ms = new MemoryStream();
        WavExporter.Export(samples, SoundSynthesizer.OutputSampleRate, ms);
        ms.Seek(0, SeekOrigin.Begin);
        var header = new byte[4];
        ms.Read(header, 0, 4);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(header));
        ms.Seek(8, SeekOrigin.Begin);
        ms.Read(header, 0, 4);
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(header));
    }
}
