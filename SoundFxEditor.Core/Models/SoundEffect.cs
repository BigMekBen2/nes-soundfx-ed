namespace SoundFxEditor.Core.Models;

public class SoundEffect
{
    public string Name { get; set; } = "New Sound";
    public SynthChannel[] Channels { get; set; } = Enumerable.Range(0, 4)
        .Select(i => new SynthChannel { ChannelId = i })
        .ToArray();
}
