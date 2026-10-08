using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>使用游戏实际玩家/六种僵尸和 MoveAndSlide 验证挤夹脱困，不豁免实体碰撞。</summary>
public partial class CrowdingRegression : Node
{
    private Node2D _game;
    private Player _player;
    private readonly Vector2 _arena = new(10000, 10000);
    private readonly List<Node> _fixtures = new();
    private int _checks;
    public override void _Ready() => Callable.From(RunTests).CallDeferred();
    private void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException(description);
        _checks++; GD.Print($"PASS {_checks}: {description}");
    }
    private async Task Frames(int count = 2)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private StaticBody2D Wall(Vector2 offset, Vector2 size)
    {
        var wall = new StaticBody2D { Position = _arena + offset };
        wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size } });
        _game.AddChild(wall); _fixtures.Add(wall); return wall;
    }
    private Zombie Enemy(string species, Vector2 offset)
    {
        var enemy = GD.Load<PackedScene>($"res://zombiegd/{species}_zom.tscn").Instantiate<Zombie>();
        enemy.Position = _arena + offset;
        _game.AddChild(enemy); _fixtures.Add(enemy);
        enemy.MoveSpeed = 0; enemy.AttackDamage = 1; enemy.AttackCd = 100;
        if (enemy is FastMoveZom fast) fast.TeleportCooldown = 100;
        if (enemy is ArrowZom ranged) ranged.BulletScene = null;
        return enemy;
    }
    private bool OverlapsBody(CharacterBody2D body)
    {
        var shape = body.GetNode<CollisionShape2D>("CollisionShape2D");
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = shape.Shape, Transform = shape.GlobalTransform,
            CollisionMask = body.CollisionMask, CollideWithAreas = false,
            Exclude = new Godot.Collections.Array<Rid> { body.GetRid() }
        };
        return body.GetWorld2D().DirectSpaceState.IntersectShape(query, 1).Count > 0;
    }
    private async Task ResetFixtures()
    {
        foreach (string action in new[] { "move_left", "move_right", "move_up", "move_down" }) Input.ActionRelease(action);
        _player.SetPhysicsProcess(false);
        foreach (var node in _fixtures) if (GodotObject.IsInstanceValid(node)) node.QueueFree();
        _fixtures.Clear();
        _player.GlobalPosition = _arena;
        _player.Velocity = Vector2.Zero;
        await Frames(4);
    }
    private async Task WallCases()
    {
        foreach (int side in new[] { 1, -1 })
        {
            Wall(new Vector2(-side * 22, 0), new Vector2(20, 420));
            var zombie = Enemy("heavy", new Vector2(side * 38, 0));
            await Frames();
            Vector2 zombieStart = zombie.GlobalPosition;
            string action = side > 0 ? "move_right" : "move_left";
            Input.ActionPress(action); _player.SetPhysicsProcess(true); await Frames(80);
            Input.ActionRelease(action); _player.SetPhysicsProcess(false);
            Check((_player.GlobalPosition.X - _arena.X) * side > 65,
                $"墙在 {(side > 0 ? "左" : "右")} 侧：玩家持续按方向能实际绕过阻挡僵尸，pos={_player.GlobalPosition - _arena}");
            Check(!OverlapsBody(_player) && !OverlapsBody(zombie),
                $"脱困后玩家与僵尸均未进入墙体或其它实体 (player={_player.GlobalPosition}, zombie={zombie.GlobalPosition}, overlapPlayer={OverlapsBody(_player)}, overlapZombie={OverlapsBody(zombie)})");
            Check(zombie.GlobalPosition.DistanceTo(zombieStart) <= 112.1f && zombie.CollisionLayer != 0,
                $"让步在局部距离内完成，僵尸实体碰撞保留 (moved={zombie.GlobalPosition.DistanceTo(zombieStart):0.0}, start={zombieStart}, now={zombie.GlobalPosition})");
            await ResetFixtures();
        }
        Wall(new Vector2(-22, 0), new Vector2(20, 420));
        Wall(new Vector2(0, -59), new Vector2(420, 20));
        var corner = Enemy("sharp", new Vector2(38, 0));
        await Frames();
        Input.ActionPress("move_right"); _player.SetPhysicsProcess(true); await Frames(80);
        Input.ActionRelease("move_right"); _player.SetPhysicsProcess(false);
        Check(_player.GlobalPosition.X > _arena.X + 65 && corner.GlobalPosition.Y > _arena.Y,
            "墙角向一侧无空位时，僵尸朝另一侧让出真实通路");
        Check(!OverlapsBody(_player) && !OverlapsBody(corner), "墙角侧让不会把实体送进上方或侧面墙体");
        await ResetFixtures();

        Wall(new Vector2(-22, 0), new Vector2(20, 420));
        var front = Enemy("heavy", new Vector2(38, 0));
        var upper = Enemy("mini", new Vector2(0, -96));
        await Frames();
        Input.ActionPress("move_right"); _player.SetPhysicsProcess(true); await Frames(85);
        Input.ActionRelease("move_right"); _player.SetPhysicsProcess(false);
        Check(_player.GlobalPosition.X > _arena.X + 65, "两只僵尸合围且存在安全空间时玩家能走出");
        Check(!OverlapsBody(_player) && !OverlapsBody(front) && !OverlapsBody(upper),
            "双敌脱困不会把任何僵尸挤进另一僵尸或玩家体内");
        await ResetFixtures();
    }
    private async Task SafetyCases()
    {
        // 轻微 safe-margin 接触/重叠仍应可恢复，保留左侧墙作为真实约束。
        Wall(new Vector2(-22, 0), new Vector2(20, 420));
        var touching = Enemy("heavy", new Vector2(34.98f, 0));
        await Frames();
        Input.ActionPress("move_right"); _player.SetPhysicsProcess(true); await Frames(85);
        Input.ActionRelease("move_right"); _player.SetPhysicsProcess(false);
        Check(_player.GlobalPosition.X > _arena.X + 65 && !OverlapsBody(_player) && !OverlapsBody(touching),
            "近于安全余量的贴身接触也能解除，不因查询余量永远挤夹");
        await ResetFixtures();

        Wall(new Vector2(-22, 0), new Vector2(20, 420));
        Wall(new Vector2(74, 0), new Vector2(20, 420));
        Wall(new Vector2(26, -58), new Vector2(116, 20));
        Wall(new Vector2(26, 57), new Vector2(116, 20));
        var enclosed = Enemy("heavy", new Vector2(38, 0));
        await Frames();
        Vector2 start = enclosed.GlobalPosition;
        Input.ActionPress("move_right"); _player.SetPhysicsProcess(true); await Frames(70);
        Input.ActionRelease("move_right"); _player.SetPhysicsProcess(false);
        Check(!OverlapsBody(_player) && !OverlapsBody(enclosed) &&
            enclosed.GlobalPosition.DistanceTo(start) < 4 && _player.GlobalPosition.X < _arena.X + 10,
            "四周完全封闭时不以穿墙或重叠方式伪造脱困");
        Check(!enclosed.TryYieldToPlayer(_player, Vector2.Right, 1),
            "不存在能容纳完整碰撞体的空间时拒绝让步");
        await ResetFixtures();

        var paused = Enemy("mini", new Vector2(38, 0));
        await Frames();
        var modal = new Stop(); AddChild(modal); modal.SetPaused(true);
        Vector2 pausePosition = paused.GlobalPosition;
        Check(!paused.TryYieldToPlayer(_player, Vector2.Right, 1) && paused.GlobalPosition == pausePosition,
            "模态暂停期间挤让不移动僵尸");
        modal.SetPaused(false); modal.QueueFree(); await Frames();
        // 让步后仍能被命中、仍能贴身近战，并非关闭碰撞/接触。
        paused.SetPhysicsProcess(false); paused.GlobalPosition = _arena + new Vector2(38, 0);
        int health = paused.Health; paused.take_damage(1);
        Check(paused.Health == health - 1 && paused.CollisionLayer != 0, "让步机制保留僵尸可受伤的实体");
        _player.GlobalPosition = _arena; await Frames();
        float hp = _player.hp; paused.AttackDamage = 7; paused.AttackCd = 0;
        paused._PhysicsProcess(101); paused._PhysicsProcess(1);
        Check(_player.hp < hp, "贴身僵尸仍能实际伤害玩家");
        await ResetFixtures();
    }
    private async Task TeleportDashCases()
    {
        Wall(new Vector2(-22, 0), new Vector2(20, 420));
        var fast = (FastMoveZom)Enemy("fast_move", new Vector2(150, 0));
        fast.SetPhysicsProcess(false); fast.TeleportCooldown = 0;
        await Frames(); fast._PhysicsProcess(1); await Frames();
        Check(fast.GlobalPosition.DistanceTo(_player.GlobalPosition) <= fast.AttackRange + 0.1f && !OverlapsBody(fast),
            "扩大体积后瞬移贴墙仍选择真实空闲落点");
        fast.SetPhysicsProcess(true);
        Input.ActionPress("move_right"); _player.SetPhysicsProcess(true);
        _player._UnhandledInput(new InputEventAction { Action = "mouse_press2", Pressed = true });
        await Frames(85); Input.ActionRelease("move_right"); _player.SetPhysicsProcess(false);
        Check(!_player.IsDashing && _player.GlobalPosition.X > _arena.X + 65 && !OverlapsBody(_player) && !OverlapsBody(fast),
            "瞬移遭遇后冲刺正确结束，接着正常行走可以脱困");
        await ResetFixtures();

        // 原始实体棺材测试：从实际可站立的一侧开棺，随后向前冲刺。
        var coffin = GD.Load<PackedScene>("res://zombiegd/coffin.tscn").Instantiate<Coffin>();
        coffin.Position = _arena + new Vector2(90, 0);
        var guardian = GD.Load<PackedScene>("res://zombiegd/heavy_zom.tscn").Instantiate<Zombie>();
        coffin.AddChild(guardian); _game.AddChild(coffin); _fixtures.Add(coffin);
        await Frames();
        guardian.MoveSpeed = 0; guardian.AttackDamage = 1; guardian.AttackCd = 100;
        coffin.Interact(_player); await Frames(40);
        Check(guardian.IsActive && !OverlapsBody(guardian), "扩大僵尸出棺不与玩家、实体棺材重叠");
        Input.ActionPress("move_right"); _player.SetPhysicsProcess(true);
        _player._UnhandledInput(new InputEventAction { Action = "mouse_press2", Pressed = true });
        await Frames(40); Input.ActionRelease("move_right"); _player.SetPhysicsProcess(false);
        Check(!_player.IsDashing && !OverlapsBody(_player) && !OverlapsBody(guardian),
            "开棺紧接冲刺不会穿棺材、卡住冲刺或把新守卫挤进实体");
        await ResetFixtures();
    }
    private async void RunTests()
    {
        try
        {
            _game = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate<Node2D>();
            GetTree().Root.AddChild(_game); GetTree().CurrentScene = _game;
            _game.GetNode<StaticBody2D>("background/edge").CollisionLayer = 0;
            _player = _game.GetNode<Player>("player");
            _player.SetPhysicsProcess(false); _player.GetNode<State>("state").SetProcess(false);
            _player.GetNode("die").SetProcess(false);
            _player.GlobalPosition = _arena; await Frames();
            foreach (string species in new[] { "mini", "fast_move", "arrow", "sharp", "heavy", "poison" })
            {
                var zombie = Enemy(species, new Vector2(400, 0));
                zombie.SetPhysicsProcess(false);
                var node = zombie.GetNode<CollisionShape2D>("CollisionShape2D");
                var capsule = (CapsuleShape2D)node.Shape;
                Check(Mathf.IsEqualApprox(capsule.Radius * 2f * node.GlobalScale.X, 48f) &&
                    Mathf.IsEqualApprox(capsule.Height * node.GlobalScale.Y, 92f),
                    $"{species} 实体体积从44×84增加为48×92像素");
                await ResetFixtures();
            }
            Check(_player.EffectiveMoveSpeed == 500, "碰撞和脱困调整保留原移速500");
            await WallCases(); await SafetyCases(); await TeleportDashCases();
            _game.QueueFree(); await Frames(4);
            GC.Collect(); GC.WaitForPendingFinalizers(); await Frames();
            GD.Print($"CROWDING RESULT: {_checks} passed, 0 failed"); GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            foreach (string action in new[] { "move_left", "move_right", "move_up", "move_down" }) Input.ActionRelease(action);
            GD.PushError($"CROWDING FAILED after {_checks}: {ex}"); GetTree().Quit(1);
        }
    }
}
