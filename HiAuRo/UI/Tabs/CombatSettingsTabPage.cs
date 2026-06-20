using HiAuRo.Infrastructure;
using HiAuRo.ImGuiLib;
using HiAuRo.Rendering;
using HiAuRo.Runtime;
using HiAuRo.Runtime.CombatEnhancements;
using System.Numerics;
using CL = HiAuRo.ImGuiLib.ComponentLibrary;

namespace HiAuRo.UI.Tabs;

public sealed class CombatSettingsTabPage : TabPageBase
{
    private readonly PluginConfig _config;
    private readonly Action _saveConfig;
    private string _dashActionInput = "";

    public CombatSettingsTabPage(PluginConfig config, Action saveConfig)
        : base("战斗设置", "combat_settings", IconHelper.Icons.Settings)
    {
        _config = config;
        _saveConfig = saveConfig;
    }

    public override void DrawContent()
    {
        ImGui.Spacing();
        CL.Card("基础参数", () =>
        {
            var aq = _config.ActionQueueInMs;
            var maxAb = _config.MaxAbilityTimesInGcd;
            var abInterval = _config.AbilityIntervalMs;
            var aoe = _config.AoeCount;
            var debug = _config.DebugEnabled;
            var selfAxisRole = (int)_config.SelfAxisRole;
            var changed = false;

            CL.FormRow("技能队列窗口 (ms)", () =>
            {
                ImGui.SetNextItemWidth(120);
                changed |= ImGui.InputInt("##combat_aq_window", ref aq, 50, 100);
            });

            CL.FormRow("GCD 内能力技次数", () =>
            {
                ImGui.SetNextItemWidth(120);
                changed |= ImGui.InputInt("##combat_max_ability_times", ref maxAb);
            });

            CL.FormRow("能力技间隔 (ms)", () =>
            {
                ImGui.SetNextItemWidth(120);
                changed |= ImGui.InputInt("##combat_ability_interval", ref abInterval, 50, 100);
            });

            CL.FormRow("AOE 判定人数", () =>
            {
                ImGui.SetNextItemWidth(120);
                changed |= ImGui.InputInt("##combat_aoe_count", ref aoe);
            });

            CL.FormRow("Debug 日志", () =>
            {
                changed |= CL.Switch("combat_debug_log", ref debug);
            });

            var selfAxisRoleNames = Enum.GetNames<AxisRole>();
            if (CL.Select("combat_self_axis_role", "全轴自身职能", ref selfAxisRole, selfAxisRoleNames))
                changed = true;

            if (changed)
            {
                _config.ActionQueueInMs = aq;
                _config.MaxAbilityTimesInGcd = maxAb;
                _config.AbilityIntervalMs = abInterval;
                _config.AoeCount = aoe;
                _config.DebugEnabled = debug;
                _config.SelfAxisRole = (AxisRole)selfAxisRole;
                _saveConfig();
            }
        });
        CL.Card("目标选择器", () =>
        {
            var tgtChanged = false;

            var tgtEnabled = _config.TargetSelectorEnabled;
            tgtChanged |= CL.Switch("tgtEnabled", "启用目标选择器", ref tgtEnabled);

            var tgtCountdown = _config.TargetAutoSelectOnCountdown;
            tgtChanged |= CL.Switch("tgtCountdown", "倒计时时自动选择目标（仅副本生效）", ref tgtCountdown);

            var tgtMode = (int)_config.TargetSelectMode;
            var tgtModeNames = Enum.GetNames<TargetSelectMode>();
            if (CL.Select("TargetSelectMode", "选择逻辑", ref tgtMode, tgtModeNames))
            {
                _config.TargetSelectMode = (TargetSelectMode)tgtMode;
                tgtChanged = true;
            }

            var tgtRange = _config.TargetSearchRange;
            tgtChanged |= CL.Slider("tgt_range", "索敌范围", ref tgtRange, 5f, 50f, "%.0f");
            ImGui.SameLine();
            ImGui.TextColored(Theme.Colors.TextSecondary, "yalms");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var tgtKeep = _config.TargetKeepCurrent;
            tgtChanged |= CL.Switch("tgtKeep", "有目标时不自动切换目标", ref tgtKeep);

            var tgtNoDeath = _config.TargetDisableOnDeath;
            tgtChanged |= CL.Switch("tgtNoDeath", "死亡自动禁用目标选择器", ref tgtNoDeath);

            var tgtNoDummies = _config.TargetExcludeDummies;
            tgtChanged |= CL.Switch("tgtNoDummies", "排除木人", ref tgtNoDummies);

            var tgtNoNonHostile = _config.TargetExcludeNonHostile;
            tgtChanged |= CL.Switch("tgtNoNonHostile", "排除非敌对目标", ref tgtNoNonHostile);

            var tgtPreferAggro = _config.TargetPreferAggroMarked;
            tgtChanged |= CL.Switch("tgtPreferAggro", "优先选中带有攻击头标的敌人", ref tgtPreferAggro);

            if (tgtChanged)
            {
                _config.TargetSelectorEnabled = tgtEnabled;
                _config.TargetAutoSelectOnCountdown = tgtCountdown;
                _config.TargetSearchRange = tgtRange;
                _config.TargetKeepCurrent = tgtKeep;
                _config.TargetDisableOnDeath = tgtNoDeath;
                _config.TargetExcludeDummies = tgtNoDummies;
                _config.TargetExcludeNonHostile = tgtNoNonHostile;
                _config.TargetPreferAggroMarked = tgtPreferAggro;
                _saveConfig();
            }
        });
        CL.Card("战斗增强", () =>
        {
            var enableSkillRangeExtension = _config.EnableSkillRangeExtension;
            var skillRangeExtension = _config.SkillRangeExtension;
            var enableAntiKnockback = _config.EnableAntiKnockback;
            var enableNoDashDisplacement = _config.EnableNoDashDisplacement;
            var noDashMode = (int)_config.NoDashDisplacementFilterMode;
            var enableAnimationLockClamp = _config.EnableAnimationLockClamp;
            var animationLockClampSeconds = _config.AnimationLockClampSeconds;
            var changed = false;

            changed |= CL.Switch("combat_enhance_range_ext", "启用技能距离扩展", ref enableSkillRangeExtension);

            CL.FormRow("技能距离扩展值", () =>
            {
                ImGui.SetNextItemWidth(120);
                changed |= ImGui.InputFloat("##combat_skill_range_extension", ref skillRangeExtension, 0.5f, 1.0f, "%.1f");
            });

            changed |= CL.Switch("combat_enhance_anti_knockback", "启用防击退", ref enableAntiKnockback);
            changed |= CL.Switch("combat_enhance_no_dash_displacement", "启用冲锋不位移", ref enableNoDashDisplacement);

            var noDashModeNames = Enum.GetNames<NoDashDisplacementFilterMode>();
            if (CL.Select("combat_no_dash_filter_mode", "冲锋不位移过滤模式", ref noDashMode, noDashModeNames))
                changed = true;

            CL.FormRow("冲锋技能列表", () =>
            {
                CL.InputText("dash_action_input", "Action ID", ref _dashActionInput, 16, 120f);
                ImGui.SameLine();
                if (CL.PrimaryButton("添加技能", new Vector2(90, 0))
                    && uint.TryParse(_dashActionInput.Trim(), out var actionId)
                    && NoDashDisplacementService.TryAddActionId(_config.NoDashDisplacementActionIds, actionId))
                {
                    _dashActionInput = "";
                    _saveConfig();
                    CombatEnhancementManager.Instance.SyncFromConfig(_config);
                }
            });

            for (var i = 0; i < _config.NoDashDisplacementActionIds.Count; i++)
            {
                var actionId = _config.NoDashDisplacementActionIds[i];
                ImGui.Text(actionId.ToString());
                ImGui.SameLine();
                if (CL.DangerButton($"删除##dash_{i}", new Vector2(56, 0)))
                {
                    _config.NoDashDisplacementActionIds.RemoveAt(i);
                    _saveConfig();
                    CombatEnhancementManager.Instance.SyncFromConfig(_config);
                    break;
                }
            }

            changed |= CL.Switch("combat_enhance_animlock_clamp", "启用动画锁清理", ref enableAnimationLockClamp);

            CL.FormRow("动画锁清理值 (秒)", () =>
            {
                ImGui.SetNextItemWidth(120);
                changed |= ImGui.InputFloat("##combat_animlock_clamp_seconds", ref animationLockClampSeconds, 0.1f, 0.5f, "%.1f");
            });

            if (changed)
            {
                _config.EnableSkillRangeExtension = enableSkillRangeExtension;
                _config.SkillRangeExtension = skillRangeExtension;
                _config.EnableAntiKnockback = enableAntiKnockback;
                _config.EnableNoDashDisplacement = enableNoDashDisplacement;
                _config.NoDashDisplacementFilterMode = (NoDashDisplacementFilterMode)noDashMode;
                _config.EnableAnimationLockClamp = enableAnimationLockClamp;
                _config.AnimationLockClampSeconds = animationLockClampSeconds;
                _saveConfig();
                CombatEnhancementManager.Instance.SyncFromConfig(_config);
            }
        });
        CL.Card("身位显示", () =>
        {
            var showPos = _config.ShowPositional;
            var showHitbox = _config.ShowTargetHitbox;
            var showAutoAttackRange = _config.ShowAutoAttackRange;
            var changed = false;

            changed |= CL.Switch("combat_show_positional", "显示身位提示", ref showPos);
            changed |= CL.Switch("combat_show_target_hitbox", "显示目标可攻击范围", ref showHitbox);
            changed |= CL.Switch("combat_show_auto_attack_range", "显示自动攻击范围", ref showAutoAttackRange);

            if (changed)
            {
                _config.ShowPositional = showPos;
                _config.ShowTargetHitbox = showHitbox;
                _config.ShowAutoAttackRange = showAutoAttackRange;
                if (!showPos)
                    PositionalVfx.Clear();

                _saveConfig();
            }
        });
    }
}
