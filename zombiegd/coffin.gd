extends Area2D

@export var zombie_prefab: PackedScene # 拖入你的 zombie.tscn

var has_spawned := false

@onready var closed: Sprite2D = $closed
@onready var open_sprite: Sprite2D = $open
@onready var detect_area: Area2D = $detect_area

func _ready():
	detect_area.body_entered.connect(_on_player_enter)

func _on_player_enter(body: Node2D) -> void:
	if has_spawned or not body.is_in_group("player"):
		return
	has_spawned = true
	# 开棺
	closed.visible = false
	open_sprite.visible = true
	# 生成僵尸：挂到场景根节点，别做成棺材子节点(免得跟着棺材动)
	if zombie_prefab == null:
		push_warning("coffin: 没有设置 zombie_prefab，僵尸无法生成")
		return
	var zombie := zombie_prefab.instantiate()
	get_parent().add_child(zombie)
	zombie.global_position = global_position
	# 把棺材和玩家交给僵尸，让它开始追击
	zombie.set("coffin", self)
	zombie.set("player", body)
