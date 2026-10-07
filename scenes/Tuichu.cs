using Godot;

/// <summary>
/// 开始界面上的"退出"按钮,挂在 main.tscn 的 HUD/Tuichu(Sprite2D)上。
///
/// 只做一件事:按下就退出整个程序。
/// </summary>
public partial class Tuichu : Sprite2D
{
	public override void _Ready()
	{
		Button button = FindButton();
		if (button == null)
		{
			GD.PushWarning("Tuichu: 本节点下没有 Button 子节点,退出按钮点不动。");
			return;
		}

		// 和项目里其它按钮一致:接 Pressed,一次点击只触发一次
		button.Pressed += OnPressed;
	}

	/// <summary>
	/// 按"类型"找按钮子节点,不按名字。场景里这个按钮实际叫 tuiiu(拼错了),
	/// 照 tuichu 去 GetNode 会找不到;按类型找,以后改名也不受影响
	/// </summary>
	private Button FindButton()
	{
		foreach (Node child in GetChildren())
		{
			if (child is Button button)
			{
				return button;
			}
		}

		return null;
	}

	private void OnPressed()
	{
		GetTree().Quit();
	}
}
