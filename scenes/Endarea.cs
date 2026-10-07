using Godot;

/// <summary>
/// 关卡终点区域,挂在 endarea(Area2D)上。
///
/// 玩家碰到这块区域,就把 HUD 下的 end(通关界面,end.tscn 的实例)显示出来。
///
/// 只认玩家:区域和 player 都在默认碰撞层上,子弹、僵尸撞进来不算数,
/// 所以这里用 body is Player 过滤一道,不用再去改碰撞层掩码。
/// </summary>
public partial class Endarea : Area2D
{
	// HUD/end,是 end.tscn 的实例,场景里默认 visible = false
	private CanvasItem _endUi;

	public override void _Ready()
	{
		// endarea 和 HUD 都是场景根的子节点,同一个爹,所以往上一层就行
		_endUi = GetNodeOrNull<CanvasItem>("../HUD/end");
		if (_endUi == null)
		{
			GD.PushWarning("Endarea: 找不到 ../HUD/end,碰到终点不会显示通关界面。");
		}

		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_endUi == null || body is not Player)
		{
			return;
		}

		_endUi.Visible = true;
		GD.Print("Endarea: 玩家到达终点,显示通关界面。");
	}
}
