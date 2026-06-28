namespace HiAuRo.ACR;

public sealed class OpenerMgr
{
    public static OpenerMgr Instance { get; } = new();

    private OpenerMgr() { }

    public async Task<bool> UseOpener(Runtime.BattleData battleData, Rotation? rotation)
    {
        if (battleData.UsedOpener) return false;
        if (rotation?.Opener == null) return false;
        if (rotation.Opener.StartCheck() < 0) return false;

        // 当前只支持 Rotation 自带 opener。
        // 若后续需要支持“执行轴/时间轴覆盖 Rotation opener”，可在这里加入选择逻辑。
        battleData.PushSequence(new Runtime.SequenceWrapper { Sequence = rotation.Opener });
        battleData.UsedOpener = true;
        await Task.CompletedTask;
        return true;
    }
}
