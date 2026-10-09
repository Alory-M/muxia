using Godot;
using System;
using System.Threading.Tasks;

/// <summary>真实背包使用、补货、弹药边界及棺材状态变化更新 HUD，不改变物品格布局。</summary>
public partial class InventoryHudRegression : Node
{
    private Node2D _game;
    private Player _player;
    private Pack _pack;
    private Packsys _bag;
    private int _checks;
    private static readonly (int Id, string Slot, string Hud)[] Items =
    {
        (1001, "button22", "icon_soul"), (1002, "button21", "icon_coin"),
        (1003, "button12", "base_bengdai/icon_bengdai"),
        (1004, "button11", "base_yaopin/icon_yaopin"),
        (1005, "button14", "icon_zidan"), (1006, "button13", "base_jieduji/icon_jieduji")
    };
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        GD.Print($"PASS INVENTORY HUD {++_checks}: {description}");
    }
    private async Task Frames(int count = 3)
    {
        for (int i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
    private void ItemVisibility(bool visible, string description)
    {
        foreach (var (id, slotPath, hudPath) in Items)
        {
            var slot = _bag.GetNode<Button>(slotPath);
            Check(slot.GetNode<Sprite2D>("image").Visible == visible &&
                slot.GetNode<Label>("counter").Visible == visible && slot.Disabled != visible &&
                _game.GetNode<CanvasItem>("HUD/" + hudPath).Visible == visible,
                $"{description}（物品 {id}）");
        }
    }
    private async Task Inventory()
    {
        var gold = _player.GetNode<Gold>("gold");
        var soul = _player.GetNode<soulpiece>("soulpiece");
        var drugSlot = _bag.GetNode<Button>("button11");
        Vector2 slotPosition = drugSlot.Position, slotSize = drugSlot.Size;
        _bag.SetOpen(true);
        foreach (SupplyKind kind in Enum.GetValues<SupplyKind>()) _pack.SetCount(kind, 0);
        _pack._totalBullet = 0; gold.Amount = 0; soul.TrySpend(soul.GetSoul());
        await Frames();
        ItemVisibility(false, "零数量隐藏快捷栏及背包物品图标、数量，并禁用空物品格");
        var supplyCounts = (Label)_game.GetNode<ExpeditionHud>("ExpeditionHud").FindChild("SupplyCounts", true, false);
        Check(supplyCounts.Text == "\n\n\n\n" && !_game.GetNode<CanvasItem>("HUD/base_yaopin").Visible &&
            !_game.GetNode<CanvasItem>("HUD/base_bengdai").Visible && !_game.GetNode<CanvasItem>("HUD/base_jieduji").Visible,
            "消耗品耗尽同时隐藏快捷栏底框、按键与数量文字，并保留原行距");
        foreach (var (id, _, _) in Items)
        {
            _bag.SelectItem(id);
            Check(!_bag.GetNode<Sprite2D>("item").Visible && !_bag.GetNode<Button>("UseItem").Visible,
                $"空物品 {id} 无法显示详情或使用按钮");
        }
        Check(_game.GetNode<CanvasItem>("HUD/icon_gun").Visible && drugSlot.Position == slotPosition && drugSlot.Size == slotSize,
            "弹药耗尽仍保留枪械图标，空格不改变原有背包布局");
        foreach (SupplyKind kind in Enum.GetValues<SupplyKind>()) _pack.Add(kind, 1);
        gold.Add(10); soul.AddSoul();
        await Frames();
        ItemVisibility(true, "补充物品后在仍打开的背包中自动恢复图标、数量和选择能力");
        Check(supplyCounts.Text == "E 药品 1\n\nQ 绷带 1\n\nZ 解毒剂 1" &&
            _game.GetNode<CanvasItem>("HUD/base_yaopin").Visible && _game.GetNode<CanvasItem>("HUD/base_bengdai").Visible &&
            _game.GetNode<CanvasItem>("HUD/base_jieduji").Visible, "补货后快捷栏底框、按键和数量文字在原位恢复");

        _player.hp = 800;
        _bag.SelectItem(1004); _bag.UseSelected();
        await Frames();
        Check(_player.hp == 900 && _pack.GetCount(SupplyKind.Drug) == 0 &&
            !drugSlot.GetNode<Sprite2D>("image").Visible &&
            !_bag.GetNode<Sprite2D>("item").Visible && !_bag.GetNode<Label>("itemname").Visible &&
            !_bag.GetNode<Label>("description").Visible && !_bag.GetNode<Button>("UseItem").Visible &&
            !_game.GetNode<CanvasItem>("HUD/base_yaopin/icon_yaopin").Visible,
            "真实使用最后一份药品后清空选中详情并同步隐藏两处图标");
        _pack.AddDrug(1); await Frames(); _bag.SelectItem(1004);
        Check(drugSlot.GetNode<Sprite2D>("image").Visible && _bag.GetNode<Sprite2D>("item").Visible &&
            _bag.GetNode<Label>("itemname").Text == GameData.Text("item_drop4"),
            "药品补货后可以再次选择并显示原有物品详情");

        var ammoSlot = _bag.GetNode<Button>("button14");
        var ammoHud = _game.GetNode<CanvasItem>("HUD/icon_zidan");
        _pack.SetCount(SupplyKind.Bullet, 0); _pack._totalBullet = 5; await Frames(); _bag.SelectItem(1005);
        Check(ammoSlot.GetNode<Sprite2D>("image").Visible && ammoHud.Visible &&
            ammoSlot.GetNode<Label>("counter").Text == "0/5" && !_bag.GetNode<Button>("UseItem").Disabled,
            "弹膛为空但后备仍有弹药时保留图标，并允许装填");
        _bag.UseSelected(); await Frames();
        Check(_pack.GetCount(SupplyKind.Bullet) == 5 && _pack.GetReserveBullet() == 0 &&
            ammoSlot.GetNode<Sprite2D>("image").Visible && ammoHud.Visible &&
            ammoSlot.GetNode<Label>("counter").Text == "5/0",
            "装填耗尽后备仍保留弹膛内弹药的图标和准确数量");
        _pack.SetCount(SupplyKind.Bullet, 0); await Frames();
        Check(!ammoSlot.GetNode<Sprite2D>("image").Visible && !ammoHud.Visible &&
            !_bag.GetNode<Sprite2D>("item").Visible && ammoSlot.Disabled,
            "仅弹膛和后备同时为空时隐藏弹药，并清空弹药详情");
        _pack.AddBullet(10); await Frames();
        Check(ammoSlot.GetNode<Sprite2D>("image").Visible && ammoHud.Visible && !ammoSlot.Disabled &&
            ammoSlot.GetNode<Label>("counter").Text == "0/10", "获得后备弹药后恢复弹药显示和背包选择");

        _pack.SetCount(SupplyKind.Bandage, 0); _pack.SetCount(SupplyKind.Antidote, 0);
        var state = _player.GetNode<State>("state");
        state.ChangeState(PlayerState.Bleed); state.ChangeState(PlayerState.Slow); await Frames();
        int activeStatusIcons = 0;
        foreach (Node node in _game.GetNode<ExpeditionHud>("ExpeditionHud").FindChildren("*", "TextureRect", true, false))
            if (node is TextureRect icon && icon.Visible &&
                (icon.Texture.ResourcePath.Contains("icon_bleed") || icon.Texture.ResourcePath.Contains("icon_poison"))) activeStatusIcons++;
        Check(activeStatusIcons == 2 && !_game.GetNode<CanvasItem>("HUD/base_bengdai/icon_bengdai").Visible &&
            !_game.GetNode<CanvasItem>("HUD/base_jieduji/icon_jieduji").Visible,
            "绷带与解毒剂耗尽不隐藏仍生效的流血、中毒状态提示");
        state.ResetToNormal(); _bag.SetOpen(false); await Frames();
    }
    private async Task CoffinCounts()
    {
        var counts = (Label)_game.GetNode<ExpeditionHud>("ExpeditionHud").FindChild("CoffinCounts", true, false);
        int total = 0;
        foreach (Node node in GetTree().GetNodesInGroup("interactable")) if (node is Coffin) total++;
        Check(total > 0 && counts.Text == $"棺材总数 {total}　未开 {total}　可摸 0", "HUD 初始显示真实棺材总数、未开与可摸数量");
        var coffin = GD.Load<PackedScene>("res://zombiegd/Fzombie/CoMini.tscn").Instantiate<Coffin>();
        coffin.Position = new Vector2(10000, 10500); _game.AddChild(coffin); total++;
        var guardian = coffin.GetNode<Zombie>("miniZom");
        guardian.SetPhysicsProcess(false);
        _player.GlobalPosition = coffin.GlobalPosition + new Vector2(90, 0); await Frames();
        Check(coffin.Status == Coffin.CoffinState.Fighting && guardian.IsActive &&
            counts.Text == $"棺材总数 {total}　未开 {total - 1}　可摸 0", "靠近自动开棺战斗只减少未开数量，尚未计入可摸");
        guardian.take_damage(guardian.Health); await Frames();
        Check(counts.Text == $"棺材总数 {total}　未开 {total - 1}　可摸 1", "真实击杀守卫后 HUD 增加可摸棺材数量");
        coffin.Interact(_player); await Frames();
        Check(coffin.Status == Coffin.CoffinState.Looted &&
            counts.Text == $"棺材总数 {total}　未开 {total - 1}　可摸 0", "领取掉落后减少可摸数量，仍保留棺材总数");
        var empty = GD.Load<PackedScene>("res://zombiegd/coffin.tscn").Instantiate<Coffin>();
        empty.Position = new Vector2(11000, 11000); _game.AddChild(empty); await Frames();
        Check(counts.Text == $"棺材总数 {total + 1}　未开 {total}　可摸 0", "动态加入棺材后 HUD 更新总数与未开数量");
        _player.GlobalPosition = empty.GlobalPosition + new Vector2(90, 0); await Frames();
        Check(counts.Text == $"棺材总数 {total + 1}　未开 {total - 1}　可摸 1", "无守卫棺材打开后立即计入可摸数量");
        empty.QueueFree(); await Frames();
        Check(counts.Text == $"棺材总数 {total}　未开 {total - 1}　可摸 0", "动态移除棺材后 HUD 不保留失效计数");
    }
    private async void Run()
    {
        int exitCode = 0;
        try
        {
            _game = GD.Load<PackedScene>("res://scenes/game.tscn").Instantiate<Node2D>();
            GetTree().Root.AddChild(_game); GetTree().CurrentScene = _game;
            _player = _game.GetNode<Player>("player"); _player.SetPhysicsProcess(false);
            _game.GetNode<StaticBody2D>("background/edge").CollisionLayer = 0;
            _player.GlobalPosition = new Vector2(10000, 10000);
            _pack = _player.GetNode<Pack>("pack"); _bag = _game.GetNode<Packsys>("HUD/packsys");
            await Frames(); await Inventory(); await CoffinCounts();
        }
        catch (Exception exception) { GD.PushError($"INVENTORY HUD FAILED after {_checks}: {exception}"); exitCode = 1; }
        finally
        {
            if (GodotObject.IsInstanceValid(_game)) _game.QueueFree();
            await Frames(); GetTree().CurrentScene = null;
            GC.Collect(); GC.WaitForPendingFinalizers(); await Frames();
            GD.Print($"INVENTORY HUD RESULT: {_checks} passed, {exitCode} failed"); GetTree().Quit(exitCode);
        }
    }
}
