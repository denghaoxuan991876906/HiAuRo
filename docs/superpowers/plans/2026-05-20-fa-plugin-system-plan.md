# HiAuRo 通用插件系统 & FA 工具分离 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 ACR/Shapes/ 危险区计算模块从 HiAuRo 分离为独立 HiAuRo.FA 插件，建立通用 IPlugin 插件加载体系。

**Architecture:** HiAuRo 定义 IPlugin 接口。HiAuRo.FA 引用 HiAuRo 并实现 IPlugin。HiAuRo 运行时通过 AssemblyLoadContext 扫描并加载 Plugins/*.dll，发现 IPlugin 实例。FA 程序集被显式注入 ScriptCompiler，时间轴脚本作者可通过 `using HiAuRo.FA.Shapes` 调用。

**Tech Stack:** .NET 10, Dalamud.NET.Sdk 15.0.0, System.Runtime.Loader, Microsoft.CodeAnalysis (Roslyn)

---

## 文件布局总览

```
E:\DalamudPlugins\
├── HiAuRo.slnx                         # 修改: 添加 HiAuRo.FA 项目
├── HiAuRo/                             # 宿主 (修改)
│   ├── Plugin/IPlugin.cs               # 新增: 通用插件接口
│   ├── Runtime/PluginLoader.cs         # 新增: 扫描+ALC加载+发现IPlugin
│   ├── Runtime/PluginLifecycle.cs      # 新增: 插件生命周期管理
│   ├── Execution/ScriptCompiler.cs     # 修改: 白名单+using+注入方法
│   ├── Plugin.cs                       # 修改: 初始化流程插入插件加载
│   ├── Runtime/RuntimeCore.cs          # 修改: OnTick 插入插件 Update
│   └── ACR/Shapes/                     # 删除: 整个目录
├── HiAuRo.FA/                          # 新项目 (独立)
│   ├── HiAuRo.FA.csproj                # 新增
│   ├── FaPlugin.cs                     # 新增: IPlugin 实现
│   └── Shapes/                         # 搬迁15文件, 命名空间 HiAuRo.FA.Shapes
│       ├── IAoeZone.cs
│       ├── IField.cs
│       ├── SafePointCalculator.cs
│       ├── SafePointConfig.cs
│       ├── SafePointResult.cs
│       ├── SafeFieldContext.cs
│       ├── CalculationBuilder.cs
│       ├── CircleField.cs
│       ├── RectField.cs
│       ├── AoeCircle.cs
│       ├── AoeRect.cs
│       ├── AoeFan.cs
│       ├── AoeRing.cs
│       ├── AoeCross.cs
│       └── AoeRingFan.cs
```

---

### Task 1: 创建 HiAuRo.FA 项目骨架

**Files:**
- Create: `E:\DalamudPlugins\HiAuRo.FA\HiAuRo.FA.csproj`
- Create: `E:\DalamudPlugins\HiAuRo.FA\FaPlugin.cs`
- Modify: `E:\DalamudPlugins\HiAuRo\HiAuRo.slnx`

- [ ] **Step 1: 创建项目目录**

```powershell
New-Item -ItemType Directory -Path "E:\DalamudPlugins\HiAuRo.FA" -Force
New-Item -ItemType Directory -Path "E:\DalamudPlugins\HiAuRo.FA\Shapes" -Force
```

- [ ] **Step 2: 创建 HiAuRo.FA.csproj**

写入 `E:\DalamudPlugins\HiAuRo.FA\HiAuRo.FA.csproj`:

```xml
<Project Sdk="Dalamud.CN.NET.Sdk/15.0.0">

  <PropertyGroup>
    <AssemblyName>HiAuRo.FA</AssemblyName>
    <RootNamespace>HiAuRo.FA</RootNamespace>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../OmenTools/OmenTools.csproj" />
    <ProjectReference Include="../HiAuRo/HiAuRo.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: 创建 FaPlugin.cs**

写入 `E:\DalamudPlugins\HiAuRo.FA\FaPlugin.cs`:

```csharp
using HiAuRo.Plugin;

namespace HiAuRo.FA;

public sealed class FaPlugin : IPlugin
{
    public string Name => "HiAuRo.FA";
    public string Version => "0.1.0";

    public void Initialize() { }
    public void Update() { }
    public void Dispose() { }
}
```

- [ ] **Step 4: 添加项目到 solution**

修改 `E:\DalamudPlugins\HiAuRo\HiAuRo.slnx`，在第二个 `</Project>` 后添加:

```xml
  <Project Path="../HiAuRo.FA/HiAuRo.FA.csproj">
    <Platform Solution="Debug|*" Project="x64" />
  </Project>
```

修改后的完整内容:
```xml
<Solution>
  <Project Path="HiAuRo/HiAuRo.csproj">
    <Platform Solution="Debug|*" Project="x64" />
  </Project>
  <Project Path="OmenTools/OmenTools.csproj">
    <Platform Solution="Debug|*" Project="x64" />
  </Project>
  <Project Path="../HiAuRo.FA/HiAuRo.FA.csproj">
    <Platform Solution="Debug|*" Project="x64" />
  </Project>
</Solution>
```

- [ ] **Step 5: 验证 FA 项目编译**

```powershell
dotnet build "E:\DalamudPlugins\HiAuRo.FA\HiAuRo.FA.csproj" -c Release -nologo
```
Expected: Build succeeded. 确认 `HiAuRo.FA.dll` 已生成。

- [ ] **Step 6: Commit**

```powershell
git add "E:\DalamudPlugins\HiAuRo\HiAuRo.slnx"
git add "E:\DalamudPlugins\HiAuRo.FA\"
git commit -m "feat: 创建 HiAuRo.FA 项目骨架，实现 IPlugin 入口"
```

---

### Task 2: 搬迁 Shapes 文件到 HiAuRo.FA

**Files:**
- Create: `E:\DalamudPlugins\HiAuRo.FA\Shapes\` 下 15 文件
- Modify: 每个文件的 namespace 声明

- [ ] **Step 1: 逐个复制并修改 namespace**

对以下 15 个文件，从源路径读取内容，将 `namespace HiAuRo.ACR.Shapes;` 替换为 `namespace HiAuRo.FA.Shapes;`，写入目标路径:

| 源文件 | 目标文件 |
|-------|---------|
| `E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\IAoeZone.cs` | `E:\DalamudPlugins\HiAuRo.FA\Shapes\IAoeZone.cs` |
| `E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\IField.cs` | `E:\DalamudPlugins\HiAuRo.FA\Shapes\IField.cs` |
| `E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\SafePointCalculator.cs` | `E:\DalamudPlugins\HiAuRo.FA\Shapes\SafePointCalculator.cs` |
| `E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\SafePointConfig.cs` | `E:\DalamudPlugins\HiAuRo.FA\Shapes\SafePointConfig.cs` |
| `E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\SafePointResult.cs` | `E:\DalamudPlugins\HiAuRo.FA\Shapes\SafePointResult.cs` |
| `E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\SafeFieldContext.cs` | `E:\DalamudPlugins\HiAuRo.FA\Shapes\SafeFieldContext.cs` |
| `E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\CalculationBuilder.cs` | `E:\DalamudPlugins\HiAuRo.FA\Shapes\CalculationBuilder.cs` |
| `E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\CircleField.cs` | `E:\DalamudPlugins\HiAuRo.FA\Shapes\CircleField.cs` |
| `E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\RectField.cs` | `E:\DalamudPlugins\HiAuRo.FA\Shapes\RectField.cs` |
| `E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\AoeCircle.cs` | `E:\DalamudPlugins\HiAuRo.FA\Shapes\AoeCircle.cs` |
| `E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\AoeRect.cs` | `E:\DalamudPlugins\HiAuRo.FA\Shapes\AoeRect.cs` |
| `E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\AoeFan.cs` | `E:\DalamudPlugins\HiAuRo.FA\Shapes\AoeFan.cs` |
| `E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\AoeRing.cs` | `E:\DalamudPlugins\HiAuRo.FA\Shapes\AoeRing.cs` |
| `E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\AoeCross.cs` | `E:\DalamudPlugins\HiAuRo.FA\Shapes\AoeCross.cs` |
| `E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\AoeRingFan.cs` | `E:\DalamudPlugins\HiAuRo.FA\Shapes\AoeRingFan.cs` |

每个文件的唯一改动: `namespace HiAuRo.ACR.Shapes;` → `namespace HiAuRo.FA.Shapes;`，其余代码（using、注释、实现）完全不变。

`using HiAuRo.ACR;` 语句（存在于 AoeFan.cs, AoeRingFan.cs, SafePointCalculator.cs）保持不变 —— FA 引用 HiAuRo，该 using 可正常解析 `MathHelper.NormalizeAngle`。

- [ ] **Step 2: 验证 FA 项目编译通过**

```powershell
dotnet build "E:\DalamudPlugins\HiAuRo.FA\HiAuRo.FA.csproj" -c Release -nologo
```
Expected: Build succeeded, 无编译错误。

- [ ] **Step 3: Commit**

```powershell
git add "E:\DalamudPlugins\HiAuRo.FA\"
git commit -m "feat: 搬迁 Shapes 15 文件到 HiAuRo.FA, namespace → HiAuRo.FA.Shapes"
```

---

### Task 3: 创建 IPlugin 接口

**Files:**
- Create: `E:\DalamudPlugins\HiAuRo\HiAuRo\Plugin\IPlugin.cs`

- [ ] **Step 1: 创建目录并写入接口**

```powershell
New-Item -ItemType Directory -Path "E:\DalamudPlugins\HiAuRo\HiAuRo\Plugin" -Force
```

写入 `E:\DalamudPlugins\HiAuRo\HiAuRo\Plugin\IPlugin.cs`:

```csharp
namespace HiAuRo.Plugin;

/// <summary>
/// 通用插件入口 —— 实现此接口的 DLL 将被 PluginLoader 自动发现并加载
/// 扫描路径: Plugins/*.dll
/// </summary>
public interface IPlugin : IDisposable
{
    string Name { get; }
    string Version { get; }
    void Initialize();
    void Update();
}
```

- [ ] **Step 2: 验证编译**

```powershell
dotnet build "E:\DalamudPlugins\HiAuRo\HiAuRo\HiAuRo.csproj" -c Release -nologo
```
Expected: Build succeeded。

- [ ] **Step 3: Commit**

```powershell
git add "E:\DalamudPlugins\HiAuRo\HiAuRo\Plugin\"
git commit -m "feat: 新增 IPlugin 通用插件接口"
```

---

### Task 4: 创建 PluginLoader

**Files:**
- Create: `E:\DalamudPlugins\HiAuRo\HiAuRo\Runtime\PluginLoader.cs`

- [ ] **Step 1: 写入 PluginLoader.cs**

```csharp
using System.Reflection;
using System.Runtime.Loader;
using HiAuRo.Plugin;

namespace HiAuRo.Runtime;

/// <summary>
/// 通用插件加载器 —— 扫描 Plugins/*.dll, ALC 加载, 发现 IPlugin 实现
/// </summary>
public static class PluginLoader
{
    private static readonly Dictionary<string, PluginRecord> _plugins = [];
    private static readonly HashSet<string> _hostPrefixes =
        ["HiAuRo", "OmenTools", "Dalamud", "FFXIVClientStructs", "Lumina", "ImGuiNET", "TerraFX", "System.", "Microsoft."];

    /// <summary>已加载的插件记录（名称 → 记录）</summary>
    public static IReadOnlyDictionary<string, PluginRecord> Plugins => _plugins;

    /// <summary>已加载插件的程序集列表（供 ScriptCompiler 注入引用）</summary>
    public static IReadOnlyList<Assembly> PluginAssemblies =>
        _plugins.Values.Select(r => r.Assembly).ToList().AsReadOnly();

    /// <summary>扫描 Plugins/ 目录并加载所有插件</summary>
    public static void LoadAll(string pluginDir, string configDir)
    {
        var pluginsPath = Path.Combine(configDir, "Plugins");
        if (!Directory.Exists(pluginsPath))
        {
            Directory.CreateDirectory(pluginsPath);
            DService.Instance().Log.Information($"[PluginLoader] Plugins 目录已创建: {pluginsPath}");
            return;
        }

        foreach (var dllPath in Directory.GetFiles(pluginsPath, "*.dll"))
        {
            try
            {
                var dllName = Path.GetFileNameWithoutExtension(dllPath);
                if (_plugins.ContainsKey(dllName)) continue;

                var dllBytes = File.ReadAllBytes(dllPath);
                var alc = new AssemblyLoadContext($"Plugin_{dllName}", isCollectible: true);
                alc.Resolving += (ctx, name) => ResolveAssembly(ctx, name, pluginsPath);

                using var ms = new MemoryStream(dllBytes, writable: false);
                var asm = alc.LoadFromStream(ms);

                var found = false;
                foreach (var type in asm.GetExportedTypes())
                {
                    if (type is { IsAbstract: false, IsInterface: false } &&
                        typeof(IPlugin).IsAssignableFrom(type))
                    {
                        if (Activator.CreateInstance(type) is IPlugin plugin)
                        {
                            _plugins[dllName] = new PluginRecord
                            {
                                Plugin = plugin,
                                Assembly = asm,
                                Context = alc,
                                DllBytes = dllBytes
                            };
                            found = true;
                            DService.Instance().Log.Information(
                                $"[PluginLoader] {dllName} → {type.Name} v{plugin.Version}");
                        }
                    }
                }

                if (!found)
                {
                    DService.Instance().Log.Warning(
                        $"[PluginLoader] {dllName} 中未找到 IPlugin 实现，卸载 ALC");
                    alc.Unload();
                }
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error(
                    $"[PluginLoader] 加载失败 {Path.GetFileName(dllPath)}: {ex.Message}");
            }
        }

        DService.Instance().Log.Information(
            $"[PluginLoader] 扫描完成，已加载 {_plugins.Count} 个插件");
    }

    /// <summary>程序集解析 —— 同 ACRLoader 模式</summary>
    private static Assembly? ResolveAssembly(AssemblyLoadContext ctx, AssemblyName name, string pluginsDir)
    {
        if (_hostPrefixes.Any(p => name.Name?.StartsWith(p) == true))
        {
            if (name.Name == "HiAuRo")
                return typeof(PluginLoader).Assembly;

            foreach (var asm in AssemblyLoadContext.Default.Assemblies)
            {
                if (asm.GetName().Name == name.Name)
                    return asm;
            }

            var hostAlc = AssemblyLoadContext.GetLoadContext(typeof(PluginLoader).Assembly);
            if (hostAlc != null)
            {
                foreach (var asm in hostAlc.Assemblies)
                {
                    if (asm.GetName().Name == name.Name)
                        return asm;
                }

                try
                {
                    return hostAlc.LoadFromAssemblyName(name);
                }
                catch { }
            }

            try
            {
                return AssemblyLoadContext.Default.LoadFromAssemblyName(name);
            }
            catch { }
        }

        var path = Path.Combine(pluginsDir, name.Name + ".dll");
        if (File.Exists(path))
        {
            var depBytes = File.ReadAllBytes(path);
            using var depMs = new MemoryStream(depBytes, writable: false);
            return ctx.LoadFromStream(depMs);
        }

        return null;
    }

    /// <summary>初始化所有已加载的插件</summary>
    public static void InitializeAll()
    {
        foreach (var (name, record) in _plugins)
        {
            try { record.Plugin.Initialize(); }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[PluginLoader] {name} Initialize 失败: {ex.Message}");
            }
        }
    }

    /// <summary>卸载所有插件和 ALC</summary>
    public static void UnloadAll()
    {
        foreach (var (name, record) in _plugins)
        {
            try { record.Plugin.Dispose(); }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[PluginLoader] {name} Dispose 失败: {ex.Message}");
            }
            try { record.Context.Unload(); }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[PluginLoader] {name} ALC Unload 失败: {ex.Message}");
            }
        }
        _plugins.Clear();
    }

    /// <summary>插件记录</summary>
    public sealed class PluginRecord
    {
        public IPlugin Plugin { get; init; } = null!;
        public Assembly Assembly { get; init; } = null!;
        public AssemblyLoadContext Context { get; init; } = null!;
        public byte[] DllBytes { get; init; } = null!;
    }
}
```

- [ ] **Step 2: 验证编译**

```powershell
dotnet build "E:\DalamudPlugins\HiAuRo\HiAuRo\HiAuRo.csproj" -c Release -nologo
```
Expected: Build succeeded。

- [ ] **Step 3: Commit**

```powershell
git add "E:\DalamudPlugins\HiAuRo\HiAuRo\Runtime\PluginLoader.cs"
git commit -m "feat: 新增 PluginLoader, 扫描 Plugins/*.dll 并 ALC 加载"
```

---

### Task 5: 创建 PluginLifecycle

**Files:**
- Create: `E:\DalamudPlugins\HiAuRo\HiAuRo\Runtime\PluginLifecycle.cs`

- [ ] **Step 1: 写入 PluginLifecycle.cs**

```csharp
namespace HiAuRo.Runtime;

/// <summary>
/// 插件生命周期管理 —— 挂载到 HiAuRo 启动/Tick/关闭流程
/// </summary>
public static class PluginLifecycle
{
    /// <summary>初始化：扫描并加载所有插件</summary>
    public static void Init(string pluginDir, string configDir)
    {
        DService.Instance().Log.Information("[PluginLifecycle] 开始加载插件...");
        PluginLoader.LoadAll(pluginDir, configDir);
        PluginLoader.InitializeAll();

        // 注入插件程序集到 ScriptCompiler
        try
        {
            foreach (var asm in PluginLoader.PluginAssemblies)
            {
                Execution.ScriptCompiler.AddPluginAssembly(asm);
            }
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[PluginLifecycle] 注入 ScriptCompiler 失败: {ex.Message}");
        }
    }

    /// <summary>每帧更新所有已加载插件</summary>
    public static void Update()
    {
        foreach (var (name, record) in PluginLoader.Plugins)
        {
            try { record.Plugin.Update(); }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[PluginLifecycle] {name} Update 失败: {ex.Message}");
            }
        }
    }

    /// <summary>关闭：卸载所有插件</summary>
    public static void Shutdown()
    {
        DService.Instance().Log.Information("[PluginLifecycle] 关闭所有插件...");
        PluginLoader.UnloadAll();
    }
}
```

- [ ] **Step 2: 验证编译**

```powershell
dotnet build "E:\DalamudPlugins\HiAuRo\HiAuRo\HiAuRo.csproj" -c Release -nologo
```
Expected: Build succeeded。

- [ ] **Step 3: Commit**

```powershell
git add "E:\DalamudPlugins\HiAuRo\HiAuRo\Runtime\PluginLifecycle.cs"
git commit -m "feat: 新增 PluginLifecycle 生命周期管理"
```

---

### Task 6: 修改 ScriptCompiler 支持插件程序集

**Files:**
- Modify: `E:\DalamudPlugins\HiAuRo\HiAuRo\Execution\ScriptCompiler.cs`

- [ ] **Step 1: 白名单添加 "HiAuRo.FA"**

修改第 23-39 行，在 `AllowedAssemblyPrefixes` 数组中添加 `"HiAuRo.FA"`:

```csharp
    private static readonly string[] AllowedAssemblyPrefixes =
    [
        "System.Runtime",
        "System.Collections",
        "System.Linq",
        "System.Numerics",
        "System.Memory",
        "System.Primitives",
        "mscorlib",
        "netstandard",
        "HiAuRo.FA",
        "HiAuRo",
        "OmenTools",
        "Dalamud",
        "FFXIVClientStructs",
        "ImGui",
        "Lumina",
    ];
```

注意 `"HiAuRo.FA"` 放在 `"HiAuRo"` **之前**，避免前缀匹配短路（"HiAuRo" 会匹配到 "HiAuRo.FA"）。

- [ ] **Step 2: 脚本 wrapper using 添加 FA namespace**

修改第 168-196 行 `AddScriptWrapper` 方法，在 using 列表添加 `using HiAuRo.FA.Shapes;`:

```csharp
    private static string AddScriptWrapper(string userCode)
    {
        return $@"
using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using HiAuRo;
using HiAuRo.ACR;
using HiAuRo.FA.Shapes;
using static HiAuRo.Data;
using HiAuRo.Execution;
using HiAuRo.Runtime;
using OmenTools;
using OmenTools.OmenService;
using OmenTools.Extensions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace HiAuRo.Scripts;

public sealed class DynamicScript : ITriggerScript
{{
    public bool Check(ITriggerCondParams? condParams)
    {{
        {userCode}
    }}
}}
";
    }
```

- [ ] **Step 3: 新增 AddPluginAssembly 方法**

在 `ClearCache()` 方法之后（第 122 行后）添加:

```csharp
    /// <summary>显式注入插件程序集到编译引用（子 ALC 加载的程序集不在 AppDomain 内）</summary>
    public static void AddPluginAssembly(Assembly asm)
    {
        try
        {
            var name = asm.GetName().Name ?? "";
            using var stream = new MemoryStream();
            // 从程序集清单模块的字节重新创建 MetadataReference
            lock (_refLock)
            {
                // 使用程序集 Location（如果可用），否则跳过
                // 子 ALC 的 LoadFromStream 程序集可能没有 Location
                if (!string.IsNullOrEmpty(asm.Location) && File.Exists(asm.Location))
                {
                    _refCache.Add(MetadataReference.CreateFromFile(asm.Location));
                }
                else
                {
                    // 使用 Assembly.GetName() 检索 MetadataReference —— 不直接支持此路径
                    // 改为由调用方（PluginLoader）提供 dll 字节
                    throw new NotSupportedException("AddPluginAssembly 需要文件路径，请改用 AddPluginReferenceFromImage");
                }

                _refsLoaded = true;
            }
            DService.Instance().Log.Debug($"[ScriptCompiler] 注入插件程序集: {name}");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[ScriptCompiler] 注入程序集失败: {ex.Message}");
        }
    }

    /// <summary>显式注入插件程序集引用（从内存字节）</summary>
    public static void AddPluginReferenceFromImage(byte[] dllBytes)
    {
        lock (_refLock)
        {
            _refCache.Add(MetadataReference.CreateFromImage(dllBytes));
            _refsLoaded = true;
        }
    }
```

修改 `PluginLifecycle.Init()` 中的调用（Task 5 已写为 `ScriptCompiler.AddPluginAssembly(asm)`），此处改为遍历 `PluginLoader.Plugins` 调用 `AddPluginReferenceFromImage`:

**不要修改 Task 5 已写入的文件。** 此处更新 ScriptCompiler 的两个方法和 PluginLifecycle 的调用方式。确保 PluginLifecycle.cs 第 22 行附近改为:

```csharp
            foreach (var (_, record) in PluginLoader.Plugins)
            {
                ScriptCompiler.AddPluginReferenceFromImage(record.DllBytes);
            }
```

- [ ] **Step 4: 验证编译**

```powershell
dotnet build "E:\DalamudPlugins\HiAuRo\HiAuRo\HiAuRo.csproj" -c Release -nologo
```
Expected: Build succeeded。

- [ ] **Step 5: Commit**

```powershell
git add "E:\DalamudPlugins\HiAuRo\HiAuRo\Execution\ScriptCompiler.cs"
git add "E:\DalamudPlugins\HiAuRo\HiAuRo\Runtime\PluginLifecycle.cs"
git commit -m "feat: ScriptCompiler 支持注入插件程序集, FA 脚本可用"
```

---

### Task 7: 接入 HiAuRo 启动与 Tick 流程

**Files:**
- Modify: `E:\DalamudPlugins\HiAuRo\HiAuRo\Plugin.cs`
- Modify: `E:\DalamudPlugins\HiAuRo\HiAuRo\Runtime\RuntimeCore.cs`

- [ ] **Step 1: Plugin.cs — 初始化流程插入插件加载**

在 `Plugin.cs` 第 120-124 行的 ACR 加载代码之后，插入插件加载。修改第 120-126 行:

```csharp
            // 加载外部 ACR
            DService.Instance().Log.Information("[ACR] 开始扫描外部 ACR...");
            ACRLifecycle.Init(_pluginInterface.ConfigDirectory.FullName);
            ACRLoader.LoadAll(_pluginInterface.AssemblyLocation.Directory?.FullName ?? ".");
            DService.Instance().Log.Information("[ACR] 扫描完成, 等待职业切换触发加载");
            ACRLifecycle.ForceRecheck(); // RuntimeCore 可能先于 LoadAll 跑了第一帧，强制重检

            // 加载通用插件
            PluginLifecycle.Init(_pluginInterface.AssemblyLocation.Directory?.FullName ?? ".",
                _pluginInterface.ConfigDirectory.FullName);
```

- [ ] **Step 2: Plugin.cs — Dispose 插入插件关闭**

在 `Plugin.cs` 的 `Dispose()` 方法中，第 226 行附近（`ACRLoader.UnloadAll()` 之前）添加:

```csharp
        PluginLifecycle.Shutdown();
```

- [ ] **Step 3: RuntimeCore.cs — OnTick 插入插件 Update**

在 `RuntimeCore.cs` 第 49 行（`ACRLifecycle.Update()` 之后）添加:

```csharp
            PluginLifecycle.Update();
```

修改后第 44-55 行:

```csharp
    private static void OnTick(Dalamud.Plugin.Services.IFramework _)
    {
        if (!IsRunning) return;
        try
        {
            if (!HiAuRo.Data.IsReady)
            {
                CombatContext.Reset();
                return;
            }

            Coroutine.Instance.Update();
            CombatContext.Check();
            EventSystem.CheckTargetChanged();
            ACR.HotkeyPoller.Update();
            ACRLifecycle.Update();
            PluginLifecycle.Update();
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[RuntimeCore] OnTick 异常: {ex}");
        }
    }
```

- [ ] **Step 4: 验证编译**

```powershell
dotnet build "E:\DalamudPlugins\HiAuRo\HiAuRo\HiAuRo.csproj" -c Release -nologo
```
Expected: Build succeeded。

- [ ] **Step 5: Commit**

```powershell
git add "E:\DalamudPlugins\HiAuRo\HiAuRo\Plugin.cs"
git add "E:\DalamudPlugins\HiAuRo\HiAuRo\Runtime\RuntimeCore.cs"
git commit -m "feat: 接入 PluginLifecycle 到启动/Tick/关闭流程"
```

---

### Task 8: 删除 HiAuRo 中的 ACR/Shapes 目录

**Files:**
- Delete: `E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\` 整个目录

- [ ] **Step 1: 删除目录并验证编译**

```powershell
Remove-Item -LiteralPath "E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes" -Recurse -Force
dotnet build "E:\DalamudPlugins\HiAuRo\HiAuRo\HiAuRo.csproj" -c Release -nologo
```
Expected: Build succeeded, 无编译错误（确认 Shapes 无外部引用）。

- [ ] **Step 2: Commit**

```powershell
git add -u "E:\DalamudPlugins\HiAuRo\HiAuRo\ACR\Shapes\"
git commit -m "refactor: 删除 ACR/Shapes, 已由 HiAuRo.FA 接管"
```

---

### Task 9: 端到端验证

- [ ] **Step 1: 构建 FA 项目**

```powershell
dotnet build "E:\DalamudPlugins\HiAuRo.FA\HiAuRo.FA.csproj" -c Release -nologo
```
Expected: 成功。

- [ ] **Step 2: 手动复制 FA.dll 到 Plugins/**

```powershell
$faBin = "E:\DalamudPlugins\HiAuRo.FA\bin\x64\Release\net10.0-windows\HiAuRo.FA.dll"
$pluginsDir = "E:\DalamudPlugins\HiAuRo\HiAuRo\bin\x64\Release\net10.0-windows\Plugins"
New-Item -ItemType Directory -Path $pluginsDir -Force
Copy-Item -LiteralPath $faBin -Destination $pluginsDir -Force
```

- [ ] **Step 3: 构建 HiAuRo 项目**

```powershell
dotnet build "E:\DalamudPlugins\HiAuRo\HiAuRo\HiAuRo.csproj" -c Release -nologo
```
Expected: 成功。

- [ ] **Step 4: 验证输出目录结构**

```powershell
Get-ChildItem -LiteralPath "E:\DalamudPlugins\HiAuRo\HiAuRo\bin\x64\Release\net10.0-windows" -Filter "HiAuRo*" | Format-Table Name
Get-ChildItem -LiteralPath "E:\DalamudPlugins\HiAuRo\HiAuRo\bin\x64\Release\net10.0-windows\Plugins" | Format-Table Name
```
Expected: 输出目录含 `HiAuRo.dll`, `Plugins/` 子目录含 `HiAuRo.FA.dll`。

- [ ] **Step 5: 代码验证 — 确认导出类型**

```powershell
# 使用 dotnet 反射检查 FA.dll 是否包含预期类型
dotnet script -e '
var asm = System.Reflection.Assembly.LoadFrom(@"E:\DalamudPlugins\HiAuRo.FA\bin\x64\Release\net10.0-windows\HiAuRo.FA.dll");
foreach (var t in asm.GetExportedTypes().OrderBy(x => x.Name))
    Console.WriteLine($"{t.Namespace}.{t.Name}");
'
```
Expected: 输出包含 `HiAuRo.FA.FaPlugin`, `HiAuRo.FA.Shapes.SafePointCalculator`, `HiAuRo.FA.Shapes.IAoeZone` 等。

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "verify: 端到端构建验证通过"
```

---

## 计划自检

- [x] Spec 覆盖: 每个 spec 章节对应一个或多个 task
  - §2 架构 → Task 1, 2
  - §3 IPlugin → Task 3
  - §4 加载机制 → Task 4, 5
  - §5 ScriptCompiler → Task 6
  - §6 HiAuRo.FA 项目 → Task 1, 2
  - §7 HiAuRo 改动 → Task 3-8
  - §8 验证标准 → Task 9
- [x] 无占位符: 无 TBD/TODO/未定义引用
- [x] 类型一致: IPlugin/PluginLoader/PluginLifecycle 接口签名一致
