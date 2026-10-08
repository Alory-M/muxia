using Godot;

/// <summary>
/// 开门机关,挂在 game.tscn 的 开门机关(Area2D)上。
///
/// 玩家碰到它**一次**,终点就解锁:
///   终点大门(远处那张 Sprite2D)藏起来 —— 门开了
///   endarea(终点触发区)显示出来,同时把检测打开 —— 变得可交互
///
/// 在那之前 endarea 是"关着"的:不可见,而且碰撞也是关的。
/// 注意光把 visible 设成 false 是拦不住玩家的 —— Area2D 的碰撞跟显示没有关系,
/// 所以这里必须同时把 monitoring 关掉,不然玩家摸到那个看不见的区域照样会通关。
///
/// 只触发一次:拉过之后再碰到不会有任何反应。
/// </summary>
public partial class 开门机关 : Area2D
{
	// ../endarea,终点触发区
	private Area2D _endArea;

	// ../终点大门,门那张图
	private CanvasItem _gate;

	private bool _triggered;
    public bool IsUnlocked => _triggered;
    public Vector2 TriggerPosition => GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.GlobalPosition ?? GlobalPosition;

	// 开门音效播放器:机关被触发、门打开时播放一次 stone_door_open.wav
	private AudioStreamPlayer _openPlayer;

	public override void _Ready()
	{
		// 机关、终点大门、endarea 都是场景根的子节点,同一个爹,往上一层就行
		_endArea = GetNodeOrNull<Area2D>("../endarea");
		if (_endArea == null)
		{
			GD.PushWarning("开门机关: 找不到 ../endarea,拉了机关也解锁不了终点。");
		}

		_gate = GetNodeOrNull<CanvasItem>("../终点大门");
		if (_gate == null)
		{
			GD.PushWarning("开门机关: 找不到 ../终点大门,拉了机关也看不到门开。");
		}

		// 起步就是"关着"。这里强制设一遍,不看场景里存的是什么值 ——
		// 免得哪天在检查器里把 endarea 的 visible 点回 true,出现"门还关着却能通关"
		SetEndAreaOpen(false);

		if (_gate != null)
		{
			_gate.Visible = true;
		}

		BodyEntered += OnBodyEntered;

		SetupOpenAudio();
	}

	// 创建开门音效播放器并加载 stone_door_open.wav
	private void SetupOpenAudio()
	{
		_openPlayer = new AudioStreamPlayer();
		AddChild(_openPlayer);

		AudioStream stream = GD.Load<AudioStream>("res://music/stone_door_open.wav");
		if (stream == null)
		{
			GD.PushWarning("开门机关: 找不到音频 res://music/stone_door_open.wav");
			return;
		}
		_openPlayer.Stream = stream;
	}

	// 播放开门音效;音频没加载到就静默跳过
	private void PlayOpenSound()
	{
		if (_openPlayer != null && _openPlayer.Stream != null)
		{
			_openPlayer.Play();
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_triggered || body is not Player player || player.hp <= 0 || Stop.IsPaused)
		{
			return;
		}

		// 先立旗子再干活:中途要是出什么岔子,也不会被连续触发第二次
		_triggered = true;

		SetEndAreaOpen(true);

		if (_gate != null)
		{
			_gate.Visible = false;   // 门开了
		}

		PlayOpenSound(); // 门开了,播放 stone_door_open

		InteractionController.Notify("墓门已开启，带上财宝前往右上方出口。");
		GD.Print("开门机关: 玩家拉下机关,终点大门打开,endarea 变成可交互");
	}

	/// <summary>
	/// 开 / 关 endarea。显示和检测要一起改 ——
	/// 只改 visible 的话,看不见的区域照样会触发通关
	/// </summary>
	private void SetEndAreaOpen(bool open)
	{
		if (_endArea == null)
		{
			return;
		}

		_endArea.Visible = open;

		// 这个函数会在 BodyEntered(物理回调)里被调到,当场改 monitoring 会被引擎拦下来
		// ("Function blocked during in/out signal"),所以走 SetDeferred 推迟到这一步跑完
		_endArea.SetDeferred(Area2D.PropertyName.Monitoring, open);
	}
}
