using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测自身是否属于指定队伍职能
/// </summary>
[TriggerDisplay("检查职能", "检测自身队伍职能（Tank/Healer/DPS 等）")]
[TriggerTypeName("TriggerCondCheckPartyRole")]
public sealed class TriggerCond_检查职能 : ITriggerCond
{
    public JobsCategory CategoryType { get; set; }
    public string Remark { get; set; } = "";

    /// <summary>检测自身是否属于指定队伍职能</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        var jobId = Me.ClassJob;
        if (jobId == 0) return false;
        return JobsCategoryHelper.GetCategory(jobId) == CategoryType;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddDropdown("CategoryType", Enum.GetNames<JobsCategory>(), CategoryType.ToString());
    }
}
