using Godot;
using System;
using System.Threading.Tasks;

/// <summary>实际移动速度及机关箭场景的美术/碰撞/射击方向验证。</summary>
public partial class PresentationRegression : Node
{
    private Node2D _game;
    private int _checks;
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException(description);
        GD.Print($"PASS {++_checks}: {description}");
    }
    private async Task Frames(int count = 2)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private async void Run()
    {
        try
        {
            _game = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate<Node2D>();
            GetTree().Root.AddChild(_game); GetTree().CurrentScene = _game;
            _game.GetNode<StaticBody2D>("background/edge").CollisionLayer = 0;
            var player = _game.GetNode<Player>("player");
            player.GlobalPosition = new Vector2(10000, 10000); await Frames();
            Vector2 start = player.GlobalPosition;
            Input.ActionPress("move_right"); await Frames(60); Input.ActionRelease("move_right");
            float distance = player.GlobalPosition.DistanceTo(start);
            Check(distance >= 480 && distance <= 520, $"原版移速实际一秒移动约500像素（{distance:0.0}）");
            var state = player.GetNode<State>("state"); state.ChangeState(PlayerState.Slow);
            Check(player.EffectiveMoveSpeed == 480, "恢复移速后中毒仍减20像素/秒");
            state.ResetToNormal(); player.ApplyBuff(3002);
            Check(player.EffectiveMoveSpeed == 600, "恢复移速后增益仍生效（500→600）");
            player.SetPhysicsProcess(false);

            var trap = _game.GetNode<Trap>("ArrowTrap");
            trap.GlobalPosition = player.GlobalPosition + new Vector2(300, 0);
            trap.ArrowOrigin = Vector2.Zero; trap.ArrowDirection = Vector2.Up;
            Check(trap.Activate(), "真实暗箭机关启动"); await Frames(2);
            var arrow = GetTree().GetFirstNodeInGroup("bullet") as Bullet;
            Check(arrow != null && arrow.SceneFilePath == "res://scenes/trap_arrow.tscn", "暗箭发射独立箭矢场景");
            var sprite = arrow.GetNode<Sprite2D>("ArrowSprite");
            Check(sprite.Texture.ResourcePath == "res://bin/projectiles/trap_arrow.png" && sprite.Texture.GetWidth() > 0,
                "暗箭使用新生成的箭矢图像");
            var image = sprite.Texture.GetImage();
            Check(image.DetectAlpha() != Image.AlphaMode.None, "箭矢图像保留透明背景");
            Check(Mathf.IsEqualApprox(arrow.Rotation, -Mathf.Pi / 2), "右朝箭纹理按发射方向自动旋转");
            float health = player.hp; arrow.ApplyDamage(player);
            Check(arrow.HitsEveryone && player.hp == health - arrow.Damage, "换用箭矢美术后机关仍伤害玩家");
            var zombie = GD.Load<PackedScene>("res://zombiegd/heavy_zom.tscn").Instantiate<Zombie>();
            _game.AddChild(zombie); zombie.GlobalPosition = player.GlobalPosition + new Vector2(600, 0); zombie.SetPhysicsProcess(false);
            int before = zombie.Health; arrow.ApplyDamage(zombie);
            Check(zombie.Health == Mathf.Max(0, before - arrow.Damage), "换用箭矢美术后机关仍伤害僵尸");
            await Frames(80);
            Check(trap.ShotsFired == 10, "暗箭仍按机关配置完成十连发");
            _game.QueueFree(); await Frames(4);
            GC.Collect(); GC.WaitForPendingFinalizers(); await Frames();
            GD.Print($"PRESENTATION RESULT: {_checks} passed, 0 failed"); GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            Input.ActionRelease("move_right");
            GD.PushError($"PRESENTATION FAILED after {_checks}: {ex}"); GetTree().Quit(1);
        }
    }
}
