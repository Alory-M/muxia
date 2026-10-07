using Godot;

/// <summary>
/// 毒僵尸（C# 版）：射击参数与行为跟弓箭僵尸完全一致，区别只在于它射出的箭带毒 ——
/// 箭**打到玩家**时额外给玩家上「迟缓」（PlayerState.Slow，由 State.cs 驱动 debuff.cs 实现）。
///
/// 注意是"命中才中毒"：箭打空了什么都不会发生，玩家光是靠近它并不会被减速。
/// 毒标在箭上、结算在 Bullet.ApplyDamage 里，而不是挂在这只僵尸身上 ——
/// 箭要飞一段才命中，那会儿僵尸自己未必还在场。
/// </summary>
public partial class PoisonZom : ArrowZom
{
	/// <summary>把这一箭标成"命中玩家时上迟缓"</summary>
	protected override void ConfigureBullet(Bullet bullet)
	{
		bullet.AppliesSlow = true;
	}
}
