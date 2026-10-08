using Godot;

/// <summary>目标、物资快捷键、独立状态图标、本局计时与属性显示。</summary>
public partial class ExpeditionHud : CanvasLayer
{
    private Player _player;
    private State _state;
    private Label _status, _objective, _stats, _supplies;
    private HBoxContainer _states;
    private Control _root;
    private TextureRect _bleedIcon, _poisonIcon;
    public double ElapsedSeconds { get; private set; }
    public override void _Ready()
    {
        Layer = 1;
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        _state = _player.GetNode<State>("state");
        var root = new Control { Theme = UiKit.Theme(), MouseFilter = Control.MouseFilterEnum.Ignore };
        _root = root;
        AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _objective = UiKit.Label("寻找开门机关，携财宝逃出古墓", 18); root.AddChild(_objective);
        _objective.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _objective.OffsetLeft = -280; _objective.OffsetRight = 280; _objective.OffsetTop = 8; _objective.OffsetBottom = 40; _objective.HorizontalAlignment = HorizontalAlignment.Center;
        _stats = UiKit.Label("", 16); root.AddChild(_stats);
        _stats.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight); _stats.OffsetLeft = -380; _stats.OffsetRight = -10; _stats.OffsetTop = 75; _stats.OffsetBottom = 125;
        _states = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; root.AddChild(_states);
        _states.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft); _states.OffsetLeft = 290; _states.OffsetTop = -72;
        _bleedIcon = Icon("res://assets/user/icon/icon_bleed.PNG"); _states.AddChild(_bleedIcon);
        _poisonIcon = Icon("res://assets/user/icon/icon_poison.PNG"); _states.AddChild(_poisonIcon);
        _status = UiKit.Label("", 18); _states.AddChild(_status);
        _supplies = UiKit.Label("", 16); root.AddChild(_supplies); _supplies.Position = new Vector2(80, 360);
        var help = UiKit.Label("WASD 移动 · J/左键 攻击 · Shift/右键 冲刺 · F 交互 · B 背包 · M 地图 · R 换弹", 14);
        root.AddChild(help); help.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        help.OffsetLeft = -490; help.OffsetRight = 490; help.OffsetTop = -28; help.OffsetBottom = -4; help.HorizontalAlignment = HorizontalAlignment.Center;
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
        var pack = _player.GetNode<Pack>("pack");
        _supplies.Text = $"E 药品 {pack.GetCount(SupplyKind.Drug)}\n\nQ 绷带 {pack.GetCount(SupplyKind.Bandage)}\n\nZ 解毒剂 {pack.GetCount(SupplyKind.Antidote)}";
    }
}
