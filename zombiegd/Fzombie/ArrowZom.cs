using Godot;

/// <summary>
/// 弓箭僵尸（C# 版）：远程攻击僵尸。玩家进入射程后，站定并朝玩家中心自动发射子弹（箭）。
/// 继承 Zombie 的全部基础行为（追击、掉落、棺材信号绑定等），只把「近战普攻」换成「远程射箭」。
/// </summary>
public partial class ArrowZom : Zombie
{
	// 子弹场景：在 Inspector 里拖入 res://scenes/bullet.tscn（外部给定）
	[Export] public PackedScene BulletScene { get; set; }

	// 射程：玩家进入这个距离就开始射箭（外部给定，比近战 attack_range 大很多）
	[Export] public float FireRange { get; set; } = 500f;

	// 射箭冷却（秒）
	[Export] public float FireCd { get; set; } = 1.5f;

	private float _fireTimer; // 射箭冷却计时器

	public override void _PhysicsProcess(double delta)
	{
		if (!_active || !EnsurePlayer())
		{
			return;
		}

		_fireTimer -= (float)delta;
		float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);

		if (dist <= FireRange)
		{
			// 玩家进入射程：站定，冷却好了就自动瞄准玩家中心射一箭
			Velocity = Vector2.Zero;
			if (_fireTimer <= 0f)
			{
				FireAtPlayer();
				_fireTimer = FireCd;
			}
		}
		else
		{
			// 超出射程：先追进射程（复用父类追击逻辑）
			Chase();
		}

		// 射箭 / 追击后都收回棺材碰撞箱内，别跑出活动范围
		ClampToCoffin();
	}

	// 自动瞄准玩家中心发射一箭
	private void FireAtPlayer()
	{
		if (BulletScene == null)
		{
			GD.PushWarning("ArrowZom: 没有指定 BulletScene，无法射箭。");
			return;
		}

		// 瞄准玩家中心：方向 = 僵尸 -> 玩家节点位置（玩家是 CharacterBody2D，原点即身体中心）
		Vector2 dir = _player.GlobalPosition - GlobalPosition;
		if (dir.LengthSquared() < 0.0001f)
		{
			return; // 和玩家完全重合，方向为零，不射
		}

		// 生成一颗子弹，挂到父节点下（别挂在僵尸下，否则会跟着僵尸跑），
		// 从僵尸位置出膛，朝玩家中心飞
		Bullet bullet = BulletScene.Instantiate<Bullet>();
		bullet.HitsPlayer = true;   // 这是僵尸射的箭，要打玩家（而不是打僵尸）
		GetParent().AddChild(bullet);
		bullet.GlobalPosition = GlobalPosition;
		bullet.Launch(dir);
		GD.Print("ArrowZom：朝玩家中心射出一箭");
	}
}
