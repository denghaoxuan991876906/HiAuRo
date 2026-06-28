using HiAuRo.ImGuiLib;
using HiAuRo.Runtime.AcrDistribution;
using System.Net.Http;

namespace HiAuRo.UI.Tabs;

public sealed class AcrSourcesTabPage : TabPageBase
{
    private string _newUrl = "";
    private string? _statusMessage;
    private readonly Dictionary<string, AcrManifestDto> _manifests = [];

    public AcrSourcesTabPage() : base("ACR 源管理", "acr_sources", IconHelper.Icons.Settings) { }

    public override void DrawContent()
    {
        ImGui.Spacing();
        ComponentLibrary.SectionHeader("作者源");

        var acrRoot = Path.Combine(Plugin.Instance.PluginInterface.ConfigDirectory.FullName, "ACR");
        var store = new AcrSourceStore(acrRoot);
        var sources = store.Load();

        ImGui.InputText("Publisher URL", ref _newUrl, 512);
        if (ComponentLibrary.PrimaryButton("添加源") && !string.IsNullOrWhiteSpace(_newUrl))
        {
            sources.Add(new AcrSourceRecord
            {
                Url = _newUrl.Trim(),
                Enabled = true
            });
            RefreshSource(store, sources, sources.Count - 1);
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
                $"Enabled: {source.Enabled}  LastSync: {source.LastSyncAt?.ToString("u") ?? "never"}  Acrs: {source.Acrs.Count}");

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

            if (source.Acrs.Count > 0)
            {
                foreach (var acr in source.Acrs)
                {
                    ImGui.BulletText($"{acr.Name}");
                    if (acr.CachedManifest is { } manifest ||
                        _manifests.TryGetValue(GetManifestCacheKey(source.Url, acr.ManifestUrl), out manifest))
                    {
                        ImGui.TextColored(
                            Theme.Colors.TextTertiary,
                            $"Latest: {manifest.LatestVersion}  AcrId: {acr.AcrId}");
                        if (!string.IsNullOrWhiteSpace(manifest.Description))
                            ImGui.TextWrapped(manifest.Description);
                    }
                    else
                    {
                        ImGui.TextColored(Theme.Colors.TextTertiary, $"AcrId: {acr.AcrId}");
                        if (!string.IsNullOrWhiteSpace(acr.ManifestLastError))
                            ImGui.TextColored(Theme.Colors.AccentRed, $"清单读取失败: {acr.ManifestLastError}");
                    }
                }
            }
            else
            {
                ImGui.TextColored(Theme.Colors.TextTertiary, "该源还没有缓存 ACR 列表，请点击“刷新”。");
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
                source.Acrs = publisher.Acrs;
                source.LastSyncAt = DateTimeOffset.UtcNow;
                source.LastError = null;
                CacheManifestsForSource(source);
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
                acr.ManifestLastError = "HTTP 请求失败或返回空内容";
                return;
            }

            acr.CachedManifest = manifest;
            acr.ManifestSyncedAt = DateTimeOffset.UtcNow;
            acr.ManifestLastError = null;
            _manifests[GetManifestCacheKey(source.Url, acr.ManifestUrl)] = manifest;
            _statusMessage = $"已读取 {manifest.Name} {manifest.LatestVersion}";
        }
        catch (Exception ex)
        {
            acr.ManifestLastError = ex.Message;
            _statusMessage = $"读取清单失败: {ex.Message}";
        }
    }

    private void CacheManifestsForSource(AcrSourceRecord source)
    {
        foreach (var acr in source.Acrs)
            LoadManifest(source, acr);
    }

    private static string GetManifestCacheKey(string sourceUrl, string manifestUrl) => $"{sourceUrl}|{manifestUrl}";
}
