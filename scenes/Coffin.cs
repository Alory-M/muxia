using Godot;

/// <summary>封闭→战斗→可摸棺→已摸棺。靠近只显示提示，F 确认才开棺。</summary>
public partial class Coffin : Area2D, IWorldInteractable
{
    [Signal] public delegate void PlayerEnteredEventHandler(Node2D player);
    public enum CoffinState { Closed, Fighting, Unlocked, Looted }
    public CoffinState Status { get; private set; }
    private Zombie _guardian;
    private Gift _gift;
    private Sprite2D _closed, _open;
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
        _closed.Visible = true;
        _open.Visible = false;
        _lootGlow = new ShaderMaterial { Shader = GD.Load<Shader>("res://scenes/coffin_loot_glow.gdshader") };
        _open.Material = _lootGlow;
        RefreshLootGlow();
    }
    public bool CanInteract => Status == CoffinState.Closed || Status == CoffinState.Unlocked;
    public string InteractionName => Status == CoffinState.Unlocked ? "摸棺" : "棺材";
    public string InteractionText => GameData.Text(Status == CoffinState.Unlocked ? "text_mo1" : "text_coffin1");
    public string GetDialogueText() => GameData.RandomText(Status == CoffinState.Unlocked ? "text_mo" : "text_coffin");
    public string ActionText => Status == CoffinState.Unlocked ? "摸棺" : "打开棺材";
    public float InteractionRadius => 110f;
    public void Interact(Player player)
    {
        if (!CanInteract || player.hp <= 0) return;
        if (Status == CoffinState.Unlocked)
        {
            if (_gift?.Drop() == true)
            {
                Status = CoffinState.Looted;
                RefreshLootGlow();
            }
            return;
        }
        Status = _guardian == null ? CoffinState.Unlocked : CoffinState.Fighting;
        _closed.Visible = false;
        _open.Visible = true;
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
        LootGlowStrength = LootGlowEnabled ? 0.36f + Mathf.Sin(_glowTime * 2.2f) * 0.07f : 0f;
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
