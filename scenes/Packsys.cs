using Godot;

/// <summary>B 打开背包：全部物资数量、详情及选择后使用；关闭时释放暂停锁。</summary>
public partial class Packsys : Control
{
    private Player _player;
    private Pack _pack;
    private Clear _clear;
    private Gold _gold;
    private soulpiece _soul;
    private Stop _stop;
    private Label _name, _description;
    private TextureRect _icon;
    private Button _use;
    private VBoxContainer _items;
    private int _selected;
    private (int, int, int, int, int, int, int, float, bool, bool) _snapshot;
    public override void _Ready()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        _pack = _player?.GetNodeOrNull<Pack>("pack");
        _clear = _player?.GetNodeOrNull<Clear>("clear");
        _gold = _player?.GetNodeOrNull<Gold>("gold");
        _soul = _player?.GetNodeOrNull<soulpiece>("soulpiece");
        _stop = GetNode<Stop>("stop"); Theme = UiKit.Theme(); ZIndex = 100;
        var shade = new Godot.ColorRect { Color = new Color(0, 0, 0, 0.7f) }; AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer(); AddChild(panel); UiKit.Center(panel, new Vector2(800, 540));
        var body = new VBoxContainer(); panel.AddChild(body);
        body.AddChild(UiKit.Label("行囊 · 墓下", 28));
        var content = new HBoxContainer(); body.AddChild(content);
        _items = new VBoxContainer { CustomMinimumSize = new Vector2(310, 0) }; content.AddChild(_items);
        var detail = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; content.AddChild(detail);
        _name = UiKit.Label("选择物品查看详情", 24); detail.AddChild(_name);
        _icon = new TextureRect { CustomMinimumSize = new Vector2(180, 140), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered }; detail.AddChild(_icon);
        _description = UiKit.Label(""); _description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _description.CustomMinimumSize = new Vector2(350, 80); detail.AddChild(_description);
        _use = UiKit.Button("使用", UseSelected); _use.Name = "UseItem"; detail.AddChild(_use);
        body.AddChild(UiKit.Button("关闭背包 · B / Esc", () => SetOpen(false)));
        SetOpen(false);
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
            _clear.Use(ev.IsActionPressed("Q") ? SupplyKind.Bandage :
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
        if (open) { _selected = 0; RefreshItems(); }
    }
    public void RefreshItems()
    {
        if (_pack == null || _items == null) return;
        foreach (Node child in _items.GetChildren()) { _items.RemoveChild(child); child.QueueFree(); }
        for (int id = 1001; id <= 1006; id++)
        {
            int itemId = id;
            var row = new HBoxContainer(); _items.AddChild(row);
            row.AddChild(new TextureRect { Texture = GameData.ItemIcon(id), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(42, 42) });
            string count = id == 1005 ? $"{_pack.GetCount(SupplyKind.Bullet)} / {_pack.GetReserveBullet()}" : Count(id).ToString();
            var button = UiKit.Button($"{GameData.Text($"item_drop{id - 1000}")}　{count}", () => SelectItem(itemId));
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(button);
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
        _use.Visible = id >= 1003;
        _icon.Texture = GameData.ItemIcon(id);
        _name.Text = id == 0 ? "选择物品查看详情" : GameData.Text($"item_drop{id - 1000}");
        _description.Text = id switch
        {
            0 => "左侧选择物品，右侧可查看说明并使用。",
            1001 => "击败僵尸获得，可在阴掌柜处兑换本局增益。",
            1002 => "开箱、摸棺获得，可购买物资。逃出古墓时按携带财宝结算得分。",
            _ => GameData.Text($"text_item{id - 1000}")
        };
        _use.Text = id == 1005 ? "装填弹药" : "使用";
        if (id == 1005)
            _use.Disabled = _player.hp <= 0 || _pack.GetReserveBullet() <= 0 ||
                _pack.GetCount(SupplyKind.Bullet) >= _pack.MagazineCapacity;
        else
        {
            string reason = "";
            _use.Disabled = id < 1003 || _clear == null || !_clear.CanUse(GameData.Supply(id), out reason);
            if (id >= 1003 && _use.Disabled) _description.Text += $"\n{reason}";
        }
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
