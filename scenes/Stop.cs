using Godot;

/// <summary>
/// 模态界面的暂停开关,挂在 packsys 的 stop 节点上。
///
/// 背包打开时把该停的东西一起停住:玩家整个冻上(不能移动、射击、冲刺、按道具热键),
/// 流血和迟缓的扣血与倒计时也停。背包关掉后原样恢复。
///
/// 以后要停别的实体(敌人、陷阱、计时器……),往 SetPaused 里加一条就行,
/// 调用方永远只认 SetPaused 这一个入口。
/// </summary>
public partial class Stop : Node
{
	private Player _player;
	private State _state;

	public override void _Ready()
	{
		// 用 "player" 组去找,而不是数 ../../.. ——
		// stop 挂在 packsys 下面,层级比 packsys 本身深一层,写死斜杠以后挪节点就会断。
		// debuff.cs 里也是同样的兜底方式
		_player = GetTree().GetFirstNodeInGroup("player") as Player;
		if (_player == null)
		{
			GD.PushWarning("Stop: 场景里找不到 player 组的 Player,开背包时角色不会被冻结。");
			return;
		}

		_state = _player.GetNodeOrNull<State>("state");
		if (_state == null)
		{
			GD.PushWarning("Stop: player 下找不到 state,开背包时减益不会被暂停。");
		}
	}

	/// <summary>
	/// 暂停 / 恢复。重复调用没有副作用,恢复时一律回到 Inherit。
	/// </summary>
	public void SetPaused(bool paused)
	{
		// 整个角色冻住:ProcessMode.Disabled 会同时停掉 _PhysicsProcess(移动、冲刺)
		// 和 _UnhandledInput(射击、Q/Z/E 热键),背包开着时就是一个纯粹的模态界面
		if (_player != null)
		{
			_player.ProcessMode = paused ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
		}

		// 显式再停一次减益。debuff 是 player 的后代,上面那行其实已经会让它们停,
		// 但这里写明白,读代码时不用去推"子节点会继承 ProcessMode"这层关系
		if (_state != null)
		{
			_state.SetDebuffsPaused(paused);
		}
	}
}
