using HiAuRo.ACR;
using HiAuRo.ACR.Extension;
using static HiAuRo.Data;
using HiAuRo.Decision;
using HiAuRo.Execution;
using HiAuRo.Execution.Events;
using HiAuRo.FactAxis;
using HiAuRo.Infrastructure;
using HiAuRo.Runtime.Movement;
using HiAuRo.Runtime.Intelligence;

namespace HiAuRo.Runtime;

public sealed partial class AIRunner
{
    public static BattleData BattleData { get; } = new();
    public IRotationEntry? CurrentEntry { get; private set; }
    public Rotation? CurrentRotation { get; private set; }
    public IAILoop? AiLoop { get; private set; }
    public IRotationEventHandler? EventHandler => CurrentRotation?.EventHandler;

    /// <summary>向后兼容 —— 热键/触发器仍通过 SpellQueue 入队</summary>
    public SpellQueue SpellQueue { get; } = new();

    internal SlotExecutor SlotExecutor => _slotExecutor;

    private readonly SlotExecutor _slotExecutor;
    private readonly List<IHotkeyEventHandler> _registeredHotkeyHandlers = [];
    private CancellationTokenSource? _slotGenerationCts;
    private Task _activeSlotTask = Task.CompletedTask;
    private long _slotGeneration;
    private bool _loaded;
    private bool _enteredRotation;

    internal bool HasActiveSlotTask => !_activeSlotTask.IsCompleted;
    internal Task SlotQuiescence => _activeSlotTask;

    /// <summary>当前战斗已持续的毫秒数（进战清零、脱战清零）</summary>
    public int BattleTimeMs { get; internal set; }

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
    private CombatContext.State _prevState;
    private uint _lastTerritoryId;

    public AIRunner()
    {
        _slotExecutor = new SlotExecutor(this);
    }

    public void Load(IRotationEntry entry, string settingFolder)
    {
        Unload();
        BeginSlotGeneration();

        var registeredHotkeyHandlers = new List<IHotkeyEventHandler>();
        var enteredRotation = false;
        try
        {
            CurrentEntry = entry;
            CurrentRotation = entry.Build(settingFolder);

            if (CurrentRotation != null)
            {
                AiLoop = new AILoop_Normal();
            }

            if (CurrentRotation?.HotkeyEventHandlers != null)
            {
                foreach (var handler in CurrentRotation.HotkeyEventHandlers)
                {
                    HotkeyHelper.RegisterHandler(handler);
                    registeredHotkeyHandlers.Add(handler);
                }
            }

            CurrentEntry.OnEnterRotation();
            enteredRotation = true;

            GameEventHook.Instance.OnEventFired += OnGameEvent;
            FactTimeline.Instance.PhaseChanged += OnPhaseChanged;

            _registeredHotkeyHandlers.AddRange(registeredHotkeyHandlers);
            _enteredRotation = enteredRotation;
            _loaded = true;
        }
        catch
        {
            RollbackFailedLoad(entry, registeredHotkeyHandlers, enteredRotation);
            throw;
        }
    }

    private void RollbackFailedLoad(
        IRotationEntry entry,
        IReadOnlyList<IHotkeyEventHandler> registeredHotkeyHandlers,
        bool enteredRotation)
    {
        StopSlotGeneration();
        DetachLoadState();
        CleanupDetachedLoad(entry, registeredHotkeyHandlers, enteredRotation);
    }

    public void Unload()
    {
        var entry = CurrentEntry;
        var rotation = CurrentRotation;
        var enteredRotation = _enteredRotation;
        var hasState = _loaded || entry != null || rotation != null || AiLoop != null
            || enteredRotation || _registeredHotkeyHandlers.Count > 0
            || _slotGenerationCts != null || HasActiveSlotTask;
        if (!hasState) return;

        StopSlotGeneration();
        var registeredHotkeyHandlers = SnapshotRegisteredHotkeyHandlers();
        DetachLoadState();
        CleanupDetachedLoad(entry, registeredHotkeyHandlers, enteredRotation);
    }

    private IHotkeyEventHandler[] SnapshotRegisteredHotkeyHandlers()
    {
        try
        {
            return [.. _registeredHotkeyHandlers];
        }
        catch (Exception ex)
        {
            LogCleanupFailure("快照热键处理器", ex);
            return [];
        }
    }

    private void DetachLoadState()
    {
        _loaded = false;
        _enteredRotation = false;
        _registeredHotkeyHandlers.Clear();
        AiLoop = null;
        CurrentRotation = null;
        CurrentEntry = null;
    }

    private void CleanupDetachedLoad(
        IRotationEntry? entry,
        IEnumerable<IHotkeyEventHandler> registeredHotkeyHandlers,
        bool enteredRotation)
    {
        TryCleanupStep(() => GameEventHook.Instance.OnEventFired -= OnGameEvent, "注销游戏事件");
        TryCleanupStep(() => FactTimeline.Instance.PhaseChanged -= OnPhaseChanged, "注销阶段事件");

        if (enteredRotation && entry != null)
            TryCleanupStep(entry.OnExitRotation, "退出 Rotation");

        TryCleanupStep(() =>
        {
            foreach (var handler in registeredHotkeyHandlers)
                TryCleanupStep(() => HotkeyHelper.UnregisterHandler(handler), "注销热键处理器");
        }, "枚举热键处理器");

        if (entry != null)
            TryCleanupStep(entry.Dispose, "释放 ACR 入口");

        TryCleanupStep(BattleData.Reset, "重置 BattleData");
        TryCleanupStep(() => CountDownHandler.Instance.Reset(), "重置倒计时");
        TryCleanupStep(SpellQueue.Clear, "清空技能队列");
        TryCleanupStep(() => SpellActionTracker.Instance.Clear(), "清空技能追踪");
        TryCleanupStep(() => Coroutine.Instance.Clear(), "清空协程");
        BattleTimeMs = 0;
    }

    internal void StartCalSlot()
    {
        var cts = _slotGenerationCts;
        if (!_loaded || cts == null || HasActiveSlotTask) return;

        var startGate = new TaskCompletionSource<bool>();
        _activeSlotTask = RunCalSlotAsync(_slotGeneration, cts.Token, startGate.Task);
        Plugin.SafeFire(_activeSlotTask, "AILoop");
        startGate.SetResult(true);
    }

    internal void ResumeSlotGeneration()
    {
        if (!_loaded || _slotGenerationCts != null) return;
        BeginSlotGeneration();
    }

    internal void StopSlotGeneration()
    {
        var cts = _slotGenerationCts;
        _slotGenerationCts = null;
        if (cts == null) return;

        cts.Cancel();
        BattleData.SlotState = false;

        var quiescence = _activeSlotTask;
        if (quiescence.IsCompleted)
        {
            cts.Dispose();
            return;
        }

        _ = quiescence.ContinueWith(
            static (_, state) => ((CancellationTokenSource)state!).Dispose(),
            cts,
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    private void BeginSlotGeneration()
    {
        _slotGeneration++;
        _slotGenerationCts = new CancellationTokenSource();
    }

    private static void TryCleanupStep(Action action, string operation)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            LogCleanupFailure(operation, ex);
        }
    }

    private static void LogCleanupFailure(string operation, Exception ex)
    {
        try
        {
            if (DService.IsInitialized)
                DService.Instance().Log.Warning($"[AIRunner] ACR 清理失败 ({operation}): {ex.Message}");
        }
        catch { }
    }

    private float _lastCountRemaining;

    internal void UpdateCountDown()
    {
        var remaining = CountDownHandler.ReadCountdown();

        // 在 countdown 0 -> >0 边沿启动一次 countdown 流程；
        // 当前仍保留“每帧轮询 UI”这条入口，后续若需改成底层 hook 入口可从这里开始找。
        if (ShouldPublishCountdownStart(_lastCountRemaining, remaining))
        {
            _lastCountRemaining = remaining;
            SpellQueue.Clear();
            BattleData.Reset();
            SpellActionTracker.Instance.Clear();
            Coroutine.Instance.Clear();
            _ = CountDownHandler.Instance.Init();
            if (CurrentRotation?.Opener != null)
                CurrentRotation.Opener.InitCountDown(CountDownHandler.Instance);
            GameEventHook.Instance.Fire(new CountdownStartParams
            {
                TimeLeftSec = (int)Math.Ceiling(remaining),
                TimeLeftRaw = remaining,
            });
        }
        else
        {
            _lastCountRemaining = remaining;
        }

        // Start 被设为 false 后不再执行任何操作
        if (!CountDownHandler.Instance.Start) return;

        _ = CountDownHandler.Instance.Update(BattleData);
    }

    internal static unsafe float ReadCountdown()
    {
        return CountDownHandler.ReadCountdown();
    }

    internal static bool ShouldPublishCountdownStartForTests(float previousRemaining, float currentRemaining)
        => ShouldPublishCountdownStart(previousRemaining, currentRemaining);

    private static bool ShouldPublishCountdownStart(float previousRemaining, float currentRemaining)
        => currentRemaining > 0 && previousRemaining <= 0;

    private void UpdateFactAxis()
    {
        var flags = PluginConfig.Instance.FactAxis;

        // 观察层 FactTimeline.Update 已上提到 Refresh()（脱离 AiLoop/ConditionFlag 门控）
        bool needDecisions = flags.TeamMitigation || flags.PersonalMitigation
                            || flags.TeamHealing || flags.ForceExecute;
        if (needDecisions)
            UpdateDecisions(flags);

        CheckPendingMitigations();

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
            if (mit.WindowEndMs > 0 && BattleTimeMs >= mit.WindowStartMs && BattleTimeMs <= mit.WindowEndMs)
            {
                var spell = FactSpellTable.构造Spell(mit.SkillId);
                if (spell != null)
                {
                    var slot = new Slot();
                    slot.Source = "FactMit";
                    slot.Add(spell);
                    if (!spell.IsAbility())
                        BattleData.HighPrioritySlots_GCD.Enqueue(slot);
                    else
                        BattleData.HighPrioritySlots_OffGCD.Enqueue(slot);
                }
                mit.Executed = true;
            }
            if (mit.WindowEndMs > 0 && BattleTimeMs > mit.WindowEndMs)
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
            slot.Source = "FactHeal";
            slot.Add(spell);
            if (!spell.IsAbility())
                BattleData.HighPrioritySlots_GCD.Enqueue(slot);
            else
                BattleData.HighPrioritySlots_OffGCD.Enqueue(slot);
        }
    }

    private bool TryResolveTarget()
    {
        var cfg = PluginConfig.Instance;
        if (cfg.TargetKeepCurrent && Data.Target.Current != null) return true;

        if (cfg.TargetDisableOnDeath && Data.Me.Object is { IsDead: true }) return false;

        var self = Data.Me.Object;
        if (self == null) return false;

        var range = cfg.TargetSearchRange;

        IBattleChara? best = null;
        float bestValue = cfg.TargetSelectMode is TargetSelectMode.当前血量百分比最高 or TargetSelectMode.当前血量数值最高 or TargetSelectMode.总血量最高
            ? float.MinValue : float.MaxValue;

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

    private void OnGameEvent(ITriggerCondParams eventParams)
    {
        var handler = CurrentRotation?.EventHandler;
        handler?.OnGameEvent(eventParams);
    }

    private void OnPhaseChanged(string phaseId, string phaseName)
    {
        var handler = CurrentRotation?.EventHandler;
        handler?.OnPhaseChanged(phaseId, phaseName);
    }
}
