namespace HiAuRo.Recording;

public static class CombatRecorderEventWriter
{
    public static IReadOnlyList<CombatFrameSnapshot> BuildEventSamples(
        CombatFrameSnapshot? previous,
        CombatFrameSnapshot current,
        bool includePreviousFrame,
        string eventGroupId)
    {
        current.EventGroupId = eventGroupId;
        current.SampleRole = "current";

        if (!includePreviousFrame || previous == null)
            return [current];

        var previousOutput = Clone(previous);
        previousOutput.EventGroupId = eventGroupId;
        previousOutput.EventType = current.EventType;
        previousOutput.Source = current.Source;
        previousOutput.ActionId = current.ActionId;
        previousOutput.ActionName = current.ActionName;
        previousOutput.Success = current.Success;
        previousOutput.Failed = current.Failed;
        previousOutput.Cancelled = current.Cancelled;
        previousOutput.IsGcd = current.IsGcd;
        previousOutput.IsAbility = current.IsAbility;
        previousOutput.GcdKind = current.GcdKind;
        previousOutput.TrackedSkills = current.TrackedSkills.Select(Clone).ToList();
        previousOutput.SampleRole = "prev";

        return [previousOutput, current];
    }

    private static CombatFrameSnapshot Clone(CombatFrameSnapshot source)
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
            TrackedSkills = source.TrackedSkills.Select(Clone).ToList()
        };
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
}
