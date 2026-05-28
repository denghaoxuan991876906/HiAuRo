using System.Reflection;
using HiAuRo.ACR;
using HiAuRo.Infrastructure;
using HiAuRo.UI;

namespace HiAuRo.Setting;

/// <summary>
/// 全局 + 职业 + ACR 设置管理器
/// 目录结构: {configDir}/setting/ACR/{author}/  → {author}-{jobName}.json, _global.json
/// </summary>
public static class SettingMgr
{
    private static string? _configDir;

    /// <summary>配置目录路径</summary>
    public static string ConfigDirectory => _configDir ?? string.Empty;

    /// <summary>初始化设置管理器</summary>
    public static void Init(string configDir)
    {
        _configDir = configDir;
        if (!Directory.Exists(configDir))
            Directory.CreateDirectory(configDir);
    }

    #region 全局 / 职业设置

    /// <summary>获取全局设置</summary>
    public static T GetSetting<T>() where T : class, new()
    {
        var path = GetGlobalSettingPath<T>();
        return Load<T>(path) ?? new T();
    }

    /// <summary>获取职业设置</summary>
    public static T GetJobSetting<T>(string jobName) where T : class, new()
    {
        var path = GetJobSettingPath<T>(jobName);
        return Load<T>(path) ?? new T();
    }

    /// <summary>保存全局设置</summary>
    public static void SaveSetting<T>(T setting) where T : class
    {
        var path = GetGlobalSettingPath<T>();
        Save(path, setting);
    }

    /// <summary>保存职业设置</summary>
    public static void SaveJobSetting<T>(string jobName, T setting) where T : class
    {
        var path = GetJobSettingPath<T>(jobName);
        Save(path, setting);
    }

    #endregion

    #region ACR 设置

    /// <summary>jobId → Jobs 枚举名（如 23 → "BRD"）</summary>
    private static string JobIdToName(uint jobId)
    {
        if (Enum.IsDefined(typeof(Jobs), (int)jobId))
            return ((Jobs)jobId).ToString();
        return jobId.ToString();
    }

    /// <summary>ACR 设置根目录: {configDir}/setting/ACR/</summary>
    public static string GetAcrRootDir() =>
        Path.Combine(ConfigDirectory, "setting", "ACR");

    /// <summary>获取 ACR 作者配置目录: {configDir}/setting/ACR/{author}/</summary>
    public static string GetAcrDir(string author) =>
        Path.Combine(GetAcrRootDir(), author);

    /// <summary>获取 ACR 职业设置路径: {configDir}/setting/ACR/{author}/{author}-{jobName}.json</summary>
    public static string GetAcrJobPath(string author, uint jobId) =>
        Path.Combine(GetAcrDir(author), $"{author}-{JobIdToName(jobId)}.json");

    /// <summary>获取 ACR 全局设置路径: {configDir}/setting/ACR/{author}/_global.json</summary>
    public static string GetAcrGlobalPath(string author) =>
        Path.Combine(GetAcrDir(author), "_global.json");

    /// <summary>读取 ACR 职业设置</summary>
    public static T GetAcrJobSetting<T>(string author, uint jobId) where T : class, new()
    {
        var path = GetAcrJobPath(author, jobId);
        return Load<T>(path) ?? new T();
    }

    /// <summary>保存 ACR 职业设置</summary>
    public static void SaveAcrJobSetting<T>(string author, uint jobId, T setting) where T : class
    {
        var path = GetAcrJobPath(author, jobId);
        Save(path, setting);
    }

    /// <summary>读取 ACR 全局设置（跨职业共享）</summary>
    public static T GetAcrGlobalSetting<T>(string author) where T : class, new()
    {
        var path = GetAcrGlobalPath(author);
        return Load<T>(path) ?? new T();
    }

    /// <summary>保存 ACR 全局设置（跨职业共享）</summary>
    public static void SaveAcrGlobalSetting<T>(string author, T setting) where T : class
    {
        var path = GetAcrGlobalPath(author);
        Save(path, setting);
    }

    /// <summary>
    /// 将 AcrSettings 的 public 属性值同步到 ControlValues
    /// </summary>
    public static void SyncControlsFromSettings(
        AcrSettings settings,
        Dictionary<string, object> controlValues,
        List<UiControlDef> controls)
    {
        if (settings == null || controlValues == null || controls == null) return;

        var props = settings.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToArray();

        foreach (var ctrl in controls)
        {
            var prop = FindMatchingProperty(props, ctrl.Label);
            if (prop == null) continue;

            try
            {
                var val = prop.GetValue(settings);
                if (val != null)
                    controlValues[ctrl.Id] = val;
            }
            catch { /* 类型不兼容则跳过 */ }
        }
    }

    /// <summary>
    /// 将 ControlValues 写回 AcrSettings 的 public 属性（label→属性名匹配）
    /// </summary>
    public static void SyncSettingsFromControls(
        AcrSettings settings,
        Dictionary<string, object> controlValues,
        List<UiControlDef> controls)
    {
        if (settings == null || controlValues == null || controls == null) return;

        var props = settings.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToArray();

        foreach (var ctrl in controls)
        {
            if (!controlValues.TryGetValue(ctrl.Id, out var val)) continue;

            var prop = FindMatchingProperty(props, ctrl.Label);
            if (prop == null) continue;

            try
            {
                var converted = Convert.ChangeType(val, prop.PropertyType);
                prop.SetValue(settings, converted);
            }
            catch { /* 类型不兼容则跳过 */ }
        }
    }

    /// <summary>
    /// 按 label 匹配属性：精确匹配 → 去掉括号后缀匹配
    /// </summary>
    private static PropertyInfo? FindMatchingProperty(PropertyInfo[] props, string label)
    {
        // 1. 精确匹配
        var exact = props.FirstOrDefault(p => p.Name == label);
        if (exact != null) return exact;

        // 2. 去掉 label 末尾括号后缀（如 "续红斩时间阈值(s)" → "续红斩时间阈值"）
        var parenIdx = label.LastIndexOf('(');
        if (parenIdx > 0)
        {
            var stripped = label[..parenIdx];
            var match = props.FirstOrDefault(p => p.Name == stripped);
            if (match != null) return match;
        }

        return null;
    }

    #endregion

    #region 底层持久化

    private static string GetGlobalSettingPath<T>() =>
        Path.Combine(ConfigDirectory, $"{typeof(T).Name}.json");

    private static string GetJobSettingPath<T>(string jobName) =>
        Path.Combine(ConfigDirectory, jobName, $"{typeof(T).Name}.json");

    private static T? Load<T>(string path) where T : class, new()
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return System.Text.Json.JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[Setting] 加载失败: {path}, {ex.Message}");
            return null;
        }
    }

    private static void Save<T>(string path, T setting) where T : class
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = System.Text.Json.JsonSerializer.Serialize(setting, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[Setting] 保存失败: {path}, {ex.Message}");
        }
    }

    #endregion
}
