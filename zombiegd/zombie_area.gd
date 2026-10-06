extends CharacterBody2D
class_name Zombie # 定义类名，别的脚本可以直接 extends Zombie

# 僵尸本体基类：负责追击、近战、受伤、死亡掉落。
# 「检测玩家→开棺→生成僵尸」由 coffin.gd 负责，这里不再重复（避免和棺材重复触发）。
# 玩家引用：coffin.gd 生成时会 set 进来；直接摆在场景里的僵尸，本脚本会按 player 分组自动查找。

# ========== 全部僵尸可配置属性（Inspector面板直接改） ==========
@export var max_hp: int = 100          #血量
@export var attack_damage: int = 10    #伤害
@export var move_speed: float = 80.0   #移速
@export var attack_cd: float = 1.2     #攻击冷却
@export var attack_range: float = 60.0 #攻击距离
@export var drop_item: PackedScene     #死亡掉落物（拖入场景资源）
@export var soul_drop: int = 1         #死亡掉落的灵魂碎片数量
@export var spawn_offset: Vector2 = Vector2(64, 0)  #预留：出棺后相对棺材的位置（coffin.gd 定位用）

# 内部变量
var hp: int
var attack_timer: float = 0.0
var player: Node2D = null   #玩家引用：coffin.gd 生成时 set 进来，或本脚本按分组自动查找
var coffin: Node2D = null   #所属棺材（可选）：coffin.gd 生成僵尸时会 set 进来

func _ready() -> void:
	hp = max_hp
	attack_timer = 0.0
	# 没被棺材设置过玩家（例如直接摆在场景里的僵尸），按 player 分组找玩家
	_ensure_player()

# 拿到有效的玩家引用；玩家为空或已失效时，按 player 分组重新找
func _ensure_player() -> bool:
	if player == null or not is_instance_valid(player):
		player = get_tree().get_first_node_in_group("player")
	return player != null

func _physics_process(delta: float) -> void:
	if not _ensure_player():
		return
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
	# 兼容 C# 玩家(PascalCase 的 TakeDamage)和 GDScript 玩家(snake_case 的 take_damage)
	if player.has_method("TakeDamage"):
		player.TakeDamage(float(attack_damage))
	elif player.has_method("take_damage"):
		player.take_damage(attack_damage)
	print("僵尸攻击，伤害：", attack_damage)

# 受伤函数
func take_damage(dmg: int) -> void:
	hp -= dmg
	if hp <= 0:
		die()

# 死亡函数：灵魂碎片直接进背包，可选的 drop_item 场景仍会在地上生成
func die() -> void:
	drop_soul()
	if drop_item:
		var item := drop_item.instantiate()
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
	# 灵魂仓库是 player/soulpiece（C# soulpiece.cs），方法叫 AddSoul
	var soul := player_node.get_node_or_null("soulpiece")
	if soul == null or not soul.has_method("AddSoul"):
		push_warning("Zombie: 找不到灵魂仓库(player/soulpiece)或没有 AddSoul 方法，灵魂碎片丢失")
		return
	soul.AddSoul(soul_drop)
	print("掉落灵魂碎片 x", soul_drop)
