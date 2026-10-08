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

背包与商店沿用羊皮纸、物品格和美术按钮，扩大面板、字号并分开图标、数量与详情。角色基础移速保持 500px/s。子弹购买数量按组计算：1 组为 10 发，2 组为 20 发，600 财宝/组。获得的子弹进入后备弹药，按 R 或背包中的“装填弹药”补入弹膛；HUD 的 `6/20` 分别表示弹膛与后备。背包内也可用 E / Q / Z，无法产生效果时会显示原因且不扣物品。小地图恢复原图、玩家图标和默认显示状态，按 M 开关。危险机关与开门机关按地砖对齐，显示在人物下方并可踩踏。地图布设 16 口实体棺材、4 个实体宝箱、12 块危险机关，包含全部六类僵尸。弓箭、暗箭使用透明箭矢，毒箭为红紫色；踩开门机关后显示打开的门洞和门扇。
镜头以 1.35 倍缩放跟随主角，让角色更清晰并收窄可见地图范围；棺材贴墙放置并使用收窄后的实体脚印。被墙和僵尸夹住时，僵尸会在真实可用空间内短距离让步，保留双方碰撞和受伤逻辑。

主角新增四向开火、后坐、枪口闪光和受击动作。六种僵尸支持出现、移动、攻击、受击、死亡动画；出棺位置避开玩家、墙体、道具与机关。流血和中毒改用独立短音并分别降低至 -22/-24 dB，不再每秒重复完整受击音。宝箱、开棺、摸棺、阴掌柜的交互文案每次打开时从 lang 对应分类随机选取，确认框打开期间保持不变。

6 个上传素材包的 122 个文件已全部加入 `assets/user/`，并接入界面、图标、角色、僵尸与道具。文件列表、完整性校验和素材来源见 [素材说明](docs/asset-sources.md)。

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
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/world_expansion_regression.tscn
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/player_feedback_regression.tscn
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/enemy_presentation_regression.tscn
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/interaction_text_regression.tscn
Godot_v4.7.2-stable_mono_linux.x86_64 --headless --script res://tests/ui_layout_regression.gd
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/coffin_layout_regression.tscn
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/crowding_regression.tscn
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/prop_feedback_regression.tscn
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/start_loading_regression.tscn
```

检查直接加载实际游戏场景，失败退出码为 1，成功打印 `RESULT` 和检查数量。覆盖弹药、攻速和暴击、治疗、购买成功与失败、独立状态、交互取消与滚轮、真实子弹命中、棺材一次性奖励、机关、结算、重开、暂停锁与窗口边界。专项检查还覆盖 GUI 焦点下快捷键、原美术按钮的真实鼠标点击、500px/s 实际移动、透明箭矢场景、小地图投影、打开门洞、16 方位瞬移安全落点，以及六种僵尸的五类动画。新增专项还覆盖实体阻挡、入口到出口可行路线、上下贴身近战、出现安全落点、状态低音量、随机原表文案与新排版真实鼠标操作。

策划实现对应、数值取舍与后续调平衡项见 [策划实现说明](docs/design-implementation.md)。
