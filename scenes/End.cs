using Godot;
using System;

/// <summary>
/// 通关界面,挂在 end.tscn 的根节点上(在 game.tscn 里是 HUD/end)。
///
/// 三个按钮各管一件事:
///   continue(继续探索)   —— 把整个界面藏起来,回去接着玩
///   tomain(返回主菜单)   —— 换回开始界面 main.tscn
///   exit(退出游戏)       —— 退出程序
///
/// 界面什么时候出现不归这里管 —— 那是 Endarea 碰到玩家时把本节点设成可见的。
/// 这里只管界面开着的时候点按钮。
/// </summary>
public partial class End : Node2D
{
	// 和 Main.GameScenePath 一样拎出来,以后改名不用翻代码
	private const string MainScenePath = "res://scenes/main.tscn";

	public override void _Ready()
	{
		// 三个按钮都是本节点的直接子节点,和背景那张 victory new 图同级
		Hook("continue", OnContinue);
		Hook("tomain", OnToMain);
		Hook("exit", OnExit);
	}

	/// <summary>
	/// 接一个按钮,找不到只警告不报错 —— 少一个按钮不该让另外两个也失灵。
	/// 注意按钮名 continue 是 C# 关键字,不能拿来当变量名,所以统一走这个函数
	/// </summary>
	private void Hook(string buttonName, Action handler)
	{
		// 和项目里其它按钮一致:接 Pressed,一次点击只触发一次
		if (GetNodeOrNull<Button>(buttonName) is Button button)
		{
			button.Pressed += handler;
			return;
		}

		GD.PushWarning($"End: 找不到按钮 {buttonName},这个按钮点了不会有反应。");
	}

	/// <summary>继续探索:把通关界面藏起来。走出区域再进来,Endarea 会重新显示它</summary>
	private void OnContinue()
	{
		Visible = false;
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
