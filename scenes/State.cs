using Godot;

public enum PlayerState { Normal, Bleed, Slow }

/// <summary>流血和中毒独立存在，分别被绷带与解毒剂清除。</summary>
public partial class State : Node
{
    [Signal] public delegate void StateChangedEventHandler(int previous, int current);
    [Export] public float BleedPerSecond { get; set; } = 5f;
    [Export] public float BleedDuration { get; set; } = 0f;
    [Export] public float SlowDuration { get; set; } = 0f;
    [Export] public int HealAmount { get; set; } = 100;
    [Export] public float PoisonDamagePerSecond { get; set; } = 5f;
    private Player _player;
    private float _bleedTime, _poisonTime, _bleedTick, _poisonTick;
    private float _interval = 1f;
    private bool _paused;
    public bool IsBleeding { get; private set; }
    public bool IsPoisoned { get; private set; }
    public float PoisonSpeedReduction { get; private set; }
    public float PoisonAttackReduction { get; private set; }
    public PlayerState Current => IsPoisoned ? PlayerState.Slow : IsBleeding ? PlayerState.Bleed : PlayerState.Normal;
    public override void _Ready()
    {
        _player = GetParent() as Player;
        var bleed = GameData.Row("status", 1).GetProperty("arguments").GetString().Split(',');
        _interval = float.Parse(bleed[0], System.Globalization.CultureInfo.InvariantCulture);
        BleedPerSecond = float.Parse(bleed[1], System.Globalization.CultureInfo.InvariantCulture);
        var poison = GameData.Row("status", 2).GetProperty("arguments").GetString().Split(',');
        PoisonSpeedReduction = float.Parse(poison[0], System.Globalization.CultureInfo.InvariantCulture) * GameData.SpeedUnit;
        PoisonAttackReduction = float.Parse(poison[1], System.Globalization.CultureInfo.InvariantCulture);
    }
    public void ChangeState(PlayerState next)
    {
        var old = Current;
        switch (next)
        {
            case PlayerState.Normal:
                IsBleeding = IsPoisoned = false; _bleedTick = _poisonTick = 0;
                _player?.StopStatusSounds(); break;
            case PlayerState.Bleed:
                if (!IsBleeding) _bleedTick = 0;
                IsBleeding = true; _bleedTime = BleedDuration; break;
            case PlayerState.Slow:
                if (!IsPoisoned) _poisonTick = 0;
                IsPoisoned = true; _poisonTime = SlowDuration; break;
        }
        EmitSignal(SignalName.StateChanged, (int)old, (int)Current);
    }
    public bool HasState(PlayerState kind) => kind == PlayerState.Bleed ? IsBleeding : kind == PlayerState.Slow ? IsPoisoned : !IsBleeding && !IsPoisoned;
    public void ClearState(PlayerState kind)
    {
        var old = Current;
        if (kind == PlayerState.Bleed) { IsBleeding = false; _bleedTick = 0; _player?.StopStatusSound(kind); }
        if (kind == PlayerState.Slow) { IsPoisoned = false; _poisonTick = 0; _player?.StopStatusSound(kind); }
        EmitSignal(SignalName.StateChanged, (int)old, (int)Current);
    }
    public void ResetToNormal() => ChangeState(PlayerState.Normal);
    public void SetDebuffsPaused(bool paused)
    {
        _paused = paused;
        if (paused) _player?.StopStatusSounds();
    }
    public override void _Process(double delta)
    {
        if (_paused || Stop.IsPaused || _player == null || _player.hp <= 0) return;
        float dt = (float)delta;
        Tick(ref _bleedTick, ref _bleedTime, IsBleeding, BleedDuration, _interval, BleedPerSecond, PlayerState.Bleed, dt);
        Tick(ref _poisonTick, ref _poisonTime, IsPoisoned, SlowDuration, 1f, PoisonDamagePerSecond, PlayerState.Slow, dt);
    }
    private void Tick(ref float elapsed, ref float remaining, bool active, float duration, float interval, float damage, PlayerState kind, float dt)
    {
        if (!active) return;
        elapsed += duration > 0 ? Mathf.Min(dt, remaining) : dt;
        while (elapsed >= interval && _player.hp > 0)
        {
            elapsed -= interval;
            _player.TakeStatusDamage(damage, kind);
        }
        if (duration > 0 && (remaining -= dt) <= 0) ClearState(kind);
    }
    public static string NameOf(PlayerState state) => state switch { PlayerState.Bleed => "流血", PlayerState.Slow => "中毒", _ => "正常" };
}
