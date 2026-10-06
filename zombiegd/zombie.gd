extends CharacterBody2D
class_name Zombie # 定义类名，别的脚本可以直接 extends Zombie

# ========== 全部僵尸可配置属性（Inspector面板直接改） ==========
@export var max_hp: int = 100          #血量
@export var attack_damage: int = 10    #伤害
@export var move_speed: float = 80.0   #移速
@export var attack_cd: float = 1.2     #攻击冷却
@export var attack_range: float = 60.0 #攻击距离
@export var drop_item: PackedScene     #死亡掉落物（拖入场景资源）

# 内部变量
var hp: int
var attack_timer: float = 0.0
@onready var player: Node2D = null

@onready var detect_area: Area2D = $DetectArea
@onready var attack_hitbox: Area2D = $AttackHitbox

func _ready():
	hp = max_hp
	# 绑定DetectArea的信号
	detect_area.body_entered.connect(_on_player_detected)
	detect_area.body_exited.connect(_on_player_lost)
	attack_hitbox.monitoring = false #攻击框默认关闭

func _on_player_detected(body:Node2D):
	if body.is_in_group("player"): #玩家节点必须加入player分组
		player = body

func _on_player_lost(body:Node2D):
	if body == player:
		player = null

	var dist: float = global_position.distance_to(player.global_position)
	
	# 1.离玩家远 → 追玩家
	if dist > attack_range:
		var dir = (player.global_position - global_position).normalized()
		velocity = dir * move_speed
		move_and_slide()
	# 2.进入攻击范围，冷却好了 → 普攻
	else:
		velocity = Vector2.ZERO
		if attack_timer <= 0.0:
			attack()
			attack_timer = attack_cd

# 【通用普攻函数，所有僵尸共用】
func attack():
	print("僵尸攻击，伤害：", attack_damage)
	# 这里写攻击逻辑：射线/碰撞盒检测玩家，扣血
	# if 命中玩家: player.take_damage(attack_damage)

# 受伤函数
func take_damage(dmg:int):
	hp -= dmg
	if hp <= 0:
		die()

# 死亡函数，生成掉落物
func die():
	if drop_item:
		var item = drop_item.instantiate()
		get_parent().add_child(item)
		item.global_position = global_position
	queue_free()
