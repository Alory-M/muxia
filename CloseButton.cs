using Godot;
using System;

/// <summary>
/// 关闭按钮的按下反馈,挂在 store.tscn 里 close 那个 Node 上。
///
/// 注意节点位置:两张图和按钮都是 close 的**兄弟节点**(同在根 Control 下),
/// 不是它的子节点 —— 所以要从父节点往下找,不能用 GetNodeOrNull 直接找子节点。
/// 这一点改过一次结构就踩到了:把图/按钮从 close 底下拖出来之后,原脚本全部返回 null。
///
/// 名字用前缀匹配:搬动时 Godot 会自动加序号,按钮现在实际叫 close2,
/// 写死 "close" 会找不到。
/// </summary>
public partial class CloseButton : Node
{
	private Sprite2D _idle;
	private Sprite2D _pressed;

	public override void _Ready()
	{
		Node host = GetParent();

		_idle = FindSprite(host, "未点击");
		_pressed = FindSprite(host, "点击");
		Button button = FindButton(host, "close");

		if (_idle == null || _pressed == null || button == null)
		{
			GD.PushWarning($"{Name}: 未点击 / 点击 / close 按钮没找齐,按下反馈不会生效。");
			return;
		}

		// 和 Animate 一样用 ButtonDown / ButtonUp,而不是 Pressed:
		// Pressed 要等整个点击完成才触发一次,做不出"按住期间一直亮着"的效果
		button.ButtonDown += () => SetPressed(true);
		button.ButtonUp += () => SetPressed(false);

		_idle.Visible = true;
		SetPressed(false);
	}

	/// <summary>按住时把 点击 叠上去,松开撤掉</summary>
	private void SetPressed(bool pressed)
	{
		_pressed.Visible = pressed;
	}

	/// <summary>按前缀找图:点击、点击2 都能认出来</summary>
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
