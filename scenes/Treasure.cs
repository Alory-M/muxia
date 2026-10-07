using Godot;

public partial class Treasure : Area2D
{
	private Button _getButton;
	private bool _playerInRange;   // 玩家是否在范围内
	private bool _collected;       // 防止重复拾取
	private AnimatedSprite2D _animatedSprite;
	private Timer _freeTimer;

	// 掉落组件,挂在 gift 子节点上。宝箱掉什么是它在管(读 data/drop.json)
	private Gift _gift;

	public override void _Ready()
	{
		_getButton = GetNode<Button>("get");
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_freeTimer = GetNode<Timer>("Timer");   // ← 这行缺失

		_gift = GetNodeOrNull<Gift>("gift");
		if (_gift == null)
		{
			GD.PushWarning("Treasure: 找不到子节点 gift,开箱不会掉东西。");
		}

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

		// 开箱动画播完、宝箱要消失的这一刻才结算掉落 ——
		// "宝箱消失"和"获得物品"是同一时刻
		if (_gift != null)
		{
			_gift.Drop();
		}

		QueueFree();
	}
}
