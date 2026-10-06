using Godot;

/// <summary>
/// 玩家跑步动画切换,挂在 player 的 run 节点(AnimatedSprite2D)上。
///
/// 四个方向键对应四套动画,横向优先于纵向:
///   D / A  → 侧跑(原图朝右,D 不翻、A 水平翻转)
///   W      → 背跑
///   S      → 正跑
///   都没按 → 站立
///
/// A 和 D 同时按住(横向互相抵消)时,保持上一次明确朝过的方向,不来回抖。
/// </summary>
public partial class Run : AnimatedSprite2D
{
	// 动画名。用 StringName 而不是 string:每帧都要和 Animation 比一次,
	// 这样比不会反复分配字符串
	private static readonly StringName SideAnim = "侧跑";
	private static readonly StringName BackAnim = "背跑";
	private static readonly StringName FrontAnim = "正跑";
	private static readonly StringName IdleAnim = "站立";

	// 上一次明确朝左还是朝右。A/D 同时按时用它兜底
	private bool _facingLeft;

	/// <summary>冲刺时动画的播放倍率。1 = 不变,2 = 两倍速</summary>
	[Export] public float DashSpeedMultiplier { get; set; } = 2.0f;

	// 不冲刺时的倍率。拎出来是为了不给 1.0 到处散落的机会
	private const float NormalSpeedScale = 1.0f;

	// 父节点,用来问"现在是不是在冲刺"
	private Player _player;

	public override void _Ready()
	{
		_player = GetParent() as Player;
		if (_player == null)
		{
			GD.PushWarning("Run: 父节点不是 Player,冲刺时动画不会加速。");
		}
	}

	// 方向键是"按住持续生效"的状态,不是一次性触发,所以在 _Process 里轮询。
	// 对比:开枪那种一次性动作才用 _UnhandledInput + IsActionPressed
	public override void _Process(double delta)
	{
		// 冲刺就把动画放快。SpeedScale 是节点级倍率,不动共享的 SpriteFrames,
		// 所以四个动画各自的基础速度(站立5 / 背跑7 / 侧跑正跑10)照样保留
		SpeedScale = _player != null && _player.IsDashing ? DashSpeedMultiplier : NormalSpeedScale;

		bool left = Input.IsActionPressed("move_left");
		bool right = Input.IsActionPressed("move_right");

		// 横向优先:只要 A 或 D 还有一个按着,就完全不管 W/S
		if (left || right)
		{
			// 两个都按 = 横向互相抵消,没有新信息,沿用上一次的朝向
			if (left != right)
			{
				_facingLeft = left;
			}

			FlipH = _facingLeft;
			PlayIfNot(SideAnim);
			return;
		}

		if (Input.IsActionPressed("move_up"))
		{
			PlayFlat(BackAnim);
		}
		else if (Input.IsActionPressed("move_down"))
		{
			PlayFlat(FrontAnim);
		}
		else
		{
			PlayFlat(IdleAnim);
		}
	}

	/// <summary>背跑/正跑/站立都是正面或背面视角,不需要翻转</summary>
	private void PlayFlat(StringName anim)
	{
		FlipH = false;
		PlayIfNot(anim);
	}

	/// <summary>
	/// 只在目标动画和当前不同时才切。Play() 会把动画拨回第一帧,
	/// 每帧无脑调用的话角色就定格在第一帧、跑不起来了
	/// </summary>
	private void PlayIfNot(StringName anim)
	{
		if (Animation != anim || !IsPlaying())
		{
			Play(anim);
		}
	}
}
