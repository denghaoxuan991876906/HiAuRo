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
    public int CastTotalMs { get; init; }
    public uint CurrentJobId { get; init; }
}

public sealed class MovementPlannerResult
{
    public long LogicalNowMs { get; init; }
    public long TimeToDeadlineMs { get; init; }
    public long PlannedStartMs { get; init; }
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
            var immediateNowMs = ResolveNowMs(input.Request, input.FactState);
            return new MovementPlannerResult
            {
                LogicalNowMs = immediateNowMs,
                PlannedStartMs = immediateNowMs,
                ShouldMarkArrived = true
            };
        }

        if (input.Request.Intent == MovementIntent.Hold)
        {
            var holdNowMs = ResolveNowMs(input.Request, input.FactState);
            return new MovementPlannerResult
            {
                LogicalNowMs = holdNowMs,
                PlannedStartMs = holdNowMs,
                ShouldStopForHold = true
            };
        }

        if (input.Request.Intent == MovementIntent.TP || input.RemoteMode == RemoteMovementMode.TP)
        {
            var tpNowMs = ResolveNowMs(input.Request, input.FactState);
            return new MovementPlannerResult
            {
                LogicalNowMs = tpNowMs,
                PlannedStartMs = tpNowMs,
                ShouldStartNow = true,
                ShouldFallbackToTp = ShouldUseTpFallback(input)
            };
        }

        var nowMs = ResolveNowMs(input.Request, input.FactState);
        var timeToDeadlineMs = (int)(input.Request.DeadlineValue - nowMs);
        if (timeToDeadlineMs <= 0)
        {
            return new MovementPlannerResult
            {
                LogicalNowMs = nowMs,
                TimeToDeadlineMs = timeToDeadlineMs,
                PlannedStartMs = nowMs,
                ShouldStartNow = true,
                ShouldFallbackToTp = ShouldUseTpFallback(input)
            };
        }

        var travelTimeMs = Math.Max(0, input.TravelTimeMs);
        if (input.Request.Intent == MovementIntent.Gather)
        {
            var gatherCanStart = !IsSlidecastJob(input.CurrentJobId)
                || !input.IsCasting
                || input.CastRemainingMs <= SlidecastThresholdMs;
            var plannedStartMs = gatherCanStart
                ? nowMs
                : nowMs + Math.Max(0, input.CastRemainingMs - SlidecastThresholdMs);

            return new MovementPlannerResult
            {
                LogicalNowMs = nowMs,
                TimeToDeadlineMs = timeToDeadlineMs,
                PlannedStartMs = plannedStartMs,
                ShouldStartNow = gatherCanStart,
                ShouldFallbackToTp = false
            };
        }

        if (input.Request.DeadlineKind == MovementDeadlineKind.TargetUtcMs)
        {
            return PlanByLatestMoveWindow(input, nowMs, timeToDeadlineMs, travelTimeMs);
        }

        if (!IsSlidecastJob(input.CurrentJobId))
        {
            return PlanByLatestMoveWindow(input, nowMs, timeToDeadlineMs, travelTimeMs);
        }

        return PlanByLatestMoveWindow(input, nowMs, timeToDeadlineMs, travelTimeMs);
    }

    public static bool ShouldMarkArrived(Vector3 currentPos, Vector3 targetPos, float tolerance = 0.5f)
        => Vector3.Distance(currentPos, targetPos) <= tolerance;

    private static long ResolveNowMs(MovementRequest request, FactAxis.FactState state)
        => request.DeadlineKind == MovementDeadlineKind.TargetUtcMs
            ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            : (long)Math.Round(state.TotalTime * 1000);

    private static MovementPlannerResult PlanByLatestMoveWindow(
        MovementPlannerInput input,
        long nowMs,
        int timeToDeadlineMs,
        int travelTimeMs)
    {
        var latestMoveTimeMs = timeToDeadlineMs - travelTimeMs;
        if (latestMoveTimeMs <= 0)
        {
            return new MovementPlannerResult
            {
                LogicalNowMs = nowMs,
                TimeToDeadlineMs = timeToDeadlineMs,
                PlannedStartMs = nowMs,
                ShouldStartNow = true,
                ShouldFallbackToTp = ShouldUseTpFallback(input)
            };
        }

        if (!IsSlidecastJob(input.CurrentJobId))
        {
            return new MovementPlannerResult
            {
                LogicalNowMs = nowMs,
                TimeToDeadlineMs = timeToDeadlineMs,
                PlannedStartMs = nowMs + latestMoveTimeMs,
                ShouldStartNow = false
            };
        }

        var nextGcdSlidecastWindowMs = input.GcdRemainingMs + input.CastTotalMs - SlidecastThresholdMs;
        if (nextGcdSlidecastWindowMs <= latestMoveTimeMs)
        {
            return new MovementPlannerResult
            {
                LogicalNowMs = nowMs,
                TimeToDeadlineMs = timeToDeadlineMs,
                PlannedStartMs = nowMs + Math.Max(0, nextGcdSlidecastWindowMs),
                ShouldStartNow = false
            };
        }

        if (!input.IsCasting)
        {
            return new MovementPlannerResult
            {
                LogicalNowMs = nowMs,
                TimeToDeadlineMs = timeToDeadlineMs,
                PlannedStartMs = nowMs,
                ShouldStartNow = true,
                ShouldFallbackToTp = ShouldUseTpFallback(input)
            };
        }

        var currentSlidecastWindowMs = input.CastRemainingMs - SlidecastThresholdMs;
        if (currentSlidecastWindowMs <= latestMoveTimeMs)
        {
            var waitMs = Math.Max(0, currentSlidecastWindowMs);
            return new MovementPlannerResult
            {
                LogicalNowMs = nowMs,
                TimeToDeadlineMs = timeToDeadlineMs,
                PlannedStartMs = nowMs + waitMs,
                ShouldStartNow = false
            };
        }

        return new MovementPlannerResult
        {
            LogicalNowMs = nowMs,
            TimeToDeadlineMs = timeToDeadlineMs,
            PlannedStartMs = nowMs,
            ShouldStartNow = true,
            ShouldFallbackToTp = ShouldUseTpFallback(input)
        };
    }

    private static bool ShouldUseTpFallback(MovementPlannerInput input)
        => input.Request.Intent == MovementIntent.TP
            || input.RemoteMode == RemoteMovementMode.TP
            || (input.Request.Intent != MovementIntent.MoveTo
                && input.RemoteMode == RemoteMovementMode.NavMesh_TP兜底);

    private static bool IsSlidecastJob(uint jobId)
        => jobId is 25 or 27 or 35 or 42 or 24 or 28 or 33 or 40;
}
