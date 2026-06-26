using System.Numerics;

namespace HiAuRo.Runtime.Movement;

public sealed class MovementDebugSnapshot
{
    public List<string> Events { get; } = [];
    public string ActiveQueuedRequestId { get; set; } = "";
    public List<string> PendingQueuedRequestIds { get; } = [];
    public string RequestId { get; set; } = "";
    public MovementSource Source { get; set; }
    public MovementIntent Intent { get; set; }
    public Vector3 StartPos { get; set; }
    public Vector3 TargetPos { get; set; }
    public Vector3 CurrentPos { get; set; }
    public long LogicalNowMs { get; set; }
    public long DeadlineMs { get; set; }
    public long TimeToDeadlineMs { get; set; }
    public long PlannedStartMs { get; set; }
    public int CurrentRemainingTravelTimeMs { get; set; }
    public float CurrentRemainingDistance { get; set; }
    public float CurrentRemainingSpeed { get; set; }
    public int? EstimatedTravelTimeOnEnqueueMs { get; set; }
    public float? EstimatedDistanceOnEnqueue { get; set; }
    public float? EstimatedSpeedOnEnqueue { get; set; }
    public float CurrentDistanceToTarget { get; set; }
    public bool ShouldStartNow { get; set; }
    public bool ShouldFallbackToTp { get; set; }
    public bool AlreadyAtTarget { get; set; }
    public bool IsCasting { get; set; }
    public int GcdRemainingMs { get; set; }
    public int GcdDurationMs { get; set; }
    public int CastRemainingMs { get; set; }
    public long LastUpdateUtcMs { get; set; }
    public bool? NavReady { get; set; }
    public bool? PathRunning { get; set; }
    public bool? NavMoveInvokeResult { get; set; }
    public string NavMoveError { get; set; } = "";
    public long? ActualStartMs { get; set; }
    public long? ActualArriveMs { get; set; }
    public int? ActualTravelTimeMs { get; set; }
    public float? ActualDistance { get; set; }
    public float? ActualSpeed { get; set; }
    public string Status { get; set; } = "Idle";
}
