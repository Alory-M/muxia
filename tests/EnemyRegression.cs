using Godot;
using System;
using System.Threading.Tasks;

/// <summary>瞬移落点、玩家脱离碰撞，以及六种僵尸的受击/死亡生命周期回归。</summary>
public partial class EnemyRegression : Node
{
    private int _checks;
    private Node2D _game;
    private Player _player;
    private readonly Vector2 _arena = new(10000, 10000);
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        _checks++; GD.Print($"PASS {_checks}: {description}");
    }
    private async Task Frames(int count = 2)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    private async Task Delay(double seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private StaticBody2D Wall(Vector2 offset, Vector2 size)
    {
        var wall = new StaticBody2D { Position = _arena + offset };
        wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size } });
        _game.AddChild(wall);
        return wall;
    }
    private bool OverlapsBody(Zombie zombie)
    {
        var collision = zombie.GetNode<CollisionShape2D>("CollisionShape2D");
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = collision.Shape, Transform = collision.GlobalTransform,
            CollisionMask = 1, CollideWithAreas = false,
            Exclude = new Godot.Collections.Array<Rid> { zombie.GetRid() }
        };
        return zombie.GetWorld2D().DirectSpaceState.IntersectShape(query, 1).Count > 0;
    }
    private bool PlayerOverlaps(Zombie zombie)
    {
        var shape = _player.GetNode<CollisionShape2D>("CollisionShape2D");
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = shape.Shape, Transform = shape.GlobalTransform,
            CollisionMask = zombie.CollisionLayer, CollideWithAreas = false,
            Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() }
        };
        foreach (var result in _player.GetWorld2D().DirectSpaceState.IntersectShape(query))
            if (result["collider"].AsGodotObject() == zombie) return true;
        return false;
    }
    private async Task OpeningOverlapCases()
    {
        // 按真实地图位置测试踩在棺材中心开棺，包含瞬移僵尸和周围原有墙体。
        foreach (Node node in _game.GetChildren())
        {
            if (node is not Coffin coffin) continue;
            Zombie guardian = null;
            foreach (Node child in coffin.GetChildren())
                if (child is Zombie found) { guardian = found; break; }
            if (guardian == null) continue;
            guardian.SetPhysicsProcess(false);
            _player.GlobalPosition = coffin.GlobalPosition;
            coffin.Interact(_player); await Frames();
            Check(PlayerOverlaps(guardian), $"{coffin.Name}: 确实复现站在棺材中心开棺的初始重叠");
            bool escaped = false;
            for (int direction = 0; direction < 8 && !escaped; direction++)
            {
                _player.GlobalPosition = coffin.GlobalPosition;
                guardian.GlobalPosition = coffin.GlobalPosition;
                await Frames();
                _player.Velocity = Vector2.Right.Rotated(direction * Mathf.Tau / 8f) * 200;
                for (int frame = 0; frame < 10; frame++) _player.MoveAndSlide();
                escaped = _player.GlobalPosition.DistanceTo(coffin.GlobalPosition) > 10 && !PlayerOverlaps(guardian);
            }
            _player.Velocity = Vector2.Zero;
            Check(escaped, $"{coffin.Name}: 真实玩家能从开棺初始重叠位置移动脱离");
            guardian.take_damage(10000); await Frames();
        }
    }
    private async Task TeleportCases()
    {
        // 大小足以容纳真实碰撞体，无法容纳原先遗漏 0.068 缩放的形状查询。
        var walls = new[]
        {
            Wall(new Vector2(-130, 0), new Vector2(20, 280)),
            Wall(new Vector2(130, 0), new Vector2(20, 280)),
            Wall(new Vector2(0, -130), new Vector2(280, 20)),
            Wall(new Vector2(0, 130), new Vector2(280, 20))
        };
        var zombie = GD.Load<PackedScene>("res://zombiegd/fast_move_zom.tscn").Instantiate<FastMoveZom>();
        zombie.Position = _arena;
        _game.AddChild(zombie); zombie.SetPhysicsProcess(false);
        zombie.MoveSpeed = 0; zombie.AttackDamage = 0; zombie.TeleportCooldown = 0;
        await Frames();
        for (int angle = 0; angle < 16; angle++)
        {
            _player.GlobalPosition = _arena;
            zombie.GlobalPosition = _arena + Vector2.Right.Rotated(angle * Mathf.Tau / 16f) * 240;
            await Frames();
            Vector2 before = zombie.GlobalPosition;
            zombie._PhysicsProcess(1);
            Check(zombie.GlobalPosition.DistanceTo(before) > 100 &&
                zombie.GlobalPosition.DistanceTo(_player.GlobalPosition) <= zombie.AttackRange + 0.1f &&
                !OverlapsBody(zombie), $"方向 {angle}: 按真实缩放瞬移且不重叠玩家或墙体；{before} → {zombie.GlobalPosition}, 玩家 {_player.GlobalPosition}, 重叠 {OverlapsBody(zombie)}");
        }
        Vector2 playerStart = _player.GlobalPosition;
        bool canMove = false;
        for (int direction = 0; direction < 8; direction++)
        {
            _player.GlobalPosition = playerStart;
            _player.Velocity = Vector2.Right.Rotated(direction * Mathf.Tau / 8f) * 200;
            _player.MoveAndSlide();
            canMove |= _player.GlobalPosition.DistanceTo(playerStart) > 1;
        }
        _player.Velocity = Vector2.Zero;
        Check(canMove, "玩家瞬移遭遇后仍可实际移动脱离");
        // 增加障碍，强制放弃最优落点并从其他方向落地。
        _player.GlobalPosition = _arena;
        var blocker = Wall(new Vector2(45, 0), new Vector2(30, 160));
        zombie.GlobalPosition = _arena + new Vector2(240, 0); await Frames();
        zombie._PhysicsProcess(1);
        Check(zombie.GlobalPosition.DistanceTo(_player.GlobalPosition) <= zombie.AttackRange + 0.1f &&
            zombie.GlobalPosition.X < _player.GlobalPosition.X && !OverlapsBody(zombie), "原落点被墙挡住时改选安全方向");
        blocker.QueueFree(); zombie.QueueFree();
        foreach (var wall in walls) wall.QueueFree();
        await Frames();

        // 狭窄通道只容纳玩家，所有瞬移候选均被墙挡住：不得强行重叠。
        var left = Wall(new Vector2(-35, 0), new Vector2(20, 300));
        var right = Wall(new Vector2(35, 0), new Vector2(20, 300));
        var trapped = GD.Load<PackedScene>("res://zombiegd/fast_move_zom.tscn").Instantiate<FastMoveZom>();
        trapped.Position = _arena + new Vector2(0, -240);
        _game.AddChild(trapped); trapped.SetPhysicsProcess(false);
        trapped.MoveSpeed = 0; await Frames();
        Vector2 start = trapped.GlobalPosition; trapped._PhysicsProcess(1);
        Check(trapped.GlobalPosition.IsEqualApprox(start), "无安全落点时回退普通追击而不传入玩家体内");
        trapped.QueueFree(); left.QueueFree(); right.QueueFree(); await Frames();
    }
    private async Task AnimationCases()
    {
        var pause = new Stop(); AddChild(pause);
        foreach (string species in new[] { "mini", "fast_move", "arrow", "sharp", "heavy", "poison" })
        {
            var zombie = GD.Load<PackedScene>($"res://zombiegd/{species}_zom.tscn").Instantiate<Zombie>();
            zombie.Position = _arena + new Vector2(100, 0);
            _game.AddChild(zombie); zombie.SetPhysicsProcess(false);
            var sprite = zombie.GetNode<Sprite2D>("normal");
            var shape = zombie.GetNode<CollisionShape2D>("CollisionShape2D");
            Transform2D shapeTransform = shape.GlobalTransform;
            Vector2 originalPosition = sprite.Position, originalScale = sprite.Scale;
            Color originalColor = sprite.Modulate;
            int health = zombie.Health, deaths = 0;
            zombie.Died += () => deaths++;
            zombie.take_damage(1); await Frames(3);
            Check(zombie.Health == health - 1 && sprite.Modulate != originalColor &&
                !sprite.Scale.IsEqualApprox(originalScale) && shape.GlobalTransform.IsEqualApprox(shapeTransform),
                $"{species}: 受击红闪与后仰只影响精灵，伤害有效且碰撞体不动");
            await Delay(0.3);
            Check(sprite.Position.IsEqualApprox(originalPosition) && sprite.Scale.IsEqualApprox(originalScale) &&
                sprite.Modulate == originalColor, $"{species}: 受击动画完整恢复初始姿态");
            zombie.take_damage(1); zombie.take_damage(1); await Delay(0.3);
            Check(sprite.Position.IsEqualApprox(originalPosition) && sprite.Scale.IsEqualApprox(originalScale),
                $"{species}: 连续受击不累计位移或缩放");
            zombie.take_damage(10000);
            var soul = _player.GetNode<soulpiece>("soulpiece");
            int reward = soul.GetSoul(); zombie.take_damage(10000);
            Check(zombie.Health == 0 && !zombie.IsActive && deaths == 1 && soul.GetSoul() == reward &&
                !zombie.IsQueuedForDeletion(), $"{species}: 死亡只结算一次，保留节点播放动画");
            await Frames(5);
            Check(zombie.CollisionLayer == 0 && shape.Disabled && sprite.Modulate.A < originalColor.A &&
                !Mathf.IsZeroApprox(sprite.Rotation), $"{species}: 死亡立即解除阻挡并倒地渐隐");
            pause.SetPaused(true);
            Color pausedColor = sprite.Modulate;
            await Delay(0.55);
            Check(GodotObject.IsInstanceValid(zombie) && sprite.Modulate == pausedColor,
                $"{species}: 模态暂停同时冻结死亡动画");
            pause.SetPaused(false); await Delay(0.55);
            Check(!GodotObject.IsInstanceValid(zombie), $"{species}: 解除暂停后动画结束并释放");
        }
        pause.QueueFree();
    }
    private async void Run()
    {
        try
        {
            _game = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate<Node2D>();
            GetTree().Root.AddChild(_game); GetTree().CurrentScene = _game;
            _player = _game.GetNode<Player>("player");
            // 禁用输入更新而保持 CollisionObject 在物理空间中；Disabled 模式会移走碰撞体。
            _player.SetProcess(false); _player.SetPhysicsProcess(false); _player.SetProcessUnhandledInput(false);
            _player.GetNode<State>("state").SetProcess(false);
            await OpeningOverlapCases();
            // 专用物理场地位于地图之外，关闭游戏的无限边界，保留场景中的真实玩家与道具。
            _game.GetNode<StaticBody2D>("background/edge").CollisionLayer = 0;
            _player.GlobalPosition = _arena; await Frames();
            await TeleportCases(); await AnimationCases();
            _game.QueueFree(); await Frames(4);
            GD.Print($"ENEMY RESULT: {_checks} passed, 0 failed"); GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"ENEMY REGRESSION FAILED after {_checks} passes: {exception}"); GetTree().Quit(1);
        }
    }
}
