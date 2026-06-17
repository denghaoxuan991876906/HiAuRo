using System;
using System.Numerics;
using HiAuRo.Infrastructure;
using HiAuRo.ImGuiLib;
using HiAuRo.Runtime;

namespace HiAuRo.UI.Tabs;

public sealed class StatusTabPage : TabPageBase
{
    private readonly PluginConfig _config;
    private readonly Action _saveConfig;

    public StatusTabPage(PluginConfig config, Action saveConfig)
        : base("状态", "status", IconHelper.Icons.Settings)
    {
        _config = config;
        _saveConfig = saveConfig;
    }

    public override void DrawContent()
    {
        DrawAcrCard();
        ImGui.Spacing();
        DrawUiSettingsCard();
        ImGui.Spacing();
        DrawWebUrlsCard();
    }

    private void DrawAcrCard()
    {
        ComponentLibrary.SectionHeader("运行控制");
        DrawAcrControl();
        DrawCombatInfo();
    }

    private void DrawAcrControl()
    {
        var running = RuntimeCore.IsRunning;
        var paused = ACR.MainControlHelper.IsPaused;

        if (running)
        {
            if (paused)
                ComponentLibrary.StatusDot(false, "已暂停", Theme.Colors.AccentOrange);
            else
                ComponentLibrary.StatusDot(true, "运行中");
        }
        else
        {
            ComponentLibrary.StatusDot(false, "已停止", Theme.Colors.AccentRed);
        }

        ImGui.SameLine();
        if (running)
        {
            if (ComponentLibrary.DangerButton("停止"))
                RuntimeCore.Stop();
            ImGui.SameLine();
            if (paused)
            {
                if (ComponentLibrary.PrimaryButton("继续"))
                    ACR.MainControlHelper.TogglePause();
            }
            else
            {
                if (ComponentLibrary.WarningButton("暂停"))
                    ACR.MainControlHelper.TogglePause();
            }
        }
        else
        {
            if (ComponentLibrary.SuccessButton("启动"))
                RuntimeCore.Start();
        }
    }

    private void DrawCombatInfo()
    {
        if (!HiAuRo.Data.IsReady)
        {
            ImGui.Text("等待角色加载...");
            return;
        }

        ComponentLibrary.Divider();
        ImGui.Text($"战斗状态: {CombatContext.CurrentState}");
        ImGui.Text($"当前职业: {Data.Me.ClassJob}");
        ImGui.Text($"当前 ACR: {ACRLifecycle.CurrentAcrName}");
        ImGui.Text($"GCD 剩余: {ACR.GCDHelper.GetGCDCooldown():F0}ms");
    }

    private void DrawUiSettingsCard()
    {
        ComponentLibrary.SectionHeader("界面设置");

        ComponentLibrary.FormRow("主题", () =>
        {
            var isLight = _config.ImGuiThemeMode == ImGuiThemeMode.Light;
            if (ImGui.RadioButton("亮色", isLight))
            {
                _config.ImGuiThemeMode = ImGuiThemeMode.Light;
                Theme.Mode = Theme.ThemeMode.Light;
                _saveConfig();
            }
            ImGui.SameLine();
        if (ImGui.RadioButton("暗色", !isLight))
        {
            _config.ImGuiThemeMode = ImGuiThemeMode.Dark;
            Theme.Mode = Theme.ThemeMode.Dark;
            _saveConfig();
        }
    });
}

    private static void DrawWebUrlsCard()
    {
        if (!ImGui.CollapsingHeader("CEF 悬浮窗"))
            return;

        ImGui.Text("游戏内 CEF 悬浮窗（浏览器打开测试）:");
        ImGui.BulletText("http://localhost:5678/main.html     — 主控制栏");
        ImGui.BulletText("http://localhost:5678/qt.html       — QT 面板");
        ImGui.BulletText("http://localhost:5678/hotkey.html   — 热键面板");
        ImGui.Spacing();
    }
}
