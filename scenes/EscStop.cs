using Godot;

public partial class EscStop : Node2D
{
    private Stop _stop;
    public override void _Ready()
    {
        _stop = GetNode<Stop>("stop"); ZIndex = 100;
        AudioSettings.Ensure();
        var root = new Control { Theme = UiKit.Theme() }; AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        root.Size = GetViewportRect().Size;
        GetViewport().SizeChanged += () => root.Size = GetViewportRect().Size;
        var shade = new Godot.ColorRect { Color = new Color(0, 0, 0, 0.75f) }; root.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer(); root.AddChild(panel); UiKit.Center(panel, new Vector2(490, 460));
        var body = new VBoxContainer(); panel.AddChild(body);
        body.AddChild(UiKit.Label("暂停 · 设置", 28));
        body.AddChild(UiKit.Button("继续游戏", () => SetOpen(false)));
        body.AddChild(UiKit.Button("重新开始", () => ChangeScene("res://scenes/game.tscn")));
        body.AddChild(UiKit.Button("返回主菜单", () => ChangeScene("res://scenes/main.tscn")));
        AddVolume(body, "音乐音量", true); AddVolume(body, "音效音量", false);
        body.AddChild(UiKit.Button("退出游戏", () => GetTree().Quit()));
        var open = GetNodeOrNull<Button>("../base_set/escstop");
        if (open != null) open.Pressed += () => { if (!Stop.IsPaused || Visible) SetOpen(true); };
        SetOpen(false);
    }
    private static void AddVolume(VBoxContainer body, string title, bool music)
    {
        body.AddChild(UiKit.Label(title, 16));
        var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.01,
            Value = music ? AudioSettings.MusicVolume : AudioSettings.SfxVolume, CustomMinimumSize = new Vector2(0, 24) };
        body.AddChild(slider); slider.ValueChanged += value => AudioSettings.SetVolume(music, (float)value);
    }
    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey key && key.Echo) return;
        if (!ev.IsActionPressed("esc")) return;
        if (!Visible && Stop.IsPaused) return;
        SetOpen(!Visible); GetViewport().SetInputAsHandled();
    }
    public void SetOpen(bool open) { Visible = open; _stop?.SetPaused(open); }
    private void ChangeScene(string path) { SetOpen(false); GetTree().ChangeSceneToFile(path); }
}
