extends CharacterBody2D

@export var hp: int = 100
var is_poisoned: bool = false
var poison_damage_per_sec: float = 0.0
var poison_duration: float = 0.0
var poison_timer: float = 0.0

func _physics_process(delta:float):
	# 中毒逻辑
	if is_poisoned:
		poison_timer -= delta
		if poison_timer <= 0:
			is_poisoned = false
		else:
			hp -= poison_damage_per_sec * delta

# 被普通攻击扣血
func take_damage(dmg:int):
	hp -= dmg
	print("玩家受到伤害，剩余血量：", hp)

# 中毒函数，毒僵尸调用这个
func apply_poison(dps:float, duration:float):
	# 可以选择：是否叠加中毒 / 刷新中毒时间，这里做成刷新
	is_poisoned = true
	poison_damage_per_sec = dps
	poison_timer = duration
	print("玩家中毒！")
