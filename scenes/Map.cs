using Godot;

/// <summary>
/// 小地图,挂在 HUD/Map 上。
///
/// 把玩家在世界地图里的相对位置(占地图宽高的比例)换算成小地图上的位置,
/// 写进子节点 in —— 也就是那个"你在这儿"的图标。
///
/// 坐标怎么算:
///   in 是 Map 的子节点,所以它的 position 是 Map 的**局部坐标**;
///   而 Sprite2D 的贴图以自身原点为中心绘制,局部范围是 ±贴图尺寸/2。
///   于是   局部坐标 = (比例 - 0.5) × 贴图尺寸。
///   Map 自己的 scale(0.0956)只影响显示大小,这里不用乘 ——
///   子节点会自动继承父节点的缩放。
/// </summary>
public partial class Map : Sprite2D
{
	// 世界地图(那张 2169×1446 的大图)。和 Camera2d 读的是同一个节点
	private const string WorldMapPath = "../../background/TextureRect";

	private Sprite2D _marker;
	private Control _worldMap;
	private Node2D _player;

	public override void _Ready()
	{
		_marker = GetNodeOrNull<Sprite2D>("in");
		if (_marker == null)
		{
			GD.PushWarning("Map: 找不到子节点 in,小地图上不会显示玩家位置。");
		}

		_worldMap = GetNodeOrNull<Control>(WorldMapPath);
		if (_worldMap == null)
		{
			GD.PushWarning($"Map: 找不到 {WorldMapPath},算不出玩家的相对位置。");
		}

		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (_player == null)
		{
			GD.PushWarning("Map: 找不到 player 组的节点,小地图上不会显示玩家位置。");
		}

		RefreshMarker();
	}

	public override void _Process(double delta)
	{
		RefreshMarker();
	}

	/// <summary>按玩家当前位置挪一下那个图标</summary>
	private void RefreshMarker()
	{
		// 少一样就什么都不做。玩家可能在 M 键打开小地图之前就动过了,
		// 所以这里不能只看一次,每帧都得重算
		if (_marker == null || _worldMap == null || _player == null || Texture == null)
		{
			return;
		}

		Rect2 world = _worldMap.GetGlobalRect();
		if (world.Size.X <= 0f || world.Size.Y <= 0f)
		{
			return;   // 地图尺寸没算出来,别做除法
		}

		Vector2 playerPos = _player.GlobalPosition;

		// 玩家占世界地图宽高的比例。夹到 0~1,跑到地图外时图标也不会飞出小地图
		float fx = Mathf.Clamp((playerPos.X - world.Position.X) / world.Size.X, 0f, 1f);
		float fy = Mathf.Clamp((playerPos.Y - world.Position.Y) / world.Size.Y, 0f, 1f);

		// 0.5 是贴图中心。减掉它再乘尺寸,就把 0~1 映射到 -尺寸/2 ~ +尺寸/2
		_marker.Position = new Vector2(
			(fx - 0.5f) * Texture.GetWidth(),
			(fy - 0.5f) * Texture.GetHeight());
	}
}
