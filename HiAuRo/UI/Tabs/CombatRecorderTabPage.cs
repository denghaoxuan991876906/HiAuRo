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

    public CombatRecorderTabPage(PluginConfig config, Action saveConfig)
        : base("战斗录制", "combat_recording", IconHelper.Icons.Clock)
    {
        _config = config;
        _saveConfig = saveConfig;
    }

    public override void DrawContent()
    {
        ImGui.Spacing();

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
            var rec = CombatRecorder.Instance;
            ImGui.TextColored(Theme.Colors.TextSecondary, $"状态: {(rec.IsRecording ? "录制中" : "待机")}");
            ImGui.TextColored(Theme.Colors.TextTertiary, $"Session: {(string.IsNullOrEmpty(rec.SessionId) ? "-" : rec.SessionId)}");
            ImGui.TextColored(Theme.Colors.TextTertiary, $"文件: {(string.IsNullOrEmpty(rec.CurrentLogPath) ? "-" : rec.CurrentLogPath)}");

            ImGui.Spacing();
            if (CL.DefaultButton("打开日志目录", new Vector2(120, 0)))
                rec.OpenLogDirectory();

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
}
