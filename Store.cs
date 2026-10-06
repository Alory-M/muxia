using Godot;
using System.Collections.Generic;

/// <summary>
/// 商店,挂在 store.tscn 的 store 节点上。
///
/// 四样商品各有单价和剩余库存(八个导出变量,在检查器里调),点"购买"时按
/// 那一行当前的份数结算:库存不够 → 提示;钱不够 → 提示;都够才
/// 扣钱、扣库存、往 player 的背包里加货。
///
/// 金币目前是脚本里的一个字段(初值 1000)。Player 上还没有金币系统,
/// 等它有了,把 _gold 的读写换成 Player 的即可,别处不用动。
/// </summary>
public partial class Store : Node
{
	// ---- 单价。默认值是随手定的,调价在检查器里改 ----
	[Export] public int PriceDrug { get; set; } = 50;
	[Export] public int PriceAntidote { get; set; } = 80;
	[Export] public int PriceBandage { get; set; } = 30;
	[Export] public int PriceBullet { get; set; } = 5;

	// ---- 剩余库存 ----
	[Export] public int StockDrug { get; set; } = 10;
	[Export] public int StockAntidote { get; set; } = 10;
	[Export] public int StockBandage { get; set; } = 10;
	[Export] public int StockBullet { get; set; } = 10;

	// 玩家的钱。Player 上还没有这个字段,先在这里顶着
	private int _gold = 1000;

	// 每种商品对应的"买几份"控件,结算时读它的 Count
	private readonly Dictionary<SupplyKind, 购买数量> _counters = new();

	// 买到的货加到这里。找不到玩家就什么都不卖,免得扣了钱却没货
	private Pack _pack;

	public override void _Ready()
	{
		// 商店是独立场景,不保证和 player 在同一层,所以按组去找玩家
		// (debuff / Stop 里也是同样的方式)
		Player player = GetTree().GetFirstNodeInGroup("player") as Player;
		_pack = player?.GetNodeOrNull<Pack>("pack");
		if (_pack == null)
		{
			GD.PushWarning("Store: 找不到玩家身上的 pack,买到的货没地方放,一律不卖。");
		}

		// 四行的对应关系。路径都从 store 出发:按钮和数量框都是 store 的兄弟节点,
		// 所以要 ../ 上一层。数量框那四个节点名就是按物品起的,照名字找即可,
		// 不用靠坐标去猜哪一列是哪样东西
		HookRow(SupplyKind.Drug, "../买药", "../购买药品数量");
		HookRow(SupplyKind.Antidote, "../买解毒剂", "../购买解毒剂数量");
		HookRow(SupplyKind.Bandage, "../买绷带", "../购买绷带数量");
		HookRow(SupplyKind.Bullet, "../买子弹", "../购买子弹数量");
	}

	/// <summary>把某个"购买"按钮和它那一行的数量框接起来</summary>
	private void HookRow(SupplyKind kind, string buyPath, string countPath)
	{
		// 买卖两件东西缺一不可:按钮没有就点不了,数量框没有就不知道买几份
		if (GetNodeOrNull<Button>(buyPath) is not Button buy)
		{
			GD.PushWarning($"Store: 找不到购买按钮 {buyPath},{ItemName(kind)}买不了。");
			return;
		}

		if (GetNodeOrNull<Label>(countPath) is not 购买数量 counter)
		{
			GD.PushWarning($"Store: 找不到 {countPath},或者它没挂 购买数量 脚本,{ItemName(kind)}买不了。");
			return;
		}

		_counters[kind] = counter;
		buy.Pressed += () => Buy(kind);
	}

	/// <summary>买 kind 一样东西,份数取那一行当前的值</summary>
	private void Buy(SupplyKind kind)
	{
		// 收货的地方都没有就别开张,不然扣了钱东西进不了背包
		if (_pack == null)
		{
			GD.Print($"Store: 找不到玩家的背包,{ItemName(kind)}买不了。");
			return;
		}

		if (!_counters.TryGetValue(kind, out 购买数量 counter))
		{
			return;   // _Ready 里已经警告过,这里不重复刷屏
		}

		int amount = counter.Count;
		if (amount <= 0)
		{
			GD.Print($"Store: {ItemName(kind)} 的购买数量是 0,没买。");
			return;
		}

		int stock = GetStock(kind);
		if (stock < amount)
		{
			GD.Print($"Store: {ItemName(kind)} 库存不足(还剩 {stock},想买 {amount})。");
			return;
		}

		int cost = GetPrice(kind) * amount;
		if (_gold < cost)
		{
			GD.Print($"Store: 买 {amount} 份{ItemName(kind)}要 {cost} 金币,只有 {_gold},钱不够。");
			return;
		}

		// 到这儿库存和钱都够了,才开始动账:
		// 扣钱 → 扣库存 → 发货。三件事要么都做,要么前面就 return 掉了
		_gold -= cost;
		SetStock(kind, stock - amount);
		_pack.Add(kind, amount);

		GD.Print($"Store: 买了 {amount} 份{ItemName(kind)},花掉 {cost},"
			+ $"还剩 {_gold} 金币、{GetStock(kind)} 库存。");
	}

	private int GetPrice(SupplyKind kind)
	{
		return kind switch
		{
			SupplyKind.Drug => PriceDrug,
			SupplyKind.Antidote => PriceAntidote,
			SupplyKind.Bandage => PriceBandage,
			SupplyKind.Bullet => PriceBullet,
			_ => 0,
		};
	}

	private int GetStock(SupplyKind kind)
	{
		return kind switch
		{
			SupplyKind.Drug => StockDrug,
			SupplyKind.Antidote => StockAntidote,
			SupplyKind.Bandage => StockBandage,
			SupplyKind.Bullet => StockBullet,
			_ => 0,
		};
	}

	private void SetStock(SupplyKind kind, int value)
	{
		// 夹到 >= 0,免得哪天算错了把库存变成负数
		int next = Mathf.Max(value, 0);

		switch (kind)
		{
			case SupplyKind.Drug: StockDrug = next; break;
			case SupplyKind.Antidote: StockAntidote = next; break;
			case SupplyKind.Bandage: StockBandage = next; break;
			case SupplyKind.Bullet: StockBullet = next; break;
		}
	}

	/// <summary>提示文字里用的中文名</summary>
	private static string ItemName(SupplyKind kind)
	{
		return kind switch
		{
			SupplyKind.Drug => "药品",
			SupplyKind.Antidote => "解毒剂",
			SupplyKind.Bandage => "绷带",
			SupplyKind.Bullet => "子弹",
			_ => "道具",
		};
	}
}
