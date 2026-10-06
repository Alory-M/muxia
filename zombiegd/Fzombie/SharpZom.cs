using Godot;

/// <summary>
/// 利爪僵尸（C# 版）：继承 Zombie 的全部基础行为，额外在玩家进入攻击范围时给玩家上「流血」。
/// 流血由玩家身上的 state 状态机（State.cs）驱动，底层组件是 debuff.cs。
/// </summary>
public partial class SharpZom : Zombie
{
	private bool _playerInRange;

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);

		if (!_active || _player == null)
		{
			return;
		}

		bool nowInRange = GlobalPosition.DistanceTo(_player.GlobalPosition) <= AttackRange;
		// 只在「刚进入范围」那一帧触发一次，避免每帧重复刷新流血时长导致倒计时不结算
		if (nowInRange && !_playerInRange)
		{
			ApplyBleed();
		}
		_playerInRange = nowInRange;
	}

	private void ApplyBleed()
	{
		if (_player == null)
		{
			return;
		}

		// 玩家身上的 state 状态机（State.cs）驱动 debuff.cs 实现流血，这里直接切状态。
		// 同在 C# 里，可以直接调 ChangeState(PlayerState.Bleed)，没有跨语言枚举转换问题。
		var state = _player.GetNodeOrNull<State>("state");
		if (state == null)
		{
			GD.PushWarning("SharpZom: 找不到 player/state 状态机，流血未生效");
			return;
		}

		state.ChangeState(PlayerState.Bleed);
		GD.Print("SharpZom：玩家进入攻击范围，施加流血");
	}
}
