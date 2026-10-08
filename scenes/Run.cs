using Godot;

/// <summary>行走、定向射击和受击的视觉状态；仅改变精灵，不移动人物碰撞体。</summary>
public partial class Run : AnimatedSprite2D
{
    private static readonly StringName SideAnim = "侧跑";
    private static readonly StringName BackAnim = "背跑";
    private static readonly StringName FrontAnim = "正跑";
    private static readonly StringName IdleAnim = "站立";
    private static readonly StringName AttackRight = "攻击_右";
    private static readonly StringName AttackLeft = "攻击_左";
    private static readonly StringName AttackBack = "攻击_背";
    private static readonly StringName AttackFront = "攻击_正";
    private static readonly StringName HurtAnim = "受击";
    private const float HurtDuration = 0.24f;
    [Export] public float DashSpeedMultiplier { get; set; } = 2f;
    private Player _player;
    private bool _facingLeft;
    private Vector2 _restPosition, _restScale;
    private Color _restColor;
    private float _attackTime, _attackDuration, _hurtTime;
    private Vector2 _aimDirection = Vector2.Right;
    private StringName _attackAnim = AttackRight;
    private Polygon2D _muzzleFlash;
    public bool IsAttacking => _attackTime > 0;
    public bool IsHurting => _hurtTime > 0;

    public override void _Ready()
    {
        _player = GetParent() as Player;
        _restPosition = Position;
        _restScale = Scale;
        _restColor = Modulate;
        if (_player == null) GD.PushWarning("Run: 父节点不是 Player。");
        // 每个人物拥有独立动画资源，避免运行时追加动作修改场景共享资源。
        SpriteFrames = SpriteFrames?.Duplicate() as SpriteFrames ?? new SpriteFrames();
        AddPoseAnimation(AttackRight, "res://assets/user/host1/host_gun_ce.png", 0.18f);
        AddPoseAnimation(AttackLeft, "res://assets/user/host1/host_gun_ce2.png", 0.18f);
        AddPoseAnimation(AttackBack, "res://assets/user/host1/host_gun_back.png", 0.18f);
        AddPoseAnimation(AttackFront, "res://assets/user/host1/host_gun_zheng.png", 0.18f);
        AddPoseAnimation(HurtAnim, "res://assets/user/host1/host_injured.png", HurtDuration);
        _muzzleFlash = new Polygon2D
        {
            Name = "MuzzleFlash", Visible = false,
            Polygon = new[] { Vector2.Zero, new Vector2(6, -3), new Vector2(4, -8), new Vector2(11, -4),
                new Vector2(18, 0), new Vector2(10, 3), new Vector2(5, 7), new Vector2(6, 2) },
            Color = new Color(1f, 0.88f, 0.42f), Scale = Vector2.One / _restScale
        };
        AddChild(_muzzleFlash);
    }

    private void AddPoseAnimation(StringName name, string path, float duration)
    {
        var texture = GD.Load<Texture2D>(path);
        if (texture == null) return;
        if (SpriteFrames.HasAnimation(name)) SpriteFrames.RemoveAnimation(name);
        SpriteFrames.AddAnimation(name);
        SpriteFrames.SetAnimationLoopMode(name, SpriteFrames.LoopMode.None);
        SpriteFrames.SetAnimationSpeed(name, 4f / duration);
        for (int frame = 0; frame < 4; frame++) SpriteFrames.AddFrame(name, texture);
    }

    /// <summary>只在成功射出子弹时调用；按射击方向选原画，并播放后坐和短枪口火光。</summary>
    public void PlayAttack(Vector2 direction)
    {
        if (global::Stop.IsPaused || _player == null || _player.hp <= 0 || direction.IsZeroApprox()) return;
        _aimDirection = direction.Normalized();
        _facingLeft = direction.X < 0;
        _attackAnim = Mathf.Abs(direction.Y) > Mathf.Abs(direction.X)
            ? direction.Y < 0 ? AttackBack : AttackFront
            : _facingLeft ? AttackLeft : AttackRight;
        _attackDuration = Mathf.Min(0.18f, 0.9f / Mathf.Max(0.1f, _player.ShotsPerSecond));
        _attackTime = _attackDuration;
        if (!IsHurting) ShowAttack();
    }

    /// <summary>直接伤害播放受伤原画与短红闪；持续流血/中毒不反复打断人物动作。</summary>
    public void PlayHurt()
    {
        _hurtTime = HurtDuration;
        _attackTime = 0;
        _muzzleFlash.Visible = false;
        ShowHurt();
    }

    public override void _Process(double delta)
    {
        if (global::Stop.IsPaused) return;
        float dt = (float)delta;
        _attackTime = Mathf.Max(0, _attackTime - dt);
        _hurtTime = Mathf.Max(0, _hurtTime - dt);
        if (_player?.hp <= 0)
        {
            _attackTime = 0;
            _muzzleFlash.Visible = false;
            return;
        }
        if (IsHurting) { ShowHurt(); return; }
        if (IsAttacking) { ShowAttack(); return; }
        RestorePose();
        SpeedScale = _player?.IsDashing == true ? DashSpeedMultiplier : 1f;
        bool left = Input.IsActionPressed("move_left"), right = Input.IsActionPressed("move_right");
        if (left || right)
        {
            if (left != right) _facingLeft = left;
            FlipH = _facingLeft;
            PlayIfNot(SideAnim);
        }
        else if (Input.IsActionPressed("move_up")) PlayFlat(BackAnim);
        else if (Input.IsActionPressed("move_down")) PlayFlat(FrontAnim);
        else PlayFlat(IdleAnim);
    }

    private void ShowAttack()
    {
        FlipH = false; // 左右各使用自己的枪姿原图。
        SpeedScale = 0.18f / _attackDuration;
        PlayIfNot(_attackAnim);
        float progress = 1f - _attackTime / _attackDuration;
        float recoil = Mathf.Sin(Mathf.Min(1f, progress * 2f) * Mathf.Pi) * 3f;
        float centerOffset = _attackAnim == AttackRight ? 7f : _attackAnim == AttackLeft ? -7f : 0f;
        Position = _restPosition + new Vector2(centerOffset, -3) - _aimDirection * recoil;
        Scale = _restScale * 0.84f;
        Rotation = 0;
        Modulate = _restColor;
        // 按每张原画的实际枪口定位，不能把正面/背面枪姿的火光放在头顶或脚边。
        Vector2 muzzlePixel = _attackAnim == AttackRight ? new Vector2(1190, 330)
            : _attackAnim == AttackLeft ? new Vector2(50, 330)
            : _attackAnim == AttackBack ? new Vector2(1030, 244) : new Vector2(608, 355);
        _muzzleFlash.Position = muzzlePixel - SpriteFrames.GetFrameTexture(_attackAnim, 0).GetSize() * 0.5f;
        _muzzleFlash.Scale = Vector2.One / Scale;
        _muzzleFlash.Rotation = _aimDirection.Angle();
        _muzzleFlash.Visible = progress < 0.35f;
    }

    private void ShowHurt()
    {
        FlipH = _facingLeft;
        SpeedScale = 1f;
        PlayIfNot(HurtAnim);
        float progress = 1f - _hurtTime / HurtDuration;
        Position = _restPosition + new Vector2(Mathf.Sin(progress * Mathf.Pi * 8f) * 2f * (1f - progress), -2);
        Scale = _restScale * 0.86f;
        Rotation = Mathf.Sin(progress * Mathf.Pi) * 0.04f;
        Modulate = new Color(1f, 0.45f + progress * 0.55f, 0.45f + progress * 0.55f, _restColor.A);
        _muzzleFlash.Visible = false;
    }

    private void RestorePose()
    {
        Position = _restPosition;
        Scale = _restScale;
        Rotation = 0;
        Modulate = _restColor;
        _muzzleFlash.Visible = false;
    }

    private void PlayFlat(StringName animation) { FlipH = false; PlayIfNot(animation); }
    private void PlayIfNot(StringName animation)
    {
        if (Animation != animation || !IsPlaying()) Play(animation);
    }
}
