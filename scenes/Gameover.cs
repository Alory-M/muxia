using Godot;
using System;

/// <summary>
/// 失败界面,挂在 gameover.tscn 的根节点上。
///
/// 三个按钮各管一件事:
///   again(再试一次)    —— 把 game.tscn 重新加载一遍,从头开始
///   tomain(返回主菜单) —— 换回开始界面 main.tscn
///   exit(退出游戏)     —— 退出程序
///
/// 结构和写法跟通关界面(End.cs)一模一样,改的时候两个一起看。
/// 界面什么时候出现不归这里管 —— 那是玩家血扣光之后由别处把它设成可见的。
/// </summary>
public partial class Gameover : Node2D
{
	// 两个场景路径都拎出来,以后改名不用翻代码
	private const string GameScenePath = "res://scenes/game.tscn";
	private const string MainScenePath = "res://scenes/main.tscn";

	public override void _Ready()
	{
		// 三个按钮都是本节点的直接子节点,和背景那张 gameover 图同级
		Hook("again", OnAgain);
		Hook("tomain", OnToMain);
		Hook("exit", OnExit);
	}

	/// <summary>接一个按钮,找不到只警告不报错 —— 少一个按钮不该让另外两个也失灵</summary>
	private void Hook(string buttonName, Action handler)
	{
		// 和项目里其它按钮一致:接 Pressed,一次点击只触发一次
		if (GetNodeOrNull<Button>(buttonName) is Button button)
		{
			button.Pressed += handler;
			return;
		}

		GD.PushWarning($"Gameover: 找不到按钮 {buttonName},这个按钮点了不会有反应。");
	}

	/// <summary>
	/// 再试一次:重新加载 game.tscn。
	/// ChangeSceneToFile 会先把当前场景整个释放掉,所以血量、背包、僵尸位置、
	/// 开过的宝箱全都回到初始状态,不用自己一样样清
	/// </summary>
	private void OnAgain()
	{
		GetTree().ChangeSceneToFile(GameScenePath);
	}

	/// <summary>返回主菜单。ChangeSceneToFile 会先把本场景整个释放掉,不用自己 QueueFree</summary>
	private void OnToMain()
	{
		GetTree().ChangeSceneToFile(MainScenePath);
	}

	/// <summary>退出游戏</summary>
	private void OnExit()
	{
		GetTree().Quit();
	}
}
