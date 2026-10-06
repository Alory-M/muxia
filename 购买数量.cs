using Godot;

/// <summary>
/// 商店里"买几份"的加减控件,挂在 购买XX数量 这个 Label 上。
///
/// 自己存份数(初值 1),管好 增 / 减 两个子按钮:增 +1,减 -1,最低 0。
/// 够不够钱、够不够货不归它管,上层(Store)结算时直接读 Count。
/// </summary>
public partial class 购买数量 : Label
{
	// 现在要买几份。最低 0;没有上限——买不买得起由 Store 判断
	public int Count { get; private set; } = 1;

	public override void _Ready()
	{
		// 场景里 text 写死成 "1",这里以 Count 为准刷一遍,
		// 免得以后改了初值、显示和实际对不上
		RefreshText();

		Button plus = GetNodeOrNull<Button>("增");
		Button minus = GetNodeOrNull<Button>("减");
		if (plus == null || minus == null)
		{
			GD.PushWarning($"{Name}: 找不到 增 / 减 子按钮,份数调不了。");
			return;
		}

		plus.Pressed += () => SetCount(Count + 1);
		minus.Pressed += () => SetCount(Count - 1);
	}

	/// <summary>设份数。负数一律夹到 0</summary>
	private void SetCount(int value)
	{
		Count = Mathf.Max(value, 0);
		RefreshText();
	}

	private void RefreshText()
	{
		Text = Count.ToString();
	}
}
