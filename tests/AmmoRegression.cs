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
            var store = _game.GetNode<Store>("HUD/store/store");
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

            pack.SetCount(SupplyKind.Bullet, 2); pack._totalBullet = 20; gold.Amount = 1800;
            store.SetOpen(true); await Frames();
            int stock = store.GetProductStock(1005);
            Check(store.TryPurchase(1005, 1) && gold.Amount == 1200 && pack.GetReserveBullet() == 30 &&
                pack.GetCount(SupplyKind.Bullet) == 2 && store.GetProductStock(1005) == stock - 1,
                "购买数量一获得十发备用弹药，花费六百财宝及一组库存");
            Check(store.TryPurchase(1005, 2) && gold.Amount == 0 && pack.GetReserveBullet() == 50 &&
                pack.GetCount(SupplyKind.Bullet) == 2 && store.GetProductStock(1005) == stock - 3,
                "购买数量二获得二十发备用弹药，花费一千二百财宝及两组库存");
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
