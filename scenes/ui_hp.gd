extends Control

@onready var hp_bar: TextureProgressBar = $hp_bar
@onready var hp_label: Label = $hp

func _ready() -> void:
	update_hp(50, 100)

func update_hp(current_hp: int, max_hp: int) -> void:
	hp_bar.max_value = max_hp
	hp_bar.value = current_hp
	hp_label.text = "%d / %d" % [current_hp, max_hp]
