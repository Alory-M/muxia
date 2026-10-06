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

	private Vector2 _facing = Vector2.Right;   // 最后一次的移动方向
	private Vector2 _dashDirection = Vector2.Zero;
	private float _dashRemaining = 0f;         // 本次冲刺还剩多少像素

	// 背包。子弹 / 药 / 绷带 / 解毒剂的数量都在子节点 pack(Pack.cs)里,
	// 这里不再自己存一份,要用就走 _pack
	private Pack _pack;

	private bool IsDashing => _dashRemaining > 0f;

	// 冲刺速度由"距离 / 时间"推出来
	private float DashSpeed => dashDistance / Mathf.Max(dashDuration, 0.0001f);

	public override void _Ready()
	{
		_pack = GetNodeOrNull<Pack>("pack");
		if (_pack == null)
		{
			GD.PushWarning("Player: 找不到子节点 pack,拿不到子弹数量,开不了枪。");
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
			_dashRemaining -= (GlobalPosition - before).Length();

			// 浮点误差会让剩余距离永远差一丁点、无法真正归零,
			// 那样就会卡死在冲刺状态(既走不动也冲不了),所以直接抹掉残值
			if (_dashRemaining <= 0.01f)
			{
				_dashRemaining = 0f;
			}
			return;
		}

		// 常规移动:把 WASD 合成一个方向向量。
		// GetVector 会自动把长度裁到 1,所以斜向移动不会比直线更快
		Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");

		if (direction != Vector2.Zero)
		{
			_facing = direction.Normalized();   // 记住最后一次的移动方向
		}

		Velocity = direction * moveSpeed;
		MoveAndSlide();
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

		// 方向 = 当前正在按的移动方向;如果站着不动,就用最后一次的移动方向
		Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		_dashDirection = input != Vector2.Zero ? input.Normalized() : _facing;

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
}
