extends SceneTree

var checks := 0
var failures := 0

func _initialize():
    call_deferred("run")

func verify(condition: bool, message: String):
    checks += 1
    if condition:
        print("PASS: ", message)
    else:
        failures += 1
        push_error("UI REGRESSION: " + message)

func frames(count := 2):
    for unused in count:
        await process_frame

func click(control: Control):
    var point := control.get_global_rect().get_center()
    var motion := InputEventMouseMotion.new()
    motion.position = point
    motion.global_position = point
    root.push_input(motion, true)
    var press := InputEventMouseButton.new()
    press.position = point
    press.global_position = point
    press.button_index = MOUSE_BUTTON_LEFT
    press.button_mask = MOUSE_BUTTON_MASK_LEFT
    press.pressed = true
    root.push_input(press, true)
    await process_frame
    var release := InputEventMouseButton.new()
    release.position = point
    release.global_position = point
    release.button_index = MOUSE_BUTTON_LEFT
    release.pressed = false
    root.push_input(release, true)
    await frames()

func paper_fits(owner: Control, message: String):
    var background: Sprite2D = owner.get_node("background")
    var corners := background.get_global_transform() * background.get_rect()
    var viewport_size := root.get_visible_rect().size
    verify(corners.position.x >= 0 and corners.position.y >= 0 and corners.end.x <= viewport_size.x and corners.end.y <= viewport_size.y, message)

func text_fits(label: Label, message: String):
    verify(label.get_theme_font_size("font_size") >= 16 and label.get_line_count() * label.get_line_height() <= label.size.y, message)

func run():
    change_scene_to_file("res://scenes/game.tscn")
    await frames(8)
    var game := current_scene
    var player := game.get_node("player")
    var pack := player.get_node("pack")
    var bag: Control = game.get_node("HUD/packsys")
    var shop_ui: Control = game.get_node("HUD/store")
    var shop := shop_ui.get_node("store")
    bag.SetOpen(true)
    await frames()
    paper_fits(bag, "背包纸面完整显示")
    verify(bag.get_node("background").texture.resource_path == "res://assets/user/interface/bag_inter.png", "接入用户背包羊皮纸素材")
    verify(bag.get_node("未点击").texture.resource_path == "res://assets/user/icon/button_close_noclick.png", "背包使用用户关闭按钮")
    for path in ["button11", "button12", "button13", "button14", "button21", "button22"]:
        var slot: Button = bag.get_node(path)
        var icon: Sprite2D = slot.get_node("image")
        var counter: Label = slot.get_node("counter")
        var icon_bounds := icon.get_transform() * icon.get_rect()
        verify(icon_bounds.end.y <= counter.position.y, "背包 " + path + " 图标与数量分开")
        verify(counter.get_theme_font_size("font_size") >= 18, "背包 " + path + " 数量可读")
    pack.TryConsume(0, 3)
    bag.RefreshItems()
    var before_reserve: int = pack.GetReserveBullet()
    await click(bag.get_node("button14"))
    verify(bag.get_node("UseItem").visible and not bag.get_node("UseItem").disabled, "真实鼠标点击子弹格选中装填")
    text_fits(bag.get_node("description"), "子弹完整说明适合详情区")
    await click(bag.get_node("UseItem"))
    verify(pack.GetCount(0) == 6 and pack.GetReserveBullet() == before_reserve - 3, "点击背包装填按钮正确消费后备弹药")
    await click(bag.get_node("button11"))
    verify(bag.get_node("itemname").text == "药品", "真实鼠标点击药品格切换详情")
    verify(bag.get_node("UseItem").disabled, "满血药品仍禁止无效消耗")
    text_fits(bag.get_node("description"), "药品说明及不可用原因完整显示")
    await click(bag.get_node("close2"))
    verify(not bag.visible, "实际点击背包关闭按钮有效")
    shop.SetOpen(true)
    await frames()
    paper_fits(shop_ui, "商店纸面完整显示")
    verify(shop_ui.get_node("background").texture.resource_path == "res://assets/user/interface/shop_inter.png", "接入用户商店羊皮纸素材")
    verify(shop_ui.get_node("SuppliesTab/Artwork").texture.resource_path == "res://assets/user/icon/shop_coin_click.png", "财宝类别使用用户选中金币按钮")
    for index in 4:
        text_fits(shop_ui.get_node("ProductDescription" + str(index + 1)), "商店第 " + str(index + 1) + " 列效果说明完整显示")
    var quantity := shop_ui.get_node("购买子弹数量")
    await click(quantity.get_node("增"))
    verify(quantity.Count == 2, "扩大后的数量加号可实际点击")
    var normal: Sprite2D = quantity.get_node("正常4")
    var pressed_plus: Sprite2D = quantity.get_node("点加4")
    var pressed_minus: Sprite2D = quantity.get_node("点减4")
    verify(normal.texture.resource_path == "res://assets/user/icon/button_add.sub_noclick.png" and pressed_plus.texture.resource_path == "res://assets/user/icon/button_add_click.png", "接入用户数量按钮常态和按下图")
    verify(pressed_plus.position.x > normal.position.x and pressed_minus.position.x < normal.position.x and (pressed_plus.get_transform() * pressed_plus.get_rect()).size.x < 30, "单圆钮按下图与加减热点对齐")
    player.get_node("gold").Amount = 1800
    before_reserve = pack.GetReserveBullet()
    await click(shop_ui.get_node("买子弹"))
    verify(pack.GetReserveBullet() == before_reserve + 20 and pack.GetCount(0) == 6, "商店实际点击购买两组二十发加入后备")
    verify(player.get_node("gold").Amount == 1500, "金币商品每组一百五十，两组扣三百")
    text_fits(shop_ui.get_node("PurchaseFeedback"), "购买反馈完整显示")
    await click(shop_ui.get_node("BuffsTab"))
    verify(shop_ui.get_node("药品").text != "药品" and shop_ui.get_node("ProductDescription1").text.contains("攻击"), "实际点击灵魂增益切换分类")
    verify(shop_ui.get_node("BuffsTab/Artwork").texture.resource_path == "res://assets/user/icon/shop_soul_click.png", "灵魂类别使用用户选中按钮")
    var buff_nodes := ["drug", "Antidote", "绷带", "Bullet"]
    var buff_icons := ["icon_attack", "icon_move_speed", "icon_attack_speed", "icon_critical"]
    for index in 4:
        text_fits(shop_ui.get_node("ProductDescription" + str(index + 1)), "增益第 " + str(index + 1) + " 列效果说明完整显示")
        var icon: Sprite2D = shop_ui.get_node(buff_nodes[index])
        verify(icon.texture.resource_path == "res://assets/user/icon/" + buff_icons[index] + ".png" and icon.region_enabled, "增益第 " + str(index + 1) + " 列使用独立用户图标并裁切透明边距")
    await click(shop_ui.get_node("SuppliesTab"))
    verify(shop_ui.get_node("药品").text == "药品", "实际点击财宝商店切回物资")
    await click(shop_ui.get_node("close2"))
    verify(not shop_ui.visible, "实际点击商店关闭按钮有效")
    game.queue_free()
    await frames(4)
    await create_timer(0.2).timeout
    var cleanup = load("res://tests/GameplayRegression.cs").new()
    cleanup.CollectManagedResources()
    cleanup.free()
    await frames()
    print("UI RESULT: ", checks - failures, " passed, ", failures, " failed")
    quit(1 if failures else 0)
