using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>真实六种场景的图集播放时长、碰撞隔离及动态移速回归。</summary>
public partial class ZombieMotionRegression : Node
{
    private Node2D _game;
    private Player _player;
    private readonly List<Zombie> _zombies = new();
    private readonly Vector2 _arena = new(16000, 16000);
    private int _checks;
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        GD.Print($"PASS {++_checks}: {message}");
    }
    private async Task Frames(int count = 2)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    private static void StopFixtureAudio(Node node)
    {
        foreach (Node child in node.GetChildren()) StopFixtureAudio(child);
        if (node is AudioStreamPlayer audio)
        {
            audio.Stop();
            audio.Stream = null;
        }
        else if (node is AudioStreamPlayer2D spatialAudio)
        {
            spatialAudio.Stop();
            spatialAudio.Stream = null;
        }
    }

    private void Speeds(float expected, string state)
    {
        _player.GlobalPosition = _arena + new Vector2(1800, 0);
        foreach (Zombie zombie in _zombies)
        {
            zombie.GlobalPosition = zombie.ReturnPosition + new Vector2(0, 200);
            zombie._PhysicsProcess(1.0 / 60.0);
            Check(Mathf.IsEqualApprox(zombie.EffectiveMoveSpeed, expected) &&
                Mathf.IsEqualApprox(zombie.Velocity.Length(), expected),
                $"种类{zombie.MonsterId} {state}: 返程实时匹配主角{expected}px/s");
        }
    }

    private void ChaseSpeeds()
    {
        foreach (Zombie zombie in _zombies) zombie.GlobalPosition = zombie.ReturnPosition + new Vector2(0, 2000);
        foreach (Zombie zombie in _zombies)
        {
            zombie.GlobalPosition = zombie.ReturnPosition;
            _player.GlobalPosition = zombie.ReturnPosition + new Vector2(120, 0);
            if (zombie is ArrowZom)
            {
                zombie.GlobalPosition -= new Vector2(100, 0);
                _player.GlobalPosition = zombie.ReturnPosition + new Vector2(450, 0);
            }
            zombie._PhysicsProcess(1.0 / 60.0);
            Check(Mathf.IsEqualApprox(zombie.Velocity.Length(), _player.EffectiveMoveSpeed),
                $"种类{zombie.MonsterId}: 追击实时匹配主角普通移速");
            zombie.GlobalPosition = zombie.ReturnPosition + new Vector2(0, 2000);
        }
    }

    private void ReturnSettles(float expectedSpeed)
    {
        _player.GlobalPosition = _arena + new Vector2(1800, 0);
        foreach (Zombie zombie in _zombies)
        {
            zombie.GlobalPosition = zombie.ReturnPosition + new Vector2(0, 6);
            for (int frame = 0; frame < 20; frame++) zombie._PhysicsProcess(1.0 / 60.0);
            Check(Mathf.IsEqualApprox(zombie.EffectiveMoveSpeed, expectedSpeed) &&
                zombie.GlobalPosition.DistanceTo(zombie.ReturnPosition) < 5f && zombie.Velocity.IsZeroApprox(),
                $"种类{zombie.MonsterId}: {expectedSpeed}px/s 高速归位后稳定停止，不往返抖动");
        }
    }

    private void Animation(Zombie zombie)
    {
        var motion = zombie.GetNode<ZombieMotionArt>("MotionArt");
        var normal = zombie.GetNode<Sprite2D>("normal");
        var attack = zombie.GetNode<Sprite2D>("attack");
        var shape = zombie.GetNode<CollisionShape2D>("CollisionShape2D");
        Transform2D collision = shape.Transform;
        Texture2D originalTexture = normal.Texture;
        Vector2 originalScale = normal.Scale;
        Check(motion.SpriteFrames.GetFrameCount("walk") == 4 && motion.SpriteFrames.GetFrameCount("attack") == 4,
            $"种类{zombie.MonsterId}: 两种动作均有四张真实图集帧");
        Check(motion.SpriteFrames.GetFrameTexture("walk", 0) is AtlasTexture first &&
            motion.SpriteFrames.GetFrameTexture("walk", 1) is AtlasTexture second && first.Region != second.Region,
            $"种类{zombie.MonsterId}: 行走使用不同画格");
        var atlas = (AtlasTexture)motion.SpriteFrames.GetFrameTexture("walk", 0);
        using (Image pixels = atlas.Atlas.GetImage())
        {
            pixels.Convert(Image.Format.Rgba8);
            byte[] rgba = pixels.GetData();
            int imageWidth = pixels.GetWidth();
            foreach (string animation in new[] { "walk", "attack" })
                for (int frame = 0; frame < 4; frame++)
                {
                    var texture = (AtlasTexture)motion.SpriteFrames.GetFrameTexture(animation, frame);
                    int left = Mathf.FloorToInt(texture.Region.Position.X + 0.5f);
                    int top = Mathf.FloorToInt(texture.Region.Position.Y + 0.5f);
                    int right = Mathf.FloorToInt(texture.Region.End.X + 0.5f);
                    int bottom = Mathf.FloorToInt(texture.Region.End.Y + 0.5f);
                    bool hasArtwork = false, clearBorder = true;
                    for (int y = top; y < bottom; y++)
                    {
                        int index = (y * imageWidth + left) * 4 + 3;
                        for (int x = left; x < right; x++, index += 4)
                        {
                            if (rgba[index] < 26) continue; // 不把1/255的不可见杂点算作图形。
                            hasArtwork = true;
                            if (x < left + 8 || x >= right - 8 || y < top + 8 || y >= bottom - 8)
                                clearBorder = false;
                        }
                    }
                    Check(hasArtwork && clearBorder,
                        $"种类{zombie.MonsterId}: {animation}第{frame}帧可见图形保留透明边距，不跨入相邻画格");
                }
        }
        zombie.Velocity = Vector2.Right * _player.EffectiveMoveSpeed;
        zombie._Process(0);
        double frameTime = 500.0 / ((zombie.MonsterId == 5 ? 8 : 10) * _player.EffectiveMoveSpeed);
        for (int frame = 1; frame <= 4; frame++)
        {
            zombie._Process(frameTime + 0.00001);
            Check(motion.Frame == frame % 4, $"种类{zombie.MonsterId}: 行走依次播放第{frame % 4}帧并循环");
        }
        Check(zombie.CurrentAnimation == "move" && motion.Visible && !normal.Visible && !attack.Visible,
            $"种类{zombie.MonsterId}: 行走实际切换图像");
        _player.GlobalPosition = zombie.GlobalPosition - new Vector2(120, 0);
        zombie._Process(0);
        Check(motion.FlipH, $"种类{zombie.MonsterId}: 默认朝右的图集正确面向左侧主角");
        var pause = new Stop(); AddChild(pause); pause.SetPaused(true);
        int pausedFrame = motion.Frame;
        Vector2 pausedPosition = motion.Position;
        _player.GlobalPosition = zombie.GlobalPosition + new Vector2(120, 0);
        zombie._Process(1);
        Check(motion.Frame == pausedFrame && motion.Position == pausedPosition,
            $"种类{zombie.MonsterId}: 暂停同时冻结画格和朝向");
        pause.SetPaused(false); pause.QueueFree();
        zombie.Velocity = Vector2.Zero;
        zombie._Process(0);
        Check(zombie.CurrentAnimation == "idle" && !motion.Visible && normal.Visible &&
            normal.Texture == originalTexture && normal.Scale.IsEqualApprox(originalScale),
            $"种类{zombie.MonsterId}: 待机恢复原始立绘");

        zombie.GlobalPosition = zombie.ReturnPosition;
        _player.GlobalPosition = zombie.GlobalPosition + new Vector2(50, 0);
        zombie.AttackDamage = 0;
        zombie._PhysicsProcess(1.0 / 60.0);
        double duration = zombie is ArrowZom ? 0.38 : 0.30;
        Check(zombie.CurrentAnimation == "attack" && motion.Visible && motion.Frame == 0 &&
            Math.Abs(motion.AttackDuration - duration) < 0.00001,
            $"种类{zombie.MonsterId}: 攻击开始于第零帧，时长{duration}秒");
        var attackPause = new Stop(); AddChild(attackPause); attackPause.SetPaused(true);
        zombie._Process(duration * 2);
        Check(zombie.CurrentAnimation == "attack" && motion.Frame == 0,
            $"种类{zombie.MonsterId}: 暂停不消耗攻击画格或时长");
        attackPause.SetPaused(false); attackPause.QueueFree();
        for (int frame = 1; frame < 4; frame++)
        {
            zombie._Process(duration / 4 + 0.00001);
            Check(zombie.CurrentAnimation == "attack" && motion.Frame == frame && shape.Transform.IsEqualApprox(collision),
                $"种类{zombie.MonsterId}: 攻击第{frame}帧保持真实碰撞变换");
        }
        zombie._Process(duration / 4 - 0.0001);
        Check(zombie.CurrentAnimation == "attack" && motion.Frame == 3,
            $"种类{zombie.MonsterId}: 最后一帧播放至完整攻击时长");
        zombie._Process(0.001);
        Check(zombie.CurrentAnimation == "idle" && !motion.Visible && normal.Visible && !attack.Visible,
            $"种类{zombie.MonsterId}: 完整攻击结束恢复原画");
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
            var state = _player.GetNode<State>("state"); state.SetProcess(false); state.ResetToNormal();
            foreach (string species in new[] { "mini", "fast_move", "arrow", "sharp", "heavy", "poison" })
            {
                var zombie = GD.Load<PackedScene>($"res://zombiegd/{species}_zom.tscn").Instantiate<Zombie>();
                zombie.Position = _arena + new Vector2(_zombies.Count * 140, 0);
                _game.AddChild(zombie); zombie.SetPhysicsProcess(false); zombie.SetProcess(false);
                if (zombie is FastMoveZom fast) fast.TeleportTriggerRange = 0;
                _zombies.Add(zombie);
            }
            await Frames();
            Speeds(500, "正常");
            state.ChangeState(PlayerState.Slow); Speeds(480, "中毒");
            state.ResetToNormal(); Speeds(500, "解毒");
            _player.ApplyBuff(3002); Speeds(600, "加速增益");
            state.ChangeState(PlayerState.Slow); Speeds(580, "增益及中毒同时存在");
            state.ResetToNormal();
            Input.ActionPress("move_right");
            _player._UnhandledInput(new InputEventAction { Action = "mouse_press2", Pressed = true });
            _player._PhysicsProcess(1.0 / 60.0);
            Input.ActionRelease("move_right");
            Check(_player.IsDashing && _player.Velocity.Length() > _player.EffectiveMoveSpeed,
                "主角确实正以更高速度冲刺");
            Speeds(600, "主角冲刺期间仍只跟随行走速度");
            _player._PhysicsProcess(1);
            ChaseSpeeds();
            // 固定正常步频，逐帧验证真实图集与完整攻击时长。
            foreach (Zombie zombie in _zombies) Animation(zombie);
            _player.ApplyBuff(3002); ReturnSettles(700);
            _player.ApplyBuff(3002, 10); ReturnSettles(1700);
            // 本测试同步触发六次攻击后马上结束；先释放音频，再留时间让混音线程
            // 清理尚在播放的背景音乐和攻击音，避免测试退出时误报原生资源泄漏。
            StopFixtureAudio(_game);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            _zombies.Clear();
            _game.QueueFree(); await Frames(8);
            GC.Collect(); GC.WaitForPendingFinalizers(); await Frames();
            for (int frame = 0; frame < 2; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print($"ZOMBIE MOTION RESULT: {_checks} passed, 0 failed"); GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            Input.ActionRelease("move_right");
            GD.PushError($"ZOMBIE MOTION FAILED after {_checks}: {exception}"); GetTree().Quit(1);
        }
    }
}
