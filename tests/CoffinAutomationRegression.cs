using Godot;
using System;
using System.Threading.Tasks;

/// <summary>真实靠近开棺、解除碰撞、战后冷色光效及独立物资奖励验证。</summary>
public partial class CoffinAutomationRegression : Node
{
    private Node2D _game;
    private Player _player;
    private int _checks;
    private readonly Vector2 _arena = new(10000, 10000);
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        GD.Print($"PASS COFFIN AUTOMATION {++_checks}: {message}");
    }
    private async Task Frames(int count = 3)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private static Zombie Guardian(Coffin coffin)
    {
        foreach (Node child in coffin.GetChildren()) if (child is Zombie guardian) return guardian;
        throw new InvalidOperationException("棺材没有守卫");
    }
    private async Task AutomaticOpening(string scene, int index)
    {
        Vector2 point = _arena + Vector2.Right * index * 1000;
        _player.GlobalPosition = point + new Vector2(180, 0);
        var coffin = GD.Load<PackedScene>($"res://zombiegd/Fzombie/{scene}.tscn").Instantiate<Coffin>();
        coffin.Position = point; _game.AddChild(coffin);
        Zombie guardian = Guardian(coffin); guardian.SetPhysicsProcess(false);
        var solid = coffin.GetNode<StaticBody2D>("SolidBody");
        var shape = solid.GetNode<CollisionShape2D>("CollisionShape2D");
        int opens = 0;
        bool collisionRemovedBeforeSignal = false;
        coffin.PlayerEntered += player =>
        {
            opens++;
            var ray = PhysicsRayQueryParameters2D.Create(point + new Vector2(-60, 6), point + new Vector2(60, 6), 1,
                new Godot.Collections.Array<Rid> { _player.GetRid(), guardian.GetRid() });
            collisionRemovedBeforeSignal = solid.CollisionLayer == 0 &&
                coffin.GetWorld2D().DirectSpaceState.IntersectRay(ray).Count == 0;
        };
        await Frames();
        coffin.Interact(_player);
        Check(coffin.Status == Coffin.CoffinState.Closed && !coffin.CanInteract && !guardian.IsActive &&
            solid.CollisionLayer == 1 && !shape.Disabled, $"{scene} 远处保持封闭，F 不再用于开棺");
        var pause = new Stop(); AddChild(pause); pause.SetPaused(true);
        _player.GlobalPosition = point + new Vector2(90, 0); await Frames();
        Check(coffin.Status == Coffin.CoffinState.Closed && opens == 0, $"{scene} 暂停时靠近不会开棺");
        pause.SetPaused(false); await Frames();
        Check(coffin.Status == Coffin.CoffinState.Fighting && guardian.IsActive && guardian.Visible &&
            opens == 1 && collisionRemovedBeforeSignal && solid.CollisionLayer == 0 && shape.Disabled,
            $"{scene} 靠近自动开棺一次，守卫现身前已解除实体碰撞");
        Check(!coffin.CanInteract && coffin.LootGlowStrength == 0,
            $"{scene} 战斗期间不能摸棺且不提前发光");
        guardian.CollisionLayer = 0; guardian.CollisionMask = 0;
        Check(_player.MoveAndCollide(point - _player.GlobalPosition) == null &&
            _player.GlobalPosition.DistanceTo(point) < 0.1f, $"{scene} 玩家实际移动可穿过已打开棺材");
        guardian.take_damage(guardian.Health);
        var material = (ShaderMaterial)coffin.GetNode<Sprite2D>("open").Material;
        Color color = material.GetShaderParameter("glow_color").AsColor();
        Check(coffin.Status == Coffin.CoffinState.Unlocked && coffin.CanInteract && coffin.LootGlowStrength >= 0.68f &&
            color.B > color.R && color.G > 0.9f, $"{scene} 击杀后点亮明显的冰蓝白摸棺提示");
        coffin.Interact(_player);
        Check(coffin.Status == Coffin.CoffinState.Looted && !coffin.CanInteract && coffin.LootGlowStrength == 0,
            $"{scene} F 摸棺结算后熄灭光效");
        var gold = _player.GetNode<Gold>("gold"); int amount = gold.Amount;
        coffin.Interact(_player); await Frames();
        Check(gold.Amount == amount && opens == 1 && solid.CollisionLayer == 0 && shape.Disabled,
            $"{scene} 已摸棺不可重复领奖、重新开棺或恢复碰撞");
        coffin.QueueFree(); pause.QueueFree(); await Frames();
    }
    private async Task WallAndDeathGuards()
    {
        Vector2 point = _arena + new Vector2(0, 1000);
        _player.GlobalPosition = point + new Vector2(90, 0);
        var wall = new StaticBody2D { Position = point + new Vector2(45, 0), CollisionLayer = 1 };
        wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(8, 160) } });
        _game.AddChild(wall);
        var coffin = GD.Load<PackedScene>("res://zombiegd/coffin.tscn").Instantiate<Coffin>();
        coffin.Position = point; _game.AddChild(coffin); await Frames();
        Check(coffin.Status == Coffin.CoffinState.Closed, "隔墙靠近不会开启相邻墓室的棺材");
        wall.QueueFree(); _player.hp = 0; await Frames();
        Check(coffin.Status == Coffin.CoffinState.Closed, "死亡玩家不会自动开棺");
        _player.hp = _player.MaxHp; await Frames();
        Check(coffin.Status == Coffin.CoffinState.Unlocked && coffin.CanInteract && coffin.LootGlowEnabled,
            "无守卫棺材靠近即开启并可摸棺");
        coffin.QueueFree(); await Frames();
    }
    private async Task BonusRewards()
    {
        GD.Seed(20261009);
        var gold = _player.GetNode<Gold>("gold");
        var pack = _player.GetNode<Pack>("pack");
        bool ammo = false, drug = false, bandage = false, antidote = false, goldOnly = false, multiple = false;
        for (int index = 0; index < 64; index++)
        {
            Vector2 point = _arena + new Vector2(0, 2000);
            _player.GlobalPosition = point + new Vector2(90, 0);
            var coffin = GD.Load<PackedScene>("res://zombiegd/coffin.tscn").Instantiate<Coffin>();
            coffin.Position = point; _game.AddChild(coffin); await Frames(2);
            int beforeGold = gold.Amount, beforeAmmo = pack.GetReserveBullet(), magazine = pack.GetCount(SupplyKind.Bullet);
            int beforeDrug = pack.GetCount(SupplyKind.Drug), beforeBandage = pack.GetCount(SupplyKind.Bandage);
            int beforeAntidote = pack.GetCount(SupplyKind.Antidote);
            coffin.Interact(_player);
            int reward = gold.Amount - beforeGold;
            int ammoCount = pack.GetReserveBullet() - beforeAmmo, drugCount = pack.GetCount(SupplyKind.Drug) - beforeDrug;
            int bandageCount = pack.GetCount(SupplyKind.Bandage) - beforeBandage;
            int antidoteCount = pack.GetCount(SupplyKind.Antidote) - beforeAntidote;
            if (coffin.Status != Coffin.CoffinState.Looted ||
                (reward != 300 && reward != 400 && reward != 500 && reward != 800) ||
                (ammoCount != 0 && ammoCount != 8) || drugCount < 0 || drugCount > 1 ||
                bandageCount < 0 || bandageCount > 1 || antidoteCount < 0 || antidoteCount > 1 ||
                pack.GetCount(SupplyKind.Bullet) != magazine)
                throw new InvalidOperationException($"第 {index + 1} 次摸棺奖励或备用弹药结算异常");
            ammo |= ammoCount > 0; drug |= drugCount > 0; bandage |= bandageCount > 0; antidote |= antidoteCount > 0;
            int supplies = (ammoCount > 0 ? 1 : 0) + drugCount + bandageCount + antidoteCount;
            goldOnly |= supplies == 0; multiple |= supplies > 1;
            coffin.QueueFree(); await Frames(2);
        }
        Check(ammo && drug && bandage && antidote, "固定随机种子下六十四次真实摸棺均保留财宝，并覆盖四类额外物资");
        Check(goldOnly && multiple, "额外物资按概率独立抽取，覆盖仅财宝和同时获得多种物资");
    }
    private async void Run()
    {
        try
        {
            _game = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate<Node2D>();
            GetTree().Root.AddChild(_game); GetTree().CurrentScene = _game;
            _player = _game.GetNode<Player>("player"); _player.SetPhysicsProcess(false);
            _player.GetNode("die").SetProcess(false);
            _game.GetNode<StaticBody2D>("background/edge").CollisionLayer = 0;
            _player.GlobalPosition = _arena + new Vector2(180, 0);
            foreach (Node node in GetTree().GetNodesInGroup("zombie")) node.SetPhysicsProcess(false);
            foreach (Node node in _game.GetChildren()) if (node is Coffin coffin) coffin.SetPhysicsProcess(false);
            await Frames();
            int index = 0;
            foreach (string scene in new[] { "CoMini", "CoFast", "CoSharp", "CoPoison", "CoArrow", "CoHeavy" })
                await AutomaticOpening(scene, index++);
            await WallAndDeathGuards(); await BonusRewards();
            _game.QueueFree(); await Frames(4); GetTree().CurrentScene = null;
            GC.Collect(); GC.WaitForPendingFinalizers(); await Frames();
            GD.Print($"RESULT COFFIN AUTOMATION {_checks} passed, 0 failed"); GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError(exception.ToString()); GetTree().Quit(1); }
    }
}
