# 运行时核心

## 三阶段管道架构（不可退化）

AIRunner 采用 partial class 三阶段管道，**数据/决策/执行严格分离**：

```
ACRLifecycle.Update():
  → AIRunner.DataStage.Refresh(state)     ← 数据层：纯读取，无 return
  → AIRunner.DecisionStage.Decide()       ← 决策层：纯判断，无 return
  → AIRunner.ExecutionStage.Execute()     ← 执行层：门控+落地，允许 return
```

- **DataStage**：状态检测、对象/队伍扫描、战斗计时器累积。绝不 return
- **DecisionStage**：轴轮询、AI Check、事实轴。产出 `PipelineDecision`，绝不调 StartSlot
- **ExecutionStage**：Slot 推进、统一调度（PrioritySlotStack）、门控
- **PrioritySlotStack**：所有技能来源统一 Push，ExecutionStage 按优先级 Pop+StartSlot

## 核心类型

### RuntimeCore — Tick 循环入口
- `Start()` / `Stop()` / `Shutdown()`
- 通过 `OmenService.FrameworkManager.Reg(OnTick)` 注册帧回调
- OnTick: Data.IsReady → Coroutine → CombatContext → EventSystem → HotkeyPoller → ACRLifecycle

### AIRunner — AI 主引擎（partial class，4 文件）
- `AIRunner.cs` — 辅助方法（ExecuteOpenerIfRunning, UpdateCountDown, UpdateFactAxis 等）
- `AIRunner.DataStage.cs` — `Refresh(CombatContext.State)` 数据层
- `AIRunner.DecisionStage.cs` — `Decide()` / `PreCombatDecide()` 决策层
- `AIRunner.ExecutionStage.cs` — `Execute(in PipelineDecision, state)` 执行层
- `Load(IRotationEntry)` → 卸载旧 ACR, 构建 Rotation, 注册回调

### AILoop_Normal — GCD/oGCD 双通道 AI 循环
- 实现 `IAILoop`
- `CheckAll()` — DecisionStage 调用，遍历 ISlotResolver.Check()
- `Build(bool blockBuild)` — ExecutionStage 调用，基于 CheckAll 结果组装 Slot
- `GetNextSlot(bool)` — [Obsolete] 向后兼容，内部委托到 CheckAll+Build

### SlotExecutor — Slot 执行器
- `StartSlot(Slot)` → 统一入口
- `ExecuteStep()` → 跨帧状态机推进

### PrioritySlotStack — 统一调度栈
- `Instance` — 全局静态访问器
- `Push(Priority, Slot)` — 任何位置可提交 Slot
- `Pop()` — 按优先级返回最高优先级 Slot（ExecAxis > AssistAxis > FactAxis > Opener > SpellQueue > AiLoop）

### 其他组件
- `CombatContext` — 战斗状态机 (Idle/Zoning/Combat)
- `CountDownHandler` — 倒计时行为（通过 IPC）
- `OpenerMgr` — 起手序列管理
- `SpellQueue` — 技能队列（同 GCD 帧多次释放）
- `Coroutine` — 协程系统（技能延迟等）
- `EventSystem` — 事件分发（TargetChanged/SpellCastSuccess 等）
- `ModeSwitch` — 模式切换 (ExecutionAxis / FactAxis)
- `ACRLoader` / `ACRLifecycle` — ACR DLL 发现与热加载

## 加载/卸载流程
```
ACRLoader 扫描 DLL → 发现 IRotationEntry → ACRLifecycle 保存
→ 用户切换职业 → ACRLifecycle 触发 LoadEntry → AIRunner.Load(entry)
→ AIRunner.Build → 注入 SlotResolvers/Handler/Opener → 进入三阶段循环
```
