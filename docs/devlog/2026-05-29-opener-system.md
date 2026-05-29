# HiAuRo 开发日志

## 2026-05-29 — Opener 系统重构

### 背景

ACR 起手系统（Opener）存在多个问题：
- 倒计时读取依赖第三方 Countdown 插件 IPC
- 倒计时结束无法自动衔接 Opener
- SlotExecutor 一帧 dump 所有 action，不等待释放条件
- OpenerMgr 每帧无脑推进一步
- 回调（BeforeSpell/OnSpellCastSuccess/AfterSpell）在 UseAction 同一帧调用

### 改动清单

#### 1. CountDownHandler — 倒计时读取 + 毫秒单位

**文件**: `HiAuRo/Runtime/CountDownHandler.cs`

- 移除 IPC 读取，改为通过 `AgentCountDownSettingDialog` 直接读游戏内存（与 AE 一致）
- `AddAction` / `Update` 单位统一为毫秒
- 增加 `_countdownWasActive` 标志，区分"从未有过倒计时"和"倒计时刚结束"
- `SetExecutor(SlotExecutor)` 注入执行器，预动作通过 `StartSlot` 释放
- `CountdownFinished` 只在倒计时从活跃变为不活跃时设为 true（每帧不再重复触发）

#### 2. SlotExecutor — 跨帧状态机重写

**文件**: `HiAuRo/Runtime/SlotExecutor.cs`

**旧设计**: `ExecuteSlot(Slot)` 同步执行，一帧 dump 所有 action。

**新设计**: 统一跨帧模式，所有 Slot 走同一路径。

| API | 用途 |
|-----|------|
| `StartSlot(Slot)` | 开始执行一个 Slot |
| `ExecuteStep()` | 每帧调用，返回 true 表示 Slot 完成 |
| `IsExecuting` | 当前是否有 Slot 在执行 |

`ExecuteStep()` 逻辑：
```
检查超时 → 超时跳过当前 action
BeforeSpell（每次尝试前调用）
检查释放条件：
  - GCD 技能：等 GCD 就绪
  - 能力技：等间隔就绪
  - 条件不满足 → return false（下帧重试）
条件满足 → UseAction
  - 成功：
    - 能力技 → 立即触发 OnSpellCastSuccess + AfterSpell
    - GCD 技能 → 挂起，等 OnPostUseAction 确认后触发
    - 移除 action，处理下一个
  - 失败 → 下帧重试
```

#### 3. OpenerMgr — PeekCurrentSlot + Advance 模式

**文件**: `HiAuRo/ACR/OpenerMgr.cs`

**旧设计**: `Update()` 返回 Slot 并立即推进到下一步。

**新设计**:
- `PeekCurrentSlot()` — 返回当前 Slot，不推进
- `Advance()` — 当前 Slot 完成后调用，构建下一个 Step 的 Slot
- 空 Slot 自动跳过
- 所有 Step 完成 → `State = Finished`

#### 4. AIRunner — 倒计时 + 起手执行流程

**文件**: `HiAuRo/Runtime/AIRunner.cs`

| 改动 | 说明 |
|------|------|
| `ReadCountdown()` | 通过 `AgentCountDownSettingDialog` 读取倒计时，返回秒数 |
| `UpdateCountDown()` | 在 Idle/Zoning 状态下也调用；倒计时结束自动启动 OpenerMgr |
| `IsExecuting` 检查 | `Update()` 最前面：有正在执行的 Slot 就继续推进，不管战斗状态 |
| Opener 执行 | `NotStarted → Start()`，`Running → PeekCurrentSlot + StartSlot + ExecuteStep + Advance` |
| 所有 `ExecuteSlot` 调用 | 改为 `StartSlot`（SpellQueue/AILoop/热键/辅助轴/事实轴） |

#### 5. Spell.Name — 惰性解析

**文件**: `HiAuRo/ACR/Spell.cs`, `HiAuRo/ACR/Spell_Computed.cs`

- `Name` 属性改为惰性解析：首次访问时从 `SpellHelper.GetActionRow(Id)?.Name` 读取游戏数据
- 移除构造函数中的 `Name = id.ToString()`
- 效果：`new Spell { Id = 16505 }` 的 Name 自动解析为 "绝望"

#### 6. EventSystem — AfterSpell 确认

**文件**: `HiAuRo/Runtime/EventSystem.cs`

- `OnPostUseAction` 中增加 `ACRLifecycle.Runner?.SlotExecutor?.NotifySpellUsed(actionId)`
- 当游戏确认技能释放后，触发等待中的 AfterSpell 回调

#### 7. 触发条件 — 移除 IPC

**文件**: `HiAuRo/Execution/Triggers/Cond/TriggerCond_倒计时开始.cs`, `TriggerCond_倒计时.cs`

- 移除 `Countdown.CountdownTimer` IPC 读取
- 改为调用 `AIRunner.ReadCountdown()`（Agent 内存读取）

### 设计参考

AE（`E:\DalamudPlugins\AEAssist 国服 1024\AEAssistCNVersion\AEAssist`）的实现：
- `Slot.Run()` — 逐个 action 执行，失败重试（100ms），超时跳过
- `SlotAction.Run()` — GCD 技能异步等待 GCD 就绪后再释放
- `BeforeSpell` — 每次尝试前调用
- `AfterSpell` — `Spell.Cast` 成功后调用
- `MemApiCountdown` — 通过 `Countdown.Instance` 读取游戏内存

### 遗留

- `OnSpellCastSuccess`（读条完成回调）的 `CharacterCompleteCastHook` 集成待做
- `SlotHelper.ExecuteSlot` 已改为 `StartSlot`，但注释需要更新

### 编译

```bash
cmd.exe /c "dotnet build E:\DalamudPlugins\HiAuRo\HiAuRo.slnx -c Debug -nologo"
# 0 errors, 14 warnings（均为预先存在的警告）
```
