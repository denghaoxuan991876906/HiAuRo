using HiAuRo.ACR;

namespace HiAuRo.Runtime;

internal sealed class BasicAcrEntry : IRotationEntry
{
    private readonly Jobs _targetJob;
    private readonly List<SlotResolverData> _resolvers;
    private readonly Jobs[] _targetJobs;

    internal BasicAcrEntry(
        string scriptTypeName,
        Jobs targetJob,
        IReadOnlyList<SlotResolverData> resolvers)
    {
        ScriptTypeName = scriptTypeName;
        _targetJob = targetJob;
        _resolvers = resolvers.ToList();
        _targetJobs = [targetJob];
    }

    internal string ScriptTypeName { get; }

    public string AuthorName => $"Basic ACR: {ScriptTypeName}";

    public AcrType AcrType => AcrType.PvE;

    public bool UseCustomUi => false;

    public IEnumerable<Jobs> TargetJobs => _targetJobs;

    public Rotation Build(string settingFolder) => new()
    {
        TargetJob = _targetJob,
        AcrType = AcrType.PvE,
        MinLevel = 1,
        MaxLevel = 100,
        Description = AuthorName,
        SlotResolvers = _resolvers.ToList(),
    };

    public IRotationUI? GetRotationUI() => null;

    public void OnDrawSetting() { }

    public void OnEnterRotation() { }

    public void OnExitRotation() { }

    public void Dispose() { }
}
