# HiAuRo 稳妥修坑第一轮设计

日期：2026-06-18

## 背景

本轮优化选择“稳妥修坑优先”，目标是在不重构三轴架构、不改变 ACR 作者公共用法的前提下，修复项目审核中发现的确定性风险点。范围只覆盖小而明确的缺陷，避免把架构降耦、UI 重做、测试体系建设混入同一轮。

## 目标

- 修复 ACR 卸载时 `OnExitRotation()` 可能被调用两次的问题。
- 修复 `SpellActionTracker` 的 slot 去重逻辑无效问题，并收紧跨线程可见性。
- 修复通用插件首次加载未记录 DLL 路径导致单插件热重载不可用的问题。
- 修复 `TreeParallel.AnyReturn` 胜出后未处理其他分支，可能留下等待任务的问题。
- 修复 `ACRLifecycle.LoadRotation()` 异常时 `IsLoadingRotation` 可能卡在 `true` 的问题。
- 让 `build.cmd` 在没有 Git Bash/WSL 时仍能在 Windows 上 fallback 到 `dotnet build`。

## 非目标

- 不引入依赖注入或大规模接口抽象。
- 不拆分 `FactTimeline`、`ACRLifecycle`、`ScriptManager` 等大类。
- 不重写 WebUI 或 ImGui UI。
- 不改变三轴互斥/并行语义。
- 不调整 ACR 作者面对的 `Rotation`、`Slot`、`Spell` 基础 API。

## 改动设计

### 1. ACR 卸载生命周期

当前 `ACRLifecycle.UnloadRotation()` 会调用 `CurrentEntry?.OnExitRotation()`，随后 `Runner.Unload()` 内部也会对 `AIRunner.CurrentEntry` 调用 `OnExitRotation()`。设计上保留 `AIRunner.Unload()` 作为 rotation 生命周期的权威出口，因为 `AIRunner.Load()` 负责 `entry.Build()`、事件订阅和 runner 内部状态，退出也应由同一对象收口。

实现时删除 `ACRLifecycle.UnloadRotation()` 中的重复 `OnExitRotation()` 调用，只保留 UI、自定义窗口、设置事件和 helper 静态状态清理。

### 2. SpellActionTracker

当前 `BeginTrack()` 判断 `slot == _lastSlot`，但 `_lastSlot` 从未赋值，去重永远不会生效。实现时在开始追踪成功后记录 `_lastSlot = slot`，在 `Clear()` 中清空 `_lastSlot`。同时把 pending spell id 的读写改为线程可见的方式：使用 `volatile uint` 字段或 `Volatile.Read/Write` 包装，保证 ActionEffect 包处理线程和主线程之间的状态观察更可靠。

`Notify(Effect)` 仍只由 `GameEventHook.OnActionEffect` 触发，不引入客户端 `OnPostUseAction` 作为 Effect 来源。

### 3. PluginLoader DllPath

`PluginLoader.ReloadPlugin()` 依赖 `PluginRecord.DllPath`，但首次 `LoadAll()` 创建记录时没有赋值。实现时在首次加载成功创建 `PluginRecord` 时写入 `DllPath = dllPath`。热重载路径不改变。

### 4. TreeParallel.AnyReturn

当前竞赛模式 `Task.WhenAny(tasks)` 返回后直接成功，未胜出的子任务可能继续挂起在条件等待里。第一轮不重写 AST 调度模型，只做最小语义修复：在 `AnyReturn` 分支胜出后将当前 `EvalContext` 标记为 disposed，让等待分支在下一次检查、延迟返回或节点入口处停止继续推进。

如发现该方式会影响同一轴后续节点，则改为局部子上下文：竞赛节点为子分支创建临时 `EvalContext`，复制 `BattleTimeMs`、`Variables`、`WaitCondFn`，胜出后只 dispose 子上下文，不污染父上下文。

### 5. LoadRotation 状态收口

`ACRLifecycle.LoadRotation()` 开头设置 `IsLoadingRotation = true`，结尾设置 false；中途异常时会卡住。实现时用 `try/finally` 保证退出时复位。异常仍按现有日志策略上抛或记录，不吞掉真正的加载失败。

### 6. build.cmd fallback

当前 `build.cmd` 找不到 `bash` 会直接失败。设计改为：

1. 优先使用 Git Bash/WSL 执行现有 `build.sh`。
2. 若找不到 `bash`，直接执行 `dotnet build "%~dp0HiAuRo.slnx" -c Debug -nologo`。
3. 保留错误码传递，让 CI/本地调用仍能判断失败。

这保持 `build.cmd` 仍是统一入口，也能满足 Windows 环境直接构建。

## 验证

- 运行 `E:\DalamudPlugins\HiAuRo\build.cmd`，确认能进入实际 `dotnet build` 并返回正确结果。
- 如变更后的纯逻辑可脱离游戏运行态执行，新增 file-based app 测试放在仓库根 `tests/` 目录，顶部使用 `#:project ../HiAuRo/HiAuRo.csproj` 和 `#:property PublishAot=false`。
- 最小测试候选为 `SpellActionTracker`：验证同一个 slot 不重复 begin、`Clear()` 后可重新追踪、错误 spell id 不污染 flag。

## 风险与回滚

- `TreeParallel.AnyReturn` 的 ctx disposed 处理如果污染父执行流，应立即切换为局部子上下文方案。
- `SpellActionTracker` 去重修复可能暴露原先被重复调用掩盖的施法重试路径，需要通过构建和进游戏验证观察。
- `build.cmd` fallback 只改变缺少 bash 时的行为；若 fallback 构建失败，应输出 dotnet 原始错误，不做静默吞错。

## 成功标准

- 第一轮只产生小范围代码差异，没有跨模块重构。
- `build.cmd` 在当前 Windows 环境能执行到 `dotnet build`。
- ACR 卸载时 `OnExitRotation()` 只触发一次。
- `PluginLoader.ReloadPlugin()` 能拿到首次加载时的 DLL 路径。
- `SpellActionTracker` 去重逻辑有可验证测试或明确手工验证路径。
