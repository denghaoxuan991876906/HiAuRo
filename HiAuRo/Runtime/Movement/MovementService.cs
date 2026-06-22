using System.Numerics;
using HiAuRo.ACR;
using HiAuRo.Runtime.Intelligence;

namespace HiAuRo.Runtime.Movement;

public sealed class MovementService
{
    public static MovementService Instance { get; } = new();

    private readonly Dictionary<string, MovementRequest> _requests = [];
    private readonly HashSet<string> _startedRequests = [];
    private long _holdUntilTick;

    private MovementService() { }

    public int PendingCount => _requests.Count;

    public void Enqueue(MovementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId))
            request.RequestId = Guid.NewGuid().ToString("N");

        _requests[request.RequestId] = request;
    }

    public void EnqueueDemand(MovementDemand demand, FactAxisFlags flags, FactAxis.FactState state)
    {
        if (!IsFactAxisDemandEnabled(demand, flags))
            return;

        Enqueue(MovementRequestFactory.FromDemand(demand, flags, state));
    }

    public void Update(FactAxis.FactState state)
    {
        if (_requests.Count == 0) return;
        if (Environment.TickCount64 < _holdUntilTick) return;

        var currentPos = Data.Me.Object?.Position;
        if (currentPos == null) return;

        var remoteMode = PluginConfig.Instance.RemoteMoveMode;
        var gcdRemainingMs = (int)Math.Round(GCDHelper.GetGCDCooldown());
        var gcdDurationMs = (int)Math.Round(GCDHelper.GetGCDDuration());
        var isCasting = DService.Instance().Condition.IsCasting;
        var castRemainingMs = isCasting ? (int)Math.Round(GetCastRemainingMs()) : 0;

        foreach (var request in _requests.Values.ToList())
        {
            var travelTimeMs = request.Intent == MovementIntent.TP
                ? 0
                : EstimateTravelTimeMs(currentPos.Value, request.TargetPos);

            var plan = MovementPlanner.Plan(new MovementPlannerInput
            {
                Request = request,
                CurrentPos = currentPos.Value,
                FactState = state,
                RemoteMode = request.Source == MovementSource.RemoteRelay
                    ? remoteMode
                    : ConvertFactAxisMode(PluginConfig.Instance.FactAxis.MovementMode),
                TravelTimeMs = travelTimeMs,
                GcdRemainingMs = gcdRemainingMs,
                GcdDurationMs = gcdDurationMs,
                IsCasting = isCasting,
                CastRemainingMs = castRemainingMs,
                CurrentJobId = Data.Me.ClassJob
            });

            if (plan.ShouldMarkArrived)
            {
                Complete(request.RequestId);
                continue;
            }

            if (plan.ShouldStopForHold)
            {
                MovementDriver.Stop();
                if (request.DurationSec.HasValue && request.DurationSec.Value > 0)
                    _holdUntilTick = Environment.TickCount64 + (long)(request.DurationSec.Value * 1000);
                Complete(request.RequestId);
                continue;
            }

            if (!plan.ShouldStartNow) continue;

            if (plan.ShouldFallbackToTp)
            {
                if (MovementDriver.TryTeleport(request.TargetPos))
                    Complete(request.RequestId);
                continue;
            }

            if (request.Intent == MovementIntent.TP)
            {
                if (MovementDriver.TryTeleport(request.TargetPos))
                    Complete(request.RequestId);
                continue;
            }

            if (!_startedRequests.Add(request.RequestId))
                continue;

            if (!MovementDriver.TryStartNavMeshMove(request.TargetPos))
                _startedRequests.Remove(request.RequestId);
        }
    }

    public void Reset()
    {
        _requests.Clear();
        _startedRequests.Clear();
        _holdUntilTick = 0;
    }

    internal void Complete(string requestId)
    {
        _requests.Remove(requestId);
        _startedRequests.Remove(requestId);
    }

    private static RemoteMovementMode ConvertFactAxisMode(MovementMode mode)
        => mode switch
        {
            MovementMode.NavMesh => RemoteMovementMode.NavMesh,
            MovementMode.TP => RemoteMovementMode.TP,
            _ => RemoteMovementMode.NavMesh_TP兜底
        };

    private static bool IsFactAxisDemandEnabled(MovementDemand demand, FactAxisFlags flags)
        => demand.Type switch
        {
            DemandType.MoveTo => flags.MoveTo,
            DemandType.TP => flags.TP,
            DemandType.Hold => flags.Hold,
            _ => false
        };

    private static unsafe float GetCastRemainingMs()
    {
        var player = DService.Instance().ObjectTable.LocalPlayer;
        if (player == null || !player.IsCasting) return 0;
        return player.CurrentCastTime * 1000f;
    }

    private static int EstimateTravelTimeMs(Vector3 from, Vector3 to)
    {
        try
        {
            var ipc = DService.Instance().PI.GetIpcSubscriber<Vector3, Vector3, bool, List<Vector3>>(
                "vnavmesh.Nav.Pathfind");
            var waypoints = ipc.InvokeFunc(from, to, false);
            if (waypoints == null || waypoints.Count < 2)
                return (int)Math.Round(Vector3.Distance(from, to) / 6.0f * 1000);

            float pathLength = 0;
            for (var i = 1; i < waypoints.Count; i++)
                pathLength += Vector3.Distance(waypoints[i - 1], waypoints[i]);

            return (int)Math.Round((pathLength / 6.0f + 0.5f) * 1000);
        }
        catch
        {
            return (int)Math.Round((Vector3.Distance(from, to) / 6.0f + 0.5f) * 1000);
        }
    }
}
