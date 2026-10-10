using Godot;
using System;
using System.Threading.Tasks;

/// <summary>验证主角动作只是视觉反馈，以及状态伤害不会重复播放完整受击音效。</summary>
public partial class PlayerFeedbackRegression : Node
{
    private int _checks;
    private Node2D _game;
    private Player _player;
    private Run _run;
    public override void _Ready() => Callable.From(RunTests).CallDeferred();
    private void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException(description);
        _checks++; GD.Print($"PASS {_checks}: {description}");
    }
    private async Task Frames(int count = 2)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    private void UserMaterials()
    {
        foreach (string animation in new[] { "侧跑", "正跑", "背跑", "站立" })
        {
            bool fromUser = true;
            for (int i = 0; i < _run.SpriteFrames.GetFrameCount(animation); i++)
                fromUser &= _run.SpriteFrames.GetFrameTexture(animation, i).ResourcePath.StartsWith("res://assets/user/host2/");
            Check(fromUser, $"{animation} 的所有实际运行帧均来自上传的 host2 素材");
        }
        foreach (var pair in new (string Animation, string File)[] {
            ("攻击_右", "host_gun_ce.png"), ("攻击_左", "host_gun_ce2.png"),
            ("攻击_背", "host_gun_back.png"), ("攻击_正", "host_gun_zheng.png"), ("受击", "host_injured.png") })
            Check(_run.SpriteFrames.GetFrameTexture(pair.Animation, 0).ResourcePath == $"res://assets/user/host1/{pair.File}",
                $"{pair.Animation} 使用上传素材中的正确方向原画");
    }

    private void Shoot(Vector2 direction, string animation)
    {
        _player._PhysicsProcess(0.3);
        var shape = _player.GetNode<CollisionShape2D>("CollisionShape2D");
        Transform2D transform = shape.GlobalTransform;
        Vector2 location = _player.GlobalPosition;
        int bullets = _player.GetNode<Pack>("pack").GetCount(SupplyKind.Bullet);
        Check(_player.FireTowards(direction), $"朝 {direction} 成功射击");
        Check(_run.IsAttacking && _run.Animation == animation && _run.GetNode<Polygon2D>("MuzzleFlash").Visible,
            $"成功发射切到 {animation} 原画并显示枪口火光");
        _run._Process(0.04);
        Check(shape.GlobalTransform == transform && _player.GlobalPosition == location,
            "后坐视觉反馈不移动人物碰撞体和世界位置");
        Check(!_player.FireTowards(direction) && _player.GetNode<Pack>("pack").GetCount(SupplyKind.Bullet) == bullets - 1,
            "射击动画不绕过开火冷却和弹药消耗");
        _run._Process(0.3);
        Check(!_run.IsAttacking && _run.Animation == "站立" && !_run.GetNode<Polygon2D>("MuzzleFlash").Visible && _run.Position == Vector2.Zero,
            "射击动作结束恢复站立位置，枪口火光消失");
    }

    private void StatusAudio()
    {
        var state = _player.GetNode<State>("state");
        var bleed = _player.GetNode<AudioStreamPlayer>("BleedStatusSfx");
        var poison = _player.GetNode<AudioStreamPlayer>("PoisonStatusSfx");
        var hurt = _player.GetNode<AudioStreamPlayer>("DirectHitSfx");
        hurt.Stop();
        Check(bleed.Bus == "Sfx" && poison.Bus == "Sfx" && bleed.VolumeDb <= -22 && poison.VolumeDb <= -24,
            "流血和中毒分别以低声量经过音效总线");
        Check(bleed.Stream != poison.Stream && bleed.Stream.ResourcePath.EndsWith("status_bleed.wav") &&
            poison.Stream.ResourcePath.EndsWith("status_poison.wav") &&
            ((AudioStreamWav)bleed.Stream).LoopMode == AudioStreamWav.LoopModeEnum.Disabled &&
            ((AudioStreamWav)poison.Stream).LoopMode == AudioStreamWav.LoopModeEnum.Disabled &&
            bleed.MaxPolyphony == 1 && poison.MaxPolyphony == 1,
            "两个状态使用不同的短音，均不循环且不能叠加多个声部");
        float before = _player.hp;
        state.ChangeState(PlayerState.Bleed); state.ChangeState(PlayerState.Slow);
        state._Process(1);
        Check(_player.hp == before - 10 && bleed.Playing && poison.Playing && !hurt.Playing && !_run.IsHurting,
            "同时流血/中毒各扣血5，只播放自己的短音，不重复直接受击喊声或姿势");
        state.ClearState(PlayerState.Bleed);
        Check(!bleed.Playing && poison.Playing && !state.IsBleeding && state.IsPoisoned,
            "治疗流血立刻停止流血声音，保留中毒反馈");
        state.ClearState(PlayerState.Slow);
        Check(!poison.Playing && !state.IsPoisoned, "治疗中毒立刻停止中毒声音");
        state.ChangeState(PlayerState.Bleed); state.ChangeState(PlayerState.Slow); state._Process(1);
        state.SetDebuffsPaused(true);
        before = _player.hp; state._Process(2);
        Check(!bleed.Playing && !poison.Playing && _player.hp == before, "显式暂停状态不会残留声音或继续扣血");
        state.SetDebuffsPaused(false); state._Process(1); state.ResetToNormal();
        Check(!bleed.Playing && !poison.Playing && state.Current == PlayerState.Normal, "重置状态停止两种声音");
        _player.hp = 5; _player.TakeStatusDamage(10, PlayerState.Bleed);
        Check(_player.hp == 0 && !bleed.Playing && !poison.Playing, "状态致死不会重新启动已停止的状态声音");
    }

    private async void RunTests()
    {
        try
        {
            _game = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate<Node2D>();
            GetTree().Root.AddChild(_game); GetTree().CurrentScene = _game;
            _player = _game.GetNode<Player>("player");
            _player.SetPhysicsProcess(false); _player.GetNode("die").SetProcess(false);
            _player.GetNode<State>("state").SetProcess(false);
            _run = _player.GetNode<Run>("run"); _run.SetProcess(false);
            _player.GlobalPosition = new Vector2(10000, 10000); await Frames();
            UserMaterials();
            Check(_player.EffectiveMoveSpeed == 500, "保留第一版移速500");
            Check(!_player.FireTowards(Vector2.Zero) && !_run.IsAttacking, "无效射击不播放攻击动画");
            Shoot(Vector2.Right, "攻击_右"); Shoot(Vector2.Left, "攻击_左");
            Shoot(Vector2.Up, "攻击_背"); Shoot(Vector2.Down, "攻击_正");
            var shape = _player.GetNode<CollisionShape2D>("CollisionShape2D");
            var transform = shape.GlobalTransform; float hp = _player.hp;
            _player.TakeDamage(7);
            Check(_player.hp == hp - 7 && _run.IsHurting && _run.Animation == "受击" && _run.Modulate.G < 1,
                "直接受伤立刻扣血并播放受伤原画和红闪");
            Check(shape.GlobalTransform == transform, "受击摆动不改变碰撞体");
            var bag = _game.GetNode<Packsys>("HUD/packsys"); bag.SetOpen(true);
            Vector2 pose = _run.Position; _run._Process(1); _player.TakeDamage(7);
            Check(_run.IsHurting && _run.Position == pose && _player.hp == hp - 7, "打开背包冻结动作和伤害");
            bag.SetOpen(false); _run._Process(0.3);
            Check(!_run.IsHurting && _run.Animation == "站立" && _run.Modulate == Colors.White && _run.Rotation == 0,
                "恢复后受击动作正常结束且无残留红色或倾斜");
            Input.ActionPress("move_right"); _run._Process(0.01);
            Check(_run.Animation == "侧跑", "受击结束后继续方向移动可切回跑步");
            Input.ActionRelease("move_right"); _run._Process(0.01);
            StatusAudio();
            Check(!_player.FireTowards(Vector2.Right), "生命归零不能启动攻击动作");
            _game.QueueFree(); await Frames(4);
            // headless 模式数百次逻辑更新可在一次音频混音之前结束；先让播放线程处理 Stop。
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            GC.Collect(); GC.WaitForPendingFinalizers(); await Frames();
            Check(!Stop.IsPaused, "释放游戏不遗留暂停所有者");
            GD.Print($"PLAYER FEEDBACK RESULT: {_checks} passed, 0 failed"); GetTree().Quit(0);
        }
        catch (Exception ex)
        { GD.PushError($"PLAYER FEEDBACK REGRESSION FAILED after {_checks} passes: {ex}"); GetTree().Quit(1); }
    }
}
