using HiAuRo.ACR;
using HiAuRo.ACR.Extension;
using static HiAuRo.Data;
using HiAuRo.Decision;
using HiAuRo.Execution;
using HiAuRo.Execution.Events;
using HiAuRo.FactAxis;
using HiAuRo.Infrastructure;
using HiAuRo.Runtime.Intelligence;

namespace HiAuRo.Runtime;

/// <summary>
/// AI 主引擎 —— 加载 ACR、调度 IAILoop + SlotExecutor
/// </summary>
public sealed class AIRunner
{
    /// <summary>当前 ACR 入口</summary>
    public IRotationEntry? CurrentEntry { get; private set; }
    /// <summary>当前 Rotation</summary>
    public Rotation? CurrentRotation { get; private set; }
    /// <summary>AI 循环实例</summary>
    public IAILoop? AiLoop { get; private set; }
    /// <summary>事件处理器</summary>
    public IRotationEventHandler? EventHandler => CurrentRotation?.EventHandler;

    /// <summary>技能队列</summary>
    public SpellQueue SpellQueue { get; } = new();
    /// <summary>起手管理器</summary>
    public OpenerMgr OpenerMgr { get; } = new();
    /// <summary>Slot 执行器</summary>
    public SlotExecutor SlotExecutor { get; private set; }
    /// <summary>倒计时处理器</summary>
    public CountDownHandler CountDownHandler { get; } = new();

    private int _battleTimeMs;
    private bool _loaded;
    private CombatContext.State _prevFactAxisState;
    // B3: 需求分治 + 去重
    private readonly HashSet<string> _processedHealEventIds = new();
    private readonly HashSet<string> _processedMitEventIds = new();
    private readonly List<PendingMitigation> _pendingMits = new();

    private sealed class PendingMitigation(string eventId, uint skillId, string skillName, long windowStartMs, long windowEndMs)
    {
        public string EventId { get; } = eventId;
        public uint SkillId { get; } = skillId;
        public string SkillName { get; } = skillName;
        public long WindowStartMs { get; } = windowStartMs;
        public long WindowEndMs { get; } = windowEndMs;
        public bool Executed { get; set; }
    }
    private CombatContext.State _prevExecAxisState; // 执行轴战斗状态追踪
    private CombatContext.State _prevAssistAxisState; // 辅助轴战斗状态追踪
    private CombatContext.State _prevState; // 用于检测战斗状态切换
    private uint _lastTerritoryId; // 用于检测切图

    /// <summary>Initializes a new instance of the <see cref="AIRunner"/> class</summary>
    public AIRunner()
    {
        SlotExecutor = new SlotExecutor(this);
    }

    /// <summary>加载 ACR</summary>
    public void Load(IRotationEntry entry, string settingFolder)
    {
        Unload();

        CurrentEntry = entry;
        CurrentRotation = entry.Build(settingFolder);

        if (CurrentRotation != null)
        {
            AiLoop = new AILoop_Normal(CurrentRotation.SlotResolvers);
        }

        // 注册倒计时行为（每次起手前由 UpdateCountDown 懒注册，此处仅注入 Executor）
        if (CurrentRotation?.Opener != null)
            CountDownHandler.SetExecutor(SlotExecutor);

        // 注册 Rotation 级热键处理器
        if (CurrentRotation?.HotkeyEventHandlers != null)
        {
            foreach (var handler in CurrentRotation.HotkeyEventHandlers)
                HotkeyHelper.RegisterHandler(handler);
        }

        CurrentEntry.OnEnterRotation();

        // 订阅游戏事件和阶段事件，转发给 ACR
        GameEventHook.Instance.OnEventFired += OnGameEvent;
        FactTimeline.Instance.PhaseChanged += OnPhaseChanged;

        _loaded = true;
    }

    /// <summary>卸载 ACR</summary>
    public void Unload()
    {
        if (!_loaded) return;

        // 取消事件订阅，防止卸载后仍收到转发
        GameEventHook.Instance.OnEventFired -= OnGameEvent;
        FactTimeline.Instance.PhaseChanged -= OnPhaseChanged;

        CurrentEntry?.OnExitRotation();

        // 注销 Rotation 级热键处理器
        if (CurrentRotation?.HotkeyEventHandlers != null)
        {
            foreach (var handler in CurrentRotation.HotkeyEventHandlers)
                HotkeyHelper.UnregisterHandler(handler);
        }

        CurrentEntry?.Dispose();

        SpellQueue.Clear();
        OpenerMgr.Reset();
        CountDownHandler.Reset();
        Coroutine.Instance.Clear();

        AiLoop = null;
        CurrentRotation = null;
        CurrentEntry = null;
        _loaded = false;
        _battleTimeMs = 0;
    }

    /// <summary>每帧由 RuntimeCore 调用</summary>
    public void Update()
    {
        if (!_loaded || AiLoop == null) return;

        var stopped = !RuntimeCore.IsRunning;
        var paused = ACR.MainControlHelper.IsPaused;
        var blockBuild = stopped || paused;  // 停止/暂停都阻断 Build+执行，但 Check 继续

        try
        {
            var state = CombatContext.CurrentState;

            // 进入战斗时清掉非战斗攒下的热键队列，避免延迟爆发
            if (state != _prevState)
            {
                DService.Instance().Log.Debug($"[AIRunner] 战斗状态切换: {_prevState} → {state}");
                if (state == CombatContext.State.InCombat)
                {
                    DService.Instance().Log.Debug("[AIRunner] 进入战斗, 清空旧 SpellQueue");
                    SpellQueue.Clear();
                }
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

            // 目标选择器 —— 所有状态下均工作
            // 倒计时时自动选择目标（仅副本生效）：无视 TargetSelectorEnabled，只要倒计时开始且无目标就自动选
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

            // SlotExecutor 有正在执行的 Slot → 继续推进
            if (SlotExecutor.IsExecuting)
            {
                var completed = SlotExecutor.ExecuteStep();
                if (completed && OpenerMgr.CurrentState == OpenerMgr.State.Running)
                    AdvanceOpener();
                return;
            }

            // Opener 推进：在所有状态下均执行（倒计时结束后立即开始，不依赖战斗状态）
            ExecuteOpenerIfRunning();
            if (SlotExecutor.IsExecuting) return;

            if (state == CombatContext.State.Idle || state == CombatContext.State.Zoning)
            {
                UpdateCountDown();
                ProcessSpellQueue(blockBuild);
                AiLoop.GetNextSlot(blockBuild: true);
                return;
            }

            if (state != CombatContext.State.InCombat)
            {
                CurrentRotation?.EventHandler?.OnPreCombat();
                UpdateCountDown();
                ProcessSpellQueue(blockBuild);
                AiLoop.GetNextSlot(blockBuild: true);
                return;
            }

            // 更新战斗数据和对象扫描
#if DEBUG
            long _aiPt = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            Data.Party.Refresh();
#if DEBUG
            PerfMonitor.Record("Party.Refresh", _aiPt); _aiPt = System.Diagnostics.Stopwatch.GetTimestamp();
#endif

            // 目标选择器已在上面执行过，若仍无目标则通知 ACR
            if (Data.Target.Current == null)
            {
                DService.Instance().Log.Debug("[AIRunner] 无目标, 调用 OnNoTarget");
                CurrentRotation?.EventHandler?.OnNoTarget();
                return;
            }

            // 战斗计时器
            _battleTimeMs += (int)(Data.Combat.DeltaTime * 1000);
            CurrentRotation?.EventHandler?.OnBattleUpdate(_battleTimeMs);
#if DEBUG
            PerfMonitor.Record("BattleUpdate", _aiPt); _aiPt = System.Diagnostics.Stopwatch.GetTimestamp();
#endif

            // ACR 暂停检查
            if (CurrentRotation?.CanPauseACRCheck != null)
            {
                var pauseResult = CurrentRotation.CanPauseACRCheck();
                if (pauseResult > 0)
                {
                    DService.Instance().Log.Debug($"[AIRunner] ACR 暂停检查返回 {pauseResult}, 跳过本帧");
                    return;
                }
            }

            // --- 执行轴检查（Phase 6） ---
            if (ModeSwitch.CurrentMode == ModeSwitch.Mode.ExecutionAxis)
            {
                // 战斗状态变化 → 启停执行轴
                if (state != _prevExecAxisState)
                {
                    _prevExecAxisState = state;
                    if (state == CombatContext.State.InCombat)
                        ExecutionAxis.Instance.Start();
                    else if (state == CombatContext.State.OutOfCombat || state == CombatContext.State.Idle)
                        ExecutionAxis.Instance.Stop();
                }

                var execOutput = ExecutionAxis.Instance.Update(_battleTimeMs);
                if (execOutput != null)
                {
                    // 暂停 ACR
                    if (execOutput.PauseAcr)
                        return;

                    // 强制切换目标（不受 blockBuild 影响）
                    if (execOutput.ForceTarget != null)
                    {
                        OmenTools.OmenService.TargetManager.Target = execOutput.ForceTarget;
                    }

                    // 强制释放技能（受 blockBuild 影响）
                    if (execOutput.ForceSpell != null && !blockBuild)
                    {
                        if (CurrentRotation?.CanUseHighPrioritySlotCheck != null)
                        {
                            var canUse = CurrentRotation.CanUseHighPrioritySlotCheck();
                            if (canUse < 0) return;
                        }

                        var slot = new Slot();
                        slot.Add(execOutput.ForceSpell);
                        SlotExecutor.StartSlot(slot);
                        return;
                    }
                }
            }

            // 起手序列：NotStarted 也可能在战斗中首次触发
            if (!blockBuild && CurrentRotation?.Opener != null
                && OpenerMgr.CurrentState == OpenerMgr.State.NotStarted)
            {
                OpenerMgr.Start(CurrentRotation.Opener);
            }

            // 执行队列中待处理的 Slot（受 blockBuild 影响，优先于 AI 循环）
            if (ProcessSpellQueue(blockBuild))
                return;

            // 从 AI 循环获取下一个 Slot（Check 总是执行，Build 受 blockBuild 影响）
            var nextSlot = AiLoop.GetNextSlot(blockBuild);
#if DEBUG
            PerfMonitor.Record("AILoop", _aiPt); _aiPt = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            if (nextSlot != null)
                SlotExecutor.StartSlot(nextSlot);
#if DEBUG
            PerfMonitor.Record("SlotExec", _aiPt);
#endif
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[AIRunner] Update 异常: {ex}");
        }
    }

    /// <summary>重置战斗状态</summary>
    public void Reset()
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

    /// <summary>倒计时阶段检查</summary>
    private void UpdateCountDown()
    {
        var remaining = ReadCountdown();

        // 倒计时开始事件：Reset + 注册
        if (remaining > 0 && !CountDownHandler.WasActive)
        {
            CountDownHandler.Reset();
            if (CurrentRotation?.Opener != null)
                CurrentRotation.Opener.InitCountDown(CountDownHandler);
        }

        if (!CountDownHandler.HasPending) return;

        try
        {
            CountDownHandler.Update(remaining * 1000f);
        }
        catch (Exception ex)
        {
            Hi.Debug($"[AIRunner] UpdateCountDown 异常: {ex.Message}");
        }

        // 倒计时结束 → 自动启动起手序列
        if (CountDownHandler.CountdownFinished
            && CurrentRotation?.Opener != null
            && OpenerMgr.CurrentState == OpenerMgr.State.NotStarted)
        {
            Hi.Debug("[AIRunner] 倒计时结束, 自动启动 OpenerMgr");
            OpenerMgr.Start(CurrentRotation.Opener);
        }
    }

    /// <summary>推进 opener，完成后 Reset CountDownHandler</summary>
    private void AdvanceOpener()
    {
        OpenerMgr.Advance();
        if (OpenerMgr.CurrentState == OpenerMgr.State.Finished)
            CountDownHandler.Reset();
    }

    /// <summary>如果 opener 已启动，推进执行（不依赖战斗状态）</summary>
    private void ExecuteOpenerIfRunning()
    {
        if (CurrentRotation?.Opener == null) return;
        if (OpenerMgr.CurrentState != OpenerMgr.State.Running) return;

        if (!SlotExecutor.IsExecuting)
        {
            var slot = OpenerMgr.PeekCurrentSlot();
            if (slot != null)
                SlotExecutor.StartSlot(slot);
            else
                AdvanceOpener();
        }

        if (SlotExecutor.IsExecuting)
        {
            var completed = SlotExecutor.ExecuteStep();
            if (completed)
                AdvanceOpener();
        }
    }

    /// <summary>读取倒计时剩余秒数，0 表示倒计时未开始或已结束</summary>
    internal static unsafe float ReadCountdown()
    {
        var module = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentModule.Instance();
        if (module == null) return 0;

        var agent = module->GetAgentByInternalId(FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentId.CountDownSettingDialog);
        if (agent == null) return 0;

        var countdown = (FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentCountDownSettingDialog*)agent;
        if (!countdown->Active) return 0;

        return countdown->TimeRemaining;
    }


    /// <summary>
    /// 事实轴更新（Phase 7）—— 不控制 ACR，只输出当前战斗事实状态
    /// </summary>
    /// <summary>
    /// 辅助轴更新 — 始终运行，独立于执行轴/事实轴
    /// </summary>
    private void UpdateAssistAxis(CombatContext.State state)
    {
        if (state != _prevAssistAxisState)
        {
            _prevAssistAxisState = state;
            if (state == CombatContext.State.InCombat)
                AssistAxis.Instance.Start();
            else if (state == CombatContext.State.OutOfCombat || state == CombatContext.State.Idle)
                AssistAxis.Instance.Stop();
        }

        if (state != CombatContext.State.InCombat) return;

        var output = AssistAxis.Instance.Update(_battleTimeMs);
        if (output?.ForceSpell != null)
        {
            var slot = new Slot();
            slot.Add(output.ForceSpell);
            SlotExecutor.StartSlot(slot);
        }
    }

    private void UpdateFactAxis(CombatContext.State state)
    {
        var flags = PluginConfig.Instance.FactAxis;

        if (state != _prevFactAxisState)
        {
            _prevFactAxisState = state;
            if (state == CombatContext.State.InCombat)
                FactTimeline.Instance.Start();
            else
                FactTimeline.Instance.Stop();
        }

        if (state != CombatContext.State.InCombat) return;

        // 时间线观测
        if (flags.Observe)
            FactTimeline.Instance.Update(_battleTimeMs);

        // 决策分配
        bool needDecisions = flags.TeamMitigation || flags.PersonalMitigation
                            || flags.TeamHealing || flags.ForceExecute;
        if (needDecisions)
            UpdateDecisions(flags);

        // 检查到期减伤
        CheckPendingMitigations();

        // 智能层 + 移动执行
        IntelligenceEngine.Instance.Update(FactTimeline.Instance);
        MovementExecutor.Instance.Update(FactTimeline.Instance.State);
    }

    private void UpdateDecisions(FactAxisFlags flags)
    {
        var state = FactTimeline.Instance.State;
        var ev = state.CurrentEvent;
        if (ev == null || ev.Actions.Count == 0) return;

        foreach (var action in ev.Actions)
        {
            switch (action)
            {
                case 需求治疗动作 heal when !_processedHealEventIds.Contains(ev.Id):
                    _processedHealEventIds.Add(ev.Id);
                    if (flags.TeamHealing)
                    {
                        var output = DecisionEngine.Instance.计算治疗(heal.Value);
                        执行分配技能(output);
                    }
                    break;

                case 需求减伤动作 mit when !_processedMitEventIds.Contains(ev.Id):
                    _processedMitEventIds.Add(ev.Id);
                    if (flags.TeamMitigation || flags.PersonalMitigation)
                    {
                        var output = DecisionEngine.Instance.计算减伤(mit.Value);
                        // 不立即执行，记录窗口
                        foreach (var alloc in output.减伤分配)
                        {
                            int durSec = 10;
                            long damageMs = (long)((ev.Time + (ev.Duration ?? 0)) * 1000);
                            long windowStart = damageMs - durSec * 1000;
                            _pendingMits.Add(new PendingMitigation(ev.Id, alloc.技能ID, alloc.技能名称, windowStart, damageMs));
                        }
                    }
                    break;
                
            }
        }
    }

    private void CheckPendingMitigations()
    {
        var flags = PluginConfig.Instance.FactAxis;
        if (!flags.ForceExecute) return;

        for (int i = _pendingMits.Count - 1; i >= 0; i--)
        {
            var mit = _pendingMits[i];
            if (mit.Executed) { _pendingMits.RemoveAt(i); continue; }
            if (mit.WindowEndMs > 0 && _battleTimeMs >= mit.WindowStartMs && _battleTimeMs <= mit.WindowEndMs)
            {
                var spell = FactSpellTable.构造Spell(mit.SkillId);
                if (spell != null)
                {
                    var slot = new Slot();
                    slot.Add(spell);
                    SlotExecutor.StartSlot(slot);
                }
                mit.Executed = true;
            }
            if (mit.WindowEndMs > 0 && _battleTimeMs > mit.WindowEndMs)
                _pendingMits.RemoveAt(i);
        }
    }

    private void 执行分配技能(DecisionOutput output)
    {
        var flags = PluginConfig.Instance.FactAxis;
        if (!flags.ForceExecute || output.执行技能IDs.Count == 0) return;

        foreach (var skillId in output.执行技能IDs)
        {
            var spell = FactSpellTable.构造Spell(skillId);
            if (spell == null) continue;
            var slot = new Slot();
            slot.Add(spell);
            SlotExecutor.StartSlot(slot);
        }
    }

    /// <summary>通过 PluginConfig 设置自动选择目标</summary>
    private bool TryResolveTarget()
    {
        var cfg = PluginConfig.Instance;
        // 注: TargetSelectorEnabled 由调用者检查，此处不重复（倒计时路径需要独立于此开关）

        // 有目标且设置为不切换，跳过
        if (cfg.TargetKeepCurrent && Data.Target.Current != null) return true;

        // 死亡自动禁用：自身死亡时跳过
        if (cfg.TargetDisableOnDeath && Data.Me.Object is { IsDead: true }) return false;

        var self = Data.Me.Object;
        if (self == null) return false;

        var range = cfg.TargetSearchRange;

        // 从敌人列表中筛选候选目标
        IBattleChara? best = null;
        float bestValue = cfg.TargetSelectMode is TargetSelectMode.当前血量百分比最高 or TargetSelectMode.当前血量数值最高 or TargetSelectMode.总血量最高
            ? float.MinValue : float.MaxValue;

        // 第一轮：若启用攻击头标优先，先筛出有头标的敌人
        var candidates = new List<IBattleChara>();
        foreach (var obj in Data.Objects.Enemies)
        {
            if (obj is not IBattleChara bc) continue;
            if (!bc.IsTargetable || bc.IsDead == true) continue;
            if (cfg.TargetExcludeDummies && bc.IsDummy()) continue;
            var dist = Data.Me.DistanceToObject3D(bc, false);
            if (dist > range) continue;
            candidates.Add(bc);
        }

        if (cfg.TargetPreferAggroMarked)
        {
            var marked = candidates.Where(c => c.IsInEnemiesList()).ToList();
            if (marked.Count > 0)
                candidates = marked;
        }

        foreach (var bc in candidates)
        {
            var dist = Data.Me.DistanceToObject3D(bc, false);

            float value = cfg.TargetSelectMode switch
            {
                TargetSelectMode.当前血量百分比最高 or TargetSelectMode.当前血量百分比最低
                    => bc.MaxHp > 0 ? (float)bc.CurrentHp / bc.MaxHp : 0f,
                TargetSelectMode.当前血量数值最高 or TargetSelectMode.当前血量数值最低
                    => bc.CurrentHp,
                TargetSelectMode.总血量最高
                    => bc.MaxHp,
                TargetSelectMode.距离最近
                    => dist,
                _ => dist,
            };

            bool isBetter = cfg.TargetSelectMode switch
            {
                TargetSelectMode.当前血量百分比最高 or TargetSelectMode.当前血量数值最高 or TargetSelectMode.总血量最高
                    => value > bestValue,
                _ => value < bestValue,
            };

            if (isBetter || best == null)
            {
                bestValue = value;
                best = bc;
            }
        }

        if (best == null) return false;

        OmenTools.OmenService.TargetManager.Target = best;
        return Data.Target.Current != null;
    }

    internal bool ProcessSpellQueue(bool blockBuild)
    {
        if (blockBuild || !SpellQueue.HasPending()) return false;
        var queued = SpellQueue.GetNext();
        if (queued != null)
        {
            DService.Instance().Log.Debug($"[AIRunner] ProcessSpellQueue: executing {queued.Actions.Count} spell(s)");
            SlotExecutor.StartSlot(queued);
            return true;
        }
        return false;
    }

    /// <summary>转发游戏事件到当前 ACR 的 EventHandler。线程安全：先捕获 handler 引用再调用，防止 Unload 期间 CurrentRotation 变 null。</summary>
    private void OnGameEvent(ITriggerCondParams eventParams)
    {
        var handler = CurrentRotation?.EventHandler;
        handler?.OnGameEvent(eventParams);
    }

    /// <summary>转发阶段切换到当前 ACR 的 EventHandler。线程安全：先捕获 handler 引用再调用，防止 Unload 期间 CurrentRotation 变 null。</summary>
    private void OnPhaseChanged(string phaseId, string phaseName)
    {
        var handler = CurrentRotation?.EventHandler;
        handler?.OnPhaseChanged(phaseId, phaseName);
    }
}
