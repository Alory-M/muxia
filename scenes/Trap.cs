using Godot;

/// <summary>踩中机关启动：地刺伤害所有重叠单位；暗箭十连发伤害路径上的双方。</summary>
public partial class Trap : Area2D
{
    [Export] public int MechanismId { get; set; } = 1;
    [Export] public Vector2 ArrowOrigin { get; set; } = new Vector2(-160, 0);
    [Export] public Vector2 ArrowDirection { get; set; } = Vector2.Right;
    private float _cooldown, _remaining, _burstTimer, _spikeTime;
    private int _damage, _arrows;
    private Sprite2D _spikes;
    private AudioStreamPlayer2D _sfx;
    public int ShotsFired { get; private set; }
    public override void _Ready()
    {
        AddToGroup("world_event");
        var row = GameData.Row("mechan", MechanismId);
        _damage = (int)GameData.Number(row, "hit"); _cooldown = GameData.Number(row, "hittime");
        _spikes = GetNode<Sprite2D>("Spikes"); _spikes.Visible = false;
        _sfx = new AudioStreamPlayer2D { Stream = GD.Load<AudioStream>(MechanismId == 1 ? "res://music/step_on_mechanism.wav" : "res://music/arrows_many.wav"), VolumeDb = -14f };
        AddChild(_sfx); _sfx.Bus = "Sfx";
        BodyEntered += OnBodyEntered;
        Stop.RegisterWorldNode(this);
    }
    private void OnBodyEntered(Node2D body)
    {
        if (body is not Player && body is not Zombie) return;
        if (body is Zombie zombie && !zombie.IsActive) return;
        if (MechanismId == 1 && _spikeTime > 0) Damage(body);
        else Activate();
    }
    public bool Activate()
    {
        if (Stop.IsPaused || _remaining > 0) return false;
        _remaining = _cooldown; _sfx.Play();
        if (MechanismId == 1)
        {
            _spikeTime = 0.8f; _spikes.Visible = true;
            foreach (Node2D body in GetOverlappingBodies()) Damage(body);
        }
        else { _arrows = 10; _burstTimer = 0; }
        return true;
    }
    private void Damage(Node2D body)
    {
        if (body is Player player) player.TakeDamage(_damage);
        else if (body is Zombie zombie) zombie.take_damage(_damage);
    }
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta; _remaining = Mathf.Max(0, _remaining - dt);
        if (_spikeTime > 0)
        {
            _spikeTime -= dt;
            if (_spikeTime <= 0) _spikes.Visible = false;
        }
        if (_arrows <= 0) return;
        _burstTimer -= dt;
        if (_burstTimer > 0) return;
        _arrows--; _burstTimer = 0.12f;
        var bullet = GD.Load<PackedScene>("res://scenes/trap_arrow.tscn").Instantiate<Bullet>();
        bullet.HitsEveryone = true; bullet.Damage = _damage; bullet.Speed = 400;
        GetTree().CurrentScene.AddChild(bullet);
        bullet.GlobalPosition = GlobalPosition + ArrowOrigin;
        bullet.Launch(ArrowDirection); ShotsFired++;
    }
}
