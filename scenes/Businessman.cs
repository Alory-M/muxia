using Godot;

public partial class Businessman : Area2D, IWorldInteractable
{
    public bool CanInteract => true;
    public string InteractionName => GameData.Text("monster_busin");
    public string InteractionText => GameData.Text("text_busin2");
    public string GetDialogueText() => GameData.RandomText("text_busin", "text_busin2");
    public string ActionText => "进入商店";
    public float InteractionRadius => 145f;
    public override void _Ready()
    {
        AddToGroup("interactable");
        var hint = GetNodeOrNull<Button>("get");
        if (hint != null) hint.Visible = false;
    }
    public void Interact(Player player)
    {
        var ui = GetTree().GetFirstNodeInGroup("store_ui");
        ui?.GetNodeOrNull<Store>("store")?.SetOpen(true);
    }
}
