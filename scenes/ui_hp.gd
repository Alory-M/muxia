extends Control

@onready var hp_bar: TextureProgressBar = $hp_bar
@onready var hp_label: Label = $hp
@onready var player = $"../player"

func _process(_delta: float) -> void:
	if player == null:
		return

	hp_bar.max_value = player.MaxHp
	hp_bar.value = player.hp
	hp_label.text = "%d / %d" % [int(player.hp), int(player.MaxHp)]
