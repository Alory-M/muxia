using Godot;

public partial class Bullet : Area2D
{
	// 飞行速度(像素/秒)
	[Export] public float Speed { get; set; } = 800.0f;

	// 飞多少像素后自行销毁
	[Export] public float MaxDistance { get; set; } = 800.0f;

	private Vector2 _direction = Vector2.Zero;   // 由 Launch() 传入,之后不再改变
	private float _traveled = 0.0f;              // 已经飞了多远
	private bool _isMoving = false;

	// 由发射者调用:给它一个方向,它就开始飞
	public void Launch(Vector2 direction)
	{
		if (direction.LengthSquared() < 0.0001f)
		{
			// 方向为零就别留一颗不动的子弹在场上
			QueueFree();
			return;
		}

		_direction = direction.Normalized();
		_traveled = 0.0f;
		_isMoving = true;
	}

	public override void _Process(double delta)
	{
		if (!_isMoving)
		{
			return;
		}

		float step = Speed * (float)delta;
		GlobalPosition += _direction * step;
		_traveled += step;

		if (_traveled >= MaxDistance)
		{
			QueueFree();
		}
	}
}
