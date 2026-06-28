using System.Net.Http;
using System.Numerics;
using HiAuRo.ACR;
using HiAuRo.ImGuiLib;
using HiAuRo.Runtime;
using HiAuRo.Runtime.AcrDistribution;

namespace HiAuRo.UI.Tabs;

public sealed class AcrInstalledTabPage : TabPageBase
{
    private string? _statusMessage;
    private Jobs _jobFilter = Jobs.None;

    public AcrInstalledTabPage() : base("ACR 列表", "acr_installed", IconHelper.Icons.Settings) { }

    public override void DrawContent()
    {
        ImGui.Spacing();
        ComponentLibrary.SectionHeader("ACR 列表");

        var acrRoot = Path.Combine(Plugin.Instance.PluginInterface.ConfigDirectory.FullName, "ACR");
        var store = new AcrSourceStore(acrRoot);
        var sources = store.Load();
        var enabledSources = sources.Where(x => x.Enabled).ToList();
        var installed = new AcrInstallationStore(acrRoot).LoadInstalled();
        var cards = AcrCatalogBuilder.BuildCards(enabledSources, installed).ToList();
        var groups = AcrCatalogBuilder.BuildJobGroups(cards, _jobFilter).ToList();

        DrawToolbar(store, sources, cards);

        if (!string.IsNullOrWhiteSpace(_statusMessage))
        {
            ImGui.TextWrapped(_statusMessage);
            ImGui.Spacing();
        }

        if (cards.Count == 0)
        {
            DrawEmptyState(sources.Count, enabledSources.Count);
            return;
        }

        var avail = ImGui.GetContentRegionAvail();
        var selectorWidth = Math.Clamp(avail.X * 0.24f, 128f, 172f);
        var panelHeight = Math.Max(260f, avail.Y);

        ImGui.BeginChild("##acr_catalog_jobs", new Vector2(selectorWidth, panelHeight), false);
        DrawJobSelector(cards);
        ImGui.EndChild();

        ImGui.SameLine(0, 10f);

        ImGui.BeginChild("##acr_catalog_cards", new Vector2(-1, panelHeight), false);
        DrawCatalog(groups);
        ImGui.EndChild();
    }

    private void DrawToolbar(AcrSourceStore store, List<AcrSourceRecord> sources, IReadOnlyList<AcrCatalogCard> cards)
    {
        if (ComponentLibrary.PrimaryButton("刷新全部源"))
        {
            RefreshAllSources(store, sources);
            _statusMessage = "已刷新全部源";
        }

        ImGui.SameLine();
        if (ComponentLibrary.DefaultButton("重载 ACR"))
        {
            ACRLifecycle.Reload();
            _statusMessage = "已重载 ACR";
        }

        ImGui.SameLine();
        ImGui.TextColored(
            Theme.Colors.TextTertiary,
            $"源 {sources.Count(x => x.Enabled)}/{sources.Count}  ACR {cards.Count}  已安装 {cards.Count(x => x.Installed)}");

        ImGui.Spacing();
        ComponentLibrary.Divider();
    }

    private void DrawJobSelector(IReadOnlyList<AcrCatalogCard> cards)
    {
        DrawJobFilterButton(Jobs.None, "全部", cards.Count, selected: _jobFilter == Jobs.None);

        foreach (var job in AcrCatalogBuilder.GetAvailableJobs(cards).Where(x => x != Jobs.None))
        {
            var count = cards.Count(x => x.SupportedJobs.Contains(job));
            DrawJobFilterButton(job, job.ToString(), count, selected: _jobFilter == job);
        }
    }

    private void DrawJobFilterButton(Jobs job, string label, int count, bool selected)
    {
        var size = new Vector2(Math.Max(ImGui.GetContentRegionAvail().X - 6f, 96f), 42f);
        using var styles = new ImRaii.StyleDisposable();
        styles.Push(ImGuiStyleVar.FrameRounding, Theme.RadiusMD);
        styles.Push(ImGuiStyleVar.FramePadding, new Vector2(10f, 6f));

        using var colors = new ImRaii.ColorDisposable();
        colors.Push(ImGuiCol.Button, selected ? Theme.Colors.SidebarActive : Vector4.Zero);
        colors.Push(ImGuiCol.ButtonHovered, Theme.Colors.BgHover);
        colors.Push(ImGuiCol.ButtonActive, Theme.Colors.FillPrimary);
        colors.Push(ImGuiCol.Text, selected ? Theme.Colors.TextPrimary : Theme.Colors.TextSecondary);

        if (ImGui.Button($"##acr_job_{job}", size))
        {
            _jobFilter = job;
        }

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var dl = ImGui.GetWindowDrawList();

        var isCurrentJob = job != Jobs.None && HiAuRo.Data.IsReady && Data.Me.ClassJob == (uint)job;
        if (selected || isCurrentJob)
        {
            dl.AddRect(
                min,
                max,
                ImGui.ColorConvertFloat4ToU32(selected ? Theme.Colors.SidebarActiveBorder : Theme.Colors.AccentGreen),
                Theme.RadiusMD,
                0,
                selected ? 1.2f : 1f);
        }

        if (job != Jobs.None)
        {
            ImGui.SetCursorScreenPos(min + new Vector2(9f, 7f));
            JobIconHelper.DrawJobIcon((uint)job, 28f);
        }
        else
        {
            var center = min + new Vector2(23f, 21f);
            IconHelper.DrawIcon(dl, center, IconHelper.Icons.Settings,
                ImGui.ColorConvertFloat4ToU32(selected ? Theme.Colors.AccentBlue : Theme.Colors.TextTertiary), 18f);
        }

        dl.AddText(min + new Vector2(46f, 6f), ImGui.ColorConvertFloat4ToU32(Theme.Colors.TextPrimary), label);
        dl.AddText(
            min + new Vector2(46f, 23f),
            ImGui.ColorConvertFloat4ToU32(isCurrentJob ? Theme.Colors.AccentGreen : Theme.Colors.TextTertiary),
            isCurrentJob ? $"当前职业 · {count} 个" : $"{count} 个");

        ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y + 4f));
    }

    private void DrawCatalog(IReadOnlyList<AcrCatalogJobGroup> groups)
    {
        if (groups.Count == 0)
        {
            ImGui.TextColored(Theme.Colors.TextTertiary, "当前职业暂无可展示的 ACR。");
            return;
        }

        foreach (var group in groups)
            DrawJobGroup(group);
    }

    private void DrawJobGroup(AcrCatalogJobGroup group)
    {
        ImGui.BeginGroup();
        if (group.Job != Jobs.None)
        {
            JobIconHelper.DrawJobIcon((uint)group.Job, 30f);
            ImGui.SameLine();
        }

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.Colors.TextPrimary, $"{DisplayJob(group.Job)}");
        ImGui.SameLine();
        ImGui.TextColored(Theme.Colors.TextTertiary, $"{group.Cards.Count} 个 ACR");
        ImGui.EndGroup();
        ImGui.Spacing();

        foreach (var card in group.Cards)
            DrawAcrCard(card, group.Job);

        ImGui.Spacing();
    }

    private void DrawAcrCard(AcrCatalogCard card, Jobs groupJob)
    {
        ComponentLibrary.Card(() =>
        {
            DrawCardHeader(card);
            DrawSupportedJobs(card);

            if (!string.IsNullOrWhiteSpace(card.Description))
            {
                ImGui.TextWrapped(card.Description);
            }
            else
            {
                ImGui.TextColored(Theme.Colors.TextTertiary, "作者暂未填写描述。");
            }

            DrawCardMeta(card);
            DrawCardActions(card, groupJob);
        });
    }

    private void DrawCardHeader(AcrCatalogCard card)
    {
        ImGui.TextColored(Theme.Colors.TextPrimary, card.DisplayName);
        ImGui.SameLine();
        DrawStatusTag(card);

        ImGui.TextColored(Theme.Colors.TextTertiary, $"作者: {card.AuthorName}");
    }

    private void DrawStatusTag(AcrCatalogCard card)
    {
        ImGui.PushID($"acr_status_{card.InstallKey}");
        try
        {
            DrawStatusTagContent(card);
        }
        finally
        {
            ImGui.PopID();
        }
    }

    private static void DrawStatusTagContent(AcrCatalogCard card)
    {
        if (!card.HasManifest)
        {
            ComponentLibrary.Tag("清单缺失", active: true, activeColor: Theme.Colors.AccentOrange);
            return;
        }

        if (!card.Installed)
        {
            ComponentLibrary.Tag("未安装", active: false);
            return;
        }

        var label = card.UpdateStatus == AcrUpdateStatus.UpdateAvailable ? "可更新" : "已安装";
        var color = card.UpdateStatus == AcrUpdateStatus.UpdateAvailable
            ? Theme.Colors.AccentOrange
            : Theme.Colors.AccentGreen;
        ComponentLibrary.Tag(label, active: true, activeColor: color);
    }

    private void DrawSupportedJobs(AcrCatalogCard card)
    {
        if (card.SupportedJobs.Count == 0)
            return;

        ImGui.TextColored(Theme.Colors.TextTertiary, "职业支持:");
        ImGui.SameLine();

        for (var i = 0; i < card.SupportedJobs.Count; i++)
        {
            var job = card.SupportedJobs[i];
            if (job == Jobs.None)
            {
                ImGui.TextColored(Theme.Colors.TextTertiary, "未声明");
            }
            else
            {
                JobIconHelper.DrawJobIcon((uint)job, 24f);
            }

            if (i < card.SupportedJobs.Count - 1)
                ImGui.SameLine();
        }
    }

    private void DrawCardMeta(AcrCatalogCard card)
    {
        var versionText = string.IsNullOrWhiteSpace(card.Version) ? "未知版本" : card.Version;
        var publishedText = card.PublishedAt?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "未知时间";

        ImGui.TextColored(
            Theme.Colors.TextTertiary,
            $"版本: {versionText}  更新: {publishedText}  来源: {card.PublisherName}");

        if (!string.IsNullOrWhiteSpace(card.ManifestError))
            ImGui.TextColored(Theme.Colors.AccentRed, $"清单错误: {card.ManifestError}");
    }

    private void DrawCardActions(AcrCatalogCard card, Jobs groupJob)
    {
        var canInstall = card.Manifest != null && card.Source != null;
        if (!card.Installed)
        {
            DrawDisabledScope(!canInstall);
            if (ComponentLibrary.PrimaryButton($"下载##{card.InstallKey}", new Vector2(82f, 0)))
                InstallCard(card);
            EndDisabledScope(!canInstall);
        }
        else
        {
            DrawDisabledScope(!canInstall);
            if (ComponentLibrary.DefaultButton($"重装##{card.InstallKey}", new Vector2(78f, 0)))
                InstallCard(card);
            EndDisabledScope(!canInstall);

            ImGui.SameLine();
            DrawDisabledScope(card.UpdateStatus != AcrUpdateStatus.UpdateAvailable || !canInstall);
            if (ComponentLibrary.WarningButton($"更新##{card.InstallKey}", new Vector2(78f, 0)))
                InstallCard(card);
            EndDisabledScope(card.UpdateStatus != AcrUpdateStatus.UpdateAvailable || !canInstall);

            ImGui.SameLine();
            if (ComponentLibrary.DangerButton($"卸载##{card.InstallKey}", new Vector2(78f, 0)))
            {
                TryDeleteInstall(card.InstallDir);
                ACRLifecycle.Reload();
                _statusMessage = $"已卸载 {card.DisplayName}";
                return;
            }
        }

        if (groupJob != Jobs.None && HiAuRo.Data.IsReady && Data.Me.ClassJob == (uint)groupJob)
        {
            ImGui.SameLine();
            DrawDisabledScope(!card.Installed);
            if (ComponentLibrary.SuccessButton($"设为当前##{card.InstallKey}", new Vector2(96f, 0)))
                TryActivateCurrentJob(card, (uint)groupJob);
            EndDisabledScope(!card.Installed);
        }
    }

    private void TryActivateCurrentJob(AcrCatalogCard card, uint currentJob)
    {
        var registered = ACRLifecycle.GetRegisteredAcrs(currentJob);
        for (var i = 0; i < registered.Count; i++)
        {
            if (string.Equals(registered[i].InstallKey, card.InstallKey, StringComparison.OrdinalIgnoreCase))
            {
                ACRLifecycle.SetActiveAcr(currentJob, i);
                _statusMessage = $"已切换到 {card.DisplayName}";
                return;
            }
        }

        _statusMessage = $"当前职业未注册 {card.DisplayName}，请先重载 ACR。";
    }

    private void InstallCard(AcrCatalogCard card)
    {
        if (card.Manifest == null || card.Source == null)
        {
            _statusMessage = $"缺少清单缓存，先刷新源：{card.DisplayName}";
            return;
        }

        try
        {
            var acrRoot = Path.Combine(Plugin.Instance.PluginInterface.ConfigDirectory.FullName, "ACR");
            var installer = new AcrPackageInstaller(acrRoot);
            installer.InstallFromManifestAsync(
                    card.Manifest,
                    card.Source.PublisherId,
                    card.Source.PublisherName,
                    card.Source.Url)
                .GetAwaiter()
                .GetResult();

            ACRLifecycle.Reload();
            _statusMessage = $"已安装 {card.DisplayName} {card.Version}";
        }
        catch (Exception ex)
        {
            _statusMessage = $"安装失败: {ex.Message}";
        }
    }

    private void RefreshAllSources(AcrSourceStore store, List<AcrSourceRecord> sources)
    {
        foreach (var source in sources.Where(x => x.Enabled))
        {
            try
            {
                using var httpClient = new HttpClient();
                var client = new AcrRepositoryClient(httpClient);
                var publisher = client.GetPublisherIndexAsync(source.Url).GetAwaiter().GetResult();
                if (publisher == null)
                {
                    source.LastError = "publisher.json 拉取失败";
                    continue;
                }

                source.PublisherId = publisher.PublisherId;
                source.PublisherName = publisher.PublisherName;
                source.Acrs = publisher.Acrs;
                source.LastSyncAt = DateTimeOffset.UtcNow;
                source.LastError = null;
                CacheManifestsForSource(source, client);
            }
            catch (Exception ex)
            {
                source.LastError = ex.Message;
            }
        }

        store.Save(sources);
    }

    private void CacheManifestsForSource(AcrSourceRecord source, AcrRepositoryClient client)
    {
        foreach (var acr in source.Acrs)
        {
            try
            {
                var manifest = client.GetManifestAsync(acr.ManifestUrl).GetAwaiter().GetResult();
                if (manifest == null)
                {
                    acr.ManifestLastError = "HTTP 请求失败或返回空内容";
                    continue;
                }

                acr.CachedManifest = manifest;
                acr.ManifestSyncedAt = DateTimeOffset.UtcNow;
                acr.ManifestLastError = null;
            }
            catch (Exception ex)
            {
                acr.ManifestLastError = ex.Message;
            }
        }
    }

    private void DrawEmptyState(int sourceCount, int enabledSourceCount)
    {
        if (sourceCount == 0)
        {
            ImGui.TextColored(Theme.Colors.TextTertiary, "当前没有配置作者源。先去“ACR 源管理”添加 publisher.json 地址。");
            return;
        }

        if (enabledSourceCount == 0)
        {
            ImGui.TextColored(Theme.Colors.TextTertiary, "当前没有启用的作者源。请在“ACR 源管理”启用至少一个源。");
            return;
        }

        ImGui.TextColored(Theme.Colors.TextTertiary, "当前源还没有缓存 ACR 列表。点击“刷新全部源”拉取 manifest。");
    }

    private static string DisplayJob(Jobs job) => job == Jobs.None ? "未声明职业" : job.ToString();

    private static void TryDeleteInstall(string? installDir)
    {
        if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir))
            return;

        Directory.Delete(installDir, recursive: true);
    }

    private static void DrawDisabledScope(bool disabled)
    {
        if (disabled)
            ImGui.BeginDisabled();
    }

    private static void EndDisabledScope(bool disabled)
    {
        if (disabled)
            ImGui.EndDisabled();
    }
}
