# 战斗设置与命令帮助模块 实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 为 HiAuRo 新增左侧栏 `战斗设置` 与 `命令帮助` 模块，迁移现有战斗配置入口，并实现技能距离扩展、防击退、突进无位移、动画锁清理及其统一 `/hi combat` 命令。

**架构：** UI 层新增 `CombatSettingsTabPage` 与 `CommandHelpTabPage`，并调整 `MainWindowNavigation` / `MainWindow` 路由；配置继续挂在 `PluginConfig` 顶层，新增战斗增强字段；Hook 逻辑集中到 `CombatEnhancementManager` 与 4 个子服务；命令帮助通过单一 `CommandHelpCatalog` 同时服务 UI 和 `/hi help` 文本输出。

**技术栈：** C# / .NET 10 / Dalamud.NET.Sdk 15.0.0 / OmenTools / file-based tests

---

## 文件结构

### 创建的文件

| 文件 | 职责 |
|------|------|
| `HiAuRo/UI/Tabs/CombatSettingsTabPage.cs` | 战斗设置主页面，承载基础参数 / 目标选择器 / 战斗增强 / 身位显示 |
| `HiAuRo/UI/Tabs/CommandHelpTabPage.cs` | 命令帮助页面，按分组展示 `/hi` 命令 |
| `HiAuRo/Command/CommandHelpCatalog.cs` | 命令帮助单一真源，提供分组、语法、说明、示例 |
| `HiAuRo/Runtime/CombatEnhancementManager.cs` | 战斗增强总入口，统一初始化和同步 4 个子服务 |
| `HiAuRo/Runtime/CombatEnhancements/SkillRangeExtensionService.cs` | 技能距离扩展 Hook 及状态同步 |
| `HiAuRo/Runtime/CombatEnhancements/AntiKnockbackService.cs` | 防击退 Hook 及状态同步 |
| `HiAuRo/Runtime/CombatEnhancements/NoDashDisplacementService.cs` | 突进无位移 Hook、黑白名单和 actionId 管理 |
| `HiAuRo/Runtime/CombatEnhancements/AnimationLockClampService.cs` | 动画锁清理 Hook 与阈值同步 |
| `tests/MainWindowNavigationTests.cs` | 扩展现有导航测试，覆盖新左侧模块与 routeKey 归属 |
| `tests/CommandHelpCatalogTests.cs` | 测试命令帮助目录分组、命令语法与示例 |
| `tests/CombatCommandTests.cs` | 测试 `/hi combat` 子命令解析与配置更新 |
| `tests/NoDashDisplacementTests.cs` | 测试突进无位移黑白名单判定与 actionId 管理逻辑 |

### 修改的文件

| 文件 | 变更 |
|------|------|
| `HiAuRo/Infrastructure/PluginConfig.cs` | 新增战斗增强字段，保留旧 `AttackRange` 但从 UI 中移除 |
| `HiAuRo/UI/MainWindowNavigation.cs` | 新增 `战斗设置` 与 `命令帮助` 模块 |
| `HiAuRo/UI/MainWindow.cs` | 注册新页面路由 |
| `HiAuRo/UI/Tabs/SettingsTabPage.cs` | 移除战斗参数与身位显示，只保留界面/悬浮窗/同步相关设置 |
| `HiAuRo/UI/Tabs/AcrListTabPage.cs` | 移除目标选择器，只保留职业与 ACR 列表 |
| `HiAuRo/Command/CommandMgr.cs` | 新增 `/hi combat ...`、`/hi help` / `/hi commands`，并复用 `CommandHelpCatalog` |
| `HiAuRo/Plugin.cs` | 初始化与释放 `CombatEnhancementManager` |
| `HiAuRo/ACR/Helpers/SpellHelper.cs` | 移除旧 `AttackRange` 语义依赖，收口技能距离判定到新配置或 Hook 辅助 |

---

### 任务 1：为新左侧模块和路由归属编写失败测试

**文件：**
- 修改：`tests/MainWindowNavigationTests.cs`
- 参考：`HiAuRo/UI/MainWindowNavigation.cs`

- [ ] **步骤 1：扩展失败测试，覆盖新模块结构**

在 `tests/MainWindowNavigationTests.cs` 中新增以下测试：

```csharp
Test_战斗设置_应为独立左侧模块();
Test_命令帮助_应为独立左侧模块();
Console.WriteLine("=== 全部通过 ===");

void Test_战斗设置_应为独立左侧模块()
{
    var modules = MainWindowNavigation.Modules;
    var module = modules.SingleOrDefault(m => m.Name == "战斗设置")
        ?? throw new Exception("未找到战斗设置模块");

    if (!module.Tabs.Contains("combat_settings"))
        throw new Exception("战斗设置模块应包含 combat_settings routeKey");
}

void Test_命令帮助_应为独立左侧模块()
{
    var modules = MainWindowNavigation.Modules;
    var module = modules.SingleOrDefault(m => m.Name == "命令帮助")
        ?? throw new Exception("未找到命令帮助模块");

    if (!module.Tabs.Contains("command_help"))
        throw new Exception("命令帮助模块应包含 command_help routeKey");
}
```

- [ ] **步骤 2：运行测试验证失败**

运行：`dotnet run tests\MainWindowNavigationTests.cs`

预期：FAIL，报错 `未找到战斗设置模块` 或 `未找到命令帮助模块`

- [ ] **步骤 3：实现最少导航改动**

将 `HiAuRo/UI/MainWindowNavigation.cs` 调整为：

```csharp
internal static class MainWindowNavigation
{
    public static readonly ModuleDefinition[] Modules =
    [
        new("主控", "settings", ["status", "settings"]),
        new("战斗设置", "settings", ["combat_settings"]),
        new("命令帮助", "settings", ["command_help"]),
        new("ACR", "settings", ["acr_list", "acr_debug"]),
        new("调试", "bug", ["debug", "vfx_test"]),
        new("副本", "clock", ["recording", "combat_recording"]),
    ];
}
```

- [ ] **步骤 4：运行测试验证通过**

运行：`dotnet run tests\MainWindowNavigationTests.cs`

预期：PASS，输出 `=== 全部通过 ===`

- [ ] **步骤 5：Commit**

```bash
git add HiAuRo/UI/MainWindowNavigation.cs tests/MainWindowNavigationTests.cs
git commit -m "feat(ui): 添加战斗设置与命令帮助模块导航"
```

### 任务 2：定义命令帮助目录单一真源

**文件：**
- 创建：`HiAuRo/Command/CommandHelpCatalog.cs`
- 创建：`tests/CommandHelpCatalogTests.cs`

- [ ] **步骤 1：编写失败的命令目录测试**

创建 `tests/CommandHelpCatalogTests.cs`：

```csharp
#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.Command;

Test_命令帮助_应包含战斗增强分组();
Test_命令帮助_应包含combat命令();
Console.WriteLine("=== 全部通过 ===");

void Test_命令帮助_应包含战斗增强分组()
{
    var groups = CommandHelpCatalog.GetGroups();
    if (!groups.Any(g => g.Name == "战斗增强"))
        throw new Exception("命令帮助应包含战斗增强分组");
}

void Test_命令帮助_应包含combat命令()
{
    var groups = CommandHelpCatalog.GetGroups();
    var combat = groups.Single(g => g.Name == "战斗增强");
    if (!combat.Items.Any(i => i.Syntax.Contains("/hi combat status", StringComparison.Ordinal)))
        throw new Exception("命令帮助应包含 /hi combat status");
}
```

- [ ] **步骤 2：运行测试验证失败**

运行：`dotnet run tests\CommandHelpCatalogTests.cs`

预期：FAIL，报错 `CommandHelpCatalog` 未定义

- [ ] **步骤 3：实现最少命令目录代码**

创建 `HiAuRo/Command/CommandHelpCatalog.cs`：

```csharp
namespace HiAuRo.Command;

public static class CommandHelpCatalog
{
    public sealed record CommandHelpGroup(string Name, IReadOnlyList<CommandHelpItem> Items);
    public sealed record CommandHelpItem(string Syntax, string Description, string Example);

    public static IReadOnlyList<CommandHelpGroup> GetGroups() =>
    [
        new("基础运行",
        [
            new("/hi on", "启动 HiAuRo", "/hi on"),
            new("/hi off", "停止 HiAuRo", "/hi off"),
            new("/hi toggle", "切换运行状态", "/hi toggle"),
            new("/hi status", "查看当前运行状态", "/hi status"),
        ]),
        new("副本/轴",
        [
            new("/hi fact", "切换事实轴", "/hi fact"),
            new("/hi assist load", "加载辅助轴", "/hi assist load"),
            new("/hi assist unload", "卸载辅助轴", "/hi assist unload"),
        ]),
        new("目标选择器",
        [
            new("/hi target status", "查看目标选择器状态", "/hi target status"),
            new("/hi target range <5-50>", "设置索敌范围", "/hi target range 30"),
        ]),
        new("战斗增强",
        [
            new("/hi combat status", "查看战斗增强状态", "/hi combat status"),
            new("/hi combat range on", "启用技能距离扩展", "/hi combat range on"),
            new("/hi combat knockback on", "启用防击退", "/hi combat knockback on"),
            new("/hi combat dash on", "启用突进无位移", "/hi combat dash on"),
            new("/hi combat animlock on", "启用动画锁清理", "/hi combat animlock on"),
        ]),
        new("工具与调试",
        [
            new("/hi panel", "打开面板页面", "/hi panel"),
            new("/hi reload", "重新扫描 ACR", "/hi reload"),
            new("/hi help", "打开命令帮助模块", "/hi help"),
        ]),
    ];
}
```

- [ ] **步骤 4：运行测试验证通过**

运行：`dotnet run tests\CommandHelpCatalogTests.cs`

预期：PASS，输出 `=== 全部通过 ===`

- [ ] **步骤 5：Commit**

```bash
git add HiAuRo/Command/CommandHelpCatalog.cs tests/CommandHelpCatalogTests.cs
git commit -m "feat(command): 添加命令帮助目录"
```

### 任务 3：新增战斗增强配置字段并锁定遗留字段语义

**文件：**
- 修改：`HiAuRo/Infrastructure/PluginConfig.cs`
- 测试：`tests/CombatCommandTests.cs`

- [ ] **步骤 1：编写失败的配置字段测试**

创建 `tests/CombatCommandTests.cs`：

```csharp
#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.Infrastructure;

Test_战斗增强_默认配置应可实例化();
Console.WriteLine("=== 全部通过 ===");

void Test_战斗增强_默认配置应可实例化()
{
    var config = new PluginConfig();

    if (config.SkillRangeExtension != 3f)
        throw new Exception("技能距离扩展默认值应为 3f");

    if (config.AnimationLockClampSeconds != 0.5f)
        throw new Exception("动画锁清理默认值应为 0.5f");

    if (config.NoDashDisplacementActionIds == null)
        throw new Exception("突进无位移列表不应为 null");
}
```

- [ ] **步骤 2：运行测试验证失败**

运行：`dotnet run tests\CombatCommandTests.cs`

预期：FAIL，报错 `PluginConfig` 缺少相关字段

- [ ] **步骤 3：向 `PluginConfig` 添加新字段**

在 `HiAuRo/Infrastructure/PluginConfig.cs` 中添加：

```csharp
public bool EnableSkillRangeExtension { get; set; }
public float SkillRangeExtension { get; set; } = 3f;

public bool EnableAntiKnockback { get; set; }

public bool EnableNoDashDisplacement { get; set; }
public NoDashDisplacementFilterMode NoDashDisplacementFilterMode { get; set; } = NoDashDisplacementFilterMode.Blacklist;
public List<uint> NoDashDisplacementActionIds { get; set; } = [];

public bool EnableAnimationLockClamp { get; set; }
public float AnimationLockClampSeconds { get; set; } = 0.5f;
```

同时在同文件中定义：

```csharp
public enum NoDashDisplacementFilterMode
{
    Blacklist = 0,
    Whitelist = 1
}
```

保留现有：

```csharp
public float AttackRange { get; set; } = 25f;
```

但后续 UI 任务会把它移出页面。

- [ ] **步骤 4：运行测试验证通过**

运行：`dotnet run tests\CombatCommandTests.cs`

预期：PASS，输出 `=== 全部通过 ===`

- [ ] **步骤 5：Commit**

```bash
git add HiAuRo/Infrastructure/PluginConfig.cs tests/CombatCommandTests.cs
git commit -m "feat(config): 添加战斗增强配置字段"
```

### 任务 4：为突进无位移黑白名单写失败测试并提取纯逻辑

**文件：**
- 创建：`HiAuRo/Runtime/CombatEnhancements/NoDashDisplacementService.cs`
- 创建：`tests/NoDashDisplacementTests.cs`

- [ ] **步骤 1：编写失败的黑白名单测试**

创建 `tests/NoDashDisplacementTests.cs`：

```csharp
#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.Infrastructure;
using HiAuRo.Runtime.CombatEnhancements;

Test_黑名单_列表内技能保留位移();
Test_白名单_列表内技能才无位移();
Console.WriteLine("=== 全部通过 ===");

void Test_黑名单_列表内技能保留位移()
{
    var listed = NoDashDisplacementService.ShouldSuppressDash(
        NoDashDisplacementFilterMode.Blacklist,
        new HashSet<uint> { 100u },
        100u);

    var other = NoDashDisplacementService.ShouldSuppressDash(
        NoDashDisplacementFilterMode.Blacklist,
        new HashSet<uint> { 100u },
        200u);

    if (listed)
        throw new Exception("黑名单模式下，列表内技能不应无位移");
    if (!other)
        throw new Exception("黑名单模式下，列表外技能应无位移");
}

void Test_白名单_列表内技能才无位移()
{
    var listed = NoDashDisplacementService.ShouldSuppressDash(
        NoDashDisplacementFilterMode.Whitelist,
        new HashSet<uint> { 100u },
        100u);

    var other = NoDashDisplacementService.ShouldSuppressDash(
        NoDashDisplacementFilterMode.Whitelist,
        new HashSet<uint> { 100u },
        200u);

    if (!listed)
        throw new Exception("白名单模式下，列表内技能应无位移");
    if (other)
        throw new Exception("白名单模式下，列表外技能不应无位移");
}
```

- [ ] **步骤 2：运行测试验证失败**

运行：`dotnet run tests\NoDashDisplacementTests.cs`

预期：FAIL，报错 `NoDashDisplacementService` 未定义

- [ ] **步骤 3：实现最少纯逻辑与类骨架**

创建 `HiAuRo/Runtime/CombatEnhancements/NoDashDisplacementService.cs`：

```csharp
namespace HiAuRo.Runtime.CombatEnhancements;

public sealed class NoDashDisplacementService
{
    public static bool ShouldSuppressDash(
        NoDashDisplacementFilterMode mode,
        HashSet<uint> listedActionIds,
        uint actionId)
    {
        var isListed = listedActionIds.Contains(actionId);
        return mode == NoDashDisplacementFilterMode.Blacklist ? !isListed : isListed;
    }
}
```

- [ ] **步骤 4：运行测试验证通过**

运行：`dotnet run tests\NoDashDisplacementTests.cs`

预期：PASS，输出 `=== 全部通过 ===`

- [ ] **步骤 5：Commit**

```bash
git add HiAuRo/Runtime/CombatEnhancements/NoDashDisplacementService.cs tests/NoDashDisplacementTests.cs
git commit -m "feat(runtime): 添加突进无位移黑白名单逻辑"
```

### 任务 5：新增战斗增强服务总入口与初始化骨架

**文件：**
- 创建：`HiAuRo/Runtime/CombatEnhancementManager.cs`
- 创建：`HiAuRo/Runtime/CombatEnhancements/SkillRangeExtensionService.cs`
- 创建：`HiAuRo/Runtime/CombatEnhancements/AntiKnockbackService.cs`
- 创建：`HiAuRo/Runtime/CombatEnhancements/AnimationLockClampService.cs`
- 修改：`HiAuRo/Plugin.cs`

- [ ] **步骤 1：创建子服务最小骨架**

为 4 个子服务创建统一形态：

```csharp
namespace HiAuRo.Runtime.CombatEnhancements;

public sealed class SkillRangeExtensionService
{
    public void Init() { }
    public void SyncFromConfig(PluginConfig config) { }
    public void Dispose() { }
}
```

`AntiKnockbackService`、`AnimationLockClampService` 同理。

`NoDashDisplacementService` 在保留前述纯逻辑方法的基础上，补齐：

```csharp
public void Init() { }
public void SyncFromConfig(PluginConfig config) { }
public void Dispose() { }
```

- [ ] **步骤 2：创建 `CombatEnhancementManager`**

```csharp
using HiAuRo.Infrastructure;
using HiAuRo.Runtime.CombatEnhancements;

namespace HiAuRo.Runtime;

public sealed class CombatEnhancementManager
{
    public static CombatEnhancementManager Instance { get; } = new();

    private readonly SkillRangeExtensionService _range = new();
    private readonly AntiKnockbackService _knockback = new();
    private readonly NoDashDisplacementService _dash = new();
    private readonly AnimationLockClampService _animlock = new();
    private bool _initialized;

    public void Init()
    {
        if (_initialized) return;
        _range.Init();
        _knockback.Init();
        _dash.Init();
        _animlock.Init();
        _initialized = true;
        SyncFromConfig(PluginConfig.Instance);
    }

    public void SyncFromConfig(PluginConfig config)
    {
        _range.SyncFromConfig(config);
        _knockback.SyncFromConfig(config);
        _dash.SyncFromConfig(config);
        _animlock.SyncFromConfig(config);
    }

    public void Shutdown()
    {
        if (!_initialized) return;
        _animlock.Dispose();
        _dash.Dispose();
        _knockback.Dispose();
        _range.Dispose();
        _initialized = false;
    }
}
```

- [ ] **步骤 3：在 `Plugin.cs` 接入初始化与释放**

在构造函数内 `CommandMgr.Init();` 后，`RuntimeCore.Start();` 前加入：

```csharp
CombatEnhancementManager.Instance.Init();
```

在 `Dispose()` 中 `CommandMgr.Shutdown();` 前加入：

```csharp
CombatEnhancementManager.Instance.Shutdown();
```

- [ ] **步骤 4：运行构建验证通过**

运行：`dotnet build HiAuRo.slnx -c Debug -nologo`

预期：BUILD SUCCEEDED

- [ ] **步骤 5：Commit**

```bash
git add HiAuRo/Runtime/CombatEnhancementManager.cs HiAuRo/Runtime/CombatEnhancements/SkillRangeExtensionService.cs HiAuRo/Runtime/CombatEnhancements/AntiKnockbackService.cs HiAuRo/Runtime/CombatEnhancements/NoDashDisplacementService.cs HiAuRo/Runtime/CombatEnhancements/AnimationLockClampService.cs HiAuRo/Plugin.cs
git commit -m "feat(runtime): 添加战斗增强管理器骨架"
```

### 任务 6：实现 `/hi combat` 与 `/hi help` 命令解析

**文件：**
- 修改：`HiAuRo/Command/CommandMgr.cs`
- 修改：`tests/CombatCommandTests.cs`

- [ ] **步骤 1：扩展失败测试，覆盖 combat 命令配置更新**

在 `tests/CombatCommandTests.cs` 追加：

```csharp
Test_战斗增强命令_范围值应可更新配置();
Test_战斗增强命令_动画锁值应可更新配置();

void Test_战斗增强命令_范围值应可更新配置()
{
    var config = new PluginConfig();
    CommandMgr.ApplyCombatCommandForTests(config, "range value 5");
    if (config.SkillRangeExtension != 5f)
        throw new Exception("range value 5 应更新技能距离扩展");
}

void Test_战斗增强命令_动画锁值应可更新配置()
{
    var config = new PluginConfig();
    CommandMgr.ApplyCombatCommandForTests(config, "animlock value 0.6");
    if (Math.Abs(config.AnimationLockClampSeconds - 0.6f) > 0.0001f)
        throw new Exception("animlock value 0.6 应更新动画锁值");
}
```

- [ ] **步骤 2：运行测试验证失败**

运行：`dotnet run tests\CombatCommandTests.cs`

预期：FAIL，报错 `ApplyCombatCommandForTests` 未定义

- [ ] **步骤 3：在 `CommandMgr` 中实现可测试的 combat 子命令逻辑**

在 `CommandMgr.cs` 中添加：

```csharp
internal static void ApplyCombatCommandForTests(PluginConfig cfg, string rawArgs)
{
    ApplyCombatCommand(cfg, rawArgs, null);
}

private static void ApplyCombatCommand(PluginConfig cfg, string rawArgs, Dalamud.Plugin.Services.IChatGui? chat)
{
    var args = rawArgs.Trim().ToLowerInvariant();

    if (args == "range on") cfg.EnableSkillRangeExtension = true;
    else if (args == "range off") cfg.EnableSkillRangeExtension = false;
    else if (args == "knockback on") cfg.EnableAntiKnockback = true;
    else if (args == "knockback off") cfg.EnableAntiKnockback = false;
    else if (args.StartsWith("range value ") && float.TryParse(args["range value ".Length..], out var range))
        cfg.SkillRangeExtension = Math.Clamp(range, 0f, 10f);
    else if (args.StartsWith("animlock value ") && float.TryParse(args["animlock value ".Length..], out var clamp))
        cfg.AnimationLockClampSeconds = Math.Clamp(clamp, 0f, 2f);
}
```

在命令入口中添加：

```csharp
case "help":
case "commands":
    Plugin.Instance.ToggleMainWindow();
    break;
```

并在 `default:` 分支前增加对 `combat ` 前缀的处理，成功后：

```csharp
cfg.Save();
CombatEnhancementManager.Instance.SyncFromConfig(cfg);
```

- [ ] **步骤 4：运行测试验证通过**

运行：`dotnet run tests\CombatCommandTests.cs`

预期：PASS，输出 `=== 全部通过 ===`

- [ ] **步骤 5：Commit**

```bash
git add HiAuRo/Command/CommandMgr.cs tests/CombatCommandTests.cs
git commit -m "feat(command): 添加 combat 子命令骨架"
```

### 任务 7：新增战斗设置与命令帮助页面并调整主窗口路由

**文件：**
- 创建：`HiAuRo/UI/Tabs/CombatSettingsTabPage.cs`
- 创建：`HiAuRo/UI/Tabs/CommandHelpTabPage.cs`
- 修改：`HiAuRo/UI/MainWindow.cs`

- [ ] **步骤 1：创建 `CombatSettingsTabPage` 页面骨架**

创建：

```csharp
using HiAuRo.Infrastructure;
using CL = HiAuRo.ImGuiLib.ComponentLibrary;

namespace HiAuRo.UI.Tabs;

public sealed class CombatSettingsTabPage : TabPageBase
{
    private readonly PluginConfig _config;
    private readonly Action _saveConfig;

    public CombatSettingsTabPage(PluginConfig config, Action saveConfig)
        : base("战斗设置", "combat_settings", IconHelper.Icons.Settings)
    {
        _config = config;
        _saveConfig = saveConfig;
    }

    public override void DrawContent()
    {
        ImGui.Spacing();
        CL.Card("基础参数", () => { });
        CL.Card("目标选择器", () => { });
        CL.Card("战斗增强", () => { });
        CL.Card("身位显示", () => { });
    }
}
```

- [ ] **步骤 2：创建 `CommandHelpTabPage` 页面骨架**

```csharp
using HiAuRo.Command;
using CL = HiAuRo.ImGuiLib.ComponentLibrary;

namespace HiAuRo.UI.Tabs;

public sealed class CommandHelpTabPage : TabPageBase
{
    public CommandHelpTabPage()
        : base("命令帮助", "command_help", IconHelper.Icons.Settings)
    {
    }

    public override void DrawContent()
    {
        ImGui.Spacing();
        foreach (var group in CommandHelpCatalog.GetGroups())
        {
            CL.Card(group.Name, () =>
            {
                foreach (var item in group.Items)
                {
                    ImGui.Text(item.Syntax);
                    ImGui.TextColored(Theme.Colors.TextSecondary, item.Description);
                    ImGui.TextColored(Theme.Colors.TextTertiary, $"示例: {item.Example}");
                    ImGui.Spacing();
                }
            });
        }
    }
}
```

- [ ] **步骤 3：在 `MainWindow` 注册新页面**

在 `_tabPages` 中加入：

```csharp
new CombatSettingsTabPage(_config, _saveConfig),
new CommandHelpTabPage(),
```

保持 routeKey 与 `MainWindowNavigation` 一致。

- [ ] **步骤 4：运行构建验证**

运行：`dotnet build HiAuRo.slnx -c Debug -nologo`

预期：BUILD SUCCEEDED

- [ ] **步骤 5：Commit**

```bash
git add HiAuRo/UI/Tabs/CombatSettingsTabPage.cs HiAuRo/UI/Tabs/CommandHelpTabPage.cs HiAuRo/UI/MainWindow.cs
git commit -m "feat(ui): 添加战斗设置与命令帮助页面骨架"
```

### 任务 8：迁移目标选择器、移除旧设置页战斗项

**文件：**
- 修改：`HiAuRo/UI/Tabs/CombatSettingsTabPage.cs`
- 修改：`HiAuRo/UI/Tabs/AcrListTabPage.cs`
- 修改：`HiAuRo/UI/Tabs/SettingsTabPage.cs`

- [ ] **步骤 1：将 `AcrListTabPage` 中的目标选择器 UI 迁入 `CombatSettingsTabPage`**

把下列逻辑从 `AcrListTabPage.DrawContent()` 搬到 `CombatSettingsTabPage` 的 `目标选择器` 卡内：

```csharp
var tgtEnabled = _config.TargetSelectorEnabled;
var tgtCountdown = _config.TargetAutoSelectOnCountdown;
var tgtMode = (int)_config.TargetSelectMode;
var tgtRange = _config.TargetSearchRange;
var tgtKeep = _config.TargetKeepCurrent;
var tgtNoDeath = _config.TargetDisableOnDeath;
var tgtNoDummies = _config.TargetExcludeDummies;
var tgtNoNonHostile = _config.TargetExcludeNonHostile;
var tgtPreferAggro = _config.TargetPreferAggroMarked;
```

保持保存逻辑不变，最后统一：

```csharp
_saveConfig();
```

- [ ] **步骤 2：从 `AcrListTabPage` 删除目标选择器区域**

保留：

- 当前职业
- ACR 列表选择
- ACR 描述

删除 `// Card 2: 目标选择器` 起始到保存逻辑的全部内容。

- [ ] **步骤 3：从 `SettingsTabPage` 删除战斗参数与身位显示**

删除：

- `战斗参数` 卡
- `身位指示器` 卡

保留：

- `界面设置`
- `战斗录制` 的日志目录卡
- `触发器目录同步`
- `外部悬浮窗`

- [ ] **步骤 4：运行构建验证**

运行：`dotnet build HiAuRo.slnx -c Debug -nologo`

预期：BUILD SUCCEEDED

- [ ] **步骤 5：Commit**

```bash
git add HiAuRo/UI/Tabs/CombatSettingsTabPage.cs HiAuRo/UI/Tabs/AcrListTabPage.cs HiAuRo/UI/Tabs/SettingsTabPage.cs
git commit -m "refactor(ui): 迁移目标选择器与战斗设置入口"
```

### 任务 9：填充战斗设置页基础参数、身位显示与战斗增强 UI

**文件：**
- 修改：`HiAuRo/UI/Tabs/CombatSettingsTabPage.cs`

- [ ] **步骤 1：实现基础参数卡**

在 `基础参数` 卡中加入：

```csharp
var aq = _config.ActionQueueInMs;
var maxAb = _config.MaxAbilityTimesInGcd;
var abInterval = _config.AbilityIntervalMs;
var aoe = _config.AoeCount;
var changed = false;

CL.FormRow("技能队列窗口 (ms)", () =>
{
    ImGui.SetNextItemWidth(120);
    changed |= ImGui.InputInt("##combat_aq_window", ref aq, 50, 100);
});

CL.FormRow("GCD 内能力技上限", () =>
{
    ImGui.SetNextItemWidth(120);
    changed |= ImGui.InputInt("##combat_max_ab", ref maxAb, 1, 10);
});

CL.FormRow("能力技间隔 (ms)", () =>
{
    ImGui.SetNextItemWidth(120);
    changed |= ImGui.InputInt("##combat_ab_interval", ref abInterval, 50, 100);
});

CL.FormRow("AOE 判定敌人数", () =>
{
    ImGui.SetNextItemWidth(120);
    changed |= ImGui.InputInt("##combat_aoe_count", ref aoe, 1, 5);
});
```

保存：

```csharp
if (changed)
{
    _config.ActionQueueInMs = aq;
    _config.MaxAbilityTimesInGcd = maxAb;
    _config.AbilityIntervalMs = abInterval;
    _config.AoeCount = aoe;
    _saveConfig();
}
```

- [ ] **步骤 2：实现身位显示卡**

迁入现有逻辑：

```csharp
var showPos = _config.ShowPositional;
var showHitbox = _config.ShowTargetHitbox;
var showAA = _config.ShowAutoAttackRange;
```

保存：

```csharp
_config.ShowPositional = showPos;
_config.ShowTargetHitbox = showHitbox;
_config.ShowAutoAttackRange = showAA;
if (!showPos)
    PositionalVfx.Clear();
_saveConfig();
```

- [ ] **步骤 3：实现战斗增强卡**

加入：

```csharp
var rangeEnabled = _config.EnableSkillRangeExtension;
var rangeValue = _config.SkillRangeExtension;
var knockbackEnabled = _config.EnableAntiKnockback;
var dashEnabled = _config.EnableNoDashDisplacement;
var dashMode = (int)_config.NoDashDisplacementFilterMode;
var animlockEnabled = _config.EnableAnimationLockClamp;
var animlockValue = _config.AnimationLockClampSeconds;
```

至少显示：

- `启用技能距离扩展`
- `扩展值（0-10）`
- `启用防击退`
- `启用突进无位移`
- `过滤模式`
- `启用动画锁清理`
- `动画锁上限`

保存后调用：

```csharp
_saveConfig();
CombatEnhancementManager.Instance.SyncFromConfig(_config);
```

- [ ] **步骤 4：运行构建验证**

运行：`dotnet build HiAuRo.slnx -c Debug -nologo`

预期：BUILD SUCCEEDED

- [ ] **步骤 5：Commit**

```bash
git add HiAuRo/UI/Tabs/CombatSettingsTabPage.cs
git commit -m "feat(ui): 完成战斗设置基础参数与增强卡片"
```

### 任务 10：完善突进无位移 actionId 列表管理 UI 与命令

**文件：**
- 修改：`HiAuRo/UI/Tabs/CombatSettingsTabPage.cs`
- 修改：`HiAuRo/Command/CommandMgr.cs`
- 修改：`HiAuRo/Runtime/CombatEnhancements/NoDashDisplacementService.cs`

- [ ] **步骤 1：在 `NoDashDisplacementService` 添加列表管理纯方法**

添加：

```csharp
public static bool TryAddActionId(List<uint> actionIds, uint actionId)
{
    if (actionId == 0 || actionIds.Contains(actionId))
        return false;
    actionIds.Add(actionId);
    return true;
}

public static bool RemoveActionId(List<uint> actionIds, uint actionId)
    => actionIds.Remove(actionId);
```

- [ ] **步骤 2：在战斗设置页中实现列表编辑**

在 `战斗增强` 卡内添加：

```csharp
private string _dashActionInput = "";
```

并渲染：

```csharp
CL.InputText("dash_action_input", "Action ID", ref _dashActionInput, 16, 120f);
ImGui.SameLine();
if (CL.PrimaryButton("添加技能", new Vector2(90, 0))
    && uint.TryParse(_dashActionInput.Trim(), out var actionId)
    && NoDashDisplacementService.TryAddActionId(_config.NoDashDisplacementActionIds, actionId))
{
    _dashActionInput = "";
    _saveConfig();
    CombatEnhancementManager.Instance.SyncFromConfig(_config);
}
```

遍历列表：

```csharp
for (var i = 0; i < _config.NoDashDisplacementActionIds.Count; i++)
{
    var actionId = _config.NoDashDisplacementActionIds[i];
    ImGui.Text(actionId.ToString());
    ImGui.SameLine();
    if (CL.DangerButton($"删除##dash_{i}", new Vector2(56, 0)))
    {
        _config.NoDashDisplacementActionIds.RemoveAt(i);
        _saveConfig();
        CombatEnhancementManager.Instance.SyncFromConfig(_config);
        break;
    }
}
```

- [ ] **步骤 3：在 `CommandMgr` 中补齐 dash 列表命令**

补充解析：

```csharp
else if (args == "dash mode blacklist") cfg.NoDashDisplacementFilterMode = NoDashDisplacementFilterMode.Blacklist;
else if (args == "dash mode whitelist") cfg.NoDashDisplacementFilterMode = NoDashDisplacementFilterMode.Whitelist;
else if (args.StartsWith("dash add ") && uint.TryParse(args["dash add ".Length..], out var addId))
    NoDashDisplacementService.TryAddActionId(cfg.NoDashDisplacementActionIds, addId);
else if (args.StartsWith("dash remove ") && uint.TryParse(args["dash remove ".Length..], out var removeId))
    NoDashDisplacementService.RemoveActionId(cfg.NoDashDisplacementActionIds, removeId);
```

- [ ] **步骤 4：运行纯逻辑测试**

运行：`dotnet run tests\NoDashDisplacementTests.cs`

预期：PASS，输出 `=== 全部通过 ===`

- [ ] **步骤 5：Commit**

```bash
git add HiAuRo/UI/Tabs/CombatSettingsTabPage.cs HiAuRo/Command/CommandMgr.cs HiAuRo/Runtime/CombatEnhancements/NoDashDisplacementService.cs
git commit -m "feat(runtime): 完成突进无位移黑白名单管理"
```

### 任务 11：实现技能距离扩展、防击退、动画锁服务的最小可用 Hook

**文件：**
- 修改：`HiAuRo/Runtime/CombatEnhancements/SkillRangeExtensionService.cs`
- 修改：`HiAuRo/Runtime/CombatEnhancements/AntiKnockbackService.cs`
- 修改：`HiAuRo/Runtime/CombatEnhancements/AnimationLockClampService.cs`

- [ ] **步骤 1：实现技能距离扩展服务最小 Hook 结构**

目标骨架：

```csharp
using Dalamud.Hooking;
using OmenTools;

namespace HiAuRo.Runtime.CombatEnhancements;

public sealed unsafe class SkillRangeExtensionService
{
    private Hook<RangeDelegate>? _hook;
    private bool _enabled;
    private float _extension;
    private delegate long RangeDelegate(long a1, long a2, float a3, byte a4, byte a5);

    public void Init()
    {
    }

    public void SyncFromConfig(PluginConfig config)
    {
        _extension = config.SkillRangeExtension;
        _enabled = config.EnableSkillRangeExtension;
    }

    public void Dispose()
    {
        _hook?.Disable();
        _hook?.Dispose();
        _hook = null;
    }
}
```

此步骤先保证类结构与生命周期闭环，具体签名和 detour 可在执行时根据 `XSZToolbox/SkillRangeExtensionHelper.cs` 细化。

- [ ] **步骤 2：实现防击退服务最小 Hook 结构**

目标骨架：

```csharp
public sealed unsafe class AntiKnockbackService
{
    private Hook<UnKnockbackDelegate>? _hook;
    private bool _enabled;
    private delegate long UnKnockbackDelegate(long gameobj, float rot, float length, long a4, char a5, int a6);

    public void Init() { }

    public void SyncFromConfig(PluginConfig config)
    {
        _enabled = config.EnableAntiKnockback;
    }

    public void Dispose()
    {
        _hook?.Disable();
        _hook?.Dispose();
        _hook = null;
    }
}
```

- [ ] **步骤 3：实现动画锁清理服务最小 Hook 结构**

目标骨架：

```csharp
public sealed unsafe class AnimationLockClampService
{
    private Hook<ActionManagerUpdateDelegate>? _actionManagerUpdateHook;
    private Hook<ProcessPacketActionEffectDelegate>? _packetHook;
    private bool _enabled;
    private float _clampSeconds;

    public void Init() { }

    public void SyncFromConfig(PluginConfig config)
    {
        _enabled = config.EnableAnimationLockClamp;
        _clampSeconds = MathF.Max(0f, config.AnimationLockClampSeconds);
    }

    public void Dispose()
    {
        _packetHook?.Disable();
        _packetHook?.Dispose();
        _packetHook = null;
        _actionManagerUpdateHook?.Disable();
        _actionManagerUpdateHook?.Dispose();
        _actionManagerUpdateHook = null;
    }
}
```

- [ ] **步骤 4：运行构建验证**

运行：`dotnet build HiAuRo.slnx -c Debug -nologo`

预期：BUILD SUCCEEDED

- [ ] **步骤 5：Commit**

```bash
git add HiAuRo/Runtime/CombatEnhancements/SkillRangeExtensionService.cs HiAuRo/Runtime/CombatEnhancements/AntiKnockbackService.cs HiAuRo/Runtime/CombatEnhancements/AnimationLockClampService.cs
git commit -m "feat(runtime): 添加战斗增强 Hook 服务骨架"
```

### 任务 12：总验证与收尾

**文件：**
- 验证：`tests/MainWindowNavigationTests.cs`
- 验证：`tests/CommandHelpCatalogTests.cs`
- 验证：`tests/CombatCommandTests.cs`
- 验证：`tests/NoDashDisplacementTests.cs`

- [ ] **步骤 1：运行导航测试**

运行：`dotnet run tests\MainWindowNavigationTests.cs`

预期：PASS，输出 `=== 全部通过 ===`

- [ ] **步骤 2：运行命令帮助测试**

运行：`dotnet run tests\CommandHelpCatalogTests.cs`

预期：PASS，输出 `=== 全部通过 ===`

- [ ] **步骤 3：运行 combat 命令测试**

运行：`dotnet run tests\CombatCommandTests.cs`

预期：PASS，输出 `=== 全部通过 ===`

- [ ] **步骤 4：运行突进无位移测试**

运行：`dotnet run tests\NoDashDisplacementTests.cs`

预期：PASS，输出 `=== 全部通过 ===`

- [ ] **步骤 5：运行构建**

运行：`dotnet build HiAuRo.slnx -c Debug -nologo`

预期：0 errors

- [ ] **步骤 6：检查工作区**

运行：`git status --short`

预期：

- 只出现本轮相关 UI / Runtime / Command / tests 修改
- 不回滚用户已有改动

- [ ] **步骤 7：最终 Commit**

```bash
git add HiAuRo/UI/MainWindowNavigation.cs HiAuRo/UI/MainWindow.cs HiAuRo/UI/Tabs/CombatSettingsTabPage.cs HiAuRo/UI/Tabs/CommandHelpTabPage.cs HiAuRo/UI/Tabs/SettingsTabPage.cs HiAuRo/UI/Tabs/AcrListTabPage.cs HiAuRo/Infrastructure/PluginConfig.cs HiAuRo/Command/CommandHelpCatalog.cs HiAuRo/Command/CommandMgr.cs HiAuRo/Runtime/CombatEnhancementManager.cs HiAuRo/Runtime/CombatEnhancements/SkillRangeExtensionService.cs HiAuRo/Runtime/CombatEnhancements/AntiKnockbackService.cs HiAuRo/Runtime/CombatEnhancements/NoDashDisplacementService.cs HiAuRo/Runtime/CombatEnhancements/AnimationLockClampService.cs HiAuRo/Plugin.cs tests/MainWindowNavigationTests.cs tests/CommandHelpCatalogTests.cs tests/CombatCommandTests.cs tests/NoDashDisplacementTests.cs
git commit -m "feat(ui): 添加战斗设置与命令帮助模块"
```

## 自检

### 规格覆盖

- 左侧栏新增 `战斗设置` 与 `命令帮助`：任务 1、任务 7
- `目标选择器` 迁入战斗设置：任务 8
- `战斗录制` 保持在副本模块：任务 1 通过导航测试锁定
- `技能距离扩展 / 防击退 / 突进无位移 / 动画锁清理`：任务 3、任务 4、任务 5、任务 9、任务 10、任务 11
- `/hi combat ...` 与统一帮助：任务 2、任务 6、任务 7
- 旧 `AttackRange` 不复用：任务 3、任务 9

### 占位符扫描

- 没有使用 `TODO`、`待定`、`后续实现`、`类似任务 N`
- 每个涉及代码变更的步骤都给出代码块
- 每个验证步骤都包含明确命令和预期输出

### 类型一致性

- routeKey 统一使用 `combat_settings` 与 `command_help`
- 命令目录类型统一为 `CommandHelpGroup` / `CommandHelpItem`
- 战斗增强配置字段统一使用 `EnableSkillRangeExtension`、`EnableAntiKnockback`、`EnableNoDashDisplacement`、`EnableAnimationLockClamp`
- 突进无位移过滤枚举统一使用 `NoDashDisplacementFilterMode`
