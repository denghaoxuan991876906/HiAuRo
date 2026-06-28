using HiAuRo.ACR;

namespace HiAuRo.Runtime.AcrDistribution;

public static class AcrCatalogBuilder
{
    private static readonly Jobs[] JobOrder =
    [
        Jobs.PLD, Jobs.WAR, Jobs.DRK, Jobs.GNB,
        Jobs.WHM, Jobs.SCH, Jobs.AST, Jobs.SGE,
        Jobs.MNK, Jobs.DRG, Jobs.NIN, Jobs.SAM, Jobs.RPR, Jobs.VPR,
        Jobs.BRD, Jobs.MCH, Jobs.DNC,
        Jobs.BLM, Jobs.SMN, Jobs.RDM, Jobs.PCT,
        Jobs.None
    ];

    private static readonly AcrCatalogJobRoleGroup[] JobRoleGroups =
    [
        new("防护", [Jobs.PLD, Jobs.WAR, Jobs.DRK, Jobs.GNB]),
        new("治疗", [Jobs.WHM, Jobs.SCH, Jobs.AST, Jobs.SGE]),
        new("近战", [Jobs.MNK, Jobs.DRG, Jobs.NIN, Jobs.SAM, Jobs.RPR, Jobs.VPR]),
        new("远敏", [Jobs.BRD, Jobs.MCH, Jobs.DNC]),
        new("法系", [Jobs.BLM, Jobs.SMN, Jobs.RDM, Jobs.PCT])
    ];

    public static IReadOnlyList<AcrCatalogCard> BuildCards(
        IEnumerable<AcrSourceRecord> sources,
        IEnumerable<InstalledAcrRecord> installed)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(installed);

        var installedByKey = installed
            .GroupBy(x => x.InstallKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var cards = new List<AcrCatalogCard>();

        foreach (var source in sources.Where(x => x.Enabled))
        {
            foreach (var acr in source.Acrs)
            {
                var installKey = BuildInstallKey(source.PublisherId, acr.AcrId);
                installedByKey.TryGetValue(installKey, out var installedRecord);

                var manifest = acr.CachedManifest;
                var supportedJobs = ResolveSupportedJobs(manifest?.Targets ?? installedRecord?.Targets ?? []);
                var updateStatus = manifest != null && installedRecord != null
                    ? AcrUpdateService.CheckForUpdate(installedRecord, manifest).Status
                    : AcrUpdateStatus.UpToDate;

                cards.Add(new AcrCatalogCard(
                    InstallKey: installKey,
                    PublisherId: source.PublisherId,
                    PublisherName: source.PublisherName,
                    SourceUrl: source.Url,
                    AcrId: acr.AcrId,
                    ManifestUrl: acr.ManifestUrl,
                    DisplayName: FirstNonEmpty(manifest?.Name, acr.Name, installedRecord?.DisplayName, acr.AcrId),
                    AuthorName: FirstNonEmpty(manifest?.Author, source.PublisherName, source.PublisherId),
                    Description: manifest?.Description ?? "",
                    Version: FirstNonEmpty(manifest?.LatestVersion, installedRecord?.Version),
                    InstalledVersion: installedRecord?.Version ?? "",
                    PublishedAt: ParseDateTimeOffset(manifest?.PublishedAt),
                    SupportedJobs: supportedJobs,
                    Installed: installedRecord != null,
                    HasManifest: manifest != null,
                    ManifestError: acr.ManifestLastError,
                    InstallDir: installedRecord?.InstallDir ?? "",
                    UpdateStatus: updateStatus,
                    InstalledRecord: installedRecord,
                    Manifest: manifest,
                    Source: source));
            }
        }

        return SortCards(cards)
            .GroupBy(x => x.InstallKey, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    public static IReadOnlyList<AcrCatalogJobGroup> BuildJobGroups(
        IEnumerable<AcrCatalogCard> cards,
        Jobs filter)
    {
        ArgumentNullException.ThrowIfNull(cards);

        var pairs = cards
            .SelectMany(card => card.SupportedJobs.Select(job => (job, card)))
            .Where(x => filter == Jobs.None || x.job == filter)
            .GroupBy(x => x.job, x => x.card)
            .Select(group => new AcrCatalogJobGroup(
                group.Key,
                SortCards(group).ToList()))
            .OrderBy(x => GetJobOrder(x.Job))
            .ToList();

        return pairs;
    }

    public static IReadOnlyList<Jobs> GetAvailableJobs(IEnumerable<AcrCatalogCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        return cards
            .SelectMany(x => x.SupportedJobs)
            .Distinct()
            .OrderBy(GetJobOrder)
            .ToList();
    }

    public static IReadOnlyList<AcrCatalogJobRoleGroup> BuildAvailableJobRoleGroups(IEnumerable<AcrCatalogCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        var available = GetAvailableJobs(cards).Where(x => x != Jobs.None).ToHashSet();
        return JobRoleGroups
            .Select(group => new AcrCatalogJobRoleGroup(
                group.Label,
                group.Jobs.Where(available.Contains).ToList()))
            .Where(group => group.Jobs.Count > 0)
            .ToList();
    }

    public static string BuildInstallKey(string publisherId, string acrId) => $"{publisherId}.{acrId}";

    private static IOrderedEnumerable<AcrCatalogCard> SortCards(IEnumerable<AcrCatalogCard> cards)
    {
        return cards
            .OrderByDescending(x => x.Installed)
            .ThenByDescending(x => x.PublishedAt ?? DateTimeOffset.MinValue)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<Jobs> ResolveSupportedJobs(IEnumerable<string> targetTexts)
    {
        var jobs = targetTexts
            .Select(ParseJob)
            .Where(x => x != Jobs.None)
            .Distinct()
            .OrderBy(GetJobOrder)
            .ToList();

        if (jobs.Count == 0)
            jobs.Add(Jobs.None);

        return jobs;
    }

    private static Jobs ParseJob(string jobText)
    {
        if (Enum.TryParse<Jobs>(jobText, ignoreCase: true, out var job))
            return job;

        if (uint.TryParse(jobText, out var rowId) && Enum.IsDefined(typeof(Jobs), (int)rowId))
            return (Jobs)rowId;

        return Jobs.None;
    }

    private static int GetJobOrder(Jobs job)
    {
        var index = Array.IndexOf(JobOrder, job);
        return index >= 0 ? index : int.MaxValue;
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return DateTimeOffset.TryParse(text, out var parsed) ? parsed : null;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return "";
    }
}

public sealed record AcrCatalogCard(
    string InstallKey,
    string PublisherId,
    string PublisherName,
    string SourceUrl,
    string AcrId,
    string ManifestUrl,
    string DisplayName,
    string AuthorName,
    string Description,
    string Version,
    string InstalledVersion,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<Jobs> SupportedJobs,
    bool Installed,
    bool HasManifest,
    string? ManifestError,
    string InstallDir,
    AcrUpdateStatus UpdateStatus,
    InstalledAcrRecord? InstalledRecord,
    AcrManifestDto? Manifest,
    AcrSourceRecord? Source);

public sealed record AcrCatalogJobGroup(Jobs Job, IReadOnlyList<AcrCatalogCard> Cards);

public sealed record AcrCatalogJobRoleGroup(string Label, IReadOnlyList<Jobs> Jobs);
