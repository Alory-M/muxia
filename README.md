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

背包与商店沿用羊皮纸、物品格和美术按钮，图标、数量与详情分开排版；金币和灵魂类别图标放大至边栏宽度。角色基础移速保持 500px/s，六种僵尸的实际移动速度实时跟随主角行走速度（含增益和中毒，不跟随冲刺）。金币商品 150 财宝/组、灵魂增益 5 碎片/份，商品不限量，仍检查余额、整数容量和暴击 100% 上限。子弹 1 组为 10 发，2 组为 20 发，只进入后备弹药；按 R 或背包中的“装填弹药”补入弹膛，HUD 的 `6/20` 分别表示弹膛与后备。数量为零的物品图标隐藏，重新获得后恢复。小地图保持原图、玩家图标和默认显示状态，按 M 开关。

地图布设 24 口贴墙棺材（六类僵尸各四只）、4 个实体宝箱、12 块危险机关。进入棺材 110px 范围且不隔墙时，僵尸自动跳出；打开的棺材立即取消碰撞。击败守卫后，棺材显示冰蓝白脉动发光，按 F 摸棺领取一次财宝，并独立概率获得 8 发后备子弹（45%）、药品（25%）、绷带（30%）、解毒剂（20%），后三种每次各 1 件。HUD 显示棺材总数、未开及可摸数量。危险机关与开门机关仍是地砖的一部分，位于人物下方可踩踏；暗箭每轮沿三条平行线齐射，十轮共 30 支箭。毒箭保持红紫色，墓门触发后显示打开状态。

镜头拉近至 2.4 倍，可见区域随玩家移动，直径 220 世界像素；外围完全遮蔽，HUD 和模态界面不被遮挡。这里放大的是画面，不改变地图碰撞坐标。被墙和僵尸夹住时，僵尸仍会在真实可用空间内短距离让步，保留双方碰撞和受伤逻辑。

初始弹药为弹膛 6 发、后备 60 发；流血和中毒各每秒扣 5 点生命。弓箭与毒箭僵尸攻击距离为 480px，瞬移僵尸领地半径为 960px。

主角支持四向开火、后坐、枪口闪光和受击动作。六种僵尸的移动和攻击使用基于原画新生成的独立图集，每种四帧移动、四帧攻击，资源位于 `assets/generated/zombie_motion/`；出现、受击、死亡保留原画过渡动画，所有动作均不修改碰撞体。出棺位置避开玩家、墙体、道具与机关。流血和中毒使用独立短音并分别降低至 -22/-24 dB，不每秒重复完整受击音。宝箱、摸棺、阴掌柜的交互文案从 lang 对应分类随机选取，确认框期间保持不变。

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
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/coffin_automation_regression.tscn
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/inventory_hud_regression.tscn
Godot_v4.7.2-stable_mono_linux.x86_64 --headless res://tests/zombie_motion_regression.tscn
```

检查直接加载实际游戏场景，失败退出码为 1，成功打印 `RESULT` 和检查数量。覆盖弹药、攻速和暴击、治疗、购买成功与失败、独立状态、交互取消与滚轮、真实子弹命中、棺材一次性奖励、机关、结算、重开、暂停锁与窗口边界。专项检查还覆盖 GUI 焦点下快捷键、原美术按钮的真实鼠标点击、500px/s 实际移动、透明箭矢场景、小地图投影、打开门洞、16 方位瞬移安全落点，以及六种僵尸的五类动画。新增专项还覆盖实体阻挡、入口到出口可行路线、上下贴身近战、出现安全落点、状态低音量、随机原表文案与新排版真实鼠标操作。

策划实现对应、数值取舍与后续调平衡项见 [策划实现说明](docs/design-implementation.md)。
