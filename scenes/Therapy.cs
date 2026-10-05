using Godot;

/// <summary>
/// HUD 上的"治疗"按钮。
///
/// 回血逻辑统一在 Clear 里(和 E 键同一条路径),这里只把 Pressed 转发过去。
/// 按钮和键位不再各维护一份 Healing / MaxHp,不会改了一个忘了另一个。
/// </summary>
public partial class Therapy : Button
{
	private Clear _clear;

	public override void _Ready()
	{
		// 在 _Ready 里获取节点（此时节点已进入场景树）
		_clear = GetNodeOrNull<Clear>("../../player/clear");
		if (_clear == null)
		{
			GD.PrintErr("Therapy: 找不到 player/clear，按钮回血不可用。");
			return;
		}

		// 订阅自己的 Pressed 信号
		Pressed += _clear.UseDrug;
	}
}
