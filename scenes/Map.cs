using Godot;

/// <summary>把玩家和地板机关的世界位置投影到小地图。</summary>
public partial class Map : Sprite2D
{
    private Sprite2D _playerMarker;
    private GateMapMarker _gateMarker;
    private Label _gateLegend;
    private Control _worldMap;
    private Node2D _player;
    private 开门机关 _gate;

    public override void _Ready()
    {
        _playerMarker = GetNodeOrNull<Sprite2D>("in");
        _worldMap = GetNodeOrNull<Control>("../../background/TextureRect");
        _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        _gate = GetNodeOrNull<开门机关>("../../开门机关");

        // 图标在地图和边框上方，人物图标仍保留在最上方。
        if (_playerMarker != null) _playerMarker.ZIndex = 3;
        _gateMarker = new GateMapMarker { Name = "GateMarker", ZIndex = 2 };
        AddChild(_gateMarker);
        _gateLegend = UiKit.Label("◆ 开门机关 · 未触发", 60);
        _gateLegend.Name = "GateLegend";
        _gateLegend.Theme = UiKit.Theme();
        _gateLegend.MouseFilter = Control.MouseFilterEnum.Ignore;
        _gateLegend.ZIndex = 2;
        AddChild(_gateLegend);
        if (Texture != null)
            _gateLegend.Position = new Vector2(-Texture.GetWidth() / 2f, Texture.GetHeight() / 2f + 18);
        RefreshMarkers();
    }

    public override void _Process(double delta) => RefreshMarkers();

    private void RefreshMarkers()
    {
        if (_worldMap == null || Texture == null) return;
        Rect2 world = _worldMap.GetGlobalRect();
        if (world.Size.X <= 0 || world.Size.Y <= 0) return;

        if (_playerMarker != null && GodotObject.IsInstanceValid(_player))
            _playerMarker.Position = Project(_player.GlobalPosition, world);
        _gateMarker.Visible = GodotObject.IsInstanceValid(_gate);
        _gateLegend.Visible = _gateMarker.Visible;
        if (!_gateMarker.Visible) return;

        // 机关根节点位于 (0, 0)，真正的地板按钮在碰撞形状的位置。
        _gateMarker.Position = Project(_gate.TriggerPosition, world);
        _gateMarker.Unlocked = _gate.IsUnlocked;
        _gateLegend.Text = _gate.IsUnlocked ? "◆ 开门机关 · 已开启" : "◆ 开门机关 · 未触发";
        _gateLegend.Modulate = _gate.IsUnlocked ? GateMapMarker.OpenColor : GateMapMarker.ClosedColor;
    }

    private Vector2 Project(Vector2 position, Rect2 world)
    {
        Vector2 ratio = (position - world.Position) / world.Size;
        ratio = ratio.Clamp(Vector2.Zero, Vector2.One);
        return (ratio - new Vector2(0.5f, 0.5f)) * Texture.GetSize();
    }
}
