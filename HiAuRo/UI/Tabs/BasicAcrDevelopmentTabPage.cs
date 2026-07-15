using System.Numerics;
using HiAuRo.ImGuiLib;
using HiAuRo.Infrastructure;
using HiAuRo.Runtime;

namespace HiAuRo.UI.Tabs;

public sealed class BasicAcrDevelopmentTabPage : TabPageBase
{
    private readonly PluginConfig _config;
    private readonly Action _saveConfig;
    private string _newSourcePath = "";
    private string? _inputError;

    public BasicAcrDevelopmentTabPage(PluginConfig config, Action saveConfig)
        : base("基础 ACR 开发", "basic_acr_development", IconHelper.Icons.Settings)
    {
        _config = config;
        _saveConfig = saveConfig;
    }

    public override void DrawContent()
    {
        ImGui.Spacing();

        if (!_config.ShowDeveloperTools)
        {
            ComponentLibrary.SectionHeader("基础 ACR 开发");
            ImGui.TextColored(Theme.Colors.TextTertiary, "开发者工具未启用");
            ImGui.Spacing();
            if (ComponentLibrary.PrimaryButton("启用开发者工具"))
            {
                _config.ShowDeveloperTools = true;
                _saveConfig();
            }
            return;
        }

        DrawRuntimeControls();
        DrawSources();
        DrawLoadStatus();
    }

    private void DrawRuntimeControls()
    {
        ComponentLibrary.SectionHeader("运行控制");

        var enabled = _config.BasicAcrScriptEnabled;
        if (ComponentLibrary.Switch("basic_acr_development_enabled", "开发模式", ref enabled)
            && BasicAcrDevelopment.SetEnabled(enabled))
            _saveConfig();

        ImGui.SameLine();
        ImGui.BeginDisabled(!_config.BasicAcrScriptEnabled);
        if (ComponentLibrary.DefaultButton("加载/重载"))
            BasicAcrDevelopment.Reload();
        ImGui.EndDisabled();

        var sources = _config.BasicAcrSources ?? [];
        var enabledCount = sources.Count(source => source is { Enabled: true });
        ImGui.TextColored(
            Theme.Colors.TextTertiary,
            $"源码 {sources.Count} 个，启用 {enabledCount} 个");
    }

    private void DrawSources()
    {
        ComponentLibrary.SectionHeader("源码文件");

        var availableWidth = Math.Max(0f, ImGui.GetContentRegionAvail().X);
        var buttonWidth = ImGui.CalcTextSize("添加源码").X + 34f;
        var inputWidth = Math.Max(120f,
            availableWidth - buttonWidth - ImGui.GetStyle().ItemSpacing.X);
        ImGui.SetNextItemWidth(inputWidth);
        var addRequested = ImGui.InputTextWithHint(
            "##basic_acr_new_source",
            @"C:\path\to\BasicAcr.cs",
            ref _newSourcePath,
            1024,
            ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        addRequested |= ComponentLibrary.PrimaryButton("添加源码");

        if (addRequested)
            AddSource();

        if (!string.IsNullOrWhiteSpace(_inputError))
            ImGui.TextColored(Theme.Colors.AccentRed, _inputError);

        ImGui.Spacing();
        var sources = _config.BasicAcrSources ??= [];
        if (sources.Count == 0)
        {
            ImGui.TextColored(Theme.Colors.TextTertiary, "尚未添加源码文件");
            return;
        }

        if (!ImGui.BeginTable(
                "##BasicAcrSources",
                3,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            return;

        ImGui.TableSetupColumn("加载", ImGuiTableColumnFlags.WidthFixed, 54f);
        ImGui.TableSetupColumn("源码路径", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##actions", ImGuiTableColumnFlags.WidthFixed, 36f);
        ImGui.TableHeadersRow();

        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            if (source is null)
                continue;

            ImGui.PushID(index);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            var sourceEnabled = source.Enabled;
            if (ComponentLibrary.Switch("enabled", ref sourceEnabled))
            {
                source.Enabled = sourceEnabled;
                _saveConfig();
            }

            ImGui.TableNextColumn();
            var sourcePath = source.Path ?? "";
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.InputText("##path", ref sourcePath, 1024))
            {
                source.Path = sourcePath;
                _inputError = null;
                _saveConfig();
            }

            ImGui.TableNextColumn();
            if (ComponentLibrary.IconButton(
                    ComponentLibrary.IconType.Close,
                    Theme.Colors.AccentRed,
                    new Vector2(28f, 24f),
                    ComponentLibrary.IconButtonStyle.Text,
                    14f))
            {
                sources.RemoveAt(index);
                _inputError = null;
                _saveConfig();
                ImGui.PopID();
                break;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("删除源码文件");

            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    private void DrawLoadStatus()
    {
        ComponentLibrary.SectionHeader("加载状态");

        var hasPendingSourceChanges = HasPendingSourceChanges();
        switch (BasicAcrDevelopment.State)
        {
            case BasicAcrDevelopmentState.Disabled:
                ComponentLibrary.StatusDot(false, "未启用", Theme.Colors.TextTertiary);
                break;
            case BasicAcrDevelopmentState.NotLoaded:
                ComponentLibrary.StatusDot(true, "未加载", Theme.Colors.AccentOrange);
                break;
            case BasicAcrDevelopmentState.Ready:
                ComponentLibrary.StatusDot(
                    true,
                    hasPendingSourceChanges ? "配置待重载" : "已就绪",
                    hasPendingSourceChanges ? Theme.Colors.AccentOrange : Theme.Colors.AccentGreen);
                break;
            case BasicAcrDevelopmentState.Failed:
                ComponentLibrary.StatusDot(true, "加载失败", Theme.Colors.AccentRed);
                break;
        }

        if (BasicAcrDevelopment.State == BasicAcrDevelopmentState.Ready)
        {
            ImGui.Text($"脚本类型: {BasicAcrDevelopment.ScriptTypeName}");
            ImGui.Text($"目标职业: {BasicAcrDevelopment.TargetJob}");
            ImGui.Text($"已加载源码: {BasicAcrDevelopment.LoadedSourcePaths.Count}");
            ImGui.Text($"加载时间: {BasicAcrDevelopment.LoadedAt:yyyy-MM-dd HH:mm:ss}");
        }

        if (!string.IsNullOrWhiteSpace(BasicAcrDevelopment.LastError))
        {
            using var errorColor = new ImRaii.ColorDisposable();
            errorColor.Push(ImGuiCol.Text, Theme.Colors.AccentRed);
            ImGui.TextWrapped(BasicAcrDevelopment.LastError);
        }

        foreach (var diagnostic in BasicAcrDevelopment.Diagnostics)
            ImGui.TextWrapped(diagnostic.ToDisplayString());
    }

    private void AddSource()
    {
        var path = _newSourcePath.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            _inputError = "请输入源码文件路径";
            return;
        }
        if (!Path.IsPathFullyQualified(path))
        {
            _inputError = "源码文件必须使用绝对路径";
            return;
        }
        if (!string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase))
        {
            _inputError = "源码文件必须是 .cs 文件";
            return;
        }

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            _inputError = "源码文件路径无效";
            return;
        }

        var sources = _config.BasicAcrSources ??= [];
        if (sources.Any(source => source is not null
                && string.Equals(
                    NormalizeForComparison(source.Path),
                    normalizedPath,
                    StringComparison.OrdinalIgnoreCase)))
        {
            _inputError = "该源码文件已经存在";
            return;
        }

        sources.Add(new BasicAcrSourceConfig
        {
            Path = normalizedPath,
            Enabled = true,
        });
        _newSourcePath = "";
        _inputError = null;
        _saveConfig();
    }

    private static string NormalizeForComparison(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            return path?.Trim() ?? "";

        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.Trim();
        }
    }

    private bool HasPendingSourceChanges()
    {
        if (BasicAcrDevelopment.State != BasicAcrDevelopmentState.Ready)
            return false;

        var configuredPaths = (_config.BasicAcrSources ?? [])
            .Where(source => source is { Enabled: true })
            .Select(source => NormalizeForComparison(source.Path))
            .ToArray();
        return !configuredPaths.SequenceEqual(
            BasicAcrDevelopment.LoadedSourcePaths,
            StringComparer.OrdinalIgnoreCase);
    }
}
