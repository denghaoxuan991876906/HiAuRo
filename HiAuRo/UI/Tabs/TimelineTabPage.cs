using System.Linq;
using System.Numerics;
using HiAuRo.Execution;
using HiAuRo.FactAxis;
using HiAuRo.Infrastructure;
using HiAuRo.ImGuiLib;
using HiAuRo.Recording;
using HiAuRo.Runtime;
using OmenTools.OmenService;

namespace HiAuRo.UI.Tabs;

public sealed class TimelineTabPage : TabPageBase
{
    private readonly PluginConfig _config;
    private readonly Action _saveConfig;
    private readonly ScriptTabPage _scriptTab;
    private int _subTabIndex;

    private static readonly string[] SubTabNames = ["录制", "事实轴", "执行轴", "辅助轴", "脚本"];

    public TimelineTabPage(PluginConfig config, Action saveConfig)
        : base("录制", "recording", IconHelper.Icons.Clock)
    {
        _config = config;
        _saveConfig = saveConfig;
        _scriptTab = new ScriptTabPage(config, saveConfig);
    }

    public override void DrawContent()
    {
        ComponentLibrary.Tabs("timeline_sub", ref _subTabIndex, SubTabNames);

        switch (_subTabIndex)
        {
            case 0: DrawRecording(); break;
            case 1: DrawFactAxisTab(); break;
            case 2: DrawExecutionAxisTab(); break;
            case 3: DrawAssistAxisTab(); break;
            case 4: _scriptTab.DrawContent(); break;
        }
    }

    private void DrawRecording()
    {
        ImGui.Spacing();

        var recorder = EncounterRecorder.Instance;
        var config = PluginConfig.Instance;

        ComponentLibrary.SectionHeader("录制");
        {
            var enabled = config.RecordingEnabled;
            if (ComponentLibrary.Switch("recenabled", "启用副本录制", ref enabled))
            {
                config.RecordingEnabled = enabled;
                config.Save();

                if (!enabled && recorder.IsRecording)
                {
                    recorder.ForceStop();
                    Hi.Print("录制已停止");
                }
            }

            ImGui.Spacing();
            ComponentLibrary.Divider();

            var isRecording = recorder.IsRecording;
            if (isRecording)
            {
                var seconds = recorder.ElapsedSeconds;
                ComponentLibrary.StatusDot(true, $"录制中 ({seconds / 60:D2}:{seconds % 60:D2})", Theme.Colors.AccentRed);
                ImGui.Text($"文件名: {recorder.CurrentFileName}");
            }
            else
            {
                ComponentLibrary.StatusDot(false, "就绪");
            }
        }
        ComponentLibrary.SectionHeader("录制历史");
        {
            var files = recorder.GetRecordFiles();
            if (files.Length == 0)
            {
                ImGui.TextDisabled("暂无录制记录");
            }
            else
            {
                using var childBorder = new ImRaii.StyleDisposable();
                childBorder.Push(ImGuiStyleVar.ChildBorderSize, 1);
                ImGui.BeginChild("##RecordingList",
                    new Vector2(-1, 80), true);

                foreach (var (name, path) in files.Take(20))
                {
                    ImGui.Text(name);
                    ImGui.SameLine();
                    ImGui.TextDisabled($"({path})");
                }

                ImGui.EndChild();
            }

            ImGui.Spacing();
            if (ComponentLibrary.DefaultButton("打开录制目录"))
            {
                var dir = Path.Combine(
                    DService.Instance().PI.ConfigDirectory.FullName, "Recordings");
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", dir);
                }
                catch (Exception ex)
                {
                    DService.Instance().Log.Debug($"[Recording] 无法打开目录: {ex.Message}");
                }
            }
        }
    }

    private void DrawFactAxisTab()
    {
        var flags = _config.FactAxis;
        bool changed = false;

        ComponentLibrary.SectionHeader("事实轴");
        {
            if (ComponentLibrary.PrimaryButton("打开事实轴编辑器"))
                OpenEditor();

            ImGui.Spacing();

            var v1 = flags.Observe;
            changed |= ComponentLibrary.Switch("observe", "时间线观测", ref v1);
            flags.Observe = v1;

            var v2 = flags.QtControl;
            changed |= ComponentLibrary.Switch("qtControl", "QT 调控", ref v2);
            flags.QtControl = v2;
        }
        ComponentLibrary.SectionHeader("决策分配");
        {
            var v = flags.TeamMitigation;
            changed |= ComponentLibrary.Switch("teamMit", "团队减伤分配", ref v);
            flags.TeamMitigation = v;
        }
        {
            var v = flags.PersonalMitigation;
            changed |= ComponentLibrary.Switch("personalMit", "单人减伤分配", ref v);
            flags.PersonalMitigation = v;
        }
        {
            var v = flags.TeamHealing;
            changed |= ComponentLibrary.Switch("teamHeal", "团队治疗分配", ref v);
            flags.TeamHealing = v;
        }
        {
            var v = flags.ForceExecute;
            changed |= ComponentLibrary.Switch("forceExec", "技能强制释放", ref v);
            flags.ForceExecute = v;
        }
        ComponentLibrary.SectionHeader("移动控制");
        {
            var v = flags.MoveTo;
            changed |= ComponentLibrary.Switch("moveTo", "MoveTo", ref v);
            flags.MoveTo = v;
        }
        {
            var v = flags.TP;
            changed |= ComponentLibrary.Switch("tp", "TP", ref v);
            flags.TP = v;
        }
        {
            var v = flags.Hold;
            changed |= ComponentLibrary.Switch("hold", "Hold", ref v);
            flags.Hold = v;
        }

        ImGui.Spacing();

        var modes = new[] { "NavMesh", "TP", "NavMesh + TP兜底" };
        int modeIdx = (int)flags.MovementMode;
        if (ComponentLibrary.Select("moveMode", "移动模式", ref modeIdx, modes))
        {
            flags.MovementMode = (MovementMode)modeIdx;
            changed = true;
        }
        ComponentLibrary.SectionHeader("运行时状态");
        {
            var state = FactTimeline.Instance.State;
            if (state.IsRunning)
            {
                ComponentLibrary.StatusDot(true, $"运行中 — {state.TimelineName}");
                ImGui.Text($"副本: {state.TimelineName}");
                ImGui.Text($"阶段: {state.PhaseName} | {state.Status}");
                ImGui.Text($"时间: 阶段{state.PhaseTime:F1}s / 总{state.TotalTime:F1}s");
                if (state.CurrentEvent != null)
                    ImGui.Text($"当前事件: {state.CurrentEvent.Name}");
                var mode = ModeSwitch.CurrentMode;
                ImGui.Text($"模式: {mode}");
            }
            else
            {
                ComponentLibrary.StatusDot(false, "未启动");
            }
        }

    }

    private void DrawExecutionAxisTab()
    {
        var config = PluginConfig.Instance;
        var axis = ExecutionAxis.Instance;

        ComponentLibrary.SectionHeader("执行轴");
        {
            if (ComponentLibrary.PrimaryButton("打开 axflow 编辑器"))
                OpenEditor();
            ImGui.SameLine();
            if (ComponentLibrary.DefaultButton("打开通用编辑器"))
                OpenEditor();

            ImGui.Spacing();
            ComponentLibrary.Divider();

            if (axis.Initialized)
            {
                ComponentLibrary.StatusDot(axis.IsRunning, axis.IsRunning ? $"运行中 — {axis.TimelineName}" : "未启动");
                if (!string.IsNullOrEmpty(axis.TimelineName))
                    ImGui.Text($"名称: {axis.TimelineName}");

                var data = axis.TimelineData;
                if (data != null)
                {
                    var territory = GameState.TerritoryType;
                    ImGui.Text($"副本: {territory} (TerritoryTypeId: {data.TerritoryId})");
                    if (!string.IsNullOrEmpty(data.Author))
                        ImGui.Text($"作者: {data.Author}");
                    if (!string.IsNullOrEmpty(data.Guid))
                    {
                        ImGui.TextColored(Theme.Colors.TextTertiary, data.Guid);
                    }
                }
            }
            else
            {
                ImGui.TextDisabled("未加载执行轴");
            }
        }
        ComponentLibrary.SectionHeader("设置");
        {
            var axis2 = ExecutionAxis.Instance;
            var data = axis2.TimelineData;

            if (axis2.Initialized && data != null && data.ExposedVarNames is { Count: > 0 })
            {
                if (!string.IsNullOrEmpty(data.ExposedVarDesc))
                {
                    ImGui.TextColored(Theme.Colors.TextTertiary, data.ExposedVarDesc);
                    ImGui.Spacing();
                }
                foreach (var varName in data.ExposedVarNames)
                {
                    var val = axis2.Context.GetVariable(varName);
                    ImGui.Text(varName);
                    if (ImGui.RadioButton("开##" + varName, val != 0)) axis2.Context.SetVariable(varName, 1);
                    ImGui.SameLine();
                    if (ImGui.RadioButton("关##" + varName, val == 0)) axis2.Context.SetVariable(varName, 0);
                }

                ImGui.Spacing();
                ComponentLibrary.Divider();
            }

            var autoLoad = config.ExecutionAxisAutoLoad;
            if (ComponentLibrary.Switch("execAutoLoad", "进副本自动加载", ref autoLoad))
            {
                config.ExecutionAxisAutoLoad = autoLoad;
                axis.AutoLoadEnabled = autoLoad;
                _saveConfig();
            }

            ImGui.Spacing();

            var dir = Path.Combine(DService.Instance().PI.ConfigDirectory.FullName, "ExecutionTimelines");
            var files = Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.json").Select(f => Path.GetFileName(f)!).ToArray()
                : [];
            var fileNames = files.Length > 0 ? files : ["(无文件)"];

            int selectedIdx = 0;
            if (!string.IsNullOrEmpty(axis.TimelineName))
            {
                var match = fileNames.FirstOrDefault(f => f == $"{axis.TerritoryId}.json");
                if (match != null) selectedIdx = Array.IndexOf(fileNames, match);
            }
            if (selectedIdx < 0) selectedIdx = 0;

            if (ComponentLibrary.Select("execFile", "选择文件", ref selectedIdx, fileNames))
            {
                var chosen = fileNames[selectedIdx];
                if (chosen != "(无文件)")
                {
                    var path = Path.Combine(dir!, chosen);
                    axis.LoadFromFile(path);
                }
            }

            ImGui.Spacing();

            if (axis.IsRunning)
            {
                if (ComponentLibrary.DangerButton("卸载"))
                    axis.Shutdown();
            }
            else
            {
                if (ComponentLibrary.PrimaryButton("加载当前副本"))
                    axis.AutoLoadTimeline();
            }
        }
    }

    private void DrawAssistAxisTab()
    {
        var config = PluginConfig.Instance;
        var axis = AssistAxis.Instance;

        ComponentLibrary.SectionHeader("辅助轴");
        {
            if (ComponentLibrary.PrimaryButton("打开编辑器"))
                OpenEditor();

            ImGui.Spacing();
            ComponentLibrary.Divider();

            if (axis.Initialized)
            {
                ComponentLibrary.StatusDot(axis.IsRunning, axis.IsRunning ? $"运行中 — {axis.TimelineName}" : "未启动");
                if (!string.IsNullOrEmpty(axis.TimelineName))
                    ImGui.Text($"名称: {axis.TimelineName}");
            }
            else
            {
                ImGui.TextDisabled("未加载辅助轴");
            }

            ImGui.Spacing();
            ComponentLibrary.Divider();

            var autoLoad = config.AssistAxisAutoLoad;
            if (ComponentLibrary.Switch("assistAutoLoad", "进副本自动加载", ref autoLoad))
            {
                config.AssistAxisAutoLoad = autoLoad;
                if (autoLoad) axis.LoadAssistTimeline();
                else axis.UnloadAssistTimeline();
                _saveConfig();
            }

            ImGui.Spacing();

            var dir = Path.Combine(DService.Instance().PI.ConfigDirectory.FullName, "AssistTimelines");
            var files = Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.txt").Select(f => Path.GetFileName(f)!).ToArray()
                : [];
            var fileNames = files.Length > 0 ? files : ["(无文件)"];

            int selectedIdx = 0;
            if (ComponentLibrary.Select("assistFile", "选择文件", ref selectedIdx, fileNames))
            {
                var chosen = fileNames[selectedIdx];
                if (chosen != "(无文件)")
                {
                    var path = Path.Combine(dir!, chosen);
                    axis.LoadFromFile(path);
                }
            }

            ImGui.Spacing();

            if (axis.IsRunning)
            {
                if (ComponentLibrary.DangerButton("卸载"))
                    axis.UnloadAssistTimeline();
            }
            else
            {
                if (ComponentLibrary.PrimaryButton("加载当前副本"))
                    axis.LoadAssistTimeline();
            }
        }
    }

    private static void OpenEditor()
    {
        MainWindow.OpenEditor();
    }
}
