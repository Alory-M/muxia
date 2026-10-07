using Godot;
using System;

/// <summary>
/// 暂停菜单,挂在 stop.tscn 的根节点上(在 game.tscn 里就是 HUD/escstop)。
///
/// 管三件事:
///   1. 显隐:开局收着,按 Esc、或者点 HUD 顶部设置图标上那颗 escstop 按钮才打开
///   2. 模态:打开时走子节点 stop(Stop.cs)把玩家、僵尸、飞在半空的子弹一起冻住
///   3. 按钮:返回游戏 / 回到主菜单 / 退出游戏,和 end.tscn 的通关界面一个套路
///
/// 没开的时候不仅看不见,也点不到:根节点 Visible=false 之后,底下那些 Button
/// 既不会被画出来,也不会再收到鼠标事件(隐藏节点的后代一律不参与 GUI 命中)。
/// 代码里再把 MouseFilter 显式切一遍,读的时候不用去推这层继承关系。
/// </summary>
public partial class EscStop : Node2D
{
	// 开关暂停菜单的键。和 Packsys / CloseButton 用的是同一个自定义动作
	private const string EscapeKey = "esc";

	// 和 End.MainScenePath 一样拎出来,以后改名不用翻代码
	private const string MainScenePath = "res://scenes/main.tscn";

	// 谁算"模态窗口"由组名决定,Win.cs 就是按这个组保证同一时间只开一个的。
	// 本界面也进这个组:开着的时候按 B 开背包,Win 会先把暂停菜单关掉,
	// 而不是两个界面叠在一起、暂停计数还留在那儿解不开
	private const string ModalGroup = "modal_ui";

	// 开界面时要一起放行/挡住的三个按钮。名字就是场景里的节点名
	private static readonly string[] MenuButtons = { "返回游戏", "回到主菜单", "退出游戏" };

	// 开界面时的暂停开关(冻结玩家、僵尸、子弹),细节都封在子节点 stop 里
	private Stop _stop;

	// 上一帧有没有别的模态界面(背包 / 商店)开着。
	// Esc 在那些界面开着时是"关掉它"的意思,不该顺手把暂停菜单也弹出来。
	// 为什么不在这一帧现查组:背包和商店处理 Esc 的顺序排在 escstop 前面,
	// 等这里跑到的时候它们已经把自己藏起来了 —— 只有上一帧的状态才问得出"刚才是开着的"
	private bool _otherModalWasOpen;

	public override void _Ready()
	{
		_stop = GetNodeOrNull<Stop>("stop");
		if (_stop == null)
		{
			GD.PushWarning("EscStop: 找不到子节点 stop,打开暂停菜单时游戏不会被冻住。");
		}

		// HUD 顶部设置图标上那颗按钮。写相对路径而不是从场景根数斜杠:
		// 本节点是 stop.tscn 的实例,层级深浅由 game.tscn 决定,数死了挪一下就断
		// (和 Packsys 里 "../../player/pack" 一个路子)
		if (GetNodeOrNull<Button>("../base_set/escstop") is Button openButton)
		{
			openButton.Pressed += () => SetOpen(true);
		}
		else
		{
			GD.PushWarning("EscStop: 找不到 ../base_set/escstop 按钮,点图标打不开暂停菜单。");
		}

		// 三个按钮各管一件事。中文名不能当标识符,所以按字符串找(End.cs 里也是这么干的)
		Hook("返回游戏", () => SetOpen(false));
		Hook("回到主菜单", OnToMain);
		Hook("退出游戏", OnExit);

		// 开局强制收起。场景实例上本来就有 visible=false,这里再兜一次底,
		// 免得以后谁在检查器里改了覆盖值,一进游戏菜单就敞着
		SetOpen(false);
	}

	// 每帧记一下"场上还有没有别的模态界面"。只在 _UnhandledInput 里现查是不行的,
	// 原因见 _otherModalWasOpen 上面那段
	public override void _Process(double delta)
	{
		_otherModalWasOpen = IsOtherModalVisible();
	}

	// 和 Packsys / CloseButton 一样用 _UnhandledInput:按键是逐个事件投递的,不会漏掉连按
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed(EscapeKey))
		{
			return;
		}

		// 已经开着:再按一次 Esc 就是关。点"返回游戏"走的是同一个入口
		if (Visible)
		{
			SetOpen(false);
			return;
		}

		// 背包 / 商店开着,这一下 Esc 是给它关的。这里什么都不做,
		// 否则会出现"按 Esc 关背包,背包关了、暂停菜单却跟着弹出来"
		if (_otherModalWasOpen)
		{
			return;
		}

		SetOpen(true);
	}

	/// <summary>
	/// 开 / 关暂停菜单。对外公开:Win.cs 关别的窗口时会调这个方法而不是直接改 Visible ——
	/// 只改 Visible 会漏掉按钮的交互开关和 stop 的暂停(和 End / Packsys 的约定一致)
	/// </summary>
	public void SetOpen(bool open)
	{
		Visible = open;

		// 藏起来的 Button 本来也点不到,这里显式切一遍 MouseFilter:
		// 开着时吃鼠标事件,关掉之后把事件让回给底下的世界
		foreach (string buttonName in MenuButtons)
		{
			if (GetNodeOrNull<Button>(buttonName) is Button button)
			{
				// 枚举挂在 Control 上:本类继承的是 Node2D,不能像 Packsys 那样直接写 MouseFilterEnum
				button.MouseFilter = open ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
			}
		}

		// 暂停菜单是模态的:开着的时候把玩家、僵尸和飞在半空的子弹一起停住,
		// 不然你盯着菜单还在被追着砍、被箭射
		if (_stop != null)
		{
			_stop.SetPaused(open);
		}
	}

	/// <summary>接一个按钮。找不到只警告不报错 —— 少一个按钮不该让另外两个也失灵</summary>
	private void Hook(string buttonName, Action handler)
	{
		if (GetNodeOrNull<Button>(buttonName) is Button button)
		{
			// 和项目里其它按钮一致:接 Pressed,一次点击只触发一次
			button.Pressed += handler;
			return;
		}

		GD.PushWarning($"EscStop: 找不到按钮 {buttonName},这个按钮点了不会有反应。");
	}

	/// <summary>场上有没有别的模态界面开着(背包、商店……),本节点不算</summary>
	private bool IsOtherModalVisible()
	{
		foreach (Node node in GetTree().GetNodesInGroup(ModalGroup))
		{
			if (node != this && node is CanvasItem ui && ui.Visible)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// 返回主菜单。ChangeSceneToFile 会先把本场景整个释放掉,不用自己 QueueFree。
	/// 换场景前必须先关一次界面:Stop 里那个暂停计数是 static,场景换了它也不会归零,
	/// 留着 1 的话下一局每颗出膛的子弹都会在 _Ready 里读到 IsPaused、一辈子冻着不动
	/// </summary>
	private void OnToMain()
	{
		SetOpen(false);
		GetTree().ChangeSceneToFile(MainScenePath);
	}

	/// <summary>退出游戏</summary>
	private void OnExit()
	{
		GetTree().Quit();
	}
}
