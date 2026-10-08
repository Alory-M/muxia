using Godot;
using System.Text.Json;

/// <summary>音乐与音效分开调节，保存在 user://，不会改动工程资源。</summary>
public static class AudioSettings
{
    private static bool _initialized;
    public static float MusicVolume { get; private set; } = 0.7f;
    public static float SfxVolume { get; private set; } = 0.8f;
    public static void Ensure()
    {
        if (_initialized) return;
        _initialized = true;
        foreach (string name in new[] { "Music", "Sfx" })
            if (AudioServer.GetBusIndex(name) < 0)
            { AudioServer.AddBus(); AudioServer.SetBusName(AudioServer.BusCount - 1, name); }
        if (FileAccess.FileExists("user://audio_settings.json"))
        {
            try
            {
                using var doc = JsonDocument.Parse(FileAccess.GetFileAsString("user://audio_settings.json"));
                if (doc.RootElement.TryGetProperty("music", out var music) && music.ValueKind == JsonValueKind.Number && music.TryGetSingle(out float mv))
                    MusicVolume = Mathf.Clamp(mv, 0, 1);
                if (doc.RootElement.TryGetProperty("sfx", out var sfx) && sfx.ValueKind == JsonValueKind.Number && sfx.TryGetSingle(out float sv))
                    SfxVolume = Mathf.Clamp(sv, 0, 1);
            }
            catch (JsonException) { GD.PushWarning("音量设置文件无效，恢复默认音量。"); }
        }
        Apply("Music", MusicVolume); Apply("Sfx", SfxVolume);
    }
    public static void SetVolume(bool music, float value)
    {
        Ensure(); value = Mathf.Clamp(value, 0, 1);
        if (music) MusicVolume = value; else SfxVolume = value;
        Apply(music ? "Music" : "Sfx", value);
        using var file = FileAccess.Open("user://audio_settings.json", FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(new { music = MusicVolume, sfx = SfxVolume }));
    }
    private static void Apply(string bus, float volume)
    {
        int index = AudioServer.GetBusIndex(bus);
        AudioServer.SetBusMute(index, volume <= 0);
        AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Mathf.Max(0.0001f, volume)));
    }
    public static void Route(Node root)
    {
        Ensure();
        foreach (Node child in root.GetChildren()) Route(child);
        if (root is AudioStreamPlayer audio)
        {
            string bus = audio.HasMeta(ScreenMusic.MusicMeta) || audio.Stream?.ResourcePath.EndsWith(".ogg") == true ? "Music" : "Sfx";
            if (audio.Bus != bus) audio.Bus = bus;
        }
        if (root is AudioStreamPlayer2D spatial)
        {
            string bus = spatial.HasMeta(ScreenMusic.MusicMeta) || spatial.Stream?.ResourcePath.EndsWith(".ogg") == true ? "Music" : "Sfx";
            if (spatial.Bus != bus) spatial.Bus = bus;
        }
    }
}
