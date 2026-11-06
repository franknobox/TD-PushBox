# 第三方依赖

## Markdig 0.37.0

- 项目：https://github.com/xoofx/markdig
- 包：https://www.nuget.org/packages/Markdig/0.37.0
- 固定版本：0.37.0
- 程序集：官方 NuGet 包中的 `lib/netstandard2.1/Markdig.dll`
- SHA-256：`DF512A5834604AEE15013BA0F43D1860993CEF2AF3F5D151DFE3F0BC1092651D`
- 用途：Unity 文档窗口的 Markdown AST 解析。
- 平台：仅 Unity Editor，`Markdig.dll.meta` 禁用所有 Player 平台。
- 许可证：BSD-2-Clause，完整声明见 `Assets/ThirdParty/Markdig/LICENSE.txt`。

此版本的 .NET Standard 2.1 程序集没有额外 NuGet 依赖，工程目标环境为 Unity 2022.3.51f1。更新时应重新确认平台设置，并验证 Markdown 解析与 UI Toolkit 渲染。
