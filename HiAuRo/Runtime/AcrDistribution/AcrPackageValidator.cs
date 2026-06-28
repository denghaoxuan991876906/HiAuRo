namespace HiAuRo.Runtime.AcrDistribution;

public static class AcrPackageValidator
{
    private static readonly string[] ReservedInstallKeys = ["_tmp", ".recover", ".", ".."];
    private static readonly string[] WindowsReservedDeviceNames =
    [
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9"
    ];
    private static readonly Dictionary<char, char> SuperscriptDigitMap = new()
    {
        ['¹'] = '1',
        ['²'] = '2',
        ['³'] = '3'
    };

    public readonly record struct ValidationResult(bool IsValid, string? Error)
    {
        public static ValidationResult Success() => new(true, null);

        public static ValidationResult Fail(string error)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(error);
            return new ValidationResult(false, error);
        }
    }

    public static ValidationResult ValidateInstallKey(string installKey)
    {
        if (string.IsNullOrWhiteSpace(installKey))
            return ValidationResult.Fail("installKey 不能为空或空白");

        if (ReservedInstallKeys.Any(name => string.Equals(name, installKey, StringComparison.OrdinalIgnoreCase)))
            return ValidationResult.Fail($"installKey 不能为保留名 {installKey}");

        var deviceStem = GetWindowsDeviceStem(installKey);
        if (WindowsReservedDeviceNames.Any(name => string.Equals(name, deviceStem, StringComparison.OrdinalIgnoreCase)))
            return ValidationResult.Fail($"installKey 不能为 Windows 设备保留名 {installKey}");

        if (installKey.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return ValidationResult.Fail("installKey 包含非法文件名字符");

        if (installKey.Contains(Path.DirectorySeparatorChar) || installKey.Contains(Path.AltDirectorySeparatorChar))
            return ValidationResult.Fail("installKey 不能包含路径分隔符");

        if (Path.IsPathRooted(installKey))
            return ValidationResult.Fail("installKey 不能是绝对路径");

        if (installKey.Contains("..", StringComparison.Ordinal))
            return ValidationResult.Fail("installKey 不能包含路径回退片段");

        if (installKey.EndsWith(' ') || installKey.EndsWith('.'))
            return ValidationResult.Fail("installKey 不能以空格或点结尾");

        return ValidationResult.Success();
    }

    private static string GetWindowsDeviceStem(string installKey)
    {
        var stem = installKey.Split('.', 2)[0];
        if (stem.IndexOfAny(['¹', '²', '³']) < 0)
            return stem;

        Span<char> chars = stackalloc char[stem.Length];
        for (var i = 0; i < stem.Length; i++)
            chars[i] = SuperscriptDigitMap.GetValueOrDefault(stem[i], stem[i]);

        return new string(chars);
    }

    public static ValidationResult ValidateInstalledMetadata(
        string installKey,
        string metadataFileName,
        string entryDllFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryDllFileName);

        var installKeyValidation = ValidateInstallKey(installKey);
        if (!installKeyValidation.IsValid)
            return installKeyValidation;

        var expectedJson = installKey + ".json";
        if (!string.Equals(metadataFileName, expectedJson, StringComparison.OrdinalIgnoreCase))
            return ValidationResult.Fail($"元数据文件名必须为 {expectedJson}");

        var expectedDll = installKey + ".dll";
        if (!string.Equals(entryDllFileName, expectedDll, StringComparison.OrdinalIgnoreCase))
            return ValidationResult.Fail($"主 DLL 文件名必须为 {expectedDll}");

        return ValidationResult.Success();
    }

    public static ValidationResult ValidatePreparedDirectory(string installKey, string preparedDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preparedDir);

        var installKeyValidation = ValidateInstallKey(installKey);
        if (!installKeyValidation.IsValid)
            return installKeyValidation;

        var metadataPath = Path.Combine(preparedDir, installKey + ".json");
        if (!File.Exists(metadataPath))
            return ValidationResult.Fail($"缺少同名元数据文件 {installKey}.json");

        var dllPath = Path.Combine(preparedDir, installKey + ".dll");
        if (!File.Exists(dllPath))
            return ValidationResult.Fail($"缺少同名主 DLL {installKey}.dll");

        return ValidateInstalledMetadata(
            installKey,
            Path.GetFileName(metadataPath),
            Path.GetFileName(dllPath));
    }
}
