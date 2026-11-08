# Art & Presentation Features

现有模型、材质与轻量表现的制作规范，说明资源入口、外观识别与空间约定。玩法规则见 [GAMEPLAY](../GAMEPLAY.md)，索引见 [FEATURES](../FEATURES.md)。

## 风格与空间约定

- 极简方块大形、直角地块、硬边与纯色材质；不使用圆角地块、复杂纹理、碎苔藓或写实细节。
- 无雾，优先让玩家辨认格子、机关状态与朝向。外围布景比棋盘更低调，不使用相同密度的规则格纹。
- 默认格距 1.12、可见地块宽 1.08、层高 0.45，保留细格缝。
- 地块根节点是规则地表锚点；角色与物件根节点是落脚点。根节点保持单位缩放，几何与材质主要放在 Visual 下。
- 正面统一为本地 -Z。占格由逻辑配置决定，大模型越出一格不等于拥有多格占位。
- 视觉子节点不挂玩法实体或额外碰撞，点击盒由逻辑根节点负责。

## 我方角色

活动角色 Prefab 在 `Prefabs/PushBox/Players`，沿用一套简约人形，仅增加便于辨认职业与朝向的元素。

| 显示名 | Prefab | 外观 |
| --- | --- | --- |
| 力士 | Warrior.prefab | 红色身体、粗壮双臂，无剑；与盾兵接近的壮实体型 |
| 盗贼 | Thief.prefab | 绿色身体，腰侧钩子尖端朝下，另一侧保留药瓶外观 |
| 法师 | Mage.prefab | 蓝色身体、方帽檐与尖帽、简易法杖 |
| 盾兵 | Shieldbearer.prefab | 黄色身体、黑发和大盾 |

药瓶、法杖、大盾用于外观识别。角色能力以 [GAMEPLAY](../GAMEPLAY.md) 中的说明为准。

`Visual/Facing Eyes` 为正面的两条深色横线；`Visual/BackHair` 覆盖后脑并留出少量颈部肤色。无 Collider，跟随角色面向，不朝摄像机旋转。外观更新器仅补缺失细节，不重复添加或强制覆盖已有节点。

对应代码：[PushBoxActorAppearance](../../TD_PushBox/Scripts/Editor/Authoring/PushBoxActorAppearance.cs)、[PushBoxActor](../../TD_PushBox/Scripts/Runtime/PushBox/PushBoxActor.cs)。

## 地面与障碍

| 资源 | 路径或位置 | 规范 |
| --- | --- | --- |
| 平地五色 | Prefabs/Tiles/Tile_Flat_* | 草地、土地、浅灰共用规则，颜色不改变通行 |
| 高地 | Prefabs/PushBox/Objects/Object_Highland.prefab | 高出地表 0.75，独立障碍；薄苔藓顶为 #819B59，下方棕色土层，保留底层地面 |
| 深渊 | Prefabs/Tiles/Tile_Abyss.prefab | 无地面或盒状底板；编辑点击盒不代表支撑 |
| 石块 | Prefabs/PushBox/Objects/Object_Rock.prefab | 深灰直角块，高出地表约 0.7，独立叠放，不删除下方地面 |
| 灌木 | Prefabs/PushBox/Objects/Object_Shrub.prefab | 粗木树干与低矮深绿树丛，树干宽约 0.66、深约 0.64，不带底板 |
| 坡道 | Prefabs/Tiles/Tile_Ramp_* | 用于配置连接不同高度的通路，当前画笔列表不提供 |

深棕平地为 `#71583A`，浅灰为 `#A9AFA6`，深灰石块为 `#838B85`；其余配色以现有材质为准。高地使用独立暗苔藓材质 `MAT_Highland_Moss`，只在顶面保留薄层，不能让侧面整体变绿。

深渊下方不得铺连续装饰地板或底座遮住缺口。地块支撑由工具按格生成，是否需要底座受定义控制。高地、石块与灌木使用上表中的独立物件 Prefab，不自带地面。

## 机关与可移动物

- 木箱是木板箱，水晶是低面紫色晶簇。水晶不带底板，直接贴地；两者在 Movables 中归类，破坏规则不同。
- 压力板是石框踏面，普通门是石框木栅，避免现代灯板或科技面板造型。
- 封印石门单格，宽约 1.08、高约 1.40、厚 0.46；黑绿封印与方块邪气表示关闭，绿光表示解封，随后下降。
- 目标点使用石框与绿色角标，激活后亮绿，失败时黑色。
- 祭坛是单格低矮阶梯石台，宽深约 1.08、高约 0.68，两种灰石加少量苔绿。只保留外观与阻挡，不播放召唤。
- 花草为组合小模型，不拆草叶与花点；无碰撞，由覆盖率刷新贴合可用地表和石块顶面，不提供手动资源卡片。

机关主体与移动部件分开，门的视觉下降不负责决定通行。Prefab 保留 movingPart 等引用，修改外观时不能丢失逻辑组件或接线。

## 敌人与妖怪外观

近战守卫与持枪哨兵沿用绿头、橙身、深色脚的方块身体，以木棒/枪械区分。守卫四向 1 格，哨兵四向 2 格，具体规则见 GAMEPLAY。

三款妖怪 Prefab 在 `Prefabs/PushBox/Enemies`：

| Prefab | 识别元素 |
| --- | --- |
| Boss_WoodMask.prefab | 棕色古木身体、面具、绿色肩部、短角与獠牙 |
| Boss_RedOni.prefab | 红色壮身体、深色头发、浅色角和护腕 |
| Boss_MossGolem.prefab | 浅灰石质身体、深灰关节与少量绿色苔块 |

它们都是单格实体，身体可适量超出格宽，脚部仍落在本格内；攻击行为统一使用接触规则。

火龙石像资源为 `Prefabs/Objects/Threat_FireDragonStatue.prefab`：灰石、方角龙头、短角、折翼和尾巴；火焰为独立 Flame 子模型，运行时按射线长度缩放。喷火由规则结算结果驱动，模型只负责显示。

## 外围布景

Forest 复用树、岩、遗迹、花草；Castle 增加少量墙、塔与旗。四边使用大小、深度和外伸不一的直角台地，疏密不对称，近侧保持低矮。

开始与选关庭院包含溪流、桥、树木与遗迹，箱子是主题视觉元素。背景物体不注册棋盘实体，不放入可通行格或遮住近侧关键操作。

Env 中生成分组与独立手工布景分开管理。修改已有场景不应通过整场重生成来抹掉作者布局。

## 轻量表现与声音

| 表现 | 当前实现 |
| --- | --- |
| 普通移动与搬运 | 人物、物体按格同步平滑过渡；不是物理推挤 |
| 法师换位 | 双方直接交换位置，交换时播放短促魔法声 |
| 切人 | 新角色闪白 0.2 秒，旧角色恢复原颜色 |
| 选中光圈 | 原位置不抬高；独立 SelectionRing Shader 用较高绘制顺序避免被地面机关遮挡 |
| 木箱压碎 | 闪白 0.3 秒后隐藏，撤销后恢复 |
| 人物死亡 | 我方倒地，敌方隐藏 |
| 敌人命中我方 | 守卫、哨兵朝目标短促前冲并回位，总计 0.18 秒、最多 0.3 格；只作演出，不改变格点，随后呈现我方倒地 |
| 火龙喷火 | 短暂显示射线对应的块状火焰，再呈现死亡 |
| 通关提示 | 黄色、加粗、84 字号的“关卡完成！”，位置下移 |
| UI | 主菜单悬停/点击反馈、庭院列表展开动画、入口提示与地图消散 |

闪白使用 MaterialPropertyBlock，不改共享角色/木箱材质；选择光圈材质在 `Art/Materials/MAT_Selection_Ring.mat`，Shader 在 `Art/Shaders/SelectionRing.shader`。没有骨骼蒙皮或外部动画源文件，不能把当前表现描述成完整角色动画系统。

四名角色共用脚步声随机池，每次成功走动（包括推拉时的走动）在移动中途播放一次；换位只播放魔法音效。切人、无效移动和撤销不播放脚步声。推拉木箱或水晶时每次行动播放一次短摩擦声，两者共用音效及交替序列：单次用 `Audio/Gameplay/CrateMove1.wav`，连续按 `CrateMove1`、`CrateMove2` 交替播放。两次触发间隔超过“单格移动时间与连续间隔的较大值 + 0.15 秒”（至少 0.5 秒），或切人、无效移动、进行其他搬运、撤销、重开后，从第一段重新开始。连推多个物体不叠加播放，只有人物位移不触发摩擦声。默认配置在 `Resources/Audio/PushBoxActions.asset`，源素材在 `Audio/Gameplay`；菜单音效在 `Audio/UI`。声音配置留空时由行动音效组件加载默认资源，脚步及箱子/水晶移动音量可在该组件调整。

木箱两段素材分别取自 `90936__robinhood76__01497-brawling-2.wav` 的 0.30–0.62 秒、1.65–1.97 秒，每段 0.32 秒；采用 8 毫秒淡入、35 毫秒淡出，并平衡响度。保留完整原音频，运行工程根目录的 `node Tools/Audio/prepare-crate-audio.cjs` 可重新生成。裁剪工具是可选的离线维护工具，Unity 运行不依赖 Node。

脚步素材库位于 `Audio/Gameplay/Footsteps`，包含 Walk 10 个、Run 10 个、Land 6 个 WAV，来源为作者提供的 Footsteps - Essentials / Footsteps_DirtyGround 素材目录。保留原文件名与导入设置，当前随机池选用 Walk 01、02、05、06。

事件音效位于 `Audio/CasualGameSounds`，保留授权文件与以下五个 WAV，统一在 `PushBoxActions.asset` 配置：

| 事件 | 文件 | 触发时机 |
| --- | --- | --- |
| 我方死亡 | DM-CGS-05 | 死亡结果呈现时，同一次结算只播放一次 |
| 关卡完成 | DM-CGS-18 | 通关表现出现时 |
| 入口进入关卡 | DM-CGS-50 | 确认有效入口后，跨场景播放完毕 |
| 木箱压碎 | DM-CGS-48 | 开始闪白时，同批压碎只播放一次 |
| 打开关卡预览 | DM-CGS-20 | 有效预览开始淡入时，持续按住不重复播放 |

撤销和状态恢复不重播事件音效。

压力板压下或松开均播放 `Audio/Gameplay/PlateChange.wav`，石门开启在亮绿等待结束、开始下降时共用此音效，配置在 `PushBoxActions.asset` 的“压力板/石门”。持续占据、撤销、重开与状态恢复不触发。同一步多个压力板变化只播放一次；石门每次实际开启播放一次。

该音效取自 `338287__werra__piano-pedal-pressed-organ-pull-stop-sound.wav` 的 0.14–0.95 秒，去除开头低音量空白与冗长尾部，并加短淡入淡出；采样率、播放速度与音调不变。导入时预加载 PCM，避免首次播放等待。保留完整原音频，运行 `node Tools/Audio/prepare-plate-audio.cjs` 可重新生成。

## 维护与来源

几何生成工具位于 [VoxelModelBuilder](../../TD_PushBox/Scripts/Editor/ArtMaintenance/VoxelModelBuilder.cs)。个性化资源先复制再改，原位改动资产应保留 .meta/GUID，并同步点击盒与落脚约定。

中文字体及 Markdown 依赖的许可见 [ThirdParty](../../ThirdParty/README.md)。外观验收以工程内 Prefab、场景和实际运行结果为准。
