using Godot;
using System.Collections.Generic;

/// <summary>
/// 窗口管理,挂在 HUD 下的 win 节点上。保证同一时间只开一个模态窗口
/// (背包 / 商店),不会两个叠在一起。
///
/// 谁算"窗口"由组名决定:节点加进 modal_ui 组就算。
/// 以后再加一个界面,给它加组、并对外提供 SetOpen(bool) 就行,这里不用改。
///
/// 关的时候走对方的 SetOpen(false) 而不是直接改 Visible ——
/// 每个界面开合时还要做别的事(切 MouseFilter、冻结玩家),
/// 直接改 Visible 会把那些收尾工作全跳过。
/// </summary>
public partial class Win : Node
{
	private const string ModalGroup = "modal_ui";

	// 窗口在 _Ready 时就都在场了,一次拿够,不用每帧重新查组
	private readonly List<CanvasItem> _windows = new();

	// 上一帧每个窗口是开是关。用来认出"这一帧刚被打开"的那个,好让最后开的赢
	private readonly Dictionary<CanvasItem, bool> _wasOpen = new();

	// 每帧复用一个列表,避免给游戏循环加无谓的分配
	private readonly List<CanvasItem> _visible = new();

	public override void _Ready()
	{
		foreach (Node node in GetTree().GetNodesInGroup(ModalGroup))
		{
			if (node is CanvasItem ui)
			{
				_windows.Add(ui);
				_wasOpen[ui] = ui.Visible;
			}
		}

		if (_windows.Count == 0)
		{
			GD.PushWarning($"Win: {ModalGroup} 组里一个窗口都没有,不会限制同时打开。");
		}
	}

	public override void _Process(double delta)
	{
		if (_windows.Count <= 1)
		{
			return;
		}

		_visible.Clear();
		CanvasItem justOpened = null;

		foreach (CanvasItem ui in _windows)
		{
			bool was = _wasOpen[ui];
			bool now = ui.Visible;

			if (now)
			{
				_visible.Add(ui);

				// 这一帧刚被打开的。同一帧开了多个时,以最后遍历到的为准
				if (!was)
				{
					justOpened = ui;
				}
			}

			_wasOpen[ui] = now;
		}

		if (_visible.Count <= 1)
		{
			return;
		}

		// 开着不止一个:留下刚打开的那个(也就是最后打开的那个),其余关掉
		CanvasItem keep = justOpened ?? _visible[0];

		foreach (CanvasItem ui in _visible)
		{
			if (ui != keep)
			{
				CloseWindow(ui);
			}
		}
	}

	/// <summary>关掉一个窗口。走它自己的 SetOpen,把收尾工作也带上</summary>
	private static void CloseWindow(CanvasItem ui)
	{
		if (ui.IsInGroup("store_ui")) { ui.GetNode<Store>("store").SetOpen(false); return; }
		if (ui.HasMethod("SetOpen"))
		{
			ui.Call("SetOpen", false);
			return;
		}

		// 没提供 SetOpen 的界面就直接藏起来。有些界面(比如商店)自己盯着可见性
		// 做收尾,这样就是对的;但如果它指望外部帮它收尾,就会静默出问题,所以提一句
		GD.Print($"Win: {ui.Name} 没有 SetOpen,直接改 Visible 隐藏。");
		ui.Visible = false;
	}
}
