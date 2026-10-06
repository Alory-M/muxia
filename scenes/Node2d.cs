using Godot;
using System;

public partial class Node2d : Node2D
{	
	private Sprite2D _Map;
	private bool _MapVisible = false;
	private Pack _pack;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_Map = GetNode<Sprite2D>("HUD/Map");
		_pack = GetNode<Pack>("player/pack");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("M"))
		{
			_MapVisible = !_MapVisible;
			_Map.Visible = _MapVisible;
		}
		if (Input.IsActionJustPressed("R"))
		{
			// 补弹 = 回到检查器里设的初始值,数值只在 pack 上维护一份
			_pack.Refill(SupplyKind.Bullet);
		}
	}
}
