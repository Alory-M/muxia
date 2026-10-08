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

func playing_music(node: Node) -> int:
    var count := 0
    if (node is AudioStreamPlayer or node is AudioStreamPlayer2D) and node.bus == &"Music" and node.playing:
        count += 1
    for child in node.get_children():
        count += playing_music(child)
    return count

func run():
    var title = load("res://scenes/main.tscn").instantiate()
    root.add_child(title)
    current_scene = title
    await frames(4)
    var title_music: AudioStreamPlayer = title.get_node("bgm")
    check(title_music.playing and title_music.bus == &"Music" and title_music.stream.loop, "开始界面 BGM 自动循环，使用 Music 总线")
    check(playing_music(root) == 1, "开始界面只播放一首 BGM")
    title.get_node("HUD/Start/start").emit_signal("pressed")
    await frames(8)

    var game = current_scene
    var player = game.get_node("player")
    var map = game.get_node("HUD/Map")
    var gate = game.get_node("开门机关")
    var trigger = gate.get_node("CollisionShape2D")
    var marker = map.get_node("GateMarker")
    var world: Rect2 = game.get_node("background/TextureRect").get_global_rect()
    var ratio: Vector2 = (trigger.global_position - world.position) / world.size
    var expected: Vector2 = (ratio - Vector2(0.5, 0.5)) * map.texture.get_size()
    check(marker.position.distance_to(expected) < 0.1, "小地图机关标记对应实际踩踏位置")
    check(map.get_node("GateLegend").text.contains("未触发"), "小地图显示机关未触发图例")
    check(game.get_node("background").z_index < gate.z_index and gate.z_index < player.z_index, "机关绘制在地板上、人物下方")
    check(gate.monitoring and trigger.disabled == false, "调整图层后机关仍检测踩踏")
    check(playing_music(root) == 1 and game.get_node("bgm").playing, "开始游戏切换为单首探索 BGM")

    player.global_position = trigger.global_position
    await frames(6)
    check(not game.get_node("终点大门").visible and game.get_node("endarea").monitoring, "玩家实际踩下地板机关打开墓门")
    check(map.get_node("GateLegend").text.contains("已开启"), "机关触发后小地图标记更新为已开启")
    game.get_node("endarea").emit_signal("body_entered", player)
    await frames(4)
    var result = game.get_node("HUD/end")
    var victory_music: AudioStreamPlayer = result.get_node("ScreenBgm")
    check(result.visible and victory_music.playing and victory_music.bus == &"Music", "成功通关界面播放独立 BGM")
    check(victory_music.stream is AudioStreamWAV and victory_music.stream.loop_mode == AudioStreamWAV.LOOP_FORWARD, "成功通关 BGM 使用无缝循环")
    check(not game.get_node("bgm").playing and playing_music(root) == 1, "结算 overlay 停止探索曲且只播放成功曲")
    result.get_node("continue").emit_signal("pressed")
    await frames(8)
    check(current_scene.get_node("bgm").playing and playing_music(root) == 1, "继续探索切换回单首探索 BGM")

    current_scene.get_node("player").call("TakeDamage", 10000.0)
    await frames(8)
    var defeat = current_scene
    var defeat_music: AudioStreamPlayer = defeat.get_node("ScreenBgm")
    check(defeat_music.playing and defeat_music.bus == &"Music", "失败界面播放独立 BGM")
    check(defeat_music.stream is AudioStreamWAV and defeat_music.stream.loop_mode == AudioStreamWAV.LOOP_FORWARD, "失败界面 BGM 循环")
    check(playing_music(root) == 1, "失败界面单首 BGM，不残留探索曲")
    defeat.get_node("tomain").emit_signal("pressed")
    await frames(8)
    check(current_scene.get_node("bgm").playing and playing_music(root) == 1, "失败返回主菜单恢复开始 BGM")

    current_scene.queue_free()
    current_scene = null
    await frames(4)
    await create_timer(0.2).timeout
    var cleanup = load("res://tests/GameplayRegression.cs").new()
    cleanup.call("CollectManagedResources")
    cleanup.free()
    await process_frame
    print("MAP_MUSIC_RESULT passed=%d failed=%d" % [passes, failures])
    quit(0 if failures == 0 else 1)
