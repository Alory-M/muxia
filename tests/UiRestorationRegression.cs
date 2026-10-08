using Godot;
using System;
using System.Threading.Tasks;

/// <summary>覆盖原版美术节点与真实背包/商店按钮，不依赖程序生成面板。</summary>
public partial class UiRestorationRegression : Node
{
    private int _checks;
    private Node2D _game;
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException(description);
        _checks++; GD.Print($"PASS {_checks}: {description}");
    }
    private async Task Frames(int count = 2)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private bool ArtFits(Sprite2D sprite)
    {
        Vector2 size = sprite.Texture.GetSize() * sprite.GlobalScale;
        var bounds = new Rect2(sprite.GlobalPosition - size / 2, size);
        return GetViewport().GetVisibleRect().Encloses(bounds);
    }
    private async Task Click(Button button)
    {
        Vector2 position = button.GetGlobalRect().GetCenter();
        // 无界面运行的物理窗口可能只有 64×64；使用逻辑视口坐标，仍由 GUI 正常命中。
        GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
        Input.FlushBufferedEvents(); await Frames();
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        Input.FlushBufferedEvents(); await Frames();
    }
    private async void Run()
    {
        try
        {
            _game = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate<Node2D>();
            GetTree().Root.AddChild(_game); GetTree().CurrentScene = _game; await Frames(3);
            var player = _game.GetNode<Player>("player");
            var pack = player.GetNode<Pack>("pack"); var gold = player.GetNode<Gold>("gold");
            var soul = player.GetNode<soulpiece>("soulpiece");
            var bag = _game.GetNode<Packsys>("HUD/packsys");
            Check(bag.GetNode<Sprite2D>("background").Texture.ResourcePath == "res://ui/背包.jpeg" && ArtFits(bag.GetNode<Sprite2D>("background")), "背包恢复原底图与视口内布局");
            bag.SetOpen(true); await Frames();
            Check(bag.GetNode<Sprite2D>("button14/image").Texture.ResourcePath == "res://bin/pack/子弹.png" &&
                bag.GetNode<Sprite2D>("button21/image").Texture == GameData.ItemIcon(1002) &&
                bag.GetNode<Sprite2D>("button22/image").Texture == GameData.ItemIcon(1001), "原格子风格继续显示弹药、金币和灵魂碎片");
            player.hp = 800; int drugs = pack.GetCount(SupplyKind.Drug);
            await Click(bag.GetNode<Button>("button11"));
            Check(bag.GetNode<Label>("itemname").Text == GameData.Text("item_drop4") && bag.GetNode<Button>("UseItem").Visible, "原药品格子真实点击打开详情与使用按钮");
            await Click(bag.GetNode<Button>("UseItem"));
            Check(player.hp == 900 && pack.GetCount(SupplyKind.Drug) == drugs - 1 && Stop.IsPaused, "原美术使用按钮真实点击生效且只扣一份");
            await Click(bag.GetNode<Button>("close2"));
            Check(!bag.Visible && !Stop.IsPaused, "原关闭按钮即时释放背包暂停锁");
            var storeUi = _game.GetNode<Control>("HUD/store");
            var store = storeUi.GetNode<Store>("store");
            Check(storeUi.GetNode<Sprite2D>("background").Texture.ResourcePath == "res://ui/商店.png" && ArtFits(storeUi.GetNode<Sprite2D>("background")), "商店恢复原底图与四列商品布局");
            store.SetOpen(true); gold.Amount = 1800; await Frames();
            var quantity = storeUi.GetNode<购买数量>("购买子弹数量");
            int reserve = pack.GetReserveBullet(), magazine = pack.GetCount(SupplyKind.Bullet), stock = store.GetProductStock(1005);
            await Click(quantity.GetNode<Button>("增"));
            Check(quantity.Count == 2 && storeUi.GetNode<Label>("ProductDescription4").Text.Contains("10"), "原加减数量框可选择两组且说明每组十发");
            await Click(storeUi.GetNode<Button>("买子弹"));
            Check(pack.GetReserveBullet() == reserve + 20 && pack.GetCount(SupplyKind.Bullet) == magazine && gold.Amount == 600 && store.GetProductStock(1005) == stock - 2, "原购买按钮两组二十发全部进入后备，按组扣钱和库存");
            soul.AddSoul(30); float attack = player.EffectiveAttack;
            await Click(storeUi.GetNode<Button>("BuffsTab"));
            Check(storeUi.GetNode<Label>("药品").Text == GameData.Text("text_buff1"), "原商店美术提供灵魂增益类别");
            await Click(storeUi.GetNode<Button>("买药"));
            Check(soul.GetSoul() == 15 && player.EffectiveAttack == attack * 1.5f, "原四列按钮在灵魂类别正确兑换增益");
            await Click(storeUi.GetNode<Button>("SuppliesTab"));
            Check(storeUi.GetNode<Label>("药品").Text == GameData.Text("item_drop4") && storeUi.GetNode<Label>("买子弹/Label").Text == "600/组", "切回财宝类别恢复药品与弹药按组价格");
            await Click(storeUi.GetNode<Button>("close2"));
            Check(!store.IsOpen && !Stop.IsPaused, "原关闭按钮即时释放商店暂停锁");
            _game.QueueFree(); await Frames(4); GC.Collect(); GC.WaitForPendingFinalizers(); await Frames();
            GD.Print($"UI RESTORATION RESULT: {_checks} passed, 0 failed"); GetTree().Quit(0);
        }
        catch (Exception ex)
        { GD.PushError($"UI RESTORATION FAILED after {_checks} passes: {ex}"); GetTree().Quit(1); }
    }
}
