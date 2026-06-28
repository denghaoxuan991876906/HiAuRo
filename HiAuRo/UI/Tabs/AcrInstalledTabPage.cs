using HiAuRo.ImGuiLib;
using HiAuRo.Runtime;
using HiAuRo.Runtime.AcrDistribution;
using System.Net.Http;

namespace HiAuRo.UI.Tabs;

public sealed class AcrInstalledTabPage : TabPageBase
{
    private string? _statusMessage;

    public AcrInstalledTabPage() : base("已安装 ACR", "acr_installed", IconHelper.Icons.Settings) { }

    public override void DrawContent()
    {
        ImGui.Spacing();
        ComponentLibrary.SectionHeader("已安装 ACR");

        var acrRoot = Path.Combine(Plugin.Instance.PluginInterface.ConfigDirectory.FullName, "ACR");
        var installed = new AcrInstallationStore(acrRoot).LoadInstalled();

        if (installed.Count == 0)
        {
            ImGui.TextColored(Theme.Colors.TextTertiary, "当前没有已安装的 ACR");
            return;
        }

        if (!string.IsNullOrWhiteSpace(_statusMessage))
        {
            ImGui.TextWrapped(_statusMessage);
            ImGui.Spacing();
        }

        if (!HiAuRo.Data.IsReady || Data.Me.ClassJob == 0)
        {
            ImGui.TextColored(Theme.Colors.TextTertiary, "当前职业未就绪，仍可浏览已安装 ACR。");
        }
        else
        {
            DrawCurrentJobSelector((uint)Data.Me.ClassJob);
            ImGui.Spacing();
            ComponentLibrary.Divider();
            ImGui.Spacing();
        }

        foreach (var item in installed)
        {
            ImGui.Text($"{ResolveDisplayName(item)} {ResolveVersion(item)}");
            ImGui.TextColored(Theme.Colors.TextTertiary, item.InstallKey);

            if (ImGui.SmallButton($"卸载##{item.InstallKey}"))
            {
                TryDeleteInstall(item.InstallDir);
                ACRLifecycle.Reload();
                _statusMessage = $"已卸载 {item.InstallKey}";
                break;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton($"检查更新##{item.InstallKey}"))
                _statusMessage = BuildUpdateStatusMessage(item, acrRoot);

            if (!string.IsNullOrWhiteSpace(item.PublisherId) || !string.IsNullOrWhiteSpace(item.AcrId))
            {
                ImGui.TextColored(
                    Theme.Colors.TextTertiary,
                    $"Publisher: {item.PublisherId}  AcrId: {item.AcrId}");
            }

            if (!string.IsNullOrWhiteSpace(item.SourceUrl))
                ImGui.TextWrapped($"Source: {item.SourceUrl}");

            ImGui.Separator();
        }
    }

    private static void DrawCurrentJobSelector(uint currentJob)
    {
        var registered = ACRLifecycle.GetRegisteredAcrs(currentJob);
        if (registered.Count == 0)
        {
            ImGui.TextColored(Theme.Colors.TextTertiary, "当前职业没有可加载的已安装 ACR");
            return;
        }

        var activeIndex = ACRLifecycle.GetActiveAcrIndex(currentJob);
        var items = registered
            .Select(r => $"{r.DisplayName} {ResolveVersion(r.Version)}")
            .ToArray();

        var index = Math.Clamp(activeIndex, 0, items.Length - 1);
        if (ComponentLibrary.Select("acrInstalledSelector", "当前职业 ACR", ref index, items))
            ACRLifecycle.SetActiveAcr(currentJob, index);
    }

    private static string ResolveDisplayName(InstalledAcrRecord item) =>
        !string.IsNullOrWhiteSpace(item.DisplayName) ? item.DisplayName : item.InstallKey;

    private static string ResolveVersion(InstalledAcrRecord item) => ResolveVersion(item.Version);

    private static string ResolveVersion(string? version) =>
        string.IsNullOrWhiteSpace(version) ? "(无版本)" : version;

    private static void TryDeleteInstall(string? installDir)
    {
        if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir))
            return;

        Directory.Delete(installDir, recursive: true);
    }

    private static string BuildUpdateStatusMessage(InstalledAcrRecord item, string acrRoot)
    {
        if (string.IsNullOrWhiteSpace(item.SourceUrl))
            return $"{item.InstallKey} 没有 SourceUrl，无法检查更新。";

        try
        {
            var sourceStore = new AcrSourceStore(acrRoot);
            var source = sourceStore.Load()
                .FirstOrDefault(x => string.Equals(x.PublisherId, item.PublisherId, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(x.Url, item.SourceUrl, StringComparison.OrdinalIgnoreCase));
            if (source == null)
                return $"{item.InstallKey} 找不到对应作者源，无法检查更新。";

            using var httpClient = new HttpClient();
            var client = new AcrRepositoryClient(httpClient);
            var publisher = client.GetPublisherIndexAsync(source.Url).GetAwaiter().GetResult();
            if (publisher == null)
                return $"{item.InstallKey} 拉取 publisher.json 失败。";

            var acr = publisher.Acrs.FirstOrDefault(x => string.Equals(x.AcrId, item.AcrId, StringComparison.OrdinalIgnoreCase));
            if (acr == null)
                return $"{item.InstallKey} 不在作者源列表中。";

            var manifest = client.GetManifestAsync(acr.ManifestUrl).GetAwaiter().GetResult();
            if (manifest == null)
                return $"{item.InstallKey} 拉取 manifest 失败。";

            var update = AcrUpdateService.CheckForUpdate(item, manifest);
            return update.Status switch
            {
                AcrUpdateStatus.UpdateAvailable => $"{item.InstallKey} 有新版本 {manifest.LatestVersion}（当前 {ResolveVersion(item)}）",
                AcrUpdateStatus.UpToDate => $"{item.InstallKey} 已是最新版本 {ResolveVersion(item)}",
                AcrUpdateStatus.InvalidLocalVersion => $"{item.InstallKey} 本地版本无效：{ResolveVersion(item)}",
                AcrUpdateStatus.InvalidRemoteVersion => $"{item.InstallKey} 远端版本无效：{manifest.LatestVersion}",
                _ => $"{item.InstallKey} 更新状态未知"
            };
        }
        catch (Exception ex)
        {
            return $"{item.InstallKey} 检查更新失败: {ex.Message}";
        }
    }
}
