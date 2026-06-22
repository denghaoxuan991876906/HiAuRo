using System.Numerics;
using HiAuRo.Runtime.Intelligence;

namespace HiAuRo.Runtime.Movement;

public sealed class MovementPlannerInput
{
    public required MovementRequest Request { get; init; }
    public required Vector3 CurrentPos { get; init; }
    public required FactAxis.FactState FactState { get; init; }
    public required RemoteMovementMode RemoteMode { get; init; }
    public int TravelTimeMs { get; init; }
    public int GcdRemainingMs { get; init; }
    public int GcdDurationMs { get; init; }
    public bool IsCasting { get; init; }
    public int CastRemainingMs { get; init; }
    public uint CurrentJobId { get; init; }
}

public sealed class MovementPlannerResult
{
    public bool ShouldStartNow { get; init; }
    public bool ShouldFallbackToTp { get; init; }
    public bool ShouldStopForHold { get; init; }
    public bool ShouldMarkArrived { get; init; }
}

public static class MovementPlanner
{
    private const int SlidecastThresholdMs = 450;

    public static MovementPlannerResult Plan(MovementPlannerInput input)
    {
        if (ShouldMarkArrived(input.CurrentPos, input.Request.TargetPos))
        {
            return new MovementPlannerResult
            {
                ShouldMarkArrived = true
            };
        }

        if (input.Request.Intent == MovementIntent.Hold)
        {
            return new MovementPlannerResult
            {
                ShouldStopForHold = true
            };
        }

        if (input.Request.Intent == MovementIntent.TP || input.RemoteMode == RemoteMovementMode.TP)
        {
            return new MovementPlannerResult
            {
                ShouldStartNow = true,
                ShouldFallbackToTp = true
            };
        }

        var nowMs = ResolveNowMs(input.Request, input.FactState);
        var timeToDeadlineMs = (int)(input.Request.DeadlineValue - nowMs);
        if (timeToDeadlineMs <= 0)
        {
            return new MovementPlannerResult
            {
                ShouldStartNow = true,
                ShouldFallbackToTp = input.RemoteMode == RemoteMovementMode.NavMesh_TP兜底
            };
        }

        var travelTimeMs = Math.Max(0, input.TravelTimeMs);
        if (input.Request.Intent == MovementIntent.Gather)
        {
            var gatherCanStart = !IsSlidecastJob(input.CurrentJobId)
                || !input.IsCasting
                || input.CastRemainingMs <= SlidecastThresholdMs;

            return new MovementPlannerResult
            {
                ShouldStartNow = gatherCanStart,
                ShouldFallbackToTp = false
            };
        }

        if (!IsSlidecastJob(input.CurrentJobId))
        {
            var shouldStart = timeToDeadlineMs <= travelTimeMs;
            return new MovementPlannerResult
            {
                ShouldStartNow = shouldStart,
                ShouldFallbackToTp = shouldStart
                    && input.RemoteMode == RemoteMovementMode.NavMesh_TP兜底
                    && timeToDeadlineMs < travelTimeMs
            };
        }

        var oneMoreGcdMs = input.GcdRemainingMs + input.GcdDurationMs + travelTimeMs;
        if (oneMoreGcdMs <= timeToDeadlineMs)
        {
            return new MovementPlannerResult
            {
                ShouldStartNow = false
            };
        }

        if (!input.IsCasting)
        {
            var shouldFallback = input.RemoteMode == RemoteMovementMode.NavMesh_TP兜底
                && timeToDeadlineMs < travelTimeMs;
            return new MovementPlannerResult
            {
                ShouldStartNow = true,
                ShouldFallbackToTp = shouldFallback
            };
        }

        if (input.CastRemainingMs <= SlidecastThresholdMs)
        {
            var shouldFallback = input.RemoteMode == RemoteMovementMode.NavMesh_TP兜底
                && timeToDeadlineMs < travelTimeMs;
            return new MovementPlannerResult
            {
                ShouldStartNow = true,
                ShouldFallbackToTp = shouldFallback
            };
        }

        var slideDepartMs = input.CastRemainingMs - SlidecastThresholdMs + travelTimeMs;
        if (slideDepartMs <= timeToDeadlineMs)
        {
            return new MovementPlannerResult
            {
                ShouldStartNow = false
            };
        }

        return new MovementPlannerResult
        {
            ShouldStartNow = true,
            ShouldFallbackToTp = input.RemoteMode == RemoteMovementMode.NavMesh_TP兜底
        };
    }

    public static bool ShouldMarkArrived(Vector3 currentPos, Vector3 targetPos, float tolerance = 0.5f)
        => Vector3.Distance(currentPos, targetPos) <= tolerance;

    private static long ResolveNowMs(MovementRequest request, FactAxis.FactState state)
        => request.DeadlineKind == MovementDeadlineKind.TargetUtcMs
            ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            : (long)Math.Round(state.TotalTime * 1000);

    private static bool IsSlidecastJob(uint jobId)
        => jobId is 25 or 27 or 35 or 42 or 24 or 28 or 33 or 40;
}
