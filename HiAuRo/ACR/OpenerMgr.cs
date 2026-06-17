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

        battleData.PushSequence(new Runtime.SequenceWrapper { Sequence = rotation.Opener });
        battleData.UsedOpener = true;
        await Task.CompletedTask;
        return true;
    }
}
