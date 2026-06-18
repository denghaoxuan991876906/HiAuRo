using System.Numerics;
using HiAuRo.ACR;
using HiAuRo.ImGuiLib;
using HiAuRo.Infrastructure;
using HiAuRo.Runtime;

namespace HiAuRo.UI.Tabs;

public sealed class DebugTabPage : TabPageBase
{
    public DebugTabPage() : base("调试", "debug", IconHelper.Icons.Bug) { }

    public override void DrawContent()
    {
        ComponentLibrary.SectionHeader("运行时信息");
        if (ComponentLibrary.PrimaryButton("重载 ACR"))
            ACRLifecycle.Runner.ResetState();
        ImGui.SameLine();
        if (ComponentLibrary.DangerButton("清空协程"))
            Coroutine.Instance.Clear();
        ImGui.Spacing();
        ImGui.Text($"ACR: {ACRLifecycle.CurrentAcrName}");
        ImGui.Text($"SpellQueue: {ACRLifecycle.Runner?.SpellQueue.QueueSize ?? 0}");
        ImGui.Text($"BattleData: HP_GCD={AIRunner.BattleData.HighPrioritySlots_GCD.Count} HP_OGCD={AIRunner.BattleData.HighPrioritySlots_OffGCD.Count}");

        // --- AuraHelper ---
        ComponentLibrary.Collapsible("AuraHelper", () =>
        {
            ImGui.Text("HasAura  buffId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##AuraHasAura_BuffId", ref _dbgAuraHasAura_BuffId, 0);
            ImGui.SameLine(); ImGui.RadioButton("自己##AuraHasAura_T", ref _dbgAuraHasAura_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##AuraHasAura_T", ref _dbgAuraHasAura_Target, 1);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##AuraHasAura"))
            {
                var t = DbgTgt(_dbgAuraHasAura_Target);
                _dbgAuraHasAura_Result = $"→ HasAura: {AuraHelper.HasAura(t, (uint)_dbgAuraHasAura_BuffId)}";
            }
            ImGui.SameLine(); ImGui.Text(_dbgAuraHasAura_Result);

            ImGui.Text("HasAnyAura  buffIds(逗号分隔)");
            ImGui.SameLine(); ImGui.SetNextItemWidth(120); ImGui.InputText("##AuraHasAnyAura_Ids", ref _dbgAuraHasAnyAura_BuffIds, 128);
            ImGui.SameLine(); ImGui.RadioButton("自己##AuraHasAnyAura_T", ref _dbgAuraHasAnyAura_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##AuraHasAnyAura_T", ref _dbgAuraHasAnyAura_Target, 1);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##AuraHasAnyAura"))
            {
                var t = DbgTgt(_dbgAuraHasAnyAura_Target);
                var ids = _dbgAuraHasAnyAura_BuffIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => uint.TryParse(s.Trim(), out var id) ? id : 0).ToArray();
                _dbgAuraHasAnyAura_Result = $"→ HasAnyAura: {AuraHelper.HasAnyAura(t, ids)}";
            }
            ImGui.SameLine(); ImGui.Text(_dbgAuraHasAnyAura_Result);

            ImGui.Text("GetAuraTimeLeft  buffId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##AuraGetTimeLeft_BuffId", ref _dbgAuraGetTimeLeft_BuffId, 0);
            ImGui.SameLine(); ImGui.RadioButton("自己##AuraGetTimeLeft_T", ref _dbgAuraGetTimeLeft_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##AuraGetTimeLeft_T", ref _dbgAuraGetTimeLeft_Target, 1);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##AuraGetTimeLeft"))
            {
                var t = DbgTgt(_dbgAuraGetTimeLeft_Target);
                _dbgAuraGetTimeLeft_Result = $"→ 剩余: {AuraHelper.GetAuraTimeLeft(t, (uint)_dbgAuraGetTimeLeft_BuffId):F0}ms";
            }
            ImGui.SameLine(); ImGui.Text(_dbgAuraGetTimeLeft_Result);

            ImGui.Text("HasSelfAura  buffId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##AuraHasSelfAura_BuffId", ref _dbgAuraHasSelfAura_BuffId, 0);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##AuraHasSelfAura"))
                _dbgAuraHasSelfAura_Result = $"→ HasSelfAura: {AuraHelper.HasSelfAura((uint)_dbgAuraHasSelfAura_BuffId)}";
            ImGui.SameLine(); ImGui.Text(_dbgAuraHasSelfAura_Result);

            ImGui.Text("HasTargetAura  buffId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##AuraHasTargetAura_BuffId", ref _dbgAuraHasTargetAura_BuffId, 0);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##AuraHasTargetAura"))
                _dbgAuraHasTargetAura_Result = $"→ HasTargetAura: {AuraHelper.HasTargetAura((uint)_dbgAuraHasTargetAura_BuffId)}";
            ImGui.SameLine(); ImGui.Text(_dbgAuraHasTargetAura_Result);
        });

        // --- ComboHelper ---
        ComponentLibrary.Collapsible("ComboHelper", () =>
        {
            ImGui.TextColored(Theme.Colors.TextTertiary, $"LastComboSpellId: {ComboHelper.LastComboSpellId}  ComboTimer: {ComboHelper.ComboTimer:F1}s  LastSpellId: {ComboHelper.LastSpellId}");

            ImGui.Text("WasLastCombo  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##ComboWasLast_SpellId", ref _dbgComboWasLastCombo_SpellId, 0);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##ComboWasLast"))
                _dbgComboWasLastCombo_Result = $"→ WasLastCombo: {ComboHelper.WasLastCombo((uint)_dbgComboWasLastCombo_SpellId)}";
            ImGui.SameLine(); ImGui.Text(_dbgComboWasLastCombo_Result);

            ImGui.Text("ComboInWindow  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##ComboInWindow_SpellId", ref _dbgComboComboInWindow_SpellId, 0);
            ImGui.SameLine(); ImGui.Text("windowMs");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##ComboInWindow_WindowMs", ref _dbgComboComboInWindow_WindowMs, 0);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##ComboInWindow"))
                _dbgComboComboInWindow_Result = $"→ ComboInWindow: {ComboHelper.ComboInWindow((uint)_dbgComboComboInWindow_SpellId, _dbgComboComboInWindow_WindowMs)}";
            ImGui.SameLine(); ImGui.Text(_dbgComboComboInWindow_Result);

            ImGui.Text("ComboAboutToExpire  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##ComboAboutToExpire_SpellId", ref _dbgComboComboAboutToExpire_SpellId, 0);
            ImGui.SameLine(); ImGui.Text("withinMs");
            ImGui.SameLine(); ImGui.SetNextItemWidth(50); ImGui.InputInt("##ComboAboutToExpire_WithinMs", ref _dbgComboComboAboutToExpire_WithinMs, 0);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##ComboAboutToExpire"))
                _dbgComboComboAboutToExpire_Result = $"→ ComboAboutToExpire: {ComboHelper.ComboAboutToExpire((uint)_dbgComboComboAboutToExpire_SpellId, _dbgComboComboAboutToExpire_WithinMs)}";
            ImGui.SameLine(); ImGui.Text(_dbgComboComboAboutToExpire_Result);
        });

        // --- SpellHelper ---
        ComponentLibrary.Collapsible("SpellHelper", () =>
        {
            ImGui.Text("CanUseSpell  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellCanUse_Id", ref _dbgSpellCanUseSpell_Id, 0);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##SpellCanUse"))
                _dbgSpellCanUseSpell_Result = $"→ CanUseSpell: {SpellHelper.CanUseSpell((uint)_dbgSpellCanUseSpell_Id)}";
            ImGui.SameLine(); ImGui.Text(_dbgSpellCanUseSpell_Result);

            ImGui.Text("IsActionReady  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellIsActionReady_Id", ref _dbgSpellIsActionReady_Id, 0);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##SpellIsActionReady"))
                _dbgSpellIsActionReady_Result = $"→ IsActionReady: {SpellHelper.IsActionReady((uint)_dbgSpellIsActionReady_Id)}";
            ImGui.SameLine(); ImGui.Text(_dbgSpellIsActionReady_Result);

            ImGui.Text("GetCooldownRemaining  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellGetCd_Id", ref _dbgSpellGetCd_Id, 0);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##SpellGetCd"))
                _dbgSpellGetCd_Result = $"→ 剩余: {SpellHelper.GetCooldownRemaining((uint)_dbgSpellGetCd_Id):F0}ms";
            ImGui.SameLine(); ImGui.Text(_dbgSpellGetCd_Result);

            ImGui.Text("GetMaxCharges  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellGetMaxCharges_Id", ref _dbgSpellGetMaxCharges_Id, 0);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##SpellGetMaxCharges"))
                _dbgSpellGetMaxCharges_Result = $"→ 最大层数: {SpellHelper.GetMaxCharges((uint)_dbgSpellGetMaxCharges_Id)}";
            ImGui.SameLine(); ImGui.Text(_dbgSpellGetMaxCharges_Result);

            ImGui.Text("GetCharges  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellGetCharges_Id", ref _dbgSpellGetCharges_Id, 0);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##SpellGetCharges"))
                _dbgSpellGetCharges_Result = $"→ 当前层数: {SpellHelper.GetCharges((uint)_dbgSpellGetCharges_Id)}";
            ImGui.SameLine(); ImGui.Text(_dbgSpellGetCharges_Result);

            ImGui.Text("GetChargeCooldown  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellGetChargeCd_Id", ref _dbgSpellGetChargeCd_Id, 0);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##SpellGetChargeCd"))
                _dbgSpellGetChargeCd_Result = $"→ 充能剩余: {SpellHelper.GetChargeCooldown((uint)_dbgSpellGetChargeCd_Id):F0}ms";
            ImGui.SameLine(); ImGui.Text(_dbgSpellGetChargeCd_Result);

            ImGui.Text("IsInRange  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellIsInRange_Id", ref _dbgSpellIsInRange_Id, 0);
            ImGui.SameLine(); ImGui.RadioButton("自己##SpellIsInRange_T", ref _dbgSpellIsInRange_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##SpellIsInRange_T", ref _dbgSpellIsInRange_Target, 1);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##SpellIsInRange"))
                _dbgSpellIsInRange_Result = $"→ IsInRange: {SpellHelper.IsInRange((uint)_dbgSpellIsInRange_Id, DbgTgt(_dbgSpellIsInRange_Target))}";
            ImGui.SameLine(); ImGui.Text(_dbgSpellIsInRange_Result);
        });

        // --- SpellHistoryHelper ---
        ComponentLibrary.Collapsible("SpellHistoryHelper", () =>
        {
            ImGui.Text("RecentlyUsed  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellHistRecentlyUsed_Id", ref _dbgSpellHistRecentlyUsed_Id, 0);
            ImGui.SameLine(); ImGui.Text("withinMs");
            ImGui.SameLine(); ImGui.SetNextItemWidth(50); ImGui.InputInt("##SpellHistRecentlyUsed_WithinMs", ref _dbgSpellHistRecentlyUsed_WithinMs, 0);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##SpellHistRecentlyUsed"))
                _dbgSpellHistRecentlyUsed_Result = $"→ RecentlyUsed: {SpellHistoryHelper.RecentlyUsed((uint)_dbgSpellHistRecentlyUsed_Id, _dbgSpellHistRecentlyUsed_WithinMs)}";
            ImGui.SameLine(); ImGui.Text(_dbgSpellHistRecentlyUsed_Result);

            ImGui.Text("GetLastSpellTime  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellHistGetLastTime_Id", ref _dbgSpellHistGetLastTime_Id, 0);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##SpellHistGetLastTime"))
            {
                var t = SpellHistoryHelper.GetLastSpellTime((uint)_dbgSpellHistGetLastTime_Id);
                _dbgSpellHistGetLastTime_Result = t >= 0 ? $"→ 距上次: {t}ms" : "→ 从未使用";
            }
            ImGui.SameLine(); ImGui.Text(_dbgSpellHistGetLastTime_Result);

            ImGui.Text("GetGcdCountFromLastGcd");
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##SpellHistGcdCount"))
                _dbgSpellHistGcdCount_Result = $"→ GCD计数: {SpellHistoryHelper.GetGcdCountFromLastGcd()}";
            ImGui.SameLine(); ImGui.Text(_dbgSpellHistGcdCount_Result);

            ImGui.Text("RecordSpell  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##SpellHistRecord_Id", ref _dbgSpellHistRecord_Id, 0);
            ImGui.SameLine();
            if (ComponentLibrary.PrimaryButton("记录##SpellHistRecord")) SpellHistoryHelper.RecordSpell((uint)_dbgSpellHistRecord_Id);
            ImGui.SameLine();
            if (ComponentLibrary.PrimaryButton("记录GCD##SpellHistRecordGcd")) SpellHistoryHelper.RecordGcd();
            ImGui.SameLine();
            if (ComponentLibrary.DangerButton("重置历史##SpellHistReset")) SpellHistoryHelper.Reset();
        });

        // --- TargetHelper ---
        ComponentLibrary.Collapsible("TargetHelper", () =>
        {
            ImGui.Text("GetNearbyEnemyCount");
            ImGui.SameLine(); ImGui.RadioButton("自己##TgtGetNearby_T", ref _dbgTargetGetNearby_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##TgtGetNearby_T", ref _dbgTargetGetNearby_Target, 1);
            ImGui.SameLine(); ImGui.Text("range");
            ImGui.SameLine(); ImGui.SetNextItemWidth(50); ImGui.InputFloat("##TgtGetNearby_Range", ref _dbgTargetGetNearby_Range);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##TgtGetNearby"))
                _dbgTargetGetNearby_Result = $"→ 附近敌人数: {TargetHelper.GetNearbyEnemyCount(DbgTgt(_dbgTargetGetNearby_Target), _dbgTargetGetNearby_Range)}";
            ImGui.SameLine(); ImGui.Text(_dbgTargetGetNearby_Result);

            ImGui.Text("IsBehind");
            ImGui.SameLine(); ImGui.RadioButton("自己##TgtIsBehind_T", ref _dbgTargetIsBehind_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##TgtIsBehind_T", ref _dbgTargetIsBehind_Target, 1);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##TgtIsBehind"))
                _dbgTargetIsBehind_Result = $"→ IsBehind: {TargetHelper.IsBehind(DbgTgt(_dbgTargetIsBehind_Target))}";
            ImGui.SameLine(); ImGui.Text(_dbgTargetIsBehind_Result);

            ImGui.Text("IsFlanking");
            ImGui.SameLine(); ImGui.RadioButton("自己##TgtIsFlanking_T", ref _dbgTargetIsFlanking_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##TgtIsFlanking_T", ref _dbgTargetIsFlanking_Target, 1);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##TgtIsFlanking"))
                _dbgTargetIsFlanking_Result = $"→ IsFlanking: {TargetHelper.IsFlanking(DbgTgt(_dbgTargetIsFlanking_Target))}";
            ImGui.SameLine(); ImGui.Text(_dbgTargetIsFlanking_Result);

            ImGui.Text("TargetCastingIsBossAOE");
            ImGui.SameLine(); ImGui.RadioButton("自己##TgtCastingAOE_T", ref _dbgTargetCastingAOE_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##TgtCastingAOE_T", ref _dbgTargetCastingAOE_Target, 1);
            ImGui.SameLine(); ImGui.Text("thresholdMs");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##TgtCastingAOE_Threshold", ref _dbgTargetCastingAOE_Threshold, 0);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##TgtCastingAOE"))
                _dbgTargetCastingAOE_Result = $"→ IsBossAOE: {TargetHelper.TargetCastingIsBossAOE(DbgTgt(_dbgTargetCastingAOE_Target) as IBattleChara, _dbgTargetCastingAOE_Threshold)}";
            ImGui.SameLine(); ImGui.Text(_dbgTargetCastingAOE_Result);

            ImGui.Text("GetCastingSpellTiming");
            ImGui.SameLine(); ImGui.RadioButton("自己##TgtCastingTime_T", ref _dbgTargetCastingTime_Target, 0);
            ImGui.SameLine(); ImGui.RadioButton("目标##TgtCastingTime_T", ref _dbgTargetCastingTime_Target, 1);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##TgtCastingTime"))
                _dbgTargetCastingTime_Result = $"→ 读条剩余: {TargetHelper.GetCastingSpellTiming(DbgTgt(_dbgTargetCastingTime_Target) as IBattleChara):F0}ms";
            ImGui.SameLine(); ImGui.Text(_dbgTargetCastingTime_Result);

            ImGui.Text("GetMostCanTargetObjects  spellId");
            ImGui.SameLine(); ImGui.SetNextItemWidth(60); ImGui.InputInt("##TgtMostCanTarget_SpellId", ref _dbgTargetMostCanTarget_SpellId, 0);
            ImGui.SameLine(); ImGui.Text("min");
            ImGui.SameLine(); ImGui.SetNextItemWidth(40); ImGui.InputInt("##TgtMostCanTarget_MinCount", ref _dbgTargetMostCanTarget_MinCount, 0);
            ImGui.SameLine(); ImGui.Text("range");
            ImGui.SameLine(); ImGui.SetNextItemWidth(50); ImGui.InputFloat("##TgtMostCanTarget_Range", ref _dbgTargetMostCanTarget_Range);
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("测试##TgtMostCanTarget"))
            {
                var best = TargetHelper.GetMostCanTargetObjects((uint)_dbgTargetMostCanTarget_SpellId, _dbgTargetMostCanTarget_MinCount, _dbgTargetMostCanTarget_Range);
                _dbgTargetMostCanTarget_Result = best != null ? $"→ 最佳目标: {best.Name}" : "→ 未找到(不足最小敌人数)";
            }
            ImGui.SameLine(); ImGui.Text(_dbgTargetMostCanTarget_Result);
        });
    }

    // AuraHelper
    private int _dbgAuraHasAura_Target;
    private int _dbgAuraHasAura_BuffId;
    private string _dbgAuraHasAura_Result = "";

    private int _dbgAuraHasAnyAura_Target;
    private string _dbgAuraHasAnyAura_BuffIds = "";
    private string _dbgAuraHasAnyAura_Result = "";

    private int _dbgAuraGetTimeLeft_Target;
    private int _dbgAuraGetTimeLeft_BuffId;
    private string _dbgAuraGetTimeLeft_Result = "";

    private int _dbgAuraHasSelfAura_BuffId;
    private string _dbgAuraHasSelfAura_Result = "";

    private int _dbgAuraHasTargetAura_BuffId;
    private string _dbgAuraHasTargetAura_Result = "";

    // ComboHelper
    private int _dbgComboWasLastCombo_SpellId;
    private string _dbgComboWasLastCombo_Result = "";

    private int _dbgComboComboInWindow_SpellId;
    private int _dbgComboComboInWindow_WindowMs = 15000;
    private string _dbgComboComboInWindow_Result = "";

    private int _dbgComboComboAboutToExpire_SpellId;
    private int _dbgComboComboAboutToExpire_WithinMs = 500;
    private string _dbgComboComboAboutToExpire_Result = "";

    // SpellHelper
    private int _dbgSpellCanUseSpell_Id;
    private string _dbgSpellCanUseSpell_Result = "";

    private int _dbgSpellIsActionReady_Id;
    private string _dbgSpellIsActionReady_Result = "";

    private int _dbgSpellGetCd_Id;
    private string _dbgSpellGetCd_Result = "";

    private int _dbgSpellGetMaxCharges_Id;
    private string _dbgSpellGetMaxCharges_Result = "";

    private int _dbgSpellGetCharges_Id;
    private string _dbgSpellGetCharges_Result = "";

    private int _dbgSpellGetChargeCd_Id;
    private string _dbgSpellGetChargeCd_Result = "";

    private int _dbgSpellIsInRange_Id;
    private int _dbgSpellIsInRange_Target;
    private string _dbgSpellIsInRange_Result = "";

    // SpellHistoryHelper
    private int _dbgSpellHistRecentlyUsed_Id;
    private int _dbgSpellHistRecentlyUsed_WithinMs = 500;
    private string _dbgSpellHistRecentlyUsed_Result = "";

    private int _dbgSpellHistGetLastTime_Id;
    private string _dbgSpellHistGetLastTime_Result = "";

    private string _dbgSpellHistGcdCount_Result = "";

    private int _dbgSpellHistRecord_Id;

    // TargetHelper
    private int _dbgTargetGetNearby_Target;
    private float _dbgTargetGetNearby_Range = 5f;
    private string _dbgTargetGetNearby_Result = "";

    private int _dbgTargetIsBehind_Target;
    private string _dbgTargetIsBehind_Result = "";

    private int _dbgTargetIsFlanking_Target;
    private string _dbgTargetIsFlanking_Result = "";

    private int _dbgTargetCastingAOE_Target;
    private int _dbgTargetCastingAOE_Threshold = 3000;
    private string _dbgTargetCastingAOE_Result = "";

    private int _dbgTargetCastingTime_Target;
    private string _dbgTargetCastingTime_Result = "";

    private int _dbgTargetMostCanTarget_SpellId;
    private int _dbgTargetMostCanTarget_MinCount = 1;
    private float _dbgTargetMostCanTarget_Range = 5f;
    private string _dbgTargetMostCanTarget_Result = "";

    private static IGameObject? DbgTgt(int sel) => sel == 0 ? Data.Me.Object : Data.Target.Current;
}
