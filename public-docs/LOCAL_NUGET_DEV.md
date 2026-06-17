# 本地 NuGet 源开发加速

## 痛点

HiAuRo 修改后，需要等 nuget.org 更新 `HiAuRo.Sdk` 包，ACR 项目才能恢复编译。几分钟的白等。

## 方案

HiAuRo 构建后自动把 `HiAuRo.Sdk.nupkg` 输出到本地目录，ACR 的 `NuGet.Config` 优先从这个目录取包，无需等待远端更新。

## 目录结构

```
E:\DalamudPlugins\
├── HiAuRo\              ← HiAuRo 框架（本仓库）
├── MyACR\                ← ACR 项目
│   └── NuGet.Config       ← 优先从 ..\local-nuget-feed\ 恢复
└── local-nuget-feed\     ← 本地包存放（HiAuRo 构建时自动创建）
```

## 一次设置

在 ACR 项目根目录创建 `NuGet.Config`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-feed" value="E:\DalamudPlugins\local-nuget-feed\" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

ACR 的 `.csproj` 中 SDK 引用改为浮动版本：

```xml
<PackageReference Include="HiAuRo.Sdk" Version="0.1.*">
    <ExcludeAssets>runtime</ExcludeAssets>
</PackageReference>
```

## 开关

`HiAuRo/HiAuRo.csproj` 中 `<UseLocalNuGetFeed>` 属性：

- `true`（默认）：构建后自动打包
- `false`：不打包，ACR 走 nuget.org

临时关闭：`dotnet build ... -p:UseLocalNuGetFeed=false`

## 构建

所有构建必须在 Windows 环境中执行：

```bash
# Windows CMD 直接执行
E:\DalamudPlugins\HiAuRo\build.cmd

# WSL 下通过 cmd.exe 转发
cmd.exe /c "E:\DalamudPlugins\HiAuRo\build.cmd"
```

## 版本升级

同时更新 `HiAuRo.csproj` 的 `<Version>` 和 `HiAuRo.Sdk.nuspec` 的 `<version>`。ACR 用浮动版本 `0.1.*`，自动拉最新，无需手动同步。
