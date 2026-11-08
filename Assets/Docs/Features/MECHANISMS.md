# Mechanisms Features

目标、机关和敌方威胁的实现边界。玩家规则见 [GAMEPLAY](../GAMEPLAY.md)，摆放接线见 [Level Editor](../LEVEL_EDITOR.md)，索引见 [FEATURES](../FEATURES.md)。

## 统一物件状态

木箱、水晶、阻挡物、压力板、门、目标点、敌人和入口共用 `PushBoxObject` 场景组件，再转换为 `PushBoxEntity`。实体类型决定参与哪些规则，而不是靠 GameObject 名字判断。

`movable` 控制搬运，`footprint` 控制占地，`dead` 控制失效。`crushable` 只用于区分 Crate 是否会被门压碎，不是角色生命值或通用伤害系统。

对应代码：[PushBoxObject](../../TD_PushBox/Scripts/Runtime/PushBox/PushBoxObject.cs)、[PushBoxEntity](../../TD_PushBox/Scripts/Runtime/PushBox/PushBoxState.cs)。

## 目标点

- Goal 不阻挡、不搬运，仅存活 Actor 能激活。
- 场上必须存在目标点，且全部同时激活，才判定通关；不要求全部角色都站点。
- 任一我方死亡则全部目标点禁用并变黑，失败优先。
- 控制器等行动演出结束后显示“关卡完成！”，允许撤销或重开。

当前只到达过关判定和提示，不包含星级、结算按钮、下一关或进度保存。

## 压力板与普通门

- Plate 由存活 Actor、Enemy 或 Crate 占据时压下；Blocker 和 Goal 不压板。
- 普通门使用 `PressurePlates` 条件，关联必须是同关卡的压力板。
- 保持式门直接跟随“全部压下”的布尔信号；切换式门用 `signal` 保存上一轮信号，只在重新成立的上升沿切换 `requestedOpen`。
- 空关联列表不自动产生开关信号，保留请求的初始开关状态。
- 实际 `open` 还要考虑不可破坏占位，不能只靠视觉门板是否下降判断通行。

门的下拉选择和 Scene 压板坐标均为 Editor 功能，不增加运行时扫描 UI。

## 关门压碎与致死

请求关门时，门格上的我方人物、敌人和可压碎木箱进入压碎/致死结算：

- 木箱：设置 `dead`，不再占位或压板；显示闪白 0.3 秒后隐藏。
- 我方：复用敌人命中后的倒地、失败锁定和目标点禁用。
- 敌方：死亡后隐藏，不再攻击、占位或计入封印门的存活敌人数。
- 水晶：`crushable=false`，不可破坏，占住门格时继续阻止门关闭。
- 其他不可压碎的阻挡物也继续防夹；若门被这类占位挡住，不会假装关门并杀死其他格的物体。

压碎/致死可能释放压力板。机关更新会迭代至不再产生新的压碎或致死，避免依赖门在列表中的顺序。所有状态纳入当前行动的撤销快照。

对应代码：`PushBoxRules.RefreshMechanisms / CloseDoor`、`PushBoxController.PlayCrushing`，分别负责结算与表现。

## 封印石门

- `AllEnemiesDefeated` 条件只看是否仍有存活 Enemy，包含火龙和大体型妖怪。
- 单格占地，不配置压力板。关闭时封印黑绿色、带方块邪气；解封变绿，等待 1 秒，再用约 0.32 秒下降。
- 逻辑开门后，控制器仍等待演出完成，避免提前穿门。
- 撤销直接恢复封印、碰撞与可通行状态，不重播解封等待。

对应代码：[EnemySealDoorVisual](../../TD_PushBox/Scripts/Runtime/PushBox/EnemySealDoorVisual.cs)。

## 接触敌人与火龙

普通 Enemy 不主动移动，使用 Contact 攻击；四邻 Actor 或其他 Enemy 都是目标。攻击先收集进集合，再统一置死，因此相邻敌人可以互杀，不受遍历顺序影响。

DragonFire 使用摆放朝向，从前一格扫描到棋盘边界。不可通行且有遮挡的地形、Crate、Blocker 和关闭的门截断射线；人物不挡火。存在人物目标才生成本次 `fireRays` 表现数据，空射线不喷火。

火龙只喷火，不额外近身攻击。守卫在正前方相邻格可以和石像同时击倒对方；侧面守卫可攻击石像，但未处于火焰线时不会受到石像反击。

近战守卫与三款妖怪执行 Contact 四向 1 格攻击，持枪哨兵执行 Ranged 四向 2 格攻击，不覆盖斜角。障碍、箱子、水晶与关闭的门截断射线，人物不挡射线。实际命中与 Scene 范围显示统一使用 `PushBoxRules.AttackCells`，火龙仅沿摆放朝向延伸到边界或首个阻挡前。祭坛仅为单格 Blocker；入口不阻挡，也不参与目标激活。

## 修改时的约束

新增机关先明确类型、占位、触发条件与状态字段，再接表现。不能让材质、动画或命名决定结算；新增可变字段需要同时检查快照复制、初始状态和恢复流程。

核心回归检查位于 [PushBoxRulesTests](../../TD_PushBox/Tests/EditMode/PushBox/PushBoxRulesTests.cs)。通过数据测试不代表当前所有关卡都有解。
