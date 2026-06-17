using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

[TriggerDisplay("Omega循环", "检测自身是否拥有绝欧米茄麻将标识（AuraId）")]
[TriggerTypeName("TriggerCondCheckOmegaLoop")]
public sealed class TriggerCond_Omega循环 : ITriggerCond
{
    public uint AuraId { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle(ITriggerCondParams? condParams = null)
    {
        var self = Data.Me.Object;
        if (self == null) return false;
        return AuraHelper.HasAura(self, AuraId);
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("AuraId", (int)AuraId);
    }
}
