namespace HiAuRo.ACR;

public sealed class OpenerMgr
{
    public static OpenerMgr Instance { get; } = new();

    private OpenerMgr() { }

    public async Task<bool> UseOpener(Runtime.BattleData battleData, Rotation? rotation)
        => await UseOpener(battleData, rotation, CancellationToken.None);

    internal async Task<bool> UseOpener(
        Runtime.BattleData battleData,
        Rotation? rotation,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (battleData.UsedOpener) return false;
        if (rotation?.Opener == null) return false;
        var startCheck = rotation.Opener.StartCheck();
        ct.ThrowIfCancellationRequested();
        if (startCheck < 0) return false;

        // 当前只支持 Rotation 自带 opener。
        // 若后续需要支持“执行轴/时间轴覆盖 Rotation opener”，可在这里加入选择逻辑。
        battleData.PushSequence(new Runtime.SequenceWrapper { Sequence = rotation.Opener });
        battleData.UsedOpener = true;
        await Task.CompletedTask;
        ct.ThrowIfCancellationRequested();
        return true;
    }
}
