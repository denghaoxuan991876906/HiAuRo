using HiAuRo.ACR;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace HiAuRo.Execution.Triggers.Action;

[TriggerDisplay("吃药", "使用指定物品")]
[TriggerTypeName("TriggerActionUsePotion")]
public sealed class TriggerAction_吃药 : ITriggerAction
{
    public uint ItemId { get; set; }
    public string Remark { get; set; } = "";

    public bool Handle()
    {
        OmenTools.OmenService.UseActionManager.Instance().UseAction(
            ActionType.Item, ItemId, 0, 0, 0, 0);
        return true;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("ItemId", (int)ItemId);
    }
}
