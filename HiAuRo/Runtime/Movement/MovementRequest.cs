using System.Numerics;
using HiAuRo.Runtime.Intelligence;

namespace HiAuRo.Runtime.Movement;

public enum MovementSource
{
    FactAxis,
    RemoteRelay
}

public enum MovementIntent
{
    MoveTo,
    Gather,
    TP,
    Hold
}

public enum MovementDeadlineKind
{
    BattleTime,
    TargetUtcMs
}

public enum RemoteMovementMode
{
    NavMesh,
    TP,
    NavMesh_TP兜底
}

public sealed class MovementRequest
{
    public MovementSource Source { get; set; }
    public MovementIntent Intent { get; set; } = MovementIntent.MoveTo;
    public Vector3 TargetPos { get; set; }
    public float? TargetHeading { get; set; }
    public float? DurationSec { get; set; }
    public MovementDeadlineKind DeadlineKind { get; set; } = MovementDeadlineKind.BattleTime;
    public long DeadlineValue { get; set; }
    public MovementPolicy Policy { get; set; } = MovementPolicy.Mechanic;
    public string RequestId { get; set; } = "";
    public int? EstimatedTravelTimeMs { get; set; }
}

public static class MovementRequestFactory
{
    public static MovementRequest FromRemoteRelay(
        string requestId,
        Vector3 targetPos,
        long targetUtcMs,
        MovementPolicy policy)
        => new()
        {
            Source = MovementSource.RemoteRelay,
            Intent = policy == MovementPolicy.Gather ? MovementIntent.Gather : MovementIntent.MoveTo,
            TargetPos = targetPos,
            DeadlineKind = MovementDeadlineKind.TargetUtcMs,
            DeadlineValue = targetUtcMs,
            Policy = policy,
            RequestId = requestId
        };

    public static MovementRequest FromDemand(MovementDemand demand, FactAxisFlags flags, FactAxis.FactState state)
    {
        var deadlineKind = flags.MovementTriggerMode == MovementTriggerMode.CombatTime
            ? MovementDeadlineKind.BattleTime
            : MovementDeadlineKind.BattleTime;

        var deadlineValue = flags.MovementTriggerMode == MovementTriggerMode.CombatTime
            ? (long)Math.Round((double)demand.AddedOrder * 1000d)
            : ResolveFactNodeDeadlineMs(state);

        return new MovementRequest
        {
            Source = MovementSource.FactAxis,
            Intent = demand.Type switch
            {
                DemandType.TP => MovementIntent.TP,
                DemandType.Hold => MovementIntent.Hold,
                _ when demand.Policy == MovementPolicy.Gather => MovementIntent.Gather,
                _ => MovementIntent.MoveTo
            },
            TargetPos = demand.TargetPos ?? default,
            TargetHeading = demand.TargetHeading,
            DurationSec = demand.Duration,
            DeadlineKind = deadlineKind,
            DeadlineValue = deadlineValue,
            Policy = demand.Policy,
            RequestId = demand.Id
        };
    }

    private static long ResolveFactNodeDeadlineMs(FactAxis.FactState state)
    {
        var deadlineSec = state.CurrentEvent?.Actions
            .OfType<FactAxis.站位需求动作>()
            .FirstOrDefault()?.Deadline;

        if (deadlineSec.HasValue)
            return (long)Math.Round(deadlineSec.Value * 1000);

        return (long)Math.Round(state.TotalTime * 1000);
    }
}
