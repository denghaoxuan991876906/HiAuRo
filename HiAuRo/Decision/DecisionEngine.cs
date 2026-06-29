using HiAuRo.ACR;
using static HiAuRo.Data;
using HiAuRo.FactAxis;

namespace HiAuRo.Decision;

/// <summary>
/// 决策引擎 — 根据队伍组成 + 需求，分配减伤/治疗
/// </summary>
public sealed class DecisionEngine
{
    public static DecisionEngine Instance { get; } = new();

    private DecisionOutput _output = new();
    private bool _initialized;

    private DecisionEngine() { }

    /// <summary>初始化内置技能数据</summary>
    public void Init()
    {
        if (_initialized) return;
        _initialized = true;
        LoadBuiltinSkills();
    }

    /// <summary>治疗需求 — 事件到达时立即分配（适合 HoT/盾预先铺）</summary>
    public DecisionOutput 计算治疗(int 需求治疗)
    {
        _output = new DecisionOutput();
        if (需求治疗 <= 0) return _output;
        var 队伍 = GetAvailableRoles();
        分配治疗(需求治疗, 队伍);
        return _output;
    }

    /// <summary>减伤需求 — 事件到达时评估，在窗口内延迟释放</summary>
    public DecisionOutput 计算减伤(int 需求减伤)
    {
        _output = new DecisionOutput();
        if (需求减伤 <= 0) return _output;
        var 队伍 = GetAvailableRoles();
        分配减伤(需求减伤, 队伍);
        return _output;
    }

    /// <summary>[Obsolete] 旧版联合计算 — 请使用 计算减伤() / 计算治疗()</summary>
    [System.Obsolete("使用 计算减伤() / 计算治疗()")]
    public DecisionOutput 计算(int 需求减伤, int 需求治疗)
    {
        _output = new DecisionOutput();
        if (需求减伤 == 0 && 需求治疗 == 0) return _output;
        var 队伍 = GetAvailableRoles();
        if (需求减伤 > 0) 分配减伤(需求减伤, 队伍);
        if (需求治疗 > 0) 分配治疗(需求治疗, 队伍);
        return _output;
    }

    #region 减伤分配

    private void 分配减伤(int 需求, List<(Jobs, bool)> 队伍)
    {
        // 动态检查：扫描队伍成员 + 当前目标，计算已有减伤总量
        int 已有减伤 = 扫描已有减伤();
        int 净需求 = Math.Max(0, 需求 - 已有减伤);
        if (净需求 <= 0) { _output.不足 = false; return; }

        var 候选 = new List<团队减伤>();

        foreach (var (job, isInParty) in 队伍)
        {
            if (!isInParty) continue;
            if (!DecisionSkillRegistry.团队减伤表.TryGetValue(job, out var skills)) continue;

            foreach (var skill in skills)
            {
                if (GetCooldownRemaining(skill.技能ID) > 0) continue;
                候选.Add(skill);
            }
        }

        // 冷却升序排列（冷却短的优先）
        候选.Sort((a, b) => a.冷却秒.CompareTo(b.冷却秒));

        int 已分配 = 0;
        foreach (var skill in 候选)
        {
            if (已分配 >= 净需求) break;

            已分配 += skill.减伤百分比;
            _output.减伤分配.Add(new 减伤分配
            {
                技能ID = skill.技能ID, 技能名称 = skill.名称,
                职业 = skill.职业, 减伤值 = skill.减伤百分比,
                持续秒 = skill.持续秒, 团队减伤 = true
            });
            _output.执行技能IDs.Add(skill.技能ID);
        }

        _output.不足 = 已分配 < 净需求;
    }

    /// <summary>扫描队伍成员 + 当前目标已生效的减伤状态，返回已有减伤总量 (%)</summary>
    private static int 扫描已有减伤()
    {
        int total = 0;
        var checkedIds = new HashSet<uint>();

        foreach (var skills in DecisionSkillRegistry.团队减伤表.Values)
        {
            foreach (var skill in skills)
            {
                if (skill.状态ID == 0 || !checkedIds.Add(skill.状态ID)) continue;

                // 检查队伍成员是否已有此 buff（团队减伤 buff）
                bool found = false;
                foreach (var member in Party.All)
                {
                    if (member.Player is IBattleChara bc && bc.StatusList.HasStatus(skill.状态ID))
                    { found = true; break; }
                }

                // 也检查当前目标（敌方减益 debuff，如雪仇/牵制/昏乱）
                if (!found && Target.Current is IBattleChara tgt)
                    found = tgt.StatusList.HasStatus(skill.状态ID);

                if (found) total += skill.减伤百分比;
            }
        }

        return total;
    }

    #endregion

    #region 治疗分配

    private void 分配治疗(int 需求, List<(Jobs, bool)> 队伍)
    {
        var 候选 = new List<团队治疗>();

        foreach (var (job, isInParty) in 队伍)
        {
            if (!isInParty) continue;
            if (!DecisionSkillRegistry.团队治疗表.TryGetValue(job, out var skills)) continue;

            foreach (var skill in skills)
            {
                if (GetCooldownRemaining(skill.技能ID) > 0) continue;
                候选.Add(skill);
            }
        }

        候选.Sort((a, b) => a.冷却秒.CompareTo(b.冷却秒));

        int 已分配 = 0;
        foreach (var skill in 候选)
        {
            if (已分配 >= 需求) break;

            已分配 += skill.恢复力;
            _output.治疗分配.Add(new 治疗分配
            {
                技能ID = skill.技能ID, 技能名称 = skill.名称,
                职业 = skill.职业, 恢复力 = skill.恢复力, 是否持续 = skill.是否持续
            });
            _output.执行技能IDs.Add(skill.技能ID);
        }

        _output.不足 = 已分配 < 需求;
    }

    #endregion

    #region 工具方法

    /// <summary>获取当前队伍中所有角色的职业列表</summary>
    private static List<(Jobs Job, bool IsInParty)> GetAvailableRoles()
    {
        var result = new List<(Jobs, bool)>();
        var selfJob = (Jobs)Data.Me.ClassJob;
        result.Add((selfJob, true));

        foreach (var member in DService.Instance().PartyList)
        {
            try
            {
                var jobId = member.ClassJob.RowId;
                if (jobId == 0) continue;
                var job = (Jobs)jobId;
                if (!result.Any(r => r.Item1 == job))
                    result.Add((job, true));
            }
            catch { }
        }

        foreach (var job in DecisionSkillRegistry.团队减伤表.Keys
            .Concat(DecisionSkillRegistry.团队治疗表.Keys).Distinct())
        {
            if (!result.Any(r => r.Item1 == job))
                result.Add((job, false));
        }

        return result;
    }

    private static int GetCooldownRemaining(uint spellId) =>
        (int)global::HiAuRo.ACR.SpellHelper.GetCooldownRemaining(spellId);

    #endregion

    #region 内置技能数据

    private static void LoadBuiltinSkills()
    {
        // ============================================================
        //  坦克 团队减伤
        // ============================================================

        // PLD 骑士
        DecisionSkillRegistry.注册(Jobs.PLD,
            teamMit:
            [
                new() { 技能ID = 7535, 名称 = "雪仇", 职业 = Jobs.PLD, 减伤百分比 = 10, 持续秒 = 10, 冷却秒 = 60, 减伤类型 = 减伤类型.全能, 状态ID = 1193 },
                new() { 技能ID = 7385, 名称 = "武装战阵", 职业 = Jobs.PLD, 减伤百分比 = 15, 持续秒 = 18, 冷却秒 = 120, 减伤类型 = 减伤类型.全能, 状态ID = 1175 },
                new() { 技能ID = 3540, 名称 = "圣光幕帘", 职业 = Jobs.PLD, 减伤百分比 = 10, 持续秒 = 30, 冷却秒 = 90, 减伤类型 = 减伤类型.全能, 状态ID = 727 },
            ],
            personalMit:
            [
                new() { 技能ID = 7382, 名称 = "介护", 职业 = Jobs.PLD, 减伤百分比 = 10, 持续秒 = 6, 冷却秒 = 10, 减伤类型 = 减伤类型.全能, 状态ID = 1174 },
                new() { 技能ID = 25746, 名称 = "圣盾阵", 职业 = Jobs.PLD, 减伤百分比 = 15, 持续秒 = 8, 冷却秒 = 5, 减伤类型 = 减伤类型.全能, 状态ID = 2674 },
            ]);

        // WAR 战士
        DecisionSkillRegistry.注册(Jobs.WAR,
            teamMit:
            [
                new() { 技能ID = 7535, 名称 = "雪仇", 职业 = Jobs.WAR, 减伤百分比 = 10, 持续秒 = 10, 冷却秒 = 60, 减伤类型 = 减伤类型.全能, 状态ID = 1193 },
                new() { 技能ID = 7388, 名称 = "聚集之心", 职业 = Jobs.WAR, 减伤百分比 = 15, 持续秒 = 15, 冷却秒 = 90, 减伤类型 = 减伤类型.全能, 状态ID = 1457 },
            ],
            personalMit:
            [
                new() { 技能ID = 3551, 名称 = "原初的直觉", 职业 = Jobs.WAR, 减伤百分比 = 20, 持续秒 = 6, 冷却秒 = 25, 减伤类型 = 减伤类型.全能, 状态ID = 735 },
                new() { 技能ID = 16464, 名称 = "原初的勇猛", 职业 = Jobs.WAR, 减伤百分比 = 10, 持续秒 = 6, 冷却秒 = 25, 减伤类型 = 减伤类型.全能, 状态ID = 1857 },
            ]);

        // DRK 暗黑骑士
        DecisionSkillRegistry.注册(Jobs.DRK,
            teamMit:
            [
                new() { 技能ID = 7535, 名称 = "雪仇", 职业 = Jobs.DRK, 减伤百分比 = 10, 持续秒 = 10, 冷却秒 = 60, 减伤类型 = 减伤类型.全能, 状态ID = 1193 },
                new() { 技能ID = 16470, 名称 = "暗黑布教", 职业 = Jobs.DRK, 减伤百分比 = 10, 持续秒 = 15, 冷却秒 = 90, 减伤类型 = 减伤类型.魔法, 状态ID = 1894 },
            ],
            personalMit:
            [
                new() { 技能ID = 7393, 名称 = "至黑之夜", 职业 = Jobs.DRK, 减伤百分比 = 25, 持续秒 = 7, 冷却秒 = 15, 减伤类型 = 减伤类型.全能, 状态ID = 1308 },
                new() { 技能ID = 25754, 名称 = "献奉", 职业 = Jobs.DRK, 减伤百分比 = 10, 持续秒 = 10, 冷却秒 = 60, 减伤类型 = 减伤类型.全能, 状态ID = 2682 },
            ]);

        // GNB 绝枪战士
        DecisionSkillRegistry.注册(Jobs.GNB,
            teamMit:
            [
                new() { 技能ID = 7535, 名称 = "雪仇", 职业 = Jobs.GNB, 减伤百分比 = 10, 持续秒 = 10, 冷却秒 = 60, 减伤类型 = 减伤类型.全能, 状态ID = 1193 },
                new() { 技能ID = 16160, 名称 = "光之心", 职业 = Jobs.GNB, 减伤百分比 = 10, 持续秒 = 15, 冷却秒 = 90, 减伤类型 = 减伤类型.魔法, 状态ID = 1838 },
            ],
            personalMit:
            [
                new() { 技能ID = 25758, 名称 = "金刚之心", 职业 = Jobs.GNB, 减伤百分比 = 15, 持续秒 = 4, 冷却秒 = 25, 减伤类型 = 减伤类型.全能, 状态ID = 2683 },
            ]);

        // ============================================================
        //  治疗 团队减伤
        // ============================================================

        // SCH 学者
        DecisionSkillRegistry.注册(Jobs.SCH,
            teamMit:
            [
                new() { 技能ID = 188, 名称 = "野战治疗阵", 职业 = Jobs.SCH, 减伤百分比 = 10, 持续秒 = 15, 冷却秒 = 30, 减伤类型 = 减伤类型.全能, 状态ID = 1944 },
                new() { 技能ID = 25868, 名称 = "进取", 职业 = Jobs.SCH, 减伤百分比 = 10, 持续秒 = 20, 冷却秒 = 120, 减伤类型 = 减伤类型.全能, 状态ID = 2713 },
            ]);

        // AST 占星术士
        DecisionSkillRegistry.注册(Jobs.AST,
            teamMit:
            [
                new() { 技能ID = 3613, 名称 = "命运之轮", 职业 = Jobs.AST, 减伤百分比 = 10, 持续秒 = 18, 冷却秒 = 60, 减伤类型 = 减伤类型.全能, 状态ID = 848 },
            ]);

        // SGE 贤者
        DecisionSkillRegistry.注册(Jobs.SGE,
            teamMit:
            [
                new() { 技能ID = 24303, 名称 = "坚岩", 职业 = Jobs.SGE, 减伤百分比 = 10, 持续秒 = 15, 冷却秒 = 30, 减伤类型 = 减伤类型.全能, 状态ID = 2619 },
                new() { 技能ID = 24310, 名称 = "全体", 职业 = Jobs.SGE, 减伤百分比 = 10, 持续秒 = 20, 冷却秒 = 120, 减伤类型 = 减伤类型.全能, 状态ID = 2628 },
            ]);

        // ============================================================
        //  DPS 团队减伤
        // ============================================================

        // MCH 机工士
        DecisionSkillRegistry.注册(Jobs.MCH,
            teamMit:
            [
                new() { 技能ID = 16889, 名称 = "策动", 职业 = Jobs.MCH, 减伤百分比 = 10, 持续秒 = 15, 冷却秒 = 90, 减伤类型 = 减伤类型.全能, 状态ID = 1951 },
            ]);

        // DNC 舞者
        DecisionSkillRegistry.注册(Jobs.DNC,
            teamMit:
            [
                new() { 技能ID = 16012, 名称 = "盾桑巴", 职业 = Jobs.DNC, 减伤百分比 = 10, 持续秒 = 15, 冷却秒 = 90, 减伤类型 = 减伤类型.全能, 状态ID = 1826 },
            ]);

        // ============================================================
        //  DPS 近战/魔法 团队减伤
        // ============================================================

        // NIN 忍者 — 攻杀必中 (敌方 5% 受伤增加) + 牵制
        DecisionSkillRegistry.注册(Jobs.NIN,
            teamMit:
            [
                new() { 技能ID = 36957, 名称 = "攻杀必中", 职业 = Jobs.NIN, 减伤百分比 = 5, 持续秒 = 20, 冷却秒 = 120, 减伤类型 = 减伤类型.全能, 状态ID = 3856 },
                new() { 技能ID = 7549, 名称 = "牵制", 职业 = Jobs.NIN, 减伤百分比 = 10, 持续秒 = 10, 冷却秒 = 90, 减伤类型 = 减伤类型.物理, 状态ID = 1195 },
            ]);

        // DRG 龙骑士 — 牵制
        DecisionSkillRegistry.注册(Jobs.DRG,
            teamMit:
            [
                new() { 技能ID = 7549, 名称 = "牵制", 职业 = Jobs.DRG, 减伤百分比 = 10, 持续秒 = 10, 冷却秒 = 90, 减伤类型 = 减伤类型.物理, 状态ID = 1195 },
            ]);

        // SAM 武士 — 牵制
        DecisionSkillRegistry.注册(Jobs.SAM,
            teamMit:
            [
                new() { 技能ID = 7549, 名称 = "牵制", 职业 = Jobs.SAM, 减伤百分比 = 10, 持续秒 = 10, 冷却秒 = 90, 减伤类型 = 减伤类型.物理, 状态ID = 1195 },
            ]);

        // RPR 钐镰客 — 牵制
        DecisionSkillRegistry.注册(Jobs.RPR,
            teamMit:
            [
                new() { 技能ID = 7549, 名称 = "牵制", 职业 = Jobs.RPR, 减伤百分比 = 10, 持续秒 = 10, 冷却秒 = 90, 减伤类型 = 减伤类型.物理, 状态ID = 1195 },
            ]);

        // BLM 黑魔法师 — 昏乱
        DecisionSkillRegistry.注册(Jobs.BLM,
            teamMit:
            [
                new() { 技能ID = 7560, 名称 = "昏乱", 职业 = Jobs.BLM, 减伤百分比 = 10, 持续秒 = 10, 冷却秒 = 90, 减伤类型 = 减伤类型.魔法, 状态ID = 1203 },
            ]);

        // SMN 召唤师 — 昏乱
        DecisionSkillRegistry.注册(Jobs.SMN,
            teamMit:
            [
                new() { 技能ID = 7560, 名称 = "昏乱", 职业 = Jobs.SMN, 减伤百分比 = 10, 持续秒 = 10, 冷却秒 = 90, 减伤类型 = 减伤类型.魔法, 状态ID = 1203 },
            ]);

        // RDM 赤魔法师 — 魔障 + 昏乱
        DecisionSkillRegistry.注册(Jobs.RDM,
            teamMit:
            [
                new() { 技能ID = 25857, 名称 = "魔障", 职业 = Jobs.RDM, 减伤百分比 = 10, 持续秒 = 10, 冷却秒 = 120, 减伤类型 = 减伤类型.魔法, 状态ID = 2707 },
                new() { 技能ID = 7560, 名称 = "昏乱", 职业 = Jobs.RDM, 减伤百分比 = 10, 持续秒 = 10, 冷却秒 = 90, 减伤类型 = 减伤类型.魔法, 状态ID = 1203 },
            ]);

        // ============================================================
        //  已有职业（补全 StatusID，覆盖原注册）
        // ============================================================

        // BRD 诗人
        DecisionSkillRegistry.注册(Jobs.BRD,
            teamMit:
            [
                new() { 技能ID = 7561, 名称 = "策动", 职业 = Jobs.BRD, 减伤百分比 = 10, 持续秒 = 15, 冷却秒 = 90, 减伤类型 = 减伤类型.全能, 状态ID = 1934 },
                new() { 技能ID = 7559, 名称 = "行吟", 职业 = Jobs.BRD, 减伤百分比 = 10, 持续秒 = 15, 冷却秒 = 90, 减伤类型 = 减伤类型.全能, 状态ID = 0 },
            ],
            teamHeal:
            [
                new() { 技能ID = 7560, 名称 = "光阴神的礼赞凯歌", 职业 = Jobs.BRD, 恢复力 = 200, 冷却秒 = 90, 治疗类型 = 治疗类型.持续, 是否持续 = true }
            ]);

        // MNK 武僧
        DecisionSkillRegistry.注册(Jobs.MNK,
            teamMit:
            [
                new() { 技能ID = 7549, 名称 = "牵制", 职业 = Jobs.MNK, 减伤百分比 = 10, 持续秒 = 15, 冷却秒 = 90, 减伤类型 = 减伤类型.全能, 状态ID = 1195 },
            ],
            personalMit:
            [
                new() { 技能ID = 3547, 名称 = "内丹", 职业 = Jobs.MNK, 减伤百分比 = 0, 持续秒 = 0, 冷却秒 = 120, 减伤类型 = 减伤类型.全能, 状态ID = 0 },
            ]);

        // WHM 白魔
        DecisionSkillRegistry.注册(Jobs.WHM,
            teamMit:
            [
                new() { 技能ID = 7433, 名称 = "节制", 职业 = Jobs.WHM, 减伤百分比 = 10, 持续秒 = 20, 冷却秒 = 120, 减伤类型 = 减伤类型.全能, 状态ID = 1872 },
            ],
            teamHeal:
            [
                new() { 技能ID = 124, 名称 = "医济", 职业 = Jobs.WHM, 恢复力 = 600, 冷却秒 = 60, 治疗类型 = 治疗类型.持续, 是否持续 = true },
                new() { 技能ID = 7434, 名称 = "全大赦", 职业 = Jobs.WHM, 恢复力 = 800, 冷却秒 = 60, 治疗类型 = 治疗类型.直疗 },
            ]);

        // ============================================================
        //  FactSpellTable — 技能执行数据（"怎么放"）
        // ============================================================

        // BRD
        FactSpellTable.注册(7561, "策动");
        FactSpellTable.注册(7559, "行吟");
        FactSpellTable.注册(7560, "光阴神的礼赞凯歌");
        // MNK
        FactSpellTable.注册(7549, "牵制");
        FactSpellTable.注册(3547, "内丹");
        // WHM
        FactSpellTable.注册(7433, "节制");
        FactSpellTable.注册(124, "医济");
        FactSpellTable.注册(7434, "全大赦");
        // PLD
        FactSpellTable.注册(7535, "雪仇");
        FactSpellTable.注册(7385, "武装战阵");
        FactSpellTable.注册(3540, "圣光幕帘");
        FactSpellTable.注册(7382, "介护");
        FactSpellTable.注册(25746, "圣盾阵");
        // WAR
        FactSpellTable.注册(7388, "聚集之心");
        FactSpellTable.注册(3551, "原初的直觉");
        FactSpellTable.注册(16464, "原初的勇猛");
        // DRK
        FactSpellTable.注册(16470, "暗黑布教");
        FactSpellTable.注册(7393, "至黑之夜");
        FactSpellTable.注册(25754, "献奉");
        // GNB
        FactSpellTable.注册(16160, "光之心");
        FactSpellTable.注册(25758, "金刚之心");
        // SCH
        FactSpellTable.注册(188, "野战治疗阵");
        FactSpellTable.注册(25868, "进取");
        // AST
        FactSpellTable.注册(3613, "命运之轮");
        // SGE
        FactSpellTable.注册(24303, "坚岩");
        FactSpellTable.注册(24310, "全体");
        // MCH
        FactSpellTable.注册(16889, "策动");
        // DNC
        FactSpellTable.注册(16012, "盾桑巴");
        // NIN
        FactSpellTable.注册(36957, "攻杀必中");
        // RDM
        FactSpellTable.注册(25857, "魔障");
        // (牵制 7549 / 昏乱 7560 已在 MNK/BRD 注册，无需重复)
    }

    #endregion
}
