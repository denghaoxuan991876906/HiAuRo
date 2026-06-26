using System.Numerics;
using HiAuRo.ACR;
using HiAuRo.Runtime.Intelligence;

namespace HiAuRo.Runtime.Movement;

public sealed class MovementService
{
    public static MovementService Instance { get; } = new();

    private readonly Dictionary<string, MovementRequest> _requests = [];
    private readonly List<string> _queuedRequestIds = [];
    private readonly HashSet<string> _startedRequests = [];
    private readonly Dictionary<string, long> _actualStartMs = [];
    private readonly Dictionary<string, Vector3> _actualStartPos = [];
    private string? _activeQueuedRequestId;
    private long _holdUntilTick;
    private MovementDebugSnapshot? _debugSnapshot;

    private MovementService() { }

    public int PendingCount => _requests.Count;
    public MovementDebugSnapshot? DebugSnapshot => _debugSnapshot;
    internal string? ActiveQueuedRequestId => _activeQueuedRequestId;

    private static int GetImmediateRequestPriority(MovementRequest request)
        => request.Intent switch
        {
            MovementIntent.TP => 0,
            MovementIntent.Hold => 1,
            _ => 2
        };

    public void Enqueue(MovementRequest request)
        => EnqueueCore(request, captureEstimate: true);

    internal void EnqueueForTests(MovementRequest request)
        => EnqueueCore(request, captureEstimate: false);

    internal void MarkQueuedRequestStartedForTests(string requestId)
    {
        _activeQueuedRequestId = requestId;
        _startedRequests.Add(requestId);
        SyncQueuedDebugSummary();
    }

    internal void ResetActiveQueuedMovementStartStateForTests()
        => ResetActiveQueuedMovementStartState();

    private void EnqueueCore(MovementRequest request, bool captureEstimate)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId))
            request.RequestId = Guid.NewGuid().ToString("N");

        if (request.Source != MovementSource.RemoteRelay)
        {
            AppendDebugEvent($"Ignore deferred movement source={request.Source} id={request.RequestId}");
            return;
        }

        var isQueuedMovement = IsQueuedMovement(request);
        if (request.Source == MovementSource.RemoteRelay && !isQueuedMovement)
            ReplaceRemoteRelayRequests(request);

        _requests[request.RequestId] = request;
        if (isQueuedMovement)
        {
            request.EstimatedTravelTimeMs = null;
            _queuedRequestIds.Remove(request.RequestId);
            _queuedRequestIds.Add(request.RequestId);
            AppendDebugEvent($"Queue {request.Source}/{request.Intent} id={request.RequestId} deadline={request.DeadlineValue}");
            SyncQueuedDebugSummary();
            return;
        }

        if (captureEstimate)
            CaptureEnqueueEstimate(request);
        AppendDebugEvent($"Enqueue {request.Source}/{request.Intent} id={request.RequestId} deadline={request.DeadlineValue} travelTimeMs={request.EstimatedTravelTimeMs ?? -1}");
    }

    public void Update(FactAxis.FactState state)
    {
        if (_debugSnapshot != null)
            SyncQueuedDebugSummary();

        if (_requests.Count == 0) return;
        if (Environment.TickCount64 < _holdUntilTick) return;

        var currentPos = Data.Me.Object?.Position;
        if (currentPos == null) return;

        var gcdRemainingMs = (int)Math.Round(GCDHelper.GetGCDCooldown());
        var gcdDurationMs = (int)Math.Round(GCDHelper.GetGCDDuration());
        var isCasting = DService.Instance().Condition.IsCasting;
        var castRemainingMs = isCasting ? (int)Math.Round(GetCastRemainingMs()) : 0;
        var castTotalMs = isCasting ? (int)Math.Round(GetCastTotalMs()) : 0;

        foreach (var request in SelectRequestsForCurrentTick(state))
        {
            ProcessRequest(request, state, currentPos.Value, gcdRemainingMs, gcdDurationMs, isCasting, castRemainingMs, castTotalMs);

            if (Environment.TickCount64 < _holdUntilTick)
                return;
        }
    }

    public void Reset()
    {
        _requests.Clear();
        _queuedRequestIds.Clear();
        _startedRequests.Clear();
        _actualStartMs.Clear();
        _actualStartPos.Clear();
        _activeQueuedRequestId = null;
        _holdUntilTick = 0;
        SyncQueuedDebugSummary();
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

        _queuedRequestIds.Remove(requestId);
        if (_activeQueuedRequestId == requestId)
            _activeQueuedRequestId = null;
        _requests.Remove(requestId);
        _startedRequests.Remove(requestId);
        _actualStartMs.Remove(requestId);
        _actualStartPos.Remove(requestId);
        SyncQueuedDebugSummary();
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

    private MovementRequest? GetActiveQueuedRequest(FactAxis.FactState state)
    {
        if (_activeQueuedRequestId != null)
        {
            if (_requests.TryGetValue(_activeQueuedRequestId, out var activeRequest) && IsQueuedMovement(activeRequest))
                return activeRequest;

            _activeQueuedRequestId = null;
            SyncQueuedDebugSummary();
        }

        if (_queuedRequestIds.Count == 0)
            return null;

        var queue = new MovementQueueModel(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            (long)Math.Round(state.TotalTime * 1000));

        foreach (var requestId in _queuedRequestIds)
        {
            if (_requests.TryGetValue(requestId, out var request) && IsQueuedMovement(request))
                queue.Enqueue(request);
        }

        var next = queue.TryActivateNext();
        if (next == null)
            return null;

        return next;
    }

    internal IReadOnlyList<MovementRequest> SelectRequestsForCurrentTick(FactAxis.FactState state)
    {
        var immediateRequests = _requests.Values.Where(request => !IsQueuedMovement(request)).ToList();
        if (immediateRequests.Count > 0)
            return immediateRequests
                .OrderBy(GetImmediateRequestPriority)
                .ThenBy(request => request.RequestId, StringComparer.Ordinal)
                .ToList();

        var queuedRequest = GetActiveQueuedRequest(state);
        return queuedRequest == null ? [] : [queuedRequest];
    }

    private void ProcessRequest(
        MovementRequest request,
        FactAxis.FactState state,
        Vector3 currentPos,
        int gcdRemainingMs,
        int gcdDurationMs,
        bool isCasting,
        int castRemainingMs,
        int castTotalMs)
    {
        var travelTimeMs = ResolveTravelTimeMs(request, currentPos, _startedRequests.Contains(request.RequestId));

        var plan = MovementPlanner.Plan(new MovementPlannerInput
        {
            Request = request,
            CurrentPos = currentPos,
            FactState = state,
            RemoteMode = RemoteMovementMode.NavMesh,
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
            state,
            plan,
            currentPos,
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
                $"Arrived id={request.RequestId} dist={Vector3.Distance(currentPos, request.TargetPos):F3} actualTravelTimeMs={actualTravelTimeMs?.ToString() ?? "-"} actualDistance={actualDistance?.ToString("F3") ?? "-"} actualSpeed={actualSpeed?.ToString("F3") ?? "-"}");
            Complete(request.RequestId);
            return;
        }

        if (plan.ShouldStopForHold)
        {
            AppendDebugEvent($"Hold start id={request.RequestId} duration={request.DurationSec}");
            InterruptActiveQueuedMovement();
            if (request.DurationSec.HasValue && request.DurationSec.Value > 0)
                _holdUntilTick = Environment.TickCount64 + (long)(request.DurationSec.Value * 1000);
            Complete(request.RequestId);
            return;
        }

        if (!plan.ShouldStartNow)
        {
            if (_debugSnapshot?.RequestId == request.RequestId && _debugSnapshot.Status != "Waiting")
            {
                _debugSnapshot.Status = "Waiting";
                var waitMs = Math.Max(0, plan.PlannedStartMs - plan.LogicalNowMs);
                AppendDebugEvent($"Waiting id={request.RequestId} waitMs={waitMs} dist={_debugSnapshot.CurrentDistanceToTarget:F3} travelTimeMs={travelTimeMs} timeToDeadlineMs={plan.TimeToDeadlineMs} plannedStartMs={plan.PlannedStartMs}");
            }
            return;
        }

        if (plan.ShouldFallbackToTp)
        {
            AppendDebugEvent($"Fallback TP id={request.RequestId}");
            if (MovementDriver.TryTeleport(request.TargetPos))
            {
                InterruptActiveQueuedMovement();
                Complete(request.RequestId);
            }
            return;
        }

        if (request.Intent == MovementIntent.TP)
        {
            AppendDebugEvent($"Direct TP id={request.RequestId}");
            if (MovementDriver.TryTeleport(request.TargetPos))
            {
                InterruptActiveQueuedMovement();
                Complete(request.RequestId);
            }
            return;
        }

        if (!_startedRequests.Add(request.RequestId))
            return;

        _actualStartMs[request.RequestId] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _actualStartPos[request.RequestId] = currentPos;
        if (_debugSnapshot?.RequestId == request.RequestId)
        {
            _debugSnapshot.ActualStartMs = _actualStartMs[request.RequestId];
            _debugSnapshot.StartPos = currentPos;
            _debugSnapshot.Status = "Moving";
        }
        AppendDebugEvent($"Invoke NavMesh id={request.RequestId} from=({currentPos.X:F3},{currentPos.Y:F3},{currentPos.Z:F3}) to=({request.TargetPos.X:F3},{request.TargetPos.Y:F3},{request.TargetPos.Z:F3}) travelTimeMs={travelTimeMs} timeToDeadlineMs={plan.TimeToDeadlineMs}");

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
        else
        {
            _activeQueuedRequestId = request.RequestId;
            request.EstimatedTravelTimeMs = travelTimeMs;
            SyncQueuedDebugSummary();

            if (_debugSnapshot?.RequestId == request.RequestId)
            {
                _debugSnapshot.NavMoveInvokeResult = true;
                _debugSnapshot.NavMoveError = "";
            }
            AppendDebugEvent($"NavMesh invoke ok id={request.RequestId}");
        }
    }

    private static int ResolveTravelTimeMs(MovementRequest request, Vector3 currentPos, bool hasStarted)
    {
        if (request.Intent == MovementIntent.TP)
            return 0;

        if (IsQueuedMovement(request))
            return ResolveQueuedTravelTimeMs(
                request,
                hasStarted,
                () => EstimateTravelTimeMs(currentPos, request.TargetPos));

        return request.EstimatedTravelTimeMs ?? EstimateTravelTimeMs(currentPos, request.TargetPos);
    }

    internal static int ResolveQueuedTravelTimeMs(MovementRequest request, bool hasStarted, Func<int> estimateFactory)
    {
        if (!hasStarted)
            return estimateFactory();

        return MovementQueueModel.PrepareQueuedRequestForActivation(request, estimateFactory);
    }

    private void CaptureEnqueueEstimate(MovementRequest request)
    {
        if (!DService.IsInitialized)
            return;

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
        FactAxis.FactState state,
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
        CaptureQueuedDebug(state);
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

    private void CaptureQueuedDebug(FactAxis.FactState state)
    {
        SyncQueuedDebugSummary();
        if (_debugSnapshot == null || _debugSnapshot.PendingQueuedRequestIds.Count <= 1)
            return;

        var pendingRequestIds = _debugSnapshot.PendingQueuedRequestIds.ToArray();
        _debugSnapshot.PendingQueuedRequestIds.Clear();

        var queue = new MovementQueueModel(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            (long)Math.Round(state.TotalTime * 1000));

        foreach (var requestId in pendingRequestIds)
        {
            if (_requests.TryGetValue(requestId, out var request) && IsQueuedMovement(request))
                queue.Enqueue(request);
        }

        _debugSnapshot.PendingQueuedRequestIds.AddRange(queue.PendingRequestIds);
    }

    private void SyncQueuedDebugSummary()
    {
        if (_debugSnapshot == null)
            return;

        _debugSnapshot.ActiveQueuedRequestId = _activeQueuedRequestId ?? "";
        _debugSnapshot.PendingQueuedRequestIds.Clear();

        foreach (var requestId in _queuedRequestIds)
        {
            if (requestId == _activeQueuedRequestId)
                continue;

            if (_requests.TryGetValue(requestId, out var request) && IsQueuedMovement(request))
                _debugSnapshot.PendingQueuedRequestIds.Add(requestId);
        }
    }

    private void AppendDebugEvent(string message)
    {
        _debugSnapshot ??= new MovementDebugSnapshot();
        var line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
        _debugSnapshot.Events.Add(line);
        if (_debugSnapshot.Events.Count > 30)
            _debugSnapshot.Events.RemoveAt(0);
        TryWritePluginDebugLog(message);
    }

    private static void TryWritePluginDebugLog(string message)
    {
        try
        {
            var dServiceType = Type.GetType("OmenTools.DService, OmenTools");
            if (dServiceType == null)
                return;

            var instanceMethod = dServiceType.GetMethod("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var instance = instanceMethod?.Invoke(null, null);
            var logProperty = dServiceType.GetProperty("Log");
            var log = logProperty?.GetValue(instance);
            var debugMethod = log?.GetType().GetMethod("Debug", [typeof(string)]);
            debugMethod?.Invoke(log, [$"[MoveTest] {message}"]);
        }
        catch
        {
        }
    }

    private static bool ReadBoolIpc(string ipcName)
    {
        if (!DService.IsInitialized)
            return false;

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
                     .Where(kv => kv.Value.Source == MovementSource.RemoteRelay
                                  && !IsQueuedMovement(kv.Value)
                                  && kv.Key != incoming.RequestId)
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

    private void InterruptActiveQueuedMovement()
    {
        MovementDriver.Stop();
        ResetActiveQueuedMovementStartState();
    }

    private void ResetActiveQueuedMovementStartState()
    {
        if (_activeQueuedRequestId == null)
            return;

        var activeQueuedRequestId = _activeQueuedRequestId;

        _startedRequests.Remove(activeQueuedRequestId);
        _actualStartMs.Remove(activeQueuedRequestId);
        _actualStartPos.Remove(activeQueuedRequestId);

        if (_requests.TryGetValue(activeQueuedRequestId, out var request) && IsQueuedMovement(request))
            request.EstimatedTravelTimeMs = null;

        if (_debugSnapshot?.RequestId == activeQueuedRequestId)
        {
            _debugSnapshot.ActualStartMs = null;
            _debugSnapshot.StartPos = default;
            _debugSnapshot.Status = "Pending";
        }

        _activeQueuedRequestId = null;
        SyncQueuedDebugSummary();
    }

    private static bool IsQueuedMovement(MovementRequest request)
        => request.Source == MovementSource.RemoteRelay
            && request.Intent is MovementIntent.MoveTo or MovementIntent.Gather;
}

internal sealed class MovementQueueModel
{
    private readonly List<Entry> _pending = [];
    private readonly long _currentUtcMs;
    private readonly long _battleNowMs;
    private long _sequence;
    private MovementRequest? _active;

    internal IReadOnlyList<string> PendingRequestIds => _pending
        .OrderBy(x => x.NormalizedDeadlineUtcMs)
        .ThenBy(x => x.Sequence)
        .Select(x => x.Request.RequestId)
        .ToList();

    internal MovementQueueModel(long currentUtcMs, long battleNowMs)
    {
        _currentUtcMs = currentUtcMs;
        _battleNowMs = battleNowMs;
    }

    internal void Enqueue(MovementRequest request)
    {
        _pending.Add(new Entry(request, NormalizeDeadlineUtcMs(request), _sequence++));
    }

    internal MovementRequest? TryActivateNext()
    {
        if (_active != null)
            return _active;

        if (_pending.Count == 0)
            return null;

        var next = _pending[0];
        for (var i = 1; i < _pending.Count; i++)
        {
            var candidate = _pending[i];
            if (candidate.NormalizedDeadlineUtcMs < next.NormalizedDeadlineUtcMs
                || (candidate.NormalizedDeadlineUtcMs == next.NormalizedDeadlineUtcMs
                    && candidate.Sequence < next.Sequence))
            {
                next = candidate;
            }
        }

        _pending.Remove(next);
        _active = next.Request;
        return _active;
    }

    internal static int PrepareQueuedRequestForActivation(MovementRequest request, Func<int> estimateFactory)
    {
        if (request.EstimatedTravelTimeMs.HasValue)
            return request.EstimatedTravelTimeMs.Value;

        var travelTimeMs = estimateFactory();
        request.EstimatedTravelTimeMs = travelTimeMs;
        return travelTimeMs;
    }

    internal void CompleteActive()
    {
        _active = null;
    }

    private long NormalizeDeadlineUtcMs(MovementRequest request)
        => request.DeadlineKind switch
        {
            MovementDeadlineKind.TargetUtcMs => request.DeadlineValue,
            MovementDeadlineKind.BattleTime => _currentUtcMs + Math.Max(0, request.DeadlineValue - _battleNowMs),
            _ => request.DeadlineValue
        };

    private sealed record Entry(MovementRequest Request, long NormalizedDeadlineUtcMs, long Sequence);
}
