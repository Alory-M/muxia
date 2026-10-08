using Godot;

public partial class Player : CharacterBody2D
{
	// 移动速度(像素/秒)
	[Export]
	private float moveSpeed = 500f;

	// 改成 public 供 Therapy / ColorRect 读写
	[Export]
	public float hp = 100f;

	// 血量上限。回血封顶、血条比例都以它为准,别再在别处写死 100
	[Export]
	public float MaxHp = 100f;

	// 要发射的子弹场景,在检查器里指定 res://scenes/bullet.tscn
	[Export]
	private PackedScene bulletScene;

	// 冲刺距离(像素)—— 外部可调
	[Export]
	private float dashDistance = 200f;

	// 这段距离用多长时间跑完(秒),越小冲得越快
	[Export]
	private float dashDuration = 0.15f;

	private Vector2 _dashDirection = Vector2.Zero;
	private float _dashRemaining = 0f;         // 本次冲刺还剩多少像素
    private float _blockedMoveTime;

	// 背包。子弹 / 药 / 绷带 / 解毒剂的数量都在子节点 pack(Pack.cs)里,
	// 这里不再自己存一份,要用就走 _pack
	private Pack _pack;
    private State _state;
    private Run _visual;
    private float _shotCooldown;
    private float _attack = 5f, _shotsPerSecond = 5f, _attackDistance = 160f;
    private float _attackBonus, _speedBonus, _fireBonus, _critical;
    public float EffectiveMoveSpeed => Mathf.Max(20f, moveSpeed * (1f + _speedBonus) - (_state?.IsPoisoned == true ? _state.PoisonSpeedReduction : 0f));
    public float EffectiveAttack => Mathf.Max(1f, _attack * (1f + _attackBonus) - (_state?.IsPoisoned == true ? _state.PoisonAttackReduction : 0f));
    public float ShotsPerSecond => _shotsPerSecond * (1f + _fireBonus);
    public float CriticalChance => Mathf.Clamp(_critical, 0f, 1f);
    public void ApplyBuff(int id, int quantity = 1)
    {
        float value = GameData.Number(GameData.Row("buff", id), "add") * quantity;
        switch (id)
        {
            case 3001: _attackBonus += value; break;
            case 3002: _speedBonus += value; break;
            case 3003: _fireBonus += value; break;
            case 3004: _critical = Mathf.Min(1f, _critical + value); break;
        }
    }

	// 冲刺中?Run 之类的子节点要据此换表现(比如把跑步动画放快),
	// 所以对外公开,不让人去反射读 _dashRemaining
	public bool IsDashing => _dashRemaining > 0f;

	// 冲刺速度由"距离 / 时间"推出来
	private float DashSpeed => dashDistance / Mathf.Max(dashDuration, 0.0001f);

	// 这一步实际走的距离不足计划距离的这个比例,就认定是被碰撞箱正面挡住了,
	// 而不是"贴着墙滑"。0.5 表示被吃掉一半以上才算撞墙。
	private const float BlockedStepFactor = 0.5f;

	// 走路音效:玩家真的在动(坐标变化)就循环放,停下就停
	private AudioStreamPlayer _walkPlayer;
	private Vector2 _lastPosition; // 上一帧位置,用来判断玩家是否真的移动了

	// 枪声 / 受伤音效:射击和受击时各播一次(单次,不循环)
	private AudioStreamPlayer _gunShotPlayer;
	private AudioStreamPlayer _hurtPlayer;
    private AudioStreamPlayer _bleedPlayer;
    private AudioStreamPlayer _poisonPlayer;

	public override void _Ready()
	{
		var stats = GameData.Row("host", 1);
        MaxHp = hp = GameData.Number(stats, "blood");
        // 保留第一版场景导出的移速（500 px/s），不再用 host 表覆盖操作手感。
        _attack = GameData.Number(stats, "hit");
        _shotsPerSecond = GameData.Number(stats, "hitspeed");
        _attackDistance = GameData.Number(stats, "hitdistance") * GameData.DistanceUnit;
        _critical = GameData.Number(stats, "critical");
        _state = GetNodeOrNull<State>("state");
        _visual = GetNodeOrNull<Run>("run");
        _pack = GetNodeOrNull<Pack>("pack");
        Stop.RegisterWorldNode(this);
		if (_pack == null)
		{
			GD.PushWarning("Player: 找不到子节点 pack,拿不到子弹数量,开不了枪。");
		}

		SetupWalkAudio();
		SetupCombatAudio();
		_lastPosition = GlobalPosition;
	}

	// 创建走路音效播放器并加载 walk.wav,设成循环
	private void SetupWalkAudio()
	{
		_walkPlayer = new AudioStreamPlayer { Name = "FootstepSfx", Bus = "Sfx" };
		AddChild(_walkPlayer);

		AudioStreamWav stream = GD.Load<AudioStreamWav>("res://music/walk.wav")?.Duplicate() as AudioStreamWav;
		if (stream == null)
		{
			GD.PushWarning("Player: 找不到音频 res://music/walk.wav");
			return;
		}
		stream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
		_walkPlayer.Stream = stream;
	}

	// 创建枪声 / 受伤音效播放器并加载音频
	private void SetupCombatAudio()
	{
		_gunShotPlayer = CreateSfxPlayer("gun_shot.wav", "GunshotSfx");
		_hurtPlayer = CreateSfxPlayer("host_be_attacked.wav", "DirectHitSfx");
        // 持续伤害不再每秒重播完整的受击喊声；两个短音各自只有一个声部。
        _bleedPlayer = CreateSfxPlayer("status_bleed.wav", "BleedStatusSfx", -22f);
        _poisonPlayer = CreateSfxPlayer("status_poison.wav", "PoisonStatusSfx", -24f);
	}

	// 创建一个挂在玩家身上的音效播放器,加载 music/ 下的音频文件
	private AudioStreamPlayer CreateSfxPlayer(string fileName, string name, float volumeDb = 0f)
	{
		AudioStreamPlayer player = new AudioStreamPlayer { Name = name, Bus = "Sfx", VolumeDb = volumeDb, MaxPolyphony = 1 };
		AddChild(player);

		AudioStream stream = GD.Load<AudioStream>($"res://music/{fileName}");
		if (stream == null)
		{
			GD.PushWarning($"Player: 找不到音频 res://music/{fileName}");
			return player;
		}
		player.Stream = stream;
		return player;
	}

	// 播放指定音效;音频没加载到就静默跳过
	private void PlaySfx(AudioStreamPlayer player)
	{
		if (player != null && player.Stream != null)
		{
			player.Play();
		}
	}

	// 根据玩家这帧有没有真的移动(坐标变化),决定走路音效要不要继续放
	private void UpdateWalkSound()
	{
		if (_walkPlayer.Stream == null)
		{
			return; // 音频没加载到,什么都不放
		}

		Vector2 now = GlobalPosition;
		bool moved = (now - _lastPosition).LengthSquared() > 0.0001f;
		_lastPosition = now;

		if (moved)
		{
			if (!_walkPlayer.Playing)
			{
				_walkPlayer.Play();
			}
		}
		else if (_walkPlayer.Playing)
		{
			_walkPlayer.Stop();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Stop.IsPaused || hp <= 0) return;
		float dt = (float)delta;
        _shotCooldown = Mathf.Max(0, _shotCooldown - dt);

		// 冲刺中:无视常规移动,沿冲刺方向前进
		if (IsDashing && dt > 0f)
		{
			Vector2 before = GlobalPosition;

			// 最后一步做了限制,不会冲过头
			float step = Mathf.Min(DashSpeed * dt, _dashRemaining);
			Velocity = _dashDirection * (step / dt);
			MoveAndSlide();

			// 按"实际移动了多少"扣减:撞墙时冲刺会提前结束
			float moved = (GlobalPosition - before).Length();
			_dashRemaining -= moved;
            RequestCrowdingRelief(_dashDirection, step, before, delta);

			// 结束冲刺的两种情况:
			// 1) 距离冲完了(浮点误差下剩的那一丁点直接抹掉);
			// 2) 这一步几乎没走成 —— 被碰撞箱正面挡死了。
			// 第 2 条是必须的:垂直撞上水平碰撞箱时,MoveAndSlide 的滑动分量正好是 0
			// (motion - normal * (motion·normal) = (0,-1) - (0,1)*(-1) = 0),
			// moved 恒为 0,剩余距离一像素都扣不掉,冲刺就永远结束不了 ——
			// IsDashing 一直是 true,_PhysicsProcess 每次都在这里提前 return,
			// 人就被永久锁死(既走不动也冲不了),光靠抹掉浮点残值救不回来。
			// 只被吃掉一个分量时(比如 45° 蹭墙)moved 还占 step 的大头,照旧滑完。
			if (_dashRemaining <= 0.01f || moved < step * BlockedStepFactor)
			{
				_dashRemaining = 0f;
			}
			UpdateWalkSound();
			return;
		}

		// 常规移动:把 WASD 合成一个方向向量。
		// GetVector 会自动把长度裁到 1,所以斜向移动不会比直线更快
		Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");

		Velocity = direction * EffectiveMoveSpeed;
		Vector2 walkStart = GlobalPosition;
		MoveAndSlide();
        RequestCrowdingRelief(direction, EffectiveMoveSpeed * dt, walkStart, delta);

		UpdateWalkSound();
	}

    private void RequestCrowdingRelief(Vector2 direction, float expectedStep, Vector2 before, double delta)
    {
        float progress = direction.IsZeroApprox() ? 0 : (GlobalPosition - before).Dot(direction.Normalized());
        // A small sliding advance still means the player remains blocked.  Reset
        // the zombie relief budget only after nearly the whole requested step is
        // available; otherwise each partially blocked frame could grant another
        // full relief budget and push one zombie indefinitely down a corridor.
        if (direction.IsZeroApprox() || expectedStep <= 0)
        {
            _blockedMoveTime = 0;
            foreach (Node node in GetTree().GetNodesInGroup(Zombie.GroupName))
                if (node is Zombie zombie && zombie.GlobalPosition.DistanceTo(GlobalPosition) <= 150f)
                    zombie.ResetYieldBudget();
            return;
        }
        if (progress > expectedStep * 0.85f)
        {
            // The player may have slid around one frame while the zombie is
            // still close. Keep its local relief budget until that nearby
            // contact is gone; resetting on every successful slide would let
            // the same enemy be pushed without a distance bound.
            _blockedMoveTime = 0;
            bool nearbyZombie = false;
            foreach (Node node in GetTree().GetNodesInGroup(Zombie.GroupName))
                if (node is Zombie zombie && zombie.IsActive && zombie.GlobalPosition.DistanceTo(GlobalPosition) <= 150f)
                {
                    nearbyZombie = true;
                    break;
                }
            if (!nearbyZombie)
                foreach (Node node in GetTree().GetNodesInGroup(Zombie.GroupName))
                    if (node is Zombie zombie && zombie.GlobalPosition.DistanceTo(GlobalPosition) <= 180f)
                        zombie.ResetYieldBudget();
            return;
        }
        _blockedMoveTime += (float)delta;
        if (_blockedMoveTime < 0.1f) return;
        var own = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (own?.Shape == null || own.Disabled) return;
        // 只处理真正挡在当前输入路径上的敌人，撞墙不会牵动身后的敌人。
        foreach (Node node in GetTree().GetNodesInGroup(Zombie.GroupName))
        {
            if (node is not Zombie zombie || !zombie.IsActive ||
                zombie.GlobalPosition.DistanceTo(GlobalPosition) > 140f) continue;
            var target = zombie.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            if (target?.Shape == null || target.Disabled) continue;
            if (own.Shape.CollideWithMotion(own.GlobalTransform, direction.Normalized() * 30f,
                target.Shape, target.GlobalTransform, Vector2.Zero))
                zombie.TryYieldToPlayer(this, direction, delta);
        }
    }

	// 用 _UnhandledInput 而不是 _Process + IsActionJustPressed:
	// 输入事件是逐个投递的,两帧之内连点也不会漏掉
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Echo) return;
        if (Stop.IsPaused || hp <= 0) return;
		if (@event.IsActionPressed("mouse_press"))
		{
			TryShoot();
		}
		else if (@event.IsActionPressed("mouse_press2"))
		{
			StartDash();
		}
	}

	private void StartDash()
	{
		if (IsDashing)
		{
			return;   // 冲刺途中不能再冲
		}

		// 冲刺方向由玩家当前按着的方向决定。一个方向键都没按(站着不动)就不给冲——
		// 方向总得由玩家给出来,不能拿"上次朝哪"凑合
		Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		if (input.LengthSquared() < 0.0001f)
		{
			return;
		}

		_dashDirection = input.Normalized();
		_dashRemaining = dashDistance;
	}

	/// <summary>有子弹就打一发、扣一发;没子弹时什么都不做(和以前一样,不提示)</summary>
    public bool TryShoot()
    {
        return FireTowards(GetGlobalMousePosition() - GlobalPosition);
    }
    public bool FireTowards(Vector2 direction)
    {
        if (Stop.IsPaused || hp <= 0 || _shotCooldown > 0 || bulletScene == null || direction.LengthSquared() < 0.0001f) return false;
        if (_pack == null || !_pack.TryConsume(SupplyKind.Bullet)) return false;
        Bullet bullet = bulletScene.Instantiate<Bullet>();
        bullet.Damage = Mathf.Max(1, Mathf.RoundToInt(EffectiveAttack * (GD.Randf() < CriticalChance ? 2f : 1f)));
        GetParent().AddChild(bullet);
        bullet.GlobalPosition = GlobalPosition;
        bullet.GetNode<Disappear>("disappear").MaxDistance = _attackDistance;
        bullet.Launch(direction);
        _shotCooldown = 1f / Mathf.Max(0.1f, ShotsPerSecond);
        _visual?.PlayAttack(direction);
        PlaySfx(_gunShotPlayer);
        return true;
    }

	/// <summary>
	/// 受到伤害。供 GDScript 的僵尸脚本调用(对应 GDScript 里的 take_damage)。
	/// 血量扣到 0 为止,不再往下扣。
	/// </summary>
	public void TakeDamage(float amount)
	{
		if (!ApplyDamage(amount)) return;
		_visual?.PlayHurt();
		PlaySfx(_hurtPlayer); // 受伤,播放受击音效
	}

    /// <summary>状态伤害只播放低声量的状态提示，避免每秒中断射击/移动姿势。</summary>
    public void TakeStatusDamage(float amount, PlayerState kind)
    {
        if (kind != PlayerState.Bleed && kind != PlayerState.Slow) return;
        if (!ApplyDamage(amount)) return;
        if (hp > 0) PlaySfx(kind == PlayerState.Bleed ? _bleedPlayer : _poisonPlayer);
    }

    private bool ApplyDamage(float amount)
    {
        if (amount <= 0f || hp <= 0f || Stop.IsPaused) return false;
        hp = Mathf.Max(hp - amount, 0f);
        if (hp <= 0) StopStatusSounds();
        return true;
    }

    public void StopStatusSound(PlayerState kind)
    {
        if (kind == PlayerState.Bleed) _bleedPlayer?.Stop();
        if (kind == PlayerState.Slow) _poisonPlayer?.Stop();
    }

    public void StopStatusSounds()
    {
        _bleedPlayer?.Stop();
        _poisonPlayer?.Stop();
    }

    public override void _ExitTree()
    {
        foreach (var audio in new[] { _walkPlayer, _gunShotPlayer, _hurtPlayer, _bleedPlayer, _poisonPlayer })
        {
            if (audio == null) continue;
            audio.Stop();
            audio.Stream = null;
        }
    }
}
