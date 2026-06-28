using System.Text.Json;

namespace HiAuRo.Runtime.AcrDistribution;

public sealed class AcrInstallationStore
{
    private readonly string _acrRoot;
    private readonly string _recoveryRoot;

    public AcrInstallationStore(string acrRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acrRoot);
        _acrRoot = acrRoot;
        _recoveryRoot = Path.Combine(_acrRoot, ".recover");
        Directory.CreateDirectory(_acrRoot);
        Directory.CreateDirectory(_recoveryRoot);
    }

    public List<InstalledAcrRecord> LoadInstalled()
    {
        RecoverInterruptedInstalls();

        var installed = new List<InstalledAcrRecord>();

        foreach (var installDir in Directory.GetDirectories(_acrRoot))
        {
            var installKey = Path.GetFileName(installDir);
            var installKeyValidation = AcrPackageValidator.ValidateInstallKey(installKey);
            if (!installKeyValidation.IsValid)
                continue;

            var metadataPath = Path.Combine(installDir, installKey + ".json");
            if (!File.Exists(metadataPath))
                continue;

            InstalledAcrRecord? record;
            try
            {
                var json = File.ReadAllText(metadataPath);
                record = JsonSerializer.Deserialize<InstalledAcrRecord>(json);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (record == null)
                continue;

            record.InstallKey = installKey;
            record.InstallDir = installDir;
            record.EntryDll = installKey + ".dll";
            record.SettingDir = GetSettingDir(installKey);
            installed.Add(record);
        }

        return installed;
    }

    private void RecoverInterruptedInstalls()
    {
        if (!Directory.Exists(_recoveryRoot))
            return;

        foreach (var pendingMarkerPath in Directory.GetFiles(_recoveryRoot, "*.pending", SearchOption.TopDirectoryOnly))
        {
            var installKey = Path.GetFileNameWithoutExtension(pendingMarkerPath);
            var installKeyValidation = AcrPackageValidator.ValidateInstallKey(installKey);
            if (!installKeyValidation.IsValid)
                continue;

            if (AcrPackageInstaller.IsInstallActive(installKey))
                continue;

            var backupDir = Path.Combine(_recoveryRoot, installKey);
            var targetDir = Path.Combine(_acrRoot, installKey);
            if (Directory.Exists(targetDir))
            {
                TryDeleteFile(pendingMarkerPath);
                TryDeleteDirectory(backupDir);
                continue;
            }

            if (!Directory.Exists(backupDir))
            {
                TryDeleteFile(pendingMarkerPath);
                continue;
            }

            try
            {
                Directory.Move(backupDir, targetDir);
                TryDeleteFile(pendingMarkerPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }
        }
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
}
