extends Zombie

# 瞬移僵尸专属参数，Inspector可调节
@export var teleport_trigger_range: float = 300.0 # 多远才会触发瞬移
@export var teleport_distance: float = 40.0       # 瞬移后离玩家的距离（避免卡玩家身上）

func _physics_process(delta: float) -> void:
	if not player: return
	attack_timer -= delta
	var dist: float = global_position.distance_to(player.global_position)

	# 1. 瞬移攻击：玩家在普攻距离外、但在瞬移触发范围内，且冷却好了
	if dist > attack_range and dist <= teleport_trigger_range and attack_timer <= 0.0:
		teleport_attack()
	# 2. 超出瞬移范围：正常追击（复用父类逻辑）
	elif dist > teleport_trigger_range:
		chase()
	# 3. 已在攻击范围内：原地普攻（复用父类逻辑）
	else:
		melee_attack()

# 沿接近方向瞬移到玩家身边，并立刻攻击
func teleport_attack() -> void:
	var dir: Vector2 = global_position - player.global_position # 从玩家指向僵尸的方向
	if dir.length() < 0.01:
		dir = Vector2.RIGHT # 几乎重合时随便选个方向，避免除零
	else:
		dir = dir.normalized()
	# 保持在自己原本那一侧，贴到玩家附近，不跟玩家重叠
	global_position = player.global_position + dir * teleport_distance
	attack()
	attack_timer = attack_cd
