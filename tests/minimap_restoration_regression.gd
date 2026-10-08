extends SceneTree

var failures := 0
var passes := 0

func _init():
    call_deferred("run")

func frames(count: int):
    for index in range(count):
        await physics_frame

func check(condition: bool, label: String):
    if condition:
        passes += 1
        print("PASS ", label)
    else:
        failures += 1
        push_error("FAIL " + label)

func press_map():
    var event := InputEventAction.new()
    event.action = &"M"
    event.pressed = true
    Input.parse_input_event(event)
    Input.flush_buffered_events()
    await frames(3)
    event = InputEventAction.new()
    event.action = &"M"
    event.pressed = false
    Input.parse_input_event(event)
    Input.flush_buffered_events()
    await frames(3)

func run():
    var game = load("res://scenes/game.tscn").instantiate()
    root.add_child(game)
    current_scene = game
    await frames(4)
    var map: Sprite2D = game.get_node("HUD/Map")
    var marker: Sprite2D = map.get_node("in")
    var player: Node2D = game.get_node("player")
    var world: Rect2 = game.get_node("background/TextureRect").get_global_rect()
    check(map.position.is_equal_approx(Vector2(110.5, 78)), "小地图恢复原位置 (110.5, 78)")
    check(map.scale.is_equal_approx(Vector2(0.22777778, 0.22666667)), "小地图保持原显示尺寸")
    check(map.texture.resource_path == "res://界面/map new.png", "小地图沿用原地图图像")
    check(marker.texture.resource_path == "res://界面/主角图标（用于小地图显示位置）.png", "小地图沿用原玩家图标")
    check(marker.z_index == 0, "玩家图标恢复原图层")
    check(map.get_child_count() == 2 and map.has_node("base_map"), "小地图只有原玩家图标与外框")
    check(not map.has_node("GateMarker") and not map.has_node("GateLegend"), "移除新增机关标记和图例")
    check(map.visible, "小地图恢复原默认显示状态")
    await press_map()
    check(not map.visible, "首次按 M 正常关闭小地图")
    await press_map()
    check(map.visible, "再次按 M 正常打开小地图")

    # 四个不同位置验证世界坐标投影，边界外位置会夹到地图边缘。
    player.set_physics_process(false)
    var samples := [Vector2(0, 0), Vector2(0.25, 0.75), Vector2(0.5, 0.5), Vector2(1.2, -0.2)]
    for index in range(samples.size()):
        var ratio: Vector2 = samples[index]
        player.global_position = world.position + ratio * world.size
        await frames(2)
        var expected: Vector2 = (ratio.clamp(Vector2.ZERO, Vector2.ONE) - Vector2(0.5, 0.5)) * map.texture.get_size()
        check(marker.position.distance_to(expected) < 0.1, "玩家坐标投影正确：位置 %d" % (index + 1))

    game.queue_free()
    current_scene = null
    await frames(4)
    await create_timer(0.2).timeout
    var cleanup = load("res://tests/GameplayRegression.cs").new()
    cleanup.call("CollectManagedResources")
    cleanup.free()
    await process_frame
    print("MINIMAP_RESTORATION_RESULT passed=%d failed=%d" % [passes, failures])
    quit(0 if failures == 0 else 1)
