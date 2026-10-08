# 墓下

Godot 4.7.2 .NET 古墓探险游戏。探索、开棺战斗、摸棺收集财宝，在阴掌柜处购买物资和本局增益，踩下开门机关后携财宝逃出古墓。

本次玩法完善、问题修复与验证结果见 [更新记录](CHANGELOG.md)。

## 开发与运行

需要 Godot **4.7.2 .NET 版**与 **.NET 8 SDK**，普通版 Godot 不能运行 C# 脚本。

```bash
# 在仓库根目录执行
Godot_v4.7.2-stable_mono_linux.x86_64 --headless --editor --import
dotnet build '枪械预设.csproj' --nologo
Godot_v4.7.2-stable_mono_linux.x86_64 --editor
```

本云环境可先执行 `source /workspace/.setup/muxia-env.sh`，随后以 `"$GODOT"` 调用已安装的引擎。编辑器按 F6 运行当前场景，F5 运行主菜单。

## 操作

| 按键 | 操作 |
| --- | --- |
| WASD | 移动 |
| J / 鼠标左键 | 朝鼠标方向射击 |
| Shift / 鼠标右键 | 沿移动方向冲刺 |
| F | 与最近的宝箱、棺材或商人交互；在确认框中确认当前选项 |
| 滚轮 | 在交互确认框中切换“确认 / 离开” |
| B | 开关背包 |
| M | 开关小地图 |
| R | 从备用子弹装填六发弹匣 |
| E / Q / Z | 药品回血 / 绷带止血 / 解毒剂解除中毒 |
| Esc | 返回当前窗口上一层；无窗口时打开设置 |

背包与商店沿用第一版美术、格子和按钮，角色基础移速恢复为 500px/s。子弹购买数量按组计算：1 组为 10 发，2 组为 20 发，600 财宝/组。获得的子弹进入后备弹药，按 R 或背包中的“装填弹药”补入弹膛；HUD 的 `6/20` 分别表示弹膛与后备。背包内也可用 E / Q / Z，无法产生效果时会显示原因且不扣物品。小地图恢复原图、玩家图标和默认显示状态，按 M 开关。暗箭机关使用新生成的透明箭矢图像；踩开门机关后显示打开的门洞和门扇。

## 回归检查

```bash
dotnet build '枪械预设.csproj' --nologo
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/gameplay_regression.tscn
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/player_regression.tscn
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/ammo_regression.tscn
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/enemy_regression.tscn
Godot_v4.7.2-stable_mono_linux.x86_64 --headless --script res://tests/map_music_regression.gd
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/ui_restoration_regression.tscn
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/presentation_regression.tscn
Godot_v4.7.2-stable_mono_linux.x86_64 --headless --script res://tests/minimap_restoration_regression.gd
Godot_v4.7.2-stable_mono_linux.x86_64 --headless --script res://tests/gate_open_regression.gd
```

检查直接加载实际游戏场景，失败退出码为 1，成功打印 `RESULT` 和检查数量。覆盖弹药、攻速和暴击、治疗、购买成功与失败、独立状态、交互取消与滚轮、真实子弹命中、棺材一次性奖励、机关、结算、重开、暂停锁与窗口边界。专项检查还覆盖 GUI 焦点下快捷键、原美术按钮的真实鼠标点击、500px/s 实际移动、透明箭矢场景、小地图投影、打开门洞、16 方位瞬移安全落点，以及六种僵尸的受击与死亡动画。

策划实现对应、数值取舍与后续调平衡项见 [策划实现说明](docs/design-implementation.md)。
