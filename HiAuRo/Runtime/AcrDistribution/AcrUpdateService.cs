namespace HiAuRo.Runtime.AcrDistribution;

public static class AcrUpdateService
{
    public static AcrUpdateCheckResult CheckForUpdate(InstalledAcrRecord local, AcrManifestDto remote)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(remote);

        if (!TryParseComparableVersion(local.Version, out var localVersion))
            return new AcrUpdateCheckResult(AcrUpdateStatus.InvalidLocalVersion);

        if (!TryParseComparableVersion(remote.LatestVersion, out var remoteVersion))
            return new AcrUpdateCheckResult(AcrUpdateStatus.InvalidRemoteVersion);

        return new AcrUpdateCheckResult(
            remoteVersion > localVersion
                ? AcrUpdateStatus.UpdateAvailable
                : AcrUpdateStatus.UpToDate);
    }

    public static bool HasUpdate(InstalledAcrRecord local, AcrManifestDto remote)
    {
        return CheckForUpdate(local, remote).HasUpdate;
    }

    private static bool TryParseComparableVersion(string versionText, out Version version)
    {
        if (!Version.TryParse(versionText, out var parsed))
        {
            version = new Version();
            return false;
        }

        version = NormalizeVersion(parsed);
        return true;
    }

    private static Version NormalizeVersion(Version version)
    {
        var build = version.Build >= 0 ? version.Build : 0;
        var revision = version.Revision >= 0 ? version.Revision : 0;
        return new Version(version.Major, version.Minor, build, revision);
    }
}
