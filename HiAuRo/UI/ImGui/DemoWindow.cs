using System.Numerics;
using Dalamud.Interface.Windowing;
using HiAuRo.Infrastructure;
using HiAuRo;

namespace HiAuRo.ImGuiLib;

/// <summary>
/// ImGui 组件展示窗口 — /hi gallery 打开
/// </summary>
public sealed class DemoWindow : Window
{
    private bool _demoSwitch = true;
    private bool _demoSwitch2;
    private float _demoSlider = 50f;
    private int _demoSliderInt = 3;
    private int _demoSelect;
    private readonly string[] _demoOptions = ["选项A", "选项B", "选项C", "选项D"];
    private int _demoInput = 42;
    private int _demoTab;
    private readonly string[] _demoTabNames = ["常规", "高级", "关于"];

    /// <summary>Initializes a new instance of the <see cref="DemoWindow"/> class</summary>
    public DemoWindow() : base("HiAuRo 组件展示##Gallery")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        IsOpen = false;
    }

    /// <summary>绘制窗口</summary>
    public override void Draw()
    {
        using var _ = Theme.PushThemeScope();

        // 主题切换
        var themeMode = (int)Theme.Mode;
        ImGui.TextColored(Theme.Colors.AccentBlue, "主题");
        ImGui.SameLine();
        if (ImGui.RadioButton("亮色", themeMode == 0))
        {
            Theme.Mode = Theme.ThemeMode.Light;
            PluginConfig.Instance.ImGuiThemeMode = ImGuiThemeMode.Light;
            Plugin.SaveConfig();
        }
        ImGui.SameLine();
        if (ImGui.RadioButton("暗色", themeMode == 1))
        {
            Theme.Mode = Theme.ThemeMode.Dark;
            PluginConfig.Instance.ImGuiThemeMode = ImGuiThemeMode.Dark;
            Plugin.SaveConfig();
        }

        ImGui.Spacing();
        ComponentLibrary.Divider();

        if (ImGui.BeginTabBar("##galleryMainTabs"))
        {
            if (ImGui.BeginTabItem("组件预览"))
            {
                DrawComponentPreview();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("按钮样式"))
            {
                DrawButtonGallery();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("主题色"))
            {
                DrawThemePreview();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawComponentPreview()
    {
        ImGui.TextColored(Theme.Colors.AccentBlue, "Button 变体");
        ComponentLibrary.PrimaryButton("Primary");
        ImGui.SameLine();
        ComponentLibrary.DangerButton("Danger");
        ImGui.SameLine();
        ComponentLibrary.SuccessButton("Success");
        ImGui.SameLine();
        ComponentLibrary.WarningButton("Warning");
        ImGui.Spacing();
        ComponentLibrary.DefaultButton("Default");
        ImGui.SameLine();
        ComponentLibrary.TextButton("Text Button");
        ImGui.SameLine();
        ComponentLibrary.LinkButton("Link");
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.AccentBlue, "图标按钮 (战斗尺寸 56×38)");
        ComponentLibrary.IconButton(ComponentLibrary.IconType.Play, Theme.Colors.AccentGreen, new Vector2(56, 38));
        ImGui.SameLine();
        ComponentLibrary.IconButton(ComponentLibrary.IconType.Stop, Theme.Colors.AccentRed, new Vector2(56, 38));
        ImGui.SameLine();
        ComponentLibrary.IconButton(ComponentLibrary.IconType.Pause, Theme.Colors.AccentOrange, new Vector2(56, 38));
        ImGui.SameLine();
        ComponentLibrary.IconButton(ComponentLibrary.IconType.Save, Theme.Colors.TextSecondary, new Vector2(56, 38),
            ComponentLibrary.IconButtonStyle.Outline);
        ImGui.SameLine();
        ComponentLibrary.IconButton(ComponentLibrary.IconType.ChevronDown, Theme.Colors.AccentOrange, new Vector2(56, 38),
            ComponentLibrary.IconButtonStyle.Text);
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.AccentBlue, "Switch");
        ComponentLibrary.Switch("sw1", "AoE 技能", ref _demoSwitch);
        ComponentLibrary.Switch("sw2", "爆发药", ref _demoSwitch2);
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.AccentBlue, "Slider");
        ComponentLibrary.Slider("sl1", "攻击距离", ref _demoSlider, 5, 40);
        ComponentLibrary.SliderInt("sl2", "AOE 数量", ref _demoSliderInt, 1, 10);
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.AccentBlue, "Select");
        ComponentLibrary.Select("sel1", "技能顺序", ref _demoSelect, _demoOptions);
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.AccentBlue, "Tabs");
        ComponentLibrary.Tabs("demoTabs", ref _demoTab, _demoTabNames);
        ImGui.Text($"当前标签: {_demoTabNames[_demoTab]}");
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.AccentBlue, "Card");
        ComponentLibrary.Card("基本设置", () =>
        {
            ComponentLibrary.Switch("cardSw", "启用功能", ref _demoSwitch);
            ComponentLibrary.InputNumber("cardNum", "最大数量", ref _demoInput, 1, 10);
        });
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.AccentBlue, "Tag");
        ComponentLibrary.Tag("AoE", true, Theme.Colors.AccentGreen);
        ImGui.SameLine();
        ComponentLibrary.Tag("爆发", true, Theme.Colors.AccentBlue);
        ImGui.SameLine();
        ComponentLibrary.Tag("疾跑", true, Theme.Colors.AccentOrange);
        ImGui.SameLine();
        ComponentLibrary.Tag("吃药", false);
        ImGui.SameLine();
        ComponentLibrary.Tag("防击退", false);
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.AccentBlue, "Badge");
        ComponentLibrary.Badge(true, Theme.Colors.AccentGreen);
        ImGui.SameLine(); ImGui.Text("运行中");
        ImGui.SameLine();
        ComponentLibrary.Badge(true, Theme.Colors.AccentOrange);
        ImGui.SameLine(); ImGui.Text("已暂停");
        ImGui.SameLine();
        ComponentLibrary.Badge(false);
        ImGui.SameLine(); ImGui.Text("已停止");
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.AccentBlue, "InputNumber");
        ComponentLibrary.InputNumber("num1", "数值输入", ref _demoInput);
        ImGui.Spacing();

        ComponentLibrary.Divider();
        ImGui.Text("上方为 Divider 分割线");
    }

    private void DrawButtonGallery()
    {
        ImGui.TextColored(Theme.Colors.AccentBlue, "Ant Design 5 按钮类型");
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.TextSecondary, "填充按钮 (Filled)");
        ComponentLibrary.PrimaryButton("Primary 主按钮");
        ImGui.SameLine();
        ComponentLibrary.DangerButton("Danger 危险");
        ImGui.SameLine();
        ComponentLibrary.SuccessButton("Success 成功");
        ImGui.SameLine();
        ComponentLibrary.WarningButton("Warning 警告");
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.TextSecondary, "默认 / 边框 / 文字 / 链接");
        ComponentLibrary.DefaultButton("Default 默认");
        ImGui.SameLine();
        ComponentLibrary.TextButton("Text 文字");
        ImGui.SameLine();
        ComponentLibrary.LinkButton("Link 链接");
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.TextSecondary, "图标按钮 (56×38 战斗尺寸)");
        ComponentLibrary.IconButton(ComponentLibrary.IconType.Play, Theme.Colors.AccentGreen, new Vector2(56, 38));
        ImGui.SameLine();
        ComponentLibrary.IconButton(ComponentLibrary.IconType.Stop, Theme.Colors.AccentRed, new Vector2(56, 38));
        ImGui.SameLine();
        ComponentLibrary.IconButton(ComponentLibrary.IconType.Pause, Theme.Colors.AccentOrange, new Vector2(56, 38));
        ImGui.SameLine();
        ComponentLibrary.IconButton(ComponentLibrary.IconType.Save, Theme.Colors.TextSecondary, new Vector2(56, 38),
            ComponentLibrary.IconButtonStyle.Outline);
        ImGui.SameLine();
        ComponentLibrary.IconButton(ComponentLibrary.IconType.ChevronDown, Theme.Colors.AccentOrange, new Vector2(56, 38),
            ComponentLibrary.IconButtonStyle.Text);
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.TextSecondary, "图标按钮变体");
        ComponentLibrary.IconButton(ComponentLibrary.IconType.Play, Theme.Colors.AccentBlue, new Vector2(56, 38),
            ComponentLibrary.IconButtonStyle.Fill);
        ImGui.SameLine();
        ComponentLibrary.IconButton(ComponentLibrary.IconType.Play, Theme.Colors.AccentBlue, new Vector2(56, 38),
            ComponentLibrary.IconButtonStyle.Outline);
        ImGui.SameLine();
        ComponentLibrary.IconButton(ComponentLibrary.IconType.Play, Theme.Colors.AccentBlue, new Vector2(56, 38),
            ComponentLibrary.IconButtonStyle.Text);
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.TextSecondary, "不同尺寸");
        ComponentLibrary.PrimaryButton("大号", new Vector2(120, 40));
        ImGui.SameLine();
        ComponentLibrary.PrimaryButton("默认");
        ImGui.SameLine();
        ComponentLibrary.PrimaryButton("小", new Vector2(60, 28));
    }

    private static void DrawThemePreview()
    {
        ImGui.TextColored(Theme.Colors.AccentBlue, "背景色");
        DrawColorSwatch("BgLayout", Theme.Colors.BgLayout);
        DrawColorSwatch("BgContainer", Theme.Colors.BgContainer);
        DrawColorSwatch("BgElevated", Theme.Colors.BgElevated);
        DrawColorSwatch("GlassBg", Theme.Colors.GlassBg);
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.AccentBlue, "文字色");
        DrawColorSwatch("TextPrimary", Theme.Colors.TextPrimary);
        DrawColorSwatch("TextSecondary", Theme.Colors.TextSecondary);
        DrawColorSwatch("TextTertiary", Theme.Colors.TextTertiary);
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.AccentBlue, "品牌色");
        DrawColorSwatch("AccentBlue", Theme.Colors.AccentBlue);
        DrawColorSwatch("AccentGreen", Theme.Colors.AccentGreen);
        DrawColorSwatch("AccentRed", Theme.Colors.AccentRed);
        DrawColorSwatch("AccentOrange", Theme.Colors.AccentOrange);
        ImGui.Spacing();

        ImGui.TextColored(Theme.Colors.AccentBlue, "边框 / 填充");
        DrawColorSwatch("Border", Theme.Colors.Border);
        DrawColorSwatch("BorderSecondary", Theme.Colors.BorderSecondary);
        DrawColorSwatch("FillPrimary", Theme.Colors.FillPrimary);
        DrawColorSwatch("FillSecondary", Theme.Colors.FillSecondary);
    }

    private static void DrawColorSwatch(string name, Vector4 color)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        dl.AddRectFilled(pos, pos + new Vector2(20, 20), ImGui.ColorConvertFloat4ToU32(color), Theme.RadiusXS);
        dl.AddRect(pos, pos + new Vector2(20, 20), ImGui.ColorConvertFloat4ToU32(Theme.Colors.Border), Theme.RadiusXS);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 28);
        ImGui.Text(name);
    }
}