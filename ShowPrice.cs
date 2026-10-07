using Godot;

/// <summary>
/// 商店里四个"购买"按钮上的单价,挂在 store.tscn 的 show price 节点上。
///
/// 价格不在这儿存 —— 单价是 Store 的导出变量(PriceDrug / PriceAntidote / ...),
/// 这里只负责把它们读出来,写进对应按钮底下的那个 Label。
///
/// 和 Limit.cs 一个套路:每帧对一次账,写 label 之前先比一下,没变就不写。
/// 价格只有检查器里能改,但每帧读一次就不用去 hook 所有改价的地方。
/// </summary>
public partial class ShowPrice : Node
{
	// 一样货物对应的"购买"按钮节点名。单价就写在按钮的子节点 Label 上
	private static readonly (SupplyKind Kind, string BuyButton)[] Rows =
	{
		(SupplyKind.Drug,     "买药"),
		(SupplyKind.Antidote, "买解毒剂"),
		(SupplyKind.Bandage,  "买绷带"),
		(SupplyKind.Bullet,   "买子弹"),
	};

	private readonly Label[] _labels = new Label[Rows.Length];

	// 价格数据源。商店本体(store)和界面节点是兄弟,都在根 Control 下
	private Store _store;

	public override void _Ready()
	{
		_store = GetNodeOrNull<Store>("../store");
		if (_store == null)
		{
			GD.PushWarning("ShowPrice: 找不到 ../store,读不到单价,价格不会显示。");
		}

		for (int i = 0; i < Rows.Length; i++)
		{
			_labels[i] = GetNodeOrNull<Label>($"../{Rows[i].BuyButton}/Label");

			// 少了哪个就只影响这一样,别让四行一起失效
			if (_labels[i] == null)
			{
				GD.PushWarning($"ShowPrice: 找不到 {Rows[i].BuyButton}/Label,"
					+ $"{Rows[i].Kind} 的单价不会显示。");
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
			if (_labels[i] == null)
			{
				continue;
			}

			string text = _store.GetPrice(Rows[i].Kind).ToString();
			if (_labels[i].Text != text)
			{
				_labels[i].Text = text;
			}
		}
	}
}
