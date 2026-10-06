extends CharacterBody2D
class_name Zombie # 定义类名，别的脚本可以直接 extends Zombie

# ========== 全部僵尸可配置属性（Inspector面板直接改） ==========
@export var max_hp: int = 100          #血量
@export var attack_damage: int = 10    #伤害
@export var move_speed: float = 80.0   #移速
@export var attack_cd: float = 1.2     #攻击冷却
@export var attack_range: float = 60.0 #攻击距离
@export var drop_item: PackedScene     #死亡掉落物（拖入场景资源）
@export var soul_drop: int = 1         #死亡掉落的灵魂碎片数量
@export var spawn_offset: Vector2 = Vector2(64, 0)  #僵尸出棺后相对棺材的位置

# 内部变量
var hp: int
var attack_timer: float = 0.0
@onready var player: Node2D = null

@onready var detect_area: Area2D = $DetectArea
@onready var attack_hitbox: Area2D = $AttackHitbox
@onready var coffin_closed: Node2D = $Coffin/Closed  # 关闭的棺材
@onready var coffin_open: Node2D = $Coffin/Open      # 打开的棺材(占位)
@onready var body: Node2D = $Sprite2D                # 僵尸本体视觉

var is_spawned: bool = false  # 是否已从棺材出来

func _ready():
	hp = max_hp
	# 绑定DetectArea的信号
	detect_area.body_entered.connect(_on_player_detected)
	detect_area.body_exited.connect(_on_player_lost)
	attack_hitbox.monitoring = false #攻击框默认关闭
	# 初始状态：僵尸藏在棺材里，等玩家进入 DetectArea 再出来
	coffin_open.visible = false
	body.visible = false

func _on_player_detected(body:Node2D):
	if body.is_in_group("player"): #玩家节点必须加入player分组
		player = body
		spawn_from_coffin()  # 玩家来了，开棺放僵尸

func _on_player_lost(body:Node2D):
	if body == player:
		player = null

# 玩家进入检测范围：打开棺材，僵尸出现在棺材外并开始追击
func spawn_from_coffin() -> void:
	if is_spawned:
		return
	is_spawned = true
	coffin_closed.visible = false
	coffin_open.visible = true
	body.visible = true
	body.position = spawn_offset  # 僵尸站到棺材外
	print("棺材打开，僵尸出现！")

func _physics_process(delta: float) -> void:
	if not player: return
	attack_timer -= delta
	var dist: float = global_position.distance_to(player.global_position)
	# 1.离玩家远 → 追玩家
	if dist > attack_range:
		chase()
	# 2.进入攻击范围 → 普攻
	else:
		melee_attack()

# 追击玩家
func chase() -> void:
	var dir: Vector2 = (player.global_position - global_position).normalized()
	velocity = dir * move_speed
	move_and_slide()

# 原地普攻（冷却好了才打）
func melee_attack() -> void:
	velocity = Vector2.ZERO
	if attack_timer <= 0.0:
		attack()
		attack_timer = attack_cd

# 【通用普攻函数，所有僵尸共用】
func attack() -> void:
	if player == null:
		return
	# 距离太远打不到（容错比攻击距离多一点）
	if global_position.distance_to(player.global_position) > attack_range + 10.0:
		return
	if player.has_method("take_damage"):
		player.take_damage(attack_damage)
	print("僵尸攻击，伤害：", attack_damage)

# 受伤函数
func take_damage(dmg:int):
	if not is_spawned:
		return  # 还没从棺材出来，打不中
	hp -= dmg
	if hp <= 0:
		die()

# 死亡函数：灵魂碎片直接进背包，可选的 drop_item 场景仍会在地上生成
func die():
	drop_soul()
	if drop_item:
		var item = drop_item.instantiate()
		get_parent().add_child(item)
		item.global_position = global_position
	queue_free()

# 掉落灵魂碎片：直接加进玩家背包，不用玩家走过去捡
func drop_soul() -> void:
	if soul_drop <= 0:
		return
	var player_node := get_tree().get_first_node_in_group("player")
	if player_node == null:
		push_warning("Zombie: 找不到 player 分组节点，灵魂碎片丢失")
		return
	var store = player_node.get_node_or_null("store")
	if store == null or not store.has_method("AddSoul"):
		push_warning("Zombie: 找不到仓库(store)或没有 AddSoul 方法，灵魂碎片丢失")
		return
	store.AddSoul(soul_drop)
	print("掉落灵魂碎片 x", soul_drop)
