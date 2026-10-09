using Godot;

/// <summary>
/// 商店里"买几份"的加减控件,挂在 购买XX数量 这个 Label 上。
///
/// 自己存份数(初值 1),管好 增 / 减 两个子按钮:增 +1,减 -1,最低 0。
/// 不设置商店库存上限；资金和背包容量由上层(Store)结算时校验。
/// </summary>
public partial class 购买数量 : Label
{
	// 现在要买几份。最低 0;上限由 Max 决定
	public int Count { get; private set; } = 1;

	// 整数表示上限。商店使用 int.MaxValue，不按库存限制数量。
	public int Max
	{
		get => _max;
		set
		{
			_max = Mathf.Max(value, 0);

			// 兼容其他需要数量上限的控件调用。
			if (Count > _max)
			{
				SetCount(_max);
			}
		}
	}

	private int _max = int.MaxValue;

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

		// 先检查再加，避免 int.MaxValue + 1 溢出后变成零。
		plus.Pressed += () => { if (Count < _max) SetCount(Count + 1); };
		minus.Pressed += () => SetCount(Count - 1);
	}

	/// <summary>设份数。夹到 [0, Max] 之间</summary>
	public void SetCount(int value)
	{
		Count = Mathf.Clamp(value, 0, _max);
		RefreshText();
	}

	private void RefreshText()
	{
		Text = Count.ToString();
	}
}
