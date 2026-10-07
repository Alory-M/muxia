using Godot;

// 玩家状态。debuff 节点只管"怎么生效",这个状态机只管"什么时候让哪一种生效"
public enum PlayerState
{
	Normal,   // 正常:身上没有任何减益
	Bleed,    // 流血:每 1 秒扣一次血
	Slow,     // 迟缓:移速乘以 SlowMultiplier
}

/// <summary>
/// 挂在 player 下面的状态机。
///
/// 内部常驻两个 debuff 组件(流血 / 迟缓),切状态时只让对应的那个生效,
/// 另一个一律 Clear();血条颜色跟着状态变,方便肉眼确认。
///
/// 减益到期由 debuff 自己发 Finished 信号,这里收到后自动切回"正常"。
/// </summary>
public partial class State : Node
{
	[Signal]
	public delegate void StateChangedEventHandler(int previous, int current);

	// ---- 流血参数 ----
	// 扣血用的是这里的值。State 在 _Ready 里 new 出 debuff 节点时会把 BleedPerSecond
	// 传进去(CreateDebuff),把 debuff.cs 里的默认值覆盖掉 —— 改那边不起作用。
	[Export] public float BleedPerSecond { get; set; } = 100f;
	[Export] public float BleedDuration { get; set; } = 5f;

	// ---- 迟缓参数。0.5 = 移速减半 ----
	[Export(PropertyHint.Range, "0.05,1,0.05")]
	public float SlowMultiplier { get; set; } = 0.5f;
	[Export] public float SlowDuration { get; set; } = 5f;

	// ---- 回血参数 ----
	// 药(E 键 / HUD 的"治疗"按钮)一份回多少血。原来写死在 Clear.cs 里,
	// 挪过来是为了跟流血强度摆在一起:一眼能对上"每秒流 100,一份药回多少"。
	[Export] public int HealAmount { get; set; } = 100;

	// 三种状态各给血条一种颜色,测试时一眼能看出当前处于哪个状态
	[Export] public Color NormalColor { get; set; } = new Color(0.972549f, 0.05882353f, 0.03137255f);
	[Export] public Color BleedColor { get; set; } = new Color(0.45f, 0f, 0f);
	[Export] public Color SlowColor { get; set; } = new Color(0.15f, 0.45f, 0.95f);

	private Player _player;
	private ColorRect _hpBar;

	private debuff _bleed;   // 常驻,不生效时处于 Clear 状态
	private debuff _slow;

	private PlayerState _current = PlayerState.Normal;

	// 减益的扣血和倒计时都靠它们自己的 _Process 推进。背包这类模态界面打开时要停掉,
	// 否则玩家盯着背包的时候还在流血、倒计时还在走
	private bool _debuffsPaused;

	// ChangeState 里主动 Clear() 也会触发 Finished,用这个标志把
	// "我主动关掉的"和"自己到期结束的"区分开,否则会绕回 Normal 造成递归
	private bool _switching;

	public PlayerState Current => _current;

	public override void _Ready()
	{
		_player = GetParent() as Player;
		if (_player == null)
		{
			GD.PushWarning("State: 父节点不是 Player,状态机不会生效。");
			return;
		}

		_hpBar = GetNodeOrNull<ColorRect>("../../HUD/ColorRect");
		if (_hpBar == null)
		{
			GD.PushWarning("State: 找不到 HUD/ColorRect,状态不会有颜色提示。");
		}

		// 两个 debuff 先建好但不生效,ApplyOnReady=false 把时机交给状态机
		_bleed = CreateDebuff("BleedDebuff", DebuffKind.Bleed, BleedPerSecond, 1f, BleedDuration);
		_slow  = CreateDebuff("SlowDebuff",  DebuffKind.Slow,  0f, SlowMultiplier, SlowDuration);

		// 刚建出来的节点默认是开着的。如果 SetDebuffsPaused 在本节点 _Ready 之前
		// 就被调用过(背包挂在 HUD 下,_Ready 比 player 子树先跑),这里按标志补上
		ApplyDebuffProcess();

		// 自动到期:debuff 自己结束时发 Finished,状态机收到就切回正常
		_bleed.Finished += () => OnDebuffFinished(PlayerState.Bleed);
		_slow.Finished  += () => OnDebuffFinished(PlayerState.Slow);

		// HUD 上的三个测试按钮,点了就切对应状态
		HookButton("../../HUD/btn_normal", PlayerState.Normal);
		HookButton("../../HUD/btn_bleed",  PlayerState.Bleed);
		HookButton("../../HUD/btn_slow",   PlayerState.Slow);

		ApplyVisual();
	}

	/// <summary>
	/// 切换状态。切到当前所处的状态 = 只刷新它的持续时间,不会叠加。
	/// 外部系统(技能、陷阱之类)也可以直接调这个方法。
	/// </summary>
	public void ChangeState(PlayerState next)
	{
		if (_player == null)
		{
			return;
		}

		PlayerState previous = _current;

		// 先把 _current 改成新状态,再 Clear 旧的:
		// Clear 触发的 Finished 处理器一比对就会发现"已经不归我管了",自动忽略
		_switching = true;
		_current = next;

		_bleed.Clear();
		_slow.Clear();

		if (next == PlayerState.Bleed)
		{
			_bleed.Apply();
		}
		else if (next == PlayerState.Slow)
		{
			_slow.Apply();
		}

		_switching = false;

		ApplyVisual();

		if (previous != next)
		{
			GD.Print($"State: {NameOf(previous)} -> {NameOf(next)}");
			EmitSignal(SignalName.StateChanged, (int)previous, (int)next);
		}
	}

	/// <summary>清掉所有减益,回到正常</summary>
	public void ResetToNormal()
	{
		ChangeState(PlayerState.Normal);
	}

	/// <summary>
	/// 暂停/恢复所有减益的扣血和倒计时(背包这类模态界面打开时用)。
	/// 已经在目标状态时重复调用没有副作用。
	///
	/// 只是"停表",不是 Clear():状态本身还在,恢复后从暂停处接着走,
	/// 所以中途用道具解除减益也照常生效。
	/// </summary>
	public void SetDebuffsPaused(bool paused)
	{
		_debuffsPaused = paused;
		ApplyDebuffProcess();
	}

	// 把 _debuffsPaused 落到两个 debuff 节点上。节点还没建出来时跳过,
	// 等 _Ready 建完再按标志补一次
	private void ApplyDebuffProcess()
	{
		bool running = !_debuffsPaused;

		if (_bleed != null)
		{
			_bleed.SetProcess(running);
		}

		if (_slow != null)
		{
			_slow.SetProcess(running);
		}
	}

	private void OnDebuffFinished(PlayerState finished)
	{
		// 是自己切换时顺手 Clear 掉的,不是自然到期,不当回事
		if (_switching)
		{
			return;
		}

		// 已经切到别的状态了,这个减益的结束和当前状态无关
		if (_current != finished)
		{
			return;
		}

		GD.Print($"State: {NameOf(finished)} 到期,自动回到正常");
		ChangeState(PlayerState.Normal);
	}

	private debuff CreateDebuff(
		string name, DebuffKind kind, float bleedPerSecond, float slowMultiplier, float duration)
	{
		debuff node = new debuff
		{
			Name = name,
			Kind = kind,
			BleedPerSecond = bleedPerSecond,
			SlowMultiplier = slowMultiplier,
			Duration = duration,
			TargetPath = _player.GetPath(),   // 显式锁定玩家,不让 debuff 自己去猜
			ApplyOnReady = false,             // 生效时机由状态机决定
		};

		// 必须挂在 state 自己下面:此刻 player 正在铺它的子节点,
		// 往 player 上 add_child() 会被引擎拒绝("Parent node is busy setting up children")。
		// 目标由 TargetPath 显式指定,所以挂在哪儿都不影响它找对玩家。
		AddChild(node);
		return node;
	}

	private void HookButton(string path, PlayerState target)
	{
		Button button = GetNodeOrNull<Button>(path);
		if (button == null)
		{
			GD.PushWarning($"State: 找不到按钮 {path},这个状态只能靠代码切换。");
			return;
		}

		button.Pressed += () => ChangeState(target);
	}

	private void ApplyVisual()
	{
		if (_hpBar == null)
		{
			return;
		}

		_hpBar.Color = _current switch
		{
			PlayerState.Bleed => BleedColor,
			PlayerState.Slow => SlowColor,
			_ => NormalColor,
		};
	}

	/// <summary>状态的中文名,Clear 之类的组件打日志也用它,避免各写一份</summary>
	public static string NameOf(PlayerState state)
	{
		return state switch
		{
			PlayerState.Bleed => "流血",
			PlayerState.Slow => "迟缓",
			_ => "正常",
		};
	}
}
