using Godot;

/// <summary>
/// 动态镜头跟随,挂在 game.tscn 的 Camera2D 上。
///
/// 做法是"每帧把镜头贴到玩家身上,越界交给 Camera2D 自带的四个 limit 管"。
/// limit 的含义是**可见区域边缘不能越过的那条线**,所以:
///   - 玩家在地图中间 → 镜头完全跟住
///   - 玩家贴到某条边 → 那个轴停住,另一个轴照常跟
///   - 两个轴各自独立判断
/// 这几条正好就是需求里描述的行为,所以不用自己逐帧算钳制 ——
/// 手算的话"镜头中心"和"可见边缘"是两套坐标,很容易搞混。
/// </summary>
public partial class Camera2d : Camera2D
{
	// 拉近画面，让人物更清晰；HUD 在独立 CanvasLayer 中保持原尺寸。
	[Export(PropertyHint.Range, "1,4,0.05")]
	public float ViewZoom { get; set; } = 2.4f;

    // 扩大至直径220世界像素；放大不改变地图碰撞和行走距离。
    [Export(PropertyHint.Range, "50,180,1")]
    public float VisionWorldRadius { get; set; } = 110f;
    public float VisibleWorldDiameter => VisionWorldRadius * 2f;
    private ShaderMaterial _visionMaterial;

	// 地图就是 background 里那张图,边界等于它的矩形。
	// 用 TextureRect 而不是它父节点 background:background 是全屏 anchors 的 Control,
	// 尺寸跟着视口走;真正带地图尺寸的是 TextureRect
	private const string MapPath = "../background/TextureRect";

	private Node2D _target;

	public override void _Ready()
	{
		Zoom = Vector2.One * Mathf.Clamp(ViewZoom, 1f, 4f);
		// 玩家用 "player" 组去找,和 debuff / Stop 一样 ——
		// 不写死 ../player 这种斜杠,场景里挪一下也不会断
		_target = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (_target == null)
		{
			GD.PushWarning("Camera2d: 找不到 player 组的节点,镜头不会跟随。");
		}

		// 硬跟,不要缓动
		PositionSmoothingEnabled = false;

		ApplyMapLimits();

		// 开局先对齐一次,免得第一帧镜头还停在检查器里的老位置
		FollowTarget();
        var layer = new CanvasLayer { Name = "TombVision", Layer = 1 };
        AddChild(layer);
        _visionMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://scenes/tomb_vision.gdshader") };
        var veil = new Godot.ColorRect
        {
            Name = "VisionVeil", Color = Colors.Black, Material = _visionMaterial,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        layer.AddChild(veil);
        veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        UpdateVision();
	}

    public override void _Process(double delta) => UpdateVision();

    private void UpdateVision()
    {
        if (_visionMaterial == null || !GodotObject.IsInstanceValid(_target)) return;
        _visionMaterial.SetShaderParameter("viewport_size", GetViewportRect().Size);
        _visionMaterial.SetShaderParameter("player_screen", GetViewport().GetCanvasTransform() * _target.GlobalPosition);
        _visionMaterial.SetShaderParameter("pixels_per_world", Zoom.X);
        _visionMaterial.SetShaderParameter("world_radius", VisionWorldRadius);
    }

	// 用 _PhysicsProcess 而不是 _Process:玩家是在 _PhysicsProcess 里 MoveAndSlide 移动的,
	// 镜头跟着同一份位置走才不会有抖动
	public override void _PhysicsProcess(double delta)
	{
		FollowTarget();
	}

	private void FollowTarget()
	{
		if (_target != null)
		{
			GlobalPosition = _target.GlobalPosition;
		}
	}

	/// <summary>把镜头的四个 limit 设成地图图片的矩形</summary>
	private void ApplyMapLimits()
	{
		Control map = GetNodeOrNull<Control>(MapPath);
		if (map == null)
		{
			GD.PushWarning($"Camera2d: 找不到 {MapPath},镜头不会被限制在地图内。");
			return;
		}

		// 地图是 Control,取它在画布坐标系下的全局矩形,
		// 正好就是 Camera2D 的 limit 用的那套世界坐标
		Rect2 rect = map.GetGlobalRect();

		LimitLeft = (int)rect.Position.X;
		LimitTop = (int)rect.Position.Y;
		LimitRight = (int)(rect.Position.X + rect.Size.X);
		LimitBottom = (int)(rect.Position.Y + rect.Size.Y);
	}
}
