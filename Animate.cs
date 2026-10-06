using Godot;
using System;

/// <summary>
/// 购买数量那一行的按键反馈,挂在 购买XX数量 下面的 animate 节点上。
///
/// 那一行有三张叠在同一位置的图:
///   正常 —— 常态,一直显示
///   点加 —— "加号被按住"时的变体
///   点减 —— "减号被按住"时的变体
/// 默认只显示 正常;按住哪个按钮就把对应那张叠上去,松开就撤掉。
///
/// 节点全按名字相对父节点去找,所以四行共用这一个脚本,以后再加行也不用改代码。
///
/// 名字用"前缀"匹配而不是全等 —— 这一点很关键:复制节点时 Godot 会自动加序号,
/// 现在四行实际叫 正常 / 正常2 / 正常3 / 正常4、点加 / 点加2 / …。
/// 写成精确匹配 "正常" 的话,只有第一行能生效,另外三行直接找不到节点、静默失效。
/// </summary>
public partial class Animate : Node
{
	private Sprite2D _normal;
	private Sprite2D _plusPressed;
	private Sprite2D _minusPressed;

	public override void _Ready()
	{
		Node host = GetParent();

		_normal = FindSprite(host, "正常");
		_plusPressed = FindSprite(host, "点加");
		_minusPressed = FindSprite(host, "点减");
		Button plus = FindButton(host, "增");
		Button minus = FindButton(host, "减");

		if (_normal == null || _plusPressed == null || _minusPressed == null
			|| plus == null || minus == null)
		{
			GD.PushWarning($"{Name}: 正常 / 点加 / 点减 / 增 / 减 没找齐,按键反馈不会生效。");
			return;
		}

		// 按住就显示对应那张,松开撤掉。这里必须用 ButtonDown / ButtonUp:
		// Pressed 要等整个点击完成才触发一次,做不出"按住期间一直亮着"的效果
		plus.ButtonDown += () => _plusPressed.Visible = true;
		plus.ButtonUp += () => _plusPressed.Visible = false;
		minus.ButtonDown += () => _minusPressed.Visible = true;
		minus.ButtonUp += () => _minusPressed.Visible = false;

		Restore();
	}

	/// <summary>回到常态:只留 正常,两张按下图都收起来</summary>
	private void Restore()
	{
		_normal.Visible = true;
		_plusPressed.Visible = false;
		_minusPressed.Visible = false;
	}

	/// <summary>按前缀找图:正常、正常2、正常3 都能认出来</summary>
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
