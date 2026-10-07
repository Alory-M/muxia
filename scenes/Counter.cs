using Godot;

/// <summary>
/// 金币图标旁边那个数字,挂在 HUD/icon_coin/counter 上。
///
/// 唯一职责:读 player/gold(Gold.cs)的 Amount,显示出来。
/// 不结算、不参与买卖 —— 钱的加减全在 Gold 里,这里只负责"看得见"。
/// </summary>
public partial class Counter : Label
{
	private Gold _gold;

	// 上一次显示过的数值。一样就不碰 Text:Label 改一次文本要重新排版,
	// 没必要每帧都做,金币又不会每帧都变
	private int _shown = int.MinValue;

	public override void _Ready()
	{
		// 和 Store.cs 一样:先按 "player" 组找玩家,再取它下面的 gold。
		// 不数斜杠 —— HUD 和 player 在场景里是兄弟,counter 自己还得再往上三层,
		// 层级一改就断,组名不会
		Player player = GetTree().GetFirstNodeInGroup("player") as Player;
		_gold = player?.GetNodeOrNull<Gold>("gold");

		if (_gold == null)
		{
			GD.PushWarning("Counter: 找不到 player/gold,金币数不会显示。");
			return;
		}

		Refresh();
	}

	// Gold.Amount 是个普通属性,没有变化信号可订阅,只能每帧去读一次
	public override void _Process(double delta)
	{
		Refresh();
	}

	/// <summary>把当前金币数写到 Label 上。数值没变就什么都不做</summary>
	private void Refresh()
	{
		if (_gold == null || _gold.Amount == _shown)
		{
			return;
		}

		_shown = _gold.Amount;
		Text = _shown.ToString();
	}
}
