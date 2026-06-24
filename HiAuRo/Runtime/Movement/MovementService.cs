using System.Numerics;
using HiAuRo.ACR;
using HiAuRo.Runtime.Intelligence;

namespace HiAuRo.Runtime.Movement;

public sealed class MovementService
{
    public static MovementService Instance { get; } = new();

    private readonly Dictionary<string, MovementRequest> _requests = [];
    private readonly HashSet<string> _startedRequests = [];
    private readonly Dictionary<string, long> _actualStartMs = [];
    private readonly Dictionary<string, Vector3> _actualStartPos = [];
    private long _holdUntilTick;
    private MovementDebugSnapshot? _debugSnapshot;

    private MovementService() { }

    public int PendingCount => _requests.Count;
    public MovementDebugSnapshot? DebugSnapshot => _debugSnapshot;

    public void Enqueue(MovementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId))
            request.RequestId = Guid.NewGuid().ToString("N");

        if (request.Source == MovementSource.RemoteRelay)
            ReplaceRemoteRelayRequests(request);

        _requests[request.RequestId] = request;
        CaptureEnqueueEstimate(request);
        AppendDebugEvent($"Enqueue {request.Source}/{request.Intent} id={request.RequestId} deadline={request.DeadlineValue} travelTimeMs={request.EstimatedTravelTimeMs ?? -1}");
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

        var gcdRemainingMs = (int)Math.Round(GCDHelper.GetGCDCooldown());
        var gcdDurationMs = (int)Math.Round(GCDHelper.GetGCDDuration());
        var isCasting = DService.Instance().Condition.IsCasting;
        var castRemainingMs = isCasting ? (int)Math.Round(GetCastRemainingMs()) : 0;
        var castTotalMs = isCasting ? (int)Math.Round(GetCastTotalMs()) : 0;

        foreach (var request in _requests.Values.ToList())
        {
            var travelTimeMs = request.Intent == MovementIntent.TP
                ? 0
                : request.EstimatedTravelTimeMs ?? EstimateTravelTimeMs(currentPos.Value, request.TargetPos);

            var plan = MovementPlanner.Plan(new MovementPlannerInput
            {
                Request = request,
                CurrentPos = currentPos.Value,
                FactState = state,
                RemoteMode = request.Source == MovementSource.RemoteRelay
                    ? RemoteMovementMode.NavMesh
                    : ConvertFactAxisMode(PluginConfig.Instance.FactAxis.MovementMode),
                TravelTimeMs = travelTimeMs,
                GcdRemainingMs = gcdRemainingMs,
                GcdDurationMs = gcdDurationMs,
                IsCasting = isCasting,
                CastRemainingMs = castRemainingMs,
                CastTotalMs = castTotalMs,
                CurrentJobId = Data.Me.ClassJob
            });

            CaptureDebug(
                request,
                plan,
                currentPos.Value,
                travelTimeMs,
                gcdRemainingMs,
                gcdDurationMs,
                isCasting,
                castRemainingMs);

            if (plan.ShouldMarkArrived)
            {
                var actualTravelTimeMs = _actualStartMs.TryGetValue(request.RequestId, out var actualStartMs)
                    ? (int?)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - actualStartMs)
                    : null;
                var actualDistance = _actualStartPos.TryGetValue(request.RequestId, out var actualStartPos)
                    ? (float?)Vector3.Distance(actualStartPos, request.TargetPos)
                    : null;
                float? actualSpeed = actualTravelTimeMs is > 0 && actualDistance.HasValue
                    ? actualDistance.Value / (actualTravelTimeMs.Value / 1000f)
                    : null;
                AppendDebugEvent(
                    $"Arrived id={request.RequestId} dist={Vector3.Distance(currentPos.Value, request.TargetPos):F3} actualTravelTimeMs={actualTravelTimeMs?.ToString() ?? "-"} actualDistance={actualDistance?.ToString("F3") ?? "-"} actualSpeed={actualSpeed?.ToString("F3") ?? "-"}");
                Complete(request.RequestId);
                continue;
            }

            if (plan.ShouldStopForHold)
            {
                AppendDebugEvent($"Hold start id={request.RequestId} duration={request.DurationSec}");
                MovementDriver.Stop();
                if (request.DurationSec.HasValue && request.DurationSec.Value > 0)
                    _holdUntilTick = Environment.TickCount64 + (long)(request.DurationSec.Value * 1000);
                Complete(request.RequestId);
                continue;
            }

            if (!plan.ShouldStartNow)
            {
                if (_debugSnapshot?.RequestId == request.RequestId && _debugSnapshot.Status != "Waiting")
                {
                    _debugSnapshot.Status = "Waiting";
                    var waitMs = Math.Max(0, plan.PlannedStartMs - plan.LogicalNowMs);
                    AppendDebugEvent($"Waiting id={request.RequestId} waitMs={waitMs} dist={_debugSnapshot.CurrentDistanceToTarget:F3} travelTimeMs={travelTimeMs} timeToDeadlineMs={plan.TimeToDeadlineMs} plannedStartMs={plan.PlannedStartMs}");
                }
                continue;
            }

            if (plan.ShouldFallbackToTp)
            {
                AppendDebugEvent($"Fallback TP id={request.RequestId}");
                if (MovementDriver.TryTeleport(request.TargetPos))
                    Complete(request.RequestId);
                continue;
            }

            if (request.Intent == MovementIntent.TP)
            {
                AppendDebugEvent($"Direct TP id={request.RequestId}");
                if (MovementDriver.TryTeleport(request.TargetPos))
                    Complete(request.RequestId);
                continue;
            }

            if (!_startedRequests.Add(request.RequestId))
                continue;

            _actualStartMs[request.RequestId] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _actualStartPos[request.RequestId] = currentPos.Value;
            if (_debugSnapshot?.RequestId == request.RequestId)
            {
                _debugSnapshot.ActualStartMs = _actualStartMs[request.RequestId];
                _debugSnapshot.StartPos = currentPos.Value;
                _debugSnapshot.Status = "Moving";
            }
            AppendDebugEvent($"Invoke NavMesh id={request.RequestId} from=({currentPos.Value.X:F3},{currentPos.Value.Y:F3},{currentPos.Value.Z:F3}) to=({request.TargetPos.X:F3},{request.TargetPos.Y:F3},{request.TargetPos.Z:F3}) travelTimeMs={travelTimeMs} timeToDeadlineMs={plan.TimeToDeadlineMs}");

            if (!MovementDriver.TryStartNavMeshMove(request.TargetPos))
            {
                _startedRequests.Remove(request.RequestId);
                _actualStartMs.Remove(request.RequestId);
                _actualStartPos.Remove(request.RequestId);
                if (_debugSnapshot?.RequestId == request.RequestId)
                {
                    _debugSnapshot.Status = "NavMeshFailed";
                    _debugSnapshot.NavMoveInvokeResult = false;
                    _debugSnapshot.NavMoveError = MovementDriver.LastNavMoveError;
                }
                AppendDebugEvent($"NavMesh failed id={request.RequestId} error={MovementDriver.LastNavMoveError}");
            }
            else if (_debugSnapshot?.RequestId == request.RequestId)
            {
                _debugSnapshot.NavMoveInvokeResult = true;
                _debugSnapshot.NavMoveError = "";
                AppendDebugEvent($"NavMesh invoke ok id={request.RequestId}");
            }
        }
    }

    public void Reset()
    {
        _requests.Clear();
        _startedRequests.Clear();
        _actualStartMs.Clear();
        _actualStartPos.Clear();
        _holdUntilTick = 0;
        _debugSnapshot = null;
    }

    internal void Complete(string requestId)
    {
        if (_debugSnapshot?.RequestId == requestId && _actualStartMs.TryGetValue(requestId, out var actualStartMs))
        {
            var arriveMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _debugSnapshot.ActualArriveMs = arriveMs;
            _debugSnapshot.ActualTravelTimeMs = (int)(arriveMs - actualStartMs);
            if (_actualStartPos.TryGetValue(requestId, out var startPos))
            {
                _debugSnapshot.ActualDistance = Vector3.Distance(startPos, _debugSnapshot.TargetPos);
                if (_debugSnapshot.ActualTravelTimeMs > 0)
                    _debugSnapshot.ActualSpeed = _debugSnapshot.ActualDistance / (_debugSnapshot.ActualTravelTimeMs.Value / 1000f);
            }
            _debugSnapshot.Status = "Arrived";
        }

        _requests.Remove(requestId);
        _startedRequests.Remove(requestId);
        _actualStartMs.Remove(requestId);
        _actualStartPos.Remove(requestId);
    }

    public void EnqueueLocalTestMove(Vector3 targetPos, int delayMs)
    {
        var requestId = $"local-test-{Guid.NewGuid():N}";
        _debugSnapshot = new MovementDebugSnapshot();
        Enqueue(MovementRequestFactory.FromRemoteRelay(
            requestId,
            targetPos,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + delayMs,
            MovementPolicy.Mechanic));
        AppendDebugEvent($"Local test target=({targetPos.X:F3}, {targetPos.Y:F3}, {targetPos.Z:F3}) delayMs={delayMs}");
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
        return Math.Max(0, (player.TotalCastTime - player.CurrentCastTime) * 1000f);
    }

    private static unsafe float GetCastTotalMs()
    {
        var player = DService.Instance().ObjectTable.LocalPlayer;
        if (player == null || !player.IsCasting) return 0;
        return Math.Max(0, player.TotalCastTime * 1000f);
    }

    private static int EstimateTravelTimeMs(Vector3 from, Vector3 to)
    {
        var speed = Math.Max(0.1f, PluginConfig.Instance.MovementSpeedMps);
        try
        {
            var ipc = DService.Instance().PI.GetIpcSubscriber<Vector3, Vector3, bool, List<Vector3>>(
                "vnavmesh.Nav.Pathfind");
            var waypoints = ipc.InvokeFunc(from, to, false);
            if (waypoints == null || waypoints.Count < 2)
                return (int)Math.Round(Vector3.Distance(from, to) / speed * 1000);

            float pathLength = 0;
            for (var i = 1; i < waypoints.Count; i++)
                pathLength += Vector3.Distance(waypoints[i - 1], waypoints[i]);

            return (int)Math.Round((pathLength / speed + 0.5f) * 1000);
        }
        catch
        {
            return (int)Math.Round((Vector3.Distance(from, to) / speed + 0.5f) * 1000);
        }
    }

    private void CaptureEnqueueEstimate(MovementRequest request)
    {
        var currentPos = Data.Me.Object?.Position;
        if (currentPos == null)
            return;

        var estimateMs = request.Intent == MovementIntent.TP
            ? 0
            : EstimateTravelTimeMs(currentPos.Value, request.TargetPos);
        var distance = Vector3.Distance(currentPos.Value, request.TargetPos);
        request.EstimatedTravelTimeMs = estimateMs;

        _debugSnapshot ??= new MovementDebugSnapshot();
        _debugSnapshot.RequestId = request.RequestId;
        _debugSnapshot.EstimatedTravelTimeOnEnqueueMs = estimateMs;
        _debugSnapshot.EstimatedDistanceOnEnqueue = distance;
        _debugSnapshot.EstimatedSpeedOnEnqueue = estimateMs > 0
            ? distance / (estimateMs / 1000f)
            : 0f;
    }

    private void CaptureDebug(
        MovementRequest request,
        MovementPlannerResult plan,
        Vector3 currentPos,
        int travelTimeMs,
        int gcdRemainingMs,
        int gcdDurationMs,
        bool isCasting,
        int castRemainingMs)
    {
        var preserveWaiting = _debugSnapshot?.RequestId == request.RequestId
            && _debugSnapshot.Status == "Waiting";

        _debugSnapshot ??= new MovementDebugSnapshot();
        _debugSnapshot.RequestId = request.RequestId;
        _debugSnapshot.Source = request.Source;
        _debugSnapshot.Intent = request.Intent;
        _debugSnapshot.TargetPos = request.TargetPos;
        _debugSnapshot.CurrentPos = currentPos;
        _debugSnapshot.LogicalNowMs = plan.LogicalNowMs;
        _debugSnapshot.LastUpdateUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _debugSnapshot.DeadlineMs = request.DeadlineValue;
        _debugSnapshot.TimeToDeadlineMs = plan.TimeToDeadlineMs;
        _debugSnapshot.PlannedStartMs = plan.PlannedStartMs;
        _debugSnapshot.CurrentRemainingTravelTimeMs = travelTimeMs;
        _debugSnapshot.CurrentRemainingDistance = Vector3.Distance(currentPos, request.TargetPos);
        _debugSnapshot.CurrentRemainingSpeed = travelTimeMs > 0
            ? _debugSnapshot.CurrentRemainingDistance / (travelTimeMs / 1000f)
            : 0f;
        _debugSnapshot.CurrentDistanceToTarget = Vector3.Distance(currentPos, request.TargetPos);
        _debugSnapshot.ShouldStartNow = plan.ShouldStartNow;
        _debugSnapshot.ShouldFallbackToTp = plan.ShouldFallbackToTp;
        _debugSnapshot.AlreadyAtTarget = plan.ShouldMarkArrived;
        _debugSnapshot.IsCasting = isCasting;
        _debugSnapshot.GcdRemainingMs = gcdRemainingMs;
        _debugSnapshot.GcdDurationMs = gcdDurationMs;
        _debugSnapshot.CastRemainingMs = castRemainingMs;
        _debugSnapshot.NavReady = ReadBoolIpc("vnavmesh.Nav.IsReady");
        _debugSnapshot.PathRunning = ReadBoolIpc("vnavmesh.Path.IsRunning");
        _debugSnapshot.Status = _startedRequests.Contains(request.RequestId)
            ? "Moving"
            : preserveWaiting ? "Waiting" : "Pending";
    }

    private void AppendDebugEvent(string message)
    {
        _debugSnapshot ??= new MovementDebugSnapshot();
        var line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
        _debugSnapshot.Events.Add(line);
        if (_debugSnapshot.Events.Count > 30)
            _debugSnapshot.Events.RemoveAt(0);
        DService.Instance().Log.Debug($"[MoveTest] {message}");
    }

    private static bool ReadBoolIpc(string ipcName)
    {
        try
        {
            return DService.Instance().PI.GetIpcSubscriber<bool>(ipcName).InvokeFunc();
        }
        catch
        {
            return false;
        }
    }

    private void ReplaceRemoteRelayRequests(MovementRequest incoming)
    {
        foreach (var requestId in _requests
                     .Where(kv => kv.Value.Source == MovementSource.RemoteRelay && kv.Key != incoming.RequestId)
                     .Select(kv => kv.Key)
                     .ToArray())
        {
            _requests.Remove(requestId);
            _startedRequests.Remove(requestId);
            _actualStartMs.Remove(requestId);
            _actualStartPos.Remove(requestId);
            AppendDebugEvent($"Drop stale remote request id={requestId}");
        }
    }
}
