using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;
using OmenTools.OmenService;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace HiAuRo.ACR;

/// <summary>
/// 技能辅助 —— 冷却 / 充能 / 距离 / 资源 综合判断
/// </summary>
public static class SpellHelper
{
    /// <summary>技能可用：冷却转好了吗</summary>
    public static bool CanUseSpell(uint id)
    {
        return UseActionManager.Instance().IsActionOffCooldown(ActionType.Action, id);
    }

    /// <summary>综合就绪检查（CD + MP + 目标 + 解锁等），需手动指定 targetId</summary>
    public static unsafe bool IsActionReady(uint actionId, ulong targetId)
        => ActionManager.Instance()->GetActionStatus(ActionType.Action, actionId, targetId, true, true, null) <= 1;

    /// <summary>综合就绪检查 —— 自动查表选择目标（ACR 无指定目标时使用）</summary>
    public static bool IsActionReady(uint actionId)
        => IsActionReady(actionId, ResolveTargetByActionTable(actionId));

    /// <summary>根据 Action 表 CanTarget* 字段自动选择检查目标</summary>
    private static ulong ResolveTargetByActionTable(uint actionId)
    {
        var row = GetActionRow(actionId);
        if (row == null) return 0xE000_0000;

        var action = row.Value;

        // 地面放置技能：不需要具体目标
        if (action.TargetArea) return 0xE000_0000;

        // 纯攻击技能：用当前目标（敌人）
        if (action.CanTargetHostile && !action.CanTargetParty && !action.CanTargetAlly && !action.CanTargetSelf)
            return Data.Target.Current?.GameObjectID ?? 0xE000_0000;

        // 友方技能（治疗/增益）：用自己作为检查目标（自己永远是合法的友方目标）
        if (action.CanTargetSelf || action.CanTargetParty || action.CanTargetAlly)
            return Data.Me.Object?.GameObjectID ?? 0xE000_0000;

        // 混合技能（既能打敌又能奶人）：用当前目标
        if (action.CanTargetHostile)
            return Data.Target.Current?.GameObjectID ?? 0xE000_0000;

        return 0xE000_0000;
    }

    /// <summary>技能剩余冷却毫秒（0 表示已冷却好）</summary>
    public static unsafe float GetCooldownRemaining(uint spellId)
    {
        var recastGroup = ActionManager.Instance()->GetRecastGroup((int)ActionType.Action, spellId);
        var detail = ActionManager.Instance()->GetRecastGroupDetail(recastGroup);
        if (detail == null || !detail->IsActive) return 0;
        return Math.Max(0, detail->Total - detail->Elapsed) * 1000f;
    }

    /// <summary>充能技能最大层数</summary>
    public static unsafe int GetMaxCharges(uint spellId)
        => ActionManager.GetMaxCharges(spellId, 0);

    /// <summary>充能技能当前层数</summary>
    public static unsafe int GetCharges(uint spellId)
        => (int)ActionManager.Instance()->GetCurrentCharges(spellId);

    /// <summary>充能技能距下次充能的毫秒数</summary>
    public static unsafe float GetChargeCooldown(uint spellId)
    {
        var currentCharges = ActionManager.Instance()->GetCurrentCharges(spellId);
        var maxCharges = ActionManager.GetMaxCharges(spellId, 0);
        if (currentCharges >= maxCharges) return 0;
        return GetCooldownRemaining(spellId);
    }

    /// <summary>目标是否在技能射程内</summary>
    public static bool IsInRange(uint id, IGameObject? target)
    {
        if (target == null) return false;
        var distance = Data.Me.DistanceToObject2D(target);
        var range = GetActionRow(id)?.Range ?? 25f;
        return distance <= range;
    }

    /// <summary>从 Lumina Action 表中获取技能行数据</summary>
    public static unsafe Lumina.Excel.Sheets.Action? GetActionRow(uint id)
        => DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRow(id);
}
