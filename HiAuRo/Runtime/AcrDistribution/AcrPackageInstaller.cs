using System.Text.Json;
using System.Security.Cryptography;
using System.IO.Compression;

namespace HiAuRo.Runtime.AcrDistribution;

public sealed class AcrPackageInstaller
{
    private static readonly Lock ActiveInstallsGate = new();
    private static readonly HashSet<string> ActiveInstalls = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _acrRoot;
    private readonly string _recoveryRoot;
    private readonly string _tempRoot;

    public AcrPackageInstaller(string acrRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acrRoot);

        _acrRoot = acrRoot;
        _recoveryRoot = Path.Combine(_acrRoot, ".recover");
        _tempRoot = Path.Combine(_acrRoot, "_tmp");
        Directory.CreateDirectory(_acrRoot);
        Directory.CreateDirectory(_recoveryRoot);
        Directory.CreateDirectory(_tempRoot);
    }

    public void InstallFromPreparedDirectory(string installKey, Action<string> populatePreparedDir)
    {
        ArgumentNullException.ThrowIfNull(populatePreparedDir);

        var installKeyValidation = AcrPackageValidator.ValidateInstallKey(installKey);
        if (!installKeyValidation.IsValid)
            throw new InvalidOperationException(installKeyValidation.Error);

        var installKeyClaimed = false;
        var preparedDir = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
        var backupDir = Path.Combine(_recoveryRoot, installKey);
        var targetDir = Path.Combine(_acrRoot, installKey);
        var pendingMarkerPath = Path.Combine(_recoveryRoot, installKey + ".pending");
        var targetWasBackedUp = false;
        var preparedWasPublished = false;
        var backupRestored = false;
        var transactionCommitted = false;

        Directory.CreateDirectory(preparedDir);

        try
        {
            ClaimActiveInstall(installKey);
            installKeyClaimed = true;

            populatePreparedDir(preparedDir);

            var validation = AcrPackageValidator.ValidatePreparedDirectory(installKey, preparedDir);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.Error);

            WriteAuthoritativeMetadata(installKey, preparedDir, preparedDir);

            if (Directory.Exists(targetDir))
            {
                File.WriteAllText(pendingMarkerPath, "");

                if (Directory.Exists(backupDir))
                    Directory.Delete(backupDir, recursive: true);

                Directory.Move(targetDir, backupDir);
                targetWasBackedUp = true;
            }

            Directory.Move(preparedDir, targetDir);
            preparedWasPublished = true;
            WriteAuthoritativeMetadata(installKey, targetDir, targetDir);
            transactionCommitted = true;

            TryDeleteFile(pendingMarkerPath);
            TryDeleteDirectory(backupDir);
        }
        catch
        {
            if (!transactionCommitted && preparedWasPublished && Directory.Exists(targetDir))
            {
                try
                {
                    Directory.Delete(targetDir, recursive: true);
                }
                catch
                {
                }
            }

            if (!transactionCommitted && targetWasBackedUp && Directory.Exists(backupDir) && !Directory.Exists(targetDir))
            {
                try
                {
                    Directory.Move(backupDir, targetDir);
                    backupRestored = true;
                    if (File.Exists(pendingMarkerPath))
                        File.Delete(pendingMarkerPath);
                }
                catch
                {
                }
            }

            throw;
        }
        finally
        {
            TryDeleteDirectory(preparedDir);

            if (targetWasBackedUp && (backupRestored || transactionCommitted))
                TryDeleteDirectory(backupDir);

            if (installKeyClaimed)
                ReleaseActiveInstall(installKey);
        }
    }

    public async Task InstallFromManifestAsync(
        AcrManifestDto manifest,
        string publisherId,
        string publisherName,
        string sourceUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var installKey = $"{publisherId}.{manifest.AcrId}";
        using var httpClient = new HttpClient();
        await using var packageStream = await httpClient.GetStreamAsync(manifest.PackageUrl, cancellationToken);
        var packageBytes = await ReadAllBytesAsync(packageStream, cancellationToken);

        if (!string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            var actualSha = Convert.ToHexStringLower(SHA256.HashData(packageBytes));
            if (!string.Equals(actualSha, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"SHA256 校验失败，期望 {manifest.Sha256}，实际 {actualSha}");
        }

        InstallFromPreparedDirectory(installKey, preparedDir =>
        {
            using var ms = new MemoryStream(packageBytes, writable: false);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);
            archive.ExtractToDirectory(preparedDir, overwriteFiles: true);

            var metadataPath = Path.Combine(preparedDir, installKey + ".json");
            InstalledAcrRecord record;
            if (File.Exists(metadataPath))
            {
                record = JsonSerializer.Deserialize<InstalledAcrRecord>(File.ReadAllText(metadataPath)) ?? new InstalledAcrRecord();
            }
            else
            {
                record = new InstalledAcrRecord();
            }

            record.InstallKey = installKey;
            record.PublisherId = publisherId;
            record.PublisherName = publisherName;
            record.AcrId = manifest.AcrId;
            record.DisplayName = manifest.Name;
            record.Version = manifest.LatestVersion;
            record.EntryDll = installKey + ".dll";
            record.Targets = manifest.Targets;
            record.SourceUrl = sourceUrl;
            record.UpdatedAt = DateTimeOffset.UtcNow;

            if (!File.Exists(Path.Combine(preparedDir, installKey + ".dll")) &&
                !string.IsNullOrWhiteSpace(manifest.EntryDll) &&
                File.Exists(Path.Combine(preparedDir, manifest.EntryDll)))
            {
                File.Move(
                    Path.Combine(preparedDir, manifest.EntryDll),
                    Path.Combine(preparedDir, installKey + ".dll"),
                    overwrite: true);
            }

            File.WriteAllText(metadataPath, JsonSerializer.Serialize(record, MetadataJsonOptions));
        });
    }

    internal static bool IsInstallActive(string installKey)
    {
        lock (ActiveInstallsGate)
        {
            return ActiveInstalls.Contains(installKey);
        }
    }

    private static void ClaimActiveInstall(string installKey)
    {
        lock (ActiveInstallsGate)
        {
            if (!ActiveInstalls.Add(installKey))
                throw new InvalidOperationException($"installKey {installKey} 正在安装中");
        }
    }

    private static void ReleaseActiveInstall(string installKey)
    {
        lock (ActiveInstallsGate)
        {
            ActiveInstalls.Remove(installKey);
        }
    }

    private void WriteAuthoritativeMetadata(string installKey, string metadataDir, string authorityInstallDir)
    {
        var metadataPath = Path.Combine(metadataDir, installKey + ".json");
        InstalledAcrRecord record;

        try
        {
            record = JsonSerializer.Deserialize<InstalledAcrRecord>(File.ReadAllText(metadataPath)) ?? new InstalledAcrRecord();
        }
        catch (JsonException)
        {
            record = new InstalledAcrRecord();
        }

        record.InstallKey = installKey;
        record.InstallDir = authorityInstallDir;
        record.EntryDll = installKey + ".dll";
        record.SettingDir = GetSettingDir(installKey);

        File.WriteAllText(metadataPath, JsonSerializer.Serialize(record, MetadataJsonOptions));
    }

    private string GetSettingDir(string installKey)
    {
        var configDir = Directory.GetParent(_acrRoot)?.FullName ?? _acrRoot;
        return Path.Combine(configDir, "setting", "ACR", installKey);
    }

    private static void TryDeleteDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }
}
