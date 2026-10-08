using Godot;
using System.Collections.Generic;

/// <summary>复用原商店美术与四列数量框，财宝购买物资，灵魂碎片兑换增益。</summary>
public partial class Store : Node
{
    public const int BulletsPerPurchase = 10;
    private static readonly (string Icon, string Name, string Buy, string Quantity, string Stock)[] ColumnPaths =
    {
        ("drug", "药品", "买药", "购买药品数量", "剩余药品"),
        ("Antidote", "解毒剂", "买解毒剂", "购买解毒剂数量", "剩余解毒剂"),
        ("绷带", "绷带2", "买绷带", "购买绷带数量", "剩余绷带"),
        ("Bullet", "子弹", "买子弹", "购买子弹数量", "剩余子弹")
    };
    private static readonly int[] SupplyIds = { 1004, 1006, 1003, 1005 };
    private sealed class Column
    {
        public Sprite2D Icon;
        public Label Name, Description, Stock, Price;
        public Button Buy;
        public 购买数量 Quantity;
        public int ProductId;
    }
    private Player _player;
    private Pack _pack;
    private Gold _gold;
    private soulpiece _soul;
    private Control _ui;
    private Stop _stop;
    private readonly Dictionary<int, int> _stock = new();
    private readonly List<Column> _columns = new();
    private Label _balance, _feedback;
    private int _category = 2;
    private bool _wasOpen;
    public bool IsOpen => _ui?.Visible == true;

    public override void _Ready()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        _pack = _player?.GetNodeOrNull<Pack>("pack");
        _gold = _player?.GetNodeOrNull<Gold>("gold");
        _soul = _player?.GetNodeOrNull<soulpiece>("soulpiece");
        _ui = GetParent() as Control;
        _ui.Theme = new Theme { DefaultFont = new SystemFont
            { FontNames = new[] { "Noto Sans CJK SC", "WenQuanYi Zen Hei", "sans-serif" } } };
        _ui.ZIndex = 100;
        _stop = GetNode<Stop>("stop");
        foreach (var row in GameData.Table("shop").EnumerateArray()) _stock[row.GetProperty("ID").GetInt32()] = 10;
        SetOpen(false);
        Callable.From(BindUi).CallDeferred();
    }
    private void BindUi()
    {
        _balance = PaperLabel("", new Vector2(331, 152), new Vector2(491, 35), 17, new Color(0.12f, 0.1f, 0.07f));
        _balance.Name = "Balance";
        _feedback = PaperLabel("选择数量，点击价格按钮购买。子弹每组 10 发。", new Vector2(331, 481), new Vector2(491, 43), 13, new Color(0.12f, 0.1f, 0.07f));
        _feedback.Name = "PurchaseFeedback";
        PaperButton("财宝商店", new Vector2(237, 224), new Vector2(73, 45), () => SelectCategory(2)).Name = "SuppliesTab";
        PaperButton("灵魂增益", new Vector2(237, 286), new Vector2(73, 45), () => SelectCategory(1)).Name = "BuffsTab";
        PaperButton("离开", new Vector2(237, 463), new Vector2(73, 30), () => SetOpen(false));
        for (int index = 0; index < ColumnPaths.Length; index++)
        {
            var paths = ColumnPaths[index];
            var column = new Column
            {
                Icon = _ui.GetNode<Sprite2D>(paths.Icon), Name = _ui.GetNode<Label>(paths.Name),
                Buy = _ui.GetNode<Button>(paths.Buy), Quantity = _ui.GetNode<购买数量>(paths.Quantity),
                Stock = _ui.GetNode<Label>(paths.Stock), Price = _ui.GetNode<Label>($"{paths.Buy}/Label")
            };
            // 说明使用未缩放的 Label，避免换增益图标时文字跟随图标缩放。
            column.Icon.GetNode<Label>("Label").Visible = false;
            float center = column.Icon.Position.X;
            column.Description = PaperLabel("", new Vector2(center - 57, 349), new Vector2(114, 43), 11, Colors.White);
            column.Description.Name = $"ProductDescription{index + 1}";
            column.Name.AddThemeFontSizeOverride("font_size", 23);
            column.Name.Position = new Vector2(center - 62, 306); column.Name.Size = new Vector2(124, 36);
            column.Stock.AddThemeFontSizeOverride("font_size", 12);
            column.Stock.Position = new Vector2(center - 60, 281); column.Stock.Size = new Vector2(120, 19);
            column.Stock.HorizontalAlignment = HorizontalAlignment.Right;
            column.Price.Position = Vector2.Zero; column.Price.Size = column.Buy.Size;
            column.Price.AddThemeFontSizeOverride("font_size", 12);
            column.Quantity.Max = 10;
            column.Buy.Pressed += () =>
            {
                TryPurchase(column.ProductId, column.Quantity.Count);
                RefreshColumns();
            };
            _columns.Add(column);
        }
        var close = _ui.GetNode<Button>("close2");
        var pressed = _ui.GetNode<Sprite2D>("点击"); pressed.Visible = false;
        close.ButtonDown += () => pressed.Visible = true;
        close.ButtonUp += () => pressed.Visible = false;
        close.Pressed += () => SetOpen(false);
        IgnoreDecorativeMouseInput(_ui);
        RefreshColumns();
    }
    private Label PaperLabel(string text, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        var label = new Label { Text = text, Position = position, Size = size,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeConstantOverride("line_spacing", -5);
        label.AddThemeColorOverride("font_color", color);
        _ui.AddChild(label); return label;
    }
    private Button PaperButton(string text, Vector2 position, Vector2 size, System.Action action)
    {
        var button = new Button { Position = position, Size = size, SelfModulate = new Color(1, 1, 1, 0) };
        _ui.AddChild(button);
        var texture = GD.Load<Texture2D>("res://ui/按钮.jpeg");
        button.AddChild(new Sprite2D { Texture = texture, Position = size / 2,
            Scale = new Vector2(size.X / texture.GetWidth(), size.Y / texture.GetHeight()) });
        var label = new Label { Text = text, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 14); label.AddThemeColorOverride("font_color", Colors.Black);
        button.AddChild(label); button.Pressed += action;
        return button;
    }
    private static void IgnoreDecorativeMouseInput(Node node)
    {
        if (node is Label label) label.MouseFilter = Control.MouseFilterEnum.Ignore;
        if (node is TextureRect texture) texture.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (Node child in node.GetChildren()) IgnoreDecorativeMouseInput(child);
    }
    public void SetOpen(bool open)
    {
        _ui.Visible = open; _ui.MouseFilter = open ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
        _wasOpen = open; _stop.SetPaused(open);
        if (open) RefreshColumns();
    }
    public override void _Process(double delta)
    {
        if (_ui.Visible != _wasOpen) SetOpen(_ui.Visible);
        if (IsOpen)
        {
            if (_balance != null) _balance.Text = $"财宝 {_gold?.Amount ?? 0}　　灵魂碎片 {_soul?.GetSoul() ?? 0}";
            RefreshStock();
        }
    }
    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey key && key.Echo) return;
        if (!IsOpen || !ev.IsActionPressed("esc")) return;
        SetOpen(false); GetViewport().SetInputAsHandled();
    }
    public void SelectCategory(int category)
    {
        if (category != 1 && category != 2) return;
        _category = category; RefreshColumns();
    }
    private void RefreshColumns()
    {
        for (int index = 0; index < _columns.Count; index++)
        {
            var column = _columns[index];
            int id = _category == 2 ? SupplyIds[index] : 3001 + index;
            column.ProductId = id;
            column.Icon.Texture = id >= 3000 ? GameData.ItemIcon(1001) : GameData.ItemIcon(id);
            var texture = column.Icon.Texture;
            column.Icon.Scale = Vector2.One * (74f / Mathf.Max(texture.GetWidth(), texture.GetHeight()));
            column.Name.Text = ProductName(id);
            column.Description.Text = ProductEffect(id);
            column.Price.Text = GetProductPrice(id) + (id == 1005 ? "/组" : "");
            column.Buy.TooltipText = $"购买 {column.Quantity.Count}{(id == 1005 ? " 组" : " 份")} {ProductName(id)}，单价 {GetProductPrice(id)}{Currency(id)}";
        }
        RefreshStock();
    }
    private void RefreshStock()
    {
        foreach (var column in _columns)
        {
            int stock = GetProductStock(column.ProductId);
            column.Quantity.Max = stock;
            column.Stock.Text = $"剩余 {stock}{(column.ProductId == 1005 ? " 组" : "")}";
            column.Buy.Disabled = stock == 0;
            column.Buy.Modulate = stock == 0 ? new Color(0.65f, 0.65f, 0.65f) : Colors.White;
        }
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
        Feedback($"购得 {ProductName(id)} {received}，消耗 {cost}{Currency(id)}。");
        RefreshStock();
        return true;
    }
    private void Feedback(string text)
    {
        if (_feedback != null) _feedback.Text = text;
        InteractionController.Notify(text);
    }
    private bool Fail(string text) { Feedback(text); return false; }
    private static string Currency(int id) => id >= 3000 ? "灵魂碎片" : "财宝";
    private static string ProductName(int id) => GameData.Text(GameData.Row("shop", id).GetProperty("name").GetString());
    private static string ProductEffect(int id)
    {
        if (id >= 3000)
        {
            var buff = GameData.Row("buff", id);
            return buff.GetProperty("effect").GetString().Replace("{}", $"{GameData.Number(buff, "add") * 100:0}%") + "\n本局有效。";
        }
        return id switch
        {
            1003 => "带血的医用绷带。\n解除流血。",
            1004 => "试管字迹模糊，药效尚在。\n恢复 100 生命值。",
            1005 => $"{BulletsPerPurchase} 发/组，进入后备。\nR 装填，弹膛 6 发。",
            1006 => "墓里的毒不能拖。\n解除中毒。",
            _ => ""
        };
    }
    public int GetPrice(SupplyKind kind) => GetProductPrice(ItemId(kind));
    public int GetStock(SupplyKind kind) => GetProductStock(ItemId(kind));
    private static int ItemId(SupplyKind kind) => kind switch { SupplyKind.Drug => 1004, SupplyKind.Bandage => 1003, SupplyKind.Antidote => 1006, _ => 1005 };
}
