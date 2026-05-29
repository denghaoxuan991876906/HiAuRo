# Opener System Redesign Spec

## 问题

当前 SlotExecutor 和 OpenerMgr 存在以下问题：

1. **SlotExecutor** — 多个技能堆在一个 Slot，能力技间隔不到直接 skip，GCD 技能 UseAction 后立即触发回调（不等游戏确认）
2. **OpenerMgr** — 每帧无脑推进一步，不等待上一步完成
3. **回调时机** — BeforeSpell/OnSpellCastSuccess/AfterSpell 全在 UseAction 同一帧调用，不等游戏确认

## 设计参考

AE（`E:\DalamudPlugins\AEAssist 国服 1024\AEAssistCNVersion\AEAssist`）的实现：

- `Slot.Run()` — 逐个 action 执行，失败重试（100ms），超时跳过
- `SlotAction.Run()` — GCD 技能异步等待 GCD 就绪后再释放
- `BeforeSpell` — 每次尝试前调用
- `AfterSpell` — `Spell.Cast` 成功后调用
- `OnSpellCastSuccess` — 网络包确认时调用
- `OpenerMgr` — opener 作为 Sequence 传入 BattleData，由 Slot 执行系统驱动

## 改动范围

### 1. SlotExecutor — 重写 ExecuteSlot

**当前问题**：一个 Slot 内堆多个技能，能力技间隔不到直接 skip，回调在 UseAction 同一帧。

**改为**：Slot 内有多个 action，逐个执行，每个 action 独立等待释放条件，失败重试，超时跳过。

```csharp
public async Task<bool> ExecuteSlot(Slot slot)
{
    var handler = _runner.EventHandler;
    var breakTime = Environment.TickCount64 + slot.MaxDuration;

    while (slot.Actions.Count > 0)
    {
        var spell = slot.Actions[0].Spell;

        // BeforeSpell — 每次尝试前调用
        handler?.BeforeSpell(slot, spell);

        // 循环等待释放条件满足
        while (Environment.TickCount64 < breakTime)
        {
            // GCD 技能：等待 GCD 就绪
            if (!spell.IsAbility() && !GCDHelper.IsGCDReady())
            {
                await Task.Delay(50);
                continue;
            }

            // 能力技：等待间隔就绪
            if (spell.IsAbility() && !Data.Combat.AbilityIntervalElapsed)
            {
                await Task.Delay(50);
                continue;
            }

            // 尝试释放
            var result = UseActionManager.Instance().UseAction(...);
            if (result)
            {
                if (spell.IsAbility())
                    Data.Combat.LastAbilityUseTime = Environment.TickCount64;
                // AfterSpell 由 OnPostUseAction 触发
                break; // 成功，处理下一个 action
            }

            await Task.Delay(50); // 重试
        }

        // 超时 → 跳过当前 action
        slot.Actions.RemoveAt(0);
    }

    return true; // 所有 action 处理完毕
}
```

**关键变化**：
- Slot 内有多个 action，逐个执行
- 每个 action 独立等待释放条件（能力技和 GCD 分开判断）
- 条件不满足就等，不 skip
- 失败重试，超时（MaxDuration 默认 600ms）跳过当前 action，继续下一个
- `BeforeSpell` 在每个 action 的尝试循环开始前调用

### 2. OpenerMgr — Step 推进改为等待 Slot 完成

**当前问题**：每帧无脑推进一步。

**改为**：Step 中的 Slot 全部执行完毕才推进到下一个 Step。

```csharp
public sealed class OpenerMgr
{
    private IOpener? _currentOpener;
    private int _currentStep;
    private int _currentSlotInStep;

    public State CurrentState { get; private set; }

    // 返回当前要执行的 Slot（不推进）
    public Slot? PeekCurrentSlot() { ... }

    // 当前 Slot 完成后调用，推进到下一个
    public void Advance() { ... }
}
```

**Step 推进流程**：
```
Step 1 有 3 个 Slot: [SlotA, SlotB, SlotC]
  ↓
PeekCurrentSlot() → SlotA
SlotExecutor 执行 SlotA → 完成
Advance() → 移动到 SlotB
  ↓
PeekCurrentSlot() → SlotB
SlotExecutor 执行 SlotB → 完成
Advance() → 移动到 SlotC
  ↓
PeekCurrentSlot() → SlotC
SlotExecutor 执行 SlotC → 完成
Advance() → 移动到 Step 2
  ↓
Step 2 ...
```

### 3. AIRunner.Update() — Opener 执行分支

```csharp
if (!blockBuild && OpenerMgr.CurrentState == OpenerMgr.State.Running)
{
    var slot = OpenerMgr.PeekCurrentSlot();
    if (slot != null)
    {
        var completed = await SlotExecutor.ExecuteSlot(slot);
        if (completed)
            OpenerMgr.Advance();
        return;
    }
}
```

### 4. 回调系统 — 基于游戏 Hook 触发

| 回调 | 触发源 | 时机 |
|------|--------|------|
| `BeforeSpell` | 框架层（SlotExecutor） | 每次尝试释放前 |
| `OnSpellCastSuccess` | `CharacterCompleteCastHook` | 读条完成时（非读条不触发） |
| `AfterSpell` | `OnPostUseAction` | UseAction 返回 true = 游戏确认 action 已发送 |

**EventSystem 改动**：

```csharp
// OnPostUseAction 中，触发 AfterSpell
private static void OnPostUseAction(bool result, ActionType actionType, uint actionId, ...)
{
    if (result)
    {
        // 触发 AfterSpell 回调
        // 需要从 SlotExecutor 获取当前正在执行的 Slot 和 Spell
        CurrentSlotExecutor?.NotifySpellUsed(actionId);
    }
}
```

**SlotExecutor 改动**：
```csharp
// 当前正在等待确认的 Slot
private Slot? _pendingSlot;
private Spell? _pendingSpell;

// OnPostUseAction 调用此方法
internal void NotifySpellUsed(uint actionId)
{
    if (_pendingSpell?.Id == actionId)
    {
        _runner.EventHandler?.AfterSpell(_pendingSlot, _pendingSpell);
        _pendingSlot = null;
        _pendingSpell = null;
    }
}
```

### 5. CountDownHandler — 保持不变

当前 CountDownHandler 已经正确：直接调用 UseAction，不走 SlotExecutor。预读条技能不需要走完整的 Slot 执行流程。

### 6. Spell.Name 自动解析 — 保持不变

已实现的 `Spell.Name` 惰性解析保持不变。

## 不改动的部分

- `IOpener` 接口（InitCountDown / Sequence / StartCheck / StopCheck）
- `Rotation` 容器
- `CountDownHandler`（预读条逻辑）
- `Spell.Name` 惰性解析

## 验证

1. 编译通过
2. 进游戏测试 BLM opener：
   - 倒计时预读条正常触发
   - Step 1: 高闪雷 → 等 GCD → 即刻咏唱 → 等间隔 → 详述 → 等间隔 → ...
   - 每个技能等待释放条件满足后才执行
   - AfterSpell 在游戏确认后触发
   - OnSpellCastSuccess 只在读条完成时触发
