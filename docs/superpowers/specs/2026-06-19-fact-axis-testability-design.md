# FactAxis 可测性优先设计

日期：2026-06-19

## 背景

在补充执行轴、事实轴、三轴协作测试时，事实轴暴露出两个现实问题：

1. `FactTimeline` 内部有大量纯逻辑值得测试，但当前直接耦合 `DService`、`GameEventHook`、`GameState`、`QTHelper`，导致纯逻辑测试很难稳定落地。
2. 为了先把测试跑起来，已经引入了 `HostlessTesting`、测试时钟入口、测试专用方法等轻量支撑。这证明事实轴逻辑本身很适合单测，但继续沿着“在单例里加测试旁路”扩展，维护成本会越来越高。

因此这轮优化不重写事实轴，也不改变运行时对外 API，而是把事实轴中已经验证出价值的纯逻辑继续剥离出来，让核心行为天然可测。

## 目标

- 提取 `FactTimeline` 中的纯逻辑核心，使其不依赖 Dalamud / OmenTools / 宿主服务即可运行。
- 保持现有 `FactTimeline.Instance.Start()/Update()/Stop()` 的运行时调用方式不变。
- 让以下行为可以直接通过 file-based app 测试稳定覆盖：
  - 时间推进
  - 变量动作生效
  - `FactState` 快照构建
  - 分支选择
  - Sync 命中校准
  - Sync 后窗口重建与后续事件推进
- 把宿主副作用（Hook、日志、QT 控制、地图/副本环境）限制在适配层。

## 非目标

- 不重写 `FactNode` 数据模型。
- 不改变 JSON 格式。
- 不调整执行轴 / 辅助轴公开接口。
- 不把事实轴整体改为新的服务注册模式。
- 不在这轮引入依赖注入框架。

## 设计总览

### 1. 结构分层

保留现有 `FactTimeline` 作为运行时适配层，新增一个纯逻辑核心，例如：

- `HiAuRo/FactAxis/FactTimelineCore.cs`
- `HiAuRo/FactAxis/FactTimelineRuntimeState.cs`（如有必要）

分层职责如下：

#### `FactTimeline`（适配层）

负责：
- 生命周期入口 (`Init/Start/Stop/Shutdown`)
- 订阅 / 退订 `GameEventHook`
- 读取 `GameState`
- 执行 `QTHelper`
- 日志输出
- 把宿主事件翻译为核心层输入

不再直接承载大部分时间推进和状态构建细节。

#### `FactTimelineCore`（纯逻辑层）

负责：
- 阶段进入
- 事件推进
- 分支切换
- Sync 窗口匹配
- Sync 校准
- 状态快照构建
- 变量更新
- 产出“待宿主执行的效果”

核心层不直接访问：
- `DService`
- `GameEventHook`
- `GameState`
- `QTHelper`

### 2. 核心输入 / 输出

核心层输入应最小化到以下几类：

- `FactTimelineData`
- 当前逻辑时间（秒 / 毫秒）
- 变量表
- “翻译后的事实事件”，例如：
  - `FactRuntimeEvent("startsUsing", 12345)`
  - `FactRuntimeEvent("ability", 9876)`

核心层输出应包含：

- 当前 `FactState`
- 更新后的内部推进状态
- 待宿主执行的效果列表，例如：
  - `SetQtEffect`
  - `ToggleQtEffect`
  - `LogEffect`

这样测试可以断言“核心产生了什么”，而运行时适配层再决定“怎么执行这些效果”。

### 3. 动作拆分策略

当前 `FactAction.Execute(FactTimeline)` 同时承载纯逻辑动作和宿主动作。可测性优先方案不需要彻底推翻这个模型，但要把行为拆成两类：

#### 纯逻辑动作

- `SetVariableAction`
- `ToggleVariableAction`
- `SkillSuggestionAction`
- 分支条件读取

这些可以在核心层直接生效。

#### 宿主副作用动作

- `设置QT动作`
- `切换QT动作`
- `LogMessageAction`

这些不在核心层直接调用宿主 API，而是由核心层返回 effect 描述，适配层消费。

### 4. 先抽离哪些函数

优先把以下已有逻辑迁移进核心层：

- `EnterPhase`
- `TrySwitchBranch`
- `AdvanceTimedEvents`
- `RunActions`（纯逻辑部分）
- `BuildSyncWindows`
- `CollectActiveWindows`
- `MatchActiveSyncs`
- `SyncTo`
- `AdvancePastExpired`
- `BuildState`

这几个函数已经在测试过程中证明是高价值区域，而且真实 bug 都出现在这里。

### 5. 运行时兼容策略

为避免一次性大改运行路径：

1. `FactTimeline` 持有一个 `FactTimelineCore` 实例
2. `Start()` 时把 `FactTimelineData` 和初始时间交给核心
3. `Update()` 时仅：
   - 取当前时间
   - 调核心推进
   - 处理核心返回的宿主 effect
   - 返回核心生成的 `FactState`
4. `OnGameEvent()` 时仅把 hook 事件翻译成 `FactRuntimeEvent` 喂给核心

这样外部调用者几乎无感。

## 测试策略

### 保留现有测试

继续保留：
- `tests/FactAxisTimelineTests.cs`

但逐步把它从“调用 `FactTimeline` 的 hostless 旁路”迁移到“直接测试 `FactTimelineCore`”。

### 新增测试组

至少补以下 3 组：

1. **连续时间推进测试**
   - 多个纯时间事件连续推进
   - 校验 `PendingEvents`、变量和 `CurrentEvent`

2. **多分支选择测试**
   - 多个 `FactSwitchBranch`
   - 条件满足时选择首个匹配分支
   - 默认分支兜底

3. **Sync 窗口与校准测试**
   - 命中窗口内事件后跳转到锚点
   - `AdvancePastExpired` 不应跳过锚点事件
   - 校准后窗口重建

### 暂不测试的内容

这轮不测：
- `GameEventHook` 原生包来源正确性
- `QTHelper` 实际调用副作用
- `DService` 日志

这些继续靠宿主验证。

## 迁移顺序

### 步骤 1：引入核心层骨架

- 新增 `FactTimelineCore`
- 搬迁最纯的内部状态与推进逻辑
- 不改外部 JSON / 调用入口

### 步骤 2：让 `FactTimeline` 转为适配层

- `Start/Update/OnGameEvent` 委托给 core
- 保持对外 API 不变

### 步骤 3：动作 effect 化

- 纯逻辑动作直接在 core 生效
- 宿主动作返回 effect 给 `FactTimeline`

### 步骤 4：迁移测试

- 让现有事实轴测试直接打 core
- 再补新的 Sync / 分支 / 多事件用例

## 风险

- 如果抽离时一次性动太多，会把本来已经稳定的运行时路径扰乱。
  规避：先保留 `FactTimeline` 外形，只把内部函数迁出。

- 如果 effect 模型设计过度，会让本来简单的事实轴动作变复杂。
  规避：这轮只 effect 化 QT / 日志类动作，不泛化所有动作。

- 如果继续保留过多 `HostlessTesting` 旁路，会形成“双系统”维护负担。
  规避：抽出 core 后，逐步删除不再需要的 hostless 旁路。

## 成功标准

- `FactTimeline` 纯逻辑主路径不再直接依赖宿主服务。
- 事实轴主要逻辑能在 file-based app 里稳定跑。
- 时间推进、变量动作、分支切换、Sync 校准都至少有 1 个稳定测试用例。
- 运行时对外 API 和 JSON 格式保持兼容。
