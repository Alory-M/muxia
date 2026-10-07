using Godot;

/// <summary>
/// 模态界面的暂停开关,挂在 packsys 的 stop 节点上。
///
/// 背包打开时把该停的东西一起停住:
///   玩家整个冻上(不能移动、射击、冲刺、按道具热键)
///   流血和迟缓的扣血与倒计时
///   场上所有僵尸(按 "zombie" 组找)
///   飞在半空的子弹和箭(按 "bullet" 组找)
/// 背包关掉后原样恢复。
///
/// 以后要停别的实体(陷阱、计时器……),往 SetPaused 里加一条就行,
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

	// 现在有几个模态界面开着。
	// 用计数而不是布尔:背包和商店各有一个 stop 节点,操作的是同一个玩家,
	// 关掉其中一个不该把另一个的暂停也解掉 —— 必须等全关完才恢复。
	// 静态就是因为要跨这两个实例共享
	private static int _openCount;

	/// <summary>
	/// 现在有没有模态界面开着。
	/// SetPaused 只在开关界面的那一刻扫一遍组,暂停期间才生成的东西扫不到 ——
	/// 它自己在 _Ready 里问一句这个,就能决定要不要一出生就冻上
	/// </summary>
	public static bool IsPaused => _openCount > 0;

	/// <summary>
	/// 暂停 / 恢复。paused=true 记一次"开",false 记一次"关";
	/// 只要有界面还开着就一直冻着,全关完才恢复。
	/// </summary>
	public void SetPaused(bool paused)
	{
		if (paused)
		{
			_openCount++;
		}
		else
		{
			_openCount = Mathf.Max(_openCount - 1, 0);
		}

		bool frozen = _openCount > 0;

		// 整个角色冻住:ProcessMode.Disabled 会同时停掉 _PhysicsProcess(移动、冲刺)
		// 和 _UnhandledInput(射击、Q/Z/E 热键),界面开着时就是一个纯粹的模态
		if (_player != null)
		{
			_player.ProcessMode = frozen ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
		}

		// 僵尸和飞在半空的子弹也得停。它们都不在 player 子树里(僵尸挂在棺材下面,
		// 子弹挂在场景根),上面那行管不到 —— 不冻的话界面开着僵尸照样追你、砍你、射箭,
		// 已经出膛的箭也照样飞过来,而你被冻着躲都躲不掉。
		// 按组找而不是按路径:有几只僵尸、哪只刚从棺材里出来、有几颗子弹在飞,这里都不用管
		PauseGroup(Zombie.GroupName, frozen);
		PauseGroup(Bullet.GroupName, frozen);

		// 显式再停一次减益。debuff 是 player 的后代,上面那行其实已经会让它们停,
		// 但这里写明白,读代码时不用去推"子节点会继承 ProcessMode"这层关系
		if (_state != null)
		{
			_state.SetDebuffsPaused(frozen);
		}
	}

	/// <summary>
	/// 把某个组里的节点一起冻住 / 恢复。
	/// 每次现查组,不缓存列表 —— 子弹是打一发生成一颗的,场上的数量一直在变
	/// </summary>
	private void PauseGroup(string group, bool frozen)
	{
		foreach (Node node in GetTree().GetNodesInGroup(group))
		{
			node.ProcessMode = frozen ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
		}
	}
}
