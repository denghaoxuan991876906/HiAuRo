# 插件窗口系统 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development

**Goal:** 给 IPlugin 新增窗口能力，插件可返回 ImGui 窗口，HiAuRo 统一管理显隐。

**Architecture:** IPluginWindow 接口定义 Draw()。IPlugin 新增 GetWindow() 默认返回 null。PluginWindowManager 扫描插件、创建 Window 包装、注册到 WindowSystem、提供 Toggle 命令。

---

### Task 1: 创建 IPluginWindow 接口 + 扩展 IPlugin

**Files:**
- Create: `E:\DalamudPlugins\HiAuRo\HiAuRo\Plugin\IPluginWindow.cs`
- Modify: `E:\DalamudPlugins\HiAuRo\HiAuRo\Plugin\IPlugin.cs`

- [ ] **Step 1: 创建 IPluginWindow.cs**

写入:

```csharp
namespace HiAuRo;

/// <summary>
/// 插件 ImGui 窗口接口 —— IPlugin.GetWindow() 返回此接口即可自动注册到 WindowSystem
/// </summary>
public interface IPluginWindow
{
    string Title { get; }
    void Draw();
}
```

- [ ] **Step 2: 修改 IPlugin.cs 加 GetWindow() 默认实现**

在 `IPlugin` 接口中，`Update()` 之后添加:

```csharp
    /// <summary>返回插件窗口，无需窗口时返回 null</summary>
    IPluginWindow? GetWindow() => null;
```

- [ ] **Step 3: 构建验证**

```powershell
dotnet build "E:\DalamudPlugins\HiAuRo\HiAuRo\HiAuRo.csproj" -c Release -nologo
```

- [ ] **Step 4: Commit**

```powershell
git add "E:\DalamudPlugins\HiAuRo\HiAuRo\Plugin\IPluginWindow.cs" "E:\DalamudPlugins\HiAuRo\HiAuRo\Plugin\IPlugin.cs"
git commit -m "feat: 新增 IPluginWindow 接口, IPlugin 默认 GetWindow() 返回 null"
```

---

### Task 2: 创建 PluginWindowManager + Window 包装

**Files:**
- Create: `E:\DalamudPlugins\HiAuRo\HiAuRo\Runtime\PluginWindowManager.cs`

- [ ] **Step 1: 创建 PluginWindowManager.cs**

写入:

```csharp
using System.Numerics;
using Dalamud.Interface.Windowing;

namespace HiAuRo.Runtime;

/// <summary>
/// 插件窗口管理器 —— 扫描有窗口的插件, 注册到 WindowSystem, 提供 Toggle 命令
/// </summary>
public static class PluginWindowManager
{
    private static readonly Dictionary<string, PluginWindowWrapper> _windows = [];

    /// <summary>已注册的插件窗口名称列表</summary>
    public static IReadOnlyDictionary<string, PluginWindowWrapper> Windows => _windows;

    /// <summary>扫描插件并注册窗口到 WindowSystem</summary>
    public static void Init(WindowSystem windowSystem)
    {
        foreach (var (name, record) in PluginLoader.Plugins)
        {
            var window = record.Plugin.GetWindow();
            if (window == null) continue;

            var wrapper = new PluginWindowWrapper(window);
            windowSystem.AddWindow(wrapper);
            _windows[name] = wrapper;
            DService.Instance().Log.Information($"[PluginWindow] 已注册: {name} ({wrapper.WindowName})");
        }
    }

    /// <summary>切换指定插件窗口的显隐</summary>
    public static void Toggle(string pluginName)
    {
        if (_windows.TryGetValue(pluginName, out var w))
        {
            w.IsOpen = !w.IsOpen;
            DService.Instance().Log.Information($"[PluginWindow] {pluginName} -> {(w.IsOpen ? "显示" : "隐藏")}");
        }
    }

    /// <summary>显示指定插件窗口</summary>
    public static void Show(string pluginName)
    {
        if (_windows.TryGetValue(pluginName, out var w))
            w.IsOpen = true;
    }

    /// <summary>隐藏指定插件窗口</summary>
    public static void Hide(string pluginName)
    {
        if (_windows.TryGetValue(pluginName, out var w))
            w.IsOpen = false;
    }

    /// <summary>ImGui Window 包装 —— 委托 IPluginWindow.Draw()</summary>
    internal sealed class PluginWindowWrapper : Window
    {
        readonly IPluginWindow _window;

        public PluginWindowWrapper(IPluginWindow window)
            : base($"{window.Title}##Plugin")
        {
            _window = window;
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(200, 150),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
            IsOpen = false;
        }

        public override void Draw() => _window.Draw();
    }
}
```

- [ ] **Step 2: 构建验证**

```powershell
dotnet build "E:\DalamudPlugins\HiAuRo\HiAuRo\HiAuRo.csproj" -c Release -nologo
```

- [ ] **Step 3: Commit**

```powershell
git add "E:\DalamudPlugins\HiAuRo\HiAuRo\Runtime\PluginWindowManager.cs"
git commit -m "feat: 新增 PluginWindowManager, 插件窗口注册到 WindowSystem"
```

---

### Task 3: 接入 Plugin.cs 初始化流程

**Files:**
- Modify: `E:\DalamudPlugins\HiAuRo\HiAuRo\Plugin.cs`

- [ ] **Step 1: 在 PluginLifecycle.Init 之后调用 PluginWindowManager.Init**

找到 Plugin.cs 中 `PluginLifecycle.Init(...)` 调用位置，在其后添加:

```csharp
            PluginWindowManager.Init(_windowSystem);
```

- [ ] **Step 2: 构建验证**

```powershell
dotnet build "E:\DalamudPlugins\HiAuRo\HiAuRo\HiAuRo.csproj" -c Release -nologo
```

- [ ] **Step 3: Commit**

```powershell
git add "E:\DalamudPlugins\HiAuRo\HiAuRo\Plugin.cs"
git commit -m "feat: 接入 PluginWindowManager 到启动流程"
```
