using Godot;

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
	[Export] public Vector2 SpawnOffset { get; set; } = new Vector2(64, 0); // 预留：出棺位置（暂未使用）

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

		_hp = MaxHp;
		_attackTimer = 0f;

		_healthBar = GetNodeOrNull<Godot.ColorRect>("ColorRect");
		if (_healthBar != null)
		{
			_healthBarFullWidth = _healthBar.Size.X;
		}
		UpdateHealthBar();

		SetSpriteState(false); // 初始为 normal 状态，等玩家进入碰撞箱再切 attack

		SetupAudio();

		BindCoffin();
		EnsurePlayer();
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
		AudioStreamPlayer player = new AudioStreamPlayer();
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
		Visible = true;
		_active = true;
		PlaySfx(_sfxShout); // 僵尸现身，播放 ready_shout
		if (playerNode != null)
		{
			_player = playerNode;
		}
		ClampToCoffin();
	}

	// 玩家进入棺材碰撞箱（detect_area）：切到 attack 状态
	private void OnDetectAreaBodyEntered(Node2D body)
	{
		if (body != null && body.IsInGroup("player"))
		{
			SetSpriteState(true);
		}
	}

	// 玩家离开棺材碰撞箱（detect_area）：切回 normal 状态
	private void OnDetectAreaBodyExited(Node2D body)
	{
		if (body != null && body.IsInGroup("player"))
		{
			SetSpriteState(false);
		}
	}

	// 切换僵尸外观：showAttack=true 显示攻击精灵、隐藏 normal；false 反之
	private void SetSpriteState(bool showAttack)
	{
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
		if (parent == null || !parent.HasSignal("player_entered"))
		{
			return;
		}

		_coffin = parent as Node2D;
		if (_coffin == null)
		{
			return;
		}

		// 初始化时把僵尸摆到棺材位置（出棺前先呆在棺材处）
		GlobalPosition = _coffin.GlobalPosition;
		_active = false;
		Visible = false;

		Callable callable = Callable.From<Node2D>(OnCoffinPlayerEntered);
		if (!parent.IsConnected("player_entered", callable))
		{
			parent.Connect("player_entered", callable);
		}

		// 连上棺材碰撞箱(detect_area)的 body 进入/离开信号，用于切换 attack / normal 精灵
		var detectArea = _coffin.GetNodeOrNull<Area2D>("detect_area");
		if (detectArea != null)
		{
			Callable enterCallable = Callable.From<Node2D>(OnDetectAreaBodyEntered);
			if (!detectArea.IsConnected("body_entered", enterCallable))
			{
				detectArea.Connect("body_entered", enterCallable);
			}

			Callable exitCallable = Callable.From<Node2D>(OnDetectAreaBodyExited);
			if (!detectArea.IsConnected("body_exited", exitCallable))
			{
				detectArea.Connect("body_exited", exitCallable);
			}
		}
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
		if (!_active)
		{
			return;
		}
		if (!EnsurePlayer())
		{
			return;
		}

		_attackTimer -= (float)delta;
		float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);

		// 1.离玩家远 → 追玩家；2.进入攻击范围 → 普攻
		if (dist > AttackRange)
		{
			Chase();
		}
		else
		{
			MeleeAttack();
		}

		// 追击/普攻后都收回棺材碰撞箱内，别让僵尸跑出活动范围
		ClampToCoffin();
	}

	// 每帧根据玩家位置更新朝向（水平翻转），独立于 _PhysicsProcess 里的移动逻辑：
	// 子类就算重写 _PhysicsProcess 不调 base，这个翻转也照样生效。
	public override void _Process(double delta)
	{
		UpdateFacing();
	}

	// 水平翻转判定：玩家横坐标小于僵尸时翻转精灵（朝左），否则恢复默认朝向（朝右）
	private void UpdateFacing()
	{
		if (_player == null || !GodotObject.IsInstanceValid(_player))
		{
			return;
		}

		bool flip = _player.GlobalPosition.X < GlobalPosition.X;
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
		if (_player == null)
		{
			return;
		}
		// 距离太远打不到（容错比攻击距离多一点）
		if (GlobalPosition.DistanceTo(_player.GlobalPosition) > AttackRange + 10f)
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
	}

	// 受伤函数。方法名故意用 snake_case：Bullet.cs 里是 body.Call("take_damage", ...)，
	// 名字必须一字不差地对上，否则子弹打不中僵尸。
	public void take_damage(int dmg)
	{
		_hp -= dmg;
		PlaySfx(_sfxHurt); // 被玩家子弹击中，播放 zom_hurted
		UpdateHealthBar();
		if (_hp <= 0)
		{
			Die();
		}
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
			float radius = circle.Radius;
			Vector2 center = detect.GlobalPosition;
			Vector2 offset = GlobalPosition - center;
			if (offset.Length() > radius)
			{
				GlobalPosition = center + offset.Normalized() * radius;
			}
		}
	}

	// 死亡函数:掉什么交给 gift 子节点结算,然后自己消失
	private void Die()
	{
		DropLoot();
		QueueFree();
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
