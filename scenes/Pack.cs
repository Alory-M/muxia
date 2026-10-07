using Godot;
using System;
using System.Collections.Generic;

/// <summary>pack 统一管理的物资种类。以后加新物资:这里补一项,再在下面 switch 里接上对应字段</summary>
public enum SupplyKind
{
	Bullet,     // 子弹:鼠标左键射击,按 R 补满
	Drug,       // 药:按 E / 点"治疗"按钮回血
	Bandage,    // 绷带:按 Q 解除流血
	Antidote,   // 解毒剂:按 Z 解除迟缓
}

/// <summary>
/// 背包:玩家身上所有消耗品的唯一数据源,挂在 player/pack 上。
///
/// 四个计数器原本在 Player.cs 里,现在搬到这儿;别的脚本不再直接读写计数字段,
/// 一律走 GetCount / Has / TryConsume / Add / Refill。
/// </summary>
public partial class Pack : Node
{
	// 数量在检查器里调。字段名和搬家前保持一致,场景里的覆盖值才好迁移
	[Export] public int _BulletCounter = 6;

	// 还没装进弹匣的备用子弹。换弹时从这里扣,不是凭空变出来的
	[Export] public int _totalBullet = 20;

	[Export] public int _drugCounter = 1;
	[Export] public int _bandageCounter = 3;
	[Export] public int _antidoteCounter = 3;

	// _Ready 时记下检查器里的初始值,Refill 还原到它,
	// 这样"补满是多少"只由检查器说了算,别处不用再写死一份
	private readonly Dictionary<SupplyKind, int> _initial = new();

	public override void _Ready()
	{
		foreach (SupplyKind kind in Enum.GetValues<SupplyKind>())
		{
			_initial[kind] = GetCount(kind);
		}
	}

	/// <summary>当前数量</summary>
	public int GetCount(SupplyKind kind)
	{
		return kind switch
		{
			SupplyKind.Bullet => _BulletCounter,
			SupplyKind.Drug => _drugCounter,
			SupplyKind.Bandage => _bandageCounter,
			SupplyKind.Antidote => _antidoteCounter,
			_ => 0,
		};
	}

	/// <summary>
	/// 还没装进弹匣的备用子弹。换弹时从这里补进弹匣,所以它会跟着减少。
	/// HUD 上子弹标签右边的数字就是它 —— 走这里读,别直接摸 _totalBullet
	/// </summary>
	public int GetReserveBullet()
	{
		return _totalBullet;
	}

	/// <summary>还有货吗(默认问"至少有一份吗")</summary>
	public bool Has(SupplyKind kind, int amount = 1)
	{
		return GetCount(kind) >= amount;
	}

	/// <summary>够就扣掉并返回 true;不够就不扣、返回 false,调用方只看返回值</summary>
	public bool TryConsume(SupplyKind kind, int amount = 1)
	{
		if (amount <= 0 || !Has(kind, amount))
		{
			return false;
		}

		SetCount(kind, GetCount(kind) - amount);
		return true;
	}

	/// <summary>捡到物资时加数量</summary>
	public void Add(SupplyKind kind, int amount = 1)
	{
		if (amount <= 0)
		{
			return;
		}

		SetCount(kind, GetCount(kind) + amount);
	}

	// ---- 给掉落表用的具名入口 ----
	// item.json 里的 location 是 "player/pack:方法名" 这种通用形式,只传一个数量,
	// 而 Add 还要一个 SupplyKind 枚举,没法从字符串传过来。所以每样货给一个同名方法,
	// 掉落表就不用管枚举了。实现都转发到上面的 Add
	public void AddBandage(int amount) => Add(SupplyKind.Bandage, amount);
	public void AddDrug(int amount) => Add(SupplyKind.Drug, amount);
	public void AddBullet(int amount) => Add(SupplyKind.Bullet, amount);
	public void AddAntidote(int amount) => Add(SupplyKind.Antidote, amount);

	/// <summary>直接改数量,负数会被夹到 0</summary>
	public void SetCount(SupplyKind kind, int amount)
	{
		int value = Mathf.Max(amount, 0);

		switch (kind)
		{
			case SupplyKind.Bullet: _BulletCounter = value; break;
			case SupplyKind.Drug: _drugCounter = value; break;
			case SupplyKind.Bandage: _bandageCounter = value; break;
			case SupplyKind.Antidote: _antidoteCounter = value; break;
		}
	}

	/// <summary>
	/// 换弹:把弹匣补满,补进去的子弹从 _totalBullet 里扣。
	///
	/// 补多少 = "弹匣还空多少" 和 "备用子弹还剩多少" 里小的那个。所以:
	///   - 弹匣满着按 R → 补 0 发,也不扣备用子弹(不会白浪费)
	///   - 备用子弹不够补满 → 有多少补多少
	///   - 弹匣永远不会超过容量
	/// </summary>
	public void ReloadBullets()
	{
		// 弹匣容量 = 检查器里给 _BulletCounter 设的初始值(_Ready 时存进 _initial 了),
		// 不在这儿再写死一个 6
		if (!_initial.TryGetValue(SupplyKind.Bullet, out int capacity))
		{
			return;
		}

		int need = capacity - _BulletCounter;
		if (need <= 0)
		{
			GD.Print("Pack: 弹匣是满的,不用换弹。");
			return;
		}

		if (_totalBullet <= 0)
		{
			GD.Print("Pack: 没有备用子弹了。");
			return;
		}

		int loaded = Mathf.Min(need, _totalBullet);
		_BulletCounter += loaded;
		_totalBullet -= loaded;

		GD.Print($"Pack: 换弹补了 {loaded} 发,弹匣 {_BulletCounter}/{capacity},备用还剩 {_totalBullet}。");
	}

	/// <summary>把某种物资补回检查器里设置的初始值</summary>
	public void Refill(SupplyKind kind)
	{
		if (_initial.TryGetValue(kind, out int start))
		{
			SetCount(kind, start);
		}
	}
}
