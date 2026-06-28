namespace HiAuRo.Runtime.AcrDistribution;

public sealed class AcrSourceRecord
{
    public string Url { get; set; } = "";
    public string PublisherId { get; set; } = "";
    public string PublisherName { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset? LastSyncAt { get; set; }
    public string? ETag { get; set; }
    public string? LastModified { get; set; }
    public string? LastError { get; set; }
    public List<PublisherAcrRefDto> Acrs { get; set; } = [];
}

public sealed class InstalledAcrRecord
{
    public string InstallKey { get; set; } = "";
    public string PublisherId { get; set; } = "";
    public string PublisherName { get; set; } = "";
    public string AcrId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Version { get; set; } = "";
    public string InstallDir { get; set; } = "";
    public string EntryDll { get; set; } = "";
    public string SettingDir { get; set; } = "";
    public List<string> Targets { get; set; } = [];
    public string? SourceUrl { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PublisherIndexDto
{
    public int SchemaVersion { get; set; }
    public string PublisherId { get; set; } = "";
    public string PublisherName { get; set; } = "";
    public string? Homepage { get; set; }
    public string? Contact { get; set; }
    public List<PublisherAcrRefDto> Acrs { get; set; } = [];
}

public sealed class PublisherAcrRefDto
{
    public string AcrId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ManifestUrl { get; set; } = "";
}

public sealed class AcrManifestDto
{
    public int SchemaVersion { get; set; }
    public string AcrId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Targets { get; set; } = [];
    public string LatestVersion { get; set; } = "";
    public string MinHiAuRoVersion { get; set; } = "";
    public string MaxHiAuRoVersion { get; set; } = "";
    public string PackageUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string PublishedAt { get; set; } = "";
    public string Changelog { get; set; } = "";
    public string EntryDll { get; set; } = "";
}

public enum AcrUpdateStatus
{
    UpToDate = 0,
    UpdateAvailable = 1,
    InvalidLocalVersion = 2,
    InvalidRemoteVersion = 3
}

public readonly record struct AcrUpdateCheckResult(AcrUpdateStatus Status)
{
    public bool HasUpdate => Status == AcrUpdateStatus.UpdateAvailable;
}
