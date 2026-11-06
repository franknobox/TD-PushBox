# AGENTS

如果你是AI智能体，需要在这个项目当中工作，请先阅读本文，理解工程结构、开发约定与修改边界。

本文面向接手项目的 AI 与工程维护者，记录架构和工作约束；同时阅读[工程根目录的协作约定](../../AGENTS.md)。功能索引见 [FEATURES](FEATURES.md)，制作操作见 [Level Editor](LEVEL_EDITOR.md)，当前限制见 [ISSUES](ISSUES.md)。

## 工作约定

- 使用 Unity 2022.3.51f1 开发与验证。
- 修改前先阅读相关源码、配置与文档，确认功能的实现范围和调用关系。
- 保留作者的场景布局、未保存修改、稳定 ID 和资源 GUID；不强制保存，不覆盖无关改动。
- 普通修改不要求关闭 Unity。需要独立批处理或存在实际文件冲突时，说明原因并请求关闭。
- 不擅自增加测试、展示或样板场景；新增场景与玩法机制先确认需求和影响范围。
- 验证以编译、必要的数据与静态检查为主；实际画面和 PlayMode 验收交给作者，非必要不使用 computer-use。
- 功能变化同步对应文档；明确区分已经实现、仅制作端支持与尚未接入，不把计划写成完成结果。

## 基本原则

稳定、简洁、清晰：场景负责制作，数据负责规则，控制器负责演出。配置能够解决的差异不新增玩法类型；不为尚未确认的功能建立通用框架。

玩法运行代码位于 `TD.PushBox` 命名空间；地形、关卡制作与相关编辑器类型使用 `JobTest.TacticsVoxel` 命名空间。修改类型名称时需要检查 Unity 序列化引用。

## 程序集

| 程序集 | 职责 | 主要项目依赖 |
| --- | --- | --- |
| TD.PushBox.Runtime | 状态、规则、会话、输入、镜头、实体表现 | 不引用 Board、Frontend 或 Editor |
| TD.PushBox.Board | 地形几何、关卡容器、场地变化数据与地形展示 | Runtime |
| TD.PushBox.Frontend | 主菜单、选关 UI、入口交互与预览显示 | Runtime、Input System、UGUI |
| TD.PushBox.Editor | 创建、编辑、预览生成、校验与 Markdown 工具 | 上述运行程序集；只在 Editor 使用 |

测试分为规则数据检查和 Authoring EditMode 检查。Markdig 仅在 Editor 导入，不进入玩家构建。

## 目录

| 路径 | 内容 |
| --- | --- |
| Assets/Docs | 六份根文档与 Features 模块详情 |
| Assets/TD_PushBox/Scenes | StartMenu、LevelSelect |
| Assets/TD_PushBox/Scenes/Levels | 作者制作的玩法关卡 |
| Scripts/Runtime/PushBox | 独立规则与运行控制 |
| Scripts/Runtime/Board | 地块、容器、定义和纯地形状态 |
| Scripts/Runtime/LevelChanges | 场地变化方案与地形展示 |
| Scripts/Runtime/Frontend | 菜单与入口 |
| Scripts/Editor/Authoring | 制作窗口、编辑操作、属性 UI、层级和校验 |
| Scripts/Editor/Frontend | 菜单生成与地图离线预览 |
| Scripts/Editor/Documentation | Unity 文档窗口 |
| Scripts/Editor/ArtMaintenance | 保留的几何生成工具，不提供维护菜单 |
| Data/Authoring | 统一 PushBoxPalette |
| Data/Definitions / Data/Levels | 地块定义与关卡专属方案 |
| Prefabs/PushBox | 玩法角色、敌人和物件；其他 Prefab 目录提供场景物件与共享资源 |
| Art / Audio | Mesh、材质、Shader、预览图及声音素材 |
| Resources | 字体、音效默认配置、入口目录、预览材质等动态加载资源 |
| Assets/ThirdParty | 第三方组件与许可证 |
| Archive | 非活动备份与一次性工具，不参与导入，不作为提交运行依赖 |

上表省略 Assets/TD_PushBox 前缀的路径均相对该目录。创建资源前先检查现有目录与引用，避免生成重复资源。

## 数据流

```text
场景制作 + 地块定义 + Prefab 配置
  -> PushBoxAuthoring 校验/烘焙
  -> PushBoxLevel.BuildState
  -> PushBoxSession + PushBoxRules
  -> 位置、存亡、压板、门、目标状态
  -> PushBoxController 演出与同步
```

保存场景时烘焙地形；进入 Play 前先只读校验，再使用当前编辑内容烘焙。草稿保存与可启动检查分开，不强制把未完成关卡当作错误资产修复。

动作先检查完整落点与高度，再统一提交。机关迭代到稳定状态，敌人攻击收集后同时结算；表现读取结果，不用动画或碰撞回调反向决定玩法。

## 制作层级与引用

关卡根节点通过六个序列化容器引用管理 Terrain、Actors、Objects、Environment、Details 和 Runtime。实体不挂地块下；花草、底座可以属于地块。

Objs 下的 Obstacles、Goals、Doors、Plates、Movables 只辅助制作。运行时递归采集组件，不通过分组名称寻找压力板或门。

地面与独立障碍分开：石块/灌木是 Blocker，平地/高地是 Tile。替换地面只更新实体落脚高度，不改 ID、横向格点或门的关联。压力板通过对象引用配置，转换为运行 ID；入口用场景 GUID，不用显示编号做加载键。

`TDProjectPaths` 集中管理目录和资源名。移动资产保留 .meta/GUID；Resources 键、稳定 ID 与文档链接分别管理，不把它们当作同一种路径。

## 撤销与生命周期

完整状态快照用于最多 512 步撤销；字段复制集中在 `PushBoxState.Copy / PushBoxEntity.Copy`。新增可变规则字段要同步检查复制、重开与恢复。

控制器禁用时取消演出并同步到当前逻辑结果；脚本重载通过检查点重建当前棋局、初始状态、选中 ID 与撤销记录，不重新触发开局攻击。入口预览拥有自己的模态锁，关闭时释放并销毁临时 UI。

入口进入时由 Frontend 的 LevelRunNavigation 保存返回场景和按编号排序的有效入口路线；上下文只存在于本次游玩。通关后显示返回和手动下一关，撤销或重开后隐藏；返回庭院或进入无关场景后销毁。直接运行玩法关卡不自动创建上下文，Runtime 不反向依赖 Frontend。

当前没有持久化存档、重做或通用回放系统。完整快照重复保存静态地形是已知成本，不应在没有测量与需求的情况下改成复杂增量框架。

## 扩展边界

- 新单位能力优先进入独立规则与配置，不依赖具体场景名称。
- 新机关必须同时考虑占位、触发、连锁、死亡优先级、撤销和演出恢复。
- 场地变化目前仅制作端；接入运行必须先定义触发与回退规则。
- 只读工程检查不得保存场景、方案或构建配置。修复操作独立、显式并可撤销。
- EntranceContentBuilder 仅负责目录和预览，不读写 Build Settings。发布名单由作者在 Unity 内管理；LevelBuildValidation 构建前及工程检查时只读校验启用场景与跳转依赖，不拍摄、保存或修复。检查期间通过 TDProjectValidation.IsRunning 暂停自动整理，只关闭自己临时打开的场景。
- 场景/GameObject 使用简短英文，中文保留给玩家 UI、Inspector 和正文。常用缩写为 Env、Objs、RT；重复对象用 Guard (2)，地块可带坐标。
