# Runtime Pipeline Separation: Data / Decision / Execution

**日期**: 2026-05-31  
**状态**: pending-review

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
- **根治**：用架构强制约束 — 数据层、决策层不存在 `return`，执行层的 `return` 天然影响不到前面

---

## 解决方案：三阶段管道

将 `AIRunner.Update()` 拆为三个独立 Stage，在 `RuntimeCore.OnTick` 中按序调用：

```
RuntimeCore.OnTick:
  PushImGuiState()
  Coroutine.Update()
  CombatContext.Check()
  EventSystem.CheckTargetChanged()
  HotkeyPoller.Update()

  ACRLifecycle.Update():                    ← 状态分发 + 三阶段调度
    CheckPendingReload()
    CheckJobSwitch()
    CheckAutoSave()

    var state = CombatContext.CurrentState
    if (state == OutOfCombat && !_resetCalled): Runner.Reset()

    Runner.DataStage.Refresh()              ← 纯读取，所有状态都跑

    if (state == InCombat):
        decisions = Runner.DecisionStage.Decide()
    else:
        decisions = Runner.DecisionStage.PreCombatDecide()

    Runner.ExecutionStage.Execute(decisions, state)

  PluginLifecycle.Update()
```

**合约**：
- `DataStage.Refresh()` 完成后，`Data.*` 静态类已是本帧最新快照
- `DecisionStage.Decide()` 直接读 `Data.*`，产出 `DecisionOutput`
- `ExecutionStage.Execute()` 消费决策结果，门控只在此阶段生效

---

## Stage 1: DataStage（数据层）

**职责**：读取游戏状态，填充 `Data.*` 静态类。无副作用。无 return。

```csharp
void Refresh()
```

| 操作 | 说明 |
|---|---|
| 战斗状态切换检测 | `state != _prevState`，触发轴 Start/Stop |
| 切图检测 | `territoryId != _lastTerritoryId`，触发 `OnTerritoryChanged` |
| `Data.Objects.Refresh()` | O(N) 对象表扫描 → `Enemies/Allies/Party/...` |
| `Data.Party.Refresh()` | 队伍扫描 → `All/Alive/Tanks/Healers/...` |
| `_battleTimeMs += (int)(Data.Combat.DeltaTime * 1000)` | 帧不变量累积 |
| 目标选择器 `TryResolveTarget()` | 自动选目标 |
| 无目标通知 `OnNoTarget()` | 通知 ACR，不中断管道 |

`Data.Objects.Refresh()` 在最前面，确保后续所有代码（含 Idle/Zoning/OutOfCombat 分支）都能读到本帧对象数据。

---

## Stage 2: DecisionStage（决策层）

**职责**：基于 `Data.*` 快照做判断，产出 `DecisionOutput`。不发起技能。不 return。

两个变体：

### Decide() — InCombat

```csharp
DecisionOutput Decide()
```

| 操作 | 说明 |
|---|---|
| `OnBattleUpdate(battleTimeMs)` | ACR 回调：告知当前战斗时间 |
| `CanPauseACRCheck()` | ACR 暂停请求（>0 → CanPauseAck=true） |
| `ExecutionAxis.Instance.Update(battleTimeMs)` | 执行轴 WaitCond 轮询 → `ExecutionOutput?` |
| `AssistAxis.Instance.Update(battleTimeMs)` | 辅助轴轮询 → `ExecutionOutput?` |
| `UpdateFactAxis()` | 事实轴事件推进 + 决策引擎 + 智能层 + 移动执行 |
| `AiLoop.CheckAll()` | 遍历 `ISlotResolver.Check()`，存结果（不 Build） |

### PreCombatDecide() — Idle / Zoning / OutOfCombat

```csharp
DecisionOutput PreCombatDecide()
```

| 操作 | 说明 |
|---|---|
| `OnPreCombat()` | ACR 回调（仅 OutOfCombat 时调用） |
| `UpdateCountDown()` | 倒计时管理 + 倒计时结束→自动启动 Opener |
| `AiLoop.CheckAll()` | Check 总是执行（blockBuild 只影响 Build） |

非战斗状态不跑轴更新和事实轴——那些依赖 `_battleTimeMs` 的战斗内数据。

**输出结构**：

```csharp
struct DecisionOutput
{
    ExecutionOutput? ExecAxis;      // 执行轴产出 (PauseAcr / ForceTarget / ForceSpell)
    ExecutionOutput? AssistAxis;    // 辅助轴产出
    bool CanPauseAck;               // CanPauseACRCheck 结果
}
```

`AiLoop` 不产出 Slot 到 DecisionOutput — Build 由 ExecutionStage 调用 `AiLoop.Build(blockBuild)` 内部基于 CheckAll 结果组装。

### AiLoop 拆分

原 `GetNextSlot(bool blockBuild)` 拆为两个方法：

- **`CheckAll()`** — DecisionStage 调用  
  遍历所有 ISlotResolver，调用 `Check()`，结果存入已有 `_debugInfos[_].CheckResult` 数组（复用，零分配）

- **`Build(bool blockBuild)`** — ExecutionStage 调用  
  基于 CheckAll 结果决定是否调用 `Build(Slot)`，组装 Slot。blockBuild 为 true 时跳过 Build

---

## Stage 3: ExecutionStage（执行层）

**职责**：推进已有执行状态，将决策落地为技能。允许 return/gate。

```csharp
void Execute(in DecisionOutput decisions, CombatContext.State state)
```

`blockBuild`（= `!RuntimeCore.IsRunning || IsPaused`）在此阶段计算，仅影响构建部分。

### 预执行（立即生效，不 return）

| 操作 | 说明 |
|---|---|
| `ForceTarget → TargetManager.Target =` | 切换目标，当帧推进就能用新目标 |
| `OpenerMgr.Start()` 检查 | 仅一次：NotStarted → Running + 构建第一个 Slot |

`ForceTarget` 必须放在推进之前，确保 `SlotExecutor.ExecuteStep()` 使用新目标。

`OpenerMgr.Start()` 仅一次运行（`State == NotStarted`），`BuildCurrentSlot()` 组装技能后由同帧 `ExecuteOpenerIfRunning()` 启动执行，零空帧。

### 非战斗态门控（允许 return）

非 InCombat 状态不进入推进和构建阶段：

```csharp
if (state != InCombat) return;
```

非 InCombat 状态直接 return，跳过推进和构建。

### 推进（不 return）

| 操作 | 说明 |
|---|---|
| `SlotExecutor.ExecuteStep()` | 推进当前执行中 Slot |
| `ExecuteOpenerIfRunning()` | 推进起手（仅在 IsExecuting=false 时启动新 Slot + 立即推一步） |

两者不交叉：`ExecuteOpenerIfRunning` 内部首行 `if (IsExecuting) return` 避免重复推进同一 Slot。

### 门控（可以 return）

| 门控 | 说明 |
|---|---|
| `if (SlotExecutor.IsExecuting) return;` | 有技能在执行，不发起新技能 |
| `if (decisions.CanPauseAck) return;` | ACR 请求暂停 |

### 决策应用（轴输出落地）

| 操作 | 说明 |
|---|---|
| `ExecAxis.PauseAcr → return` | 执行轴要求暂停 |
| `ExecAxis.ForceSpell → StartSlot(slot), return` | 执行轴强制技能 |
| `AssistAxis.ForceSpell → StartSlot(slot), return` | 辅助轴强制技能 |

### 构建（新 Slot 发起）

| 操作 | 说明 |
|---|---|
| `ProcessSpellQueue(blockBuild) → StartSlot, return` | 处理 SpellQueue |
| `AiLoop.Build(blockBuild) → StartSlot` | 基于 CheckAll 结果构建并启动 Slot |

---

## 管道不变量保证

| 保证 | 实现方式 |
|---|---|
| DataStage 无 return | 接口约定 + Code Review |
| DecisionStage 无 return | 接口约定 + DecisionStage 内部不含 Slot 发起 |
| 门控只影响 ExecutionStage | 架构强制 — 前两个 Stage 根本没有门控 |
| 新操作不会破坏分离 | `IDataPipeline` / `IDecisionPipeline` 接口不含 `bool` 返回，编译器层面约束 |
| 零额外分配 | `Data.*` 复用静态列表，`DecisionOutput` 为 stack-allocated struct |

---

## 文件变更范围

| 文件 | 变更 |
|---|---|
| `Runtime/AIRunner.cs` | 拆为 `DataStage.cs` + `DecisionStage.cs` + `ExecutionStage.cs`（partial class 或独立 Stage 类） |
| `Runtime/AILoop_Normal.cs` | `GetNextSlot()` 拆为 `CheckAll()` + `Build(blockBuild)` |
| `Runtime/IAILoop.cs` | 接口新增 `CheckAll()` + `Build(bool)` |
| `Runtime/ACRLifecycle.cs` | 调用三个 Stage 替代单个 `Runner.Update()` |
| `Runtime/RuntimeCore.cs` | 不直接接触 AIRunner 内部，无需变更 |

**不涉及**：ACR 接口（ISlotResolver / IRotationEventHandler 不变）、轴引擎（ExecutionAxis / AssistAxis / FactTimeline 不变）、Data.*（已是独立模块，不变）

---

## 性能分析

| 维度 | 说明 |
|---|---|
| 调用深度 | 增加一次间接调用（Update → 3×Stage），CPU 分支预测下可忽略 |
| 内存分配 | `DecisionOutput` 是 struct（≤4个字段），栈分配零 GC |
| 对象扫描 | `Objects.Refresh()` + `Party.Refresh()` 本身是 O(N)+O(8)，不因拆分增加次数 |
| AiLoop.CheckAll | Check 结果复用 `_debugInfos` 数组，零分配 |
| 管道开销 | 三个 Stage 就是原来 Update() 的逻辑平摊到三个方法，无新增逻辑 |

结论：**性能无退步**。
