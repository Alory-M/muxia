using Godot;

/// <summary>把玩家的世界位置投影到小地图，保留原来的地图和玩家图标。</summary>
public partial class Map : Sprite2D
{
    private Sprite2D _playerMarker;
    private Control _worldMap;
    private Node2D _player;

    public override void _Ready()
    {
        _playerMarker = GetNodeOrNull<Sprite2D>("in");
        _worldMap = GetNodeOrNull<Control>("../../background/TextureRect");
        _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        RefreshMarker();
    }

    public override void _Process(double delta) => RefreshMarker();

    private void RefreshMarker()
    {
        if (_playerMarker == null || _worldMap == null || Texture == null ||
            !GodotObject.IsInstanceValid(_player)) return;
        Rect2 world = _worldMap.GetGlobalRect();
        if (world.Size.X <= 0 || world.Size.Y <= 0) return;

        Vector2 ratio = (_player.GlobalPosition - world.Position) / world.Size;
        ratio = ratio.Clamp(Vector2.Zero, Vector2.One);
        _playerMarker.Position = (ratio - new Vector2(0.5f, 0.5f)) * Texture.GetSize();
    }
}
