namespace SoundFxEditor.Core.IO;
using System.Text.Json;
using SoundFxEditor.Core.Models;

public static class ProjectFile
{
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static void Save(SoundEffect sound, string path)
        => File.WriteAllText(path, JsonSerializer.Serialize(sound, Opts));

    public static SoundEffect Load(string path)
        => JsonSerializer.Deserialize<SoundEffect>(File.ReadAllText(path))
           ?? throw new InvalidDataException("Invalid project file.");
}
