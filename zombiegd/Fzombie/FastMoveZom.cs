using Godot;

/// <summary>
/// 瞬移僵尸（C# 版）。继承 Zombie 的全部基础行为，额外多出「瞬移贴脸攻击」。
/// 对应原来的 fast_move_zom.gd。
/// </summary>
public partial class FastMoveZom : Zombie
{
	[Export] public float TeleportTriggerRange { get; set; } = 300f; // 多远才会触发瞬移
	[Export] public float TeleportDistance { get; set; } = 40f;      // 瞬移后离玩家的距离（避免卡玩家身上）

	public override void _PhysicsProcess(double delta)
	{
		if (!_active || !EnsurePlayer())
		{
			return;
		}

		_attackTimer -= (float)delta;
		float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);

		// 1. 瞬移攻击：玩家在普攻距离外、但在瞬移触发范围内，且冷却好了
		if (dist > AttackRange && dist <= TeleportTriggerRange && _attackTimer <= 0f)
		{
			TeleportAttack();
		}
		// 2. 超出瞬移范围：正常追击（复用父类逻辑）
		else if (dist > TeleportTriggerRange)
		{
			Chase();
		}
		// 3. 已在攻击范围内：原地普攻（复用父类逻辑）
		else
		{
			MeleeAttack();
		}

		// 瞬移/追击后同样收回棺材碰撞箱内，别跑出活动范围
		ClampToCoffin();
	}

	// 沿接近方向瞬移到玩家身边，并立刻攻击
	private void TeleportAttack()
	{
		Vector2 dir = GlobalPosition - _player.GlobalPosition; // 从玩家指向僵尸的方向
		if (dir.Length() < 0.01f)
		{
			dir = Vector2.Right; // 几乎重合时随便选个方向，避免除零
		}
		else
		{
			dir = dir.Normalized();
		}

		// 保持在自己原本那一侧，贴到玩家附近，不跟玩家重叠
		GlobalPosition = _player.GlobalPosition + dir * TeleportDistance;
		Attack();
		_attackTimer = AttackCd;
	}
}
