using HiAuRo.ImGuiLib;
using HiAuRo.Runtime;
using HiAuRo.Runtime.AcrDistribution;
using System.Net.Http;

namespace HiAuRo.UI.Tabs;

public sealed class AcrSourcesTabPage : TabPageBase
{
    private string _newUrl = "";
    private string? _statusMessage;
    private readonly Dictionary<string, PublisherIndexDto> _publisherIndexes = [];
    private readonly Dictionary<string, AcrManifestDto> _manifests = [];

    public AcrSourcesTabPage() : base("ACR 源管理", "acr_sources", IconHelper.Icons.Settings) { }

    public override void DrawContent()
    {
        ImGui.Spacing();
        ComponentLibrary.SectionHeader("作者源");

        var acrRoot = Path.Combine(Plugin.Instance.PluginInterface.ConfigDirectory.FullName, "ACR");
        var store = new AcrSourceStore(acrRoot);
        var sources = store.Load();
        var installedByKey = new AcrInstallationStore(acrRoot)
            .LoadInstalled()
            .ToDictionary(x => x.InstallKey, StringComparer.OrdinalIgnoreCase);

        ImGui.InputText("Publisher URL", ref _newUrl, 512);
        if (ComponentLibrary.PrimaryButton("添加源") && !string.IsNullOrWhiteSpace(_newUrl))
        {
            sources.Add(new AcrSourceRecord
            {
                Url = _newUrl.Trim(),
                Enabled = true
            });
            store.Save(sources);
            _newUrl = "";
            sources = store.Load();
        }
        ImGui.SameLine();
        if (ImGui.Button("刷新全部"))
            RefreshAllSources(store, sources);

        ImGui.Spacing();
        ComponentLibrary.Divider();
        ImGui.Spacing();

        if (!string.IsNullOrWhiteSpace(_statusMessage))
        {
            ImGui.TextWrapped(_statusMessage);
            ImGui.Spacing();
        }

        if (sources.Count == 0)
        {
            ImGui.TextColored(Theme.Colors.TextTertiary, "当前没有配置作者源");
            return;
        }

        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            ImGui.Text(string.IsNullOrWhiteSpace(source.PublisherName) ? source.Url : source.PublisherName);
            ImGui.TextColored(Theme.Colors.TextTertiary, source.Url);
            ImGui.TextColored(
                Theme.Colors.TextTertiary,
                $"Enabled: {source.Enabled}  LastSync: {source.LastSyncAt?.ToString("u") ?? "never"}");

            if (ImGui.SmallButton($"刷新##{i}"))
            {
                RefreshSource(store, sources, i);
                source = sources[i];
            }
            ImGui.SameLine();
            if (ImGui.SmallButton($"启停##{i}"))
            {
                source.Enabled = !source.Enabled;
                store.Save(sources);
            }
            ImGui.SameLine();
            if (ImGui.SmallButton($"删除##{i}"))
            {
                sources.RemoveAt(i);
                store.Save(sources);
                _statusMessage = $"已删除源 {source.Url}";
                break;
            }

            if (!string.IsNullOrWhiteSpace(source.LastError))
                ImGui.TextWrapped($"LastError: {source.LastError}");

            if (_publisherIndexes.TryGetValue(source.Url, out var publisher))
            {
                foreach (var acr in publisher.Acrs)
                {
                    var installKey = BuildInstallKey(source.PublisherId, acr.AcrId);
                    var installed = installedByKey.TryGetValue(installKey, out var installedRecord)
                        ? installedRecord
                        : null;

                    ImGui.BulletText($"{acr.Name} ({acr.AcrId})");
                    if (installed != null)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(Theme.Colors.TextTertiary, $"已安装 {installed.Version}");
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"读取清单##{source.Url}##{acr.AcrId}"))
                        LoadManifest(source, acr);

                    if (_manifests.TryGetValue(GetManifestCacheKey(source.Url, acr.ManifestUrl), out var manifest))
                    {
                        ImGui.SameLine();
                        if (ImGui.SmallButton($"{(installed != null ? "重装" : "安装")}##{source.Url}##{acr.AcrId}"))
                            InstallManifest(source, manifest);

                        ImGui.TextColored(
                            Theme.Colors.TextTertiary,
                            $"Latest: {manifest.LatestVersion}  Entry: {manifest.EntryDll}");
                    }
                }
            }

            ImGui.Separator();
        }
    }

    private void RefreshAllSources(AcrSourceStore store, List<AcrSourceRecord> sources)
    {
        for (var i = 0; i < sources.Count; i++)
            RefreshSource(store, sources, i);
    }

    private void RefreshSource(AcrSourceStore store, List<AcrSourceRecord> sources, int index)
    {
        var source = sources[index];
        try
        {
            using var httpClient = new HttpClient();
            var client = new AcrRepositoryClient(httpClient);
            var publisher = client.GetPublisherIndexAsync(source.Url).GetAwaiter().GetResult();
            if (publisher == null)
            {
                source.LastError = "publisher.json 拉取失败";
                _statusMessage = $"刷新失败: {source.Url}";
            }
            else
            {
                source.PublisherId = publisher.PublisherId;
                source.PublisherName = publisher.PublisherName;
                source.LastSyncAt = DateTimeOffset.UtcNow;
                source.LastError = null;
                _publisherIndexes[source.Url] = publisher;
                _statusMessage = $"已刷新 {publisher.PublisherName} ({publisher.Acrs.Count} 个 ACR)";
            }
        }
        catch (Exception ex)
        {
            source.LastError = ex.Message;
            _statusMessage = $"刷新失败: {ex.Message}";
        }

        store.Save(sources);
    }

    private void LoadManifest(AcrSourceRecord source, PublisherAcrRefDto acr)
    {
        try
        {
            using var httpClient = new HttpClient();
            var client = new AcrRepositoryClient(httpClient);
            var manifest = client.GetManifestAsync(acr.ManifestUrl).GetAwaiter().GetResult();
            if (manifest == null)
            {
                _statusMessage = $"读取清单失败: {acr.ManifestUrl}";
                return;
            }

            _manifests[GetManifestCacheKey(source.Url, acr.ManifestUrl)] = manifest;
            _statusMessage = $"已读取 {manifest.Name} {manifest.LatestVersion}";
        }
        catch (Exception ex)
        {
            _statusMessage = $"读取清单失败: {ex.Message}";
        }
    }

    private void InstallManifest(AcrSourceRecord source, AcrManifestDto manifest)
    {
        try
        {
            var acrRoot = Path.Combine(Plugin.Instance.PluginInterface.ConfigDirectory.FullName, "ACR");
            var installer = new AcrPackageInstaller(acrRoot);
            installer.InstallFromManifestAsync(
                    manifest,
                    source.PublisherId,
                    source.PublisherName,
                    source.Url)
                .GetAwaiter()
                .GetResult();

            ACRLifecycle.Reload();
            _statusMessage = $"已安装 {manifest.Name} {manifest.LatestVersion}";
        }
        catch (Exception ex)
        {
            _statusMessage = $"安装失败: {ex.Message}";
        }
    }

    private static string GetManifestCacheKey(string sourceUrl, string manifestUrl) => $"{sourceUrl}|{manifestUrl}";

    private static string BuildInstallKey(string publisherId, string acrId) => $"{publisherId}.{acrId}";
}
