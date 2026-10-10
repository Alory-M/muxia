using Godot;
using System;
using System.Threading.Tasks;

/// <summary>在真实游戏场景中验证策划流程和边界，不替代玩家操作体验测试。</summary>
public partial class GameplayRegression : Node
{
    private int _checks;
    private Node2D _game;
    private Player _player;
    public override void _Ready() => Callable.From(Run).CallDeferred();
    // Godot.NET 的临时资源包装器由 GC 释放；外部快速截图/冒烟脚本可在退出前调用。
    public void CollectManagedResources() { GC.Collect(); GC.WaitForPendingFinalizers(); }
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++; GD.Print($"PASS {_checks}: {message}");
    }
    private async Task Frames(int count = 2)
    { for (int i = 0; i < count; i++)
      { await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); } }
    private static void Action(string name)
    {
        Input.ParseInputEvent(new InputEventAction { Action = name, Pressed = true });
        Input.ParseInputEvent(new InputEventAction { Action = name, Pressed = false });
        Input.FlushBufferedEvents();
    }
    private async Task StartGame()
    {
        _game = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate<Node2D>();
        GetTree().Root.AddChild(_game); GetTree().CurrentScene = _game;
        _player = _game.GetNode<Player>("player"); await Frames();
    }
    private async Task ClearGame()
    {
        _game.QueueFree(); await Frames();
        await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
        GetTree().CurrentScene = null;
        Check(!Stop.IsPaused, "退出场景释放全部暂停锁");
    }
    private async void Run()
    {
        try
        {
            await StartGame();
            var pack = _player.GetNode<Pack>("pack");
            var state = _player.GetNode<State>("state");
            var clear = _player.GetNode<Clear>("clear");
            var gold = _player.GetNode<Gold>("gold");
            var soul = _player.GetNode<soulpiece>("soulpiece");
            var store = _game.GetNode<Store>("HUD/store/store");
            var bag = _game.GetNode<Packsys>("HUD/packsys");
            var dialogue = _game.GetNode<InteractionController>("Interactions");
            float initialMusic = AudioSettings.MusicVolume, initialSfx = AudioSettings.SfxVolume;
            AudioSettings.SetVolume(true, 0); AudioSettings.SetVolume(false, 0.4f);
            Check(AudioServer.IsBusMute(AudioServer.GetBusIndex("Music")) && !AudioServer.IsBusMute(AudioServer.GetBusIndex("Sfx")) &&
                Mathf.IsEqualApprox(AudioServer.GetBusVolumeLinear(AudioServer.GetBusIndex("Sfx")), 0.4f), "音乐、音效分开调节，零音量可静音");
            Check(FileAccess.FileExists("user://audio_settings.json"), "音量设置已持久化");
            AudioSettings.SetVolume(true, initialMusic); AudioSettings.SetVolume(false, initialSfx);
            var species = new System.Collections.Generic.HashSet<int>();
            foreach (Node node in GetTree().GetNodesInGroup("zombie")) species.Add(((Zombie)node).MonsterId);
            Check(species.Count == 6, "当前地图包含六种策划僵尸");
            Check(_player.MaxHp == 1000 && _player.EffectiveAttack == 5 && _player.EffectiveMoveSpeed == 500, "生命与攻击读取 host 表，移速恢复第一版场景值 500");
            Check(InputMap.ActionHasEvent("mouse_press", new InputEventKey { PhysicalKeycode = Key.J }) &&
                InputMap.ActionHasEvent("mouse_press2", new InputEventKey { PhysicalKeycode = Key.Shift }), "J 和 Shift 替代操作已注册");
            pack.SetCount(SupplyKind.Bullet, 2); int reserve = pack.GetReserveBullet();
            pack.AddBullet(12);
            Check(pack.GetCount(SupplyKind.Bullet) == 2 && pack.GetReserveBullet() == reserve + 12, "获得弹药只增加备用子弹");
            pack.ReloadBullets();
            Check(pack.GetCount(SupplyKind.Bullet) == 6 && pack.GetReserveBullet() == reserve + 8, "换弹从备用数补到六发");
            reserve = pack.GetReserveBullet(); pack.ReloadBullets();
            Check(pack.GetReserveBullet() == reserve, "满弹匣换弹不浪费子弹");
            Check(!_player.FireTowards(Vector2.Zero) && pack.GetCount(SupplyKind.Bullet) == 6, "无效射击不消耗弹药");
            Check(_player.FireTowards(Vector2.Right), "有效射击产生子弹");
            Check(!_player.FireTowards(Vector2.Right) && pack.GetCount(SupplyKind.Bullet) == 5, "射击遵循攻速冷却");
            foreach (Node bullet in GetTree().GetNodesInGroup("bullet")) bullet.QueueFree();
            state.SetProcess(false);
            state.ChangeState(PlayerState.Bleed); state.ChangeState(PlayerState.Slow);
            Check(state.IsBleeding && state.IsPoisoned && _player.EffectiveMoveSpeed == 480 && _player.EffectiveAttack == 2, "中毒与流血同时存在，毒减速与降攻击正确");
            float hp = _player.hp; state._Process(1.01);
            Check(state.BleedPerSecond == 5 && state.PoisonDamagePerSecond == 5 && _player.hp == hp - 10,
                "一秒流血和中毒各扣五点，总伤害降低到十点");
            int bandages = pack.GetCount(SupplyKind.Bandage); clear.UseBandage();
            Check(!state.IsBleeding && state.IsPoisoned && pack.GetCount(SupplyKind.Bandage) == bandages - 1, "绷带只解除流血");
            clear.UseBandage();
            Check(pack.GetCount(SupplyKind.Bandage) == bandages - 1, "无流血时不浪费绷带");
            int antidotes = pack.GetCount(SupplyKind.Antidote); clear.UseAntidote();
            Check(!state.IsPoisoned && _player.EffectiveMoveSpeed == 500 && _player.EffectiveAttack == 5 && pack.GetCount(SupplyKind.Antidote) == antidotes - 1, "解毒恢复属性且扣一份道具");
            _player.hp = _player.MaxHp - 50; int drugs = pack.GetCount(SupplyKind.Drug); clear.UseDrug(); clear.UseDrug();
            Check(_player.hp == _player.MaxHp && pack.GetCount(SupplyKind.Drug) == drugs - 1, "药品回血封顶，满血不消耗");
            Action("B");
            await Frames();
            Check(bag.Visible && Stop.IsPaused && _player.ProcessMode == ProcessModeEnum.Disabled, "B 打开背包并冻结世界");
            var bagBackground = bag.GetNode<Sprite2D>("background");
            var bagRect = bagBackground.GetGlobalTransform() * bagBackground.GetRect();
            Check(bagRect.Position.X >= 0 && bagRect.End.X <= GetViewport().GetVisibleRect().Size.X &&
                bagBackground.Texture.ResourcePath == "res://assets/user/interface/bag_inter.png", "第一版背包美术面板完整显示在视口内");
            state.ChangeState(PlayerState.Bleed); hp = _player.hp; state._Process(2);
            Check(_player.hp == hp, "模态窗口期间状态不扣血");
            Action("esc");
            Check(!bag.Visible && !Stop.IsPaused && !_game.GetNode<EscStop>("HUD/escstop").Visible, "Esc 仅关闭背包，不顺便打开设置");
            state.ResetToNormal(); state.SetProcess(true);
            store.SetOpen(true); store.SetOpen(true);
            await Frames();
            var storeUi = _game.GetNode<Control>("HUD/store");
            var storeBackground = storeUi.GetNode<Sprite2D>("background");
            var storeRect = storeBackground.GetGlobalTransform() * storeBackground.GetRect();
            Check(storeRect.Position.X >= 0 && storeRect.End.X <= GetViewport().GetVisibleRect().Size.X &&
                storeBackground.Texture.ResourcePath == "res://assets/user/interface/shop_inter.png", "第一版商店美术面板完整显示在视口内");
            gold.Amount = 149; int stock = store.GetProductStock(1005); reserve = pack.GetReserveBullet();
            Check(!store.TryPurchase(1005, 1) && gold.Amount == 149 && store.GetProductStock(1005) == stock && pack.GetReserveBullet() == reserve, "余额不足不扣款、发货，商品不限库存");
            gold.Amount = 1800;
            Check(!store.TryPurchase(1005, 0) && !store.TryPurchase(1005, int.MaxValue) && gold.Amount == 1800, "拒绝零数量和超量购买");
            Check(store.TryPurchase(1005, 2) && gold.Amount == 1500 && pack.GetReserveBullet() == reserve + 20 && pack.GetCount(SupplyKind.Bullet) == 5, "150 一组购买两组二十发进入后备，弹匣不变");
            Check(!store.TryPurchase(3001, 1) && soul.GetSoul() == 0, "增益不足碎片时不能购买");
            soul.AddSoul(150);
            float attack = _player.EffectiveAttack;
            Check(store.TryPurchase(3001, 1) && soul.GetSoul() == 145 && _player.EffectiveAttack == attack * 1.5f, "5 碎片兑换攻击 +50%");
            Check(store.TryPurchase(3002, 1) && Mathf.IsEqualApprox(_player.EffectiveMoveSpeed, 600), "原版移速仍正确应用 +20% 增益");
            Check(store.TryPurchase(3003, 1) && Mathf.IsEqualApprox(_player.ShotsPerSecond, 6), "攻速增益实际作用于射击");
            Check(store.TryPurchase(3004, 5) && _player.CriticalChance == 1 && !store.TryPurchase(3004, 1), "暴击封顶，不出售无效增益");
            store.SetOpen(false); store.SetOpen(false);
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            Check(_player.FireTowards(Vector2.Right), "提升攻速后可再次射击");
            var criticalBullet = GetTree().GetFirstNodeInGroup("bullet") as Bullet;
            Check(criticalBullet != null && criticalBullet.Damage == 15, "100% 暴击将提升后的攻击翻倍为实际弹伤");
            criticalBullet.QueueFree();
            Check(!Stop.IsPaused, "重复开关不会遗留暂停锁");
            var coffin = _game.GetNode<Coffin>("coffin");
            var guardian = coffin.GetNode<Zombie>("miniZom");
            _player.GlobalPosition = coffin.GlobalPosition + new Vector2(170, 0); await Frames();
            guardian.take_damage(999);
            Check(coffin.Status == Coffin.CoffinState.Closed && !coffin.CanInteract && guardian.Health == 15 &&
                !guardian.Visible && guardian.CollisionLayer == 0, "范围之外棺材保持封闭，未开启的僵尸不受伤、不挡路");
            dialogue.OpenDialogue(coffin);
            Check(!dialogue.IsDialogOpen && !Stop.IsPaused, "封闭棺材不开放F摸棺对话");
            guardian.SetPhysicsProcess(false);
            coffin.SetPhysicsProcess(false);
            _player.GlobalPosition = coffin.GlobalPosition + new Vector2(90, 0); await Frames();
            coffin.SetPhysicsProcess(true); await Frames();
            Check(coffin.Status == Coffin.CoffinState.Fighting && guardian.Visible && guardian.IsActive &&
                guardian.CollisionLayer != 0 && coffin.GetNode<StaticBody2D>("SolidBody").CollisionLayer == 0,
                "靠近自动开棺激活对应僵尸，并解除棺材实体阻挡");
            dialogue.OpenDialogue(coffin);
            Check(!coffin.CanInteract && !dialogue.IsDialogOpen && !Stop.IsPaused,
                "战斗期间F不会触发摸棺或暂停敌人");
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
            _player.GlobalPosition = coffin.GlobalPosition + new Vector2(900, 0);
            guardian.GlobalPosition = guardian.ReturnPosition + new Vector2(0, -40);
            Vector2 returnDirection = guardian.GlobalPosition.DirectionTo(guardian.ReturnPosition);
            guardian._PhysicsProcess(0.016);
            Check(guardian.Velocity.Dot(returnDirection) > 0, "离开领地后僵尸返回棺材外的安全位置");
            // 从真实空闲且未被墙体或其它实体遮挡的位置射击守卫。
            var playerShape = _player.GetNode<CollisionShape2D>("CollisionShape2D");
            bool foundShot = false;
            for (int angle = 0; angle < 24 && !foundShot; angle++)
            {
                Vector2 candidate = guardian.GlobalPosition + Vector2.Right.Rotated(angle * Mathf.Tau / 24f) * 90;
                Transform2D transform = playerShape.GlobalTransform;
                transform.Origin += candidate - _player.GlobalPosition;
                var excludes = new Godot.Collections.Array<Rid> { _player.GetRid(), guardian.GetRid() };
                var query = new PhysicsShapeQueryParameters2D { Shape = playerShape.Shape, Transform = transform,
                    CollisionMask = 1, CollideWithAreas = false, Exclude = excludes };
                var ray = PhysicsRayQueryParameters2D.Create(candidate, guardian.GlobalPosition, 1, excludes);
                if (_player.GetWorld2D().DirectSpaceState.IntersectShape(query, 1).Count == 0 &&
                    _player.GetWorld2D().DirectSpaceState.IntersectRay(ray).Count == 0)
                { _player.GlobalPosition = candidate; foundShot = true; }
            }
            Check(foundShot, "棺材外保留未被实体遮挡的射击位置");
            int beforeSoul = soul.GetSoul();
            await ToSignal(GetTree().CreateTimer(1f / _player.ShotsPerSecond + 0.04f), SceneTreeTimer.SignalName.Timeout);
            Check(_player.FireTowards(guardian.GlobalPosition - _player.GlobalPosition), "向实际守卫射击");
            await Frames(15);
            Check(coffin.Status == Coffin.CoffinState.Unlocked && soul.GetSoul() - beforeSoul >= 2 && soul.GetSoul() - beforeSoul <= 5, "击败守卫获得碎片并解锁棺材");
            _player.GlobalPosition = coffin.GlobalPosition + new Vector2(90, 0); await Frames();
            int beforeGold = gold.Amount;
            dialogue.OpenDialogue(coffin);
            Check(dialogue.IsDialogOpen && Stop.IsPaused, "守卫死亡后F摸棺对话确认暂停世界");
            Action("esc");
            Check(coffin.Status == Coffin.CoffinState.Unlocked && !Stop.IsPaused && gold.Amount == beforeGold,
                "取消摸棺不领取掉落，解锁状态保留");
            dialogue.OpenDialogue(coffin);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true });
            Input.FlushBufferedEvents(); Action("interact");
            Check(coffin.Status == Coffin.CoffinState.Unlocked && !dialogue.IsDialogOpen && gold.Amount == beforeGold,
                "滚轮可选择离开，F不误领摸棺掉落");
            dialogue.OpenDialogue(coffin); Action("interact");
            int afterGold = gold.Amount; coffin.Interact(_player);
            Check(afterGold - beforeGold >= 300 && afterGold - beforeGold <= 800 && gold.Amount == afterGold && coffin.Status == Coffin.CoffinState.Looted, "摸棺按 2005 掉落表发奖且仅一次");
            var chest = _game.GetNode<Treasure>("Treasure2");
            reserve = pack.GetReserveBullet(); beforeGold = gold.Amount;
            chest.Interact(_player); chest.Interact(_player);
            Check(pack.GetReserveBullet() == reserve + 12 && gold.Amount - beforeGold >= 100 && gold.Amount - beforeGold <= 500 && !chest.CanInteract, "宝箱按 2004 财宝与补给一次结算");
            var gate = _game.GetNode<开门机关>("开门机关");
            var exit = _game.GetNode<Endarea>("endarea");
            var result = _game.GetNode<End>("HUD/end");
            Check(!exit.Monitoring && !gate.IsUnlocked, "未踩开门机关时出口不可通关");
            exit.EmitSignal(Area2D.SignalName.BodyEntered, _player);
            Check(!result.Visible, "出口关闭时无法提前获得胜利");
            gate.EmitSignal(Area2D.SignalName.BodyEntered, _player); await Frames();
            Check(gate.IsUnlocked && gate.IsOpen && exit.Monitoring && _game.GetNode<CanvasItem>("终点大门").Visible,
                "开门机关显示打开门图像并启用终点检测");
            Check(_game.GetNode<ExpeditionHud>("ExpeditionHud").GetChild<Control>(0).GetChild<Label>(0).Text.Contains("墓门已开启"), "开门后探索目标提示正确更新");
            exit.EmitSignal(Area2D.SignalName.BodyEntered, _player);
            Check(result.Visible && Stop.IsPaused && result.FinalScore == gold.Amount, "逃出时携带财宝结算，胜利界面冻结世界");
            result.GetNode<Button>("continue").EmitSignal(Button.SignalName.Pressed); await Frames(4);
            _game = GetTree().CurrentScene as Node2D; _player = _game.GetNode<Player>("player");
            Check(_player.hp == 1000 && _game.GetNode<Coffin>("coffin").Status == Coffin.CoffinState.Closed && !Stop.IsPaused, "继续探索开始新一局，物资、敌人和暂停状态重置");
            await ClearGame();
            await StartGame();
            var trap = _game.GetNode<Trap>("SpikeTrap");
            _player.GlobalPosition = trap.GlobalPosition; await Frames(4);
            Check(_player.hp == 940, "地刺实际碰撞造成 60 伤害");
            Check(!trap.Activate(), "机关遵循五秒冷却，不能重复触发");
            var arrows = _game.GetNode<Trap>("ArrowTrap");
            Check(arrows.Activate(), "暗箭机关可以启动"); await Frames(100);
            Check(arrows.SalvosFired == 10 && arrows.ShotsFired == 30, "暗箭机关十轮齐射，每轮三条平行弹道共三十支箭");
            var poisonBullet = GD.Load<PackedScene>("res://scenes/bullet.tscn").Instantiate<Bullet>();
            poisonBullet.HitsPlayer = true; poisonBullet.AppliesSlow = true; poisonBullet.Damage = 40;
            _game.AddChild(poisonBullet); hp = _player.hp; poisonBullet.ApplyDamage(_player);
            Check(_player.hp == hp - 40 && _player.GetNode<State>("state").IsPoisoned, "毒箭命中时造成伤害并施加中毒");
            poisonBullet.QueueFree();
            var targetCoffin = _game.GetNode<Coffin>("coffin"); targetCoffin.SetPhysicsProcess(false);
            var target = targetCoffin.GetNode<Zombie>("miniZom");
            target.SetPhysicsProcess(false);
            _player.GlobalPosition = targetCoffin.GlobalPosition + new Vector2(90, 0); await Frames();
            targetCoffin.SetPhysicsProcess(true); await Frames();
            Check(target.IsActive && targetCoffin.Status == Coffin.CoffinState.Fighting,
                "机关箭验证使用靠近自动激活的真实守卫");
            var everyone = GD.Load<PackedScene>("res://scenes/bullet.tscn").Instantiate<Bullet>();
            everyone.HitsEveryone = true; everyone.Damage = 10; _game.AddChild(everyone);
            everyone.ApplyDamage(target); hp = _player.hp; everyone.ApplyDamage(_player);
            Check(target.Health == 5 && _player.hp == hp - 10, "机关箭伤害玩家和僵尸双方"); everyone.QueueFree();
            var settings = _game.GetNode<EscStop>("HUD/escstop");
            settings.SetOpen(true);
            await Frames();
            var settingsPanel = settings.GetChild<Control>(1).GetChild<PanelContainer>(1);
            Check(settingsPanel.GetGlobalRect().Position.X >= 0 && settingsPanel.GetGlobalRect().Position.Y >= 0 && settingsPanel.GetGlobalRect().End.X <= GetViewport().GetVisibleRect().Size.X, "设置面板居中且不被裁切");
            Action("esc"); Check(!Stop.IsPaused && !settings.Visible, "设置菜单可暂停并由 Esc 返回");
            _player.TakeDamage(10000); await Frames(4);
            Check(GetTree().CurrentScene.SceneFilePath == "res://scenes/gameover.tscn" && !Stop.IsPaused, "生命归零进入失败场景，暂停状态不泄漏");
            GetTree().CurrentScene.GetNode<Button>("again").EmitSignal(Button.SignalName.Pressed); await Frames(4);
            _game = GetTree().CurrentScene as Node2D;
            Check(_game.GetNode<Player>("player").hp == 1000 && !Stop.IsPaused, "失败后重新挑战恢复全新游戏");
            _game.GetNode<EscStop>("HUD/escstop").SetOpen(true);
            await ClearGame();
            CollectManagedResources(); await Frames();
            GD.Print($"RESULT: {_checks} passed, 0 failed"); GetTree().Quit(0);
        }
        catch (Exception ex)
        { GD.PushError($"REGRESSION FAILED after {_checks} passes: {ex}"); GetTree().Quit(1); }
    }
}
