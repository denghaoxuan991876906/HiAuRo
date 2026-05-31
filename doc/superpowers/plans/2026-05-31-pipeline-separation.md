# Runtime Pipeline Separation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split `AIRunner.Update()` into three partial-class stages (DataStage → DecisionStage → ExecutionStage) to enforce data-reading/before-decision/before-execution ordering and eliminate frame invariant starvation.

**Architecture:** AIRunner becomes `partial class` across 4 files. ACRLifecycle dispatches by combat state and calls the 3 stages in order. `PipelineDecision` is a stack-allocated struct carrying axis outputs. `AILoop_Normal` splits `GetNextSlot()` into `CheckAll()` + `Build()`.

**Tech Stack:** C# (.NET 10), Dalamud.NET.Sdk 15.0.0, OmenTools, partial class pattern

---

### Task 1: Define PipelineDecision struct + IAILoop interface update

**Files:**
- Create: `HiAuRo/Runtime/PipelineDecision.cs`
- Modify: `HiAuRo/Runtime/IAILoop.cs`

- [ ] **Step 1: Create PipelineDecision struct**

```csharp
using HiAuRo.ACR;
using HiAuRo.Execution;

namespace HiAuRo.Runtime;

/// <summary>
/// DecisionStage 产出的决策结果。栈分配 struct，零 GC。
/// </summary>
internal struct PipelineDecision
{
    public ExecutionOutput? ExecAxis;       // 执行轴产出
    public ExecutionOutput? AssistAxis;     // 辅助轴产出
    public bool CanPauseAck;                // ACR 暂停请求
    public List<Slot>? FactSlots;           // 事实轴待执行技能列表（复用已有 List 实例，不新增分配）
}
```

- [ ] **Step 2: Update IAILoop interface — add CheckAll() + Build()**

Read current `HiAuRo/Runtime/IAILoop.cs`:

```csharp
using HiAuRo.ACR;

namespace HiAuRo.Runtime;

public interface IAILoop
{
    Slot? GetNextSlot(bool blockBuild = false);
}
```

Replace with:

```csharp
using HiAuRo.ACR;

namespace HiAuRo.Runtime;

public interface IAILoop
{
    /// <summary>遍历所有 ISlotResolver.Execute Check()，结果存内部</summary>
    void CheckAll();

    /// <summary>基于 CheckAll 结果构建 Slot。blockBuild=true 时跳过 Build</summary>
    Slot? Build(bool blockBuild);

    /// <summary>[Obsolete] 向后兼容，内部委托到 CheckAll + Build</summary>
    [Obsolete("Use CheckAll() + Build() instead")]
    Slot? GetNextSlot(bool blockBuild = false);
}
```

- [ ] **Step 3: Verify build**

Run: `./build.sh`
Expected: PASS (no callers of new methods yet, just declarations)

- [ ] **Step 4: Commit**

```bash
git add HiAuRo/Runtime/PipelineDecision.cs HiAuRo/Runtime/IAILoop.cs
git commit -m "feat: add PipelineDecision struct + IAILoop.CheckAll/Build interface"
```

---

### Task 2: Split AILoop_Normal.GetNextSlot() into CheckAll() + Build()

**Files:**
- Modify: `HiAuRo/Runtime/AILoop_Normal.cs`

- [ ] **Step 1: Add CheckAll() method — extract the Check loop from GetNextSlot**

Add after the constructor body (before existing `GetNextSlot`):

```csharp
/// <summary>遍历所有 ISlotResolver，执行 Check()，结果存入 _debugInfos。无目标时跳过。</summary>
public void CheckAll()
{
    // 每帧重置 debug 数据
    foreach (var info in _debugInfos)
    {
        info.CheckResult = -99;
        info.CheckThrew = false;
        info.CheckError = "";
        info.PassedWindow = false;
        info.BuiltSlot = false;
        info.BuiltSkills = "";
    }

    if (_resolvers.Count == 0)
    {
        DService.Instance().Log.Error("[AILoop] 没有已注册的 SlotResolver");
        return;
    }

    // 无目标时不调 Check（保持当前行为）
    if (Data.Target.Current == null)
        return;

    // 扩容结果数组
    if (_checkResults.Length != _resolvers.Count)
        _checkResults = new int[_resolvers.Count];

    for (int i = 0; i < _resolvers.Count; i++)
    {
        var data = _resolvers[i];
        var info = _debugInfos[i];

        int checkResult;
        try
        {
            checkResult = data.Resolver.Check();
            info.CheckResult = checkResult;
        }
        catch (Exception ex)
        {
            checkResult = -99;
            info.CheckResult = -99;
            info.CheckThrew = true;
            info.CheckError = ex.Message;
            DService.Instance().Log.Error($"[AILoop] Check#{data.Resolver.GetType().Name} 异常: {ex}");
        }
        _checkResults[i] = checkResult;
    }

    // 暂停/停止时只阻断 Build，Check 已完成（标记由 Build 处理）
}
```

- [ ] **Step 2: Add Build() method — match current GetNextSlot Build section exactly**

```csharp
/// <summary>基于 CheckAll 结果 + GCD 窗口判定，构建 Slot。blockBuild=true 时跳过。</summary>
public Slot? Build(bool blockBuild)
{
    // 暂停/停止时只阻断 Build
    if (blockBuild) return null;

    bool isGcdReady = GCDHelper.CanUseGCD();
    bool isOffGcdWindow = GCDHelper.CanUseOffGcd();
    bool is2ndAbWindow = GCDHelper.Is2ndAbilityTime();
    float gcdRemain = GCDHelper.GetGCDCooldown();

    // ── 第二遍：按顺序找到第一个 Check >= 0 且窗口匹配的 Resolver，调 Build ──
    for (int i = 0; i < _resolvers.Count; i++)
    {
        if (_checkResults[i] < 0) continue;

        var data = _resolvers[i];
        var info = _debugInfos[i];

        // 窗口判定：Mode 控制 Build/执行时机
        bool canExecute = data.Mode switch
        {
            SlotMode.Gcd    => isGcdReady,
            SlotMode.OffGcd => isOffGcdWindow
                && Data.Combat.AbilityIntervalElapsed
                && Data.Combat.AbilityCountInGcd < Data.Combat.MaxAbilityTimesInGcd,
            SlotMode.Always => true,
            _              => false
        };

        if (!canExecute)
        {
            info.PassedWindow = false;
            continue;
        }

        info.PassedWindow = true;

        // Build + 执行
        try
        {
            var slot = new Slot();
            data.Resolver.Build(slot);
            var resolverName = data.Resolver.GetType().Name;

            info.BuiltSlot = true;
            info.BuiltSkills = string.Join(",", slot.Actions.Select(a => a.Spell.Name));

            DService.Instance().Log.Debug($"[AILoop] Build: {resolverName} → {slot.Actions.Count}个技能 = [{string.Join(", ", slot.Actions.Select(a => $"{a.Spell.Name}({a.Spell.Id})"))}] (GCD={isGcdReady} oGCD={isOffGcdWindow} GCDr={gcdRemain:F0}ms 2ndAb={is2ndAbWindow} ab={Data.Combat.AbilityCountInGcd}/{Data.Combat.MaxAbilityTimesInGcd})");

            if (data.Mode == SlotMode.Gcd)
                Data.Combat.AbilityCountInGcd = 0;
            else if (slot.Actions.Any(a => a.Spell.IsAbility()))
                Data.Combat.AbilityCountInGcd++;

            return slot;
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[AILoop] Build error: {ex.Message}\n{ex.StackTrace}");
        }

        // Build 成功会 return，到这里的只有 Build 抛异常，继续找下一个
    }

    return null;
}
```

- [ ] **Step 3: Replace GetNextSlot() body with delegation to new methods**

Replace the existing `GetNextSlot(bool blockBuild = false)` method body with:

```csharp
[Obsolete("Use CheckAll() + Build() instead")]
public Slot? GetNextSlot(bool blockBuild = false)
{
    CheckAll();
    return Build(blockBuild);
}
```

Remove the old GetNextSlot implementation body. Keep the `[Obsolete]` attribute.

- [ ] **Step 4: Verify build**

Run: `./build.sh`
Expected: PASS (existing callers use GetNextSlot which delegates to new methods)

- [ ] **Step 5: Commit**

```bash
git add HiAuRo/Runtime/AILoop_Normal.cs
git commit -m "refactor: split GetNextSlot into CheckAll + Build, keep old method as delegate"
```

---

### Task 3: Extract DataStage.Refresh() from AIRunner.Update()

**Files:**
- Create: `HiAuRo/Runtime/AIRunner.DataStage.cs`
- Modify: `HiAuRo/Runtime/AIRunner.cs` (add `partial` keyword, extract fields)

- [ ] **Step 1: Change AIRunner to partial class**

In `HiAuRo/Runtime/AIRunner.cs`, change:

```csharp
public sealed class AIRunner
```
to:
```csharp
public sealed partial class AIRunner
```

- [ ] **Step 2: Create AIRunner.DataStage.cs with Refresh() method**

```csharp
using static HiAuRo.Data;
using HiAuRo.ACR;
using HiAuRo.Execution;
using HiAuRo.FactAxis;
using HiAuRo.Infrastructure;

namespace HiAuRo.Runtime;

public sealed partial class AIRunner
{
    /// <summary>数据层：读取游戏状态，填充 Data.*。无副作用。内部可用 if-guard 但不允许提前 return。</summary>
    internal void Refresh(CombatContext.State state)
    {
        // 战斗状态切换检测 + 轴 Start/Stop
        if (state != _prevState)
        {
            DService.Instance().Log.Debug($"[AIRunner] 战斗状态切换: {_prevState} → {state}");
            if (state == CombatContext.State.InCombat)
            {
                DService.Instance().Log.Debug("[AIRunner] 进入战斗, 清空旧 SpellQueue");
                SpellQueue.Clear();
                if (ModeSwitch.CurrentMode == ModeSwitch.Mode.ExecutionAxis)
                    ExecutionAxis.Instance.Start();
                AssistAxis.Instance.Start();
                FactTimeline.Instance.Start();
            }
            else if (state == CombatContext.State.OutOfCombat || state == CombatContext.State.Idle)
            {
                if (ModeSwitch.CurrentMode == ModeSwitch.Mode.ExecutionAxis)
                    ExecutionAxis.Instance.Stop();
                AssistAxis.Instance.Stop();
                FactTimeline.Instance.Stop();
            }

            // 首次进入 OutOfCombat 时重置战斗状态（仅一次）
            if (state == CombatContext.State.OutOfCombat)
                ResetState();
        }
        _prevState = state;

        // 切图检测
        var territoryId = Data.Combat.TerritoryType;
        if (territoryId != _lastTerritoryId)
        {
            if (_lastTerritoryId != 0)
            {
                DService.Instance().Log.Information($"[AIRunner] 切图: {_lastTerritoryId} → {territoryId}");
                Data.Combat.AbilityCountInGcd = 0;
                Data.Combat.LastAbilityUseTime = 0;
                Data.Combat.MaxAbilityTimesInGcd = PluginConfig.Instance.MaxAbilityTimesInGcd;
                CurrentRotation?.EventHandler?.OnTerritoryChanged();
            }
            _lastTerritoryId = territoryId;
        }

        // 对象扫描（各分支都依赖）
        Data.Objects.Refresh();

        // 目标选择器
        var doAutoTarget = PluginConfig.Instance.TargetSelectorEnabled
            || (PluginConfig.Instance.TargetAutoSelectOnCountdown
                && Data.Combat.IsInInstanceArea
                && ReadCountdown() > 0
                && Data.Target.Current == null);

        if (doAutoTarget)
        {
            if (PluginConfig.Instance.TargetKeepCurrent)
            {
                if (Data.Target.Current == null)
                    TryResolveTarget();
            }
            else
            {
                TryResolveTarget();
            }
        }

        // InCombat 专属操作
        if (state == CombatContext.State.InCombat)
        {
            Data.Party.Refresh();

            if (Data.Target.Current == null)
            {
                DService.Instance().Log.Debug("[AIRunner] 无目标, 调用 OnNoTarget");
                CurrentRotation?.EventHandler?.OnNoTarget();
            }

            // 战斗计时器（帧不变量）
            _battleTimeMs += (int)(Data.Combat.DeltaTime * 1000);
            CurrentRotation?.EventHandler?.OnBattleUpdate(_battleTimeMs);
        }
    }
}
```

- [ ] **Step 3: Add ResetState() private helper to AIRunner.DataStage.cs**

```csharp
    private void ResetState()
    {
        SpellQueue.Clear();
        OpenerMgr.Reset();
        CountDownHandler.Reset();
        Coroutine.Instance.Clear();
        _battleTimeMs = 0;
        Data.Combat.AbilityCountInGcd = 0;
        Data.Combat.LastAbilityUseTime = 0;
        Data.Combat.MaxAbilityTimesInGcd = PluginConfig.Instance.MaxAbilityTimesInGcd;
        CurrentRotation?.EventHandler?.OnResetBattle();
        IntelligenceEngine.Instance.Reset();
        MovementExecutor.Instance.Reset();
    }
```

- [ ] **Step 4: Verify build**

Run: `./build.sh`
Expected: PASS (DataStage not yet called, just compilation check)

- [ ] **Step 5: Commit**

```bash
git add HiAuRo/Runtime/AIRunner.cs HiAuRo/Runtime/AIRunner.DataStage.cs
git commit -m "feat: extract DataStage.Refresh from AIRunner, add partial class"
```

---

### Task 4: Extract DecisionStage.Decide() + PreCombatDecide()

**Files:**
- Create: `HiAuRo/Runtime/AIRunner.DecisionStage.cs`

- [ ] **Step 1: Create AIRunner.DecisionStage.cs**

```csharp
using HiAuRo.Execution;
using HiAuRo.FactAxis;
using HiAuRo.Runtime.Intelligence;

namespace HiAuRo.Runtime;

public sealed partial class AIRunner
{
    /// <summary>决策层 InCombat：轴轮询 + AI Check + 事实轴</summary>
    internal PipelineDecision Decide()
    {
        var d = new PipelineDecision();

        if (CurrentRotation?.CanPauseACRCheck != null)
        {
            d.CanPauseAck = CurrentRotation.CanPauseACRCheck() > 0;
        }

        if (ModeSwitch.CurrentMode == ModeSwitch.Mode.ExecutionAxis)
            d.ExecAxis = ExecutionAxis.Instance.Update(_battleTimeMs);

        d.AssistAxis = AssistAxis.Instance.Update(_battleTimeMs);

        // 事实轴（暂保留 StartSlot 调用，迁移步骤 2 再分离到 FactSlots）
        UpdateFactAxis();

        AiLoop?.CheckAll();

        return d;
    }

    /// <summary>决策层非战斗态：倒计时 + PreCombat + AI Check</summary>
    internal PipelineDecision PreCombatDecide(CombatContext.State state)
    {
        var d = new PipelineDecision();

        if (state == CombatContext.State.OutOfCombat)
            CurrentRotation?.EventHandler?.OnPreCombat();

        UpdateCountDown();
        AiLoop?.CheckAll();

        return d;
    }
}
```

- [ ] **Step 2: Verify build**

Run: `./build.sh`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add HiAuRo/Runtime/AIRunner.DecisionStage.cs
git commit -m "feat: extract DecisionStage with Decide + PreCombatDecide"
```

---

### Task 5: Extract ExecutionStage.Execute()

**Files:**
- Create: `HiAuRo/Runtime/AIRunner.ExecutionStage.cs`

- [ ] **Step 1: Create AIRunner.ExecutionStage.cs**

```csharp
using static HiAuRo.Data;
using HiAuRo.ACR;

namespace HiAuRo.Runtime;

public sealed partial class AIRunner
{
    /// <summary>执行层：推进已有 Slot + 门控 + 轴输出落地 + Slot 构建。允许 return。</summary>
    internal void Execute(in PipelineDecision decisions, CombatContext.State state)
    {
        var stopped = !RuntimeCore.IsRunning;
        var paused = ACR.MainControlHelper.IsPaused;
        var blockBuild = stopped || paused;

        // ── 预执行（立即生效，不 return）──
        // ForceTarget 提前到门控前，确保当前执行的 Slot 使用新目标
        if (decisions.ExecAxis?.ForceTarget != null)
            OmenTools.OmenService.TargetManager.Target = decisions.ExecAxis.ForceTarget;

        if (!blockBuild && CurrentRotation?.Opener != null
            && OpenerMgr.CurrentState == OpenerMgr.State.NotStarted)
        {
            OpenerMgr.Start(CurrentRotation.Opener);
        }

        // ProcessSpellQueue 在非战斗态也需要（热键排队的技能）
        if (!blockBuild && SpellQueue.HasPending())
        {
            var queued = SpellQueue.GetNext();
            if (queued != null)
            {
                DService.Instance().Log.Debug($"[AIRunner] ProcessSpellQueue: executing {queued.Actions.Count} spell(s)");
                SlotExecutor.StartSlot(queued);
            }
        }

        // ── 推进（不 return）──
        if (SlotExecutor.IsExecuting)
        {
            var completed = SlotExecutor.ExecuteStep();
            if (completed && OpenerMgr.CurrentState == OpenerMgr.State.Running)
                AdvanceOpener();
        }

        ExecuteOpenerIfRunning();

        // ── 非战斗态门控 ──
        if (state != CombatContext.State.InCombat) return;

        // ── Slot 构建门控 ──
        if (SlotExecutor.IsExecuting) return;
        if (decisions.CanPauseAck) return;

        // ── 轴输出应用 ──
        if (decisions.ExecAxis != null)
        {
            if (decisions.ExecAxis.PauseAcr) return;

            if (decisions.ExecAxis.ForceTarget != null)
                OmenTools.OmenService.TargetManager.Target = decisions.ExecAxis.ForceTarget;

            if (decisions.ExecAxis.ForceSpell != null && !blockBuild)
            {
                if (CurrentRotation?.CanUseHighPrioritySlotCheck != null)
                {
                    if (CurrentRotation.CanUseHighPrioritySlotCheck() < 0) return;
                }
                var slot = new Slot();
                slot.Add(decisions.ExecAxis.ForceSpell);
                SlotExecutor.StartSlot(slot);
                return;
            }
        }

        if (decisions.AssistAxis?.ForceSpell != null && !blockBuild)
        {
            var slot = new Slot();
            slot.Add(decisions.AssistAxis.ForceSpell);
            SlotExecutor.StartSlot(slot);
            return;
        }

        // 事实轴待执行技能（迁移步骤 2：当 FactSlots 启用后走此路径）
        if (decisions.FactSlots != null)
        {
            foreach (var factSlot in decisions.FactSlots)
            {
                SlotExecutor.StartSlot(factSlot);
                return; // 每帧只释放一个事实轴技能
            }
        }

        // ── 构建（新 Slot 发起）──
        if (AiLoop == null) return;

        var nextSlot = AiLoop.Build(blockBuild);
        if (nextSlot != null)
            SlotExecutor.StartSlot(nextSlot);
    }
}
```

- [ ] **Step 2: Verify build**

Run: `./build.sh`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add HiAuRo/Runtime/AIRunner.ExecutionStage.cs
git commit -m "feat: extract ExecutionStage.Execute with pre-exec + exec + gate + build sections"
```

---

### Task 6: Update ACRLifecycle to call 3 stages

**Files:**
- Modify: `HiAuRo/Runtime/ACRLifecycle.cs`

- [ ] **Step 1: Replace ACRLifecycle.Update() body**

Read current implementation at lines 125-148:

```csharp
public static void Update()
{
    CheckPendingReload();
    CheckJobSwitch();
    CheckAutoSave();

    var state = CombatContext.CurrentState;
    if (state == CombatContext.State.Idle || state == CombatContext.State.Zoning)
    {
        _resetCalled = false;
        Runner.Update();
        return;
    }

    if (state == CombatContext.State.OutOfCombat)
    {
        if (!_resetCalled) { Runner.Reset(); _resetCalled = true; }
        Runner.Update();
        return;
    }

    _resetCalled = false;
    Runner.Update();
}
```

Replace with:

```csharp
public static void Update()
{
    CheckPendingReload();
    CheckJobSwitch();
    CheckAutoSave();

    var runner = Runner;
    if (runner == null || runner.AiLoop == null) return;

    var state = CombatContext.CurrentState;

    runner.Refresh(state);

    PipelineDecision decisions;
    if (state == CombatContext.State.InCombat)
        decisions = runner.Decide();
    else
        decisions = runner.PreCombatDecide(state);

    runner.Execute(in decisions, state);
}
```

- [ ] **Step 2: Remove _resetCalled field from ACRLifecycle**

Find and delete:
```csharp
private static bool _resetCalled;
```
(Currently at approximately line 48-50 in ACRLifecycle.cs)

- [ ] **Step 3: Verify build**

Run: `./build.sh`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add HiAuRo/Runtime/ACRLifecycle.cs
git commit -m "refactor: ACRLifecycle calls 3-stage pipeline instead of Runner.Update()"
```

---

### Task 7: Remove old AIRunner.Update() + cleanup

**Files:**
- Modify: `HiAuRo/Runtime/AIRunner.cs` (remove Update(), remove Reset(), keep helpers)
- Modify: `HiAuRo/Runtime/AIRunner.DataStage.cs` (ensure helper methods are accessible)

- [ ] **Step 1: Remove the old Update() method from AIRunner.cs**

Delete the entire `public void Update()` method body (lines 127-341 in the current file). Keep the empty stub with comment for documentation:

```csharp
// Update() 已拆为三个 partial class 文件：DataStage.Refresh() / DecisionStage.Decide() / ExecutionStage.Execute()
// 入口在 ACRLifecycle.Update() 中调度
```

- [ ] **Step 2: Remove old Reset() from AIRunner.cs**

Delete the `public void Reset()` method (lines 343-355). Reset logic is now in `AIRunner.DataStage.ResetState()`.

- [ ] **Step 3: Ensure remaining private methods in AIRunner.cs are accessible to partial files**

Verify these methods exist in AIRunner.cs and are `private` (partial class shares all private members):
- `ExecuteOpenerIfRunning()`
- `AdvanceOpener()`
- `UpdateCountDown()`
- `ReadCountdown()`
- `UpdateFactAxis()`
- `UpdateDecisions()`
- `CheckPendingMitigations()`
- `TryResolveTarget()`
- `OnGameEvent()`
- `OnPhaseChanged()`

No changes needed — partial class already shares private members.

- [ ] **Step 4: Update file list in project (.csproj)**

No change needed — .csproj uses wildcard `**/*.cs` which auto-includes new partial files.

- [ ] **Step 5: Verify build**

Run: `./build.sh`
Expected: PASS, 0 errors

- [ ] **Step 6: Verify no remaining references to removed methods**

Run: `rg "Runner\.Update\(\)" HiAuRo/Runtime/ --type cs`
Expected: no matches (only the method definition, which is now the stub)

Run: `rg "Runner\.Reset\(\)" HiAuRo/Runtime/ --type cs`
Expected: no matches

- [ ] **Step 7: Commit**

```bash
git add HiAuRo/Runtime/AIRunner.cs HiAuRo/Runtime/AIRunner.DataStage.cs
git commit -m "chore: remove old AIRunner.Update and Reset, replaced by 3-stage pipeline"
```

---

### Task 8: Final verification — build + manual diff review

**Files:**
- All modified files

- [ ] **Step 1: Full clean build**

Run: `./build.sh`
Expected: PASS, 0 errors, only pre-existing warnings

- [ ] **Step 2: Verify file structure**

Run: `ls HiAuRo/Runtime/AIRunner*.cs`
Expected output:
```
AIRunner.cs
AIRunner.DataStage.cs
AIRunner.DecisionStage.cs
AIRunner.ExecutionStage.cs
```

- [ ] **Step 3: Verify AILoop interface**

Run: `git diff HEAD~7 -- HiAuRo/Runtime/IAILoop.cs`
Expected: shows CheckAll() + Build() + [Obsolete] GetNextSlot

- [ ] **Step 4: Verify ACRLifecycle**

Run: `git diff HEAD~7 -- HiAuRo/Runtime/ACRLifecycle.cs`
Expected: shows _resetCalled removed, Update() calls 3 stages

- [ ] **Step 5: Commit final state**

```bash
git add -A
git commit -m "verify: pipeline separation complete, full build passes"
```

---

### Post-Implementation Notes

1. **ACR compatibility**: No ACR interface changes (ISlotResolver.Check/Build signatures unchanged). ACR authors don't need to modify their code.
2. **Fact axis side-effects**: `UpdateFactAxis()` → `UpdateDecisions()` → `CheckPendingMitigations()` still calls `SlotExecutor.StartSlot()` internally. This is the known transitional allowance documented in the spec. Task 2 of migration (separate PR) will move these to `FactSlots`.
3. **Runtime behavior**: Frame invariant starvation is eliminated. `_battleTimeMs`, `OnBattleUpdate`, and axis updates always run every frame in combat, regardless of `SlotExecutor.IsExecuting` state.
