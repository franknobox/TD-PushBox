# Terrain Changes Features

场地变化的制作端功能，运行触发尚未接入。使用步骤见 [Level Editor](../LEVEL_EDITOR.md)，索引见 [FEATURES](../FEATURES.md)。

## 当前范围

支持方案数据、地形/障碍/目标点/敌人/机关编辑、Boss 预制体与出生位置、临时预览和合法性检查。关卡专属方案存放在 `Data/Levels`。

当前不会因为踩板、祭坛、敌人死亡、回合或步数自动执行方案，也没有预警坍塌、运行生成等待、撤离阶段或这些变化的运行撤销。界面能配置不等于玩法已接入。

## 方案数据

`VoxelTerrainChangeDefinition` 与场景分离：

| 字段 | 职责 |
| --- | --- |
| basis | 新建时复制的基础地形 |
| tiles | 相对基础的修改，同格覆盖基础 |
| exits / targetExitPrefab | 目标点布局与对应 Prefab |
| enemies / objects | 新增、移动、替换或移除敌人、机关及独立障碍 |
| existingId | 引用初始场景中的物件 |
| key | 标识方案中的新增实体 |
| bossPrefab / bossPosition | 预留生成对象与出生格配置 |

初始角色与物件不重复复制。石块、灌木使用独立 Blocker，地面仍保存在 Tile 方案里，不重新混成一块替换地形。

新增压力板引用使用方案 key，已有压力板使用稳定 ID；门可配置是否覆盖原关联和开关状态。数据通过组件引用或 ID 连接，不通过层级名连接。

## 制作流程

1. 在关卡编辑顶部切换“场地变化”，选择方案或新建。
2. 新建会复制当前地形与目标点作为基础，保存独立 asset。
3. 用分类画笔编辑地面、障碍、敌人、机关与目标点；修改已有实体沿用 existingId，新实体分配 key。
4. 如需预留 Boss 出生配置，选择敌方 Prefab 与位置，可在 Scene 点选格子；它目前只是方案数据。
5. 点击“校验方案”，检查最终地面、边界、多格同高要求、Prefab、重复 key 与压板引用。
6. 保存方案和承载关联的场景，退出模式确认初始场地仍完整。

复制基础是新建时的快照，不会因为之后修改初始地形而自动重建。

## 预览生命周期

预览只隐藏原显示，并生成无玩法脚本、无碰撞的临时模型。已有对象保持场景身份，预览自身不参与状态采集。

切换模式、保存场景、进入 Play、脚本重载或关闭窗口会清理预览并恢复显示，不把预览模型保存为正式布局。

这与关卡入口的 PNG 地图预览不是一套流程：这里是制作中的场地差异预览，入口预览是离线拍摄已保存关卡。

## 接入运行前的约束

后续需要先确定触发条件、推进时机、被占格处理与回退边界，再接入独立规则状态。运行变化应由独立状态统一驱动，不能仅移动场景模型而不更新规则数据。

未来新增动态地形、实体生成与坠落时，必须一起处理快照、稳定 ID、占位、压板连锁、死亡优先级和演出恢复；本文件不宣称这些已经完成。

对应代码：[方案定义](../../TD_PushBox/Scripts/Runtime/LevelChanges/VoxelTerrainChangeDefinition.cs)、[方案校验](../../TD_PushBox/Scripts/Editor/Authoring/VoxelTerrainPlanValidation.cs)、[制作预览](../../TD_PushBox/Scripts/Editor/Authoring/VoxelTerrainChangePreview.cs)。
