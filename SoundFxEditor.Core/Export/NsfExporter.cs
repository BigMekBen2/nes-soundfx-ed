using System;
using System.Collections.Generic;
using System.IO;
using SoundFxEditor.Core.Models;

namespace SoundFxEditor.Core.Export;

/// <summary>
/// Exports a SoundEffect to NES Sound Format (NSF).
/// Layout:
///   File $0000–$007F : NSF header (128 bytes)
///   File $0080–$00FF : 6502 player code (128 bytes, NES $8000)
///   File $0100–$015F : APU timer lo table, 96 entries (NES $8080)
///   File $0160–$01BF : APU timer hi table, 96 entries (NES $80E0)
///   File $01C0–$01FF : padding to align song data to NES $8200 = file $0280
///   File $0280+      : frame data, 7 bytes/frame, terminated by 7×$FF
/// </summary>
public static class NsfExporter
{
    private const int LoadAddr     = 0x8000;
    private const int TimerLoAddr  = 0x8100;
    private const int TimerHiAddr  = 0x8160;
    private const int SongDataAddr = 0x8200;

    private static readonly byte[] PlayerCode = new byte[]
    {
        // ── INIT ($8000, 27 bytes) ────────────────────────────────────────
        0xA9, 0x0F, 0x8D, 0x15, 0x40,   // LDA #$0F, STA $4015  enable ch
        0xA9, 0x40, 0x8D, 0x17, 0x40,   // LDA #$40, STA $4017  frame ctr
        0xA9, 0x00, 0x85, 0x00,          // LDA #$00, STA $00    ptr lo
        0xA9, 0x82, 0x85, 0x01,          // LDA #$82, STA $01    ptr hi
        0xA9, 0xFF, 0x85, 0x02,          // LDA #$FF, STA $02    prev sq1
        0x85, 0x03,                      // STA $03              prev sq2
        0x85, 0x04,                      // STA $04              prev tri
        0x60,                            // RTS → ends at $801A

        // ── PLAY ($801B) ─────────────────────────────────────────────────
        // Square 1  (bytes 0=note, 1=vol)
        0xA0, 0x00,              // LDY #0
        0xB1, 0x00,              // LDA ($00),Y  note index
        0xC9, 0xFF,              // CMP #$FF
        0xF0, 0x21,              // BEQ sq1_sil
        0xAA,                    // TAX
        0xBD, 0x00, 0x81,        // LDA $8100,X  timer lo
        0x8D, 0x02, 0x40,        // STA $4002
        0x8A,                    // TXA
        0xC5, 0x02,              // CMP $02      same note?
        0xF0, 0x0A,              // BEQ sq1_vol
        0x86, 0x02,              // STX $02      save prev
        0xBD, 0x60, 0x81,        // LDA $8160,X  timer hi
        0x29, 0x07,              // AND #$07
        0x8D, 0x03, 0x40,        // STA $4003
        // sq1_vol:
        0xC8,                    // INY
        0xB1, 0x00,              // LDA ($00),Y  volume
        0x09, 0x30,              // ORA #$30
        0x8D, 0x00, 0x40,        // STA $4000
        0x4C, 0x4D, 0x80,        // JMP sq1_done
        // sq1_sil:
        0xA9, 0xFF,              // LDA #$FF
        0x85, 0x02,              // STA $02
        0xA9, 0x00,              // LDA #$00
        0x8D, 0x00, 0x40,        // STA $4000
        // sq1_done:

        // Square 2  (bytes 2=note, 3=vol)
        0xA0, 0x02,              // LDY #2
        0xB1, 0x00,
        0xC9, 0xFF,
        0xF0, 0x21,              // BEQ sq2_sil
        0xAA,
        0xBD, 0x00, 0x81,
        0x8D, 0x06, 0x40,        // STA $4006
        0x8A,
        0xC5, 0x03,              // CMP $03
        0xF0, 0x0A,              // BEQ sq2_vol
        0x86, 0x03,
        0xBD, 0x60, 0x81,
        0x29, 0x07,
        0x8D, 0x07, 0x40,        // STA $4007
        // sq2_vol:
        0xC8,
        0xB1, 0x00,
        0x09, 0x30,
        0x8D, 0x04, 0x40,        // STA $4004
        0x4C, 0x7F, 0x80,        // JMP sq2_done
        // sq2_sil:
        0xA9, 0xFF,
        0x85, 0x03,
        0xA9, 0x00,
        0x8D, 0x04, 0x40,
        // sq2_done:

        // Triangle  (byte 4=note, no volume)
        0xA0, 0x04,              // LDY #4
        0xB1, 0x00,
        0xC9, 0xFF,
        0xF0, 0x1E,              // BEQ tri_sil
        0xAA,
        0xBD, 0x00, 0x81,
        0x8D, 0x0A, 0x40,        // STA $400A
        0x8A,
        0xC5, 0x04,              // CMP $04
        0xF0, 0x1B,              // BEQ tri_done
        0x86, 0x04,
        0xBD, 0x60, 0x81,
        0x29, 0x07,
        0x8D, 0x0B, 0x40,        // STA $400B
        0xA9, 0x80,              // LDA #$80
        0x8D, 0x08, 0x40,        // STA $4008
        0x4C, 0xAE, 0x80,        // JMP tri_done
        // tri_sil:
        0xA9, 0xFF,
        0x85, 0x04,
        0xA9, 0x00,
        0x8D, 0x08, 0x40,        // STA $4008
        // tri_done:

        // Noise  (bytes 5=period-idx, 6=vol)
        0xA0, 0x05,              // LDY #5
        0xB1, 0x00,
        0xC9, 0xFF,
        0xF0, 0x10,              // BEQ noise_sil
        0x29, 0x0F,              // AND #$0F
        0x8D, 0x0E, 0x40,        // STA $400E
        0xC8,
        0xB1, 0x00,
        0x09, 0x30,
        0x8D, 0x0C, 0x40,        // STA $400C
        0x4C, 0xCB, 0x80,        // JMP noise_done
        // noise_sil:
        0xA9, 0x00,
        0x8D, 0x0C, 0x40,
        // noise_done:

        // Advance pointer by 7
        0x18,                    // CLC
        0xA5, 0x00,              // LDA $00
        0x69, 0x07,              // ADC #7
        0x85, 0x00,              // STA $00
        0x90, 0x02,              // BCC done
        0xE6, 0x01,              // INC $01
        // done:
        0x60,                    // RTS
    };

    public static void Export(SoundEffect sound, string outputPath)
    {
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        Export(sound, fs);
    }

    public static void Export(SoundEffect sound, Stream output)
    {
        var frames = BuildFrameSequence(sound);
        using var bw = new BinaryWriter(output, System.Text.Encoding.ASCII, leaveOpen: true);

        WriteHeader(bw, sound);

        var code = new byte[256];
        Array.Copy(PlayerCode, code, Math.Min(PlayerCode.Length, 256));
        bw.Write(code);

        for (int n = 0; n < 96; n++)
        {
            var (lo, _) = NoteToApuTimer(n);
            bw.Write(lo);
        }

        for (int n = 0; n < 96; n++)
        {
            var (_, hi) = NoteToApuTimer(n);
            bw.Write(hi);
        }

        // Padding to file $0280
        int padBytes = 0x0280 - (int)output.Position;
        if (padBytes > 0) bw.Write(new byte[padBytes]);

        foreach (var rec in frames)
            bw.Write(rec);

        bw.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
    }

    private static void WriteHeader(BinaryWriter bw, SoundEffect sound)
    {
        bw.Write(new byte[] { 0x4E, 0x45, 0x53, 0x4D, 0x1A }); // "NESM\x1A"
        bw.Write((byte)0x01);   // version
        bw.Write((byte)0x01);   // total songs
        bw.Write((byte)0x01);   // starting song

        bw.Write((ushort)LoadAddr);
        bw.Write((ushort)LoadAddr);
        bw.Write((ushort)0x801B); // play address

        WriteFixedString(bw, sound.Name ?? "Untitled", 32);
        WriteFixedString(bw, "", 32); // artist
        WriteFixedString(bw, "", 32); // copyright

        bw.Write((ushort)16666); // NTSC speed ~60Hz
        bw.Write(new byte[8]);   // bankswitch (none)
        bw.Write((ushort)0);     // PAL speed
        bw.Write((byte)0x00);    // NTSC only
        bw.Write((byte)0x00);    // extra chips: none
        bw.Write(new byte[4]);   // reserved
    }

    private static void WriteFixedString(BinaryWriter bw, string s, int len)
    {
        var buf = new byte[len];
        var bytes = System.Text.Encoding.ASCII.GetBytes(s);
        Array.Copy(bytes, buf, Math.Min(bytes.Length, len));
        bw.Write(buf);
    }

    private static (byte lo, byte hi) NoteToApuTimer(int noteIndex)
    {
        double freq = 440.0 * Math.Pow(2.0, (noteIndex - 69) / 12.0);
        int period = (int)Math.Round(1789773.0 / (16.0 * freq) - 1);
        period = Math.Clamp(period, 0, 0x7FF);
        return ((byte)(period & 0xFF), (byte)((period >> 8) & 0x07));
    }

    private static List<byte[]> BuildFrameSequence(SoundEffect sound)
    {
        int totalFrames = 0;
        foreach (var ch in sound.Channels)
            if (ch.IsActive) totalFrames = Math.Max(totalFrames, ch.DurationFrames);

        var result = new List<byte[]>(totalFrames);

        for (int f = 0; f < totalFrames; f++)
        {
            var rec = new byte[7];

            // Square 1 (ChannelId 0)
            {
                var ch = sound.Channels[0];
                if (ch.IsActive && f < ch.DurationFrames)
                {
                    int vol = Math.Max(0, ch.InitialVolume - ch.VolumeDecayPerFrame * f);
                    if (vol > 0)
                    {
                        int period = Math.Max(0, ch.InitialPeriod + ch.PeriodSweepRate * Math.Min(f, ch.SweepDuration));
                        double freq = 1789773.0 / (16.0 * (period + 1));
                        int noteIdx = (int)Math.Round(69 + 12 * Math.Log2(freq / 440.0));
                        noteIdx = Math.Clamp(noteIdx, 0, 95);
                        rec[0] = (byte)noteIdx;
                        rec[1] = (byte)Math.Clamp(vol, 0, 15);
                    }
                    else { rec[0] = 0xFF; rec[1] = 0; }
                }
                else { rec[0] = 0xFF; rec[1] = 0; }
            }

            // Square 2 (ChannelId 1)
            {
                var ch = sound.Channels[1];
                if (ch.IsActive && f < ch.DurationFrames)
                {
                    int vol = Math.Max(0, ch.InitialVolume - ch.VolumeDecayPerFrame * f);
                    if (vol > 0)
                    {
                        int period = Math.Max(0, ch.InitialPeriod + ch.PeriodSweepRate * Math.Min(f, ch.SweepDuration));
                        double freq = 1789773.0 / (16.0 * (period + 1));
                        int noteIdx = (int)Math.Round(69 + 12 * Math.Log2(freq / 440.0));
                        noteIdx = Math.Clamp(noteIdx, 0, 95);
                        rec[2] = (byte)noteIdx;
                        rec[3] = (byte)Math.Clamp(vol, 0, 15);
                    }
                    else { rec[2] = 0xFF; rec[3] = 0; }
                }
                else { rec[2] = 0xFF; rec[3] = 0; }
            }

            // Triangle (ChannelId 2)
            {
                var ch = sound.Channels[2];
                if (ch.IsActive && f < ch.DurationFrames)
                {
                    int vol = Math.Max(0, ch.InitialVolume - ch.VolumeDecayPerFrame * f);
                    if (vol > 0)
                    {
                        int period = Math.Max(0, ch.InitialPeriod + ch.PeriodSweepRate * Math.Min(f, ch.SweepDuration));
                        double freq = 1789773.0 / (16.0 * (period + 1));
                        int noteIdx = (int)Math.Round(69 + 12 * Math.Log2(freq / 440.0));
                        noteIdx = Math.Clamp(noteIdx, 0, 95);
                        rec[4] = (byte)noteIdx;
                    }
                    else rec[4] = 0xFF;
                }
                else rec[4] = 0xFF;
            }

            // Noise (ChannelId 3)
            {
                var ch = sound.Channels[3];
                if (ch.IsActive && f < ch.DurationFrames)
                {
                    int vol = Math.Max(0, ch.InitialVolume - ch.VolumeDecayPerFrame * f);
                    if (vol > 0)
                    {
                        int periodIdx = Math.Clamp(ch.InitialPeriod + ch.PeriodSweepRate * Math.Min(f, ch.SweepDuration), 0, 15);
                        rec[5] = (byte)periodIdx;
                        rec[6] = (byte)Math.Clamp(vol, 0, 15);
                    }
                    else { rec[5] = 0xFF; rec[6] = 0; }
                }
                else { rec[5] = 0xFF; rec[6] = 0; }
            }

            result.Add(rec);
        }

        return result;
    }
}
