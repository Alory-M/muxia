using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

// 减益种类:一个 debuff 节点只管一种,想要两种减益就挂两个节点
public enum DebuffKind
{
	Bleed,   // 流血:每 1 秒扣 BleedPerSecond 点血
	Slow,    // 迟缓:移动速度乘以 SlowMultiplier
}

/// <summary>
/// 减益组件。挂在 player 节点下面会自动锁定它;
/// 挂在别处也可以,用 TargetPath 指定 player。
///
/// Player.cs 里的 hp / moveSpeed 都是 private 的,这里用反射读写,
/// 所以不需要改动 Player.cs。
/// </summary>
public partial class debuff : Node
{
	[Signal]
	public delegate void FinishedEventHandler();

	[Export] public DebuffKind Kind = DebuffKind.Bleed;

	// 持续时间(秒)。<= 0 表示永久,要手动调 Clear() 才结束
	[Export] public float Duration = 5f;

	// 流血:每 1 秒扣多少点血
	[Export] public float BleedPerSecond = 1f;

	// 迟缓:移速乘数。0.5 = 移速减半
	[Export(PropertyHint.Range, "0.05,1,0.05")]
	public float SlowMultiplier = 0.5f;

	// 目标。留空 = 自动找(先看父节点,再找 "player" 组)
	[Export] public NodePath TargetPath;

	// 进入场景树就立刻生效
	[Export] public bool ApplyOnReady = true;

	private Player _target;
	private bool _active;
	private float _remaining;      // 还剩多少秒
	private float _tickTimer;      // 流血计时器
	private float _baseMoveSpeed;  // 减速前的原始移速,结束时还原用

	public bool IsActive => _active;

	public override void _Ready()
	{
		_target = ResolveTarget();

		if (_target == null)
		{
			GD.PushWarning("debuff: 找不到目标 Player,减益不会生效。");
			return;
		}

		if (ApplyOnReady)
		{
			Apply();
		}
	}

	public override void _Process(double delta)
	{
		if (!_active)
		{
			return;
		}

		float dt = (float)delta;

		if (Kind == DebuffKind.Bleed)
		{
			// 攒够 1 秒才扣一次:做成"每秒一滴"的跳字,而不是每帧扣一点点
			_tickTimer += dt;
			while (_tickTimer >= 1f)
			{
				_tickTimer -= 1f;
				BleedOnce();
			}
		}

		if (Duration > 0f)
		{
			_remaining -= dt;
			if (_remaining <= 0f)
			{
				Clear();
			}
		}
	}

	/// <summary>施加减益。已经生效时重复调用 = 只刷新持续时间</summary>
	public void Apply()
	{
		if (_target == null)
		{
			return;
		}

		if (!_active)
		{
			if (Kind == DebuffKind.Slow)
			{
				float? speed = ReadNumber("moveSpeed");
				if (speed == null)
				{
					GD.PushWarning("debuff: 读不到 Player.moveSpeed,迟缓不会生效。");
					return;
				}

				// 原始移速只在第一次生效时记一次,之后刷新时长不会越减越慢
				_baseMoveSpeed = speed.Value;
				WriteNumber("moveSpeed", _baseMoveSpeed * SlowMultiplier);
				GD.Print($"debuff: 迟缓生效,移速 {_baseMoveSpeed} -> {_baseMoveSpeed * SlowMultiplier}");
			}

			_active = true;
			_tickTimer = 0f;
		}

		_remaining = Mathf.Max(Duration, 0f);
	}

	/// <summary>结束减益,并把移速还原</summary>
	public void Clear()
	{
		if (!_active)
		{
			return;
		}

		_active = false;

		if (Kind == DebuffKind.Slow)
		{
			WriteNumber("moveSpeed", _baseMoveSpeed);
			GD.Print($"debuff: 迟缓结束,移速还原为 {_baseMoveSpeed}");
		}

		EmitSignal(SignalName.Finished);
	}

	private void BleedOnce()
	{
		float? hp = ReadNumber("hp");
		if (hp == null)
		{
			GD.PushWarning("debuff: 读不到 Player.hp,流血不会生效。");
			Clear();
			return;
		}

		float next = Mathf.Max(hp.Value - BleedPerSecond, 0f);
		WriteNumber("hp", next);
		GD.Print($"debuff: 流血 -{BleedPerSecond},剩余 HP {next}");

		// 血扣光了就先停掉流血,死亡逻辑等以后有需要再加
		if (next <= 0f)
		{
			GD.Print("debuff: HP 已归零。");
			Clear();
		}
	}

	private Player ResolveTarget()
	{
		if (TargetPath != null && TargetPath.ToString() != "")
		{
			Player byPath = GetNodeOrNull<Player>(TargetPath);
			if (byPath != null)
			{
				return byPath;
			}
			GD.PushWarning($"debuff: TargetPath \"{TargetPath}\" 没指向 Player,改用自动查找。");
		}

		// 最常见的情况:直接挂在 player 节点下
		if (GetParent() is Player parent)
		{
			return parent;
		}

		// 兜底:场景里 "player" 组的第一个
		foreach (Node node in GetTree().GetNodesInGroup("player"))
		{
			if (node is Player player)
			{
				return player;
			}
		}

		return null;
	}

	// ---- 用反射读写 Player 的 private 字段,这样就不用改 Player.cs ----
	private static readonly Dictionary<string, FieldInfo> FieldCache = new();

	private static FieldInfo FindField(string name)
	{
		if (FieldCache.TryGetValue(name, out FieldInfo cached))
		{
			return cached;
		}

		FieldInfo field = typeof(Player).GetField(
			name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		FieldCache[name] = field;
		return field;
	}

	private float? ReadNumber(string fieldName)
	{
		FieldInfo field = FindField(fieldName);
		if (field == null)
		{
			return null;
		}
		return Convert.ToSingle(field.GetValue(_target));
	}

	private void WriteNumber(string fieldName, float value)
	{
		FindField(fieldName)?.SetValue(_target, value);
	}
}
