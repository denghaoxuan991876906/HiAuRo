# HiAuRo — OmenTools 使用指南

## 概述

[OmenTools](https://github.com/AtmoOmen/OmenTools) 是项目核心依赖库，提供了统一的 Dalamud 服务入口、大量游戏事件包装、UI 组件和实用工具。本文档说明 HiAuRo 各层如何使用 OmenTools 提供的 API，以及哪些部分需要 HiAuRo 自行实现。

---

## 1. 初始化与生命周期

在 `Plugin.cs` 中直接接入：

```csharp
public Plugin(IDalamudPluginInterface pluginInterface)
{
    DService.Init(pluginInterface);
    // ...
}

public void Dispose()
{
    // ...
    DService.Uninit();
}
```

- `DService.Init()` 会注入 Dalamud 原生服务并通过 Dependency Injection 赋值所有 `[PluginService]` 属性
- `DService.Uninit()` 自动释放所有 OmenService、TaskHelper、MemoryPatch、Hook
- 无需额外包装层或 ServiceLocator

---

## 2. 核心 API 速查

### 2.1 DService — Dalamud 原生服务

OmenTools 把 Dalamud 原生服务全部挂在 `DService` 上，HiAuRo 可直接使用：

| DService 属性 | 类型 | HiAuRo 用途 |
|---------------|------|-------------|
| `ClientState` | `IClientState` | 登录状态、TerritoryType、LocalPlayer（已过时，用 ObjectTable） |
| `ObjectTable` | `IObjectTable`(扩展) | 全对象池、LocalPlayer、SearchById |
| `PlayerState` | `IPlayerState` | 角色名、职业、等级、ContentID |
| `Targets` | `ITargetManager` | 目标链（原生接口） |
| `PartyList` | `IPartyList` | 队伍列表 |
| `Condition` | `ICondition` | 条件标记（InCombat, Casting 等） |
| `JobGauges` | `IJobGauges` | 职业资源条 |
| `Framework` | `IFramework` | 帧更新 |
| `BuddyList` | `IBuddyList` | 宠物/陆行鸟 |
| `Fate` | `IFateTable` | 临危受命(FATE)数据 |
| `DutyState` | `IDutyState` | 副本状态 |
| `Chat` | `IChatGui` | 聊天输出 |
| `Toast` | `IToastGui` | Toast 提示 |
| `Command` | `ICommandManager` | 执行游戏命令 |
| `Log` | `IPluginLog` | 日志输出 |

> **注意**: OmenTools 通过 `[PluginService]` 自动注入，HiAuRo 自己不需要再注入这些服务。

### 2.2 OmenService — 扩展服务

通过 `DService.Instance().GetOmenService<T>()` 访问，或直接用静态成员（大部分 Manager 的公开 API 是静态的）：

| 服务 | 类型 | 核心能力 |
|------|------|----------|
| **GameState** | static | 游戏全局状态 |
| **LocalPlayerState** | static | 本地玩家状态 |
| **TargetManager** | manager | 目标控制 (含 Hook) |
| **UseActionManager** | manager | 技能使用 (含 Hook) |
| **FrameworkManager** | manager | 帧更新调度 (含节流) |
| **CommandManager** | manager | 自定义指令注册 |
| **ChatManager** | manager | 聊天消息处理 |
| **GamePacketManager** | manager | 网络包 Hook |
| **AchievementManager** | manager | 成就事件 |
| **CharacterStatusManager** | manager | 角色状态变更 |
| **GameTooltipManager** | manager | 悬浮提示数据 |
| **IPCManager** | manager | 插件间 IPC |
| **DataShareManager** | manager | 插件间数据共享 |

---

## 3. Phase 3 数据层：HiAuRo 如何使用 OmenTools

### 3.1 可直接用，无需 HiAuRo 封装的

以下数据 OmenTools 已经直接提供，HiAuRo.Data 直接转发即可：

#### 玩家数据（Self）

```csharp
// 直接用 OmenTools
LocalPlayerState.Object;          // IPlayerCharacter? — 当前玩家对象
LocalPlayerState.ClassJob;        // uint — 当前职业 ID
LocalPlayerState.ClassJobData;    // ClassJob — 职业表数据
LocalPlayerState.CurrentLevel;    // ushort — 当前等级
LocalPlayerState.Name;            // string — 角色名
LocalPlayerState.IsMoving;        // bool — 是否移动中
LocalPlayerState.IsInParty;       // bool — 是否在队伍中
LocalPlayerState.IsPartyLeader;   // bool — 是否是队长
LocalPlayerState.IsWalking;       // bool — 是否步行
LocalPlayerState.EntityID;        // uint — 实体 ID
LocalPlayerState.AccountID;       // ulong — 账户 ID
LocalPlayerState.ContentID;       // ulong — 内容 ID

// 距离计算
LocalPlayerState.DistanceToObject2D(target);    // float
LocalPlayerState.DistanceToObject3D(target);    // float
LocalPlayerState.DistanceToObject2DSquared(target);

// Buff/Status
LocalPlayerState.HasStatus(statusID, out index, sourceID);

// 事件
LocalPlayerState.PlayerMoveStateChanged;  // event Action<bool>
```

**HiAuRo.Data.Self** → 直接用 `LocalPlayerState.*`，不需要重复实现。

#### 目标数据（Target）

```csharp
// OmenTools TargetManager 提供了更强大的目标控制（含 Pre/Post Hook）：
TargetManager.Target;              // IGameObject? — 当前硬目标
TargetManager.FocusTarget;         // IGameObject? — 焦点目标
TargetManager.SoftTarget;          // IGameObject? — 软目标
TargetManager.MouseOverTarget;     // IGameObject? — 鼠标指向目标
TargetManager.PreviousTarget;      // IGameObject? — 上一个目标
TargetManager.GPoseTarget;         // IGameObject? — GPose 目标
TargetManager.MouseOverNameplateTarget; // IGameObject? — 鼠标名条目标

// 目标控制
TargetManager.Instance.SetHardTarget(target);
TargetManager.Instance.SetSoftTarget(target);
TargetManager.Instance.SetFocusTarget(gameObjectID);
TargetManager.Instance.InteractWithObject(target);
```

> 与 Dalamud 原生 `ITargetManager` 不同，OmenTools 的 `TargetManager` 的属性可读写(set/get 都支持)，且支持 Hook 订阅。

**HiAuRo.Data.Target** → 直接用 `TargetManager.*`，不需要重复实现。

#### 战斗状态（Combat）

```csharp
// 全局状态
GameState.IsLoggedIn;              // bool — 安全登录且可操作
GameState.IsInInstanceArea;        // bool — 在副本区域
GameState.IsInPVPArea;            // bool — 在 PVP 区域
GameState.IsInPVPInstance;         // bool — 在 PVP 副本
GameState.IsInIdleCam;             // bool — 观景视角
GameState.IsTerritoryLoaded;       // bool — 地图加载完毕
GameState.IsForeground;            // bool — 窗口前台
GameState.TerritoryType;           // uint — 当前 TerritoryType ID
GameState.TerritoryTypeData;       // TerritoryType — 地图表数据
GameState.Map;                     // uint — 当前 Map ID
GameState.MapData;                 // Map — 地图表数据
GameState.ContentFinderCondition;  // uint — 当前副本内容 ID
GameState.ContentFinderConditionData; // ContentFinderCondition
GameState.ServerTime;              // DateTime — 服务器时间
GameState.ServerTimeUnix;          // long — 服务器 Unix 时间戳
GameState.DeltaTime;               // float — 帧 Delta Time
GameState.FrameRate;               // float — 帧率
GameState.IsCN / IsGL / IsKR / IsTC; // 客户端区域

// 事件
GameState.Instance.Login;          // event Action — 登录且可用
GameState.Instance.Logout;         // event Action — 登出
GameState.Instance.EnterFate;      // event Action<uint> — 进入 FATE

// 条件标记（通过 ICondition 扩展方法）
DService.Condition.IsCasting();    // bool — 是否在读条
DService.Condition.IsBoundByDuty(); // bool — 是否在副本中
DService.Condition.IsOnMount();    // bool — 是否在坐骑上
DService.Condition.IsOccupiedInEvent(); // bool — 是否在事件中
DService.Condition.IsBetweenAreas();   // bool — 是否在切图中
DService.Condition.IsWatchingCutscene(); // bool — 是否在看动画
```

**HiAuRo.Data.Combat** → 直接用 `GameState.*` + `DService.Condition.*`，不需要重复实现。

#### 对象表（Objects）

```csharp
// OmenTools 扩展的 IObjectTable，提供更多对象类型：
DService.ObjectTable.LocalPlayer;   // IPlayerCharacter? — 返回扩展接口类型
DService.ObjectTable[0];            // IGameObject? — 按索引访问 (OmenTools 返回值变为扩展接口)
DService.ObjectTable.SearchById(id); // IGameObject?

// OmenTools 扩展的对象接口比 Dalamud 原生更丰富：
// IPlayerCharacter, IBattleChara, IBattleNPC, ICharacter, INPC, IEventObj
```

> 注意：OmenTools 的 GlobalUsing 重定义了 `IPlayerCharacter`、`IBattleChara`、`IGameObject` 等接口类型（`OmenTools.Dalamud.Services.ObjectTable.Abstractions.*`），不是 Dalamud 原生类型。建议 HiAuRo 统一使用 OmenTools 版本。

#### 技能使用（Action）

```csharp
// OmenTools UseActionManager
UseActionManager.Instance.UseAction(ActionType.Spell, actionID, targetID, extraParam, queueState, comboRouteID);
UseActionManager.Instance.UseActionLocation(ActionType.Spell, actionID, targetID, location, extraParam);
UseActionManager.Instance.IsActionOffCooldown(ActionType.Spell, actionID);

// Hook 事件
UseActionManager.Instance.RegPreUseAction(delegate);     // 技能使用前
UseActionManager.Instance.RegPostUseAction(delegate);    // 技能使用后
UseActionManager.Instance.RegPreCharacterStartCast(delegate);  // 开始读条前
UseActionManager.Instance.RegPostCharacterCompleteCast(delegate); // 读条完成后
```

**HiAuRo ACR 层** → 直接使用 `UseActionManager.UseAction()` 执行技能，不需要自己封装。

#### 帧更新调度

```csharp
// OmenTools FrameworkManager — 带节流控制的帧更新
FrameworkManager.Instance.Reg(method, throttleMS);     // 注册帧更新
FrameworkManager.Instance.Unreg(method);               // 取消注册
```

**HiAuRo Runtime** → 直接用 `FrameworkManager.Reg()` 做 Tick 循环，支持节流。

### 3.2 HiAuRo 仍需自己实现的

#### Party 队伍数据整理

OmenTools 提供了 `DService.PartyList`（原生队伍列表），但不提供按角色/距离分类的视图。HiAuRo 需要在 `Data.Party.cs` 中实现一次扫描、多视图复用：

```csharp
// HiAuRo Data.Party 中实现:
// 输入: DService.PartyList
// 输出: 存活 / 死亡 / T / 治疗 / DPS / 5y内 / 10y内 / 15y内
```

#### Objects 对象分类

OmenTools 提供全对象表但不做语义分类。`Data.Objects.cs` 中实现：

```csharp
// HiAuRo Data.Objects 中实现:
// 输入: DService.ObjectTable 一趟扫描
// 输出: 敌人 / 友方 / 小队 / 宠物 / 环境对象
// 分类联合 ObjectKind + BattleNpcSubKind + OwnerId + BuddyList
```

#### ACR 抽象层

OmenTools 不提供 ACR 框架。HiAuRo 自建：
- `IRotationEntry`、`Rotation`、`SlotResolverData`、`SlotMode`
- Slot Resolver 调度逻辑

#### HiAuRo.Helper 辅助库

HiAuRo.Helper 是独立仓库（21职业 Helper），同样遵循 OmenTools 直取模式，不做额外包装。HelperUpdater 从 GitHub Release 自动更新加载，编译时不依赖运行时。

#### 执行轴 / 事实轴 / 智能层

完全由 HiAuRo 实现，OmenTools 只提供底层数据和服务。

---

## 4. ImGuiOm UI 组件

OmenTools 提供了一套 ImGui 封装组件，HiAuRo 的悬浮控制面板可以复用：

| 文件 | 组件 |
|------|------|
| `ImGuiOm/Button.cs` | 自定义按钮 |
| `ImGuiOm/Checkbox.cs` | 自定义复选框 |
| `ImGuiOm/Text.cs` | 格式化文本 |
| `ImGuiOm/Selectable.cs` | 可选列表项 |
| `ImGuiOm/Tooltip.cs` | 悬浮提示 |
| `ImGuiOm/TreeNode.cs` | 树形节点 |
| `ImGuiOm/Widgets/Combos/` | 下拉选择组件（职业/技能/地图等） |

GlobalUsing 已自动 `global using OmenTools.ImGuiOm`，可直接使用。

### HiAuRo 该直接用 vs 该自己写

| 需求 | 用 OmenTools | HiAuRo 自建 |
|------|-------------|------------|
| 登录/地图/副本状态 | `GameState.*` | — |
| 玩家对象/职业/等级 | `LocalPlayerState.*` | — |
| 目标链 | `TargetManager.Target` 等 | — |
| 条件标记 | `DService.Condition.*` + 扩展 | — |
| 对象表 | `DService.ObjectTable` | 对象分类（Data.Objects） |
| 队伍列表 | `DService.PartyList` | 队伍分类（Data.Party） |
| 技能执行 | `UseActionManager` | SlotResolver 调度逻辑 |
| 帧更新 | `FrameworkManager.Reg()` | RuntimeCore 主循环 |
| 距离计算 | `LocalPlayerState.DistanceTo*` | — |
| Buff/DOT 检测 | `LocalPlayerState.HasStatus()` | — |
| 职业资源 | `DService.JobGauges` | — |
| ACR 接口 | — | `IRotationEntry/Rotation` |
| 执行轴 | — | `ExecutionAxis` |
| 事实轴 | — | `FactAxis` |
| 智能层 | — | `DecisionLayer` |
| UI 面板 | `ImGuiOm` 组件 | 面板布局和逻辑 |

---

## 5. 与旧规划（ECommons 方案）的对比

| 方面 | ECommons 方案（旧） | OmenTools 方案（新） |
|------|-------------------|---------------------|
| 接入方式 | `ECommonsMain.Init/Dispose` | `DService.Init/Uninit` |
| 服务入口 | `Svc.Targets` / `Svc.Objects` | `DService.Targets`(原生) / `TargetManager.*`(扩展) |
| 目标控制 | 只读 | 可读写 + Hook 订阅 |
| 登录状态 | 自己写就绪检查 | `GameState.IsLoggedIn` 直接用 |
| 战斗状态 | 自己组合 `ICondition` | `GameState.*` + `ICondition` 扩展 |
| 技能使用 | 需要自己封装或引入额外库 | `UseActionManager.UseAction()` 直接用 |
| 帧调度 | 自己写或引入额外库 | `FrameworkManager.Reg()` 带节流 |
| UI 组件 | 无 | `ImGuiOm` 全套 |
| 扩展方法 | 较少 | `ICondition`/`GameObject`/`ActionManager` 全扩展 |
| 包管理 | NuGet 包 | Git 子模块或 NuGet |
| csproj 依赖 | `ECommons` NuGet 包 | `OmenTools` 项目引用 |

### 简化效果

使用 OmenTools 后，HiAuRo 的 Phase 3 数据层可以显著减薄：

```diff
- 就绪检查（自己写）       → 删，用 GameState.IsLoggedIn
- Data.Self.cs (大部分)     → 删，用 LocalPlayerState.*
- Data.Target.cs (全部)     → 删，用 TargetManager.*
- Data.Combat.cs (大部分)   → 删，用 GameState.* + Condition.*
+ Data.Party.cs 队伍分类     → 保留，OmenTools 不提供
+ Data.Objects.cs 对象分类   → 保留，OmenTools 不提供
```

---

## 6. 使用 GlobalUsing

建议将 OmenTools 的 `GlobalUsing.OmenTools.cs` 文件内容合并到 HiAuRo 的 GlobalUsings 中（或直接用 `<ImplicitUsings>` 方式），这样可以：

- 直接使用 `GameState.IsLoggedIn` 而不需要 `using OmenTools.OmenService`
- 直接使用 `DService.Instance()` 而不需要 `using OmenTools`
- 自动获得所有扩展方法（`IsCasting()`, `IsBoundByDuty()`, `FindNearest()` 等）

---

## 7. 注意事项

1. **对象类型优先用 OmenTools 版本**: `IPlayerCharacter`, `IBattleChara`, `IGameObject` 等 OmenTools 扩展接口比 Dalamud 原生更丰富
2. **不要混用原生 `ITargetManager`**: 用 OmenTools 的 `TargetManager`（静态属性更强大）
3. **`DService.Targets` vs `TargetManager`**: `DService.Targets` 是 Dalamud 原生接口（只读），`TargetManager` 是 OmenTools 扩展（可读写 + Hook）。HiAuRo 应优先用后者
4. **`DService.ObjectTable` vs `DService.ObjectTable` 原生**: OmenTools 把它扩展了，返回类型是扩展接口
5. **GameState.IsLoggedIn** 检查更严格，需要 `LobbyUpdateStage == 1` 和 `LobbyUIStage == 1`，比 `ClientState.IsLoggedIn` 更可靠

---

## 8. 参考路径

```
../资料/           — 参考资料目录 (外部，不在仓库内)
../OmenTools/      — OmenTools 本地克隆
```

---

*Last updated: 2026-05-08*
