# HiAuRo — 技术栈

## 运行时

| 项目 | 版本 | 用途 |
|------|------|------|
| .NET | 10.0 | 运行时与 SDK |
| C# | 13.0 | 编程语言 |
| Dalamud.NET.Sdk | 15.0.0 | Dalamud 插件开发 SDK |

## 核心依赖

| 包 | 用途 |
|----|------|
| Dalamud.NET.Sdk 15.0.0 | 插件构建、Dalamud API |
| OmenTools | DService 服务入口、ImGuiOm UI 组件、Managers 事件包装 |
| HttpListener (System.Net) | .NET 内置 HTTP + WebSocket 服务器 |
| Microsoft.CodeAnalysis.CSharp | Roslyn，执行轴 C# 脚本动态编译 |
| FFXIVClientStructs | 游戏数据结构（通过 OmenTools 间接引用） |
| VNavmesh | 寻路插件（角色移动驱动的底层能力，通过 IPC 调用） |

## 悬浮窗渲染

CEF 渲染由 [Browsingway](https://github.com/ProjectAliceDev/browsingway) 处理，HiAuRo 通过 localhost:5678 提供 Web 内容，Browsingway 渲染到游戏内 D3D11 共享纹理。

- **方案**: Browsingway 外部进程 + D3D11 共享纹理
- **开发**: 浏览器直接访问 localhost:5678，无需游戏环境
- **原因**: 避免自建 CEF 的打包膨胀和进程不稳定

## OmenTools 使用边界

- **接入方式**: `DService.Init(pluginInterface)` / `DService.Uninit()` 直接挂在 `Plugin.cs`
- **内部使用**: `DService.*` 作为数据层内部骨架
- **不创建** OmenTools 的额外包装层或 ServiceLocator
- **公开入口**: 上层推荐走 `HiAuRo.Data`，`DService` 允许在职业逻辑里直接使用但非推荐

## 外部依赖

| 包 | 用途 | 状态 |
|----|------|------|
| ImGui.NET | UI 渲染（浮窗控制面板） | Dalamud 内置（或通过 OmenTools ImGuiOm） |

## HiAuRo.Helper

- **HiAuRo.Helper**（独立仓库）— 21职业辅助库，HelperUpdater 自动从 GitHub Release 更新加载
- **位置**: `HiAuRo/Runtime/HelperUpdater.cs`（插件内自动管理）
- **仓库**: https://github.com/denghaoxuan991876906/HiAuRo.Helper

## 开发工具

| 工具 | 用途 |
|------|------|
| dotnet CLI | 构建、还原依赖 |
| dotnet test | 单元测试 |
| XIVLauncher (dev mode) | 插件加载与调试 |
| Dalamud DevPlugins | 本地插件开发环境 |

## csproj 配置

```xml
<Project Sdk="Dalamud.NET.Sdk/15.0.0">
  <PropertyGroup>
    <AssemblyName>HiAuRo</AssemblyName>
    <RootNamespace>HiAuRo</RootNamespace>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Version>0.1.0</Version>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

## 项目结构

- 单项目（单 `.csproj`），不建 `.sln`
- `HiAuRo/` 目录下包含所有源码
- 按功能模块分子目录（Data / Runtime / ACR / Jobs / Execution / UI）
- 项目结构扩展：Execution/ 目录，包含 TriggerLine/NodeProgressor/ExecutionDebug + Triggers/
- 没有额外的 test project（通过 in-game 验证）

## 约束

- 不在项目里引入 WinForms / WPF 依赖
- 不引入额外的 IoC 容器或 Service Locator 框架
- 配置持久化走 Dalamud 原生 `IPluginConfiguration` 路径
- 日志走 Dalamud 原生 `IPluginLog`

---

*Last updated: 2026-05-27*
