using Godot;

/// <summary>
/// 灵魂碎片图标旁边那个数字,挂在 HUD/icon_soul/counter 上。
///
/// 唯一职责:读 player/soulpiece(soulpiece.cs)的碎片数,显示出来。
/// 和金币的 Counter.cs 是同一套路,只是数据源不同 ——
/// soulpiece 对外给的是 GetSoul(),不是 Gold 那样的 Amount 属性,所以没并成一份。
/// </summary>
public partial class SoulCounter : Label
{
	private soulpiece _soul;

	// 上一次显示过的数值。一样就不碰 Text:Label 改一次文本要重新排版,
	// 没必要每帧都做,碎片数又不会每帧都变
	private int _shown = int.MinValue;

	public override void _Ready()
	{
		// 和 Counter.cs / Store.cs 一样:先按 "player" 组找玩家,再取它下面的 soulpiece。
		// 不数斜杠 —— HUD 和 player 在场景里是兄弟,层级一改路径就断,组名不会
		Player player = GetTree().GetFirstNodeInGroup("player") as Player;
		_soul = player?.GetNodeOrNull<soulpiece>("soulpiece");

		if (_soul == null)
		{
			GD.PushWarning("SoulCounter: 找不到 player/soulpiece,灵魂碎片数不会显示。");
			return;
		}

		Refresh();
	}

	// soulpiece 没有变化信号可订阅,只能每帧去读一次
	public override void _Process(double delta)
	{
		Refresh();
	}

	/// <summary>把当前碎片数写到 Label 上。数值没变就什么都不做</summary>
	private void Refresh()
	{
		if (_soul == null || _soul.GetSoul() == _shown)
		{
			return;
		}

		_shown = _soul.GetSoul();
		Text = _shown.ToString();
	}
}
