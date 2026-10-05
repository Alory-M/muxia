using Godot;
using System;

public partial class ColorRect : Godot.ColorRect
{
	private Player _player;
	private const float MaxWidth = 555f; // 满血时血条宽度

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_player = GetNode<Player>("../../player");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (_player == null || _player.MaxHp <= 0f) return;

		// 血量百分比（0~1）。上限以 Player.MaxHp 为准，Clamp 保证宽度不会超过 MaxWidth
		float percent = Mathf.Clamp(_player.hp / _player.MaxHp, 0f, 1f);

		// 按比例设置宽度
		Size = new Vector2(MaxWidth * percent, Size.Y);
	}
}
