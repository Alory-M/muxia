using Godot;

/// <summary>
/// 仓库:玩家身上的货币 / 材料,挂在 player/store 上。
/// 和背包(Pack.cs)分开——Pack 管消耗品(药 / 绷带 / 解毒剂 / 子弹),
/// 这里管灵魂碎片这类攒起来、不直接消耗的东西。
/// </summary>
public partial class Store : Node
{
	// 灵魂碎片数量,检查器里可调初始值
	[Export] public int _soulCounter = 0;

	/// <summary>当前灵魂碎片数量</summary>
	public int GetSoul() => _soulCounter;

	/// <summary>加灵魂碎片。给 GDScript 的僵尸脚本调,免去跨语言传枚举的麻烦</summary>
	public void AddSoul(int amount = 1)
	{
		if (amount <= 0)
		{
			return;
		}
		_soulCounter += amount;
	}
}
