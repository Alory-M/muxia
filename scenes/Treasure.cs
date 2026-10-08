using Godot;

/// <summary>开箱确认后一次性结算；财宝使用 2004 掉落表，额外补给参数独立可调。</summary>
public partial class Treasure : Area2D, IWorldInteractable
{
    [Export] public int BonusBullets { get; set; } = 12;
    [Export] public int BonusSupplies { get; set; } = 1;
    private bool _collected;
    private AudioStreamPlayer _audio;
    public bool CanInteract => !_collected;
    public string InteractionName => "宝箱";
    public string InteractionText => GameData.Text("text_box1");
    public string ActionText => "打开宝箱";
    public float InteractionRadius => 110f;
    public override void _Ready()
    {
        AddToGroup("interactable"); AddToGroup("world_event");
        GetNode<Button>("get").Visible = false;
        _audio = new AudioStreamPlayer { Stream = GD.Load<AudioStream>("res://music/treasure_box_open.wav") };
        AddChild(_audio);
        GetNode<Timer>("Timer").Timeout += QueueFree;
        Stop.RegisterWorldNode(this);
    }
    public void Interact(Player player)
    {
        if (_collected || player.hp <= 0) return;
        if (GetNode<Gift>("gift").Drop() != true) return;
        _collected = true;
        var pack = player.GetNode<Pack>("pack");
        pack.AddBullet(BonusBullets);
        var supplies = new[] { SupplyKind.Drug, SupplyKind.Bandage, SupplyKind.Antidote };
        var kind = supplies[GD.RandRange(0, supplies.Length - 1)];
        pack.Add(kind, BonusSupplies);
        InteractionController.Notify($"获得宝箱财宝，备用子弹 +{BonusBullets}，{kind switch { SupplyKind.Drug => "药品", SupplyKind.Bandage => "绷带", _ => "解毒剂" }} +{BonusSupplies}");
        GetNode<AnimatedSprite2D>("AnimatedSprite2D").Play("open");
        _audio.Play(); GetNode<Timer>("Timer").Start(1.2);
    }
}
