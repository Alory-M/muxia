using Godot;

public partial class Player : CharacterBody2D
{
	// 移动速度(像素/秒)
	[Export]
	private float moveSpeed = 100f;

	[Export]
	private float hp = 100f;
	
	// 要发射的子弹场景,在检查器里指定 res://scenes/bullet.tscn
	[Export]
	private PackedScene bulletScene;

	public override void _PhysicsProcess(double delta)
	{
		// 把 WASD 合成一个方向向量。
		// GetVector 会自动把长度裁到 1,所以斜向移动不会比直线更快
		Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");

		Velocity = direction * moveSpeed;
		MoveAndSlide();
	}

	// 用 _UnhandledInput 而不是 _Process + IsActionJustPressed:
	// 输入事件是逐个投递的,两帧之内连点也不会漏掉
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("mouse_press"))
		{
			Shoot();
		}
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
