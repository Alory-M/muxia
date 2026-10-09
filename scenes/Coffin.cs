using Godot;

/// <summary>封闭→战斗→可摸棺→已摸棺。靠近自动开棺，F 仅用于战后摸棺。</summary>
public partial class Coffin : Area2D, IWorldInteractable
{
    [Signal] public delegate void PlayerEnteredEventHandler(Node2D player);
    public enum CoffinState { Closed, Fighting, Unlocked, Looted }
    public CoffinState Status { get; private set; }
    private Zombie _guardian;
    private Gift _gift;
    private Sprite2D _closed, _open;
    private Player _player;
    private StaticBody2D _solid;
    private ShaderMaterial _lootGlow;
    private float _glowTime;
    public bool LootGlowEnabled => Status == CoffinState.Unlocked;
    public float LootGlowStrength { get; private set; }
    public override void _Ready()
    {
        AddToGroup("interactable");
        _gift = GetNodeOrNull<Gift>("gift");
        foreach (Node node in GetChildren())
            if (node is Zombie zombie) { _guardian = zombie; zombie.Died += Unlock; break; }
        _closed = GetNode<Sprite2D>("closed");
        _open = GetNode<Sprite2D>("open");
        _solid = GetNodeOrNull<StaticBody2D>("SolidBody");
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        _closed.Visible = true;
        _open.Visible = false;
        _lootGlow = new ShaderMaterial { Shader = GD.Load<Shader>("res://scenes/coffin_loot_glow.gdshader") };
        _lootGlow.SetShaderParameter("glow_color", new Color(0.55f, 0.94f, 1f));
        _open.Material = _lootGlow;
        RefreshLootGlow();
    }
    public bool CanInteract => Status == CoffinState.Unlocked;
    public string InteractionName => "摸棺";
    public string InteractionText => GameData.Text("text_mo1");
    public string GetDialogueText() => GameData.RandomText("text_mo");
    public string ActionText => "摸棺";
    public float InteractionRadius => 110f;
    public void Interact(Player player)
    {
        if (!CanInteract || player.hp <= 0) return;
        if (_gift?.Drop() == true)
        {
            Status = CoffinState.Looted;
            RefreshLootGlow();
        }
    }
    public override void _PhysicsProcess(double delta)
    {
        if (Status != CoffinState.Closed || Stop.IsPaused) return;
        if (!GodotObject.IsInstanceValid(_player))
            _player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (_player == null || _player.hp <= 0 ||
            GlobalPosition.DistanceSquaredTo(_player.GlobalPosition) > InteractionRadius * InteractionRadius) return;
        // 隔墙经过另一间墓室不触发开棺；棺材自己的实体不挡住这条视线。
        var exclusions = new Godot.Collections.Array<Rid> { _player.GetRid() };
        if (_solid != null) exclusions.Add(_solid.GetRid());
        var ray = PhysicsRayQueryParameters2D.Create(_player.GlobalPosition, GlobalPosition, 1, exclusions);
        if (GetWorld2D().DirectSpaceState.IntersectRay(ray).Count > 0) return;
        Open(_player);
    }
    private void Open(Player player)
    {
        Status = _guardian == null ? CoffinState.Unlocked : CoffinState.Fighting;
        _closed.Visible = false;
        _open.Visible = true;
        if (_solid != null)
        {
            // 在发出守卫信号前移除碰撞层，出棺位置查询和本帧玩家移动立即可穿过。
            // 形状的禁用延后到安全时机，避免物理服务器刷新期间修改形状。
            _solid.CollisionLayer = 0;
            _solid.CollisionMask = 0;
            foreach (Node child in _solid.GetChildren())
                if (child is CollisionShape2D shape) shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        }
        RefreshLootGlow();
        GetNodeOrNull<AudioStreamPlayer2D>("coffin_open")?.Play();
        EmitSignal(SignalName.PlayerEntered, player);
    }
    private void Unlock()
    {
        Status = CoffinState.Unlocked;
        _glowTime = 0;
        RefreshLootGlow();
    }
    private void RefreshLootGlow()
    {
        LootGlowStrength = LootGlowEnabled ? 0.78f + Mathf.Sin(_glowTime * 2.2f) * 0.10f : 0f;
        _lootGlow?.SetShaderParameter("glow_strength", LootGlowStrength);
    }
    public override void _Process(double delta)
    {
        if (!LootGlowEnabled || Stop.IsPaused) return;
        _glowTime += (float)delta;
        RefreshLootGlow();
    }
    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_guardian)) _guardian.Died -= Unlock;
        var audio = GetNodeOrNull<AudioStreamPlayer2D>("coffin_open");
        if (audio != null) { audio.Stop(); audio.Stream = null; }
        if (GodotObject.IsInstanceValid(_open)) _open.Material = null;
        _lootGlow = null;
    }
}
