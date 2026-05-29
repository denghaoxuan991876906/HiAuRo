# HiAuRo 插件源码

## 目录结构

```
HiAuRo/
├── Plugin.cs            # 插件入口 (IDalamudPlugin)
├── Plugin_Browsingway.cs # CEF 渲染集成 (partial class)
├── GlobalUsings.cs       # 全局 using (OmenTools + Dalamud + C#)
├── ACR/                  # ACR 抽象层
├── Command/              # /hi 命令系统
├── Data/                 # 游戏数据层
├── Execution/            # 执行轴 (Phase 6)
├── Infrastructure/       # 配置、调试、日志
├── Runtime/              # 运行时核心 (Tick/AIRunner/ACRLifecycle)
├── Setting/              # 设置管理
├── UI/                   # Web UI + WebSocket 桥接 + ImGui 主窗口
│   └── web/              # 前端文件 (HTML/CSS/JS)
└── Sdk/                  # Sdk 工具 (可选)
```

## 初始化顺序 (Plugin.cs:Constructor)

```
DService.Init → LoadConfig → BrowsingwayPluginInit
→ SettingMgr.Init → CommandMgr.Init → EventSystem.Init
→ RuntimeCore.Start → WebUiBridge + WebUiServer.Start
→ ACRLifecycle.Init → ACRLoader.LoadAll
→ WindowSystem + MainWindow
```

## 释放顺序

与初始化顺序相反，RuntimeCore → EventSystem → CommandMgr → BrowsingwayDispose → DService.Uninit

## Tick 循环 (RuntimeCore.OnTick)

每帧执行：Data.IsReady 检查 → Coroutine.Update → CombatContext.Check → EventSystem.CheckTargetChanged → HotkeyPoller.Update → ACRLifecycle.Update

## 构建

```bash
# Windows CMD
build.cmd

# WSL
./build.sh
```

构建后自动将 `HiAuRo.Sdk.nupkg` 输出到 `E:\DalamudPlugins\local-nuget-feed\`。Browsingway (CEF 渲染器) 和 OmenTools 是外部依赖。
