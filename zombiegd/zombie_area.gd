extends CharacterBody2D

@export var speed: float = 80.0
@export var max_range: float = 250.0  # 玩家超出这个范围就停止追击
@export var max_hp: int = 100         # 血量
@export var soul_drop: int = 1        # 死亡掉落的灵魂碎片数量
@export var attack_damage: int = 10   # 攻击伤害
@export var attack_cd: float = 1.0    # 攻击冷却(秒)

var coffin: Node2D = null   # 棺材脚本会给它赋值
var player: Node2D = null   # 棺材脚本会给它赋值
var hp: int

var player_in_attack_range := false  # 玩家是否在攻击箱内
var attack_timer := 0.0              # 攻击冷却计时
var attack_visual_timer := 0.0       # 攻击形象持续时间

@onready var normal_sprite: Sprite2D = $normal
@onready var attack_sprite: Sprite2D = $attack
@onready var attack_hitbox: Area2D = $AttackHitbox

func _ready():
	hp = max_hp
	# 攻击形象贴到 normal 的位置上，切换时才不会跳
	attack_sprite.position = normal_sprite.position
	attack_sprite.scale = normal_sprite.scale
	attack_hitbox.body_entered.connect(_on_attack_body_entered)
	attack_hitbox.body_exited.connect(_on_attack_body_exited)

func _on_attack_body_entered(body: Node2D) -> void:
	if body.is_in_group("player"):
		player_in_attack_range = true

func _on_attack_body_exited(body: Node2D) -> void:
	if body.is_in_group("player"):
		player_in_attack_range = false

func _physics_process(delta):
	# 攻击形象显示一小段时间后恢复成 normal
	if attack_visual_timer > 0.0:
		attack_visual_timer -= delta
		if attack_visual_timer <= 0.0:
			show_normal()

	if player == null or coffin == null:
		velocity = Vector2.ZERO
		move_and_slide()
		return

	attack_timer -= delta
	var dir := (player.global_position - global_position).normalized()
	normal_sprite.flip_h = dir.x < 0  # 始终面向玩家
	attack_sprite.flip_h = dir.x < 0

	if player_in_attack_range:
		# 玩家进入攻击箱：停下攻击
		velocity = Vector2.ZERO
		if attack_timer <= 0.0:
			do_attack()
	else:
		# 玩家在追击范围内就追，否则原地不动
		var dist_to_coffin := player.global_position.distance_to(coffin.global_position)
		if dist_to_coffin < max_range:
			velocity = dir * speed
		else:
			velocity = Vector2.ZERO

	move_and_slide()

func do_attack() -> void:
	if player == null:
		return
	attack_timer = attack_cd
	# normal → attack
	normal_sprite.visible = false
	attack_sprite.visible = true
	attack_visual_timer = 0.3  # 攻击形象显示 0.3 秒
	# 伤害玩家
	if player.has_method("take_damage"):
		player.call("take_damage", attack_damage)
	print("僵尸攻击，伤害：", attack_damage)

func show_normal() -> void:
	normal_sprite.visible = true
	attack_sprite.visible = false

# 受伤：子弹命中时由 Bullet.cs 调 body.Call("take_damage", Damage)
func take_damage(dmg: int) -> void:
	hp -= dmg
	if hp <= 0:
		die()

# 死亡：掉灵魂碎片，然后移除自己
func die() -> void:
	drop_soul()
	queue_free()

# 掉落灵魂碎片：直接加进玩家身上的 soulpiece，不用走过去捡
func drop_soul() -> void:
	if soul_drop <= 0:
		return
	var player_node := get_tree().get_first_node_in_group("player")
	if player_node == null:
		push_warning("Zombie: 找不到 player 分组节点，灵魂碎片丢失")
		return
	var soulpiece = player_node.get_node_or_null("soulpiece")
	if soulpiece == null or not soulpiece.has_method("add_soul"):
		push_warning("Zombie: 找不到灵魂碎片仓库(soulpiece)或没有 add_soul 方法，灵魂碎片丢失")
		return
	soulpiece.call("add_soul", soul_drop)
	print("掉落灵魂碎片 x", soul_drop)
