using Godot;

/// <summary>
/// 子弹的朝向组件,挂在 bullet.tscn 的 rotate 节点上。
///
/// 子弹美术本来就是水平向右的,所以"朝右"= 0 度,
/// 把飞行方向换算成角度直接写回去就行,不需要再加什么偏移量。
/// </summary>
public partial class Rotate : Node
{
	// rotate 自己是普通 Node,没有 transform,只能替宿主(子弹本体)算好角度再写进去。
	// 和 Clear / State / Stop 一样,是"组件节点操作宿主"的写法
	private Node2D _host;

	public override void _Ready()
	{
		_host = GetParent() as Node2D;
		if (_host == null)
		{
			GD.PushWarning("Rotate: 父节点不是 Node2D,子弹不会转向。");
			return;
		}

		// 初始状态:水平向右。子弹是代码动态生成的,这里显式抹平,
		// 免得哪天复用了一个带旋转的实例,出来就朝错方向
		_host.Rotation = 0.0f;
	}

	/// <summary>
	/// 让子弹朝向 <paramref name="direction"/>。朝右 = 0 度,
	/// 所以方向角就是旋转角,不用额外补偿。
	/// </summary>
	public void FaceDirection(Vector2 direction)
	{
		if (_host == null)
		{
			return;
		}

		// 零向量没有方向可言,保持原样,别把子弹掰回朝右
		if (direction.LengthSquared() < 0.0001f)
		{
			return;
		}

		_host.Rotation = direction.Angle();
	}
}
