using Godot;

public partial class Player : CharacterBody2D
{
	// 移动速度(像素/秒)
	[Export]
	private float moveSpeed = 100f;

	// 改成 public 供 Therapy / ColorRect 读写
	[Export]
	public float hp = 100f;

	// 血量上限。回血封顶、血条比例都以它为准,别再在别处写死 100
	[Export]
	public float MaxHp = 100f;

	// 要发射的子弹场景,在检查器里指定 res://scenes/bullet.tscn
	[Export]
	private PackedScene bulletScene;

	// 冲刺距离(像素)—— 外部可调
	[Export]
	private float dashDistance = 200f;

	// 这段距离用多长时间跑完(秒),越小冲得越快
	[Export]
	private float dashDuration = 0.15f;

	private Vector2 _dashDirection = Vector2.Zero;
	private float _dashRemaining = 0f;         // 本次冲刺还剩多少像素

	// 背包。子弹 / 药 / 绷带 / 解毒剂的数量都在子节点 pack(Pack.cs)里,
	// 这里不再自己存一份,要用就走 _pack
	private Pack _pack;

	// 冲刺中?Run 之类的子节点要据此换表现(比如把跑步动画放快),
	// 所以对外公开,不让人去反射读 _dashRemaining
	public bool IsDashing => _dashRemaining > 0f;

	// 冲刺速度由"距离 / 时间"推出来
	private float DashSpeed => dashDistance / Mathf.Max(dashDuration, 0.0001f);

	// 这一步实际走的距离不足计划距离的这个比例,就认定是被碰撞箱正面挡住了,
	// 而不是"贴着墙滑"。0.5 表示被吃掉一半以上才算撞墙。
	private const float BlockedStepFactor = 0.5f;

	// 走路音效:玩家真的在动(坐标变化)就循环放,停下就停
	private AudioStreamPlayer _walkPlayer;
	private Vector2 _lastPosition; // 上一帧位置,用来判断玩家是否真的移动了

	public override void _Ready()
	{
		_pack = GetNodeOrNull<Pack>("pack");
		if (_pack == null)
		{
			GD.PushWarning("Player: 找不到子节点 pack,拿不到子弹数量,开不了枪。");
		}

		SetupWalkAudio();
		_lastPosition = GlobalPosition;
	}

	// 创建走路音效播放器并加载 walk.wav,设成循环
	private void SetupWalkAudio()
	{
		_walkPlayer = new AudioStreamPlayer();
		AddChild(_walkPlayer);

		AudioStreamWav stream = GD.Load<AudioStreamWav>("res://music/walk.wav");
		if (stream == null)
		{
			GD.PushWarning("Player: 找不到音频 res://music/walk.wav");
			return;
		}
		stream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
		_walkPlayer.Stream = stream;
	}

	// 根据玩家这帧有没有真的移动(坐标变化),决定走路音效要不要继续放
	private void UpdateWalkSound()
	{
		if (_walkPlayer.Stream == null)
		{
			return; // 音频没加载到,什么都不放
		}

		Vector2 now = GlobalPosition;
		bool moved = (now - _lastPosition).LengthSquared() > 0.0001f;
		_lastPosition = now;

		if (moved)
		{
			if (!_walkPlayer.Playing)
			{
				_walkPlayer.Play();
			}
		}
		else if (_walkPlayer.Playing)
		{
			_walkPlayer.Stop();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		// 冲刺中:无视常规移动,沿冲刺方向前进
		if (IsDashing && dt > 0f)
		{
			Vector2 before = GlobalPosition;

			// 最后一步做了限制,不会冲过头
			float step = Mathf.Min(DashSpeed * dt, _dashRemaining);
			Velocity = _dashDirection * (step / dt);
			MoveAndSlide();

			// 按"实际移动了多少"扣减:撞墙时冲刺会提前结束
			float moved = (GlobalPosition - before).Length();
			_dashRemaining -= moved;

			// 结束冲刺的两种情况:
			// 1) 距离冲完了(浮点误差下剩的那一丁点直接抹掉);
			// 2) 这一步几乎没走成 —— 被碰撞箱正面挡死了。
			// 第 2 条是必须的:垂直撞上水平碰撞箱时,MoveAndSlide 的滑动分量正好是 0
			// (motion - normal * (motion·normal) = (0,-1) - (0,1)*(-1) = 0),
			// moved 恒为 0,剩余距离一像素都扣不掉,冲刺就永远结束不了 ——
			// IsDashing 一直是 true,_PhysicsProcess 每次都在这里提前 return,
			// 人就被永久锁死(既走不动也冲不了),光靠抹掉浮点残值救不回来。
			// 只被吃掉一个分量时(比如 45° 蹭墙)moved 还占 step 的大头,照旧滑完。
			if (_dashRemaining <= 0.01f || moved < step * BlockedStepFactor)
			{
				_dashRemaining = 0f;
			}
			UpdateWalkSound();
			return;
		}

		// 常规移动:把 WASD 合成一个方向向量。
		// GetVector 会自动把长度裁到 1,所以斜向移动不会比直线更快
		Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");

		Velocity = direction * moveSpeed;
		MoveAndSlide();

		UpdateWalkSound();
	}

	// 用 _UnhandledInput 而不是 _Process + IsActionJustPressed:
	// 输入事件是逐个投递的,两帧之内连点也不会漏掉
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("mouse_press"))
		{
			TryShoot();
		}
		else if (@event.IsActionPressed("mouse_press2"))
		{
			StartDash();
		}
	}

	private void StartDash()
	{
		if (IsDashing)
		{
			return;   // 冲刺途中不能再冲
		}

		// 冲刺方向由玩家当前按着的方向决定。一个方向键都没按(站着不动)就不给冲——
		// 方向总得由玩家给出来,不能拿"上次朝哪"凑合
		Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		if (input.LengthSquared() < 0.0001f)
		{
			return;
		}

		_dashDirection = input.Normalized();
		_dashRemaining = dashDistance;
	}

	/// <summary>有子弹就打一发、扣一发;没子弹时什么都不做(和以前一样,不提示)</summary>
	private void TryShoot()
	{
		if (_pack == null || !_pack.TryConsume(SupplyKind.Bullet))
		{
			return;
		}

		Shoot();
	}

	private void Shoot()
	{
		if (bulletScene == null)
		{
			GD.PushWarning("Player: 没有指定 bulletScene,无法发射。");
			return;
		}

		// 方向 = 玩家 -> 鼠标
		Vector2 toMouse = GetGlobalMousePosition() - GlobalPosition;
		if (toMouse.LengthSquared() < 0.0001f)
		{
			GD.PushWarning("Player: 鼠标与玩家位置重合,方向为零,不发射。");
			return;
		}

		// 生成一颗全新的子弹,挂到场景根节点下(而不是玩家下,否则会跟着玩家跑)
		Bullet bullet = bulletScene.Instantiate<Bullet>();
		GetParent().AddChild(bullet);
		bullet.GlobalPosition = GlobalPosition;
		bullet.Launch(toMouse);
	}

	/// <summary>
	/// 受到伤害。供 GDScript 的僵尸脚本调用(对应 GDScript 里的 take_damage)。
	/// 血量扣到 0 为止,不再往下扣。
	/// </summary>
	public void TakeDamage(float amount)
	{
		if (amount <= 0f)
		{
			return;
		}
		hp = Mathf.Max(hp - amount, 0f);
	}
}
