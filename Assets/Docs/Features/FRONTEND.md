# Frontend Features

开始、选关与入口预览。游玩步骤见 [README](../README.md)，入口配置见 [Level Editor](../LEVEL_EDITOR.md)，索引见 [FEATURES](../FEATURES.md)。

## 开始界面

StartMenu 的标题是 PushBox，只有“开始游戏”和“退出游戏”。

- 开始游戏淡出菜单并以 Single 模式异步进入 LevelSelect，加载时避免重复点击。
- 退出游戏在编辑器中停止 Play，在独立程序中调用退出应用；不依赖额外绑定事件。
- 按钮提供悬停高亮、箭头/文字位移、按下缩放与短音效。背景有树木、角色和水面轻微动效。
- 林间遗迹、四名角色和木箱用于表现主题；背景模型不带玩法输入、占位或碰撞。
- UI 以 1920×1080 为参考，通过 CanvasScaler 缩放；不是运行时重新生成整张菜单。

对应代码：[StartMenuController](../../TD_PushBox/Scripts/Runtime/Frontend/StartMenuController.cs)、[StartMenuWorldMotion](../../TD_PushBox/Scripts/Runtime/Frontend/StartMenuWorldMotion.cs)。

## 可行走选关庭院

LevelSelect 是 9×6 的可行走庭院，复用格点移动和固定透视镜头。左下返回开始；Esc 在列表展开时先收起列表，否则返回主菜单。

右侧“自制关卡”默认收纳，点击展开，支持短动画、条目反馈与滚动。鼠标位于列表上时滚轮优先滚动列表，不缩放棋盘。当前仍为界面外观与名称预览，没有连接实际自制关卡数据或加载。

不得把列表中配置的显示名称当作场景加载路径，也不自动把 Levels 目录里的所有场景摆成庭院入口。

## 入口编号与交互

- 入口配置最多 3 字符编号和目标场景 GUID，选择范围限定为 `Scenes/Levels`。
- 编号贴近地块表面显示，站上入口且移动完成后隐藏，替换为 E 开门图标/进入与 V 眼镜图标/预览提示。
- E 检查配置和场景是否可加载，再淡出并以 Single 模式加载目标关卡。
- 未配置目标关卡时，进入和预览均提示“还没有配置关卡呢～”。
- 按住 V 开始预览，0.6 秒完全浮现；松开后 0.6 秒消失，途中反向不会突跳。
- 地图居中、保持原比例，宽高不超过屏幕的 75%，边缘使用消散效果，不加硬边框。
- 淡入淡出期间隐藏入口操作提示，锁住移动、切人、撤销、重开、缩放与返回，消退结束后恢复。

预览界面禁用时清理临时 UI，恢复自己占用的菜单、镜头和输入状态；重新启用时重建，避免遗留锁定。

对应代码：[PushBoxEntrance](../../TD_PushBox/Scripts/Runtime/PushBox/PushBoxEntrance.cs)、[LevelEntrancePresenter](../../TD_PushBox/Scripts/Runtime/Frontend/LevelEntrancePresenter.cs)、[EntranceOverlay](../../TD_PushBox/Scripts/Runtime/Frontend/EntranceOverlay.cs)。

## 通关导航

第二章入口对应关系：`2-1 → Level_11`、`2-2 → Level_12`、`2-3 → Level_13`、`2-4 → Level_14`、`2-5 → Level_15`。路线先按章节编号、再按关卡编号排序，支持第一章末关进入第二章，`2-5` 无后续有效入口时只显示返回选关。

从庭院入口进入时，`LevelRunNavigation` 保存本次返回场景与入口路线，并随游戏流程跨场景保留。完成行动和机关演出后，左下“返回选关”回到原庭院，右下“下一关”进入后续关卡；经过下一关跳转后仍保留返回能力。

- 路线来自进入时庭院中启用、已配置且可加载的入口，只取 `x-y` 编号，按数字排序，不依赖场景文件名或构建列表顺序。
- 空目标、其他编号和重复编号不参与下一关路线；最后一关或当前编号不在有效路线中时只显示返回。
- 直接运行玩法关卡不会创建导航。撤销或 R 重开使通关状态消失时，按钮同步隐藏。
- 加载期间阻止重复点击和玩法输入；返回庭院或进入无关场景后清理本次上下文。路线不作为进度存档。
- 按钮随屏幕尺寸缩放并提供背景悬停反馈，通关标题始终为黄色，不受悬停影响。

对应代码：[LevelRunNavigation](../../TD_PushBox/Scripts/Runtime/Frontend/LevelRunNavigation.cs)、[LevelRunRoute](../../TD_PushBox/Scripts/Runtime/Frontend/LevelRunRoute.cs)。

## 离线地图预览

预览不是运行时加法加载另一份玩法关卡，而是编辑器提前拍摄的图片：

1. 读取目标关卡；未保存修改不被强制保存用于拍照。
2. 在独立 Preview Scene 中复制渲染内容和灯光，不复制玩法脚本、碰撞或 UI。
3. 用目标镜头拍摄，并显式限制场景裁剪范围；临时使用源关卡环境光，结束后恢复。
4. 缓存到 `Art/Previews`，目录保存到 `Resources/PushBoxLevels.asset`；材质在 `Resources/UI/LevelPreview.mat`。
5. 依赖哈希与预览版本号共同决定是否重拍，保证缓存与目标数据一致。

关卡保存后会请求更新预览与入口目录。生成过程不读写 Build Settings，构建期间不自动拍摄；预览失败不修改发布名单，也不会强制保存作者场景。

对应代码：[EntranceContentBuilder](../../TD_PushBox/Scripts/Editor/Frontend/EntranceContentBuilder.cs)。

## 构建配置

`File > Build Settings` 是独立的发布场景名单，保留作者的启用状态和排序，不从 Levels 文件夹自动补齐。创建选关场景也不改变发布名单。

`LevelBuildValidation` 在构建前只读检查启用场景、菜单跳转、入口目标与目录一致性；“检查工程”复用同一检查。仅关联到启用场景的内容参与检查，不要求加入所有草稿；已启用入口的目标缺失或未启用时明确报错，不静默添加。预览图片缺失不阻断构建。

检查临时加载未打开的场景时，自动整理暂停；结束后只关闭自己打开的场景，恢复原活动场景。已打开但未保存的构建场景会要求作者先保存，不代为保存。

对应代码：[LevelBuildValidation](../../TD_PushBox/Scripts/Editor/Frontend/LevelBuildValidation.cs)、[LevelBuildSceneRules](../../TD_PushBox/Scripts/Editor/Frontend/LevelBuildSceneRules.cs)。

## 当前边界

已建立主菜单、庭院、入口进入和按住预览的机制；具体哪些关卡能从庭院进入，取决于作者实际保存的入口关联。

已支持入口进入后的通关返回与手动下一关；通关评分、全通关汇总、进度存档及关卡中途返回菜单尚未实现，选关列表尚不加载。场景资产存在、加入 Build Settings 与关卡可解是三件不同的事。
