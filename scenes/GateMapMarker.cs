using Godot;

/// <summary>机关使用菱形标记，黄表示待踩下，绿表示墓门已开启。</summary>
public partial class GateMapMarker : Node2D
{
    public static readonly Color ClosedColor = new Color("ffd36a");
    public static readonly Color OpenColor = new Color("76e59b");
    private bool _unlocked;
    public bool Unlocked
    {
        get => _unlocked;
        set { if (_unlocked == value) return; _unlocked = value; QueueRedraw(); }
    }

    public override void _Draw()
    {
        Vector2[] diamond = { new(0, -26), new(26, 0), new(0, 26), new(-26, 0) };
        Color color = _unlocked ? OpenColor : ClosedColor;
        DrawCircle(Vector2.Zero, 36, new Color(0.08f, 0.06f, 0.03f, 0.95f));
        DrawColoredPolygon(diamond, color);
        DrawPolyline(new[] { diamond[0], diamond[1], diamond[2], diamond[3], diamond[0] }, Colors.White, 5, true);
    }
}
