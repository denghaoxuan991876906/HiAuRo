# HiAuRo — 架构设计

## 分层架构

```
┌─────────────────────────────────────────────────────┐
│                     Dalamud Plugin Host              │
│                  (HiAuRo.csproj / Plugin.cs)         │
├─────────────────────────────────────────────────────┤
│  Authoring Layer   │  编辑器 / 调试 / 复盘 (CEF + Web)  │
├─────────────────────────────────────────────────────┤
│  Decision Layer    │  策略输出 / 减伤控制 / 移动协调      │
├─────────────────────────────────────────────────────┤
│  Fact Axis         │  Boss 时间线 JSON                  │
├─────────────────────────────────────────────────────┤
│  Assist Axis       │  安全坐标计算（始终运行）            │
├─────────────────────────────────────────────────────┤
│  Execution Axis    │  条件驱动执行控制                   │
├─────────────────────────────────────────────────────┤
│  ACR Abstraction   │  职业执行器接口                     │
├─────────────────────────────────────────────────────┤
│  Runtime Core      │  Tick / 状态 / 生命周期             │
├─────────────────────────────────────────────────────┤
│  Data Layer        │  HiAuRo.Data 统一入口              │
├─────────────────────────────────────────────────────┤
│  Infrastructure    │  配置 / 日志 / 调试                 │
├─────────────────────────────────────────────────────┤
│  Host Layer        │  插件生命周期                       │
└─────────────────────────────────────────────────────┘
```

## 双模式设计

### 模式一：执行轴模式（默认模式）

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│ Execution    │────→│     ACR      │────→│    Actions   │
│ Axis         │     │ (Job Logic)  │     │ (skill use)  │
│ (timeline    │     │              │     │              │
│  + triggers) │     │ SlotResolver │     │ GCD / oGCD   │
└──────────────┘     └──────────────┘     └──────────────┘
       │                      │
       │  控制信号             │  读取数据
       │  - 切换 A/B 策略      │
       │  - 指定技能           │  ┌──────────────┐
       │  - 暂停/恢复          │  │  Data Layer  │
       │                      │  │  (Self/Trgt  │
       │                      │  │   Party/Obj  │
       └──────────────────────┴──│   Combat)    │
                                 └──────────────┘
```

- 执行轴是 ACR 的上层指挥官
- 可根据时间/事件/条件切换 ACR 的 AOE/单体/停手等行为
- 可指定特定技能强制 ACR 使用
- 适合副本固定时间轴的场景

### 模式二：事实轴模式（高级模式）

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────┐
│ Fact Axis    │────→│  Decision    │────→│     ACR      │────→│ Actions  │
│ (Boss 时间线) │     │  Layer       │     │ (Job Logic)  │     │          │
│              │     │ (策略裁决)    │     │              │     │          │
└──────────────┘     └──────────────┘     └──────────────┘     └──────────┘
       │                     ↑
       │  "打到哪了"           │ 移动需求激活
       │  - 当前阶段           │
       │  - 下个技能    ┌──────┴──────────┐
       │  - 时间偏差    │   辅助轴 +       │
       │               │   决策层·移动协调  │
       └───────────────│                  │
                       └──────────────────┘
```
- 事实轴回答"战斗推进到哪了"（客观）
- 智能层回答"现在该做什么"（决策 + 移动协调）
- 辅助轴提供安全坐标计算，智能层释放并驱动移动
- ACR 执行具体技能选择（个人）
- 模式互斥，不同时运行

### 无轴模式（默认，向后兼容）

```
┌──────────────┐     ┌──────────────┐
│     ACR      │────→│   Actions    │
│ (Job Logic)  │     │              │
│              │     │ GCD + oGCD   │
│ SlotResolver │     │              │
└──────────────┘     └──────────────┘
       │
       │  读取数据
       ↓
┌──────────────┐
│  Data Layer  │
└──────────────┘
```

- ACR 独立运行，不做执行轴控制
- ACR 从数据层读取状态，自己做技能选择
- 通过 `ModeSwitch` 可切换到执行轴模式

---

## 模块设计

### Data Layer

```
HiAuRo.Data/
├── Data.cs                  # 公开根入口: public static partial class Data
├── Data.Self.cs             # 玩家: 转发 LocalPlayerState.*
├── Data.Target.cs           # 目标: 转发 TargetManager.*
├── Data.Party.cs            # 队伍: 扫描 PartyList，按角色/距离分类
├── Data.Objects.cs          # 对象: 扫描 ObjectTable，按敌/友/宠/环境分类
├── Data.Combat.cs           # 战斗: 转发 GameState.* + Condition.*
└── Jobs/
    └── BRDHelp.cs           # 诗人职业快捷入口（XXHelp.cs 首版）
```

**数据流原则**:
- Self / Target / Combat → 直接读 OmenTools 已有的静态属性，即时取值
- Party / Objects → 一次扫描，就地构建分类视图（方法写在 `Data.Party` / `Data.Objects` 里）
- 就绪判断 → 直接用 `GameState.IsLoggedIn`（OmenTools 已提供）
- 不存在长期缓存或快照树

### Runtime Core

```
HiAuRo.Runtime/
├── RuntimeCore.cs           # 主 Tick 循环入口（基于 FrameworkManager.Reg）
├── EventSystem.cs           # 底层事件监听分发（Hook 层）
├── Coroutine.cs             # 轻量协程调度器（不引入 Task/async）
├── CombatContext.cs         # 战斗上下文状态机（进战斗/脱战/切图）
├── ACRLifecycle.cs          # ACR 的 Init / Update / Dispose 管理
└── ModeSwitch.cs            # 模式切换
```

**主循环逻辑**（每帧）:
1. 检查客户端就绪（IsLoggedIn, IsLoaded）
2. 推进 Coroutine 协程（`Coroutine.Instance.Update()`）
3. 读取战斗上下文（进战斗/脱战/切图）
4. 更新战斗上下文状态机
5. 若在战斗中 → 驱动当前 ACR 的 Update
6. 若脱战/切图 → 暂停 ACR

### ACR Abstraction

```
HiAuRo.ACR/
├── IRotationEntry.cs            # 职业执行器统一接口
├── IRotationUI.cs            # ACR 作者 UI 定义（RegisterControls → IUiBuilder）
├── IUiBuilder.cs              # 描述性 UI 控件（Tab/Group/Checkbox/Slider/Dropdown/Hotkey/IntInput/Label/Separator/Tooltip）
├── Rotation.cs                  # Rotation 容器
├── SlotResolverData.cs          # 技能槽位（Checker + SlotMode）
├── SlotMode.cs                  # Gcd / OffGcd / Always
├── ISlotResolver.cs             # 槽位解析器接口
├── Spell.cs                     # 技能定义（ID/名称/类型/目标）
├── SlotAction.cs                # 单个技能执行行为
├── Slot.cs                      # 技能执行单元（一组 SlotAction）
├── SpellTargetType.cs           # 技能目标类型枚举（Target/Self/Party 等 11 种）
├── SpellCategory.cs             # 技能分类枚举（Default/LB/Potion/Sprint 等）
├── SpellType.cs                 # 技能类型枚举（RealGcd/GeneralGcd/Ability）
├── Jobs.cs                      # 战斗职业枚举（含基础职业 → 特职映射）
├── JobsCategory.cs              # 职业职能分类（Tank/Healer/Melee/Ranged/Caster）
├── IOpener.cs / OpenerMgr.cs    # 起手爆发序列
├── ISlotSequence.cs             # 技能序列
├── ITriggerAction.cs            # 触发动作接口
├── ITriggerCond.cs              # 触发条件接口
├── ITriggerBase.cs              # 触发器基础接口
├── ITriggerCondParams.cs        # 触发条件参数接口
├── IRotationEventHandler.cs     # 战斗事件回调（OnBattleUpdate）
├── ITargetResolver.cs           # 目标选择器接口（ResolveTarget）
├── IHotkeyEventHandler.cs       # ACR Rotation 热键事件处理器（Run(HotkeyConfig)）
├── HotkeyConfig.cs              # 热键配置数据结构
├── SpellsDefine.cs / AurasDefine.cs  # 技能/BUFF ID 常量（中文注释）
├── GCDHelper.cs                 # GCD 剩余时间 / 窗口判断
├── SpellHelper.cs               # 技能可用性/冷却/距离
├── TargetHelper.cs              # 目标选择/敌人数/身位
├── AuraHelper.cs                # Buff/DOT 检测
├── CooldownHelper.cs            # 充能技能冷却
└── HotkeyHelper.cs              # 热键管理（IHotkeyResolver + IHotkeyEventHandler）

HiAuRo.Jobs/BRD/
├── BRDRotationEntry.cs          # BRD 打样: 实现 IRotationEntry
├── BRDBattleData.cs             # BRD 战斗数据
├── BRD_GCD_强力射击.cs           # 1 个 GCD 技能示例
├── BRD_oGCD_失血箭.cs           # 1 个 oGCD 技能示例
└── BRDOpener.cs                 # 诗人起手示例

HiAuRo.Command/
└── CommandMgr.cs                # /hi 命令行系统

HiAuRo.Setting/
└── SettingMgr.cs                # 全局 + 职业独立设置管理

HiAuRo.UI/
├── MainWindow.cs                  # ImGui 主面板（状态/设置/Debug Tab）
├── UiBuilderImpl.cs              # IUiBuilder 实现（描述符 → JSON）
├── UiControlDef.cs               # UI 控件定义模型
├── WebUiServer.cs                 # HttpListener HTTP + WebSocket 服务器（非 Kestrel）
├── WebUiBridge.cs                 # C# ↔ JS 消息路由（JSON）
└── web/                           # HTML/CSS/JS 前端
    ├── main.html                  # 主控制栏
    ├── jobview.html              # 职业悬浮窗
    ├── hotkey.html               # 热键按钮面板
    ├── qt.html                   # Quick Toggle 面板
    ├── preview.html              # 预览页
    ├── app.js                    # WebSocket 客户端 + UI 逻辑
    └── style.css                 # 样式
```

HiAuRo.Runtime/
├── IAILoop.cs / AILoop_Normal.cs  # AI 循环接口与实现
├── SpellQueue.cs                  # Slot 调度队列
├── AIRunner.cs                    # AI 主引擎（加载/调度/执行/执行轴协调）
├── SlotExecutor.cs                # Slot/SlotAction 执行引擎
└── CountDownHandler.cs            # 倒计时管理器

**ACR 辅助类（实际已实现）**:
- GCDHelper / SpellHelper / TargetHelper / AuraHelper / CooldownHelper / ComboHelper
- MainControlHelper（暂停/保存）/ QTHelper / HotkeyHelper / HotkeyPoller / ItemHelper
- KeyBindingParser / MathHelper / SpellExtension / Spell_Computed / SpellHistoryHelper
- UiSettingsStore（UI 设置持久化）
- TargetResolvers: 按DataId / 最低HP敌人 / 最佳AOE位置 / 最近敌人 / 读条敌人
- HotkeyResolvers: 技能 / 吃药 / 极限技 / 疾跑 + NormalSpell / Potion / Sprint / LB
- Extension: GameObjectExtension / LocalPlayerExtension / Relationship

**ACR 作者开发流程**:
1. 创建 `XXRotationEntry : IRotationEntry`
2. 实现 `Build(settingFolder)` → 返回 `Rotation`
3. `Rotation` 包含 `List<SlotResolverData>`（技能槽位列表）
4. 每个 `SlotResolverData` = 一个 ISlotResolver + 一个 SlotMode
5. Resolver.Check() 返回 int：正数=可用，负数=禁止，0=不关心
6. 可添加 Opener（起手）、SlotSequence（序列）、TriggerAction（触发动作）、TriggerCondition（触发条件）
7. 可选实现 `IRotationUI`（自定义悬浮窗）、`OnDrawSetting`（职业设置页）

**ACR 接口定义**:
```csharp
public interface IRotationEntry
{
    string AuthorName { get; }
    bool UseCustomUi { get; }   // false=IUiBuilder / true=ACR 自带 HTML
    Rotation? Build(string settingFolder);
    IRotationUI? GetRotationUI();
    void OnDrawSetting();  // 可选
    void Dispose();
}

public interface IRotationUI
{
    void RegisterControls(IUiBuilder builder);  // 注册 UI 控件，HiAuRo 转为 Web 前端渲染
}

public class Rotation
{
    // === MVP 必需字段 ===
    public List<SlotResolverData> SlotResolvers;   // 技能槽位列表
    public List<ISlotSequence> SlotSequences;      // 技能序列
    public IOpener? Opener;                        // 起手爆发
    public IRotationEventHandler? EventHandler;     // 战斗事件回调
    public List<ITriggerAction> TriggerActions;     // 触发动作
    public List<ITriggerCond> TriggerConditions; // 触发条件

    public Jobs TargetJob;            // 适配职业（MVP 必需）
    public AcrType AcrType;           // ACR 类型：Both / PvE / PvP（MVP 必需）
    public int MinLevel;              // 最低等级（MVP 必需）
    public int MaxLevel;              // 最高等级（MVP 必需）
    public string Description;        // 描述文本（MVP 必需）

    public List<ITargetResolver> TargetResolvers;       // 目标选择器（MVP 必需）
    public List<IHotkeyEventHandler> HotkeyEventHandlers; // 热键事件处理（MVP 必需）

    // === 可选字段 ===
    public Func<int>? CanPauseACRCheck;  // 暂停 ACR 的条件检查

    // === 扩展字段 ===
    public Func<int>? CanUseHighPrioritySlotCheck; // 高优先级技能插入合法性检查

    // === 链式调用方法 ===
    public Rotation AddOpener(IOpener opener);
    public Rotation AddSlotSequences(params ISlotSequence[] seqs);
    public Rotation AddTriggerAction(ITriggerAction action);
    public Rotation AddTriggerCondition(ITriggerCond cond);
    public Rotation AddTargetResolver(ITargetResolver resolver);
    public Rotation AddHotkeyEventHandlers(params IHotkeyEventHandler[] handlers);
    public Rotation AddCanPauseACRCheck(Func<int> check);
}

public interface IRotationEventHandler
{
    void OnPreCombat();                       // 非战斗每帧
    void OnResetBattle();                     // 战斗重置
    void OnNoTarget();                        // 无目标时
    void OnSpellCastSuccess(Slot s, Spell sp); // 读条完成（可滑步）
    void BeforeSpell(Slot s, Spell sp);        // 技能使用前
    void AfterSpell(Slot s, Spell sp);         // 技能使用后
    void OnBattleUpdate(int timeMs);           // 战斗中每帧
    void OnEnterRotation();                   // 切入当前 ACR
    void OnExitRotation();                    // 切出当前 ACR
    void OnTerritoryChanged();                // 切图
}

public class SlotResolverData
{
    public ISlotResolver Resolver;  // Check() 返回 int
    public SlotMode Mode;           // Gcd / OffGcd / Always
}

public enum SlotMode { Gcd, OffGcd, Always }

public interface IOpener : ISlotSequence
{
    uint Level { get; }
    void InitCountDown(CountDownHandler handler);  // 倒计时阶段行为注册
}

public interface ISlotSequence
{
    /// HiAuRo 原生方式：委托列表构建序列
    List<Action<Slot>> Sequence { get; }
    int StartCheck();
    int StopCheck(int index);
}

public interface ITargetResolver
{
    bool ResolveTarget(out IBattleChara agent);  // 选择目标对象，返回 true 表示成功
}

public interface IHotkeyEventHandler
{
    /// 热键触发时处理，返回 true 表示已处理（阻止后续 handler）
    bool Run(HotkeyConfig config);
}

public class HotkeyConfig
{
    public string Id { get; init; }
    public string Label { get; init; }
    public string Key { get; set; }
    public bool Enabled { get; set; }
    // 扩展
    public uint SpellId { get; init; }
    public string Description { get; init; }
}
```

**BRD 打样范围**:
- 1 个 GCD: 自动使用 `强力射击`（Heavy Shot, ID 97）
- 1 个 oGCD: 在冷却好时使用 `失血箭`（Bloodletter, ID 110）
- 不做完整循环、不做 DOT 管理、不做歌曲

### Execution Axis

```
HiAuRo.Execution/
├── ExecutionAxis.cs              # 执行轴主逻辑（异步树 + WaitCond TCS + ExecutionOutput）
├── ExecutionNode.cs              # 异步 AST（Sequence/Parallel/Select/Loop/Delay/Cond/Action/Script/Print/ClearWait）
├── ExecutionJson.cs              # 时间轴 JSON ↔ TriggerNode 反序列化 + 类型注册
├── ScriptCompiler.cs             # Roslyn C# 动态编译（TreeScriptNode 驱动）
├── AssistAxis.cs                 # 辅助轴（独立于执行轴/事实轴，.txt 加载）
├── TriggerMetadata.cs            # 触发器元数据注册（类型/参数/文档）
└── Triggers/
    ├── Cond/                     # 触发条件（18 种）
    └── Action/                   # 触发动作（10 种）
```

**核心设计**：
- 树求值是 `async Task` 一次性调用
- `TreeCondNode` 等待模式：`WaitCond()` → TCS 挂起 → `CheckWaitingConds()` 每帧唤醒
- `TreeDelayNode`：`Coroutine.DelayAsync(seconds)` → TCS
- `TreeScriptNode`：Roslyn 动态编译 → `ITriggerScript.Check()`
- `ExecutionJsonLoader.RegisterFromRotation()`：ACR 作者注册自定义 Cond/Action
- `AssistAxis`：始终运行，独立于执行轴/事实轴，加载 `AssistTimelines/{ID}.txt`

**执行轴数据流**：
```
战斗开始 → Start() → async void RunTreeAsync()
  ├─ await Root.Execute(ctx)       ← 一次性 Task 调用
  │   ├─ Sequence: await child.Execute()
  │   ├─ Parallel: Task.WhenAll / Task.WhenAny
  │   ├─ Cond(等待): await WaitCond() → TCS → 每帧 CheckWaitingConds()
  │   ├─ Delay: await Coroutine.DelayAsync(s)
  │   ├─ Action: 同步 Handle() → SetForceSpell / SetPause
  │   └─ Script: Roslyn 编译 → Check()
  └─ Root Task 完成 → 树终结
战斗结束 → Stop() → CTS.Cancel()

每帧 Update():
  ├─ CheckWaitingConds() → 唤醒挂起条件
  └─ 检查 _forceSpell / _paused → ExecutionOutput
```

**TriggerNode AST 节点**：

| 节点 | 求值方式 |
|------|---------|
| TreeSequence | `await child.Execute()` 依次，IgnoreNodeResult 不短路 |
| TreeParallel | `Task.WhenAll` / `Task.WhenAny`（竞赛） |
| TreeSelect | 依次尝试，失败时仍返回 true |
| TreeLoop | 外层循环 Times 次 |
| TreeCondNode | CheckOnce 立即检查；等待模式 `await WaitCond()` |
| TreeActionNode | 同步执行所有 ITriggerAction |
| TreeDelayNode | `await Coroutine.DelayAsync(seconds)` |
| TreeScriptNode | Roslyn 动态编译 → ITriggerScript.Check() |
| TreePrintDebugInfoNode | 输出日志 |
| TreeClearWaitNode | 清理挂起条件 |

**WaitCond 机制**：
- `TreeCondNode` 等待模式时调用 `ExecutionAxis.WaitCond(node)` → 创建 `TaskCompletionSource<bool>` → 注册到 `_waitingConds`
- 每帧 `CheckWaitingConds()` 遍历注册表 → 调用 `node.EvaluateConds()` → 满足则 `TCS.SetResult(true)` → `await` 返回 → 树继续推进

---

## 辅助轴（AssistAxis）

辅助轴**始终运行**，独立于 ModeSwitch 模式切换。与执行轴/事实轴互斥不同，辅助轴不参与模式选择——无论用哪种模式，辅助轴都在后台并行。

核心职责：
- **安全坐标计算**：根据实时游戏状态（Boss 朝向、场地状态、队伍站位）计算角色应该去的安全位置
- **按副本加载**：每个 Boss 的安全区域算法不同，按副本编号加载对应的坐标计算脚本
- **生成移动需求**：计算结果写入移动需求缓冲区，由智能层的移动协调在合适时机释放

辅助轴与事实轴的关系："事实轴提供什么时候，辅助轴提供去哪里。"

```
事实轴：Boss 马上要放十字形 AOE（发出事件）
         ↓
辅助轴脚本节点：收到事件通知
  → 读取 Boss 当前朝向（实时）
  → 计算安全坐标
  → 生成移动需求，写入缓冲区
         ↓
智能层·移动协调：等事件释放 → 激活移动需求 → 驱动角色移动
```

---

## 事实轴 → 决策层 → ACR 数据流

```
FactTimeline (事件+需求) → DecisionEngine (分配减伤/治疗) → AIRunner (强制技能输出)
AssistAxis (安全坐标)    → DecisionEngine·移动协调 (释放+驱动) → 角色移动
```

**执行流程**：
1. **FactTimeline** 根据游戏事件（战斗开始、Boss读条、AOE预警）推进时间线
2. 当遇到需要团队资源（减伤、治疗）的事件时，向 **DecisionEngine** 提出需求
3. **DecisionEngine** 根据当前队伍状态（职业、CD、血量）进行贪心分配
4. 分配结果通过 **AIRunner** 强制对应职业的 ACR 使用指定技能
5. **辅助轴**同步计算安全坐标，写入移动需求缓冲区
6. **DecisionEngine·移动协调**在事件到达时释放移动需求，驱动角色移动

---

## 项目结构

```
HiAuRo/
├── HiAuRo.csproj
├── HiAuRo.json                      # Dalamud manifest
├── Plugin.cs                        # 插件生命周期入口
├── Infrastructure/
│   └── PluginConfig.cs              # 配置根对象
├── Data/
│   ├── Data.cs                      # HiAuRo.Data 根入口
│   ├── Data.Self.cs                 # 转发 LocalPlayerState.*
│   ├── Data.Target.cs               # 转发 TargetManager.*
│   ├── Data.Party.cs                # 队伍扫描 + 角色/距离分类
│   ├── Data.Objects.cs              # 对象扫描 + 语义分类
│   ├── Data.Combat.cs               # 转发 GameState.* + Condition.*
│   └── Jobs/
│       └── BRDHelp.cs               # 诗人职业快捷入口
├── Runtime/
│   ├── RuntimeCore.cs               # 主 Tick（基于 FrameworkManager）
│   ├── EventSystem.cs               # 战斗事件监听分发（Hook 层）
│   ├── Coroutine.cs                 # 轻量协程调度器
│   ├── CombatContext.cs             # 战斗上下文状态机
│   ├── ACRLifecycle.cs              # ACR 生命周期
│   ├── ACRLoader.cs                 # 外部 ACR 动态加载器
│   ├── ModeSwitch.cs                # 模式切换（None / ExecutionAxis / FactAxis）
│   ├── IAILoop.cs                   # AI 循环接口
│   ├── AILoop_Normal.cs             # 普通 PVE AI 循环
│   ├── SpellQueue.cs                # Slot 调度队列
│   ├── AIRunner.cs                  # AI 主引擎（加载/调度/执行/执行轴协调）
│   ├── SlotExecutor.cs              # Slot/SlotAction 执行引擎
│   ├── CountDownHandler.cs          # 倒计时管理器
│   └── HelperUpdater.cs             # HiAuRo.Helper.dll 自动更新 + ALC 隔离加载
├── ACR/
│   ├── IRotationEntry.cs            # 职业执行器接口
│   ├── Rotation.cs                  # Rotation 容器
│   ├── SlotResolverData.cs          # 技能槽位
│   ├── SlotMode.cs                  # Gcd / OffGcd / Always
│   ├── ISlotResolver.cs             # 槽位解析器接口
│   ├── Spell.cs                     # 技能定义
│   ├── SlotAction.cs                # 单个技能执行行为
│   ├── Slot.cs                      # 技能执行单元
│   ├── SpellTargetType.cs           # 技能目标类型枚举
│   ├── SpellCategory.cs             # 技能分类枚举
│   ├── SpellType.cs                 # 技能类型枚举（GCD/oGCD）
│   ├── IRotationUI.cs            # ACR 作者 UI 定义（RegisterControls → IUiBuilder）
│   ├── IUiBuilder.cs              # 描述性 UI 控件注册
│   ├── ITargetResolver.cs           # 目标选择器接口
│   ├── IHotkeyEventHandler.cs       # ACR 热键事件处理接口
│   ├── HotkeyConfig.cs              # 热键配置数据结构
│   ├── IOpener.cs / OpenerMgr.cs    # 起手爆发
│   ├── ISlotSequence.cs             # 技能序列
│   ├── ITriggerAction.cs            # 触发动作接口
│   ├── ITriggerCond.cs              # 触发条件接口
│   ├── ITriggerBase.cs              # 触发器基础接口
│   ├── ITriggerCondParams.cs        # 触发条件参数传递
│   ├── IRotationEventHandler.cs     # 战斗回调（OnBattleUpdate）
│   ├── SpellsDefine.cs / AurasDefine.cs  # 技能/BUFF 常量
│   ├── Jobs.cs                      # 战斗职业枚举
│   ├── JobsCategory.cs              # 职业职能分类
│   ├── GCDHelper.cs                 # GCD 工具
│   ├── SpellHelper.cs               # 技能辅助
│   ├── TargetHelper.cs              # 目标/敌人辅助
│   ├── AuraHelper.cs                # Buff/DOT 辅助
│   ├── CooldownHelper.cs            # 冷却辅助
│   ├── ComboHelper.cs               # 连击状态辅助
│   ├── MainControlHelper.cs         # 暂停/保存控制
│   ├── QTHelper.cs                  # Quick Toggle 管理
│   ├── ItemHelper.cs                # 道具使用辅助
│   ├── SpellHistoryHelper.cs        # 技能历史记录
│   ├── HotkeyHelper.cs              # 热键管理
│   ├── HotkeyPoller.cs              # 热键轮询器
│   ├── UiSettingsStore.cs           # UI 设置持久化
│   ├── TargetResolvers/             # 目标选择器实现
│   ├── HotkeyResolvers/             # 热键技能解析器
│   ├── Extension/                   # 游戏对象扩展方法
│   └── Data/                        # ACR 数据类型定义
├── Execution/                        # 执行轴
│   ├── ExecutionAxis.cs              # 执行轴主逻辑（TriggerLine 管理）
│   ├── ExecutionNode.cs              # AST 节点定义 + ExecutionEntry
│   ├── ExecutionJson.cs              # JSON 反序列化 + 类型注册
│   ├── ScriptCompiler.cs             # Roslyn C# 动态编译
│   ├── AssistAxis.cs                 # 辅助轴（.txt 加载）
│   ├── TriggerMetadata.cs            # 触发器元数据注册
│   └── Triggers/
│       ├── Cond/                     # 触发条件（18 种）
│       └── Action/                   # 触发动作（10 种）
├── FactAxis/                         # 事实轴
│   ├── FactNode.cs                   # 数据模型 (Phase → Event → PhaseSwitch → nested Branch)
│   ├── FactTimeline.cs               # 时间线运行时 (双时钟, Sync校准, 分支切换)
│   └── sample_timeline.json          # 示例时间线文件
├── Decision/                         # 决策层
│   ├── DecisionTypes.cs              # 技能数据类 + 注册表 + 输出模型
│   └── DecisionEngine.cs             # 贪心分配引擎 + 内置技能 (BRD/MNK/WHM)
├── Authoring/                        # 编辑器后端
│   └── AuthoringServer.cs            # WebSocket trigger catalog 注册（纯前端编辑器无需 CRUD 后端）
├── Command/
│   └── CommandMgr.cs                # /hi 命令系统
├── Setting/
│   └── SettingMgr.cs                # 全局 + 职业设置管理
└── UI/
    ├── MainWindow.cs                 # ImGui 主面板
    ├── UiBuilderImpl.cs              # IUiBuilder 实现
    ├── UiControlDef.cs               # UI 控件定义
    ├── WebUiServer.cs                # HttpListener HTTP + WebSocket
    ├── WebUiBridge.cs                # C# ↔ JS 消息路由
    └── web/                          # HTML/CSS/JS 前端
```

- CEF 渲染由 Browsingway 处理（游戏内悬浮窗），HiAuRo 仅提供 Web UI (`localhost:5678`)
- BRD 示例 ACR 位于 `example/BRD/`（独立 .csproj，引用 HiAuRo.dll）

---

## 依赖方向

```
Execution ──→ Runtime ──→ Data ──→ Infrastructure ──→ Plugin (Dalamud)
   │              │
   │              └──→ ACR（接口层，无运行时依赖）
   └──→ OmenTools (DService.*)
```

- Execution 层依赖 Runtime（通过 AIRunner 接收调度）+ Data（触发器读取游戏状态）
- 所有层通过 `HiAuRo.Data` 访问游戏数据
- 内部实现允许直接用 `DService.*`，但推荐的公开入口是 `HiAuRo.Data`

---

*Last updated: 2026-05-27*
