using Godot;

/// <summary>
/// 子弹的消失组件,挂在 bullet.tscn 的 disappear 节点上。
///
/// 两种消失条件都收在这儿,和 Rotate.cs 一样是"组件节点操作宿主"的写法:
///
/// 1. 飞够了 MaxDistance。距离由宿主每帧喂进来(Advance),组件不自己去数位移
/// 2. 撞到任何非玩家的物理实体(StaticBody2D / CharacterBody2D 这些)就消失
///
/// 只接 BodyEntered,故意不接 AreaEntered —— 场景里那些 Area2D 大多是"检测圈"不是实体:
/// 僵尸的 AttackHitbox(半径 65,本体才 22)、棺材的 detect_area(半径 226)。
/// 接上 AreaEntered 子弹会在离目标还有几十像素的地方就消失,所以这里不认 Area2D。
///
/// 判定为"该消失"时,先让宿主结算伤害(它才知道打多少、打谁),再把子弹 QueueFree。
/// "该放过谁"的规则也在这儿:玩家射的子弹无视玩家自己(出生瞬间和玩家重叠),
/// 僵尸射的箭无视僵尸。
/// </summary>
public partial class Disappear : Node
{
	/// <summary>飞多少像素后自行消失</summary>
	[Export] public float MaxDistance { get; set; } = 800.0f;

	private Bullet _host;
	private float _traveled;   // 已经飞了多远
	private bool _gone;        // 宿主只该被释放一次

	/// <summary>已经消失了</summary>
	public bool IsGone => _gone;

	public override void _Ready()
	{
		_host = GetParent() as Bullet;
		if (_host == null)
		{
			GD.PushWarning("Disappear: 父节点不是 Bullet,子弹不会自己消失。");
			return;
		}

		_host.BodyEntered += OnBodyEntered;
	}

	/// <summary>宿主每飞一步就喂进来。距离累计只此一处,别在宿主里再数一遍</summary>
	public void Advance(float step)
	{
		if (_gone || _host == null)
		{
			return;
		}

		_traveled += step;
		if (_traveled >= MaxDistance)
		{
			Vanish();
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_gone || _host == null || ShouldPassThrough(body))
		{
			return;
		}

		_host.ApplyDamage(body);
		Vanish();
	}

	/// <summary>该穿过去、不当碰撞的情况</summary>
	private bool ShouldPassThrough(Node2D body)
	{
		if (_host.HitsPlayer)
		{
			// 僵尸射的箭:僵尸是自己人,穿过去。玩家不在这一支里 ——
			// Player 的方法是 PascalCase 的 TakeDamage,HasMethod("take_damage") 认不出来
			return body.HasMethod("take_damage");
		}

		// 玩家射的子弹:出生瞬间和玩家重叠,玩家不算目标
		return body.IsInGroup("player");
	}

	private void Vanish()
	{
		_gone = true;

		if (_host != null)
		{
			_host.QueueFree();
		}
	}
}
