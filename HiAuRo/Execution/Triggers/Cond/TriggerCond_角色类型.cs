using HiAuRo.ACR;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测自身职业类型分类
/// </summary>
[TriggerDisplay("角色类型", "检测自身职业类型分类（Tank/Healer/Melee/Ranged/Caster）")]
[TriggerTypeName("TriggerCond_CheckCharacterType")]
public sealed class TriggerCond_角色类型 : ITriggerCond
{
    public JobsCategory CategoryType { get; set; }
    public string Remark { get; set; } = "";

    /// <summary>检测自身职业类型是否匹配</summary>
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
