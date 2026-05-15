using System;

namespace SoundFxEditor.Core.Synthesis;

public class NoiseGenerator
{
    private uint _lfsr = 0x0001;
    private int _counter = 0;
    private int _period = 1;
    private int _output = 1;

    // NES noise frequency table (NTSC)
    private static readonly int[] PeriodTable = {
        4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068
    };

    public bool ShortMode { get; set; } = false;

    public void SetPeriodIndex(int index)
    {
        _period = PeriodTable[Math.Clamp(index, 0, 15)];
    }

    public void SetPitch(int noteIndex) // 0-15 maps to noise pitches
    {
        _period = PeriodTable[Math.Clamp(noteIndex % 16, 0, 15)];
    }

    public float Next(float amplitude, int sampleRate)
    {
        _counter++;
        if (_counter >= _period)
        {
            _counter = 0;
            uint feedback;
            if (ShortMode)
            {
                // XOR bits 0 and 6, insert at bit 6
                feedback = ((_lfsr >> 0) ^ (_lfsr >> 6)) & 1;
                _lfsr = (_lfsr >> 1) | (feedback << 6);
            }
            else
            {
                // XOR bits 0 and 1, insert at bit 14
                feedback = ((_lfsr >> 0) ^ (_lfsr >> 1)) & 1;
                _lfsr = (_lfsr >> 1) | (feedback << 14);
            }
            _output = (_lfsr & 1) == 0 ? 1 : -1;
        }
        return _output * amplitude;
    }

    public void Reset() { _lfsr = 0x0001; _counter = 0; }
}
