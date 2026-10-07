using Godot;

/// <summary>
/// 子弹图标旁边的数字,挂在 HUD/icon_zidan/yudan 上。
///
/// 显示成 "可用子弹/总共子弹",例 "6/20":
///   左边 = 弹匣里现在能打的子弹
///   右边 = 还没装进弹匣的备用子弹(换弹时补进弹匣,所以会跟着变少)
///
/// 两个数都从 player/pack 读,和金币 / 灵魂那两个 counter 一个套路 ——
/// 每帧对一次账,文本没变就不写 Text(改 Label 文本要重新排版)。
/// </summary>
public partial class Yudan : Label
{
	private Pack _pack;

	// 上一次显示过的文本。初值是 null,所以 _Ready 里那次一定会写进去
	private string _shown;

	public override void _Ready()
	{
		// 和 Counter / SoulCounter / Store 一样:先按 "player" 组找玩家,再取它下面的 pack
		Player player = GetTree().GetFirstNodeInGroup("player") as Player;
		_pack = player?.GetNodeOrNull<Pack>("pack");

		if (_pack == null)
		{
			GD.PushWarning("Yudan: 找不到 player/pack,子弹数不会显示。");
			return;
		}

		Refresh();
	}

	// Pack 的计数器没有变化信号可订阅,只能每帧去读一次
	public override void _Process(double delta)
	{
		Refresh();
	}

	/// <summary>把当前子弹数写到 Label 上。文本没变就什么都不做</summary>
	private void Refresh()
	{
		if (_pack == null)
		{
			return;
		}

		int loaded = _pack.GetCount(SupplyKind.Bullet);
		int reserve = _pack.GetReserveBullet();
		string text = $"{loaded}/{reserve}";

		if (text == _shown)
		{
			return;
		}

		_shown = text;
		Text = text;
	}
}
