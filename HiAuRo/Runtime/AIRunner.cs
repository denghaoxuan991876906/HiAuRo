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
public sealed partial class AIRunner
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

    // Update() 已拆为三个 partial class 文件: DataStage.Refresh() / DecisionStage.Decide() / ExecutionStage.Execute()
    // 入口在 ACRLifecycle.Update() 中调度

    // Reset() 已迁移到 AIRunner.DataStage.ResetState()

    /// <summary>倒计时阶段检查</summary>
    private void UpdateCountDown()
    {
        var remaining = ReadCountdown();

        // 倒计时开始事件：整个 opener 状态重置
        if (remaining > 0 && !CountDownHandler.WasActive)
        {
            CountDownHandler.Reset();
            OpenerMgr.Reset();
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

        // 倒计时结束前 50ms → 提前启动起手序列（弥补进战斗前的网络/动画空窗）
        if ((remaining > 0 && remaining * 1000 <= 50
             || CountDownHandler.CountdownFinished)
            && CurrentRotation?.Opener != null
            && OpenerMgr.CurrentState == OpenerMgr.State.NotStarted)
        {
            Hi.Debug($"[AIRunner] 倒计时 {remaining*1000:F0}ms 启动 OpenerMgr");
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

    /// <summary>如果 opener 已启动，推进执行（不依赖战斗状态）。</summary>
    private void ExecuteOpenerIfRunning()
    {
        if (CurrentRotation?.Opener == null) return;
        if (OpenerMgr.CurrentState != OpenerMgr.State.Running) return;

        // 有 Slot 正在执行（由顶层推进），不重复干预
        if (SlotExecutor.IsExecuting) return;

        var slot = OpenerMgr.PeekCurrentSlot();
        if (slot != null)
        {
            PrioritySlotStack.Instance.Push(PrioritySlotStack.Priority.Opener, slot);
        }
        else
        {
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


    /// <summary>事实轴更新（Phase 7）—— 时间线观测 + 决策分配 + 智能层 + 移动执行。Start/Stop 由 Update() 顶层状态转换统一处理。</summary>
    private void UpdateFactAxis()
    {
        var flags = PluginConfig.Instance.FactAxis;

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
                    PrioritySlotStack.Instance.Push(PrioritySlotStack.Priority.FactAxis, slot);
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
            PrioritySlotStack.Instance.Push(PrioritySlotStack.Priority.FactAxis, slot);
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
