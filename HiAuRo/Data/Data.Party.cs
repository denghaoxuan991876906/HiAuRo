namespace HiAuRo;

public static partial class Data
{
    /// <summary>队伍成员信息</summary>
    public readonly struct PartyMemberInfo
    {
        public IPlayerCharacter? Player { get; init; }
        public uint JobId { get; init; }
        public float Distance { get; init; }
        public bool IsAlive => Player?.IsDead != true && Player != null;
    }

    /// <summary>
    /// 队伍数据 —— 一次扫描 DService.PartyList，多视图复用
    /// </summary>
    public static class Party
    {
        public static readonly List<PartyMemberInfo> All = [];
        public static readonly List<PartyMemberInfo> Alive = [];
        public static readonly List<PartyMemberInfo> Dead = [];
        public static readonly List<PartyMemberInfo> Tanks = [];
        public static readonly List<PartyMemberInfo> Healers = [];
        public static readonly List<PartyMemberInfo> Dps = [];
        public static readonly List<PartyMemberInfo> Melees = [];
        public static readonly List<PartyMemberInfo> Rangeds = [];
        public static readonly List<PartyMemberInfo> Casters = [];
        public static readonly List<PartyMemberInfo> Nearby5y = [];
        public static readonly List<PartyMemberInfo> Nearby10y = [];
        public static readonly List<PartyMemberInfo> Nearby15y = [];
        public static readonly List<PartyMemberInfo> CastableParty = [];
        public static readonly List<PartyMemberInfo> CastableTanks = [];
        public static readonly List<PartyMemberInfo> CastableHealers = [];
        public static readonly List<PartyMemberInfo> CastableDps = [];
        public static readonly List<PartyMemberInfo> CastableMainTanks = [];
        public static readonly List<PartyMemberInfo> CastableMelees = [];
        public static readonly List<PartyMemberInfo> CastableRangeds = [];
        public static readonly List<PartyMemberInfo> CastableAlliesWithin20 = [];
        public static readonly List<PartyMemberInfo> CastableAlliesWithin25 = [];
        public static readonly List<PartyMemberInfo> CastableAlliesWithin30 = [];

        private static readonly uint[] TankStances = [79, 91, 743, 1833];

        public static void Refresh()
        {
            ClearAll();

            if (!IsReady) return;

            var self = Me.Object;
            if (self == null) return;

            foreach (var member in DService.Instance().PartyList)
            {
                if (member.GameObject == null) continue;

                // 先直接转型，避免 CreateObjectReference 原生调用
                var player = member.GameObject as IPlayerCharacter
                    ?? DService.Instance().ObjectTable.CreateObjectReference(member.GameObject.Address) as IPlayerCharacter;
                if (player == null) continue;

                var distance = Me.DistanceToObject2D(player);
                var jobId = player.ClassJob.RowId;
                var info = new PartyMemberInfo
                {
                    Player = player,
                    JobId = jobId,
                    Distance = distance
                };

                All.Add(info);

                if (info.IsAlive) Alive.Add(info);
                else Dead.Add(info);

                if (IsTank(jobId))
                {
                    Tanks.Add(info);
                    if (info.IsAlive && HasTankStance(player))
                        CastableMainTanks.Add(info);
                }
                else if (IsHealer(jobId))
                    Healers.Add(info);
                else
                {
                    Dps.Add(info);
                    if (IsMelee(jobId)) Melees.Add(info);
                    else if (IsRanged(jobId)) Rangeds.Add(info);
                    else if (IsCaster(jobId)) Casters.Add(info);
                }

                if (player.GameObjectID == self.GameObjectID) continue;

                if (distance <= 5f) Nearby5y.Add(info);
                if (distance <= 10f) Nearby10y.Add(info);
                if (distance <= 15f) Nearby15y.Add(info);

                if (!info.IsAlive) continue;

                CastableParty.Add(info);

                if (IsTank(jobId))
                    CastableTanks.Add(info);
                else if (IsHealer(jobId))
                    CastableHealers.Add(info);
                else
                {
                    CastableDps.Add(info);
                    if (IsMelee(jobId)) CastableMelees.Add(info);
                    else if (IsRanged(jobId)) CastableRangeds.Add(info);
                }

                if (distance <= 20f) CastableAlliesWithin20.Add(info);
                if (distance <= 25f) CastableAlliesWithin25.Add(info);
                if (distance <= 30f) CastableAlliesWithin30.Add(info);
            }
        }

        private static void ClearAll()
        {
            All.Clear(); Alive.Clear(); Dead.Clear();
            Tanks.Clear(); Healers.Clear(); Dps.Clear();
            Melees.Clear(); Rangeds.Clear(); Casters.Clear();
            Nearby5y.Clear(); Nearby10y.Clear(); Nearby15y.Clear();
            CastableParty.Clear(); CastableTanks.Clear(); CastableHealers.Clear(); CastableDps.Clear();
            CastableMainTanks.Clear(); CastableMelees.Clear(); CastableRangeds.Clear();
            CastableAlliesWithin20.Clear(); CastableAlliesWithin25.Clear(); CastableAlliesWithin30.Clear();
        }

        private static bool IsTank(uint jobId) => jobId is 19 or 21 or 32 or 37;
        private static bool IsHealer(uint jobId) => jobId is 24 or 28 or 33 or 40;
        private static bool IsMelee(uint jobId) => jobId is 20 or 22 or 30 or 34 or 39 or 41;
        private static bool IsRanged(uint jobId) => jobId is 23 or 31 or 38;
        private static bool IsCaster(uint jobId) => jobId is 25 or 27 or 35 or 42;

        private static bool HasTankStance(IPlayerCharacter player)
        {
            if (player is not IBattleChara bc) return false;
            var list = bc.StatusList;
            for (int i = 0; i < list.Length; i++)
            {
                var sid = list[i].StatusID;
                for (int j = 0; j < TankStances.Length; j++)
                {
                    if (sid == TankStances[j]) return true;
                }
            }
            return false;
        }
    }
}
