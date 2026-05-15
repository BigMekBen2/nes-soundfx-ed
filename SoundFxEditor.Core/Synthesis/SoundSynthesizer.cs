namespace SoundFxEditor.Core.Synthesis;

using SoundFxEditor.Core.Models;

public class SoundSynthesizer
{
    private const int SampleRate = 44100;
    private const int FrameRate = 60;
    private const int SamplesPerFrame = SampleRate / FrameRate; // 735

    public static float[] Synthesize(SoundEffect sound)
    {
        int totalFrames = sound.Channels.Where(c => c.IsActive).Select(c => c.DurationFrames).DefaultIfEmpty(0).Max();
        if (totalFrames == 0) return Array.Empty<float>();

        var samples = new float[totalFrames * SamplesPerFrame];

        var sq1 = new SquareOscillator();
        var sq2 = new SquareOscillator();
        var tri = new TriangleOscillator();
        var noise = new NoiseGenerator();

        for (int frame = 0; frame < totalFrames; frame++)
        {
            int offset = frame * SamplesPerFrame;

            float[] chAmplitude = new float[4];
            float[] chFreq = new float[4];
            int[] chDuty = new int[4];

            for (int ch = 0; ch < 4; ch++)
            {
                var c = sound.Channels[ch];
                if (!c.IsActive || frame >= c.DurationFrames) continue;

                int vol = Math.Max(0, c.InitialVolume - c.VolumeDecayPerFrame * frame);
                chAmplitude[ch] = vol / 15f * 0.25f;

                int sweepFrames = Math.Min(frame, c.SweepDuration);
                int period = c.InitialPeriod + c.PeriodSweepRate * sweepFrames;
                period = Math.Max(0, period);

                if (ch == 3)
                {
                    noise.ShortMode = c.ShortMode;
                    noise.SetPeriodIndex(Math.Clamp(period, 0, 15));
                }
                else
                {
                    double freq = period > 0 ? 1789773.0 / (16.0 * (period + 1)) : 0;
                    chFreq[ch] = (float)freq;
                }
                chDuty[ch] = c.DutyCycle;
            }

            for (int s = 0; s < SamplesPerFrame; s++)
            {
                float mix = 0f;
                if (chAmplitude[0] > 0) mix += sq1.Next(chFreq[0], (byte)chDuty[0], chAmplitude[0], SampleRate);
                if (chAmplitude[1] > 0) mix += sq2.Next(chFreq[1], (byte)chDuty[1], chAmplitude[1], SampleRate);
                if (chAmplitude[2] > 0) mix += tri.Next(chFreq[2], chAmplitude[2], SampleRate);
                if (chAmplitude[3] > 0) mix += noise.Next(chAmplitude[3], SampleRate);
                samples[offset + s] = Math.Clamp(mix, -1f, 1f);
            }
        }

        return samples;
    }

    public const int OutputSampleRate = SampleRate;
}
