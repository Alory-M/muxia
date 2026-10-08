using Godot;

/// <summary>
/// 开门机关,挂在 game.tscn 的 开门机关(Area2D)上。
///
/// 玩家踩下机关一次后，墓门显示打开状态并开启终点检测。
/// 背景图已包含关闭的栅栏，所以开门时同时绘制门洞和靠边的门扇。
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
	private Sprite2D _gate;
	private Node2D _openGateVisual;

	private bool _triggered;
    public bool IsUnlocked => _triggered;
    public bool IsOpen => _openGateVisual?.Visible == true;
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

		_gate = GetNodeOrNull<Sprite2D>("../终点大门");
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
			SetupGateVisual();
		}

		BodyEntered += OnBodyEntered;

		SetupOpenAudio();
	}

	private void SetupGateVisual()
	{
		_openGateVisual = new Node2D { Name = "OpenGateVisual", Visible = false };
		_gate.AddChild(_openGateVisual);

		// 坐标对应原门纹理中栅栏的四个角，保持原门框、位置和缩放。
		// 覆盖背景中烘焙的关闭栅栏，露出可通行的暗门洞。
		_openGateVisual.AddChild(new Polygon2D
		{
			Name = "Doorway",
			Polygon = new[]
			{
				new Vector2(-233, -399), new Vector2(54, -446),
				new Vector2(54, 423), new Vector2(-233, 380)
			},
			Color = new Color(0.012f, 0.018f, 0.023f)
		});

		// 原栅栏收拢到门洞右侧，明确呈现打开后的门扇。
		_openGateVisual.AddChild(new Sprite2D
		{
			Name = "OpenDoorLeaf",
			Texture = _gate.Texture,
			Position = new Vector2(43, 0),
			Scale = new Vector2(0.14f, 1),
			ZIndex = 1
		});
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
			// SelfModulate 只隐藏关闭的栅栏纹理，子节点继续显示门洞和打开的门扇。
			_gate.SelfModulate = new Color(1, 1, 1, 0);
			_openGateVisual.Visible = true;
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
