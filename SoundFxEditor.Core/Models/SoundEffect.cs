namespace SoundFxEditor.Core.Models;

public class SoundEffect
{
    public string Name = "New Sound";
    public SynthChannel[] Channels = Enumerable.Range(0, 4)
        .Select(i => new SynthChannel { ChannelId = i })
        .ToArray();
}
