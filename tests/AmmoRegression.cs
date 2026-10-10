using Godot;
using System;
using System.Threading.Tasks;

/// <summary>真实游戏场景中的弹药库存及商店结算回归。</summary>
public partial class AmmoRegression : Node
{
    private int _checks;
    private Node _game;
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        GD.Print($"PASS {++_checks}: {message}");
    }
    private async Task Frames(int count = 3)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    private async void Run()
    {
        int exitCode = 0;
        try
        {
            _game = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate();
            GetTree().Root.AddChild(_game); GetTree().CurrentScene = _game;
            await Frames();
            var pack = _game.GetNode<Pack>("player/pack");
            var gold = _game.GetNode<Gold>("player/gold");
            var player = _game.GetNode<Player>("player");
            var soul = player.GetNode<soulpiece>("soulpiece");
            var store = _game.GetNode<Store>("HUD/store/store");
            Check(pack.GetCount(SupplyKind.Bullet) == 6 && pack.GetReserveBullet() == 60,
                "实际新局初始弹药为六发弹膛和六十发后备");
            pack.SetCount(SupplyKind.Bullet, 2); pack._totalBullet = 20;
            pack.Add(SupplyKind.Bullet, 5);
            Check(pack.GetCount(SupplyKind.Bullet) == 2 && pack.GetReserveBullet() == 25, "通用获取子弹入口只增加备用弹药");
            pack.AddBullet(10);
            Check(pack.GetCount(SupplyKind.Bullet) == 2 && pack.GetReserveBullet() == 35, "掉落子弹入口同样只增加备用弹药");
            pack.AddBullet(-1); pack.Add(SupplyKind.Bullet, 0); pack.AddBullet(int.MaxValue);
            Check(pack.GetReserveBullet() == 35, "无效或溢出获取数量不改变备用弹药");
            pack.ReloadBullets();
            Check(pack.GetCount(SupplyKind.Bullet) == 6 && pack.GetReserveBullet() == 31, "换弹从备用弹药转移四发到弹膛");
            pack.ReloadBullets();
            Check(pack.GetReserveBullet() == 31, "满弹膛重复换弹不会消耗备用弹药");
            pack.SetCount(SupplyKind.Bullet, int.MaxValue);
            Check(pack.GetCount(SupplyKind.Bullet) == pack.MagazineCapacity, "弹膛数量始终不超过容量");
            pack.TryConsume(SupplyKind.Bullet);
            Check(pack.GetCount(SupplyKind.Bullet) == 5 && pack.GetReserveBullet() == 31, "射击消费弹膛，备用弹药不变");
            pack.SetCount(SupplyKind.Drug, int.MaxValue);
            pack.AddDrug(1);
            Check(!pack.CanAdd(SupplyKind.Drug, 1) && pack.GetCount(SupplyKind.Drug) == int.MaxValue, "其他物资不会因累加溢出变为负数");

            pack.SetCount(SupplyKind.Bullet, 2); pack._totalBullet = 20; gold.Amount = 450;
            store.SetOpen(true); await Frames();
            foreach (var product in GameData.Table("shop").EnumerateArray())
            {
                int id = product.GetProperty("ID").GetInt32();
                Check(store.GetProductPrice(id) == (product.GetProperty("type").GetInt32() == 1 ? 5 : 150) &&
                    store.GetProductStock(id) == int.MaxValue, $"商品 {id} 使用降低后的单价且库存不限量");
            }
            int stock = store.GetProductStock(1005);
            Check(store.TryPurchase(1005, 1) && gold.Amount == 300 && pack.GetReserveBullet() == 30 &&
                pack.GetCount(SupplyKind.Bullet) == 2 && store.GetProductStock(1005) == stock,
                "购买一组获得十发备用弹药，花费一百五十财宝且商品不限库存");
            Check(store.TryPurchase(1005, 2) && gold.Amount == 0 && pack.GetReserveBullet() == 50 &&
                pack.GetCount(SupplyKind.Bullet) == 2 && store.GetProductStock(1005) == stock,
                "购买两组获得二十发备用弹药，花费三百财宝且不减少库存");
            stock = store.GetProductStock(1005);
            Check(!store.TryPurchase(1005, 1) && gold.Amount == 0 && pack.GetReserveBullet() == 50 &&
                store.GetProductStock(1005) == stock, "余额不足不扣库存或增加子弹");
            gold.Amount = 600;
            Check(!store.TryPurchase(1005, 0) && !store.TryPurchase(1005, -1) &&
                !store.TryPurchase(1005, int.MaxValue) && !store.TryPurchase(9999, 1) &&
                gold.Amount == 600 && pack.GetReserveBullet() == 50 && store.GetProductStock(1005) == stock,
                "零数量、负数、超量和未知商品不会改变结算状态");
            pack._totalBullet = int.MaxValue - 2;
            Check(!store.TryPurchase(1005, 1) && gold.Amount == 600 && pack.GetReserveBullet() == int.MaxValue - 2 &&
                store.GetProductStock(1005) == stock, "备用弹药无法完整容纳十发时拒绝购买且不扣款");
            Check(!store.TryPurchase(1004, 1) && gold.Amount == 600 && pack.GetCount(SupplyKind.Drug) == int.MaxValue,
                "普通物资达到整数容量时拒绝购买且不扣款");
            pack._totalBullet = 50; gold.Amount = 4500;
            Check(store.TryPurchase(1005, 15) && store.TryPurchase(1005, 15) && gold.Amount == 0 &&
                pack.GetReserveBullet() == 350 && pack.GetCount(SupplyKind.Bullet) == 2 && store.GetProductStock(1005) == stock,
                "同一商品可连续购买十五组，累计三十组不会耗尽库存或改变弹匣");
            gold.Amount = int.MaxValue; pack._totalBullet = 0;
            int largestAffordable = int.MaxValue / store.GetProductPrice(1005);
            Check(store.TryPurchase(1005, largestAffordable) && gold.Amount == int.MaxValue % 150 &&
                pack.GetReserveBullet() == largestAffordable * Store.BulletsPerPurchase && store.GetProductStock(1005) == stock,
                "接近整数金额边界的大批量弹药按长整数结算并完整发货");
            soul._soulCounter = 125; float attack = player.EffectiveAttack;
            Check(store.TryPurchase(3001, 11) && store.TryPurchase(3001, 12) && soul.GetSoul() == 10 &&
                Mathf.IsEqualApprox(player.EffectiveAttack, attack * 12.5f) && store.GetProductStock(3001) == int.MaxValue,
                "灵魂增益可连续购买超过十份，效果累计且不消耗库存");
            soul._soulCounter = 100;
            Check(!store.TryPurchase(3004, 6) && soul.GetSoul() == 100 && player.CriticalChance == 0,
                "不限库存仍拒绝超过暴击百分之百上限的整笔购买且不扣款");
            Check(store.TryPurchase(3004, 5) && soul.GetSoul() == 75 && player.CriticalChance == 1 &&
                !store.TryPurchase(3004, 1) && soul.GetSoul() == 75 && store.GetProductStock(3004) == int.MaxValue,
                "暴击达到百分之百后不再收费，限制来自属性上限而非库存");
            store.SetOpen(false);
            Check(!store.TryPurchase(1005, 1), "关闭商店后不能远程购买");
        }
        catch (Exception error) { GD.PushError(error.ToString()); exitCode = 1; }
        finally
        {
            if (GodotObject.IsInstanceValid(_game)) _game.QueueFree();
            await Frames();
            await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
            GetTree().CurrentScene = null;
            GC.Collect(); GC.WaitForPendingFinalizers();
            await Frames();
            GD.Print($"RESULT: {_checks} passed, {exitCode} failed");
            GetTree().Quit(exitCode);
        }
    }
}
