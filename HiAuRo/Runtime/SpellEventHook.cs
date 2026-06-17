using Dalamud.Hooking;

namespace HiAuRo.Runtime;

/// <summary>
/// ActionRejected/CancelCast hook —— 复用 RSR/BMCN 已验证的签名。
/// 同时捕获 ActorControl、ActorControlSelf、ActorControlTarget 三种包。
/// </summary>
public sealed class SpellEventHook : IDisposable
{
    public delegate void ProcessActorControlDelegate(
        uint entityId, uint category,
        uint p1, uint p2, uint p3, uint p4,
        uint p5, uint p6, uint p7, uint p8,
        ulong targetId, byte replaying);

    private readonly Hook<ProcessActorControlDelegate> _hook;

    public event Action<uint, bool>? OnSpellResult; // (actionId, isCancelled)

    public static bool IsReplaying { get; private set; }

    public SpellEventHook()
    {
        var ptr = DService.Instance().SigScanner.ScanText(
            "E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64");

        if (ptr == IntPtr.Zero)
        {
            DService.Instance().Log.Error("[SpellEventHook] 签名未找到，CancelCast/ActionRejected 检测不可用");
            return;
        }

        _hook = DService.Instance().Hook.HookFromAddress<ProcessActorControlDelegate>(
            ptr, Detour);

        _hook.Enable();
    }

    private void Detour(uint entityId, uint category,
        uint p1, uint p2, uint p3, uint p4,
        uint p5, uint p6, uint p7, uint p8,
        ulong targetId, byte replaying)
    {
        IsReplaying = replaying != 0;

        _hook.Original(entityId, category, p1, p2, p3, p4, p5, p6, p7, p8, targetId, replaying);

        if (Data.Me.Object == null || entityId != Data.Me.Object.GameObjectID)
            return;

        switch (category)
        {
            case 15: // CancelCast — p3=actionId
                OnSpellResult?.Invoke(p3, true);
                SpellActionTracker.Instance.Notify(SpellActionType.CancelCast, p3);
                break;
            case 700: // ActionRejected — p3=actionId
                OnSpellResult?.Invoke(p3, false);
                SpellActionTracker.Instance.Notify(SpellActionType.ActionRejected, p3);
                break;
        }
    }

    public void Dispose()
    {
        _hook?.Disable();
        _hook?.Dispose();
        OnSpellResult = null;
    }
}
