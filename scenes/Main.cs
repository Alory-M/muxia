using Godot;

/// <summary>
/// 开始界面,挂在 main.tscn 的根节点上。
///
/// 按下 start 立刻换成游戏场景:不等开场音效、不做过渡、不留尾巴。
///
/// 换场景用 ChangeSceneToFile —— 它会先把当前场景(也就是本场景)整个释放掉,
/// 再加载新的。所以不用自己 QueueFree,也不会出现两个场景同时挂在树上,
/// 本场景的画面节点和音频节点(begin / bgm)跟着一起被释放,声音不会带到游戏里。
/// </summary>
public partial class Main : Node2D
{
	// 要加载的场景。拎出来是为了以后改名/换场景不用翻代码
	private const string GameScenePath = "res://scenes/game.tscn";

	// 防止连点:ChangeSceneToFile 是延迟到本帧末尾才执行的,这中间再点一下会排进第二次换场景
	private bool _starting;
    private AudioStreamPlayer _music;
    public override void _EnterTree() => AudioSettings.Ensure();

	public override void _Ready()
	{
		AudioSettings.Route(this);
        _music = ScreenMusic.Start(this, "res://music/BGM_begin.ogg", "bgm");
        // 按钮挂在 HUD/Start 下面,和它上面那张美术图是父子关系
		Button start = GetNodeOrNull<Button>("HUD/Start/start");
		if (start == null)
		{
			GD.PushWarning("Main: 找不到 HUD/Start/start 按钮,开始界面点不动。");
			return;
		}

		// 和项目里其它按钮一致:接 Pressed,一次点击只触发一次
		start.Pressed += OnStartPressed;
        var hint = UiKit.Label("WASD 移动 · F 交互 · J/左键 攻击 · Shift/右键 冲刺\nB 背包 · M 地图 · R 换弹 · E 药品 · Q 绷带 · Z 解毒", 16);
        hint.Theme = UiKit.Theme();
        GetNode("HUD").AddChild(hint);
        hint.Position = new Vector2(270, 560); hint.Size = new Vector2(660, 70); hint.HorizontalAlignment = HorizontalAlignment.Center;
	}

    public override void _ExitTree() => ScreenMusic.Release(_music);

	private void OnStartPressed()
	{
		if (_starting)
		{
			return;
		}
		_starting = true;

		// 先掐掉声音再换场景。ChangeSceneToFile 是延迟执行的,而它开头那句
		// ResourceLoader.load(game.tscn) 是同步的、要花时间;这中间老场景还活着,
		// 声音也还在响。显式停一下,才能保证"点下去那一刻"音乐就断,
		// 而不是等新场景加载完才被连带释放掉
		StopSceneAudio(this);

		GetTree().ChangeSceneToFile(GameScenePath);
	}

	/// <summary>
	/// 停掉本场景里所有音频(目前是根节点下的 begin 开场音效和 bgm)。
	/// 按类型递归找而不是写死节点名:以后改名字、把音频挪进子树都不用回来改这里
	/// </summary>
	private static void StopSceneAudio(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is AudioStreamPlayer streamPlayer)
			{
				streamPlayer.Stop();
			}
			else if (child is AudioStreamPlayer2D positionalPlayer)
			{
				positionalPlayer.Stop();
			}

			StopSceneAudio(child);
		}
	}
}
