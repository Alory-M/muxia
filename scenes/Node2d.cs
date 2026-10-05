using Godot;
using System;

public partial class Node2d : Node2D
{	
	private Sprite2D _Map;
	private bool _MapVisible = false;
	private Player _player;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_Map = GetNode<Sprite2D>("HUD/Map");
		_player =GetNode<Player>("player");
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
			_player._BulletCounter=6;
		}
	}
}
