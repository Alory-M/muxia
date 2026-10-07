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

	// 开箱音效播放器:玩家按 F 开箱时播放一次 treasure_box_open.wav
	private AudioStreamPlayer _openPlayer;

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

		SetupOpenAudio();
	}

	// 创建开箱音效播放器并加载 treasure_box_open.wav
	private void SetupOpenAudio()
	{
		_openPlayer = new AudioStreamPlayer();
		AddChild(_openPlayer);

		AudioStream stream = GD.Load<AudioStream>("res://music/treasure_box_open.wav");
		if (stream == null)
		{
			GD.PushWarning("Treasure: 找不到音频 res://music/treasure_box_open.wav");
			return;
		}
		_openPlayer.Stream = stream;
	}

	// 播放开箱音效;音频没加载到就静默跳过
	private void PlayOpenSound()
	{
		if (_openPlayer != null && _openPlayer.Stream != null)
		{
			_openPlayer.Play();
		}
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
		PlayOpenSound();   // 开箱音效
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
