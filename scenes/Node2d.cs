using Godot;
using System;

public partial class Node2d : Node2D
{	
	private Sprite2D _Map;
	private bool _MapVisible = false;
	private Pack _pack;
    public override void _EnterTree() => AudioSettings.Ensure();
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_Map = GetNode<Sprite2D>("HUD/Map");
        _Map.Visible = false;
		_pack = GetNode<Pack>("player/pack");
        AddChild(new InteractionController { Name = "Interactions" });
        AddChild(new ExpeditionHud { Name = "ExpeditionHud" });
        AudioSettings.Route(this);
        // 同一动作的键盘替代键不改变现有鼠标操作。
        AddKey("mouse_press", Key.J);
        AddKey("mouse_press2", Key.Shift);
	}

    private static void AddKey(string action, Key key)
    {
        var ev = new InputEventKey { PhysicalKeycode = key };
        if (!InputMap.ActionHasEvent(action, ev)) InputMap.ActionAddEvent(action, ev);
    }
    public override void _ExitTree()
    {
        var bgm = GetNodeOrNull<AudioStreamPlayer>("bgm");
        if (bgm != null) { bgm.Stop(); bgm.Stream = null; }
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (!Stop.IsPaused && Input.IsActionJustPressed("M"))
		{
			_MapVisible = !_MapVisible;
			_Map.Visible = _MapVisible;
		}
		if (!Stop.IsPaused && Input.IsActionJustPressed("R"))
		{
			// 换弹 = 补满弹匣,子弹从 pack 的备用数(_totalBullet)里扣,
			// 具体补多少、够不够都由 pack 自己判断
			_pack.ReloadBullets();
		}
	}
}
