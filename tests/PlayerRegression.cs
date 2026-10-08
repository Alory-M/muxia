using Godot;
using System;
using System.Threading.Tasks;

/// <summary>验证真实快捷键/背包使用和高速、近身子弹的物理命中。</summary>
public partial class PlayerRegression : Node
{
    private int _checks;
    private Node2D _game;
    private Player _player;
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException(description);
        _checks++; GD.Print($"PASS {_checks}: {description}");
    }
    private async Task Frames(int count = 2)
    { for (int i = 0; i < count; i++)
      { await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); } }
    private static void Action(string name)
    {
        Key key = name == "Q" ? Key.Q : name == "Z" ? Key.Z : Key.E;
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Unicode = (uint)name.ToLowerInvariant()[0], Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        Input.FlushBufferedEvents();
    }
    private Bullet Projectile(Vector2 position, Vector2 direction, float speed = 800)
    {
        var bullet = GD.Load<PackedScene>("res://scenes/bullet.tscn").Instantiate<Bullet>();
        bullet.Damage = 5; bullet.Speed = speed;
        _game.AddChild(bullet); bullet.GlobalPosition = position; bullet.Launch(direction);
        return bullet;
    }
    private async Task Items()
    {
        var pack = _player.GetNode<Pack>("pack");
        var state = _player.GetNode<State>("state"); state.SetProcess(false);
        var clear = _player.GetNode<Clear>("clear");
        pack.SetCount(SupplyKind.Drug, 5); pack.SetCount(SupplyKind.Bandage, 5); pack.SetCount(SupplyKind.Antidote, 5);
        var focus = new LineEdit { Size = new Vector2(100, 40) }; _game.GetNode("HUD").AddChild(focus); focus.GrabFocus();
        _player.hp = 800; Action("E"); await Frames();
        Check(_player.hp == 900 && pack.GetCount(SupplyKind.Drug) == 4 && focus.Text == "", "GUI 持有焦点时 E 仍可靠使用药品且输入仅处理一次");
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.E, Pressed = true, Echo = true });
        Input.FlushBufferedEvents(); await Frames();
        Check(_player.hp == 900 && pack.GetCount(SupplyKind.Drug) == 4, "按住快捷键的重复事件不连续消耗物品");
        state.ChangeState(PlayerState.Bleed); state.ChangeState(PlayerState.Slow);
        Action("Q"); await Frames();
        Check(!state.IsBleeding && state.IsPoisoned && pack.GetCount(SupplyKind.Bandage) == 4, "Q 真实输入只清除流血");
        Action("Z"); await Frames();
        Check(!state.IsPoisoned && pack.GetCount(SupplyKind.Antidote) == 4, "Z 真实输入清除中毒");
        Action("E"); Action("E"); await Frames();
        Check(_player.hp == 1000 && pack.GetCount(SupplyKind.Drug) == 3, "满生命的无效治疗不浪费库存");
        _player.hp = 0;
        Check(!clear.TryUse(SupplyKind.Drug, out _) && pack.GetCount(SupplyKind.Drug) == 3, "生命归零不能被道具复活");
        _player.hp = 1000; focus.QueueFree();
        var bag = _game.GetNode<Packsys>("HUD/packsys"); bag.SetOpen(true); bag.SelectItem(1004);
        var use = bag.FindChild("UseItem", true, false) as Button;
        Check(use.Disabled, "满生命时背包药品显示禁用原因");
        _player.hp = 850; await Frames();
        Check(!use.Disabled, "背包按钮跟随当前状态刷新，不保留过期禁用状态");
        bag.UseSelected();
        Check(_player.hp == 950 && pack.GetCount(SupplyKind.Drug) == 2 && Stop.IsPaused, "暂停背包中可点击使用药品且只扣一次");
        state.ChangeState(PlayerState.Bleed); state.ChangeState(PlayerState.Slow);
        Action("Q"); Action("Z"); Action("E"); await Frames();
        Check(!state.IsBleeding && !state.IsPoisoned && _player.hp == 1000 &&
            pack.GetCount(SupplyKind.Bandage) == 3 && pack.GetCount(SupplyKind.Antidote) == 3 && pack.GetCount(SupplyKind.Drug) == 1,
            "背包冻结玩家时 Q/Z/E 仍由背包处理使用");
        bag.SetOpen(false);
        var settings = _game.GetNode<EscStop>("HUD/escstop"); settings.SetOpen(true);
        _player.hp = 800; Action("E"); await Frames();
        Check(_player.hp == 800 && pack.GetCount(SupplyKind.Drug) == 1, "其他暂停窗口不触发消耗品快捷键");
        settings.SetOpen(false); Action("E"); await Frames();
        Check(_player.hp == 900 && pack.GetCount(SupplyKind.Drug) == 0 && !Stop.IsPaused, "关闭暂停窗口后立即恢复物品快捷键");
    }
    private async Task Projectiles()
    {
        _game.GetNode<StaticBody2D>("background/edge").CollisionLayer = 0;
        _player.SetPhysicsProcess(false); _player.GlobalPosition = new Vector2(10000, 10000);
        var zombie = GD.Load<PackedScene>("res://zombiegd/fast_move_zom.tscn").Instantiate<FastMoveZom>();
        _game.AddChild(zombie); zombie.SetPhysicsProcess(false); zombie.MoveSpeed = 0; zombie.AttackDamage = 0;
        zombie.GlobalPosition = _player.GlobalPosition + new Vector2(40, 0); await Frames();
        int health = zombie.Health;
        Check(_player.FireTowards(Vector2.Right), "玩家可以向贴近的瞬移僵尸射击"); await Frames(3);
        Check(zombie.Health == health - 5, "近身发射子弹造成实际伤害且每发只命中一次");
        zombie.GlobalPosition = _player.GlobalPosition + new Vector2(20, 0); await Frames(); health = zombie.Health;
        Projectile(_player.GlobalPosition, Vector2.Right); await Frames(3);
        Check(zombie.Health == health - 5, "子弹出生于敌人碰撞体内也能命中");
        zombie.GlobalPosition = _player.GlobalPosition + new Vector2(160, 0); await Frames(); health = zombie.Health;
        var fast = Projectile(_player.GlobalPosition, Vector2.Right, 12000); fast.SetPhysicsProcess(false);
        fast._PhysicsProcess(0.02); await Frames();
        Check(zombie.Health == health - 5, "一帧越过目标的高速子弹通过形状扫掠命中");
        var wall = new StaticBody2D { Position = _player.GlobalPosition + new Vector2(70, 0) };
        wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(4, 120) } });
        _game.AddChild(wall); await Frames(); health = zombie.Health;
        fast = Projectile(_player.GlobalPosition, Vector2.Right, 12000); fast.SetPhysicsProcess(false);
        fast._PhysicsProcess(0.02); await Frames();
        Check(zombie.Health == health && !GodotObject.IsInstanceValid(fast), "高速子弹被目标前方的薄墙阻挡");
        wall.QueueFree(); zombie.QueueFree(); await Frames();
    }
    private async void Run()
    {
        try
        {
            _game = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate<Node2D>();
            GetTree().Root.AddChild(_game); GetTree().CurrentScene = _game;
            _player = _game.GetNode<Player>("player"); await Frames();
            await Items(); await Projectiles();
            _game.QueueFree(); await Frames(4);
            GC.Collect(); GC.WaitForPendingFinalizers(); await Frames();
            GD.Print($"PLAYER RESULT: {_checks} passed, 0 failed"); GetTree().Quit(0);
        }
        catch (Exception ex)
        { GD.PushError($"PLAYER REGRESSION FAILED after {_checks} passes: {ex}"); GetTree().Quit(1); }
    }
}
