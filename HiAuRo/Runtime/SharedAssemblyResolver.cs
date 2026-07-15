using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace HiAuRo.Runtime;

internal static class SharedAssemblyResolver
{
    private static readonly HashSet<string> ReservedHostAssemblyNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "HiAuRo",
            "HiAuRo.Helper",
            "OmenTools",
        };

    private static readonly string[] ReservedHostAssemblyPrefixes =
    [
        "Dalamud",
        "FFXIVClientStructs",
        "Lumina",
        "ImGui",
        "TerraFX",
    ];

    private static readonly string[] FrameworkAssemblyPrefixes =
    [
        "System.",
        "Microsoft.",
    ];

    private static readonly HashSet<string> TrustedPlatformAssemblyNames =
        GetTrustedPlatformAssemblyNames();

    internal sealed record Binding(
        Assembly Assembly,
        byte[]? AssemblyBytes,
        bool IsExtension);

    internal sealed class Snapshot
    {
        private readonly IReadOnlyDictionary<string, Binding> _bindings;

        internal Snapshot(IReadOnlyDictionary<string, Binding> bindings)
        {
            _bindings = bindings;
        }

        internal Assembly? Resolve(AssemblyName requestedName)
        {
            var name = requestedName.Name;
            if (string.IsNullOrEmpty(name))
                return null;
            if (_bindings.TryGetValue(name, out var binding))
                return binding.Assembly;
            if (!TrustedPlatformAssemblyNames.Contains(name))
                return null;

            try { return AssemblyLoadContext.Default.LoadFromAssemblyName(requestedName); }
            catch { return null; }
        }

        internal bool IsHost(string? name)
        {
            if (IsReservedHostAssemblyName(name)
                || (!string.IsNullOrEmpty(name) && TrustedPlatformAssemblyNames.Contains(name)))
                return true;
            return !string.IsNullOrEmpty(name)
                && _bindings.TryGetValue(name, out var binding)
                && !binding.IsExtension;
        }

        internal bool IsShared(string? name) =>
            IsHost(name)
            || (!string.IsNullOrEmpty(name) && _bindings.ContainsKey(name));

        internal MetadataReference[] GetMetadataReferences(
            Func<string, bool> isAllowed,
            bool includeExtensions)
        {
            var references = new List<MetadataReference>(_bindings.Count);
            foreach (var (name, binding) in _bindings)
            {
                if (!isAllowed(name) && !(includeExtensions && binding.IsExtension))
                    continue;

                var reference = CreateMetadataReference(binding);
                if (reference is not null)
                    references.Add(reference);
            }

            return references.ToArray();
        }
    }

    internal static Snapshot Capture() => Capture(includeExtensions: true);

    internal static Snapshot CaptureHost() => Capture(includeExtensions: false);

    private static Snapshot Capture(bool includeExtensions)
    {
        var bindings = new Dictionary<string, Binding>(StringComparer.OrdinalIgnoreCase);

        var helperSnapshot = HelperUpdater.HelperSnapshot;
        if (helperSnapshot?.Assembly.GetName().Name == "HiAuRo.Helper")
            TryAdd(bindings, helperSnapshot.Assembly, helperSnapshot.AssemblyBytes, false);

        var hostRoots = new[]
        {
            typeof(SharedAssemblyResolver).Assembly,
            typeof(OmenTools.OmenService.GameState).Assembly,
            typeof(Dalamud.Plugin.IDalamudPlugin).Assembly,
            typeof(object).Assembly,
            typeof(List<>).Assembly,
            typeof(Enumerable).Assembly,
            typeof(System.Numerics.Vector3).Assembly,
        };

        foreach (var assembly in hostRoots)
        {
            TryAdd(bindings, assembly, null, false);
            AddHostContextAssemblies(bindings, AssemblyLoadContext.GetLoadContext(assembly));
        }

        AddHostContextAssemblies(bindings, AssemblyLoadContext.Default);

        if (includeExtensions)
        {
            foreach (var record in PluginLoader.Plugins.Values)
                TryAdd(bindings, record.Assembly, record.DllBytes, true);

            foreach (var assembly in ACRLoader.LoadedAcrAssemblies)
                TryAdd(bindings, assembly, null, true);
        }

        return new Snapshot(bindings);
    }

    internal static bool IsReservedHostAssemblyName(string? name) =>
        !string.IsNullOrEmpty(name)
        && (ReservedHostAssemblyNames.Contains(name)
            || ReservedHostAssemblyPrefixes.Any(prefix =>
                name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

    internal static bool IsHostAssemblyFile(string path, Snapshot bindings)
    {
        try { return bindings.IsHost(AssemblyName.GetAssemblyName(path).Name); }
        catch { return false; }
    }

    internal static void PreloadDependencies(
        AssemblyLoadContext context,
        Assembly rootAssembly,
        Snapshot bindings,
        Func<AssemblyName, Assembly?> resolvePrivate,
        string owner,
        string? allowedHostRootName = null)
    {
        var rootName = rootAssembly.GetName();
        if (!string.Equals(rootName.Name, allowedHostRootName, StringComparison.OrdinalIgnoreCase)
            && bindings.IsHost(rootName.Name)
            && !ReferenceEquals(rootAssembly, bindings.Resolve(rootName)))
            throw new InvalidOperationException(
                $"{owner} 根程序集与宿主实例冲突: {rootName.Name}");

        var pending = new Stack<Assembly>();
        var visited = new HashSet<Assembly>(ReferenceEqualityComparer.Instance);
        pending.Push(rootAssembly);

        while (pending.TryPop(out var assembly))
        {
            if (!visited.Add(assembly))
                continue;

            foreach (var refName in assembly.GetReferencedAssemblies())
            {
                var isHost = bindings.IsHost(refName.Name);
                Assembly? resolved;
                try
                {
                    resolved = context.LoadFromAssemblyName(refName);
                }
                catch when (!isHost)
                {
                    resolved = resolvePrivate(refName);
                }

                if (resolved is null)
                    throw new FileNotFoundException(
                        $"{owner} 依赖程序集未加载: {refName.FullName}");

                if (isHost)
                {
                    if (!ReferenceEquals(resolved, bindings.Resolve(refName)))
                        throw new InvalidOperationException(
                            $"{owner} 宿主程序集实例不一致: {refName.Name}");
                    continue;
                }

                if (!ReferenceEquals(AssemblyLoadContext.GetLoadContext(resolved), context))
                    throw new InvalidOperationException(
                        $"{owner} 私有依赖未隔离: {refName.Name}");
                pending.Push(resolved);
            }
        }
    }

    private static void AddHostContextAssemblies(
        IDictionary<string, Binding> bindings,
        AssemblyLoadContext? context)
    {
        if (context is null)
            return;

        foreach (var assembly in context.Assemblies)
        {
            var name = assembly.GetName().Name;
            if (!IsHostCandidateName(name))
                continue;

            TryAdd(bindings, assembly, null, false);
        }
    }

    private static bool IsHostCandidateName(string? name) =>
        !string.IsNullOrEmpty(name)
        && (IsReservedHostAssemblyName(name)
            || name is "System" or "mscorlib" or "netstandard"
            || TrustedPlatformAssemblyNames.Contains(name)
            || FrameworkAssemblyPrefixes.Any(prefix =>
                name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

    private static HashSet<string> GetTrustedPlatformAssemblyNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string paths)
            return names;

        foreach (var path in paths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrEmpty(name))
                names.Add(name);
        }

        return names;
    }

    private static void TryAdd(
        IDictionary<string, Binding> bindings,
        Assembly assembly,
        byte[]? assemblyBytes,
        bool isExtension)
    {
        if (assembly.IsDynamic)
            return;

        var name = assembly.GetName().Name;
        if (string.IsNullOrEmpty(name) || bindings.ContainsKey(name))
            return;

        bindings[name] = new Binding(assembly, assemblyBytes, isExtension);
    }

    private static unsafe MetadataReference? CreateMetadataReference(Binding binding)
    {
        if (binding.AssemblyBytes is not null)
        {
            try { return MetadataReference.CreateFromImage(binding.AssemblyBytes); }
            catch { }
        }

        try
        {
            if (binding.Assembly.TryGetRawMetadata(out var metadata, out var size))
            {
                var module = ModuleMetadata.CreateFromMetadata((IntPtr)metadata, size);
                return AssemblyMetadata.Create(module)
                    .GetReference(display: binding.Assembly.FullName);
            }
        }
        catch { }

        return null;
    }
}
