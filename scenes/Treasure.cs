using Godot;

public partial class Treasure : Area2D
{
	private Button _getButton;

	public override void _Ready()
	{
		_getButton = GetNode<Button>("get");

		// 初始状态:不可见、不可交互
		SetGetAvailable(false);

		// 玩家进入 / 离开碰撞范围
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;

		// 按下 get 按钮
		_getButton.Pressed += OnGetPressed;
	}

	// 切换 get 的"可见 + 可交互"状态
	private void SetGetAvailable(bool available)
	{
		_getButton.Visible = available;
		_getButton.Disabled = !available;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body.IsInGroup("player"))
		{
			SetGetAvailable(true);
		}
	}

	private void OnBodyExited(Node2D body)
	{
		if (body.IsInGroup("player"))
		{
			SetGetAvailable(false);
		}
	}

	private void OnGetPressed()
	{
		// 只有 get 可见时才生效
		if (!_getButton.Visible)
		{
			return;
		}

		GD.Print("您已获得宝箱");
		QueueFree();
	}
}
