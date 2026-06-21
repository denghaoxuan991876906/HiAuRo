using System.Reflection;
using System.Text.Json;
using HiAuRo.Runtime;

namespace HiAuRo.Execution;

/// <summary>
/// 统一导出本地触发器目录，并让运行时类型注册与目录来源保持一致。
/// </summary>
public static class TriggerCatalogExporter
{
    public static TriggerCatalog BuildMergedCatalog()
    {
        var merged = new TriggerCatalog();
        TriggerCatalogBuilder.MergeInto(merged,
            TriggerCatalogBuilder.BuildFromAssembly(typeof(TriggerCatalogExporter).Assembly, "builtin"));

        foreach (var acrAsm in ACRLoader.LoadedAcrAssemblies)
            TriggerCatalogBuilder.MergeInto(merged, TriggerCatalogBuilder.BuildFromAssembly(acrAsm, "acr"));

        foreach (var pluginAsm in PluginLoader.PluginAssemblies)
            TriggerCatalogBuilder.MergeInto(merged, TriggerCatalogBuilder.BuildFromAssembly(pluginAsm, "plugin"));

        return merged;
    }

    public static void RebuildRuntimeTypeRegistry()
    {
        var assemblies = EnumerateTriggerAssemblies();
        ExecutionJsonLoader.RebuildFromAssemblies(assemblies);
    }

    public static void ExportToConfigDirectory(string configDirectory)
    {
        var merged = BuildMergedCatalog();
        var catalogPath = Path.Combine(configDirectory, "trigger-catalog.json");
        var catalogJson = JsonSerializer.Serialize(merged,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        File.WriteAllText(catalogPath, catalogJson);
        DService.Instance().Log.Information(
            $"[TriggerCatalog] 已生成 ({merged.Conditions.Count}C {merged.Actions.Count}A {merged.Scripts.Count}S)");
    }

    internal static IEnumerable<Assembly> EnumerateTriggerAssemblies()
    {
        yield return typeof(TriggerCatalogExporter).Assembly;

        foreach (var acrAsm in ACRLoader.LoadedAcrAssemblies)
            yield return acrAsm;

        foreach (var pluginAsm in PluginLoader.PluginAssemblies)
            yield return pluginAsm;
    }
}
