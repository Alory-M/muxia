using Godot;

/// <summary>
/// 毒僵尸（C# 版）：在弓箭僵尸的基础上，射击参数与行为完全一致，
/// 额外在玩家进入射程时给玩家上「迟缓」（PlayerState.Slow，由 State.cs 驱动 debuff.cs 实现）。
/// </summary>
public partial class PoisonZom : ArrowZom
{
	private bool _playerInRange;

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta); // 复用弓箭僵尸的射箭逻辑

		if (!_active || _player == null)
		{
			return;
		}

		bool nowInRange = GlobalPosition.DistanceTo(_player.GlobalPosition) <= FireRange;
		// 只在「刚进入射程」那一帧触发一次，避免每帧重复刷新迟缓时长
		if (nowInRange && !_playerInRange)
		{
			ApplySlow();
		}
		_playerInRange = nowInRange;
	}

	private void ApplySlow()
	{
		if (_player == null)
		{
			return;
		}

		// 玩家身上的 state 状态机（State.cs）驱动 debuff.cs 实现迟缓，这里直接切状态。
		var state = _player.GetNodeOrNull<State>("state");
		if (state == null)
		{
			GD.PushWarning("PoisonZom: 找不到 player/state 状态机，迟缓未生效");
			return;
		}

		state.ChangeState(PlayerState.Slow);
		GD.Print("PoisonZom：玩家进入射程，施加迟缓");
	}
}
