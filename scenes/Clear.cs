using Godot;

/// <summary>
/// 消耗品组件,挂在 player 下面。所有道具都遵循同一条规则:
/// 数量为 0、或者当前用不上,就不消耗。
///
/// 数量统一存在同级节点 pack(Pack.cs)里,这里只管"什么时候用、用掉哪一样"。
///
/// Q 消耗绷带(SupplyKind.Bandage)解除"流血";
/// Z 消耗解毒剂(SupplyKind.Antidote)解除"迟缓";
/// E 消耗药(SupplyKind.Drug)回血,回多少由 State.HealAmount 决定。
/// </summary>
public partial class Clear : Node
{
	// 键位单独拎出来,以后改键不用翻代码
	private const string BleedKey = "Q";
	private const string SlowKey = "Z";
	private const string DrugKey = "E";

	private Player _player;
	private State _state;
	private Pack _pack;

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

		_pack = GetNodeOrNull<Pack>("../pack");
		if (_pack == null)
		{
			GD.PushWarning("Clear: 找不到同级节点 pack,拿不到道具数量,清除减益功能不会生效。");
		}
	}

	// 三个引用缺一不可。必须先判空:下面一取字段就解引用了,晚了会空引用
	private bool RefsReady => _player != null && _state != null && _pack != null;

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
		if (!RefsReady)
		{
			return;
		}

		TryUseItem(PlayerState.Bleed, "绷带", SupplyKind.Bandage);
	}

	/// <summary>用解毒剂解除迟缓。没解毒剂、或当前没迟缓时不消耗</summary>
	public void UseAntidote()
	{
		if (!RefsReady)
		{
			return;
		}

		TryUseItem(PlayerState.Slow, "解毒剂", SupplyKind.Antidote);
	}

	/// <summary>用一份药回血。没药、或血已经满了时不消耗</summary>
	public void UseDrug()
	{
		// 回血量现在也归 State 管,所以和另外两个道具一样要三个引用齐全
		if (!RefsReady)
		{
			return;
		}

		// 血满了就别浪费药了。上限以 Player.MaxHp 为准
		if (_player.hp >= _player.MaxHp)
		{
			GD.Print("Clear: 血量已满,省下一份药。");
			return;
		}

		// 数量够才扣;不够就不扣,只提示
		if (!_pack.TryConsume(SupplyKind.Drug))
		{
			GD.Print("Clear: 没有药了,回不了血。");
			return;
		}

		_player.hp = Mathf.Min(_player.hp + _state.HealAmount, _player.MaxHp);

		GD.Print($"Clear: 用掉一份药,回血 {_state.HealAmount},当前 HP {_player.hp},还剩 {_pack.GetCount(SupplyKind.Drug)} 份。");
	}

	/// <summary>几个道具共用的一套判断:确实中了对应的减益 + 有货,才扣一份并解除</summary>
	private void TryUseItem(PlayerState target, string itemName, SupplyKind kind)
	{
		// 没中这个减益就别浪费道具了
		if (_state.Current != target)
		{
			GD.Print($"Clear: 当前没有{State.NameOf(target)},省下一份{itemName}。");
			return;
		}

		// 数量不够时 TryConsume 不扣、返回 false
		if (!_pack.TryConsume(kind))
		{
			GD.Print($"Clear: 没有{itemName}了,清除不了{State.NameOf(target)}。");
			return;
		}

		// 状态机自己负责收尾(停 tick、还原移速、血条颜色),这里只管扣道具
		_state.ResetToNormal();

		GD.Print($"Clear: 用掉一份{itemName},{State.NameOf(target)}解除,还剩 {_pack.GetCount(kind)} 份。");
	}
}
