using Godot;
using System.Collections.Generic;

/// <summary>目标、物资快捷键、独立状态图标、本局计时与属性显示。</summary>
public partial class ExpeditionHud : CanvasLayer
{
    private Player _player;
    private State _state;
    private Pack _pack;
    private Gold _gold;
    private soulpiece _soul;
    private Label _status, _objective, _stats, _supplies, _coffinCounts;
    private HBoxContainer _states;
    private Control _root;
    private TextureRect _bleedIcon, _poisonIcon;
    private readonly Dictionary<int, CanvasItem> _itemIcons = new();
    private readonly Dictionary<SupplyKind, CanvasItem> _supplySlots = new();
    public double ElapsedSeconds { get; private set; }
    public override void _Ready()
    {
        Layer = 3;
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        _state = _player.GetNode<State>("state");
        _pack = _player.GetNode<Pack>("pack");
        _gold = _player.GetNode<Gold>("gold");
        _soul = _player.GetNode<soulpiece>("soulpiece");
        foreach (var (id, path) in new[]
        {
            (1001, "HUD/icon_soul"), (1002, "HUD/icon_coin"),
            (1003, "HUD/base_bengdai/icon_bengdai"), (1004, "HUD/base_yaopin/icon_yaopin"),
            (1005, "HUD/icon_zidan"), (1006, "HUD/base_jieduji/icon_jieduji")
        }) _itemIcons[id] = GetParent().GetNode<CanvasItem>(path);
        foreach (var (kind, path) in new[]
        {
            (SupplyKind.Drug, "HUD/base_yaopin"), (SupplyKind.Bandage, "HUD/base_bengdai"),
            (SupplyKind.Antidote, "HUD/base_jieduji")
        }) _supplySlots[kind] = GetParent().GetNode<CanvasItem>(path);
        var root = new Control { Theme = UiKit.Theme(), MouseFilter = Control.MouseFilterEnum.Ignore };
        _root = root;
        AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _objective = UiKit.Label("寻找开门机关，携财宝逃出古墓", 18); root.AddChild(_objective);
        _objective.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _objective.OffsetLeft = -280; _objective.OffsetRight = 280; _objective.OffsetTop = 8; _objective.OffsetBottom = 40; _objective.HorizontalAlignment = HorizontalAlignment.Center;
        _stats = UiKit.Label("", 16); root.AddChild(_stats);
        _stats.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight); _stats.OffsetLeft = -380; _stats.OffsetRight = -10; _stats.OffsetTop = 75; _stats.OffsetBottom = 125;
        _coffinCounts = UiKit.Label("", 16); _coffinCounts.Name = "CoffinCounts"; root.AddChild(_coffinCounts);
        _coffinCounts.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _coffinCounts.OffsetLeft = -380; _coffinCounts.OffsetRight = -10; _coffinCounts.OffsetTop = 132; _coffinCounts.OffsetBottom = 158;
        _states = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; root.AddChild(_states);
        _states.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft); _states.OffsetLeft = 290; _states.OffsetTop = -72;
        _bleedIcon = Icon("res://assets/user/icon/icon_bleed.PNG"); _states.AddChild(_bleedIcon);
        _poisonIcon = Icon("res://assets/user/icon/icon_poison.PNG"); _states.AddChild(_poisonIcon);
        _status = UiKit.Label("", 18); _states.AddChild(_status);
        _supplies = UiKit.Label("", 16); _supplies.Name = "SupplyCounts"; root.AddChild(_supplies); _supplies.Position = new Vector2(80, 360);
        var help = UiKit.Label("WASD 移动 · J/左键 攻击 · Shift/右键 冲刺 · F 交互 · B 背包 · M 地图 · R 换弹", 14);
        root.AddChild(help); help.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        help.OffsetLeft = -490; help.OffsetRight = 490; help.OffsetTop = -28; help.OffsetBottom = -4; help.HorizontalAlignment = HorizontalAlignment.Center;
        RefreshItemIcons(); RefreshCoffinCounts();
    }
    private static TextureRect Icon(string path) => new() { Texture = GD.Load<Texture2D>(path),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        CustomMinimumSize = new Vector2(30, 30), MouseFilter = Control.MouseFilterEnum.Ignore };
    public override void _Process(double delta)
    {
        _root.Visible = !Stop.IsPaused;
        if (!Stop.IsPaused && _player.hp > 0) ElapsedSeconds += delta;
        _bleedIcon.Visible = _state.IsBleeding; _poisonIcon.Visible = _state.IsPoisoned;
        _status.Text = (_state.IsBleeding ? "流血 · Q 止血 " : "") + (_state.IsPoisoned ? "中毒 · Z 解毒" : "");
        bool opened = GetParent().GetNode<开门机关>("开门机关").IsUnlocked;
        _objective.Text = opened ? "墓门已开启 · 前往右上方出口" : "寻找开门机关，携财宝逃出古墓";
        _stats.Text = $"攻击 {_player.EffectiveAttack:0.#}　攻速 {_player.ShotsPerSecond:0.#}　移速 {_player.EffectiveMoveSpeed:0}\n暴击 {_player.CriticalChance:P0}　探索 {(int)ElapsedSeconds / 60:00}:{(int)ElapsedSeconds % 60:00}";
        _supplies.Text = string.Join("\n\n", SupplyLabel(SupplyKind.Drug, "E 药品"),
            SupplyLabel(SupplyKind.Bandage, "Q 绷带"), SupplyLabel(SupplyKind.Antidote, "Z 解毒剂"));
        RefreshItemIcons(); RefreshCoffinCounts();
    }
    private void RefreshItemIcons()
    {
        foreach (var (kind, slot) in _supplySlots) slot.Visible = _pack.GetCount(kind) > 0;
        foreach (var (id, icon) in _itemIcons)
            icon.Visible = id switch
            {
                1001 => _soul.GetSoul() > 0,
                1002 => _gold.Amount > 0,
                1005 => _pack.GetCount(SupplyKind.Bullet) > 0 || _pack.GetReserveBullet() > 0,
                _ => _pack.GetCount(GameData.Supply(id)) > 0
            };
    }
    private string SupplyLabel(SupplyKind kind, string prefix)
    {
        int count = _pack.GetCount(kind);
        return count > 0 ? $"{prefix} {count}" : "";
    }
    private void RefreshCoffinCounts()
    {
        int total = 0, unopened = 0, lootable = 0;
        foreach (Node node in GetTree().GetNodesInGroup("interactable"))
        {
            if (node is not Coffin coffin || coffin.IsQueuedForDeletion()) continue;
            total++;
            if (coffin.Status == Coffin.CoffinState.Closed) unopened++;
            else if (coffin.Status == Coffin.CoffinState.Unlocked) lootable++;
        }
        _coffinCounts.Text = $"棺材总数 {total}　未开 {unopened}　可摸 {lootable}";
    }
}
