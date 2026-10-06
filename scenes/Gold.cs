using Godot;

/// <summary>
/// 玩家身上的钱,挂在 player/store 上。
///
/// 只负责"有多少钱"和"能不能花掉",不关心是谁在花。
/// 商店(store.tscn 里的 Store.cs)结算时来这里查余额、扣钱。
///
/// 节点名和历史原因叫 store,但它不是商店 —— 商店在 store.tscn 里。
/// </summary>
public partial class Gold : Node
{
	[Export] public int Amount { get; set; } = 1000;

	/// <summary>
	/// 够就扣掉并返回 true;不够就不扣、返回 false,调用方只看返回值。
	/// 和 Pack.TryConsume 一个套路:判断和扣减放在一起,不给"先查后扣"留出错的空间。
	///
	/// 花 0 块算花得起(商品单价可以设成 0 当免费送),负数才拒绝。
	/// </summary>
	public bool TrySpend(int cost)
	{
		if (cost < 0 || Amount < cost)
		{
			return false;
		}

		Amount -= cost;
		return true;
	}

	/// <summary>进账。卖东西、捡到钱之类以后用得上</summary>
	public void Add(int value)
	{
		if (value > 0)
		{
			Amount += value;
		}
	}
}
