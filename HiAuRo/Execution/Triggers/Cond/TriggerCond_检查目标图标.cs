using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("检查目标图标", "检测目标是否有指定图标")]
[TriggerTypeName("HiAuRo.Execution.Triggers.Cond.TriggerCond_检查目标图标, HiAuRo")]

public sealed class TriggerCond_检查目标图标 : ITriggerCond
{
    public uint IconId { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        var target = Data.Target.Current;
        if (target != null && target.NamePlateIconID == IconId)
            return true;

        foreach (var enemy in Objects.Enemies)
        {
            if (enemy.NamePlateIconID == IconId)
                return true;
        }

        return false;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("IconId", (int)IconId);
    }
}
