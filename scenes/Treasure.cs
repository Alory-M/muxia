using Godot;

public partial class Treasure : Area2D
{
	private Button _getButton;
	private bool _playerInRange;   // 玩家是否在范围内
	private bool _collected;       // 防止重复拾取
	private AnimatedSprite2D _animatedSprite;
	private Timer _freeTimer;

	public override void _Ready()
	{
		_getButton = GetNode<Button>("get");
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_freeTimer = GetNode<Timer>("Timer");   // ← 这行缺失

		// 初始状态:不可见、不可交互
		SetGetAvailable(false);

		// 玩家进入 / 离开碰撞范围
		BodyEntered += OnBodyEntered;
		BodyExited  += OnBodyExited;
		_freeTimer.Timeout += OnFreeTimerTimeout;
	}

	// 每帧检测 F 键
	public override void _Process(double delta)
	{
		if (_collected) return;
		if (!_playerInRange) return;

		// 检测 "interact" 动作是否刚被按下(即按 F 的那一刻)
		if (Input.IsActionJustPressed("interact"))
		{
			OnGetPressed();
		}
	}

	private void SetGetAvailable(bool available)
	{
		_getButton.Visible  = available;
		_getButton.Disabled = !available;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_collected) return;
		if (body.IsInGroup("player"))
		{
			_playerInRange = true;
			SetGetAvailable(true);
		}
	}

	private void OnBodyExited(Node2D body)
	{
		if (_collected) return;
		if (body.IsInGroup("player"))
		{
			_playerInRange = false;
			SetGetAvailable(false);
		}
	}

	private void OnGetPressed()
	{
		if (_collected) return;
		if (!_getButton.Visible) return;

		_collected = true;
		_animatedSprite.Animation = "open";   // 属性，设置当前动画
		
		_freeTimer.Start();
		
		GD.Print("您已获得宝箱");
	}
	private void OnFreeTimerTimeout()
	{
		SetGetAvailable(false);
		QueueFree();
	}
}
