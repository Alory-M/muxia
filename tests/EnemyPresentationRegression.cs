using Godot;
using System;
using System.Threading.Tasks;

/// <summary>六种真实棺材守卫的出棺、步态、攻击及箭矢命中回归。</summary>
public partial class EnemyPresentationRegression : Node
{
    private int _checks;
    private Node2D _game;
    private Player _player;
    private readonly Vector2 _arena = new(10000, 10000);
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        GD.Print($"PASS {++_checks}: {message}");
    }
    private async Task Frames(int count = 2)
    {
        for (int i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
    private async Task Delay(double seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private bool OverlapsBody(Zombie zombie)
    {
        var shape = zombie.GetNode<CollisionShape2D>("CollisionShape2D");
        return zombie.GetWorld2D().DirectSpaceState.IntersectShape(new PhysicsShapeQueryParameters2D
        {
            Shape = shape.Shape, Transform = shape.GlobalTransform,
            CollisionMask = 1, CollideWithAreas = false, Margin = 0.1f,
            Exclude = new Godot.Collections.Array<Rid> { zombie.GetRid() }
        }, 1).Count != 0;
    }
    private async Task Species(string species)
    {
        _player.GlobalPosition = _arena + new Vector2(170, 0);
        var coffin = GD.Load<PackedScene>($"res://zombiegd/Fzombie/Co{species}.tscn").Instantiate<Coffin>();
        coffin.Position = _arena;
        _game.AddChild(coffin);
        Zombie zombie = null;
        foreach (Node child in coffin.GetChildren()) if (child is Zombie found) zombie = found;
        if (zombie == null) throw new InvalidOperationException($"{species}: missing guardian");
        zombie.SetPhysicsProcess(false);
        _player.GetNode<State>("state").ResetToNormal();
        await Frames();
        var normal = zombie.GetNode<Sprite2D>("normal");
        var attack = zombie.GetNode<Sprite2D>("attack");
        var motion = zombie.GetNode<ZombieMotionArt>("MotionArt");
        var shape = zombie.GetNode<CollisionShape2D>("CollisionShape2D");
        Vector2 scale = normal.Scale;
        Transform2D localShape = shape.Transform;
        string art = species switch
        {
            "Fast" => "shunyi", "Arrow" => "gongjian", "Sharp" => "lizhua",
            "Heavy" => "daju", "Poison" => "dujian", _ => "mini"
        };
        Check(normal.Texture.ResourcePath == $"res://assets/user/monster/{art}.png" &&
            attack.Texture.ResourcePath == $"res://assets/user/monster/{art}_attack.png",
            $"{species}: 实际使用新上传素材的待机和攻击立绘");
        foreach (string animation in new[] { "walk", "attack" })
        {
            Check(motion.SpriteFrames.GetFrameCount(animation) == 4,
                $"{species}: {animation}采用四张真实分帧图像");
            byte[] previous = null;
            for (int frame = 0; frame < 4; frame++)
            {
                var texture = motion.SpriteFrames.GetFrameTexture(animation, frame) as AtlasTexture;
                int row = animation == "walk" ? 0 : 1;
                Check(texture != null && texture.Atlas.ResourcePath == $"res://assets/generated/zombie_motion/{art}.png" &&
                    texture.Region.Position.IsEqualApprox(new Vector2(frame * texture.Atlas.GetWidth() / 4f,
                        row * texture.Atlas.GetHeight() / 2f)) &&
                    texture.Region.Size.IsEqualApprox(new Vector2(texture.Atlas.GetWidth() / 4f, texture.Atlas.GetHeight() / 2f)),
                    $"{species}: {animation}第{frame + 1}帧来自4×2图集的独立区域");
                Image image = texture.GetImage();
                byte[] pixels = image.GetData();
                Check(image.GetUsedRect().Size != Vector2I.Zero && image.DetectAlpha() != Image.AlphaMode.None &&
                    (previous == null || !pixels.AsSpan().SequenceEqual(previous)),
                    $"{species}: {animation}第{frame + 1}帧具有透明背景和不同的实际动作像素");
                previous = pixels;
            }
        }
        Rect2I normalPixels = normal.Texture.GetImage().GetUsedRect();
        Rect2I attackPixels = attack.Texture.GetImage().GetUsedRect();
        float attackBottom = attack.RegionEnabled ? Mathf.Min(attackPixels.End.Y, attack.RegionRect.End.Y) : attackPixels.End.Y;
        float normalFoot = normal.Position.Y + (normalPixels.End.Y - normal.Texture.GetHeight() / 2f) * normal.Scale.Y;
        float attackCenter = attack.RegionEnabled ? attack.RegionRect.Size.Y / 2f : attack.Texture.GetHeight() / 2f;
        float attackFoot = attack.Position.Y + (attackBottom - attackCenter) * attack.Scale.Y;
        float normalHeight = normalPixels.Size.Y * normal.Scale.Y;
        float attackHeight = (attackBottom - attackPixels.Position.Y) * attack.Scale.Y;
        Check(Mathf.Abs(normalFoot - attackFoot) < 0.8f && Mathf.Abs(normalHeight - attackHeight) < 0.9f,
            $"{species}: 不同画布的待机/攻击立绘脚底与身高校正一致");
        Check(!zombie.Visible && zombie.CurrentAnimation == "hidden" && !motion.Visible,
            $"{species}: 开棺前隐藏且不提前播放攻击姿态");
        coffin.SetPhysicsProcess(false);
        _player.GlobalPosition = _arena + new Vector2(90, 0); await Frames();
        coffin.SetPhysicsProcess(true); await Frames(3);
        Vector2 spawned = zombie.GlobalPosition;
        Transform2D spawnShape = shape.GlobalTransform;
        Check(zombie.IsActive && zombie.CurrentAnimation == "appear" && !OverlapsBody(zombie),
            $"{species}: 出棺选真实空位，不重叠玩家、棺材实体");
        Check(normal.Modulate.A > 0 && normal.Modulate.A < 1 && normal.Scale.Y < scale.Y &&
            shape.Transform.IsEqualApprox(localShape), $"{species}: 现身渐显升起只改变立绘，碰撞尺寸保留");
        var pause = new Stop(); AddChild(pause); pause.SetPaused(true);
        Color paused = normal.Modulate;
        await Delay(0.2);
        Check(normal.Modulate == paused && zombie.GlobalPosition.IsEqualApprox(spawned), $"{species}: 暂停冻结现身动画和身体");
        pause.SetPaused(false); await Delay(0.55);
        Check(zombie.CurrentAnimation == "idle" && normal.Scale.IsEqualApprox(scale) &&
            shape.GlobalTransform.IsEqualApprox(spawnShape), $"{species}: 现身完成恢复正常立绘且身体不被动画移动");

        _player.GlobalPosition = _arena + new Vector2(900, 0);
        zombie.GlobalPosition = zombie.ReturnPosition + new Vector2(0, 20);
        zombie._PhysicsProcess(1.0 / 60.0);
        Transform2D movingShape = shape.GlobalTransform;
        await Frames(4);
        Check(zombie.Velocity.LengthSquared() > 0 && zombie.CurrentAnimation == "move" &&
            motion.Visible && motion.Animation == "walk" && !normal.Visible && !attack.Visible &&
            shape.GlobalTransform.IsEqualApprox(movingShape),
            $"{species}: 返回安全点时播放真实步态分帧，立绘不移动碰撞体");
        int walkFrame = motion.Frame; await Frames(10);
        Check(motion.Frame != walkFrame && shape.GlobalTransform.IsEqualApprox(movingShape),
            $"{species}: 行走动作推进到下一张分帧而碰撞体不变");
        pause.SetPaused(true); Vector2 walkPosition = motion.Position; int pausedWalkFrame = motion.Frame;
        await Delay(0.1);
        Check(motion.Position.IsEqualApprox(walkPosition) && motion.Frame == pausedWalkFrame,
            $"{species}: 暂停冻结移动位置与分帧动画");
        pause.SetPaused(false);

        zombie.GlobalPosition = zombie.ReturnPosition;
        Vector2 inward = zombie.GlobalPosition.DirectionTo(_arena);
        _player.GlobalPosition = zombie.GlobalPosition + inward * 56;
        await Frames();
        float health = _player.hp;
        zombie._PhysicsProcess(1.0 / 60.0);
        Bullet fired = null;
        if (zombie is ArrowZom)
        {
            foreach (Node child in coffin.GetChildren()) if (child is Bullet bullet) fired = bullet;
            Check(fired != null && fired.HitsPlayer && fired.Damage == zombie.AttackDamage && fired.AppliesSlow == (zombie is PoisonZom),
                $"{species}: 真实射箭保留阵营、伤害和命中才中毒的标记");
            Check(fired.SceneFilePath == (zombie is PoisonZom ? "res://scenes/poison_arrow.tscn" : "res://scenes/enemy_arrow.tscn") &&
                fired.GetNode<Sprite2D>("ArrowSprite").Texture.ResourcePath == "res://bin/projectiles/trap_arrow.png",
                $"{species}: 射出箭矢图像而非子弹图标");
            if (zombie is PoisonZom)
            {
                var material = fired.GetNode<Sprite2D>("ArrowSprite").Material as ShaderMaterial;
                Check(material?.Shader.ResourcePath == "res://bin/projectiles/poison_arrow.gdshader" &&
                    material.GetShaderParameter("head_color").AsColor().R > 0.9f &&
                    material.GetShaderParameter("shaft_color").AsColor().B > 0.9f,
                    "Poison: 箭头红色、箭杆紫色，与僵尸立绘一致");
                Check(zombie.ArtFacesLeft && attack.FlipH == (_player.GlobalPosition.X > zombie.GlobalPosition.X) &&
                    motion.FlipH == (_player.GlobalPosition.X < zombie.GlobalPosition.X),
                    "Poison: 朝左的原立绘和朝右的分帧图集分别正确翻转面向玩家");
            }
        }
        Transform2D attackingShape = shape.GlobalTransform;
        await Frames(3);
        Check(zombie.CurrentAnimation == "attack" && motion.Visible && motion.Animation == "attack" &&
            !attack.Visible && !normal.Visible && shape.GlobalTransform.IsEqualApprox(attackingShape),
            $"{species}: 真正攻击时播放攻击图集，碰撞体不变");
        pause.SetPaused(true); Vector2 attackPosition = motion.Position; int attackFrame = motion.Frame;
        await Delay(0.1);
        Check(motion.Position.IsEqualApprox(attackPosition) && motion.Frame == attackFrame,
            $"{species}: 暂停冻结攻击动画分帧");
        pause.SetPaused(false);
        await Frames(8);
        Check(motion.Frame > attackFrame && shape.GlobalTransform.IsEqualApprox(attackingShape),
            $"{species}: 攻击推进真实分帧且身体不被动画移动");
        Check(_player.hp == health - zombie.AttackDamage &&
            (zombie is not PoisonZom || _player.GetNode<State>("state").IsPoisoned),
            $"{species}: 攻击动画对应真实近战/箭矢伤害，毒箭命中产生中毒");
        await Delay(0.45);
        Check(zombie.CurrentAnimation == "idle" && normal.Visible && !attack.Visible && !motion.Visible &&
            normal.Texture.ResourcePath == $"res://assets/user/monster/{art}.png",
            $"{species}: 攻击完成返回待机立绘");
        zombie.take_damage(1); await Frames(3);
        Check(zombie.CurrentAnimation == "hurt" && normal.Visible && normal.Modulate.G < 1 && !motion.Visible,
            $"{species}: 受击覆盖攻击状态并播放红闪后仰");
        await Delay(0.3);
        int deaths = 0; zombie.Died += () => deaths++;
        zombie.take_damage(10000); zombie.take_damage(10000); await Frames(3);
        Check(deaths == 1 && zombie.CurrentAnimation == "death" && !zombie.IsActive && zombie.CollisionLayer == 0 &&
            shape.Disabled && normal.Modulate.A < 1 && !motion.Visible,
            $"{species}: 死亡恢复原立绘倒地渐隐，解除碰撞且只结算一次");
        await Delay(0.55);
        Check(!GodotObject.IsInstanceValid(zombie), $"{species}: 死亡动画结束释放节点");
        pause.QueueFree(); coffin.QueueFree(); await Frames();
    }

    private async Task BlockedSpawn()
    {
        _player.GlobalPosition = _arena + new Vector2(170, 0);
        var coffin = GD.Load<PackedScene>("res://zombiegd/Fzombie/CoMini.tscn").Instantiate<Coffin>();
        coffin.Position = _arena; _game.AddChild(coffin);
        coffin.SetPhysicsProcess(false);
        Zombie zombie = null;
        foreach (Node child in coffin.GetChildren()) if (child is Zombie found) zombie = found;
        // 第二碰撞层仅用于守卫出棺查询，保留玩家到棺材的一层视线。
        zombie.CollisionMask |= 2;
        var blocker = new StaticBody2D { Position = _arena, CollisionLayer = 2 };
        blocker.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(400, 400) } });
        _game.AddChild(blocker);
        _player.GlobalPosition = _arena + new Vector2(90, 0); await Frames();
        coffin.SetPhysicsProcess(true);
        await Delay(0.35);
        Check(coffin.Status == Coffin.CoffinState.Fighting && !zombie.IsActive && !zombie.Visible && zombie.CollisionLayer == 0,
            "出棺周围没有可容纳真实碰撞体的空位时，等待而不挤入玩家/实体");
        blocker.QueueFree(); await Delay(0.35);
        Check(zombie.IsActive && zombie.Visible && !OverlapsBody(zombie), "空位释放后自动安全现身，不留下无法完成的棺材战斗");
        coffin.QueueFree(); await Frames();
    }

    private async Task VerticalMelee()
    {
        foreach (string species in new[] { "mini", "fast_move", "sharp", "heavy" })
        {
            foreach (Vector2 offset in new[]
            {
                new Vector2(0, -86), new Vector2(0, 86),
                new Vector2(0, -104), new Vector2(0, 104),
                new Vector2(-75, 0), new Vector2(75, 0)
            })
            {
                var zombie = GD.Load<PackedScene>($"res://zombiegd/{species}_zom.tscn").Instantiate<Zombie>();
                zombie.Position = _arena; _game.AddChild(zombie);
                zombie.SetPhysicsProcess(false);
                if (zombie is FastMoveZom teleporter) teleporter.TeleportTriggerRange = 0;
                _player.GlobalPosition = _arena + offset;
                var state = _player.GetNode<State>("state"); state.ResetToNormal();
                await Frames();
                float before = _player.hp;
                zombie._PhysicsProcess(1.0 / 60.0); await Frames();
                bool touching = Mathf.Abs(offset.Y) == 86;
                Check(touching
                    ? _player.hp == before - zombie.AttackDamage && zombie.CurrentAnimation == "attack"
                    : _player.hp == before && zombie.CurrentAnimation != "attack",
                    $"{species}: 偏移{offset}的真实上下贴身可近战，较远和横向范围外不攻击");
                if (zombie is SharpZom)
                    Check(state.IsBleeding == touching, $"Sharp: 偏移{offset}只有真实命中才产生流血");
                zombie.QueueFree(); await Frames();
            }
        }
    }

    private async Task TrapSpawn()
    {
        foreach (int mechanism in new[] { 1, 2 })
        {
            _player.GlobalPosition = _arena + new Vector2(170, 0);
            var coffin = GD.Load<PackedScene>("res://zombiegd/Fzombie/CoMini.tscn").Instantiate<Coffin>();
            coffin.Position = _arena; _game.AddChild(coffin);
            coffin.SetPhysicsProcess(false);
            Zombie zombie = null;
            foreach (Node child in coffin.GetChildren()) if (child is Zombie found) zombie = found;
            zombie.SetPhysicsProcess(false);
            var trap = GD.Load<PackedScene>("res://scenes/trap.tscn").Instantiate<Trap>();
            trap.MechanismId = mechanism; trap.Position = _arena + new Vector2(193, -43); _game.AddChild(trap);
            _player.GlobalPosition = _arena + new Vector2(90, 0); await Frames();
            coffin.SetPhysicsProcess(true);
            await Frames(4);
            Check(zombie.IsActive && zombie.Health == zombie.MaxHp && !trap.GetOverlappingBodies().Contains(zombie) && trap.ShotsFired == 0,
                $"机关{mechanism}: 出棺落点避开真实地刺/暗箭Area，不在出现动画中瞬间死亡或发射箭阵");
            coffin.QueueFree(); trap.QueueFree(); await Frames();
        }
    }

    private async void Run()
    {
        try
        {
            _game = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate<Node2D>();
            GetTree().Root.AddChild(_game); GetTree().CurrentScene = _game;
            _game.GetNode<StaticBody2D>("background/edge").CollisionLayer = 0;
            _player = _game.GetNode<Player>("player");
            _player.SetPhysicsProcess(false); _player.SetProcess(false); _player.SetProcessUnhandledInput(false);
            _player.GetNode<State>("state").SetProcess(false);
            foreach (string species in new[] { "Mini", "Fast", "Arrow", "Sharp", "Heavy", "Poison" }) await Species(species);
            await BlockedSpawn();
            await TrapSpawn();
            await VerticalMelee();
            _game.QueueFree(); await Frames(4);
            GC.Collect(); GC.WaitForPendingFinalizers(); await Frames();
            GD.Print($"ENEMY PRESENTATION RESULT: {_checks} passed, 0 failed"); GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"ENEMY PRESENTATION FAILED after {_checks}: {exception}"); GetTree().Quit(1);
        }
    }
}
