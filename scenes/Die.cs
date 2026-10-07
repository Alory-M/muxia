using Godot;

/// <summary>
/// 玩家死亡,挂在 player 下的 die 节点上。
///
/// 每帧看一眼玩家的 hp,掉到 0 就切到失败界面 gameover.tscn ——
/// ChangeSceneToFile 会把当前这个 game 场景整个释放掉,不用自己清。
///
/// 为什么用"每帧看一眼 hp"而不是在 Player.TakeDamage 里发信号:
/// 扣玩家血的一共三条路 —— 僵尸近战、僵尸的箭、流血 —— 其中**流血是 debuff.cs
/// 用反射直接写 hp 字段的,根本没走 TakeDamage**。挂在这儿盯 hp 本身,三条路都盖得住,
/// 以后再加扣血方式也不用回来改这里。
///
/// 只触发一次:切场景是下一帧才生效的,这中间别再被触发第二遍。
/// </summary>
public partial class Die : Node
{
	// 失败界面。拎出来,以后改名不用翻代码
	private const string GameOverScenePath = "res://scenes/gameover.tscn";

	private Player _player;
	private bool _dead;

	public override void _Ready()
	{
		// 和 State / Clear 一样,挂在 player 下面就直接认父节点
		_player = GetParent() as Player;
		if (_player == null)
		{
			GD.PushWarning("Die: 父节点不是 Player,血量归零不会切到失败界面。");
		}
	}

	public override void _Process(double delta)
	{
		if (_dead || _player == null || _player.hp > 0f)
		{
			return;
		}

		_dead = true;

		GD.Print("Die: 玩家血量归零,切到失败界面");
		GetTree().ChangeSceneToFile(GameOverScenePath);
	}
}
