using Godot;
using System.Collections.Generic;

/// <summary>沿用原背包底图、格子和使用按钮，物资与使用效果仍来自玩家节点。</summary>
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
        // 只统一中文字体，保留场景里原有美术、颜色和透明点击区域。
        Theme = new Theme { DefaultFont = new SystemFont
            { FontNames = new[] { "Noto Sans CJK SC", "WenQuanYi Zen Hei", "sans-serif" } } };
        ZIndex = 100;
        _name = GetNode<Label>("itemname");
        _description = GetNode<Label>("description");
        _icon = GetNode<Sprite2D>("item");
        _use = GetNode<Button>("UseItem");
        _use.Pressed += UseSelected;
        foreach (var (slotPath, id) in DisplayItems)
        {
            int itemId = id;
            var slot = GetNode<Button>(slotPath);
            var counter = slot.GetNode<Label>("counter");
            counter.Position = new Vector2(2, 48); counter.Size = new Vector2(66, 21);
            counter.AddThemeFontSizeOverride("font_size", 14);
            counter.HorizontalAlignment = HorizontalAlignment.Right;
            _slots[id] = (slot, slot.GetNode<Sprite2D>("image"), counter);
            slot.Pressed += () => SelectItem(itemId);
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
            slot.Icon.Scale = FitIcon(slot.Icon.Texture, 71);
            slot.Counter.Text = id == 1005 ? $"{_pack.GetCount(SupplyKind.Bullet)}/{_pack.GetReserveBullet()}" : Count(id).ToString();
        }
        _snapshot = Snapshot(); SelectItem(_selected);
    }
    private static Vector2 FitIcon(Texture2D texture, float size) =>
        texture == null ? Vector2.One : Vector2.One * (size / Mathf.Max(texture.GetWidth(), texture.GetHeight()));
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
        _use.Visible = id >= 1003;
        _icon.Texture = GameData.ItemIcon(id);
        _icon.Scale = FitIcon(_icon.Texture, 110);
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
