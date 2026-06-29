using System.Numerics;
using HiAuRo.FactAxis;
using HiAuRo.Infrastructure;
using HiAuRo.Runtime.Movement;
using OmenTools;
using OmenTools.Interop.Game.Models;
using OmenTools.Info.Game.Packets.Downstream;
using HiAuRo.ACR;

namespace HiAuRo.Runtime.Intelligence;

/// <summary>
/// FactAxis 移动执行预留点。
/// 当前仅保留未来接回入口，不再通过 MovementService 实际排队/执行 FactAxis 移动。
/// </summary>
public sealed class MovementExecutor
{
    /// <summary>移动执行器单例</summary>
    public static MovementExecutor Instance { get; } = new();
    private MovementExecutor() { }

    private readonly HashSet<string> _executedDemandIds = new();
    private readonly Dictionary<string, double> _startedMoveDemands = new();

    private static unsafe ActorSetPosPacket.Delegate? _actorSetPosFunc;
    private static readonly object _tpInitLock = new();
    private long _holdUntilMs;

    // 移动参数（参考 BossMod）
    private const float 基础移速 = 6.0f;
    private const float 安全缓冲 = 0.5f;
    private const float 滑步阈值ms = 450f;

    /// <summary>重置执行器状态</summary>
    public void Reset()
    {
        _executedDemandIds.Clear();
        _startedMoveDemands.Clear();
        _holdUntilMs = 0;
        IntelligenceEngine.Instance.ActiveDemands.Clear();
    }

    /// <summary>每帧更新移动执行</summary>
    public void Update(FactState state)
    {
        var demands = IntelligenceEngine.Instance.ActiveDemands;
        if (demands.Count == 0) return;

        // FactAxis 战斗时间目前仍围绕 UTC 概念重新厘清，且移动执行/测试面尚未稳定。
        // 先只保留这里作为未来接回 FactAxis movement 的单一入口，当前不经过 MovementService 执行。
        demands.Clear();
    }

    private void 处理MoveTo(MovementDemand demand, FactState state, FactAxisFlags flags)
    {
        if (demand.TargetPos == null) return;

        double? deadline = flags.MovementTriggerMode == MovementTriggerMode.CombatTime
            ? demand.AddedOrder
            : state.CurrentEvent?.Actions.OfType<站位需求动作>().FirstOrDefault()?.Deadline;
        if (deadline == null) return;

        var playerPos = Data.Me.Object?.Position;
        if (playerPos == null) return;

        if (已到达目标(playerPos.Value, demand.TargetPos.Value))
        {
            标记执行完成(demand.Id);
            return;
        }

        var isTP = flags.MovementMode == MovementMode.TP;
        var travelTime = isTP ? 0f : 计算移动耗时(playerPos.Value, demand.TargetPos!.Value);

        // TP 兜底：寻路出发但来不及，改瞬移
        if (!isTP && flags.MovementMode == MovementMode.NavMesh_TP兜底
            && _startedMoveDemands.TryGetValue(demand.Id, out _))
        {
            if (deadline.Value - state.TotalTime < travelTime)
            {
                瞬移(demand.TargetPos.Value, demand.TargetHeading);
                标记执行完成(demand.Id);
                return;
            }
        }
        var now = state.TotalTime;
        var timeToDeadline = deadline.Value - now;

        // 非读条职业：卡 deadline 走
        if (!IsSlidecastJob(Data.Me.ClassJob))
        {
            if (timeToDeadline <= travelTime)
                执行移动(demand, flags, now);
            return;
        }

        // 读条职业：前向策略
        var gcdRemainSec = GCDHelper.GetGCDCooldown() / 1000f;
        var gcdDurationSec = GCDHelper.GetGCDDuration() / 1000f;
        var oneMoreGcd = gcdRemainSec + gcdDurationSec + travelTime;

        if (now + oneMoreGcd <= deadline.Value)
            return;

        if (IsCasting)
        {
            var castRemainSec = GetCastRemainingMs() / 1000f;
            var slidecastThresholdSec = 滑步阈值ms / 1000f;
            if (castRemainSec <= slidecastThresholdSec)
            {
                执行移动(demand, flags, now);
                return;
            }
            var slideDepart = now + castRemainSec - slidecastThresholdSec + travelTime;
            if (slideDepart <= deadline.Value)
                return;
            执行移动(demand, flags, now);
        }
        else
        {
            执行移动(demand, flags, now);
        }
    }

    private void 执行移动(MovementDemand demand, FactAxisFlags flags, double now)
    {
        if (flags.MovementMode == MovementMode.TP)
        {
            瞬移(demand.TargetPos!.Value, demand.TargetHeading);
            标记执行完成(demand.Id);
        }
        else
        {
            if (!TryMarkNavMoveStarted(_startedMoveDemands, demand.Id, now))
                return;

            try
            {
                var ipc = DService.Instance().PI.GetIpcSubscriber<Vector3, bool, bool>(
                    "vnavmesh.SimpleMove.PathfindAndMoveTo");
                ipc.InvokeFunc(demand.TargetPos!.Value, false);
            }
            catch (Exception ex)
            {
                _startedMoveDemands.Remove(demand.Id);
                DService.Instance().Log.Debug($"[Movement] VNavmesh IPC 不可用: {ex.Message}");
            }
        }
    }

    private void 处理TP(MovementDemand demand)
    {
        if (demand.TargetPos == null) return;
        瞬移(demand.TargetPos.Value, demand.TargetHeading);
        标记执行完成(demand.Id);
    }

    private void 处理Hold(MovementDemand demand)
    {
        try
        {
            DService.Instance().PI.GetIpcSubscriber<object>("vnavmesh.Path.Stop").InvokeAction();
        }
        catch { /* VNavmesh may not be installed */ }

        if (demand.Duration.HasValue && demand.Duration.Value > 0)
            _holdUntilMs = Environment.TickCount64 + (long)(demand.Duration.Value * 1000);
        标记执行完成(demand.Id);
    }

    private static bool IsSlidecastJob(uint jobId)
        => jobId is 25 or 27 or 35 or 42 or 24 or 28 or 33 or 40;

    private static unsafe float GetCastRemainingMs()
    {
        var player = DService.Instance().ObjectTable.LocalPlayer;
        if (player == null || !player.IsCasting) return 0;
        return player.CurrentCastTime * 1000f;
    }

    private static bool IsCasting
    {
        get
        {
            var player = DService.Instance().ObjectTable.LocalPlayer;
            return player?.IsCasting ?? false;
        }
    }

    private void 处理Gather_MoveTo(MovementDemand demand, FactState state, FactAxisFlags flags)
    {
        if (demand.TargetPos == null) return;

        var playerPos = Data.Me.Object?.Position;
        if (playerPos != null && 已到达目标(playerPos.Value, demand.TargetPos.Value))
        {
            标记执行完成(demand.Id);
            return;
        }

        if (!IsSlidecastJob(Data.Me.ClassJob) || !IsCasting)
        {
            执行移动(demand, flags, state.TotalTime);
            return;
        }

        var castRemainSec = GetCastRemainingMs() / 1000f;
        if (castRemainSec <= 滑步阈值ms / 1000f)
        {
            执行移动(demand, flags, state.TotalTime);
        }
    }

    private void 处理Gather_TP(MovementDemand demand)
    {
        if (demand.TargetPos == null) return;

        if (!IsSlidecastJob(Data.Me.ClassJob) || !IsCasting)
        {
            瞬移(demand.TargetPos.Value, demand.TargetHeading);
            标记执行完成(demand.Id);
            return;
        }

        var castRemainSec = GetCastRemainingMs() / 1000f;
        if (castRemainSec <= 滑步阈值ms / 1000f)
        {
            瞬移(demand.TargetPos.Value, demand.TargetHeading);
            标记执行完成(demand.Id);
        }
    }

    private void 处理Gather_Hold(MovementDemand demand)
    {
        处理Hold(demand);
    }

    private float 计算移动耗时(Vector3 from, Vector3 to)
    {
        try
        {
            var ipc = DService.Instance().PI.GetIpcSubscriber<Vector3, Vector3, bool, List<Vector3>>(
                "vnavmesh.Nav.Pathfind");
            var waypoints = ipc.InvokeFunc(from, to, false);

            if (waypoints == null || waypoints.Count < 2)
                return Vector3.Distance(from, to) / 基础移速;

            float pathLength = 0;
            for (int i = 1; i < waypoints.Count; i++)
                pathLength += Vector3.Distance(waypoints[i - 1], waypoints[i]);

            return pathLength / 基础移速 + 安全缓冲;
        }
        catch
        {
            return Vector3.Distance(from, to) / 基础移速 + 安全缓冲;
        }
    }

    private static unsafe void 瞬移(Vector3 pos, float? heading)
    {
        var localPlayer = DService.Instance().ObjectTable.LocalPlayer;
        if (localPlayer == null) return;

        if (_actorSetPosFunc == null)
        {
            lock (_tpInitLock)
            {
                if (_actorSetPosFunc == null)
                {
                    _actorSetPosFunc = ActorSetPosPacket.Signature.GetDelegate<ActorSetPosPacket.Delegate>();
                }
            }
        }
        if (_actorSetPosFunc == null) return;

        try
        {
            var packet = new ActorSetPosPacket(pos);
            _actorSetPosFunc(localPlayer.EntityID, &packet);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[Movement] TP 执行失败: {ex.Message}");
        }
    }

    private void 标记执行完成(string demandId)
    {
        _executedDemandIds.Add(demandId);
        _startedMoveDemands.Remove(demandId);
        IntelligenceEngine.Instance.ActiveDemands.RemoveAll(d => d.Id == demandId);
    }

    internal static bool TryMarkNavMoveStarted(Dictionary<string, double> startedMoveDemands, string demandId, double now)
    {
        if (startedMoveDemands.ContainsKey(demandId))
            return false;

        startedMoveDemands[demandId] = now;
        return true;
    }

    internal static bool ShouldMarkArrived(Vector3 currentPos, Vector3 targetPos, float tolerance = 0.5f)
        => 已到达目标(currentPos, targetPos, tolerance);

    private static bool 已到达目标(Vector3 currentPos, Vector3 targetPos, float tolerance = 0.5f)
        => Vector3.Distance(currentPos, targetPos) <= tolerance;
}
