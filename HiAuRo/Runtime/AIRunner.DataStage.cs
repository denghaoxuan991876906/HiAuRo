using HiAuRo.Execution;
using HiAuRo.FactAxis;
using static HiAuRo.Data;
using HiAuRo.Runtime.Intelligence;
using HiAuRo.Runtime.Movement;

namespace HiAuRo.Runtime;

public sealed partial class AIRunner
{
    /// <summary>数据刷新阶段 —— 始终执行，不可跳过</summary>
    internal void Refresh(CombatContext.State state)
    {
        try
        {
        // 战斗状态切换检测
        if (state != _prevState)
        {
            DService.Instance().Log.Debug($"[AIRunner] 战斗状态切换: {_prevState} -> {state}");
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
                if (state == CombatContext.State.OutOfCombat)
                    ResetState();
            }
        }
        _prevState = state;

        // 切图检测
        var territoryId = Data.Combat.TerritoryType;
        if (territoryId != _lastTerritoryId)
        {
            if (_lastTerritoryId != 0)
            {
                DService.Instance().Log.Information($"[AIRunner] 切图: {_lastTerritoryId} -> {territoryId}");
                Data.Combat.AbilityCountInGcd = 0;
                Data.Combat.LastAbilityUseTime = 0;
                Data.Combat.LastGCDCompleted = 0;
                Data.Combat.MaxAbilityTimesInGcd = PluginConfig.Instance.MaxAbilityTimesInGcd;
                AbilityThrottle.Reset();
                EventHandler?.OnTerritoryChanged();
            }
            _lastTerritoryId = territoryId;
        }

        // 对象扫描（Data.Objects/Party.Refresh 已上提到 RuntimeCore.OnTick，脱离 IsRunning 门控）

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

        if (state != CombatContext.State.InCombat) return;

        if (Data.Target.Current == null)
        {
            EventHandler?.OnNoTarget();
        }

        BattleTimeMs += (int)(Data.Combat.DeltaTime * 1000);

        // 事实轴观察层（Boss 时间表推进 + State 重建）属数据刷新，提到此处脱离 AiLoop/ConditionFlag 门控
        if (PluginConfig.Instance.FactAxis.Observe)
            FactTimeline.Instance.Update(BattleTimeMs);

        // 执行轴/辅助轴推进（状态机 CheckWaitingConds + 输出缓存）属帧不变量，
        // 提到此处脱离 ConditionFlag 门控；ForceSpell 输出消费仍在 PushCombatSources
        _execOutput = ModeSwitch.CurrentMode == ModeSwitch.Mode.ExecutionAxis
            ? ExecutionAxis.Instance.Update(BattleTimeMs)
            : null;
        _assistOutput = AssistAxis.Instance.Update(BattleTimeMs);

        EventHandler?.OnBattleUpdate(BattleTimeMs);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[AIRunner.DataStage] Refresh 异常: {ex}");
        }
    }

    internal void ResetState()
    {
        SpellQueue.Clear();
        BattleData.Reset();
        CountDownHandler.Instance.Reset();
        Coroutine.Instance.Clear();
        BattleTimeMs = 0;
        Data.Combat.AbilityCountInGcd = 0;
        Data.Combat.LastAbilityUseTime = 0;
        Data.Combat.LastGCDCompleted = 0;
        Data.Combat.MaxAbilityTimesInGcd = PluginConfig.Instance.MaxAbilityTimesInGcd;
        AbilityThrottle.Reset();
        EventHandler?.OnResetBattle();
        IntelligenceEngine.Instance.Reset();
        MovementExecutor.Instance.Reset();
        MovementService.Instance.Reset();
    }
}
