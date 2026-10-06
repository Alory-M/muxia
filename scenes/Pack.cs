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

	/// <summary>把某种物资补回检查器里设置的初始值</summary>
	public void Refill(SupplyKind kind)
	{
		if (_initial.TryGetValue(kind, out int start))
		{
			SetCount(kind, start);
		}
	}
}
