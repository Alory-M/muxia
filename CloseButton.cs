using Godot;
using System;

/// <summary>
/// 关闭按钮,挂在 store.tscn 里 close 那个 Node 上。管两件事:
///
/// 1. 按下反馈:两张叠在同一位置的图 未点击 / 点击,按住 close2 时换图,松开还原
/// 2. 关商店:点 close2、或者按 Esc,都把整个商店界面藏起来
///
/// 节点位置注意:两张图和按钮不是 close 的子节点,而是它的**兄弟节点**
/// (同在商店根 Control 下),所以要从父节点往下找。
///
/// 名字用前缀匹配:搬动节点时 Godot 会自动加序号,按钮现在实际叫 close2,
/// 写死 "close" 会找不到。
/// </summary>
public partial class CloseButton : Node
{
	// 和 Packsys 用的是同一个动作名(项目里自定义的 Esc 动作)
	private const string EscapeKey = "esc";

	private Sprite2D _idle;
	private Sprite2D _pressed;

	// 商店界面的根节点 = close 的父节点。关商店就是把它藏起来
	private CanvasItem _ui;

	public override void _Ready()
	{
		_ui = GetParent() as CanvasItem;
		if (_ui == null)
		{
			GD.PushWarning($"{Name}: 父节点不是 CanvasItem,关不了商店。");
		}

		Node host = GetParent();
		_idle = FindSprite(host, "未点击");
		_pressed = FindSprite(host, "点击");
		Button button = FindButton(host, "close");

		// 点击关闭。Pressed 是整个点击完成时触发,和上面的按下/松开动画不冲突
		if (button != null)
		{
			button.Pressed += CloseStore;
		}

		if (_idle == null || _pressed == null || button == null)
		{
			GD.PushWarning($"{Name}: 未点击 / 点击 / close 按钮没找齐,按下反馈不会生效。");
			return;
		}

		// 按住就显示对应那张,松开撤掉。这里必须用 ButtonDown / ButtonUp:
		// Pressed 要等整个点击完成才触发一次,做不出"按住期间一直亮着"的效果
		button.ButtonDown += () => SetPressed(true);
		button.ButtonUp += () => SetPressed(false);

		_idle.Visible = true;
		SetPressed(false);
	}

	// 和 Packsys 一样用 _UnhandledInput:按键是逐个事件投递的,不会漏掉连按
	public override void _UnhandledInput(InputEvent @event)
	{
		// 只在商店开着时管 Esc。否则商店都没开,按 Esc 也会被这里吃掉
		if (_ui == null || !_ui.Visible || !@event.IsActionPressed(EscapeKey))
		{
			return;
		}

		CloseStore();
	}

	/// <summary>关商店:把整个界面藏起来</summary>
	private void CloseStore()
	{
		if (_ui != null)
		{
			_ui.Visible = false;
		}
	}

	/// <summary>按住时把 点击 叠上去,松开撤掉</summary>
	private void SetPressed(bool pressed)
	{
		_pressed.Visible = pressed;
	}

	/// <summary>按前缀找图:未点击、点击2 都能认出来</summary>
	private static Sprite2D FindSprite(Node host, string prefix)
	{
		foreach (Node child in host.GetChildren())
		{
			if (child is Sprite2D sprite && HasPrefix(sprite.Name, prefix))
			{
				return sprite;
			}
		}

		return null;
	}

	private static Button FindButton(Node host, string prefix)
	{
		foreach (Node child in host.GetChildren())
		{
			if (child is Button button && HasPrefix(button.Name, prefix))
			{
				return button;
			}
		}

		return null;
	}

	// 用 Ordinal:中文前缀是精确字符比较,不该受语言环境影响
	private static bool HasPrefix(StringName name, string prefix)
	{
		return name.ToString().StartsWith(prefix, StringComparison.Ordinal);
	}
}
