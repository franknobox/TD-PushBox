# Core Features

角色控制、格点状态与行动恢复。玩家规则见 [GAMEPLAY](../GAMEPLAY.md)，模块索引见 [FEATURES](../FEATURES.md)。

## 格点状态与场景解耦

功能说明：

- `PushBoxState` 保存尺寸、格子字典和实体列表；实体记录 ID、类型、位置、朝向、占地、存亡及机关字段。
- `PushBoxLevel` 保存制作端烘焙的地形，运行开始时采集启用的角色和物件。
- `PushBoxRules` 只操作状态，不查场景物体、不播放声音，也不使用物理碰撞推动物体。
- `PushBoxController` 负责输入、会话和表现时序。模型尺寸和点击盒不作为规则占位来源。

这样同一套规则可供关卡运行、数据检查与核心测试使用，不需要每个场景维护一套搬运脚本。

对应代码：[PushBoxState](../../TD_PushBox/Scripts/Runtime/PushBox/PushBoxState.cs)、[PushBoxLevel](../../TD_PushBox/Scripts/Runtime/PushBox/PushBoxLevel.cs)、[PushBoxRules](../../TD_PushBox/Scripts/Runtime/PushBox/PushBoxRules.cs)。

## 多角色方向输入

- WASD 转换为四邻方向，Q 按角色采集顺序切换。
- 力士沿直线收集最多三个可移动单格实体；先检查整条链和各段高差，再一次性提交。
- 盗贼合法移动后，把身后相邻的 Crate 或我方 Actor 拉到原格；水晶也复用 Crate。目标须可移动、占单格且高差连通。
- 法师沿直线取最近可交换目标；目标之前的阻挡物截断查找，深渊不截断射线，双方落点必须可站立。
- 盾兵仅普通移动。力士对应 `PushBoxRole.Warrior`，界面名称为力士。

非法操作可改变面向，但不移动占位、不新增撤销记录。当前反馈原因保存在 `LastFeedback`，尚未统一显示到玩家界面，见 [ISSUES](../ISSUES.md)。

## 行动结算与演出

一次合法行动按以下顺序推进：

1. 提交搬运后的格点与朝向。
2. 更新目标点、压板和门；关门可能压碎木箱或压死人物，重复结算直到机关稳定。
3. 收集仍存活敌人的接触攻击和喷火目标，同时结算死亡。
4. 再次更新目标点与机关。
5. 控制器按前后状态播放移动/换位、关门压碎、敌人前冲和喷火，最后同步倒地、隐藏与通关提示。非火焰攻击通过只读的 `strikes` 记录真实攻击者与目标；前冲只修改临时显示位置，回位后同步格点，关门致死不产生敌人前冲。

普通移动默认 0.30 秒，连续移动间隔 0.34 秒，换位表现 0.16 秒，均可在控制器配置。演出期间阻止下一次移动或切人，R 可中断演出并重开；结算不依赖碰撞回调或动画命中事件。

## 撤销、重开与死亡锁定

- `PushBoxSession` 在合法行动前复制状态，保留最近 512 步。
- 快照包含地形、实体位置、朝向、死亡、木箱破坏、压板及门的开关信号。
- Undo 直接恢复上一个状态，不重新执行敌人攻击；重开使用进入关卡时的初始状态。
- 开局接触或关门造成死亡时也保留可撤销的初始状态。
- 我方死亡优先于通关；失败时移动、切人和前端返回锁定，允许 Ctrl+Z 撤销或 R 重开。R 可中断行动演出，恢复初始状态、选中首名角色并清空撤销记录。
- `VoxelPlayUndoGuard` 在 Play 中接管编辑器撤销事件；即使没有玩法撤销记录，也不允许落入场景编辑的 Ctrl+Z。

当前采用完整快照，简单直观但会重复复制静态地形。没有死局求解、回放文件、重做或持久化存档；也不把这套 Undo 等同于动画倒放。

对应代码：[PushBoxSession](../../TD_PushBox/Scripts/Runtime/PushBox/PushBoxRules.cs)、[VoxelPlayUndoGuard](../../TD_PushBox/Scripts/Editor/Authoring/VoxelPlayUndoGuard.cs)。

## 生命周期恢复

控制器禁用时终止演出并对齐到当前逻辑结果；重新启用不重新开局。脚本重载使用 `PushBoxSessionCheckpoint` 保存初始状态、当前状态、选中 ID 与撤销记录，再重建运行字典。

恢复后的表现以逻辑结果为准，不尝试接着播放已经中断的半段演出。检查点不是磁盘存档，不承诺关闭编辑器后继续游戏。

对应代码：[PushBoxController](../../TD_PushBox/Scripts/Runtime/PushBox/PushBoxController.cs)、[PushBoxSessionCheckpoint](../../TD_PushBox/Scripts/Runtime/PushBox/PushBoxSessionCheckpoint.cs)。
