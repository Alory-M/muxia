using Godot;

public partial class Bullet : Area2D
{
	/// <summary>
	/// 所有子弹都进这个组。模态界面(背包 / 商店)打开时,Stop 靠它把飞在半空的子弹也冻住 ——
	/// 不冻的话箭已经出膛了,你开背包躲不掉,它照样飞过来打中你
	/// </summary>
	public const string GroupName = "bullet";

	// 飞行速度(像素/秒)
	[Export] public float Speed { get; set; } = 800.0f;

	// 命中目标时造成的伤害
	[Export] public int Damage { get; set; } = 25;

	// 谁发射的：false=玩家(只打僵尸)；true=僵尸(只打玩家)。由发射方在 Launch 前设置
	public bool HitsPlayer { get; set; } = false;

	// 打中玩家时顺便给玩家上"迟缓"。毒僵尸的箭会置 true,同样由发射方在发射前设置。
	// 效果标记跟着箭走,而不是挂在僵尸身上 —— 箭可能飞很久才命中,那会儿僵尸未必还在
	public bool AppliesSlow { get; set; } = false;
    public bool HitsEveryone { get; set; } = false;

	private Vector2 _direction = Vector2.Zero;   // 由 Launch() 传入,之后不再改变
	private bool _isMoving = false;

	// 朝向组件,挂在 rotate 子节点上。缺了只是不转向,不影响飞行
	private Rotate _rotate;

	// 消失组件,挂在 disappear 子节点上。飞多远消失、撞到什么消失,判定都在它那儿
	private Disappear _disappear;
    private CollisionShape2D _shape;

	public override void _Ready()
	{
		// 先报名,让模态界面的 Stop 找得到自己
		AddToGroup(GroupName);

		// 万一是在暂停期间出膛的,自己先冻上再飞。
		// 正常不会发生(那会儿玩家和僵尸都被冻着,没人能开火),但这句能让"暂停"
		// 这件事对子弹来说永远是完整的,不用去推"谁会在暂停时开火"
		Stop.RegisterWorldNode(this);

		_rotate = GetNodeOrNull<Rotate>("rotate");
		if (_rotate == null)
		{
			GD.PushWarning("Bullet: 找不到子节点 rotate,子弹不会跟着飞行方向转向。");
		}

		_disappear = GetNodeOrNull<Disappear>("disappear");
        _shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (_disappear == null)
		{
			GD.PushWarning("Bullet: 找不到子节点 disappear,子弹既不会到距离消失、也不会撞东西消失。");
		}
	}

	/// <summary>
	/// 结算伤害。由 disappear 组件在判定"这一下会让子弹消失"时调用 ——
	/// 该不该打(玩家自己 / 友方僵尸 / 墙)由组件负责判,这儿只管打多少、
	/// 以及带不带附加效果(目前只有毒箭的迟缓)
	/// </summary>
	public void ApplyDamage(Node target)
	{
		if (Stop.IsPaused) return;
        if (HitsEveryone && target is Zombie zombie) { zombie.take_damage(Damage); return; }
        if (HitsPlayer || HitsEveryone)
		{
			if (target is Player player)
			{
				player.TakeDamage(Damage);

				if (AppliesSlow)
				{
					SlowPlayer(player);
				}
			}
			return;
		}

		// 僵尸的方法名故意是 snake_case 的 take_damage(见 Zombie.cs),墙没有这个方法
		if (target.HasMethod("take_damage"))
		{
			target.Call("take_damage", Damage);
		}
	}

	/// <summary>
	/// 给玩家上迟缓。走玩家身上的状态机(player/state),由它驱动 debuff 组件 ——
	/// 和 SharpZom 上流血是同一套写法
	/// </summary>
	private static void SlowPlayer(Player player)
	{
		State state = player.GetNodeOrNull<State>("state");
		if (state == null)
		{
			GD.PushWarning("Bullet: 找不到 player/state,迟缓没上成。");
			return;
		}

		state.ChangeState(PlayerState.Slow);
		GD.Print("Bullet: 毒箭命中,玩家中了迟缓");
	}

	// 由发射者调用:给它一个方向,它就开始飞
	public void Launch(Vector2 direction)
	{
		if (direction.LengthSquared() < 0.0001f)
		{
			// 方向为零就别留一颗不动的子弹在场上
			QueueFree();
			return;
		}

		_direction = direction.Normalized();
		_isMoving = true;

		// 转向交给 rotate 组件。子弹美术本来朝右,所以 0 度 = 朝右
		if (_rotate != null)
		{
			_rotate.FaceDirection(_direction);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_isMoving || Stop.IsPaused || _disappear?.IsGone == true)
		{
			return;
		}

        float step = Mathf.Min(Mathf.Max(0, Speed * (float)delta), _disappear?.RemainingDistance ?? float.MaxValue);
        if (_shape?.Shape != null && _disappear != null)
        {
            var query = new PhysicsShapeQueryParameters2D
            {
                Shape = _shape.Shape, Transform = _shape.GlobalTransform,
                CollisionMask = CollisionMask, CollideWithBodies = true, CollideWithAreas = false,
                Margin = 0.05f
            };
            var excluded = new Godot.Collections.Array<Rid> { GetRid() };
            // 只忽略同阵营实体，墙体与敌方在出生点也可以命中。
            foreach (string group in new[] { "player", "zombie" })
                foreach (Node node in GetTree().GetNodesInGroup(group))
                    if (node is CollisionObject2D body && _disappear.ShouldPassThrough(body)) excluded.Add(body.GetRid());
            query.Exclude = excluded;
            var space = GetWorld2D().DirectSpaceState;
            if (HitAt(space, query)) return;
            query.Motion = _direction * step;
            float[] cast = space.CastMotion(query);
            if (cast.Length >= 2 && cast[0] < 1f)
            {
                // 不安全比例落在接触面；略向前取样使 IntersectShape 能返回碰撞实体。
                var contact = query.Transform;
                float traveled = Mathf.Min(step, cast[1] * step + 0.1f);
                contact.Origin += _direction * traveled;
                query.Transform = contact; query.Motion = Vector2.Zero;
                GlobalPosition += _direction * traveled;
                if (!HitAt(space, query)) _disappear.Vanish();
                return;
            }
            else GlobalPosition += _direction * step;
        }
        else GlobalPosition += _direction * step;

		// 飞了多远、够不够 MaxDistance、撞没撞到东西,全交给组件
		if (_disappear != null)
		{
			_disappear.Advance(step);
		}
	}
    private bool HitAt(PhysicsDirectSpaceState2D space, PhysicsShapeQueryParameters2D query)
    {
        foreach (var result in space.IntersectShape(query, 32))
            if (result["collider"].AsGodotObject() is Node2D body && _disappear.TryHit(body)) return true;
        return false;
    }
}
