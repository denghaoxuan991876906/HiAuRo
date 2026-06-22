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
            .RegisterAction((pos, targetUtcMs) => MovementService.Instance.Enqueue(
                MovementRequestFactory.FromRemoteRelay($"relay-moveto-{Guid.NewGuid():N}", pos, targetUtcMs, MovementPolicy.Mechanic)));
        pi.GetIpcProvider<Vector3, long, object>($"{Prefix}.GatherTo")
            .RegisterAction((pos, targetUtcMs) => MovementService.Instance.Enqueue(
                MovementRequestFactory.FromRemoteRelay($"relay-gatherto-{Guid.NewGuid():N}", pos, targetUtcMs, MovementPolicy.Gather)));
    }

    public void Dispose()
    {
        var pi = DService.Instance().PI;
        try { pi.GetIpcProvider<bool>($"{Prefix}.IsReady").UnregisterFunc(); } catch { }
        try { pi.GetIpcProvider<Vector3, long, object>($"{Prefix}.MoveTo").UnregisterAction(); } catch { }
        try { pi.GetIpcProvider<Vector3, long, object>($"{Prefix}.GatherTo").UnregisterAction(); } catch { }
    }
}
