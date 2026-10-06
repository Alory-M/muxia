extends Area2D

# 棺材只负责：玩家进入碰撞箱后发一个信号，通知里面的僵尸"开门了"。
# 生成/现身、追击、攻击、限范围等逻辑全部在僵尸脚本里，棺材不碰。

## 玩家进入碰撞箱后发出。僵尸脚本连上它，收到后自行现身并开始追击。
signal player_entered(player: Node2D)

@onready var closed: Sprite2D = $closed
@onready var open_sprite: Sprite2D = $open
@onready var detect_area: Area2D = $detect_area

var _has_triggered := false

func _ready() -> void:
	closed.visible = true
	detect_area.body_entered.connect(_on_player_enter)

func _on_player_enter(body: Node2D) -> void:
	if _has_triggered or not body.is_in_group("player"):
		return
	_has_triggered = true
	# 开棺
	closed.visible = false
	open_sprite.visible = true
	# 只发信号，其余交给僵尸脚本
	player_entered.emit(body)
