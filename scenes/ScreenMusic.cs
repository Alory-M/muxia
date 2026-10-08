using Godot;
using System;

/// <summary>每个界面持有自己的循环音乐；切换时停止上一首，使用独立 Music 总线。</summary>
public static class ScreenMusic
{
    public const string MusicMeta = "screen_music";

    public static AudioStreamPlayer Start(Node owner, string path, string name = "ScreenBgm")
    {
        AudioSettings.Ensure();
        AudioStream stream = GD.Load<AudioStream>(path)?.Duplicate() as AudioStream;
        if (stream == null) { GD.PushWarning($"无法加载界面 BGM：{path}"); return null; }
        if (stream is AudioStreamOggVorbis ogg) ogg.Loop = true;
        if (stream is AudioStreamWav wav)
        {
            wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            wav.LoopBegin = 0;
            wav.LoopEnd = (int)Math.Round(wav.GetLength() * wav.MixRate);
        }

        StopAll(owner.GetTree().Root);
        var player = owner.GetNodeOrNull<AudioStreamPlayer>(name);
        if (player == null) { player = new AudioStreamPlayer { Name = name }; owner.AddChild(player); }
        player.SetMeta(MusicMeta, true);
        player.Bus = "Music";
        player.Stream = stream;
        player.Play();
        return player;
    }

    public static void StopAll(Node root)
    {
        foreach (Node child in root.GetChildren()) StopAll(child);
        if (root is AudioStreamPlayer player && player.Bus == "Music") player.Stop();
        if (root is AudioStreamPlayer2D spatial && spatial.Bus == "Music") spatial.Stop();
    }

    public static void Release(AudioStreamPlayer player)
    {
        if (!GodotObject.IsInstanceValid(player)) return;
        player.Stop();
        player.Stream = null;
    }
}
