using Godot;
using System.Collections.Generic;

/// <summary>
/// 僵尸基类（C# 版）。对应原来的 zombiegd/zombie_area.gd，功能一一保留：
/// 追击、近战、受伤、死亡掉落、棺材信号绑定、把活动范围限制在棺材碰撞箱内。
/// 放进棺材里的僵尸自己连棺材的 player_entered 信号，收到信号后现身并开始追击。
///
/// 死亡掉落走 gift 子节点（见 DropLoot）：每个僵尸场景自己挂一个、填自己的 DropId，
/// 具体掉什么由 data/drop.json 决定。所以加新僵尸不用改这个基类。
/// </summary>
public partial class Zombie : CharacterBody2D
{
    [Signal] public delegate void DiedEventHandler();
    [Export] public int MonsterId { get; set; } = 1;
    [Export] public bool ArtFacesLeft { get; set; }
    public int Health => _hp;
    public bool IsActive => _active && !_dead;
    protected float TerritoryRadius;
    private bool _dead;
    private uint _collisionLayer;
    private Vector2 _home;
    private Tween _presentationTween;
    private bool _isAppearing;
    private bool _pendingSpawn;
    private float _spawnRetry;
    private float _walkPhase;
    private Vector2 _returnPosition;
    private float _yieldHold;
    private float _yieldDistance;
    private Vector2 _yieldDirection;
    private Vector2 _yieldInputDirection;
    protected bool IsYielding => _yieldHold > 0f;
    public string CurrentAnimation { get; private set; } = "idle";
    public Vector2 ReturnPosition => _returnPosition;
    protected bool IsAppearing => _isAppearing;
    private readonly List<SpritePose> _spritePoses = new();

    // 只改精灵，不改 CharacterBody2D 的碰撞变换或世界位置。
    private sealed class SpritePose
    {
        public readonly Sprite2D Sprite;
        private readonly Vector2 _position;
        public Vector2 Position => new(Sprite.FlipH ? -_position.X : _position.X, _position.Y);
        public readonly Vector2 Scale;
        public readonly float Rotation;
        public readonly Color Modulate;
        public SpritePose(Sprite2D sprite)
        {
            Sprite = sprite; _position = sprite.Position; Scale = sprite.Scale;
            Rotation = sprite.Rotation; Modulate = sprite.Modulate;
        }
        public void Restore()
        {
            Sprite.Position = Position; Sprite.Scale = Scale;
            Sprite.Rotation = Rotation; Sprite.Modulate = Modulate;
        }
    }

	/// <summary>
	/// 所有僵尸都进这个组。背包 / 商店这类模态界面打开时,Stop 靠它把场上的僵尸全冻住 ——
	/// 不这么找的话就得去猜僵尸挂在哪:可能是场景根的子节点,也可能是棺材的子节点
	/// </summary>
	public const string GroupName = "zombie";

	// ========== 全部僵尸可配置属性（Inspector 面板直接改） ==========
	[Export] public int MaxHp { get; set; } = 100;          // 血量
	[Export] public int AttackDamage { get; set; } = 10;    // 伤害
	[Export] public float MoveSpeed { get; set; } = 80f;    // 移速
	[Export] public float AttackCd { get; set; } = 1.2f;    // 攻击冷却
	[Export] public float AttackRange { get; set; } = 60f;  // 攻击距离
	[Export] public Vector2 SpawnOffset { get; set; } = new Vector2(72, 0);

	// 内部状态
	private int _hp;
	protected float _attackTimer;
	protected Node2D _player;   // 玩家引用：棺材信号传入，或按 player 分组自动查找
	private Node2D _coffin;     // 所属棺材：一般是 GetParent()
	protected bool _active = true; // 是否已"生成"。放进棺材里的僵尸在收到信号前先待机隐身

	private Godot.ColorRect _healthBar;   // 头顶血条
	private float _healthBarFullWidth;    // 血条满血宽度，_Ready 里记录

	// 音效播放器：现身 / 受伤 / 攻击各一个，_Ready 里创建并加载音频
	private AudioStreamPlayer _sfxShout;   // 僵尸现身（ready_shout）
	private AudioStreamPlayer _sfxHurt;    // 僵尸被子弹击中（zom_hurted）
	private AudioStreamPlayer _sfxAttack;  // 僵尸击中玩家（zom_attack）

	public override void _Ready()
	{
		// 先报名,让模态界面的 Stop 找得到自己
		AddToGroup(GroupName);

		var stats = GameData.Row("monster", MonsterId);
        MaxHp = (int)GameData.Number(stats, "blood");
        AttackDamage = (int)GameData.Number(stats, "hit");
        MoveSpeed = GameData.Number(stats, "speed") * GameData.SpeedUnit;
        AttackCd = GameData.Number(stats, "hittime");
        AttackRange = GameData.Number(stats, "hitdistance") * GameData.DistanceUnit;
        TerritoryRadius = GameData.Number(stats, "around") * GameData.DistanceUnit;
        if (this is ArrowZom archer) { archer.FireRange = AttackRange; archer.FireCd = AttackCd; }
        var gift = GetNodeOrNull<Gift>("gift");
        if (gift != null) gift.DropId = int.Parse(stats.GetProperty("drop").GetString());
        _collisionLayer = CollisionLayer;
        _home = GlobalPosition;
        _returnPosition = _home;
        _hp = MaxHp;
		_attackTimer = 0f;

		_healthBar = GetNodeOrNull<Godot.ColorRect>("ColorRect");
		if (_healthBar != null)
		{
			_healthBarFullWidth = _healthBar.Size.X;
		}
		UpdateHealthBar();

		SetSpriteState(false); // 初始为 normal 状态，等玩家进入碰撞箱再切 attack
        foreach (string name in new[] { "normal", "attack" })
        {
            var sprite = GetNodeOrNull<Sprite2D>(name);
            if (sprite != null) _spritePoses.Add(new SpritePose(sprite));
        }

		SetupAudio();

		BindCoffin();
		EnsurePlayer();
        UpdateFacing();
        foreach (var pose in _spritePoses) pose.Restore();
        Stop.RegisterWorldNode(this);
	}

	// ========== 音效：现身 / 受伤 / 攻击 ==========
	// 三个 AudioStreamPlayer 都在 _Ready 里创建，音频用 res:// 路径硬编码加载。
	// 文件找不到时只打警告、不崩——音频还没提交到仓库时也能正常跑。
	private void SetupAudio()
	{
		_sfxShout = CreateSfxPlayer("zombie_shout.wav");
		_sfxHurt = CreateSfxPlayer("zombie_be_attacked_or_die.wav");
		_sfxAttack = CreateSfxPlayer("zombie_attack_near.wav");
	}

	// 创建一个挂在僵尸身上的音效播放器，并加载音频文件
	private AudioStreamPlayer CreateSfxPlayer(string fileName)
	{
		AudioStreamPlayer player = new AudioStreamPlayer { Bus = "Sfx" };
		AddChild(player);

		AudioStream stream = GD.Load<AudioStream>($"res://music/{fileName}");
		if (stream == null)
		{
			GD.PushWarning($"Zombie: 找不到音频 res://music/{fileName}");
		}
		else
		{
			player.Stream = stream;
		}
		return player;
	}

	// 播放指定音效；没加载到流（文件缺失）时静默跳过
	private void PlaySfx(AudioStreamPlayer player)
	{
		if (player != null && player.Stream != null)
		{
			player.Play();
		}
	}

	// 棺材开门信号回调：现身并锁定进入棺材的玩家，然后开始活动
	private void OnCoffinPlayerEntered(Node2D playerNode)
	{
		if (_dead) return;
		if (playerNode != null) _player = playerNode;
        _pendingSpawn = true;
        TryActivateOutsideCoffin();
	}

    // 棺材有实体碰撞，必须先找到真正空闲的位置，不能在原点把敌人挤进玩家或棺材。
    private void TryActivateOutsideCoffin()
    {
        if (!_pendingSpawn || Stop.IsPaused || !TryFindSpawnPosition(out Vector2 position)) return;
        _pendingSpawn = false;
        GlobalPosition = position;
        _returnPosition = position;
        Visible = true;
        _active = true;
        SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, _collisionLayer);
        PlaySfx(_sfxShout);
        UpdateFacing();
        PlayAppearAnimation();
    }

    private bool TryFindSpawnPosition(out Vector2 position)
    {
        position = GlobalPosition;
        var shapeNode = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (shapeNode?.Shape == null) return false;
        Vector2 direction = _player != null && GodotObject.IsInstanceValid(_player)
            ? _home.DirectionTo(_player.GlobalPosition) : SpawnOffset.Normalized();
        if (direction.IsZeroApprox()) direction = Vector2.Right;
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = shapeNode.Shape,
            CollisionMask = CollisionMask | (_player is CollisionObject2D body ? body.CollisionLayer : 0),
            CollideWithBodies = true,
            CollideWithAreas = false,
            Margin = 3f,
            Exclude = new Godot.Collections.Array<Rid> { GetRid() }
        };
        var trapQuery = new PhysicsShapeQueryParameters2D
        {
            Shape = shapeNode.Shape, CollisionMask = uint.MaxValue,
            CollideWithBodies = false, CollideWithAreas = true, Margin = 3f
        };
        bool found = false;
        float bestDistance = float.MaxValue;
        Vector2 preferred = _player != null && GodotObject.IsInstanceValid(_player)
            ? _player.GlobalPosition : _home + SpawnOffset;
        foreach (float desiredRadius in new[] { Mathf.Max(72f, SpawnOffset.Length()), 96f, 128f, 152f })
        {
            float radius = Mathf.Min(desiredRadius, TerritoryRadius - 3f);
            for (int index = 0; index < 24; index++)
            {
                int step = (index + 1) / 2;
                float angle = step * Mathf.Tau / 24f * (index % 2 == 0 ? -1f : 1f);
                Vector2 candidate = _home + direction.Rotated(angle) * radius;
                if (!IsWithinTerritory(candidate)) continue;
                Transform2D transform = shapeNode.GlobalTransform;
                transform.Origin += candidate - GlobalPosition;
                query.Transform = transform;
                if (GetWorld2D().DirectSpaceState.IntersectShape(query, 1).Count != 0) continue;
                // 地刺也能伤敌人，但不应在出现动画尚未结束时把新守卫直接秒杀。
                // 只避开 Trap；棺材交互圈/敌人攻击圈不是实体，也不能阻止正常生成。
                trapQuery.Transform = transform;
                bool onTrap = false;
                foreach (var result in GetWorld2D().DirectSpaceState.IntersectShape(trapQuery, 64))
                    if (result["collider"].AsGodotObject() is Trap) { onTrap = true; break; }
                if (onTrap) continue;
                float distance = candidate.DistanceSquaredTo(preferred);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                position = candidate;
                found = true;
            }
        }
        return found;
    }

	// 切换僵尸外观：showAttack=true 显示攻击精灵、隐藏 normal；false 反之
	private void SetSpriteState(bool showAttack)
	{
		if (_dead) return;
		var normal = GetNodeOrNull<Sprite2D>("normal");
		var attack = GetNodeOrNull<Sprite2D>("attack");
		if (normal != null)
		{
			normal.Visible = !showAttack;
		}
		if (attack != null)
		{
			attack.Visible = showAttack;
		}
	}

	// 把父节点当成棺材：连上它的 player_entered 信号，收到信号前先藏着、不活动
	private void BindCoffin()
	{
		Node parent = GetParent();
		if (parent is not Coffin coffin)
		{
			return;
		}

		_coffin = coffin;
		if (_coffin == null)
		{
			return;
		}

		// 初始化时把僵尸摆到棺材位置（出棺前先呆在棺材处）
		GlobalPosition = _coffin.GlobalPosition;
		_active = false;
		Visible = false;
        CollisionLayer = 0;
        _home = _coffin.GlobalPosition;
        _returnPosition = _home;
        CurrentAnimation = "hidden";

		Callable callable = Callable.From<Node2D>(OnCoffinPlayerEntered);
		if (!parent.IsConnected(Coffin.SignalName.PlayerEntered, callable))
		{
			parent.Connect(Coffin.SignalName.PlayerEntered, callable);
		}

		// 攻击立绘只在真正攻击时显示，靠近棺材不会提前进入攻击动画。
	}

	// 拿到有效的玩家引用；玩家为空或已失效时，按 player 分组重新找
	protected bool EnsurePlayer()
	{
		if (_player == null || !GodotObject.IsInstanceValid(_player))
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		}
		return _player != null;
	}

	public override void _PhysicsProcess(double delta)
	{
        if (!PrepareAiFrame(delta)) return;
        _attackTimer -= (float)delta;
        // 被玩家和墙挤住时只暂缓追击；贴身攻击仍按原冷却生效。
        if (IsYielding)
        {
            if (CanEngage() && IsWithinMeleeRange()) MeleeAttack();
            else Velocity = Vector2.Zero;
            return;
        }
        if (!CanEngage()) { ReturnHome(); return; }
        if (!IsWithinMeleeRange()) Chase();
        else MeleeAttack();
        ClampToCoffin();
    }

    protected bool PrepareAiFrame(double delta)
    {
        if (Stop.IsPaused || _dead) return false;
        _yieldHold = Mathf.Max(0, _yieldHold - (float)delta);
        if (_pendingSpawn)
        {
            _spawnRetry -= (float)delta;
            if (_spawnRetry <= 0) { TryActivateOutsideCoffin(); _spawnRetry = 0.25f; }
        }
        if (!_active || !EnsurePlayer()) return false;
        if (_isAppearing) { Velocity = Vector2.Zero; return false; }
        return true;
    }

    /// <summary>
    /// 主角持续朝本实体移动却被堵住时，僵尸小步侧让。始终扫掠完整实体，
    /// 不忽略玩家、墙、棺材或其它僵尸；没有真实空位时保持原位。
    /// </summary>
    public bool TryYieldToPlayer(Player player, Vector2 inputDirection, double delta)
    {
        if (!IsActive || IsAppearing || Stop.IsPaused || player == null || player.hp <= 0 ||
            inputDirection.IsZeroApprox() || delta <= 0 || GlobalPosition.DistanceTo(player.GlobalPosition) > 140f)
            return false;
        var own = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (own?.Shape == null || own.Disabled) return false;
        Vector2 input = inputDirection.Normalized();
        // Keep one local relief budget while the player keeps pressing the same
        // direction. It prevents repeated 3px steps from pushing an enemy an
        // unbounded distance along a corridor.
        if (_yieldInputDirection.IsZeroApprox() || _yieldInputDirection.Dot(input) < 0.95f)
        {
            _yieldDistance = 0;
            _yieldDirection = Vector2.Zero;
            _yieldInputDirection = input;
        }
        // 每个连续挤让阶段最多移动 100px。让步速度略高于玩家当前步长，
        // 这样玩家持续顶住时，僵尸会先离开碰撞面再恢复追击，不会被玩家
        // 的下一帧移动重新压回同一个实体交叠点。
        if (_yieldDistance >= 100f) return false;
        float playerStep = player.EffectiveMoveSpeed * (float)delta;
        float step = Mathf.Min(Mathf.Min(12f, Mathf.Max(4f, playerStep * 1.15f)), 100f - _yieldDistance);
        Vector2 direction = input;
        Vector2 sideways = direction.Orthogonal();
        Vector2 away = player.GlobalPosition.DirectionTo(GlobalPosition);
        var directions = new[] { _yieldDirection, sideways, -sideways, away,
            away.Rotated(Mathf.Pi / 4f), away.Rotated(-Mathf.Pi / 4f), direction };
        var query = new PhysicsShapeQueryParameters2D
        {
            // The relief probe must see the player even when a scene or a
            // temporary state has disabled one side of the usual layer mask.
            // The node itself is excluded below, while every real body (walls,
            // coffins, other zombies and the player) remains a hard blocker.
            Shape = own.Shape, CollisionMask = uint.MaxValue,
            CollideWithBodies = true, CollideWithAreas = false, Margin = 0.04f,
            Exclude = new Godot.Collections.Array<Rid> { GetRid() }
        };
        var playerShape = player.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        foreach (Vector2 candidate in directions)
        {
            if (candidate.IsZeroApprox()) continue;
            Vector2 motion = candidate.Normalized() * step;
            if (!IsWithinTerritory(GlobalPosition + motion)) continue;
            // CharacterBody2D layer settings can change while a modal state or
            // a spawned enemy is being initialized. Check the two actual shapes
            // as well, so a relief step can never end inside the player.
            if (playerShape?.Shape != null &&
                own.Shape.CollideWithMotion(own.GlobalTransform, motion,
                    playerShape.Shape, playerShape.GlobalTransform, Vector2.Zero)) continue;
            // 终点空闲还不够：路径也必须容纳整个胶囊，不能跨过薄墙。
            query.Transform = own.GlobalTransform;
            query.Motion = motion;
            var fraction = GetWorld2D().DirectSpaceState.CastMotion(query);
            if (fraction.Length < 2 || fraction[0] < 0.999f) continue;
            Transform2D end = own.GlobalTransform;
            end.Origin += motion;
            query.Transform = end;
            query.Motion = Vector2.Zero;
            if (GetWorld2D().DirectSpaceState.IntersectShape(query, 1).Count != 0) continue;
            Vector2 before = GlobalPosition;
            MoveAndCollide(motion, false, 0.02f);
            float moved = GlobalPosition.DistanceTo(before);
            if (moved < 0.01f) continue;
            _yieldDistance += moved;
            _yieldDirection = candidate.Normalized();
            _yieldHold = 0.3f;
            Velocity = Vector2.Zero;
            return true;
        }
        return false;
    }

    /// <summary>Called when the player has made meaningful progress or released input.</summary>
    public void ResetYieldBudget()
    {
        _yieldHold = 0;
        _yieldDistance = 0;
        _yieldDirection = Vector2.Zero;
        _yieldInputDirection = Vector2.Zero;
    }

	// 每帧根据玩家位置更新朝向（水平翻转），独立于 _PhysicsProcess 里的移动逻辑：
	// 子类就算重写 _PhysicsProcess 不调 base，这个翻转也照样生效。
	public override void _Process(double delta)
	{
		UpdateFacing();
        if (!_active || _dead || _presentationTween != null || Stop.IsPaused) return;
        SetSpriteState(false);
        if (Velocity.LengthSquared() < 1f)
        {
            foreach (var pose in _spritePoses) pose.Restore();
            CurrentAnimation = "idle";
            return;
        }
        CurrentAnimation = "move";
        float cadence = MonsterId == 5 ? 6.5f : 10f;
        _walkPhase += (float)delta * cadence;
        float step = Mathf.Sin(_walkPhase);
        foreach (var pose in _spritePoses)
        {
            pose.Sprite.Position = pose.Position + new Vector2(0, -Mathf.Abs(step) * 2.5f);
            pose.Sprite.Rotation = pose.Rotation + step * 0.035f;
            pose.Sprite.Scale = pose.Scale * new Vector2(1f + 0.015f * step, 1f - 0.015f * step);
        }
	}

	// 水平翻转判定：玩家横坐标小于僵尸时翻转精灵（朝左），否则恢复默认朝向（朝右）
	private void UpdateFacing()
	{
		if (_dead || _player == null || !GodotObject.IsInstanceValid(_player))
		{
			return;
		}

		bool flip = (_player.GlobalPosition.X < GlobalPosition.X) != ArtFacesLeft;
		var normal = GetNodeOrNull<Sprite2D>("normal");
		var attack = GetNodeOrNull<Sprite2D>("attack");
		if (normal != null)
		{
			normal.FlipH = flip;
		}
		if (attack != null)
		{
			attack.FlipH = flip;
		}
	}

	// 追击玩家
	protected bool CanEngage()
    {
        return _player is Player player && player.hp > 0 &&
            _player.GlobalPosition.DistanceTo(_home) <= TerritoryRadius;
    }
    protected bool IsWithinTerritory(Vector2 position)
    {
        Vector2 center = _coffin?.GetNodeOrNull<Area2D>("detect_area")?.GlobalPosition ?? _home;
        return position.DistanceTo(center) <= TerritoryRadius;
    }
    // 主角/僵尸的碰撞体都高约84~88px，上下贴身的中心距会大于64px表射程。
    // 保留原射程；额外只允许沿朝向6px扫掠可触到的真实碰撞体，不扩大横向射程。
    protected bool IsWithinMeleeRange()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player)) return false;
        if (GlobalPosition.DistanceTo(_player.GlobalPosition) <= AttackRange) return true;
        var own = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        var target = _player.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (own?.Shape == null || target?.Shape == null || own.Disabled || target.Disabled) return false;
        Vector2 reach = own.GlobalPosition.DirectionTo(target.GlobalPosition) * 6f;
        return own.Shape.CollideWithMotion(own.GlobalTransform, reach, target.Shape, target.GlobalTransform, Vector2.Zero);
    }
    protected void ReturnHome()
    {
        if (GlobalPosition.DistanceTo(_returnPosition) < 5f) { Velocity = Vector2.Zero; return; }
        Velocity = GlobalPosition.DirectionTo(_returnPosition) * MoveSpeed;
        MoveAndSlide();
    }
    protected void Chase()
	{
		Vector2 dir = (_player.GlobalPosition - GlobalPosition).Normalized();
		Velocity = dir * MoveSpeed;
		MoveAndSlide();
	}

	// 原地普攻（冷却好了才打）
	protected void MeleeAttack()
	{
		Velocity = Vector2.Zero;
		if (_attackTimer <= 0f)
		{
			Attack();
			_attackTimer = AttackCd;
		}
	}

	// 【通用普攻函数，所有僵尸共用】——子类可重写（如 SharpZom 加流血）
	protected virtual void Attack()
	{
		if (_player == null || !CanEngage() || !IsWithinMeleeRange())
		{
			return;
		}
		// 兼容 C# 玩家(Player.TakeDamage)和 GDScript 玩家(snake_case 的 take_damage)
		if (_player is Player player)
		{
			player.TakeDamage(AttackDamage);
		}
		else if (_player.HasMethod("take_damage"))
		{
			_player.Call("take_damage", AttackDamage);
		}
		GD.Print($"僵尸攻击，伤害：{AttackDamage}");
		PlaySfx(_sfxAttack); // 这一下打到了玩家，播放 zom_attack
        PlayAttackAnimation();
	}

	// 受伤函数。方法名故意用 snake_case：Bullet.cs 里是 body.Call("take_damage", ...)，
	// 名字必须一字不差地对上，否则子弹打不中僵尸。
	public void take_damage(int dmg)
	{
		if (!_active || _dead || dmg <= 0 || Stop.IsPaused) return;
        _hp = Mathf.Max(0, _hp - dmg);
		PlaySfx(_sfxHurt); // 被玩家子弹击中，播放 zom_hurted
		UpdateHealthBar();
		if (_hp <= 0)
		{
			Die();
		}
        else PlayHitAnimation();
	}

    private void ResetPresentation()
    {
        if (_presentationTween != null && _presentationTween.IsValid()) _presentationTween.Kill();
        _presentationTween = null;
        _isAppearing = false;
        foreach (var pose in _spritePoses) pose.Restore();
    }

    private void EndPresentation()
    {
        foreach (var pose in _spritePoses) pose.Restore();
        _presentationTween = null;
        _isAppearing = false;
        CurrentAnimation = "idle";
        SetSpriteState(false);
    }

    private void PlayAppearAnimation()
    {
        ResetPresentation();
        SetSpriteState(false);
        _isAppearing = true;
        CurrentAnimation = "appear";
        foreach (var pose in _spritePoses)
        {
            pose.Sprite.Position = pose.Position + new Vector2(0, 18);
            pose.Sprite.Scale = pose.Scale * new Vector2(0.8f, 0.5f);
            Color transparent = pose.Modulate;
            transparent.A = 0;
            pose.Sprite.Modulate = transparent;
        }
        _presentationTween = CreateTween();
        _presentationTween.TweenMethod(Callable.From<float>(progress =>
        {
            foreach (var pose in _spritePoses)
            {
                pose.Sprite.Position = pose.Position + new Vector2(0, 18f * (1f - progress));
                pose.Sprite.Scale = pose.Scale * new Vector2(Mathf.Lerp(0.8f, 1f, progress), Mathf.Lerp(0.5f, 1f, progress));
                Color tint = pose.Modulate;
                tint.A *= progress;
                pose.Sprite.Modulate = tint;
            }
        }), 0f, 1f, 0.5).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _presentationTween.TweenCallback(Callable.From(EndPresentation));
    }

    protected void PlayAttackAnimation()
    {
        if (_dead || !_active) return;
        ResetPresentation();
        UpdateFacing();
        SetSpriteState(true);
        CurrentAnimation = "attack";
        float direction = _spritePoses.Count > 0 && _spritePoses[0].Sprite.FlipH != ArtFacesLeft ? -1f : 1f;
        _presentationTween = CreateTween();
        _presentationTween.TweenMethod(Callable.From<float>(progress =>
        {
            float pulse = Mathf.Sin(progress * Mathf.Pi);
            foreach (var pose in _spritePoses)
            {
                pose.Sprite.Position = pose.Position + new Vector2(direction * 7f, -2f) * pulse;
                pose.Sprite.Rotation = pose.Rotation - direction * 0.12f * pulse;
                pose.Sprite.Scale = pose.Scale * new Vector2(1f + 0.07f * pulse, 1f - 0.04f * pulse);
            }
        }), 0f, 1f, this is ArrowZom ? 0.38 : 0.3);
        _presentationTween.TweenCallback(Callable.From(EndPresentation));
    }

    private void PlayHitAnimation()
    {
        ResetPresentation();
        SetSpriteState(false);
        CurrentAnimation = "hurt";
        Vector2 recoil = Vector2.Zero;
        if (_player != null && GodotObject.IsInstanceValid(_player))
            recoil = ToLocal(GlobalPosition + _player.GlobalPosition.DirectionTo(GlobalPosition) * 3f);
        float lean = recoil.X < 0 ? -0.08f : 0.08f;
        _presentationTween = CreateTween();
        _presentationTween.TweenMethod(Callable.From<float>(progress =>
        {
            float pulse = Mathf.Sin(progress * Mathf.Pi);
            foreach (var pose in _spritePoses)
            {
                pose.Sprite.Position = pose.Position + recoil * pulse;
                pose.Sprite.Scale = pose.Scale * new Vector2(1f + 0.08f * pulse, 1f - 0.08f * pulse);
                pose.Sprite.Rotation = pose.Rotation + lean * pulse;
                pose.Sprite.Modulate = pose.Modulate.Lerp(new Color(1f, 0.25f, 0.2f, pose.Modulate.A), 1f - progress);
            }
        }), 0f, 1f, 0.22);
        _presentationTween.TweenCallback(Callable.From(EndPresentation));
    }

	// 按当前血量刷新头顶血条：满血=初始宽度，扣血按比例缩短
	private void UpdateHealthBar()
	{
		if (_healthBar == null)
		{
			return;
		}
		float ratio = Mathf.Clamp((float)_hp / MaxHp, 0f, 1f);
		_healthBar.Size = new Vector2(_healthBarFullWidth * ratio, _healthBar.Size.Y);
	}

	// 把僵尸位置收回棺材碰撞箱内（detect_area 的形状范围）。
	// 没有棺材、或形状不是圆形时不做限制，直接摆在场景里的僵尸不受影响。
	protected void ClampToCoffin()
	{
		if (_coffin == null || !GodotObject.IsInstanceValid(_coffin))
		{
			return;
		}

		var detect = _coffin.GetNodeOrNull<Area2D>("detect_area");
		if (detect == null)
		{
			return;
		}
		var shapeNode = detect.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (shapeNode == null || shapeNode.Shape == null)
		{
			return;
		}

		if (shapeNode.Shape is CircleShape2D circle)
		{
			float radius = TerritoryRadius;
			Vector2 center = detect.GlobalPosition;
			Vector2 offset = GlobalPosition - center;
			if (offset.Length() > radius)
			{
				GlobalPosition = center + offset.Normalized() * radius;
			}
		}
	}

	// 死亡先停 AI/禁碰撞/一次性结算，再播放倒地渐隐，最后释放。
	private void Die()
	{
		if (_dead) return;
        _dead = true;
        _active = false;
        Velocity = Vector2.Zero;
        ResetPresentation();
        CurrentAnimation = "death";
        SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, 0);
        SetDeferred(CollisionObject2D.PropertyName.CollisionMask, 0);
        GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
        var hitbox = GetNodeOrNull<Area2D>("AttackHitbox");
        hitbox?.SetDeferred(Area2D.PropertyName.Monitoring, false);
        hitbox?.SetDeferred(Area2D.PropertyName.Monitorable, false);
        if (_healthBar != null) _healthBar.Visible = false;
        DropLoot();
        EmitSignal(SignalName.Died);
        float direction = _spritePoses.Count > 0 && _spritePoses[0].Sprite.FlipH != ArtFacesLeft ? -1f : 1f;
        _presentationTween = CreateTween();
        _presentationTween.TweenMethod(Callable.From<float>(progress =>
        {
            foreach (var pose in _spritePoses)
            {
                pose.Sprite.Position = pose.Position + new Vector2(direction * 8f, 15f) * progress;
                pose.Sprite.Rotation = pose.Rotation + direction * 1.25f * progress;
                pose.Sprite.Scale = pose.Scale * new Vector2(1f - 0.15f * progress, 1f - 0.45f * progress);
                Color tint = pose.Modulate;
                tint.A *= 1f - progress;
                pose.Sprite.Modulate = tint;
            }
        }), 0f, 1f, 0.45).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        _presentationTween.TweenCallback(Callable.From(QueueFree));
	}

	/// <summary>
	/// 死亡掉落。掉什么、掉多少由 gift 子节点读 data/drop.json 决定 ——
	/// 每个僵尸场景自己挂一个 gift 填上对应的 DropId:
	///   mini_zom 2001 / fast_move_zom + arrow_zom 2002 / sharp_zom + heavy_zom + poison_zom 2003
	/// 没挂 gift 的就是不掉(比如基础大巨 zombie.tscn),这里静默跳过,不刷警告
	/// </summary>
	private void DropLoot()
	{
		Gift gift = GetNodeOrNull<Gift>("gift");
		if (gift == null)
		{
			return;
		}

		gift.Drop();
	}
}
