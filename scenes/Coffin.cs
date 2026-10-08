using Godot;

/// <summary>封闭→战斗→可摸棺→已摸棺。靠近只显示提示，F 确认才开棺。</summary>
public partial class Coffin : Area2D, IWorldInteractable
{
    [Signal] public delegate void PlayerEnteredEventHandler(Node2D player);
    public enum CoffinState { Closed, Fighting, Unlocked, Looted }
    public CoffinState Status { get; private set; }
    private Zombie _guardian;
    private Gift _gift;
    public override void _Ready()
    {
        AddToGroup("interactable");
        _gift = GetNodeOrNull<Gift>("gift");
        foreach (Node node in GetChildren())
            if (node is Zombie zombie) { _guardian = zombie; zombie.Died += Unlock; break; }
        GetNode<Sprite2D>("closed").Visible = true;
        GetNode<Sprite2D>("open").Visible = false;
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
            if (_gift?.Drop() == true) Status = CoffinState.Looted;
            return;
        }
        Status = _guardian == null ? CoffinState.Unlocked : CoffinState.Fighting;
        GetNode<Sprite2D>("closed").Visible = false;
        GetNode<Sprite2D>("open").Visible = true;
        GetNodeOrNull<AudioStreamPlayer2D>("coffin_open")?.Play();
        EmitSignal(SignalName.PlayerEntered, player);
    }
    private void Unlock() => Status = CoffinState.Unlocked;
}
