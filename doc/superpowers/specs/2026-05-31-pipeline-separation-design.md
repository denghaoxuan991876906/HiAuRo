# Runtime Pipeline Separation: Data / Decision / Execution

**日期**: 2026-05-31  
**状态**: pending-review  
**审阅**: GLM 5.1 + MIMO 2.5-pro 双模型审阅（已修复所有严重问题）

---

## 问题陈述

### 根因

`AIRunner.Update()` 将数据读取、决策判断、技能执行三类操作混合在一个方法中，**执行门控和数据累积未解耦**。当 `SlotExecutor.IsExecuting` 等条件触发 `return` 时，会连带跳过战斗计时器累积、轴状态机轮询等帧不变量操作，导致**帧不变量饥饿（Frame Invariant Starvation）**。

### 具体影响

| 被跳过的操作 | 后果 |
|---|---|
| `_battleTimeMs += DeltaTime` | 战斗计时器漂移 |
| `OnBattleUpdate(battleTimeMs)` | ACR 时钟错误（MCH 误判新开怪清内部状态，120s 锚点偏移） |
| `ExecutionAxis.Update()` | WaitCond 轮询断帧 |
| `AssistAxis.Update()` | 辅助轴断帧 |
| `FactTimeline.Update()` + 决策引擎 + 移动协调 | 事实轴事件滞后 |

### 临时修补 vs 根治

- **临时修补**（已完成）：移除 `return`，重排 Ordering — 解决了已知饥饿点，但依赖注释和自觉维护，无法防止未来再犯
- **根治**：用架构强制约束 — 数据层、决策层无早期 return，执行层的 return 天然影响不到前面

---

## 解决方案：三阶段管道

将 `AIRunner.Update()` 拆为三个 partial class 文件，在 `ACRLifecycle.Update()` 中按序调用：

```
ACRLifecycle.Update():
  CheckPendingReload()
  CheckJobSwitch()
  CheckAutoSave()

  if (!_loaded || AiLoop == null) return          ← 前置守卫

  var state = CombatContext.CurrentState
  if (state == OutOfCombat && !_resetCalled): Runner.Reset()

  Runner.DataStage.Refresh(state)                 ← 纯读取，所有状态都跑

  if (state == InCombat):
      decisions = Runner.DecisionStage.Decide()
  else:
      decisions = Runner.DecisionStage.PreCombatDecide(state)

  Runner.ExecutionStage.Execute(in decisions, state)

ACRLifecycle 每帧调用一次三个 Stage，传入本帧的 state 快照——确保同一帧内 state 不变。
```

**合约**：
- `DataStage.Refresh()` 完成后，`Data.*` 静态类已是本帧最新快照
- `DecisionStage.Decide()` 直接读 `Data.*`，产出 `PipelineDecision`
- `ExecutionStage.Execute()` 消费决策结果。门控和 return 只在此阶段生效
- **三个 Stage 为 AIRunner 的 partial class**，共享实例字段（`_battleTimeMs`、`_prevState` 等），无接口开销

---

## 接口定义

```csharp
// AIRunner 内部 partial class 方法，不是独立接口
partial class AIRunner
{
    // 数据层 — 读取游戏状态
    void Refresh(CombatContext.State state);

    // 决策层 — 基于本帧数据做判断
    PipelineDecision Decide();           // InCombat
    PipelineDecision PreCombatDecide(CombatContext.State state);  // 非战斗

    // 执行层 — 落地执行
    void Execute(in PipelineDecision decisions, CombatContext.State state);
}
```

采用 **partial class** 而非独立类/接口的原因：
- 零间接调用开销，状态共享无需 Context 传递
- 符合 AGENTS.md "简单优先、不过度抽象" 原则
- `void` 方法内部仍可写 `return;`——真正的保障是 Code Review，而非编译器约束

---

## Stage 1: DataStage（数据层）

**职责**：读取游戏状态，填充 `Data.*` 静态类。无副作用。内部可用 if-guard 根据 state 跳过无意义的操作，但不允许提前 return（不会遮盖本阶段其他操作）。

```csharp
void Refresh(CombatContext.State state)
```

| 操作 | 说明 | 条件 |
|---|---|---|
| 战斗状态切换检测 | `state != _prevState`，触发轴 Start/Stop | 总是 |
| 切图检测 | `territoryId != _lastTerritoryId`，触发 `OnTerritoryChanged` | 总是 |
| `Data.Objects.Refresh()` | O(N) 对象表扫描 → `Enemies/Allies/Party/...` | 总是 |
| `Data.Party.Refresh()` | 队伍扫描 → `All/Alive/Tanks/Healers/...` | InCombat 时 |
| `_battleTimeMs += (int)(Data.Combat.DeltaTime * 1000)` | 帧不变量累积 | InCombat 时 |
| `OnBattleUpdate(battleTimeMs)` | ACR 时钟回调（帧不变量，紧跟累积） | InCombat 时 |
| 目标选择器 `TryResolveTarget()` | 自动选目标 | 总是 |
| 无目标通知 `OnNoTarget()` | 通知 ACR，不中断管道 | InCombat + 无目标时 |

`_battleTimeMs` 是 AIRunner 实例字段（partial class 共享），仅 InCombat 累积 + 回调。此操作必须在 DataStage — 而非 DecisionStage — 因为它是帧不变量，不能被 DecisionStage 异常路径跳过。

`Data.Objects.Refresh()` 在最前面，确保后续所有代码（含 Idle/Zoning/OutOfCombat 分支）都能读到本帧对象数据。

---

## Stage 2: DecisionStage（决策层）

**职责**：基于 `Data.*` 快照做判断，产出 `PipelineDecision`。不发起技能。不 return。

两个变体：

### Decide() — InCombat

```csharp
PipelineDecision Decide()
```

| 操作 | 说明 |
|---|---|
| `CanPauseACRCheck()` | ACR 暂停请求（>0 → CanPauseAck=true） |
| `ExecutionAxis.Instance.Update(battleTimeMs)` | 执行轴 WaitCond 轮询 → `ExecutionOutput?` |
| `AssistAxis.Instance.Update(battleTimeMs)` | 辅助轴轮询 → `ExecutionOutput?` |
| `FactTimeline.Instance.Update(battleTimeMs)` | 事实轴事件推进（仅内部状态，不触发技能） |
| `DecisionEngine + IntelligenceEngine + MovementExecutor` | 智能判断 → 产出待执行技能列表（不调用 StartSlot） |
| `AiLoop.CheckAll()` | 遍历 `ISlotResolver.Check()`，存结果（不 Build） |

注意：当前 `UpdateFactAxis()` → `UpdateDecisions()` → `CheckPendingMitigations()` 中有直接调用 `SlotExecutor.StartSlot()` 的代码，**必须在实施时抽走**，改为产出待执行技能列表（见下方 `PipelineDecision` 的新字段）。

### PreCombatDecide() — Idle / Zoning / OutOfCombat

```csharp
PipelineDecision PreCombatDecide(CombatContext.State state)
```

| 操作 | 说明 |
|---|---|
| `OnPreCombat()` | ACR 回调（仅 OutOfCombat 时调用） |
| `UpdateCountDown()` | 倒计时管理 + 倒计时结束→自动启动 Opener |
| `AiLoop.CheckAll()` | Check 总是执行（blockBuild 只影响 Build） |

非战斗状态不跑轴更新和事实轴——那些依赖 `_battleTimeMs` 的战斗内数据。

### 输出结构

```csharp
struct PipelineDecision   // 注意：非 DecisionOutput，避免与 Decision.DecisionOutput 冲突
{
    ExecutionOutput? ExecAxis;          // 执行轴产出 (PauseAcr / ForceTarget / ForceSpell)
    ExecutionOutput? AssistAxis;        // 辅助轴产出
    bool CanPauseAck;                   // CanPauseACRCheck 结果
    List<Slot>? FactSlots;              // 事实轴待执行技能列表（从 DecisionEngine 产出，由 ExecutionStage 执行）
}
```

`AiLoop` 不产出 Slot 到 PipelineDecision — Build 由 ExecutionStage 调用 `AiLoop.Build(blockBuild)` 内部基于 CheckAll 结果组装。

### AiLoop 拆分

原 `GetNextSlot(bool blockBuild)` 拆为两个方法，**保留旧方法**标记 `[Obsolete]` 向后兼容：

- **`CheckAll()`** — DecisionStage 调用  
  遍历所有 ISlotResolver，调用 `Check()`，结果存入已有 `_debugInfos[_].CheckResult` 数组（复用，零分配）。**无目标时跳过所有 Check**（与当前 `GetNextSlot()` 行为一致，ACR Check 实现的隐式前提）。

- **`Build(bool blockBuild)`** — ExecutionStage 调用  
  基于 CheckAll 结果 + GCD 窗口判定，决定是否调用 `Build(Slot)` 组装 Slot。blockBuild 为 true 时跳过 Build。`Data.Combat.AbilityCountInGcd` 等副作用在此阶段处理。

### 事实轴拆分

原 `UpdateFactAxis()` 调用链中有两处直接调用 `SlotExecutor.StartSlot()`：

1. `执行分配技能()` — 治疗分配
2. `CheckPendingMitigations()` — 减伤窗口到期

**修复**：这两处改为产出待执行 Slot 列表，存入 `PipelineDecision.FactSlots` 字段。ExecutionStage 消费该列表调用 `StartSlot()`。

`UpdateDecisions()` 使用的去重集合（`_processedHealEventIds`、`_processedMitEventIds`）和减伤队列（`_pendingMits`）保留为 AIRunner 实例字段（partial class 共享）。

---

## Stage 3: ExecutionStage（执行层）

**职责**：推进已有执行状态，将决策落地为技能。允许 return/gate。

```csharp
void Execute(in PipelineDecision decisions, CombatContext.State state)
```

`blockBuild`（= `!RuntimeCore.IsRunning || IsPaused`）在进入 Execute 时直接从全局状态读取，不依赖 DecisionOutput。

### 预执行 + 推进（立即生效，无 return）

| 操作 | 说明 |
|---|---|
| `ForceTarget → TargetManager.Target =` | 切换目标，当帧推进就能用新目标（**行为变更说明**：提前到门控前，即使 Slot 在执行也会切目标） |
| `OpenerMgr.Start()` 检查 | 仅一次：NotStarted → Running + 构建第一个 Slot |
| `SlotExecutor.ExecuteStep()` | 推进当前执行中 Slot（所有状态下都运行，包括非战斗） |
| `ExecuteOpenerIfRunning()` | 推进起手（同上） |

`SlotExecutor.ExecuteStep()` 和 `ExecuteOpenerIfRunning()` 必须在非战斗态门控**之前**，因为：
- 倒计时结束→Opener 启动→Slot 推进行为需要非战斗态也运行
- 读条技能在渡秒结束时仍需推进，不能因为 state 变了就遗弃

两者不交叉：`ExecuteOpenerIfRunning` 内部首行 `if (OpenerMgr.State != Running) return; if (IsExecuting) return;` 避免重复推进。

### 非战斗态门控（可以 return）

```csharp
if (state != CombatContext.State.InCombat) return;
```

非 InCombat 状态直接 return，跳过以下所有构建操作。预执行段的 ForceTarget/Opener/ExecuteStep 已处理。

### Slot 构建门控（可以 return）

| 门控 | 说明 |
|---|---|
| `if (SlotExecutor.IsExecuting) return;` | 有技能在执行，不发起新技能 |
| `if (decisions.CanPauseAck) return;` | ACR 请求暂停 |

### 决策应用（轴输出 + 事实轴落地）

| 操作 | 说明 |
|---|---|
| `ExecAxis.PauseAcr → return` | 执行轴要求暂停 |
| `CanUseHighPrioritySlotCheck() < 0 → return` | ACR 高优先级检查（在 ForceSpell 前执行） |
| `ExecAxis.ForceSpell → StartSlot(slot), return` | 执行轴强制技能 |
| `AssistAxis.ForceSpell → StartSlot(slot), return` | 辅助轴强制技能 |
| `FactSlots 遍历 → StartSlot(slot), return` | 事实轴待执行技能 |

### 构建（新 Slot 发起）

| 操作 | 说明 |
|---|---|
| `ProcessSpellQueue(blockBuild) → StartSlot, return` | 处理 SpellQueue |
| `AiLoop.Build(blockBuild) → StartSlot` | 基于 CheckAll 结果构建并启动 Slot |

---

## 管道不变量保证

| 保证 | 实现方式 |
|---|---|
| DataStage 无 leave-early return | partial class 内部约定 + Code Review。可用 if-guard 跳过无效操作，但不允许跳到本阶段末尾之前 |
| DecisionStage 无 return | 同上。且不调用 StartSlot / UseAction 等副作用 API |
| 门控只影响 ExecutionStage | 架构强制 — 推进/门控/构建全部在 ExecutionStage 中 |
| 零额外分配 | `Data.*` 复用静态列表，`PipelineDecision` 为 stack-allocated struct。`FactSlots` 列表在事实轴决策阶段经由 `UpdateDecisions` 已分配，不新增 |

---

## 错误处理

每个 Stage 独立 try-catch，单阶段失败不阻塞整条管道：

| 阶段 | 异常处理 |
|---|---|
| `DataStage.Refresh()` | catch 中仍需累积 `_battleTimeMs`（最小不变量，避免计时器断帧），然后跳过本帧 Decision/Execution。下一帧重新读取。若 state == OutOfCombat 则在 catch 中执行 Reset 逻辑 |
| `DecisionStage.Decide()` | catch 后返回空 `PipelineDecision`（所有字段 default），Execution 只推进已有 Slot，不发起新技能 |
| `ExecutionStage.Execute()` | catch 后记录日志，下一帧从推进段重新开始 |

---

## 文件变更范围

| 文件 | 变更 |
|---|---|
| `Runtime/AIRunner.cs` | 拆为 `DataStage.cs` + `DecisionStage.cs` + `ExecutionStage.cs`（partial class，三个文件） |
| `Runtime/AILoop_Normal.cs` | `GetNextSlot()` 拆为 `CheckAll()` + `Build(blockBuild)`。原 `GetNextSlot` 保留，标记 `[Obsolete]`，内部委托到 `CheckAll + Build` |
| `Runtime/IAILoop.cs` | 接口新增 `CheckAll()` + `Build(bool)`，保留 `GetNextSlot`（向后兼容） |
| `Runtime/ACRLifecycle.cs` | 删除 `_resetCalled` 和 `Runner.Reset()` 调用（重置逻辑移至 DataStage 内部），调用三个 Stage 替代单个 `Runner.Update()` |
| `Runtime/CombatContext.cs` | 无需变更 |

**不涉及**：ACR 接口（ISlotResolver ↔ Check/Build 签名不变）、轴引擎（ExecutionAxis/AssistAxis 不变）、FactTimeline 接口不变（仅内部决策/执行拆分）

---

## 验证策略与迁移计划

### 验证方式

1. **帧级行为对比**：录制同一个短战斗（倒计时→开怪→10s→脱战）的 key frame 日志，对比拆分前后每帧的 Objects count、BattleTimeMs 值、Slot 执行序列
2. **MCH 回归**：验证 120s 锚点场景（起手 2 次 Reassemble → 2:00 钻头 → 人偶延迟是否消失）
3. **边缘场景**：战斗中 Boss 消失/转阶段（无目标）→ 战斗计时器是否继续走、轴事件是否继续推进

### 迁移步骤

1. 单独 PR，先实现 Stage 拆分 + AiLoop 分离（事实轴 `StartSlot()` 调用暂时保留在 DecisionStage 中，步骤 2 再移除）
2. 事实轴副作用分离（StartSlot → FactSlots 列表），`_resetCalled` 逻辑移入 DataStage

---

## 性能分析

| 维度 | 说明 |
|---|---|
| 调用深度 | 增加一次间接调用（Update → 3×partial 方法），CPU 分支预测下可忽略 |
| 内存分配 | `PipelineDecision` 是 struct（≤5 字段），栈分配零 GC。`FactSlots` 列表在 `UpdateDecisions` 中已有分配，不新增 |
| 对象扫描 | `Objects.Refresh()` 所有状态均执行（无变化）；`Party.Refresh()` 仅 InCombat 执行（无变化） |
| AiLoop.CheckAll | Check 结果复用 `_debugInfos` 数组，零分配 |
| 非战斗帧增量 | `Party.Refresh()` 不额外执行（InCombat 条件不变）。`TryResolveTarget()` 已有 doAutoTarget 短路，无实际增量 |

结论：**性能无退步**。
