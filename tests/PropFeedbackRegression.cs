using Godot;
using System;
using System.Threading.Tasks;

/// <summary>靠近开棺、守卫死亡后的摸棺提示、开放通行与真实机关踩踏反馈。</summary>
public partial class PropFeedbackRegression : Node
{
    private Node2D _game;
    private Player _player;
    private int _checks;
    private readonly Vector2 _arena = new(10000, 10000);
    public override void _Ready() => Callable.From(RunTests).CallDeferred();
    private void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        GD.Print($"PASS PROP FEEDBACK {++_checks}: {description}");
    }
    private async Task Frames(int count = 2)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private async Task Delay(double seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async Task CoffinFeedback()
    {
        _player.GlobalPosition = _arena + new Vector2(170, 0);
        var coffin = GD.Load<PackedScene>("res://zombiegd/Fzombie/CoMini.tscn").Instantiate<Coffin>();
        coffin.Position = _arena; _game.AddChild(coffin);
        var open = coffin.GetNode<Sprite2D>("open");
        var shape = coffin.GetNode<CollisionShape2D>("SolidBody/CollisionShape2D");
        var transform = shape.GlobalTransform;
        var scale = open.Scale;
        Zombie guardian = null;
        foreach (Node child in coffin.GetChildren()) if (child is Zombie zombie) guardian = zombie;
        guardian.SetPhysicsProcess(false);
        await Frames();
        Check(coffin.Status == Coffin.CoffinState.Closed && !coffin.CanInteract &&
            !coffin.LootGlowEnabled && coffin.LootGlowStrength == 0 &&
            open.Material is ShaderMaterial material && material.Shader.ResourcePath == "res://scenes/coffin_loot_glow.gdshader",
            "封闭棺材不发光、不开放F交互，打开精灵具有独立的摸棺材质");
        coffin.Interact(_player);
        Check(coffin.Status == Coffin.CoffinState.Closed, "F交互不会提前打开封闭棺材");
        var wall = new StaticBody2D { Position = _arena + new Vector2(45, 0) };
        wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(20, 120) } });
        _game.AddChild(wall);
        _player.GlobalPosition = _arena + new Vector2(90, 0); await Frames(3);
        Check(coffin.Status == Coffin.CoffinState.Closed && !guardian.IsActive && !guardian.Visible,
            "110像素内隔墙靠近仍保持封闭，不从另一条墓道触发守卫");
        wall.QueueFree(); await Frames(3);
        var solid = coffin.GetNode<StaticBody2D>("SolidBody");
        Check(coffin.Status == Coffin.CoffinState.Fighting && guardian.IsActive &&
            !coffin.CanInteract && coffin.LootGlowStrength == 0 && solid.CollisionLayer == 0 && shape.Disabled,
            "靠近自动开棺并释放棺材实体，战斗期间不提前显示摸棺提示");
        guardian.take_damage(guardian.Health);
        Check(coffin.Status == Coffin.CoffinState.Unlocked && coffin.CanInteract && coffin.LootGlowEnabled &&
            coffin.LootGlowStrength >= 0.65f && coffin.LootGlowStrength <= 1.2f && open.Visible &&
            ((ShaderMaterial)open.Material).GetShaderParameter("glow_color").AsColor().G > 0.9f &&
            ((ShaderMaterial)open.Material).GetShaderParameter("glow_color").AsColor().B > 0.95f,
            "真实击杀守卫立即解锁摸棺，并点亮清晰的青白色提示");
        float first = coffin.LootGlowStrength;
        await Delay(0.15);
        Check(coffin.LootGlowStrength != first && coffin.LootGlowStrength >= 0.65f &&
            coffin.LootGlowStrength <= 1.2f && open.Scale == scale && shape.GlobalTransform == transform,
            "青白色脉动只改材质，不扩大棺材图像和碰撞体");
        var obstacle = _player.MoveAndCollide(new Vector2(-180, 0));
        Check(obstacle == null && _player.GlobalPosition.DistanceTo(_arena + new Vector2(-90, 0)) < 0.1f,
            "守卫死亡后玩家可真实穿过已打开的棺材");
        var pause = new Stop(); AddChild(pause); pause.SetPaused(true);
        float paused = coffin.LootGlowStrength; await Delay(0.15);
        Check(coffin.LootGlowStrength == paused, "打开模态界面冻结摸棺光效脉动");
        pause.SetPaused(false); await Delay(0.1);
        Check(coffin.LootGlowStrength != paused, "恢复游戏后继续光效脉动");
        coffin.Interact(_player);
        Check(coffin.Status == Coffin.CoffinState.Looted && !coffin.LootGlowEnabled && coffin.LootGlowStrength == 0 &&
            ((ShaderMaterial)open.Material).GetShaderParameter("glow_strength").AsSingle() == 0,
            "摸棺取得掉落后立即熄灭提示，不能再次摸棺");
        coffin.Interact(_player);
        Check(coffin.Status == Coffin.CoffinState.Looted && !coffin.CanInteract,
            "重复操作已摸棺棺材不会重新点亮或开放交互");

        var empty = GD.Load<PackedScene>("res://zombiegd/coffin.tscn").Instantiate<Coffin>();
        empty.Position = _arena + new Vector2(400, 0); _game.AddChild(empty);
        _player.GlobalPosition = empty.Position + new Vector2(90, 0); await Frames();
        Check(empty.Status == Coffin.CoffinState.Unlocked && empty.LootGlowEnabled && empty.LootGlowStrength > 0 &&
            empty.GetNode<Sprite2D>("open").Material != open.Material && coffin.LootGlowStrength == 0,
            "靠近无守卫棺材自动打开即可摸棺，且光效材质互不污染");
        empty.Interact(_player);
        Check(empty.Status == Coffin.CoffinState.Looted && empty.LootGlowStrength == 0,
            "无守卫棺材领取掉落后也立即熄灭光效");
        coffin.QueueFree(); empty.QueueFree(); pause.QueueFree(); await Frames(3);
    }

    private async Task TrapFeedback(int mechanismId)
    {
        Vector2 point = _arena + new Vector2(mechanismId * 1000, 1200);
        var trap = GD.Load<PackedScene>("res://scenes/trap.tscn").Instantiate<Trap>();
        trap.Position = point; trap.MechanismId = mechanismId;
        trap.ArrowOrigin = new Vector2(350, 0); trap.ArrowDirection = Vector2.Right;
        _game.AddChild(trap);
        var trigger = trap.GetNode<AudioStreamPlayer2D>("TriggerSfx");
        string name = mechanismId == 1 ? "地刺" : "暗箭";
        Check(trigger.Stream.ResourcePath == "res://music/step_on_mechanism.wav" && trigger.Bus == "Sfx" &&
            trigger.VolumeDb == -14 && trigger.MaxPolyphony == 1 && !trigger.Playing,
            $"{name}机关具有独立、低声量的踩踏音效，尚未踩到时不播放");
        _player.GlobalPosition = point + new Vector2(-150, 0); await Frames();
        _player.MoveAndCollide(new Vector2(150, 0)); await Frames(3);
        Check(trap.GetOverlappingBodies().Contains(_player) && trap.TriggerCount == 1 && trigger.Playing &&
            trigger.GetStreamPlayback() != null,
            $"玩家真实移动踩到{name}机关，BodyEntered 触发一次实际音频播放");
        if (mechanismId == 1)
            Check(trap.GetNode<Sprite2D>("Spikes").Visible && trap.ShotsFired == 0,
                "踩踏音效保留地刺展开动作，不误发暗箭");
        else
        {
            var burst = trap.GetNode<AudioStreamPlayer2D>("ArrowBurstSfx");
            Check(burst.Stream.ResourcePath == "res://music/arrows_many.wav" && burst.Bus == "Sfx" &&
                burst.VolumeDb == -19 && burst.MaxPolyphony == 1 && burst.Playing && trap.ShotsFired > 0,
                "暗箭踩踏与低声量十连发音效分开播放，射箭逻辑保留");
        }
        trigger.Stop(); _player.MoveAndCollide(new Vector2(-150, 0)); await Frames();
        _player.MoveAndCollide(new Vector2(150, 0)); await Frames();
        Check(trap.TriggerCount == 1 && !trigger.Playing,
            $"冷却期间重复踩踏{name}机关不会叠加触发音效");
        var pause = new Stop(); AddChild(pause); pause.SetPaused(true);
        int shots = trap.ShotsFired; trap._PhysicsProcess(6);
        Check(!trap.Activate() && trap.TriggerCount == 1 && !trigger.Playing && trap.ShotsFired == shots,
            $"暂停期间{name}机关不触发、不推进冷却或继续射击");
        pause.SetPaused(false);
        _player.GlobalPosition = point + new Vector2(-150, 0); await Frames();
        trap._PhysicsProcess(6); _player.MoveAndCollide(new Vector2(150, 0)); await Frames(3);
        Check(trap.TriggerCount == 2 && trigger.Playing,
            $"{name}机关冷却完成后可再次通过实际踩踏播放声音");
        trap.QueueFree(); pause.QueueFree(); await Frames(3);
        Check(!GodotObject.IsInstanceValid(trigger), $"离开{name}机关释放音效播放器");
    }
    private async void RunTests()
    {
        try
        {
            _game = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate<Node2D>();
            GetTree().Root.AddChild(_game); GetTree().CurrentScene = _game;
            _player = _game.GetNode<Player>("player"); _player.SetPhysicsProcess(false);
            _player.GetNode("die").SetProcess(false);
            _game.GetNode<StaticBody2D>("background/edge").CollisionLayer = 0;
            _player.GlobalPosition = _arena + new Vector2(90, 0); await Frames();
            await CoffinFeedback(); await TrapFeedback(1); await TrapFeedback(2);
            _game.QueueFree(); await Frames(4); await Delay(0.2);
            GC.Collect(); GC.WaitForPendingFinalizers(); await Frames();
            Check(!Stop.IsPaused, "反馈场景退出不遗留暂停锁");
            GD.Print($"PROP FEEDBACK RESULT: {_checks} passed, 0 failed"); GetTree().Quit();
        }
        catch (Exception exception)
        { GD.PushError($"PROP FEEDBACK FAILED after {_checks}: {exception}"); GetTree().Quit(1); }
    }
}
