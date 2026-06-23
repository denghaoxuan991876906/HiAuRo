using System.Numerics;
using HiAuRo.Runtime.Intelligence;

namespace HiAuRo.Runtime.Movement;

public sealed class RemoteMoveIpcProvider : IDisposable
{
    public const string Prefix = "HiAuRo.RemoteMove";

    public void Register()
    {
        var pi = DService.Instance().PI;
        pi.GetIpcProvider<bool>($"{Prefix}.IsReady").RegisterFunc(() => true);
        pi.GetIpcProvider<Vector3, long, object>($"{Prefix}.MoveTo")
            .RegisterAction((pos, targetUtcMs) =>
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var currentPos = Data.Me.Object?.Position;
                var travelTimeMs = currentPos == null ? -1 : EstimateTravelTimeMs(currentPos.Value, pos);
                DService.Instance().Log.Debug($"[MoveTrace][HiAuRoIpc] MoveTo recv now={now} targetUtc={targetUtcMs} remaining={targetUtcMs - now} travelTimeMs={travelTimeMs} pos=({pos.X:F2},{pos.Y:F2},{pos.Z:F2})");
                MovementService.Instance.Enqueue(
                    MovementRequestFactory.FromRemoteRelay($"relay-moveto-{Guid.NewGuid():N}", pos, targetUtcMs, MovementPolicy.Mechanic));
            });
        pi.GetIpcProvider<Vector3, long, object>($"{Prefix}.GatherTo")
            .RegisterAction((pos, targetUtcMs) =>
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var currentPos = Data.Me.Object?.Position;
                var travelTimeMs = currentPos == null ? -1 : EstimateTravelTimeMs(currentPos.Value, pos);
                DService.Instance().Log.Debug($"[MoveTrace][HiAuRoIpc] GatherTo recv now={now} targetUtc={targetUtcMs} remaining={targetUtcMs - now} travelTimeMs={travelTimeMs} pos=({pos.X:F2},{pos.Y:F2},{pos.Z:F2})");
                MovementService.Instance.Enqueue(
                    MovementRequestFactory.FromRemoteRelay($"relay-gatherto-{Guid.NewGuid():N}", pos, targetUtcMs, MovementPolicy.Gather));
            });
    }

    public void Dispose()
    {
        var pi = DService.Instance().PI;
        try { pi.GetIpcProvider<bool>($"{Prefix}.IsReady").UnregisterFunc(); } catch { }
        try { pi.GetIpcProvider<Vector3, long, object>($"{Prefix}.MoveTo").UnregisterAction(); } catch { }
        try { pi.GetIpcProvider<Vector3, long, object>($"{Prefix}.GatherTo").UnregisterAction(); } catch { }
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
}
