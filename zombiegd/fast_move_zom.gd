extends Zombie

# 新增瞬移僵尸专属参数，Inspector可调节
@export var teleport_trigger_range: float = 300.0 # 多远才会触发瞬移
@export var teleport_offset: Vector2 = Vector2(40,0) #瞬移到玩家旁边的偏移，避免直接卡玩家身上

func _physics_process(delta: float) -> void:
	if not player: return
	var dist = global_position.distance_to(player.global_position)
	attack_timer -= delta

	# ===== 瞬移逻辑 =====
	# 条件：玩家超出普攻距离，但在瞬移触发范围内，冷却完毕
	if dist > attack_range and dist <= teleport_trigger_range and attack_timer <= 0.0:
		# 瞬移到玩家位置+偏移
		global_position = player.global_position + teleport_offset
		# 瞬移后立刻攻击
		attack()
		attack_timer = attack_cd #重置攻击CD

	# 玩家离太远：正常追人（复用父类逻辑）
	elif dist > teleport_trigger_range:
		super._physics_process(delta)

	# 玩家已经在攻击范围内：原地不动，等待普攻（和父类逻辑一样）
	else:
		velocity = Vector2.ZERO
		if attack_timer <= 0.0:
			attack()
			attack_timer = attack_cd
