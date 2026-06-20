namespace HiAuRo.Recording;

public static class CombatRecorderEventWriter
{
    public static CombatFrameSnapshot BuildEventSample(
        CombatFrameSnapshot? before,
        CombatFrameSnapshot current,
        string eventGroupId)
    {
        current.EventGroupId = eventGroupId;
        current.SampleRole = "current";
        current.Before = before == null ? null : CloneAsBefore(before, current, eventGroupId);
        return current;
    }

    private static CombatFrameSnapshot CloneAsBefore(
        CombatFrameSnapshot source,
        CombatFrameSnapshot current,
        string eventGroupId)
    {
        var before = Clone(source);
        before.EventGroupId = eventGroupId;
        before.EventType = current.EventType;
        before.Source = current.Source;
        before.ActionId = current.ActionId;
        before.ActionName = current.ActionName;
        before.Success = current.Success;
        before.Failed = current.Failed;
        before.Cancelled = current.Cancelled;
        before.IsGcd = current.IsGcd;
        before.IsAbility = current.IsAbility;
        before.GcdKind = current.GcdKind;
        before.TrackedSkills = current.TrackedSkills.Select(Clone).ToList();
        before.SampleRole = "before";
        before.Before = null;
        before.RunnerDebug = null;
        return before;
    }

    public static CombatFrameSnapshot Clone(CombatFrameSnapshot source)
    {
        return new CombatFrameSnapshot
        {
            Timestamp = source.Timestamp,
            FrameIndex = source.FrameIndex,
            SampleIndex = source.SampleIndex,
            SessionId = source.SessionId,
            EventGroupId = source.EventGroupId,
            EventType = source.EventType,
            SampleRole = source.SampleRole,
            Source = source.Source,
            Job = source.Job,
            ActionId = source.ActionId,
            ActionName = source.ActionName,
            QueuedActionId = source.QueuedActionId,
            CastActionId = source.CastActionId,
            IsGcd = source.IsGcd,
            IsAbility = source.IsAbility,
            GcdKind = source.GcdKind,
            Success = source.Success,
            Failed = source.Failed,
            Cancelled = source.Cancelled,
            Hp = source.Hp,
            MaxHp = source.MaxHp,
            HpPercent = source.HpPercent,
            Mp = source.Mp,
            MaxMp = source.MaxMp,
            MpPercent = source.MpPercent,
            Level = source.Level,
            InCombat = source.InCombat,
            IsMoving = source.IsMoving,
            IsCasting = source.IsCasting,
            TotalCastTime = source.TotalCastTime,
            CurrentCastTime = source.CurrentCastTime,
            GcdCooldown = source.GcdCooldown,
            GcdDuration = source.GcdDuration,
            LastComboSpellId = source.LastComboSpellId,
            SelfBuffs = source.SelfBuffs.Select(Clone).ToList(),
            TargetId = source.TargetId,
            TargetName = source.TargetName,
            Distance = source.Distance,
            IsBoss = source.IsBoss,
            IsDead = source.IsDead,
            IsTargetable = source.IsTargetable,
            TargetHp = source.TargetHp,
            TargetMaxHp = source.TargetMaxHp,
            TargetHpPercent = source.TargetHpPercent,
            TargetBuffs = source.TargetBuffs.Select(Clone).ToList(),
            EnemyCountNearSelf5 = source.EnemyCountNearSelf5,
            EnemyCountNearSelf10 = source.EnemyCountNearSelf10,
            EnemyCountNearSelf15 = source.EnemyCountNearSelf15,
            EnemyCountNearSelf25 = source.EnemyCountNearSelf25,
            EnemyCountNearTarget5 = source.EnemyCountNearTarget5,
            EnemyCountNearTarget10 = source.EnemyCountNearTarget10,
            EnemyCountNearTarget15 = source.EnemyCountNearTarget15,
            EnemyCountNearTarget25 = source.EnemyCountNearTarget25,
            JobGauge = new Dictionary<string, object?>(source.JobGauge),
            QtStates = new Dictionary<string, bool>(source.QtStates),
            CurrentAcrName = source.CurrentAcrName,
            CurrentRotationName = source.CurrentRotationName,
            ResolverResults = source.ResolverResults.Select(Clone).ToList(),
            TrackedSkills = source.TrackedSkills.Select(Clone).ToList(),
            RunnerDebug = source.RunnerDebug == null ? null : Clone(source.RunnerDebug)
        };
    }

    public static CombatFrameSnapshot CreateFromCacheSample(CombatFrameCacheSample source)
    {
        var snapshot = new CombatFrameSnapshot
        {
            Timestamp = source.Timestamp,
            SampleIndex = source.SampleIndex,
            SessionId = source.SessionId,
            SampleRole = "before",
            Source = "cache",
            Job = source.Job,
            Hp = source.Hp,
            MaxHp = source.MaxHp,
            Mp = source.Mp,
            MaxMp = source.MaxMp,
            Level = source.Level,
            InCombat = source.InCombat,
            IsMoving = source.IsMoving,
            IsCasting = source.IsCasting,
            TotalCastTime = source.TotalCastTime,
            CurrentCastTime = source.CurrentCastTime,
            GcdCooldown = source.GcdCooldown,
            GcdDuration = source.GcdDuration,
            LastComboSpellId = source.LastComboSpellId,
            TargetId = source.TargetId,
            TargetName = source.TargetName,
            Distance = source.Distance,
            IsBoss = source.IsBoss,
            IsDead = source.IsDead,
            IsTargetable = source.IsTargetable,
            TargetHp = source.TargetHp,
            TargetMaxHp = source.TargetMaxHp,
            QtStates = new Dictionary<string, bool>(source.QtStates),
            JobGauge = new Dictionary<string, object?>(source.JobGauge),
            SelfBuffs = source.SelfBuffs.Select(Clone).ToList(),
            TargetBuffs = source.TargetBuffs.Select(Clone).ToList()
        };

        snapshot.HpPercent = Percent(snapshot.Hp, snapshot.MaxHp);
        snapshot.MpPercent = Percent(snapshot.Mp, snapshot.MaxMp);
        snapshot.TargetHpPercent = Percent(snapshot.TargetHp, snapshot.TargetMaxHp);
        return snapshot;
    }

    private static CombatBuffSnapshot Clone(CombatBuffSnapshot source)
    {
        return new CombatBuffSnapshot
        {
            Id = source.Id,
            Name = source.Name,
            Stack = source.Stack,
            RemainMs = source.RemainMs
        };
    }

    private static CombatResolverSnapshot Clone(CombatResolverSnapshot source)
    {
        return new CombatResolverSnapshot
        {
            Name = source.Name,
            Mode = source.Mode,
            CheckResult = source.CheckResult,
            PassedWindow = source.PassedWindow
        };
    }

    private static CombatTrackedSkillSnapshot Clone(CombatTrackedSkillSnapshot source)
    {
        return new CombatTrackedSkillSnapshot
        {
            ActionId = source.ActionId,
            ActionName = source.ActionName,
            CooldownRemainingMs = source.CooldownRemainingMs,
            Charges = source.Charges,
            MaxCharges = source.MaxCharges
        };
    }

    private static CombatRunnerDebugSnapshot Clone(CombatRunnerDebugSnapshot source)
    {
        return new CombatRunnerDebugSnapshot
        {
            Phase = source.Phase,
            SlotSource = source.SlotSource,
            CanGcd = source.CanGcd,
            CanOgcd = source.CanOgcd,
            HasNextSlot = source.HasNextSlot,
            HasWaitGcdSlot = source.HasWaitGcdSlot,
            HasCurrSlot = source.HasCurrSlot
        };
    }

    private static float Percent(uint value, uint max) => max > 0 ? (float)value / max : 0f;
}
