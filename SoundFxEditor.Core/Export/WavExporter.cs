using System;
using System.IO;

namespace SoundFxEditor.Core.Export;

public static class WavExporter
{
    public static void Export(float[] pcm, int sampleRate, Stream output)
    {
        using var bw = new BinaryWriter(output, System.Text.Encoding.ASCII, leaveOpen: true);

        int numSamples = pcm.Length;
        int byteRate = sampleRate * 2; // 16-bit mono
        int dataSize = numSamples * 2;

        // RIFF header
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataSize);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        // fmt chunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);        // chunk size
        bw.Write((short)1); // PCM
        bw.Write((short)1); // mono
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((short)2); // block align
        bw.Write((short)16); // bits per sample
        // data chunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);
        foreach (var s in pcm)
        {
            short val = (short)Math.Clamp((int)(s * 32767), -32768, 32767);
            bw.Write(val);
        }
    }

    public static void Export(float[] pcm, int sampleRate, string outputPath)
    {
        using var fs = File.OpenWrite(outputPath);
        Export(pcm, sampleRate, fs);
    }
}
