# Tools Features

制作工具的职责和维护约束。具体操作只在 [Level Editor](../LEVEL_EDITOR.md) 维护，索引见 [FEATURES](../FEATURES.md)。

## 创建与关卡编辑

两个窗口分工明确：

- `TDLevelCreateWindow / TDLevelSceneFactory`：验证名称、目录和尺寸，创建新场景、基础地面、容器、光照、镜头与外围主题。默认棋盘花草覆盖率 0%，不自动摆角色或目标。
- `VoxelLevelWindow`：当前场景的分类资源、选择、绘制、矩形、吸取、移动、擦除与参数编辑。
- `VoxelLevelEditOperations`：场景修改、占位检查、Undo、落脚高度和 Prefab override 记录。
- `VoxelLevelPalette`：资源标签、分类、Prefab、花草标记与基础合法性检查。

统一资源目录为 `Data/Authoring/PushBoxPalette.asset`。标签用于中文界面，玩法身份用组件、类型和稳定 ID，不用标签字符串判断单位能力。

地形画笔区分真正的 Tile 与 `Entry.IsTerrainObject`：地面使用 Tile；高地、石块、灌木作为 Blocker 叠放，不删除原地面。矩形跳过不能占用的格子，不挤走实体。

对应代码：[创建工厂](../../TD_PushBox/Scripts/Editor/Authoring/TDLevelSceneFactory.cs)、[编辑窗口](../../TD_PushBox/Scripts/Editor/Authoring/VoxelLevelWindow.cs)、[编辑操作](../../TD_PushBox/Scripts/Editor/Authoring/VoxelLevelEditOperations.cs)。

## 组织与关联

- `PushBoxObjectGroups` 在 Objs 下维护 Obstacles、Goals、Doors、Plates、Movables。新物件按 Kind 入组；已有直属或受管分组内的物件可以整理，不重排其他手工子分组。
- `VoxelLevelHierarchyNames` 维护简短英文显示名和重复编号；改名、换父节点不改格点、ID 或机关引用。
- `PushBoxObjectEditor` 提供压力板专用下拉框，排除其他关卡、未启用压力板和非 Plate 物件；原有失效关联显示待修复，不静默换成别的对象。
- 压力板的 Scene 坐标标签不要求选中，只在 Scene/Gizmos 中绘制，不进入 Game 视图。

这些是制作辅助，不是运行时查找路径的约定。玩法递归收集组件，不通过 `Objs/Plates/某个名字` 寻址。

## 环境生成与花草

`TDLevelScenery` 按尺寸、Forest/Castle 主题和种子组合直角台地、树石、遗迹或墙塔旗。四边疏密与外伸不同，近侧低矮，不固定生成正下方步石出口。

`VoxelDetailScatter` 独立处理棋盘花草，支持覆盖率、随机刷新和清空，由环境栏的覆盖率、刷新与清空功能管理。花草跟随可用地表与石块顶面，无玩法和碰撞组件。

两种刷新均是显式操作，保存配置但不会每次打开场景自动重建：

- 刷新布景只替换已登记的工具生成分组，保留棋盘、机关、镜头和棋盘花草。
- 刷新花草只重铺棋盘，不动外围。
- 手工布景替换需要确认；分组含玩法/自定义脚本时拒绝删除。
- 失败回退本次修改，两类刷新分别形成一次 Undo。

## 只读校验与显式位置同步

`PushBoxLevelValidation` 复用在关卡根 Inspector、Play 前和工程检查中，读取制作数据检查：

- 地面尺寸、重复/越界格点、地块定义与有效支撑。
- 实体缺失/重复 ID、重叠占位、多组件冲突、嵌套在地块下的实体。
- 场景位置与逻辑格点的一致性。
- 必要容器、控制器、镜头、门部件与压力板关联。
- 已配置场地方案的数据合法性。

进入 Play 前发现错误则取消进入；保存草稿不强制要求全部通过。检查不生成修复、不保证有解，也不替作者调整敌人站位。

两种位置同步明确选择数据来源：

| 操作 | 来源 | 结果 |
| --- | --- | --- |
| 按格点对齐场景 | 已有逻辑格点 | 恢复场景位置 |
| 按场景位置更新格点 | 直接拖动后的场景 | 先验证、再写格点与吸附 |

同步保留 ID 与对象引用，整次 Undo；非法目标在提交前拒绝。只读工程检查保留打开场景及未保存内容，只关闭自己临时打开的场景，并抑制自动升级与预览生成回调。

对应代码：[校验器](../../TD_PushBox/Scripts/Editor/Authoring/PushBoxLevelValidation.cs)、[制作桥接](../../TD_PushBox/Scripts/Editor/Authoring/PushBoxAuthoring.cs)、[工程检查](../../TD_PushBox/Scripts/Editor/Common/TDProjectValidation.cs)。

## Unity 文档窗口

`TD推箱子 > 文档` 递归读取 `Assets/Docs`，默认 README，支持搜索、页内目录、相对 Markdown 链接、工程资源链接、图片、代码块和表格。

“源文件”在同一窗口编辑 Markdown，支持滚动、保存/Ctrl+S 和还原。检测外部文件修改后阻止直接覆盖，保留当前草稿。文档入口为 Assets/Docs，读取范围不包含 Archive。

解析使用 Editor-only 的 Markdig；第三方信息见 [ThirdParty](../../ThirdParty/README.md)。新增子文档不需要改工具代码或手动注册。

## 验证边界

核心规则测试与少量 Authoring EditMode 检查随工程保留。文档和数据检查不能代替当前版本的 PlayMode、场景视觉或解法验收；具体清单见 [ISSUES](../ISSUES.md)。
