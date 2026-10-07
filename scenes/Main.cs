using Godot;

/// <summary>
/// 开始界面,挂在 main.tscn 的根节点上。
///
/// 目前只做一件事:按下 start 就换成游戏场景。
///
/// 换场景用 ChangeSceneToFile —— 它会先把当前场景(也就是本场景)整个释放掉,
/// 再加载新的。所以不用自己 QueueFree,也不会出现两个场景同时挂在树上。
/// </summary>
public partial class Main : Node2D
{
	// 要加载的场景。拎出来是为了以后改名/换场景不用翻代码
	private const string GameScenePath = "res://scenes/game.tscn";

	// 开场音效,挂在根节点下、名字叫 begin 的场景节点(玩家自己加的)。
	// 点开始时先播它,播完再进游戏。
	private AudioStreamPlayer2D _begin;

	// 防止开场音效没播完时连点 start,重复触发
	private bool _starting;

	public override void _Ready()
	{
		// 按钮挂在 HUD/Start 下面,和它上面那张美术图是父子关系
		Button start = GetNodeOrNull<Button>("HUD/Start/start");
		if (start == null)
		{
			GD.PushWarning("Main: 找不到 HUD/Start/start 按钮,开始界面点不动。");
			return;
		}

		// 和项目里其它按钮一致:接 Pressed,一次点击只触发一次
		start.Pressed += OnStartPressed;

		_begin = GetNodeOrNull<AudioStreamPlayer2D>("begin");
	}

	private void OnStartPressed()
	{
		if (_starting)
		{
			return; // 开场音效还在播,忽略连点
		}
		_starting = true;

		if (_begin != null && _begin.Stream != null)
		{
			_begin.Play();
			// 等开场音效播完再换场景 —— ChangeSceneToFile 会把本场景整个释放掉,
			// 现在就换的话声音刚响一声就被一起清了
			_begin.Finished += OnBeginFinished;
		}
		else
		{
			GoToGame();
		}
	}

	// 开场音效播完,进游戏
	private void OnBeginFinished()
	{
		GoToGame();
	}

	private void GoToGame()
	{
		GetTree().ChangeSceneToFile(GameScenePath);
	}
}
