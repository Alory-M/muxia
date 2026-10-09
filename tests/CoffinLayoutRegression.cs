using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>真实玩家体积验证贴墙棺材、可见脚印和逐个可达的交互位置。</summary>
public partial class CoffinLayoutRegression : Node
{
    private const int Columns = 183, Rows = 127;
    private const float Step = 10;
    private Node2D _game;
    private Player _player;
    private CollisionShape2D _playerShape;
    private readonly List<Coffin> _coffins = new();
    private readonly List<CollisionShape2D> _walls = new();
    private readonly Dictionary<string, Image> _images = new();
    private readonly bool[,] _reachable = new bool[Columns, Rows];
    private int _checks;

    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        GD.Print($"PASS COFFIN LAYOUT {++_checks}: {message}");
    }
    private async Task Frames(int count = 2)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    private PhysicsShapeQueryParameters2D Query(CollisionShape2D shape, Vector2 origin, params Rid[] excluded)
    {
        Transform2D transform = shape.GlobalTransform;
        transform.Origin += origin - shape.GetParent<Node2D>().GlobalPosition;
        return new PhysicsShapeQueryParameters2D
        {
            Shape = shape.Shape, Transform = transform, CollisionMask = 1,
            CollideWithBodies = true, CollideWithAreas = false, Margin = 0.5f,
            Exclude = new Godot.Collections.Array<Rid>(excluded)
        };
    }
    private bool PlayerSpaceFree(Vector2 point) => _player.GetWorld2D().DirectSpaceState.IntersectShape(
        Query(_playerShape, point, _player.GetRid()), 1).Count == 0;
    private bool SafeInteractionPoint(Vector2 point)
    {
        if (!PlayerSpaceFree(point)) return false;
        var query = Query(_playerShape, point, _player.GetRid());
        query.CollideWithAreas = true;
        foreach (var hit in _player.GetWorld2D().DirectSpaceState.IntersectShape(query, 64))
            if (hit["collider"].AsGodotObject() is Trap) return false;
        return true;
    }
    private static Vector2 GridPoint(int x, int y) => new(185 + x * Step, 185 + y * Step);
    private void BuildReachableGrid(Vector2 start)
    {
        var open = new bool[Columns, Rows];
        Vector2I first = new(-1, -1);
        float nearest = float.MaxValue;
        for (int x = 0; x < Columns; x++)
            for (int y = 0; y < Rows; y++)
            {
                Vector2 point = GridPoint(x, y);
                if (!PlayerSpaceFree(point)) continue;
                open[x, y] = true;
                float distance = start.DistanceSquaredTo(point);
                if (distance < nearest) { first = new(x, y); nearest = distance; }
            }
        Check(first.X >= 0 && nearest < Step * Step, "出生位置可进入真实玩家尺寸的导航网格");
        var queue = new Queue<Vector2I>();
        queue.Enqueue(first); _reachable[first.X, first.Y] = true;
        while (queue.Count > 0)
        {
            Vector2I point = queue.Dequeue();
            foreach (Vector2I direction in new[] { Vector2I.Right, Vector2I.Left, Vector2I.Up, Vector2I.Down })
            {
                Vector2I next = point + direction;
                if (next.X < 0 || next.Y < 0 || next.X >= Columns || next.Y >= Rows ||
                    !open[next.X, next.Y] || _reachable[next.X, next.Y]) continue;
                // 连续扫掠完整玩家形状，避免相邻空格跨过窄实体或旋转墙角。
                var query = Query(_playerShape, GridPoint(point.X, point.Y), _player.GetRid());
                query.Motion = GridPoint(next.X, next.Y) - GridPoint(point.X, point.Y);
                float[] motion = _player.GetWorld2D().DirectSpaceState.CastMotion(query);
                if (motion.Length < 2 || motion[0] < 0.999f) continue;
                _reachable[next.X, next.Y] = true; queue.Enqueue(next);
            }
        }
    }
    private bool Reachable(Vector2 point)
    {
        if (!PlayerSpaceFree(point)) return false;
        Vector2 grid = (point - new Vector2(185, 185)) / Step;
        int centerX = (int)Mathf.Round(grid.X), centerY = (int)Mathf.Round(grid.Y);
        // The requested room marker does not have to land exactly on a grid
        // node. Search a small neighborhood and sweep the full player shape
        // into the marker, which avoids false negatives at wall corners.
        for (int x = Math.Max(0, centerX - 4); x <= Math.Min(Columns - 1, centerX + 4); x++)
            for (int y = Math.Max(0, centerY - 4); y <= Math.Min(Rows - 1, centerY + 4); y++)
            {
                if (!_reachable[x, y]) continue;
                var query = Query(_playerShape, GridPoint(x, y), _player.GetRid());
                query.Motion = point - GridPoint(x, y);
                float[] motion = _player.GetWorld2D().DirectSpaceState.CastMotion(query);
                if (motion.Length >= 2 && motion[0] >= 0.999f) return true;
            }
        return false;
    }
    private bool FindReachableApproach(Coffin coffin, out Vector2 point)
    {
        var solid = coffin.GetNode<StaticBody2D>("SolidBody");
        foreach (float radius in new[] { 90f, 105f, 75f })
            for (int angle = 0; angle < 16; angle++)
            {
                Vector2 candidate = coffin.GlobalPosition + Vector2.Right.Rotated(angle * Mathf.Tau / 16) * radius;
                if (!SafeInteractionPoint(candidate) || !Reachable(candidate)) continue;
                // The approach must really reach the coffin before a wall or
                // another prop. Simulate the same player sweep used below and
                // restore the candidate afterwards so this probe has no side
                // effects on the following checks.
                Vector2 previous = _player.GlobalPosition;
                _player.GlobalPosition = candidate;
                var collision = _player.MoveAndCollide(coffin.GlobalPosition - candidate);
                _player.GlobalPosition = previous;
                if (collision?.GetCollider() != solid) continue;
                point = candidate; return true;
            }
        point = Vector2.Zero; return false;
    }
    private Image ArtImage(Sprite2D sprite)
    {
        string path = sprite.Texture.ResourcePath;
        if (!_images.TryGetValue(path, out Image image))
        {
            image = GD.Load<Texture2D>(path)?.GetImage();
            if (image == null) throw new InvalidOperationException($"无法读取素材图像: {path}");
            _images[path] = image;
        }
        return image;
    }
    private Rect2 VisibleRect(Sprite2D sprite)
    {
        Rect2I used = ArtImage(sprite).GetUsedRect();
        Vector2 top = sprite.ToGlobal(sprite.GetRect().Position + used.Position);
        Vector2 bottom = sprite.ToGlobal(sprite.GetRect().Position + used.End);
        return new(top, bottom - top);
    }
    private bool EntireFootprintIsVisible(CollisionShape2D shape, Sprite2D sprite)
    {
        var rectangle = (RectangleShape2D)shape.Shape;
        Image image = ArtImage(sprite);
        // 检查实体内部和边缘；包括角落，不能只比较含大量透明留白的画布尺寸。
        for (float x = -rectangle.Size.X / 2; x <= rectangle.Size.X / 2 + 0.01f; x += 1)
            for (float y = -rectangle.Size.Y / 2; y <= rectangle.Size.Y / 2 + 0.01f; y += 1)
            {
                Vector2 pixel = sprite.ToLocal(shape.ToGlobal(new Vector2(x, y))) - sprite.GetRect().Position;
                int px = (int)Mathf.Round(pixel.X), py = (int)Mathf.Round(pixel.Y);
                if (px < 0 || py < 0 || px >= image.GetWidth() || py >= image.GetHeight() ||
                    image.GetPixel(px, py).A < 0.1f) return false;
            }
        return true;
    }
    private static Vector2[] Corners(Rect2 rectangle) => new[]
    {
        rectangle.Position, new Vector2(rectangle.End.X, rectangle.Position.Y),
        rectangle.End, new Vector2(rectangle.Position.X, rectangle.End.Y)
    };
    private static float PolygonDistance(Vector2[] a, Vector2[] b)
    {
        float result = float.MaxValue;
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
            {
                result = Mathf.Min(result, a[i].DistanceTo(Geometry2D.GetClosestPointToSegment(a[i], b[j], b[(j + 1) % 4])));
                result = Mathf.Min(result, b[j].DistanceTo(Geometry2D.GetClosestPointToSegment(b[j], a[i], a[(i + 1) % 4])));
            }
        return result;
    }
    private float NearestWall(Rect2 visual)
    {
        float result = float.MaxValue;
        Vector2[] corners = Corners(visual);
        foreach (var wall in _walls)
        {
            if (wall.Shape is RectangleShape2D rectangle)
            {
                Vector2[] polygon = Corners(new Rect2(-rectangle.Size / 2, rectangle.Size));
                for (int i = 0; i < 4; i++) polygon[i] = wall.ToGlobal(polygon[i]);
                result = Mathf.Min(result, PolygonDistance(corners, polygon));
            }
            else if (wall.Shape is WorldBoundaryShape2D boundary)
            {
                foreach (Vector2 point in corners)
                    result = Mathf.Min(result, Mathf.Abs(boundary.Normal.Dot(wall.ToLocal(point)) - boundary.Distance));
            }
        }
        return result;
    }
    private bool ArtDoesNotOverlapWall(Rect2 visual)
    {
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = new RectangleShape2D { Size = visual.Size },
            Transform = new Transform2D(0, visual.GetCenter()), CollisionMask = 1,
            CollideWithBodies = true, CollideWithAreas = false, Margin = 0
        };
        foreach (var hit in _player.GetWorld2D().DirectSpaceState.IntersectShape(query, 32))
            if (hit["collider"].AsGodotObject() is StaticBody2D body && body.GetParent() == _game.GetNode("background"))
                return false;
        return true;
    }
    private async void Run()
    {
        try
        {
            _game = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate<Node2D>();
            GetTree().Root.AddChild(_game); GetTree().CurrentScene = _game;
            _player = _game.GetNode<Player>("player"); _player.SetPhysicsProcess(false);
            _playerShape = _player.GetNode<CollisionShape2D>("CollisionShape2D");
            Vector2 start = _player.GlobalPosition;
            foreach (Node node in _game.GetChildren())
            {
                if (node is Coffin coffin) { _coffins.Add(coffin); coffin.SetPhysicsProcess(false); }
                if (node is Trap trap) { trap.SetPhysicsProcess(false); trap.Monitoring = false; }
            }
            foreach (Node body in _game.GetNode("background").GetChildren())
                if (body is StaticBody2D)
                    foreach (Node child in body.GetChildren()) if (child is CollisionShape2D shape) _walls.Add(shape);
            foreach (Node enemy in GetTree().GetNodesInGroup("zombie")) enemy.SetPhysicsProcess(false);
            await Frames();
            Check(_coffins.Count == 24, "二十四口贴墙棺材及其六种守卫均保留");
            var species = new Dictionary<int, int>();
            foreach (Coffin coffin in _coffins)
                foreach (Node child in coffin.GetChildren())
                    if (child is Zombie guardian)
                        species[guardian.MonsterId] = species.GetValueOrDefault(guardian.MonsterId) + 1;
            Check(species.Count == 6 && System.Linq.Enumerable.All(species.Values, count => count == 4),
                "六种守卫各有四口棺材，新增棺材保持种类均衡");
            BuildReachableGrid(start);
            foreach (Coffin coffin in _coffins)
            {
                var solid = coffin.GetNode<StaticBody2D>("SolidBody");
                var shape = solid.GetNode<CollisionShape2D>("CollisionShape2D");
                var size = ((RectangleShape2D)shape.Shape).Size;
                Check(size.X < 64 && size.Y < 54 && solid.CollisionLayer == 1 && !shape.Disabled,
                    $"{coffin.Name} 缩小实体脚印并保留阻挡");
                foreach (string name in new[] { "closed", "open" })
                {
                    var sprite = coffin.GetNode<Sprite2D>(name);
                    Check(EntireFootprintIsVisible(shape, sprite), $"{coffin.Name} {name} 实体完整落在非透明实像内部");
                    Rect2 visual = VisibleRect(sprite);
                    Check(NearestWall(visual) <= 10 && ArtDoesNotOverlapWall(visual),
                        $"{coffin.Name} {name} 图像贴近墙边且未压入墙体 (nearest={NearestWall(visual):0.0}, pos={coffin.GlobalPosition})");
                }
                Check(FindReachableApproach(coffin, out Vector2 approach),
                    $"{coffin.Name} 有从入口可达、无需站到危险机关上的交互点");
                _player.GlobalPosition = approach;
                var collision = _player.MoveAndCollide(coffin.GlobalPosition - approach);
                Check(collision?.GetCollider() == solid, $"{coffin.Name} 实际走向棺材中心仍被实体阻挡");
                _player.GlobalPosition = start;
            }
            var roomPoints = new Dictionary<string, Vector2>
            {
                ["入口下方通道"] = new(243, 1250), ["西南通道"] = new(250, 850),
                ["西侧中间墓室"] = new(480, 700), ["西北墓室"] = new(500, 260),
                ["北侧中央通道"] = new(800, 280), ["北侧窄墓室"] = new(1000, 230),
                ["中央西侧墓室"] = new(930, 650), ["中央东侧墓室"] = new(1200, 650),
                ["东北墓室"] = new(1450, 250), ["东侧上方通道"] = new(1900, 430),
                ["东侧中间墓室"] = new(1800, 730), ["中央南侧通道"] = new(1300, 1020),
                ["南侧中央墓室"] = new(1100, 1200), ["东南墓室"] = new(1600, 1250),
                ["开门机关位置"] = _game.GetNode<Node2D>("开门机关").GlobalPosition,
                ["出口门前通道"] = new(1965, 275)
            };
            foreach (var room in roomPoints)
                Check(Reachable(room.Value), $"入口到{room.Key}保留完整玩家体积的可行路线");
            _game.QueueFree(); await Frames(4); GetTree().CurrentScene = null;
            foreach (Image image in _images.Values) image.Dispose();
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            GC.Collect(); GC.WaitForPendingFinalizers(); await Frames();
            GD.Print($"RESULT COFFIN LAYOUT {_checks} passed, 0 failed"); GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError(exception.ToString()); GetTree().Quit(1); }
    }
}
