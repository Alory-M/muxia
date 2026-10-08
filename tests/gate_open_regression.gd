extends SceneTree

var count = 0
var failures = 0

func _initialize():
    call_deferred("run")

func frames():
    for i in range(8):
        await physics_frame

func check(ok, label):
    if not ok:
        failures += 1
        push_error(label)
        return
    count += 1
    print("PASS: ", label)

func run():
    change_scene_to_file("res://scenes/game.tscn")
    await frames()
    var game = current_scene
    var player = game.get_node("player")
    var trigger = game.get_node("开门机关")
    var door = game.get_node("终点大门")
    var exit = game.get_node("endarea")
    var result = game.get_node("HUD/end")
    player.global_position = exit.get_node("CollisionShape2D").global_position
    await frames()
    check(not result.visible and not exit.monitoring and not trigger.get("IsOpen"), "关闭墓门不能提前通关")
    player.global_position = trigger.get_node("CollisionShape2D").global_position
    await frames()
    check(trigger.get("IsOpen") and exit.monitoring and door.visible and door.get_node("OpenGateVisual").visible, "实际踩踏机关显示打开门洞")
    check(game.get_node("background").z_index < door.z_index and door.z_index < player.z_index, "门洞和门扇位于背景上方人物下方")
    player.global_position = exit.get_node("CollisionShape2D").global_position
    await frames()
    check(result.visible, "进入与打开门洞对齐的出口实际通关")
    game.queue_free()
    current_scene = null
    await frames()
    await create_timer(0.2).timeout
    var cleanup = load("res://tests/GameplayRegression.cs").new()
    cleanup.CollectManagedResources()
    cleanup.free()
    await process_frame
    print("GATE RESULT: %d passed, %d failed" % [count, failures])
    quit(0 if failures == 0 else 1)
