using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>在真实物理世界中检查实体占位、出棺空位、机关踩踏与迷宫可达性。</summary>
public partial class WorldExpansionRegression : Node
{
    private static readonly Vector2 CellSize = new(91.58f, 88.688f);
    private static readonly Vector2 FirstCellCenter = new(298.84f, 232.324f);
    private Node2D _game;
    private Player _player;
    private int _checks;
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        GD.Print($"PASS WORLD {++_checks}: {message}");
    }
    private async Task Frames(int count = 2)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    private PhysicsShapeQueryParameters2D Query(CollisionShape2D shape, Vector2 origin, params Rid[] exclusions)
    {
        Transform2D transform = shape.GlobalTransform;
        transform.Origin += origin - shape.GetParent<Node2D>().GlobalPosition;
        return new PhysicsShapeQueryParameters2D
        {
            Shape = shape.Shape, Transform = transform, CollisionMask = 1,
            CollideWithBodies = true, CollideWithAreas = false, Margin = 1,
            Exclude = new Godot.Collections.Array<Rid>(exclusions)
        };
    }
    private bool IsPlayerSpaceFree(Vector2 position)
    {
        var shape = _player.GetNode<CollisionShape2D>("CollisionShape2D");
        return _player.GetWorld2D().DirectSpaceState.IntersectShape(Query(shape, position, _player.GetRid()), 1).Count == 0;
    }
    private bool FindApproach(Node2D prop, out Vector2 approach)
    {
        foreach (Vector2 direction in new[] { Vector2.Right, Vector2.Left, Vector2.Up, Vector2.Down })
        {
            Vector2 point = prop.GlobalPosition + direction * 90;
            if (!IsPlayerSpaceFree(point)) continue;
            approach = point;
            return true;
        }
        approach = Vector2.Zero;
        return false;
    }
    private bool ActualPlayerMovementHits(StaticBody2D solid)
    {
        foreach (Vector2 direction in new[] { Vector2.Right, Vector2.Left, Vector2.Up, Vector2.Down })
        {
            Vector2 point = solid.GetParent<Node2D>().GlobalPosition + direction * 90;
            if (!IsPlayerSpaceFree(point)) continue;
            _player.GlobalPosition = point;
            KinematicCollision2D collision = _player.MoveAndCollide(-direction * 140);
            if (collision?.GetCollider() == solid) return true;
        }
        return false;
    }
    private bool HasRouteToExit(Vector2 start)
    {
        // 步长小于墙体加玩家体积的总厚度，连续的相邻空格不能跨过实体墙。
        const int columns = 37, rows = 24;
        var open = new bool[columns, rows];
        Vector2 GridPoint(int x, int y) => new(175 + x * 50, 175 + y * 50);
        for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++) open[x, y] = IsPlayerSpaceFree(GridPoint(x, y));
        Vector2I first = new(-1, -1);
        float nearest = float.MaxValue;
        for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
            {
                if (!open[x, y]) continue;
                float distance = GridPoint(x, y).DistanceSquaredTo(start);
                if (distance < nearest) { first = new Vector2I(x, y); nearest = distance; }
            }
        if (nearest > 50 * 50 || first.X < 0) return false;
        var visited = new bool[columns, rows];
        var queue = new Queue<Vector2I>(); queue.Enqueue(first); visited[first.X, first.Y] = true;
        while (queue.Count > 0)
        {
            Vector2I current = queue.Dequeue();
            if (current.X == columns - 1 && current.Y == 2) return true;
            foreach (Vector2I step in new[] { Vector2I.Right, Vector2I.Left, Vector2I.Up, Vector2I.Down })
            {
                Vector2I next = current + step;
                if (next.X < 0 || next.Y < 0 || next.X >= columns || next.Y >= rows ||
                    !open[next.X, next.Y] || visited[next.X, next.Y]) continue;
                visited[next.X, next.Y] = true; queue.Enqueue(next);
            }
        }
        return false;
    }
    private static Zombie Guardian(Coffin coffin)
    {
        foreach (Node child in coffin.GetChildren()) if (child is Zombie zombie) return zombie;
        throw new InvalidOperationException($"{coffin.Name} 没有守卫");
    }
    private void CheckFloorTile(Node2D floor, Sprite2D sprite)
    {
        Vector2 cell = (floor.GlobalPosition - FirstCellCenter) / CellSize;
        Check(cell.DistanceTo(cell.Round()) < 0.001f && sprite.GlobalPosition.DistanceTo(floor.GlobalPosition) < 0.01f,
            $"{floor.Name} 的图像与踩踏区域位于同一地砖中心");
        Vector2 displayedSize = sprite.GetRect().Size * sprite.GlobalScale;
        Check(displayedSize.DistanceTo(CellSize) < 0.1f && sprite.Texture is AtlasTexture &&
            floor.ZIndex < _player.ZIndex && floor.ZIndex > _game.GetNode<Control>("background").ZIndex,
            $"{floor.Name} 裁去透明留白、覆盖单块地砖并位于人物下方");
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = new RectangleShape2D { Size = displayedSize },
            Transform = new Transform2D(0, floor.GlobalPosition), CollisionMask = 1,
            CollideWithAreas = false, Margin = 1,
            Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() }
        };
        Check(_player.GetWorld2D().DirectSpaceState.IntersectShape(query, 1).Count == 0,
            $"{floor.Name} 完整地砖图像未与墙体、棺材或宝箱重叠");
    }
    private async void Run()
    {
        try
        {
            _game = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate<Node2D>();
            GetTree().Root.AddChild(_game); GetTree().CurrentScene = _game;
            _player = _game.GetNode<Player>("player"); _player.SetPhysicsProcess(false);
            Vector2 initialPosition = _player.GlobalPosition;
            var coffins = new List<Coffin>(); var traps = new List<Trap>();
            foreach (Node child in _game.GetChildren())
            {
                if (child is Coffin coffin) coffins.Add(coffin);
                if (child is Trap trap) { traps.Add(trap); trap.SetPhysicsProcess(false); trap.Monitoring = false; }
            }
            foreach (Node node in GetTree().GetNodesInGroup("zombie")) node.SetPhysicsProcess(false);
            await Frames();
            Check(coffins.Count == 16 && traps.Count == 12, "地图扩充为十六口棺材、十二块危险机关");
            var species = new Dictionary<int, int>();
            foreach (Coffin coffin in coffins)
            {
                int id = Guardian(coffin).MonsterId;
                species[id] = species.GetValueOrDefault(id) + 1;
            }
            Check(species.Count == 6 && System.Linq.Enumerable.All(species.Values, count => count >= 2), "六种僵尸均至少出现两次");
            foreach (Trap trap in traps) CheckFloorTile(trap, trap.GetNode<Sprite2D>("Tile"));
            var gate = _game.GetNode<开门机关>("开门机关");
            CheckFloorTile(gate, gate.GetNode<Sprite2D>("Sprite2D"));
            int propCount = 0;
            foreach (Node node in GetTree().GetNodesInGroup("solid_prop"))
            {
                var solid = (StaticBody2D)node;
                var shape = solid.GetNode<CollisionShape2D>("CollisionShape2D");
                string label = solid.GetParent().Name;
                Check(solid.CollisionLayer == 1 && !shape.Disabled, $"{label} 的实体碰撞已启用");
                Check(_player.GetWorld2D().DirectSpaceState.IntersectShape(Query(shape, solid.GlobalPosition,
                    solid.GetRid(), _player.GetRid()), 1).Count == 0, $"{label} 未与墙体或其他道具重叠");
                Check(FindApproach(solid.GetParent<Node2D>(), out _), $"{label} 可从旁边靠近交互");
                Check(ActualPlayerMovementHits(solid), $"实际玩家移动被 {label} 阻挡，无法穿过所在位置");
                _player.GlobalPosition = initialPosition; propCount++;
            }
            Check(propCount == 20, "十六口棺材和四个宝箱全部具有实体占位");
            Check(HasRouteToExit(initialPosition), "新增实体保留从入口到右上方出口的可行路线");
            foreach (Coffin coffin in coffins)
            {
                Check(FindApproach(coffin, out Vector2 point), $"{coffin.Name} 开棺前有安全玩家位置");
                _player.GlobalPosition = point; await Frames();
                coffin.Interact(_player); await Frames();
                Zombie guardian = Guardian(coffin);
                Check(guardian.IsActive && guardian.Visible && guardian.ReturnPosition.DistanceTo(coffin.GlobalPosition) >= 60,
                    $"{coffin.Name} 的守卫出现在实体棺材之外");
                var shape = guardian.GetNode<CollisionShape2D>("CollisionShape2D");
                Check(_player.GetWorld2D().DirectSpaceState.IntersectShape(Query(shape, guardian.GlobalPosition,
                    guardian.GetRid()), 1).Count == 0, $"{coffin.Name} 的守卫未挤进玩家、墙体或道具");
            }
            var spike = _game.GetNode<Trap>("SpikeTrap3");
            _player.GlobalPosition = spike.GlobalPosition + new Vector2(-100, 0); await Frames();
            spike.Monitoring = true; spike.SetPhysicsProcess(true); await Frames();
            float health = _player.hp;
            _player.MoveAndCollide(new Vector2(100, 0)); await Frames(4);
            Check(_player.hp == health - 60, "玩家可踩到贴地机关，真实移动触发地刺伤害");
            _player.GlobalPosition = gate.GlobalPosition + new Vector2(-120, -40); await Frames();
            KinematicCollision2D obstacle = _player.MoveAndCollide(new Vector2(120, 0)); await Frames(4);
            Check(obstacle == null && gate.IsUnlocked && gate.IsOpen, "开门地砖位于可走位置，实际踩踏打开墓门");
            _game.QueueFree(); await Frames(3); GetTree().CurrentScene = null;
            Check(!Stop.IsPaused, "离开扩充地图释放全部暂停状态");
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            GC.Collect(); GC.WaitForPendingFinalizers(); await Frames();
            GD.Print($"RESULT WORLD {_checks} passed, 0 failed"); GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError(exception.ToString()); GetTree().Quit(1); }
    }
}
