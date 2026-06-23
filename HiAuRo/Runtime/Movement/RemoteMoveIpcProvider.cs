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
                var request = MovementRequestFactory.FromRemoteRelay($"relay-moveto-{Guid.NewGuid():N}", pos, targetUtcMs, MovementPolicy.Mechanic);
                MovementService.Instance.Enqueue(request);
                DService.Instance().Log.Debug($"[MoveTrace][HiAuRoIpc] MoveTo recv now={now} targetUtc={targetUtcMs} remaining={targetUtcMs - now} travelTimeMs={request.EstimatedTravelTimeMs?.ToString() ?? "-1"} pos=({pos.X:F2},{pos.Y:F2},{pos.Z:F2})");
            });
        pi.GetIpcProvider<Vector3, long, object>($"{Prefix}.GatherTo")
            .RegisterAction((pos, targetUtcMs) =>
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var request = MovementRequestFactory.FromRemoteRelay($"relay-gatherto-{Guid.NewGuid():N}", pos, targetUtcMs, MovementPolicy.Gather);
                MovementService.Instance.Enqueue(request);
                DService.Instance().Log.Debug($"[MoveTrace][HiAuRoIpc] GatherTo recv now={now} targetUtc={targetUtcMs} remaining={targetUtcMs - now} travelTimeMs={request.EstimatedTravelTimeMs?.ToString() ?? "-1"} pos=({pos.X:F2},{pos.Y:F2},{pos.Z:F2})");
            });
    }

    public void Dispose()
    {
        var pi = DService.Instance().PI;
        try { pi.GetIpcProvider<bool>($"{Prefix}.IsReady").UnregisterFunc(); } catch { }
        try { pi.GetIpcProvider<Vector3, long, object>($"{Prefix}.MoveTo").UnregisterAction(); } catch { }
        try { pi.GetIpcProvider<Vector3, long, object>($"{Prefix}.GatherTo").UnregisterAction(); } catch { }
    }

}
