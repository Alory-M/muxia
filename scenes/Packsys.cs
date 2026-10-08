using Godot;
using System.Collections.Generic;

/// <summary>沿用羊皮纸和物品格美术，图标、数量及物品详情独立排版。</summary>
public partial class Packsys : Control
{
    private static readonly (string Slot, int ItemId)[] DisplayItems =
    {
        ("button11", 1004), ("button12", 1003), ("button13", 1006),
        ("button14", 1005), ("button21", 1002), ("button22", 1001)
    };
    private Player _player;
    private Pack _pack;
    private Clear _clear;
    private Gold _gold;
    private soulpiece _soul;
    private Stop _stop;
    private Label _name, _description;
    private Sprite2D _icon;
    private Button _use;
    private Label _hint;
    private readonly Dictionary<int, (Button Button, Sprite2D Icon, Label Counter)> _slots = new();
    private int _selected;
    private (int, int, int, int, int, int, int, float, bool, bool) _snapshot;

    public override void _Ready()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        _pack = _player?.GetNodeOrNull<Pack>("pack");
        _clear = _player?.GetNodeOrNull<Clear>("clear");
        _gold = _player?.GetNodeOrNull<Gold>("gold");
        _soul = _player?.GetNodeOrNull<soulpiece>("soulpiece");
        _stop = GetNode<Stop>("stop");
        Theme = PaperUiLayout.Theme();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ZIndex = 100;
        _name = GetNode<Label>("itemname");
        _description = GetNode<Label>("description");
        _icon = GetNode<Sprite2D>("item");
        _use = GetNode<Button>("UseItem");
        var background = GetNode<Sprite2D>("background");
        background.Position = new Vector2(576, 324);
        PaperUiLayout.Fit(background, new Vector2(820, 541));
        PaperUiLayout.Close(this);
        _icon.Position = new Vector2(827, 191);
        PaperUiLayout.Label(_name, new Vector2(712, 280), new Vector2(240, 39), 24, PaperUiLayout.Ink, HorizontalAlignment.Center);
        PaperUiLayout.Label(_description, new Vector2(714, 370), new Vector2(238, 143), 17, PaperUiLayout.Ink);
        _description.VerticalAlignment = VerticalAlignment.Top;
        PaperUiLayout.ArtButton(_use, new Vector2(750, 531), new Vector2(155, 41));
        _hint = new Label { Text = "点击物品查看详情\n选择物资后可使用", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_hint);
        PaperUiLayout.Label(_hint, new Vector2(714, 371), new Vector2(238, 72), 18, PaperUiLayout.Ink, HorizontalAlignment.Center);
        _use.Pressed += UseSelected;
        int slotIndex = 0;
        foreach (var (slotPath, id) in DisplayItems)
        {
            int itemId = id;
            var slot = GetNode<Button>(slotPath);
            var counter = slot.GetNode<Label>("counter");
            slot.Position = new Vector2(202 + (slotIndex % 4) * 123, 103 + (slotIndex / 4) * 123);
            slot.Size = new Vector2(88, 88);
            PaperUiLayout.Label(counter, new Vector2(2, 64), new Vector2(84, 24), 18, Colors.White, HorizontalAlignment.Center);
            counter.AddThemeColorOverride("font_shadow_color", new Color(0.12f, 0.1f, 0.07f));
            counter.AddThemeConstantOverride("shadow_offset_x", 1); counter.AddThemeConstantOverride("shadow_offset_y", 1);
            counter.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            counter.AutowrapMode = TextServer.AutowrapMode.Off;
            var icon = slot.GetNode<Sprite2D>("image");
            icon.Position = new Vector2(44, 34);
            _slots[id] = (slot, slot.GetNode<Sprite2D>("image"), counter);
            slot.Pressed += () => SelectItem(itemId);
            slot.TooltipText = GameData.Text($"item_drop{id - 1000}");
            slotIndex++;
        }
        var close = GetNode<Button>("close2");
        var pressed = GetNode<Sprite2D>("点击");
        pressed.Visible = false;
        close.ButtonDown += () => pressed.Visible = true;
        close.ButtonUp += () => pressed.Visible = false;
        close.Pressed += () => SetOpen(false);
        IgnoreDecorativeMouseInput(this);
        SetOpen(false);
    }

    private static void IgnoreDecorativeMouseInput(Node node)
    {
        if (node is Label label) label.MouseFilter = MouseFilterEnum.Ignore;
        if (node is TextureRect texture) texture.MouseFilter = MouseFilterEnum.Ignore;
        foreach (Node child in node.GetChildren()) IgnoreDecorativeMouseInput(child);
    }
    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey key && key.Echo) return;
        if (ev.IsActionPressed("B") && (Visible || !Stop.IsPaused))
        { SetOpen(!Visible); GetViewport().SetInputAsHandled(); }
        else if (Visible && ev.IsActionPressed("esc"))
        { SetOpen(false); GetViewport().SetInputAsHandled(); }
        else if (Visible && (ev.IsActionPressed("Q") || ev.IsActionPressed("E") || ev.IsActionPressed("Z")))
        {
            _clear?.Use(ev.IsActionPressed("Q") ? SupplyKind.Bandage :
                ev.IsActionPressed("Z") ? SupplyKind.Antidote : SupplyKind.Drug);
            RefreshItems(); GetViewport().SetInputAsHandled();
        }
    }
    public override void _Process(double delta)
    {
        if (Visible && _pack != null && Snapshot() != _snapshot) RefreshItems();
    }
    private (int, int, int, int, int, int, int, float, bool, bool) Snapshot() =>
        (_pack.GetCount(SupplyKind.Bullet), _pack.GetReserveBullet(), _pack.GetCount(SupplyKind.Drug),
         _pack.GetCount(SupplyKind.Bandage), _pack.GetCount(SupplyKind.Antidote), _gold.Amount, _soul.GetSoul(),
         _player.hp, _player.GetNode<State>("state").IsBleeding, _player.GetNode<State>("state").IsPoisoned);
    public void SetOpen(bool open)
    {
        Visible = open; MouseFilter = open ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _stop?.SetPaused(open);
        _selected = 0;
        if (open) RefreshItems(); else SelectItem(0);
    }
    public void RefreshItems()
    {
        if (_pack == null || _slots.Count == 0) return;
        foreach (var (id, slot) in _slots)
        {
            slot.Icon.Texture = GameData.ItemIcon(id);
            PaperUiLayout.Icon(slot.Icon, 60);
            slot.Counter.Text = id == 1005 ? $"{_pack.GetCount(SupplyKind.Bullet)}/{_pack.GetReserveBullet()}" : Count(id).ToString();
            slot.Button.TooltipText = $"{GameData.Text($"item_drop{id - 1000}")}\n数量：{slot.Counter.Text}";
        }
        _snapshot = Snapshot(); SelectItem(_selected);
    }
    private int Count(int id) => id switch
    {
        1001 => _soul.GetSoul(), 1002 => _gold.Amount, 1005 => _pack.GetReserveBullet(),
        _ => _pack.GetCount(GameData.Supply(id))
    };
    public void SelectItem(int id)
    {
        if (id != 0 && (id < 1001 || id > 1006)) return;
        _selected = id;
        if (_use == null) return;
        _icon.Visible = _name.Visible = _description.Visible = id != 0;
        _hint.Visible = id == 0;
        _use.Visible = id >= 1003;
        foreach (var (itemId, slot) in _slots)
            slot.Icon.Modulate = itemId == id || id == 0 ? Colors.White : new Color(0.77f, 0.77f, 0.77f);
        _icon.Texture = GameData.ItemIcon(id);
        PaperUiLayout.Icon(_icon, 115);
        _name.Text = id == 0 ? "" : GameData.Text($"item_drop{id - 1000}");
        _description.Text = id switch
        {
            0 => "",
            1001 => "击败僵尸获得，可在阴掌柜处兑换本局增益。",
            1002 => "开箱、摸棺获得，可购买物资。逃出古墓时按携带财宝结算得分。",
            1005 => "弹膛 / 后备弹药。按 R 装填，弹膛容量 6 发。商店每组 10 发加入后备。",
            _ => GameData.Text($"text_item{id - 1000}")
        };
        _use.GetNode<Label>("Label").Text = id == 1005 ? "装填弹药" : "使用";
        if (id == 1005)
            _use.Disabled = _player.hp <= 0 || _pack.GetReserveBullet() <= 0 ||
                _pack.GetCount(SupplyKind.Bullet) >= _pack.MagazineCapacity;
        else
        {
            string reason = "";
            _use.Disabled = id < 1003 || _clear == null || !_clear.CanUse(GameData.Supply(id), out reason);
            if (id >= 1003 && _use.Disabled) _description.Text += $"\n{reason}";
        }
        _use.Modulate = _use.Disabled ? new Color(0.65f, 0.65f, 0.65f) : Colors.White;
    }
    public void UseSelected()
    {
        if (!Visible || _clear == null || _player.hp <= 0) return;
        switch (_selected)
        {
            case 1003: _clear.Use(SupplyKind.Bandage); break;
            case 1004: _clear.Use(SupplyKind.Drug); break;
            case 1005: _pack.ReloadBullets(); break;
            case 1006: _clear.Use(SupplyKind.Antidote); break;
        }
        RefreshItems();
    }
}
