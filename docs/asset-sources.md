# 素材来源与接入

用户提供的 6 个分包已全部解包加入 `assets/user/`，保留原文件名和内容，共 **122 个文件**，117,720,004 字节。`manifest.json` 记录各包及文件的 SHA-256、大小，可执行 `python3 tools/verify_user_assets.py` 检查素材完整性。

| 素材包 | 文件数 | 项目目录 | 实际用途 |
| --- | ---: | --- | --- |
| item.zip | 16 | `assets/user/item/` | 物资、棺材、宝箱、机关、地刺、墓门 |
| icon.zip | 38 | `assets/user/icon/` | 物品和增益图标、按钮、血条、状态、小地图玩家标记 |
| interface.zip | 8 | `assets/user/interface/` | 背包、商店、地图、开始、成功与失败界面 |
| host1.zip | 19 | `assets/user/host1/` | 主角四向开火与受伤立绘，其余原图归档保留 |
| host2.zip | 28 | `assets/user/host2/` | 侧跑、正跑、背跑 PNG 帧序列；GIF 原文件一并保留 |
| monster.zip | 13 | `assets/user/monster/` | 六类僵尸普通/攻击立绘、阴掌柜 |

部分上传图片与原仓库图片完全相同。运行场景已改为引用 `assets/user/` 中的上传版本，不依靠仅凭画面相同判断素材是否接入。阴掌柜与僵尸按图片透明区域匹配身高、脚底和朝向，机关按图片透明边界裁切至单块地砖；原素材保持在原目录。

背包与商店使用上传羊皮纸、物品框和按钮，扩大面板与字号，图标、数量、库存、效果和详情分区。四种增益分别使用攻击、移速、攻速、暴击图标。主角使用上传开火/受伤立绘，加后坐、枪口火光、红闪；僵尸使用上传普通/攻击立绘，加出现、步态、出手、受击与死亡动画。

素材包没有提供音频。流血和中毒的 `music/status_bleed.wav` / `status_poison.wav` 为本次原创轻心跳/气泡短音，分别以 -22/-24 dB 播放，可通过 `tools/create_status_audio.py` 重建。成功/失败循环音乐来自此前原创 `BGM_victory.wav` / `BGM_defeat.wav`。

暗箭和弓箭复用此前按用户要求生成的透明箭图 `bin/projectiles/trap_arrow.png`；毒箭通过 `poison_arrow.gdshader` 呈现红色箭头、紫色箭杆，匹配上传的毒箭僵尸立绘。交互原文仍来自用户策划表 `data/lang.json`。
