extends Zombie

# 毒僵尸：普攻在造成伤害的同时给玩家上中毒（持续掉血）。
# 依赖玩家的 apply_poison(dps, duration) 方法（对应 zombiegd/player.gd）。
# 注意：主场景的玩家是 C# 的 Player.cs，目前只有流血/迟缓(State.cs)、没有中毒，
#       所以打 C# 玩家时只会造成普攻伤害、不会上毒。

# 中毒参数（Inspector 可调）
@export var poison_dps: float = 5.0        # 中毒：每秒扣多少血
@export var poison_duration: float = 3.0   # 中毒持续秒数

# 重写普攻：先照常打伤害，再附加中毒
func attack() -> void:
	super.attack()
	if player == null or not player.has_method("apply_poison"):
		return
	# 和普攻相同的距离容错，太远上不了毒
	if global_position.distance_to(player.global_position) > attack_range + 10.0:
		return
	player.apply_poison(poison_dps, poison_duration)
	print("毒僵尸上毒：每秒", poison_dps, "，持续", poison_duration, "秒")
