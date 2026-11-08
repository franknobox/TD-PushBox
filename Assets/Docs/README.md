# PushBox

一个以多角色协作搬运为核心的推箱子 Demo，包含 Unity 内的关卡创建、编辑、配置与校验工具。使用 **Unity 2022.3.51f1**，当前交付以 Windows 编辑器中的游玩与制作流程为主。

## 文档导航

| 文档 | 用途 | 适合谁看 |
| --- | --- | --- |
| [GAMEPLAY](GAMEPLAY.md) | 操作、通关规则、角色能力、物体与敌人 | 玩家、试玩者 |
| [Level Editor](LEVEL_EDITOR.md) | 工具设计思路、从零搭关、机关接线、环境与检查 | 关卡制作者 |
| [FEATURES](FEATURES.md) | 当前功能索引，按模块跳转到实现说明与资源规范 | 技术策划、程序、美术 |
| [ISSUES](ISSUES.md) | 未完成部分、已知限制及上传前验收清单 | 提交与验收人员 |
| [AGENTS](AGENTS.md) | AI 工作约定、目录、程序集、数据流和扩展边界 | AI 智能体、工程维护者 |

FEATURES 的详情统一放在 [Features](FEATURES.md) 索引下的子文档中；本页不重复维护模块详情。

## 快速启动

1. 在 Unity Hub 添加本工程根目录，即同时包含 `Assets`、`Packages` 和 `ProjectSettings` 的目录。
2. 用 **2022.3.51f1** 打开，等待依赖恢复和资源导入。
3. 打开 `TD推箱子 > 打开场景 > StartMenu 开始界面`，按 Play，然后点击 Game 视图使其获得键盘焦点。
4. 点击“开始游戏”进入选关庭院。用 WASD 走到已配置的关卡入口，按 E 进入；按住 V 可以预览，松开关闭预览。
5. 若当前庭院尚未配置要测试的入口，停止 Play，在 Project 窗口的 [Scenes/Levels](../TD_PushBox/Scenes/Levels) 中双击对应关卡场景，再按 Play。“打开场景”菜单只提供开始界面和选关场景入口。
6. WASD 移动，Q 切人，Ctrl+Z 撤销，R 重开。所有绿色目标点同时由我方角色占据即通关；死亡后可 Ctrl+Z 撤销或 R 重开。
7. 从庭院入口进入的关卡通关后，可用左下按钮返回选关，或用右下按钮按 `x-y` 编号进入下一关；最后一关只显示返回。

角色能力随方向输入自动执行，不需要额外技能键。完整规则见 [GAMEPLAY](GAMEPLAY.md)。

## 推荐编辑器布局

工程随附 [goodtouse.wlt](../../EditorLayout/goodtouse.wlt)，推荐首次查看工程时使用。在 Unity 顶部选择 `Window > Layouts > Load Layout from File…`，加载工程根目录 `EditorLayout/goodtouse.wlt`，即可恢复推荐的窗口分区与标签布局。

布局不会自动覆盖个人设置；不同分辨率、系统缩放或多显示器环境下，可自行微调窗口大小。布局文件不替代关卡场景或项目设置。

## 制作与阅读入口

- `TD推箱子 > 创建关卡`：指定名称、尺寸与外围主题，从空白棋盘开始制作。
- `TD推箱子 > 关卡编辑`：编辑当前场景，配置角色、障碍、机关和目标点。
- `TD推箱子 > 检查工程`：只读检查，不代替关卡解法和实机验收。
- `TD推箱子 > 文档`：在 Unity 中阅读本目录；也可双击 Markdown。支持搜索、目录和相对链接；“源文件”模式可编辑，保存或 Ctrl+S 写入。

新关卡默认没有角色、没有目标点，也不铺设棋盘花草。保存草稿不要求可玩；进入 Play 前必须先通过关卡检查。

## 运行范围

主菜单与选关场景在 `Assets/TD_PushBox/Scenes`；玩法场景在 `Scenes/Levels`。关卡入口需要作者配置，不能仅凭场景文件存在就认为已经接入庭院。

当前支持过关判定、通关提示，以及入口进入后的返回选关与手动下一关；评分、全通关汇总和进度存档尚未实现。“自制关卡”列表仍是界面外观。场地变化仅支持方案制作与预览，尚未接入运行触发。详见 [ISSUES](ISSUES.md)。

## 工程与提交

- 工程内容分为推箱子规则、场景制作、界面和表现，职责与修改边界见 [AGENTS](AGENTS.md)。
- 仓库名为 `TD-PushBox`。提交 `Assets`（含全部 `.meta`）、`Packages`、`ProjectSettings`、`EditorLayout`、`Tools`、`.gitignore`、根 README 和 AGENTS。依赖版本见 [manifest.json](../../Packages/manifest.json)。
- 不提交生成缓存 `Library`、`Temp`、`Obj`、`Logs`、`UserSettings`，以及非活动备份 `Archive`。`Archive` 已被忽略，不是运行依赖。
- 字体和 Markdown 解析库的说明及许可证见 [ThirdParty](../ThirdParty/README.md)。复用资源和生成内容的来源说明应随提交保留，不把第三方许可等同于整个工程的授权。
- 上传前按 [ISSUES](ISSUES.md) 完成人工验收，并在干净检出目录中验证导入、编译和启动。本轮文档整理不代表已经完成该验证。
