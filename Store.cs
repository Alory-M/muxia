using Godot;
using System.Collections.Generic;

/// <summary>羊皮纸四列商店：不限库存，按实际购买数量结算。</summary>
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
        public Button Buy, Select;
        public 购买数量 Quantity;
        public int ProductId;
    }
    private Player _player;
    private Pack _pack;
    private Gold _gold;
    private soulpiece _soul;
    private Control _ui;
    private Stop _stop;
    private readonly HashSet<int> _products = new();
    private readonly List<Column> _columns = new();
    private Label _balance, _feedback, _categoryHint;
    private Button _suppliesTab, _buffsTab;
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
        _ui.Theme = PaperUiLayout.Theme();
        _ui.ZIndex = 100;
        _stop = GetNode<Stop>("stop");
        foreach (var row in GameData.Table("shop").EnumerateArray()) _products.Add(row.GetProperty("ID").GetInt32());
        SetOpen(false);
        Callable.From(BindUi).CallDeferred();
    }
    private void BindUi()
    {
        var background = _ui.GetNode<Sprite2D>("background");
        background.Position = new Vector2(576, 324);
        PaperUiLayout.Fit(background, new Vector2(820, 541));
        PaperUiLayout.Close(_ui);
        _balance = PaperLabel("", new Vector2(306, 117), new Vector2(600, 38), 20, PaperUiLayout.Ink);
        _balance.Name = "Balance";
        _feedback = PaperLabel("点击物品查看说明。子弹每组 10 发，购买后加入后备弹药。", new Vector2(306, 527), new Vector2(623, 49), 16, PaperUiLayout.Ink);
        _feedback.Name = "PurchaseFeedback";
        _suppliesTab = CategoryButton("财宝商店", new Vector2(191, 188), () => SelectCategory(2));
        _suppliesTab.Name = "SuppliesTab";
        _buffsTab = CategoryButton("灵魂增益", new Vector2(191, 313), () => SelectCategory(1));
        _buffsTab.Name = "BuffsTab";
        _categoryHint = PaperLabel("", new Vector2(191, 442), new Vector2(91, 66), 16, Colors.White);
        _categoryHint.HorizontalAlignment = HorizontalAlignment.Center;
        PaperButton("离开", new Vector2(191, 519), new Vector2(91, 41), () => SetOpen(false));
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
            float center = 373 + index * 161;
            var frame = _ui.GetNode<Sprite2D>(index == 0 ? "Sprite2D" : $"Sprite2D{index + 1}");
            frame.Position = new Vector2(center, 264); PaperUiLayout.Fit(frame, new Vector2(86, 86), true, true);
            column.Icon.Position = new Vector2(center, 264);
            column.Description = PaperLabel("", new Vector2(center - 60, 353), new Vector2(120, 50), 16, Colors.White);
            column.Description.Name = $"ProductDescription{index + 1}";
            column.Description.HorizontalAlignment = HorizontalAlignment.Center;
            PaperUiLayout.Label(column.Name, new Vector2(center - 63, 313), new Vector2(126, 34), 22, PaperUiLayout.Ink, HorizontalAlignment.Center);
            PaperUiLayout.Label(column.Stock, new Vector2(center - 61, 191), new Vector2(122, 29), 16, Colors.White, HorizontalAlignment.Center);
            PaperUiLayout.ArtButton(column.Buy, new Vector2(center - 57, 463), new Vector2(114, 37), 17);
            LayoutQuantity(column.Quantity, center);
            column.Select = new Button { Position = new Vector2(center - 60, 226), Size = new Vector2(120, 177), SelfModulate = new Color(1, 1, 1, 0) };
            _ui.AddChild(column.Select);
            column.Select.Pressed += () => ShowProductDetails(column.ProductId);
            column.Quantity.Max = int.MaxValue;
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
    private static void LayoutQuantity(购买数量 quantity, float center)
    {
        PaperUiLayout.Label(quantity, new Vector2(center - 25, 416), new Vector2(50, 34), 18, Colors.White, HorizontalAlignment.Center);
        // 新素材的按下态仅包含被按的圆钮，按常态坐标还原，避免拉伸覆盖整条数量框。
        Sprite2D basis = null;
        foreach (Node node in quantity.GetChildren())
            if (node is Sprite2D sprite && sprite.Name.ToString().StartsWith("正常")) { basis = sprite; break; }
        if (basis == null) return;
        var normalRect = basis.Texture.GetImage().GetUsedRect();
        Vector2 normalSize = normalRect.Size;
        Vector2 normalCenter = normalRect.Position + normalSize / 2;
        foreach (Node node in quantity.GetChildren())
        {
            if (node is Sprite2D sprite)
            {
                var used = sprite.Texture.GetImage().GetUsedRect();
                Vector2 usedSize = used.Size;
                Vector2 usedCenter = used.Position + usedSize / 2;
                sprite.Position = new Vector2(25, 17) + (usedCenter - normalCenter) / normalSize * new Vector2(120, 32);
                PaperUiLayout.Fit(sprite, usedSize / normalSize * new Vector2(120, 32), true);
            }
        }
        var plus = quantity.GetNode<Button>("增"); var minus = quantity.GetNode<Button>("减");
        plus.Position = new Vector2(58, 0); plus.Size = new Vector2(28, 34); plus.TooltipText = "数量 +1";
        minus.Position = new Vector2(-36, 0); minus.Size = new Vector2(28, 34); minus.TooltipText = "数量 -1";
    }
    private Button CategoryButton(string text, Vector2 position, System.Action action)
    {
        var button = new Button { Position = position, Size = new Vector2(91, 110), SelfModulate = new Color(1, 1, 1, 0) };
        _ui.AddChild(button);
        button.AddChild(new Sprite2D { Name = "Artwork", Position = new Vector2(45.5f, 41.5f), RegionEnabled = true });
        var label = new Label { Name = "CategoryLabel", Text = text };
        button.AddChild(label);
        PaperUiLayout.Label(label, new Vector2(0, 85), new Vector2(91, 25), 17, Colors.White, HorizontalAlignment.Center);
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        button.Pressed += action;
        return button;
    }
    private static void SetCategoryArtwork(Button button, string type, bool selected)
    {
        var sprite = button.GetNode<Sprite2D>("Artwork");
        sprite.Texture = GD.Load<Texture2D>($"res://assets/user/icon/shop_{type}_{(selected ? "click" : "noclick")}.png");
        // 原稿保留整个商店画布，只取绘制按钮的区域，去掉画布上方残留参考线。
        sprite.RegionRect = new Rect2(31, type == "coin" ? 73 : 214, 169, 154);
        sprite.Scale = new Vector2(91f / 169, 83f / 154);
    }
    private void ShowProductDetails(int id)
    {
        _feedback.Text = $"{ProductName(id)}：{ProductEffect(id).Replace("\n", " ")}";
    }
    private Label PaperLabel(string text, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        var label = new Label { Text = text, Position = position, Size = size,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore };
        PaperUiLayout.Label(label, position, size, fontSize, color);
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
        label.AddThemeFontSizeOverride("font_size", 18); label.AddThemeColorOverride("font_color", PaperUiLayout.Ink);
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
        if (_feedback != null) _feedback.Text = category == 2 ? "物资使用财宝购买。子弹每组 10 发，加入后备弹药。" : "使用灵魂碎片兑换增益，立即生效，持续至本局结束。";
    }
    private void RefreshColumns()
    {
        for (int index = 0; index < _columns.Count; index++)
        {
            var column = _columns[index];
            int id = _category == 2 ? SupplyIds[index] : 3001 + index;
            column.ProductId = id;
            column.Icon.Texture = id >= 3000 ? GD.Load<Texture2D>($"res://assets/user/icon/{id switch { 3001 => "icon_attack", 3002 => "icon_move_speed", 3003 => "icon_attack_speed", _ => "icon_critical" }}.png") : GameData.ItemIcon(id);
            PaperUiLayout.Icon(column.Icon, 72);
            column.Name.Text = ProductName(id);
            column.Description.Text = ProductSummary(id);
            column.Select.TooltipText = ProductEffect(id);
            column.Price.Text = GetProductPrice(id) + (id == 1005 ? " / 组" : id >= 3000 ? " 碎片" : " 财宝");
        }
        if (_suppliesTab != null) SetCategoryArtwork(_suppliesTab, "coin", _category == 2);
        if (_buffsTab != null) SetCategoryArtwork(_buffsTab, "soul", _category == 1);
        if (_categoryHint != null) _categoryHint.Text = _category == 2 ? $"{BulletsPerPurchase} 发 / 组\n{GetProductPrice(1005)} 财宝" : "本局增益\n灵魂兑换";
        RefreshStock();
    }
    private void RefreshStock()
    {
        foreach (var column in _columns)
        {
            bool capped = column.ProductId == 3004 && GetRemainingCriticalPurchases() == 0;
            column.Quantity.Max = int.MaxValue;
            column.Stock.Text = capped ? "暴击已满" : "不限量";
            column.Buy.TooltipText = $"购买 {column.Quantity.Count}{(column.ProductId == 1005 ? " 组" : " 份")} {ProductName(column.ProductId)}，合计 {(long)GetProductPrice(column.ProductId) * column.Quantity.Count}{Currency(column.ProductId)}";
            column.Buy.Disabled = capped;
            column.Buy.Modulate = capped ? new Color(0.65f, 0.65f, 0.65f) : Colors.White;
        }
    }
    public int GetProductPrice(int id) => GameData.Row("shop", id).GetProperty("prize").GetInt32();
    // 保留旧库存查询接口；MaxValue 表示已上架商品不限量，不随购买减少。
    public int GetProductStock(int id) => _products.Contains(id) ? int.MaxValue : 0;
    private int GetRemainingCriticalPurchases() => _player == null ? 0 : Mathf.Max(0,
        Mathf.CeilToInt((1f - _player.CriticalChance) / GameData.Number(GameData.Row("buff", 3004), "add") - 0.0001f));
    public bool TryPurchase(int id, int quantity)
    {
        if (!IsOpen || _player == null || _pack == null || _gold == null || _soul == null || !_products.Contains(id)) return false;
        if (quantity <= 0) return Fail("购买数量必须大于零。");
        if (id == 3004 && quantity > GetRemainingCriticalPurchases()) return Fail("暴击增益超过 100% 上限，本次未扣款。");
        var product = GameData.Row("shop", id);
        long cost = (long)GetProductPrice(id) * quantity;
        if (cost < 0 || cost > int.MaxValue) return Fail("购买金额超出范围。");
        bool buff = product.GetProperty("type").GetInt32() == 1;
        long supplyAmount = id == 1005 ? (long)quantity * BulletsPerPurchase : quantity;
        if (!buff && (supplyAmount > int.MaxValue || !_pack.CanAdd(GameData.Supply(id), (int)supplyAmount)))
            return Fail("物资数量超出背包范围，本次未扣款。");
        if (!(buff ? _soul.TrySpend((int)cost) : _gold.TrySpend((int)cost))) return Fail($"{Currency(id)}不足，本次未扣款。");
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
    private static string ProductSummary(int id)
    {
        if (id >= 3000)
        {
            string stat = id switch { 3001 => "攻击", 3002 => "移速", 3003 => "攻速", _ => "暴击" };
            return $"{stat} +{GameData.Number(GameData.Row("buff", id), "add") * 100:0}%\n本局有效";
        }
        return id switch { 1003 => "解除流血", 1004 => "恢复 100 生命", 1005 => $"{BulletsPerPurchase} 发 / 组\n加入后备弹药", 1006 => "解除中毒", _ => "" };
    }
    public int GetPrice(SupplyKind kind) => GetProductPrice(ItemId(kind));
    public int GetStock(SupplyKind kind) => GetProductStock(ItemId(kind));
    private static int ItemId(SupplyKind kind) => kind switch { SupplyKind.Drug => 1004, SupplyKind.Bandage => 1003, SupplyKind.Antidote => 1006, _ => 1005 };
}
