using HiAuRo.ACR;
using HiAuRo.Execution.Events;
using static HiAuRo.Data;

namespace HiAuRo.Execution.Triggers.Cond;

/// <summary>
/// 检测敌人是否在读指定技能
/// 事件驱动：匹配 ActorCastParams 快速唤醒；轮询：遍历 Objects.Enemies 检查 CastActionID
/// </summary>
[TriggerDisplay("敌人读条", "检测指定敌人是否在读指定技能")]
[TriggerTypeName("TriggerCondEnemyCastSpell")]
public sealed class TriggerCond_敌人读条 : ITriggerCond
{
    public uint SpellId { get; set; }
    public uint EnemyDataId { get; set; }
    public string Remark { get; set; } = "";

    /// <summary>检测敌人是否在读指定技能</summary>
    public bool Handle(ITriggerCondParams? condParams = null)
    {
        if (condParams is EnemyCastSpellCondParams cast)
        {
            if (cast.SpellId != SpellId) return false;

            if (EnemyDataId != 0 && cast.DataId != EnemyDataId)
                return false;
            return true;
        }

        foreach (var enemy in Objects.Enemies)
        {
            if (enemy is not IBattleNPC battleNpc) continue;
            if (!battleNpc.IsCasting) continue;

            if (EnemyDataId != 0 && battleNpc.DataID != EnemyDataId) continue;
            if (battleNpc.CastActionID != SpellId) continue;

            return true;
        }
        return false;
    }

    public void Draw(ACR.IUiBuilder builder)
    {
        builder.AddIntInput("SpellId", (int)SpellId);
        builder.AddIntInput("EnemyDataId", (int)EnemyDataId);
    }
}
