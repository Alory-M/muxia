extends Control
## 血条长度控制器:按 player 的血量比例缩放 hp_bar 的宽度。
##
## 思路跟 ColorRect.cs 一致——不碰 TextureProgressBar 的 value,
## 直接改 size.x:编辑器里 hp_bar 摆多宽,就代表满血(MaxHp)的长度;
## 掉血后按 hp / MaxHp 的比例把这段长度缩掉,右端往回收。

@onready var hp_bar: TextureProgressBar = $hp_bar
@onready var hp_label: Label = $hp

# 满血时该多长:_ready 时取编辑器里的预设宽度,只读一次。
# 每帧重读会把"已经缩过的宽度"当成新的满血长度,越缩越短。
var _full_width: float = 0.0

# 玩家用 "player" 组找。HUD 和 player 是兄弟节点,不是父子,
# 数斜杠容易错(../player 指向 HUD/player,根本不存在);
# 组名不随层级变动,场景改结构也照样找得到。
var _player = null

func _ready() -> void:
	_full_width = hp_bar.size.x
	# 宽度既然交给脚本了,就让进度条自身恒为满格,
	# 免得 value / max_value 再叠一层裁剪,变成双重缩水。
	hp_bar.max_value = 1.0
	hp_bar.value = 1.0
	_player = get_tree().get_first_node_in_group("player")

func _process(_delta: float) -> void:
	if _player == null:
		# 玩家可能是后加进场景的,补找一次,别让血条整局不动
		_player = get_tree().get_first_node_in_group("player")
		if _player == null:
			return

	var max_hp = _player.MaxHp
	# MaxHp 为 0 或负数时不给比例,直接当空血,顺便避开除零
	var ratio = 0.0 if max_hp <= 0.0 else clampf(_player.hp / max_hp, 0.0, 1.0)

	hp_bar.size = Vector2(_full_width * ratio, hp_bar.size.y)
	hp_label.text = "%d / %d" % [int(_player.hp), int(max_hp)]
