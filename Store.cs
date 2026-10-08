using Godot;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>财宝购买消耗品、灵魂碎片兑换增益，确认数量后一次性扣款发货。</summary>
public partial class Store : Node
{
    public const int BulletsPerPurchase = 5;
    private Player _player;
    private Pack _pack;
    private Gold _gold;
    private soulpiece _soul;
    private Control _ui;
    private Stop _stop;
    private readonly Dictionary<int, int> _stock = new();
    private VBoxContainer _rows;
    private Label _balance, _feedback, _purchaseText;
    private PanelContainer _popup;
    private Control _popupShade;
    private SpinBox _quantity;
    private int _category = 2, _selectedId;
    private bool _wasOpen;
    public bool IsOpen => _ui?.Visible == true;
    public override void _Ready()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        _pack = _player?.GetNodeOrNull<Pack>("pack");
        _gold = _player?.GetNodeOrNull<Gold>("gold");
        _soul = _player?.GetNodeOrNull<soulpiece>("soulpiece");
        _ui = GetParent() as Control;
        _ui.Theme = UiKit.Theme(); _ui.ZIndex = 100;
        _stop = GetNode<Stop>("stop");
        foreach (var row in GameData.Table("shop").EnumerateArray()) _stock[row.GetProperty("ID").GetInt32()] = 10;
        SetOpen(false);
        Callable.From(BuildUi).CallDeferred();
    }
    private void BuildUi()
    {
        var shade = new Godot.ColorRect { Color = new Color(0, 0, 0, 0.7f) };
        _ui.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer(); _ui.AddChild(panel); UiKit.Center(panel, new Vector2(830, 540));
        var body = new VBoxContainer(); panel.AddChild(body);
        body.AddChild(UiKit.Label("阴掌柜 · 墓中交易", 28));
        _balance = UiKit.Label(""); body.AddChild(_balance);
        var middle = new HBoxContainer(); body.AddChild(middle);
        var sidebar = new VBoxContainer { CustomMinimumSize = new Vector2(160, 0) }; middle.AddChild(sidebar);
        sidebar.AddChild(UiKit.Button("财宝商店", () => SelectCategory(2)));
        sidebar.AddChild(UiKit.Button("灵魂增益", () => SelectCategory(1)));
        sidebar.AddChild(UiKit.Button("离开商店", () => SetOpen(false)));
        _rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; middle.AddChild(_rows);
        _feedback = UiKit.Label("选择商品，确认数量后购买。"); body.AddChild(_feedback);
        body.AddChild(UiKit.Label("Esc 返回上一层", 16));
        _popupShade = new Godot.ColorRect { Color = new Color(0, 0, 0, 0.6f), Visible = false };
        _ui.AddChild(_popupShade); _popupShade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _popup = new PanelContainer { Visible = false }; _ui.AddChild(_popup); UiKit.Center(_popup, new Vector2(490, 260));
        var purchase = new VBoxContainer(); _popup.AddChild(purchase);
        _purchaseText = UiKit.Label(""); _purchaseText.AutowrapMode = TextServer.AutowrapMode.WordSmart; purchase.AddChild(_purchaseText);
        _quantity = new SpinBox { MinValue = 1, MaxValue = 10, Step = 1, Value = 1 }; purchase.AddChild(_quantity);
        _quantity.ValueChanged += _ => UpdatePurchase();
        var buttons = new HBoxContainer(); purchase.AddChild(buttons);
        buttons.AddChild(UiKit.Button("确认购买", () =>
        {
            if (TryPurchase(_selectedId, (int)_quantity.Value)) { ClosePurchase(); RefreshRows(); }
            else UpdatePurchase();
        }));
        buttons.AddChild(UiKit.Button("取消", ClosePurchase));
        RefreshRows();
    }
    public void SetOpen(bool open)
    {
        _ui.Visible = open; _wasOpen = open; _stop.SetPaused(open);
        if (open) { ClosePurchase(); RefreshRows(); }
    }
    public override void _Process(double delta)
    {
        if (_ui.Visible != _wasOpen) SetOpen(_ui.Visible);
        if (IsOpen && _balance != null) _balance.Text = $"财宝 {_gold?.Amount ?? 0}　　灵魂碎片 {_soul?.GetSoul() ?? 0}";
    }
    public override void _Input(InputEvent ev)
    {
        if (!IsOpen || !ev.IsActionPressed("esc")) return;
        if (_popup.Visible) ClosePurchase(); else SetOpen(false);
        GetViewport().SetInputAsHandled();
    }
    private void SelectCategory(int category) { _category = category; ClosePurchase(); RefreshRows(); }
    private void RefreshRows()
    {
        foreach (Node child in _rows.GetChildren()) { _rows.RemoveChild(child); child.QueueFree(); }
        foreach (var product in GameData.Table("shop").EnumerateArray())
        {
            if (product.GetProperty("type").GetInt32() != _category) continue;
            int id = product.GetProperty("ID").GetInt32();
            var row = new HBoxContainer(); _rows.AddChild(row);
            if (id < 3000)
            {
                var icon = new TextureRect { Texture = GameData.ItemIcon(id), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(48, 48) };
                row.AddChild(icon);
            }
            var description = UiKit.Label($"{ProductName(id)}\n{ProductEffect(id)}", 18);
            description.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            description.CustomMinimumSize = new Vector2(320, 0); row.AddChild(description);
            string unit = id == 1005 ? " / 组" : "";
            var buy = UiKit.Button($"购买 · {GetProductPrice(id)}{Currency(id)}{unit}\n剩余 {GetProductStock(id)}{(id == 1005 ? " 组" : "")}", () => SelectProduct(id));
            buy.Disabled = GetProductStock(id) == 0; row.AddChild(buy);
        }
    }
    private void SelectProduct(int id)
    {
        _selectedId = id; _quantity.MaxValue = Mathf.Max(1, GetProductStock(id)); _quantity.Value = 1;
        _popupShade.Visible = _popup.Visible = true; UpdatePurchase(); _quantity.GrabFocus();
    }
    private void ClosePurchase() { _popupShade.Visible = _popup.Visible = false; }
    private void UpdatePurchase()
    {
        int quantity = (int)_quantity.Value;
        string amount = _selectedId == 1005 ? $"数量 {quantity} 组（{(long)quantity * BulletsPerPurchase} 发）" : $"数量 {quantity}";
        _purchaseText.Text = $"{ProductName(_selectedId)}\n{ProductEffect(_selectedId)}\n{amount} · 共 {(long)GetProductPrice(_selectedId) * quantity}{Currency(_selectedId)}\n{_feedback.Text}";
    }
    public int GetProductPrice(int id) => GameData.Row("shop", id).GetProperty("prize").GetInt32();
    public int GetProductStock(int id)
    {
        int stock = _stock.TryGetValue(id, out var value) ? value : 0;
        if (id == 3004 && _player != null)
            stock = Mathf.Min(stock, Mathf.CeilToInt((1f - _player.CriticalChance) / GameData.Number(GameData.Row("buff", id), "add") - 0.0001f));
        return Mathf.Max(0, stock);
    }
    public bool TryPurchase(int id, int quantity)
    {
        if (!IsOpen || _player == null || _pack == null || _gold == null || _soul == null || !_stock.ContainsKey(id)) return false;
        if (quantity <= 0 || quantity > GetProductStock(id)) return Fail("数量无效或库存不足。");
        var product = GameData.Row("shop", id);
        long cost = (long)GetProductPrice(id) * quantity;
        if (cost < 0 || cost > int.MaxValue) return Fail("购买金额超出范围。");
        bool buff = product.GetProperty("type").GetInt32() == 1;
        long supplyAmount = id == 1005 ? (long)quantity * BulletsPerPurchase : quantity;
        if (!buff && (supplyAmount > int.MaxValue || !_pack.CanAdd(GameData.Supply(id), (int)supplyAmount)))
            return Fail("物资数量超出背包范围，本次未扣款。");
        if (!(buff ? _soul.TrySpend((int)cost) : _gold.TrySpend((int)cost))) return Fail($"{Currency(id)}不足，本次未扣款。");
        _stock[id] -= quantity;
        if (buff) _player.ApplyBuff(id, quantity);
        else _pack.Add(GameData.Supply(id), (int)supplyAmount);
        string received = id == 1005 ? $"{quantity} 组 / {supplyAmount} 发，已加入备用弹药" : $"× {quantity}";
        _feedback.Text = $"购得 {ProductName(id)} {received}，消耗 {cost}{Currency(id)}。";
        InteractionController.Notify(_feedback.Text);
        return true;
    }
    private bool Fail(string text) { _feedback.Text = text; return false; }
    private static string Currency(int id) => id >= 3000 ? "灵魂碎片" : "财宝";
    private static string ProductName(int id) => GameData.Text(GameData.Row("shop", id).GetProperty("name").GetString());
    private static string ProductEffect(int id)
    {
        if (id >= 3000)
        {
            var buff = GameData.Row("buff", id);
            return buff.GetProperty("effect").GetString().Replace("{}", $"{GameData.Number(buff, "add") * 100:0}%");
        }
        return id == 1005 ? $"每组 {BulletsPerPurchase} 发，加入备用弹药，按 R 装填，弹匣容量 6 发。" : GameData.Text($"text_item{id - 1000}");
    }
    // 保留旧数量组件的查询接口，方便复用。
    public int GetPrice(SupplyKind kind) => GetProductPrice(ItemId(kind));
    public int GetStock(SupplyKind kind) => GetProductStock(ItemId(kind));
    private static int ItemId(SupplyKind kind) => kind switch { SupplyKind.Drug => 1004, SupplyKind.Bandage => 1003, SupplyKind.Antidote => 1006, _ => 1005 };
}
