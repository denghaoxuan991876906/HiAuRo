# HiAuRo 通用插件系统 & FA 工具分离 设计文档

**日期**: 2026-05-20
**状态**: 已批准
**关联需求**: 将危险区计算从 HiAuRo 分离为独立 FA 工具插件

---

## 1. 背景与动机

HiAuRo 现有的 `ACR/Shapes/` 模块（15 文件）提供 AOE 形状定义 + 安全点计算能力。该模块完全自包含，零外部引用。

目标：将此模块分离为独立项目 `HiAuRo.FA`（全自动副本工具），通过通用插件系统动态加载。FA 定位类似 ACR 插件，区别是没有 GCD 循环，只做副本工具功能。

同时建立通用 `IPlugin` 接口，扫描 `Plugins/*.dll`，为未来更多工具型插件提供统一加载骨架。

---

## 2. 架构概览

```
HiAuRo (宿主)
├── Plugin/IPlugin.cs            ← 通用插件接口（类似 IRotationEntry）
├── Runtime/PluginLoader.cs      ← 扫描 Plugins/*.dll, ALC 加载, 发现 IPlugin
├── Runtime/PluginLifecycle.cs   ← 插件实例生命周期管理
├── Execution/ScriptCompiler.cs  ← 加载插件后注入 MetadataReference 到编译上下文
├── (删除) ACR/Shapes/           ← 15 文件搬家到 HiAuRo.FA
└── 不引用 HiAuRo.FA（纯运行时加载）

HiAuRo.FA (独立项目)
├── HiAuRo.FA.csproj             ← Dalamud SDK + OmenTools
├── ProjectReference → HiAuRo.csproj   ← 依赖 HiAuRo，获取 IPlugin（同 ACR 模式）
├── FaPlugin.cs                   ← 实现 IPlugin
└── Shapes/                      ← 搬迁 15 文件, 命名空间 HiAuRo.FA.Shapes
```

**依赖方向（单向，无循环）**:

```
HiAuRo.FA ──引用──→ HiAuRo
  (实现 IPlugin)     (定义 IPlugin)

HiAuRo ──运行时 ALC 加载──→ HiAuRo.FA.dll
  (扫描 Plugins/)            (发现 IPlugin)
```

**与 ACR 插件的对比**:

| | ACR 插件 | FA 插件 |
|---|---------|---------|
| 接口 | IRotationEntry | IPlugin |
| 扫描目录 | ACR/{author}/*.dll | Plugins/*.dll |
| 加载方式 | AssemblyLoadContext | AssemblyLoadContext |
| 循环 | GCD/oGCD 循环 | 无循环，纯工具 |
| 编译时引用 | ACR → HiAuRo | FA → HiAuRo |

### 消费者

| 消费者 | 方式 |
|-------|------|
| 时间轴脚本作者 | ScriptCompiler 注入 FA 程序集 → `using HiAuRo.FA.Shapes` |
| HiAuRo 自身（后续） | 通过 IPlugin 派生接口或反射调用 FA API |

---

## 3. 通用插件接口

```csharp
// HiAuRo/Plugin/IPlugin.cs
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

## 4. 插件加载机制

### 4.1 加载流程

```
PluginLoader.LoadAll()
  ├── 扫描 {ConfigDirectory}/Plugins/*.dll
  ├── 对每个 DLL:
  │   ├── 读入内存字节（释放文件句柄，允许后续更新）
  │   ├── 创建 AssemblyLoadContext($"Plugin_{name}", isCollectible: true)
  │   ├── 注册 Resolving handler:
  │   │   ├── HiAuRo.* → 宿主 ALC（关键：避免类型重复）
  │   │   ├── OmenTools.* → 宿主 ALC
  │   │   ├── Dalamud.* → Default/宿主 ALC
  │   │   └── System.* / Microsoft.* → Default ALC
  │   ├── LoadFromStream → 保存 Assembly + ALC
  │   ├── 反射扫描 → 发现 IPlugin 实现
  │   ├── 实例化 → 存入 _plugins 列表
  │   └── 将 Assembly 的 MetadataReference 注入 ScriptCompiler
  ├── 逐个调用 Initialize()
  └── 日志输出加载结果
```

### 4.2 生命周期

```
PluginLifecycle
  LoadAll()     ← HiAuRo 启动时调用（ACRLifecycle.Init 之后）
  Update()      ← 每帧 RuntimeCore.OnTick 中调用（ACRLifecycle.Update 之后）
  Reload()      ← 卸载所有插件 ALC → 重新扫描
  Shutdown()    ← Dispose 所有插件 → Unload ALC
```

### 4.3 设计约束

- 同 ACRLoader 风格：扫描目录、ALC 加载、Resolving 共享宿主
- Resolving handler 将 `HiAuRo.*` 引用重定向到宿主 ALC——FA 插件的 IPlugin 类型身份必须与 HiAuRo 的 IPlugin 一致
- 插件 DLL 加载失败不阻塞启动，记录日志继续
- 不需要 Reflection.Emit 代理（Helper 需要回写 HiAuRo 内部字段，FA 不需要）

---

## 5. ScriptCompiler 集成

### 5.1 目标

让时间轴脚本作者在 TreeScriptNode 的 C# 代码中直接使用 FA 类型：

```csharp
using HiAuRo.FA.Shapes;

var calc = new SafePointCalculator(new CircleField(boss.Position, 20));
var result = calc.Begin()
    .WithAoe(new AoeCircle(target.Position, 5))
    .Calculate(new SafePointConfig().RefPoint(Self.Position).Nearest(1));
```

### 5.2 实现

- `AllowedAssemblyPrefixes` 添加 `"HiAuRo.FA"`
- `PluginLoader.LoadAll()` 完成后：
  - 对所有已加载插件的 Assembly，调用 `MetadataReference.CreateFromImage(byte[])` 创建引用
  - 将引用添加至 `ScriptCompiler._refCache`
  - 调用 `ScriptCompiler.ClearCache()` 不一定够，因为子 ALC 加载的程序集不在 `AppDomain.CurrentDomain.GetAssemblies()` 中
  - 需要显式的 `ScriptCompiler.AddPluginReference(Assembly)` 方法
- 脚本 wrapper 的 using 列表添加 `using HiAuRo.FA.Shapes;`

---

## 6. HiAuRo.FA 项目

### 6.1 项目配置

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

### 6.2 FaPlugin.cs

```csharp
// HiAuRo.FA/FaPlugin.cs
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

### 6.3 搬迁文件清单（15 文件）

| 原路径 | 新路径 | 新命名空间 |
|-------|--------|----------|
| `ACR/Shapes/IAoeZone.cs` | `Shapes/IAoeZone.cs` | `HiAuRo.FA.Shapes` |
| `ACR/Shapes/IField.cs` | `Shapes/IField.cs` | `HiAuRo.FA.Shapes` |
| `ACR/Shapes/SafePointCalculator.cs` | `Shapes/SafePointCalculator.cs` | `HiAuRo.FA.Shapes` |
| `ACR/Shapes/SafePointConfig.cs` | `Shapes/SafePointConfig.cs` | `HiAuRo.FA.Shapes` |
| `ACR/Shapes/SafePointResult.cs` | `Shapes/SafePointResult.cs` | `HiAuRo.FA.Shapes` |
| `ACR/Shapes/SafeFieldContext.cs` | `Shapes/SafeFieldContext.cs` | `HiAuRo.FA.Shapes` |
| `ACR/Shapes/CalculationBuilder.cs` | `Shapes/CalculationBuilder.cs` | `HiAuRo.FA.Shapes` |
| `ACR/Shapes/CircleField.cs` | `Shapes/CircleField.cs` | `HiAuRo.FA.Shapes` |
| `ACR/Shapes/RectField.cs` | `Shapes/RectField.cs` | `HiAuRo.FA.Shapes` |
| `ACR/Shapes/AoeCircle.cs` | `Shapes/AoeCircle.cs` | `HiAuRo.FA.Shapes` |
| `ACR/Shapes/AoeRect.cs` | `Shapes/AoeRect.cs` | `HiAuRo.FA.Shapes` |
| `ACR/Shapes/AoeFan.cs` | `Shapes/AoeFan.cs` | `HiAuRo.FA.Shapes` |
| `ACR/Shapes/AoeRing.cs` | `Shapes/AoeRing.cs` | `HiAuRo.FA.Shapes` |
| `ACR/Shapes/AoeCross.cs` | `Shapes/AoeCross.cs` | `HiAuRo.FA.Shapes` |
| `ACR/Shapes/AoeRingFan.cs` | `Shapes/AoeRingFan.cs` | `HiAuRo.FA.Shapes` |

每个文件仅需修改 `namespace` 声明行（`HiAuRo.ACR.Shapes` → `HiAuRo.FA.Shapes`），其余代码不变。

---

## 7. HiAuRo 改动清单

### 7.1 新增文件

| 文件 | 说明 |
|------|------|
| `Plugin/IPlugin.cs` | 通用插件接口 |
| `Runtime/PluginLoader.cs` | 扫描 Plugins/*.dll + ALC 加载 + 发现 IPlugin |
| `Runtime/PluginLifecycle.cs` | 插件生命周期管理 |

### 7.2 修改文件

| 文件 | 修改内容 |
|------|---------|
| `Execution/ScriptCompiler.cs` | `AllowedAssemblyPrefixes` 加 `"HiAuRo.FA"`；wrapper using 加 `using HiAuRo.FA.Shapes;`；新增 `AddPluginReference(Assembly)` 方法供 PluginLoader 注入 MetadataReference |
| `Plugin.cs` | 初始化流程中插入 `PluginLoader.LoadAll()` |
| `Runtime/RuntimeCore.cs` | OnTick 中插入 `PluginLifecycle.Update()` |

### 7.3 删除

| 目标 | 说明 |
|------|------|
| `ACR/Shapes/` 整个目录 | 15 文件全部由 HiAuRo.FA 接管 |
| `HiAuRo.csproj` 无需加 ProjectReference 到 FA | 纯运行时 ALC 加载 |

---

## 8. Phase 1 验证标准

1. `dotnet build HiAuRo.FA/HiAuRo.FA.csproj -c Release` 通过
2. `dotnet build HiAuRo/HiAuRo.csproj -c Release` 通过（删除 Shapes 后无编译错误）
3. HiAuRo.FA.dll 输出到 HiAuRo 的 `Plugins/` 目录
4. HiAuRo 启动后日志显示加载了 HiAuRo.FA 插件
5. 时间轴脚本中可编译并运行 `using HiAuRo.FA.Shapes; new SafePointCalculator(new CircleField(...))` 
6. 确保 Resolving handler 正确共享宿主类型（无类型身份冲突）

---

## 9. 后续 Phase（不纳入本次）

- Phase 2: FA 危险区渲染（游戏内 AOE 范围绘制）
- Phase 3: FA 安全点标记（坐标箭头绘制）
- Phase N: 副本导航、怪物预警等 FA 功能

---

## 10. 风险与注意事项

- **ALC 类型身份**: Resolving handler 必须在 LoadFromStream 前注册。`HiAuRo.*` 引用必须重定向到宿主 ALC 中的 HiAuRo 程序集，否则 `obj is IPlugin` 会因类型身份不同而失败
- **MetadataReference 来源**: 子 ALC 加载的程序集可能在 `AppDomain.CurrentDomain.GetAssemblies()` 中不可见，需 `MetadataReference.CreateFromImage(byte[])` 从内存字节创建
- **脚本编译时机**: `PluginLoader.LoadAll()` 必须在 `ScriptCompiler` 首次编译前完成
- **构建产物部署**: FA.dll 的 `CopyToOutputDirectory` 需配置为 HiAuRo 项目 `bin/.../Plugins/` 目录
