# HiAuRo

FFXIV Dalamud 战斗辅助框架（.NET 10，Dalamud.CN.NET.Sdk 15.0.0）。

不是职业循环 — 提供运行时、数据层、ACR 接口、执行轴、事实轴、决策层和 Web 编辑器。

[![Build](https://github.com/denghaoxuan991876906/HiAuRo/actions/workflows/ci.yml/badge.svg)](https://github.com/denghaoxuan991876906/HiAuRo/actions)

## ACR 开发者 — 一键开始

详见 **[ACR 作者上手指南](public-docs/ACR_AUTHOR_GUIDE.md)** — 从零写出职业循环的完整教程。

```bash
dotnet add package HiAuRo.Sdk
```

```csharp
using HiAuRo.ACR;
using HiAuRo.Helper;

public class MyAcr : IRotationEntry
{
    public IEnumerable<Jobs> TargetJobs => [Jobs.BRD];
    public Rotation? Build(string settingFolder) { return new Rotation(); }
}
```

## 编译

```bash
git clone --recurse-submodules https://github.com/denghaoxuan991876906/HiAuRo
cd HiAuRo
dotnet build HiAuRo.slnx -c Release
```

### 本地开发加速

内部开发者可将 `HiAuRo.Sdk` 自动输出到本地 NuGet 源，ACR 秒级恢复，无需等待 nuget.org 更新。

详见 **[本地 NuGet 源开发指南](public-docs/LOCAL_NUGET_DEV.md)** — 一键设置，零等待。

> HiAuRo.Helper 是独立仓库，编译时不依赖。运行时通过 HelperUpdater 自动拉取最新 DLL。

## 项目结构

```
HiAuRo/              ← 主插件
├── ACR/             ← 接口 + Helper + Slot 系统 + 目标解析器
├── Command/         ← /hi 命令处理
├── Data/            ← 游戏数据层（战斗、对象、队伍、目标）
├── Execution/       ← 执行轴 + 触发器元数据 + 脚本编译器
├── Runtime/         ← 运行时核心、AIRunner、ACR 生命周期、法术队列
├── UI/              ← Web UI（Kestrel + CEF）+ ImGui 覆盖层
├── FactAxis/        ← 事实轴（法术表、时间线、事实节点）
├── Decision/        ← 决策引擎 + 决策类型
├── Authoring/       ← 编辑器后端
├── Infrastructure/  ← 日志、配置、Browsingway IPC
├── Recording/       ← 战斗录制
└── Setting/         ← 设置管理器

OmenTools/           ← Dalamud 服务封装（submodule）
Browsingway/         ← CEF 渲染参考（submodule）
```

## 命令

| 命令 | 说明 |
|------|------|
| `/hi on/off/toggle` | 启停 ACR |
| `/hi status` | 查看状态 |
| `/hi fact` | 切换事实轴 |
| `/hi assist load/unload` | 辅助轴加载/卸载 |
| `/hi reload` | 重新扫描 ACR |

## 相关仓库

| 仓库 | 说明 |
|------|------|
| [HiAuRo-SampleACR](https://github.com/denghaoxuan991876906/HiAuRo-SampleACR) | 示例 ACR 实现 |
| [HiAuRo.Helper](https://github.com/denghaoxuan991876906/HiAuRo.Helper) | 全职业数据辅助库 |
| [HiAuRo.Sdk](https://www.nuget.org/packages/HiAuRo.Sdk) | ACR 开发 NuGet 包 || 仓库 | 说明 |
