using Godot;

/// <summary>
/// 商店的库存上限,挂在 limit 节点上。管两件事:
///
/// 1. 把每种货物的剩余库存写进对应的"剩余 xx"标签
/// 2. 把这个库存当作那一行"买几份"控件的上限 ——
///    份数不能小于 0(下限由 购买数量 自己保证),也不能超过库存
///
/// 每帧对一次账:库存只有买成功时才会变,但这样写不用去 hook 所有改动点,
/// 以后商店加别的扣库存方式也不会漏。写 label 前先比一下,没变就不写。
/// </summary>
public partial class Limit : Node
{
	// 一样货物对应的两个界面节点名。顺序按 SupplyKind 的枚举顺序
	private static readonly (SupplyKind Kind, string StockLabel, string CounterNode)[] Rows =
	{
		(SupplyKind.Drug,     "剩余药品",   "购买药品数量"),
		(SupplyKind.Antidote, "剩余解毒剂", "购买解毒剂数量"),
		(SupplyKind.Bandage,  "剩余绷带",   "购买绷带数量"),
		(SupplyKind.Bullet,   "剩余子弹",   "购买子弹数量"),
	};

	private readonly Label[] _labels = new Label[Rows.Length];
	private readonly 购买数量[] _counters = new 购买数量[Rows.Length];

	// 库存数据源。商店本体和界面是兄弟节点(都在根 Control 下)
	private Store _store;

	public override void _Ready()
	{
		_store = GetNodeOrNull<Store>("../store");
		if (_store == null)
		{
			GD.PushWarning("Limit: 找不到 ../store,读不到库存,剩余数量和购买上限都不会生效。");
		}

		for (int i = 0; i < Rows.Length; i++)
		{
			_labels[i] = GetNodeOrNull<Label>("../" + Rows[i].StockLabel);
			_counters[i] = GetNodeOrNull<购买数量>("../" + Rows[i].CounterNode);

			// 少了哪个就只影响这一样,别让整行一起失效
			if (_labels[i] == null)
			{
				GD.PushWarning($"Limit: 找不到 {Rows[i].StockLabel},{Rows[i].Kind} 的剩余数量不会显示。");
			}

			if (_counters[i] == null)
			{
				GD.PushWarning($"Limit: 找不到 {Rows[i].CounterNode},或者它没挂 购买数量 脚本,"
					+ $"{Rows[i].Kind} 的购买数量不会被限制。");
			}
		}
	}

	public override void _Process(double delta)
	{
		if (_store == null)
		{
			return;
		}

		for (int i = 0; i < Rows.Length; i++)
		{
			int stock = _store.GetStock(Rows[i].Kind);

			if (_labels[i] != null)
			{
				string text = stock.ToString();
				if (_labels[i].Text != text)
				{
					_labels[i].Text = text;
				}
			}

			if (_counters[i] != null)
			{
				// 上限就是库存。份数已经超过上限的话,购买数量 的 Max setter 会当场夹回去
				_counters[i].Max = stock;
			}
		}
	}
}
