using System.Numerics;
using HiAuRo.Infrastructure;
using HiAuRo.ImGuiLib;
using HiAuRo.Recording;
using CL = HiAuRo.ImGuiLib.ComponentLibrary;

namespace HiAuRo.UI.Tabs;

public sealed class CombatRecorderTabPage : TabPageBase
{
    private readonly PluginConfig _config;
    private readonly Action _saveConfig;
    private string _trackedSkillInput = "";

    public CombatRecorderTabPage(PluginConfig config, Action saveConfig)
        : base("战斗录制", "combat_recording", IconHelper.Icons.Clock)
    {
        _config = config;
        _saveConfig = saveConfig;
    }

    public override void DrawContent()
    {
        ImGui.Spacing();
        var currentJob = HiAuRo.Data.Me.ClassJob;
        if (!_config.CombatRecorderTrackedSkillsByJob.TryGetValue(currentJob, out var trackedSkillsForJob))
        {
            trackedSkillsForJob = [];
            _config.CombatRecorderTrackedSkillsByJob[currentJob] = trackedSkillsForJob;
        }

        CL.Card("Combat Recorder", () =>
        {
            var enabled = _config.CombatRecorderEnabled;
            if (CL.Switch("combat_rec_enabled", "启用录制", ref enabled))
            {
                _config.CombatRecorderEnabled = enabled;
                _saveConfig();
            }

            var source = (int)_config.CombatRecorderSourceMode;
            var sourceNames = new[] { "玩家操作", "ACR", "未知" };
            if (CL.Select("combat_rec_source", "记录来源", ref source, sourceNames))
            {
                _config.CombatRecorderSourceMode = (CombatRecorderSourceMode)source;
                _saveConfig();
            }

            var onlyCombat = _config.CombatRecorderOnlyInCombat;
            var onlyJob = _config.CombatRecorderOnlyCurrentJob;
            var includePrev = _config.CombatRecorderIncludePreviousFrame;
            var includeBuffs = _config.CombatRecorderIncludeBuffDetails;
            var includeResolvers = _config.CombatRecorderIncludeResolverResults;
            var changed = false;

            changed |= CL.Switch("combat_rec_only_combat", "仅战斗中录制", ref onlyCombat);
            changed |= CL.Switch("combat_rec_only_job", "仅当前职业录制", ref onlyJob);
            changed |= CL.Switch("combat_rec_prev", "记录上一帧", ref includePrev);
            changed |= CL.Switch("combat_rec_buffs", "记录 Buff 明细", ref includeBuffs);
            changed |= CL.Switch("combat_rec_resolvers", "记录 resolver 结果", ref includeResolvers);

            if (changed)
            {
                _config.CombatRecorderOnlyInCombat = onlyCombat;
                _config.CombatRecorderOnlyCurrentJob = onlyJob;
                _config.CombatRecorderIncludePreviousFrame = includePrev;
                _config.CombatRecorderIncludeBuffDetails = includeBuffs;
                _config.CombatRecorderIncludeResolverResults = includeResolvers;
                _saveConfig();
            }

            var interval = Math.Clamp(_config.CombatRecorderSampleIntervalMs, 30, 50);
            if (CL.SliderInt("combat_rec_interval", "缓存间隔(ms)", ref interval, 30, 50))
            {
                _config.CombatRecorderSampleIntervalMs = interval;
                _saveConfig();
            }

            var keepDays = Math.Max(1, _config.CombatRecorderKeepDays);
            if (CL.InputNumber("combat_rec_keep_days", "日志保留天数", ref keepDays, 1))
            {
                _config.CombatRecorderKeepDays = keepDays;
                _saveConfig();
            }

            ImGui.Spacing();
            ComponentLibrary.SectionHeader("自定义技能");
            ImGui.TextColored(Theme.Colors.TextSecondary, $"当前职业 {currentJob} 的技能会额外写入技能ID、技能名、剩余CD、当前层数与最大层数。");

            CL.InputText("combat_rec_tracked_skill_input", "技能ID", ref _trackedSkillInput, 16, 120f);
            ImGui.SameLine();
            if (CL.PrimaryButton("添加技能", new Vector2(90, 0)) && TryAddTrackedSkill(_trackedSkillInput, currentJob))
            {
                _trackedSkillInput = "";
                _saveConfig();
            }

            if (trackedSkillsForJob.Count == 0)
            {
                ImGui.TextDisabled("暂无自定义技能");
            }
            else if (ImGui.BeginTable("##combat_rec_tracked_skills", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("技能ID", ImGuiTableColumnFlags.WidthFixed, 80f);
                ImGui.TableSetupColumn("技能名", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("当前值", ImGuiTableColumnFlags.WidthFixed, 180f);
                ImGui.TableSetupColumn("操作", ImGuiTableColumnFlags.WidthFixed, 70f);
                ImGui.TableHeadersRow();

                for (var i = 0; i < trackedSkillsForJob.Count; i++)
                {
                    var actionId = trackedSkillsForJob[i];
                    var actionName = actionId == 0 ? "-" : OmenTools.Interop.Game.Lumina.LuminaWrapper.GetActionName(actionId);
                    var cooldown = HiAuRo.ACR.SpellHelper.GetCooldownRemaining(actionId);
                    var charges = HiAuRo.ACR.SpellHelper.GetCharges(actionId);
                    var maxCharges = HiAuRo.ACR.SpellHelper.GetMaxCharges(actionId);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.Text(actionId.ToString());
                    ImGui.TableNextColumn(); ImGui.Text(string.IsNullOrWhiteSpace(actionName) ? "-" : actionName);
                    ImGui.TableNextColumn(); ImGui.Text($"CD {cooldown:F0}ms | 层数 {charges}/{maxCharges}");
                    ImGui.TableNextColumn();
                    if (CL.DangerButton($"删除##combat_rec_tracked_skill_{i}", new Vector2(56, 0)))
                    {
                        trackedSkillsForJob.RemoveAt(i);
                        _saveConfig();
                        break;
                    }
                }

                ImGui.EndTable();
            }

            ImGui.Spacing();
            var rec = CombatRecorder.Instance;
            ImGui.TextColored(Theme.Colors.TextSecondary, $"状态: {(rec.IsRecording ? "录制中" : "待机")}");
            ImGui.TextColored(Theme.Colors.TextTertiary, $"Session: {(string.IsNullOrEmpty(rec.SessionId) ? "-" : rec.SessionId)}");
            ImGui.TextColored(Theme.Colors.TextTertiary, $"文件: {(string.IsNullOrEmpty(rec.CurrentLogPath) ? "-" : rec.CurrentLogPath)}");
            ImGui.TextColored(Theme.Colors.TextTertiary, $"目录: {rec.LogRoot}");

            ImGui.Spacing();
            if (CL.DefaultButton("打开日志目录", new Vector2(120, 0)))
                rec.OpenLogDirectory();

            ImGui.SameLine();
            if (CL.PrimaryButton("新录制", new Vector2(90, 0)))
            {
                rec.StartNewSession();
                DService.Instance().Chat.Print("[HiAuRo] Combat Recorder 已切换到新的录制文件");
            }

            ImGui.SameLine();
            if (CL.PrimaryButton("打开日志阅读器", new Vector2(120, 0)))
                MainWindow.OpenEditor(CombatRecorderViewerPageConfig.FileName);

            ImGui.SameLine();
            if (CL.DangerButton("清理旧日志", new Vector2(120, 0)))
            {
                var deleted = rec.ClearOldLogs(_config.CombatRecorderKeepDays);
                DService.Instance().Chat.Print($"[HiAuRo] Combat Recorder 已清理 {deleted} 个旧日志");
            }
        });
    }

    private bool TryAddTrackedSkill(string rawInput, uint currentJob)
    {
        if (!uint.TryParse(rawInput.Trim(), out var actionId) || actionId == 0)
            return false;

        if (!_config.CombatRecorderTrackedSkillsByJob.TryGetValue(currentJob, out var trackedSkills))
        {
            trackedSkills = [];
            _config.CombatRecorderTrackedSkillsByJob[currentJob] = trackedSkills;
        }

        if (trackedSkills.Any(x => x == actionId))
            return false;

        trackedSkills.Add(actionId);
        return true;
    }
}
