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
	}

	private void OnStartPressed()
	{
		GetTree().ChangeSceneToFile(GameScenePath);
	}
}
