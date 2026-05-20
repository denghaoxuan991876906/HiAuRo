using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using HiAuRo.ACR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace HiAuRo.Execution;

/// <summary>
/// C# 脚本动态编译器 — 对齐 AE 的 CSharpCodeProvider
/// 将脚本字符串编译为可执行的 ITriggerScript 实例
/// </summary>
public static class ScriptCompiler
{
    private static readonly ConcurrentDictionary<string, ITriggerScript> _cache = [];
    private static readonly List<MetadataReference> _refCache = [];
    private static readonly object _refLock = new();
    private static bool _refsLoaded;
    private static AssemblyLoadContext? _alc;

    /// <summary>允许引用的程序集名称前缀（白名单）</summary>
    private static readonly string[] AllowedAssemblyPrefixes =
    [
        "System.Runtime",
        "System.Collections",
        "System.Linq",
        "System.Numerics",
        "System.Memory",
        "System.Primitives",
        "mscorlib",
        "netstandard",
        "HiAuRo.FA",
        "HiAuRo",
        "OmenTools",
        "Dalamud",
        "FFXIVClientStructs",
        "ImGui",
        "Lumina",
    ];

    /// <summary>禁止引用的程序集（即使前缀匹配也排除）</summary>
    private static readonly string[] BlockedAssemblies =
    [
        "System.IO.FileSystem",
        "System.Net",
        "System.Diagnostics.Process",
        "System.Threading.Tasks.Parallel",
    ];

    /// <summary>编译脚本并使用缓存。同一段代码只编译一次</summary>
    public static ITriggerScript? Compile(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var key = code.Trim();
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(AddScriptWrapper(code));

            var compilation = CSharpCompilation.Create(
                $"Script_{Guid.NewGuid():N}",
                [syntaxTree],
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Release));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                var errors = string.Join("\n", result.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()));
                DService.Instance().Log.Error($"[ScriptCompiler] 编译失败:\n{errors}");
                return null;
            }

            ms.Seek(0, SeekOrigin.Begin);

            _alc ??= new AssemblyLoadContext("ScriptCompiler", isCollectible: true);
            var assembly = _alc.LoadFromStream(ms);
            var type = assembly.GetTypes().FirstOrDefault(t =>
                typeof(ITriggerScript).IsAssignableFrom(t) && !t.IsAbstract);

            if (type == null)
            {
                DService.Instance().Log.Error("[ScriptCompiler] 脚本中未找到 ITriggerScript 实现");
                return null;
            }

            var instance = (ITriggerScript)Activator.CreateInstance(type)!;
            _cache[key] = instance;
            return instance;
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Error($"[ScriptCompiler] 编译异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>清除编译缓存</summary>
    public static void ClearCache()
    {
        _cache.Clear();
        lock (_refLock)
        {
            _refCache.Clear();
            _refsLoaded = false;
        }

        // 卸载旧的 ALC，下次编译时创建新的
        if (_alc != null)
        {
            try { _alc.Unload(); } catch { }
            _alc = null;
        }
    }

    /// <summary>从内存字节注入程序集引用到编译上下文（供 PluginLoader 使用）</summary>
    public static void AddPluginReferenceFromImage(byte[] dllBytes)
    {
        lock (_refLock)
        {
            _refCache.Add(MetadataReference.CreateFromImage(dllBytes));
        }
    }

    /// <summary>批量注册所有已加载插件的程序集引用</summary>
    public static void RegisterPluginReferences()
    {
        foreach (var (name, record) in Runtime.PluginLoader.Plugins)
        {
            try
            {
                AddPluginReferenceFromImage(record.DllBytes);
                DService.Instance().Log.Debug($"[ScriptCompiler] 已注入插件程序集: {name}");
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[ScriptCompiler] 注入 {name} 失败: {ex.Message}");
            }
        }
    }

    /// <summary>收集编译引用（白名单过滤）</summary>
    private static List<MetadataReference> GetReferences()
    {
        lock (_refLock)
        {
            if (_refsLoaded) return _refCache;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                if (asm.IsDynamic || string.IsNullOrEmpty(asm.Location)) continue;
                var name = asm.GetName().Name ?? "";
                if (!IsAllowed(name)) continue;
                try
                {
                    _refCache.Add(MetadataReference.CreateFromFile(asm.Location));
                }
                catch { }
            }

            _refsLoaded = true;
            DService.Instance().Log.Debug($"[ScriptCompiler] 加载 {_refCache.Count} 个程序集引用 (白名单过滤)");
            return _refCache;
        }
    }

    private static bool IsAllowed(string name)
    {
        foreach (var blocked in BlockedAssemblies)
        {
            if (name.StartsWith(blocked, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        foreach (var prefix in AllowedAssemblyPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>给用户脚本包上 using + namespace + class</summary>
    private static string AddScriptWrapper(string userCode)
    {
        return $@"
using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using HiAuRo;
using HiAuRo.ACR;
using HiAuRo.FA.Shapes;
using static HiAuRo.Data;
using HiAuRo.Execution;
using HiAuRo.Runtime;
using OmenTools;
using OmenTools.OmenService;
using OmenTools.Extensions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace HiAuRo.Scripts;

public sealed class DynamicScript : ITriggerScript
{{
    public bool Check(ITriggerCondParams? condParams)
    {{
        {userCode}
    }}
}}
";
    }
}
