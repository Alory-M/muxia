using Godot;

/// <summary>
/// 消耗品组件,挂在 player 下面。所有道具都遵循同一条规则:
/// 数量为 0、或者当前用不上,就不消耗。
///
/// Q 消耗绷带(Player._bandageCounter)解除"流血";
/// Z 消耗解毒剂(Player._antidoteCounter)解除"迟缓";
/// E 消耗药(Player._drugCounter)回血,回多少由 HealAmount 决定。
/// </summary>
public partial class Clear : Node
{
	// 键位单独拎出来,以后改键不用翻代码
	private const string BleedKey = "Q";
	private const string SlowKey = "Z";
	private const string DrugKey = "E";

	/// <summary>按 E 一次回多少血</summary>
	[Export] public int HealAmount { get; set; } = 10;

	private Player _player;
	private State _state;

	public override void _Ready()
	{
		_player = GetParent() as Player;
		if (_player == null)
		{
			GD.PushWarning("Clear: 父节点不是 Player,清除减益功能不会生效。");
			return;
		}

		_state = GetNodeOrNull<State>("../state");
		if (_state == null)
		{
			GD.PushWarning("Clear: 找不到同级节点 state,清除减益功能不会生效。");
		}
	}

	// 和 Player 一样用 _UnhandledInput:按键是逐个事件投递的,不会漏掉连按
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed(BleedKey))
		{
			UseBandage();
		}
		else if (@event.IsActionPressed(SlowKey))
		{
			UseAntidote();
		}
		else if (@event.IsActionPressed(DrugKey))
		{
			UseDrug();
		}
	}

	/// <summary>用绷带解除流血。没绷带、或当前没流血时不消耗</summary>
	public void UseBandage()
	{
		// 必须先判空:下面 ref 取字段时就解引用了,晚了会空引用
		if (_player == null || _state == null)
		{
			return;
		}

		TryUseItem(PlayerState.Bleed, "绷带", ref _player._bandageCounter);
	}

	/// <summary>用解毒剂解除迟缓。没解毒剂、或当前没迟缓时不消耗</summary>
	public void UseAntidote()
	{
		if (_player == null || _state == null)
		{
			return;
		}

		TryUseItem(PlayerState.Slow, "解毒剂", ref _player._antidoteCounter);
	}

	/// <summary>用一份药回血。没药、或血已经满了时不消耗</summary>
	public void UseDrug()
	{
		// 只用到 _player,不需要 _state,所以在这里单独判空
		if (_player == null)
		{
			return;
		}

		if (_player._drugCounter <= 0)
		{
			GD.Print("Clear: 没有药了,回不了血。");
			return;
		}

		// 血满了就别浪费药了。上限以 Player.MaxHp 为准
		if (_player.hp >= _player.MaxHp)
		{
			GD.Print("Clear: 血量已满,省下一份药。");
			return;
		}

		_player._drugCounter -= 1;
		_player.hp = Mathf.Min(_player.hp + HealAmount, _player.MaxHp);

		GD.Print($"Clear: 用掉一份药,回血 {HealAmount},当前 HP {_player.hp},还剩 {_player._drugCounter} 份。");
	}

	/// <summary>两个道具共用的一套判断:有货 + 确实中了对应的减益,才扣一份并解除</summary>
	private void TryUseItem(PlayerState target, string itemName, ref int counter)
	{
		if (counter <= 0)
		{
			GD.Print($"Clear: 没有{itemName}了,清除不了{State.NameOf(target)}。");
			return;
		}

		// 没中这个减益就别浪费道具了
		if (_state.Current != target)
		{
			GD.Print($"Clear: 当前没有{State.NameOf(target)},省下一份{itemName}。");
			return;
		}

		counter -= 1;

		// 状态机自己负责收尾(停 tick、还原移速、血条颜色),这里只管扣道具
		_state.ResetToNormal();

		GD.Print($"Clear: 用掉一份{itemName},{State.NameOf(target)}解除,还剩 {counter} 份。");
	}
}
