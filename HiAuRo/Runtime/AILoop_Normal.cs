using HiAuRo.ACR;

namespace HiAuRo.Runtime;

/// <summary>
/// PVE 正常循环 —— 对齐 AE
/// Check() 不区分 GCD/oGCD 窗口，所有 Resolver 每帧都调用
/// SlotMode 只控制 Build/执行时机，不控制 Check()
/// </summary>
public sealed class AILoop_Normal : IAILoop
{
    private readonly List<SlotResolverData> _resolvers;
    private readonly List<ResolverDebugInfo> _debugInfos;
    private int[] _checkResults = []; // 复用数组，避免每帧 new 分配

    /// <summary>ACR Debug 面板数据（每帧刷新，只读访问）</summary>
    public IReadOnlyList<ResolverDebugInfo> DebugResolvers => _debugInfos;

    /// <summary>Initializes a new instance of the <see cref="AILoop_Normal"/> class</summary>
    public AILoop_Normal(List<SlotResolverData> resolvers)
    {
        _resolvers = resolvers;
        _debugInfos = new List<ResolverDebugInfo>(resolvers.Count);
        foreach (var data in resolvers)
            _debugInfos.Add(new ResolverDebugInfo
            {
                Name = data.Resolver.GetType().Name,
                Mode = data.Mode
            });
    }

    public void CheckAll()
    {
        foreach (var info in _debugInfos)
        {
            info.CheckResult = -99;
            info.CheckThrew = false;
            info.CheckError = "";
            info.PassedWindow = false;
            info.BuiltSlot = false;
            info.BuiltSkills = "";
        }

        if (_resolvers.Count == 0)
        {
            DService.Instance().Log.Error("[AILoop] 没有已注册的 SlotResolver");
            return;
        }

        if (Data.Target.Current == null)
        {
            // 无目标时跳过 Check，同时清除上次的 check 结果避免 Build 读到脏数据
            if (_checkResults.Length == _resolvers.Count)
                Array.Fill(_checkResults, -99);
            return;
        }

        if (_checkResults.Length != _resolvers.Count)
            _checkResults = new int[_resolvers.Count];
        for (int i = 0; i < _resolvers.Count; i++)
        {
            var data = _resolvers[i];
            var info = _debugInfos[i];

            int checkResult;
            try
            {
                checkResult = data.Resolver.Check();
                info.CheckResult = checkResult;
            }
            catch (Exception ex)
            {
                checkResult = -99;
                info.CheckResult = -99;
                info.CheckThrew = true;
                info.CheckError = ex.Message;
                DService.Instance().Log.Error($"[AILoop] Check#{data.Resolver.GetType().Name} 异常: {ex}");
            }
            _checkResults[i] = checkResult;
        }
    }

    public Slot? Build(bool blockBuild)
    {
        if (blockBuild) return null;

        if (_resolvers.Count == 0 || _checkResults.Length != _resolvers.Count)
            return null;

        bool isGcdReady = GCDHelper.CanUseGCD();
        bool isOffGcdWindow = GCDHelper.CanUseOffGcd();
        bool is2ndAbWindow = GCDHelper.Is2ndAbilityTime();
        float gcdRemain = GCDHelper.GetGCDCooldown();

        for (int i = 0; i < _resolvers.Count; i++)
        {
            if (_checkResults[i] < 0) continue;

            var data = _resolvers[i];
            var info = _debugInfos[i];

            bool canExecute = data.Mode switch
            {
                SlotMode.Gcd    => isGcdReady,
                SlotMode.OffGcd => isOffGcdWindow
                    && Data.Combat.AbilityIntervalElapsed
                    && Data.Combat.AbilityCountInGcd < Data.Combat.MaxAbilityTimesInGcd,
                SlotMode.Always => true,
                _              => false
            };

            if (!canExecute)
            {
                info.PassedWindow = false;
                continue;
            }

            info.PassedWindow = true;

            try
            {
                var slot = new Slot();
                data.Resolver.Build(slot);
                var resolverName = data.Resolver.GetType().Name;

                info.BuiltSlot = true;
                info.BuiltSkills = string.Join(",", slot.Actions.Select(a => a.Spell.Name));

                if (data.Mode == SlotMode.Gcd)
                    Data.Combat.AbilityCountInGcd = 0;
                else if (slot.Actions.Any(a => a.Spell.IsAbility()))
                    Data.Combat.AbilityCountInGcd++;

                return slot;
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Error($"[AILoop] Build error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        return null;
    }

    [Obsolete("Use CheckAll() + Build() instead")]
    public Slot? GetNextSlot(bool blockBuild = false)
    {
        CheckAll();
        return Build(blockBuild);
    }
}
