// 「公會的祝福」——出發前選的被動。三選一，一局整場都生效。
//
// 可以選幾個看委託等級：C 級 1 個、B 級 2 個、A 級 3 個、S 級 4 個。
// 每一次都是重新抽三個出來選一個，所以高等級委託的組合會很不一樣。
//
// 設計上刻意跟「升級三選一」分開：升級是打的過程中越滾越強，
// 祝福是**出發前就決定這一趟怎麼打**——選了吸血就走消耗流，選了連擊就走爆發流。
//
// 為了不讓 30 幾個祝福變成 30 處 if，這裡的做法是：
// 祝福只負責往 SVPassiveState 這張「修正表」上疊數字，
// 各系統各自去讀自己那一格。要加新的祝福，多半只要動一個欄位就好。
//
// 注意所有 Apply 都是「疊上去」（+= 或 *=）而不是指定 ——
// 因為高等級委託可以選好幾個，指定會把前一個蓋掉。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    /// <summary>一局之內生效的所有修正。祝福選好之後就不再變動。</summary>
    public class SVPassiveState
    {
        /// <summary>這一局選了哪些，給 HUD 顯示用。</summary>
        public readonly List<SVPassive> Chosen = new List<SVPassive>();

        // ---- 隨擊殺成長 ----
        public float HpPerKill;
        public float AtkPerKill;
        public float ShieldPerKill;

        // ---- 普攻 ----
        public float DoubleAttackChance;
        public float CritChance;
        public float CritMultiplier = SVCrit.DefaultMultiplier;
        public float DamageMul = 1f;     // 舊欄位，已不使用（改成下面四種加成）

        // ---- 四種傷害加成（同類相加、不同類相乘；0.4 = +40%）----
        public float GeneralDamageBonus;   // 通用傷害：全部都吃
        public float BossDamageBonus;      // BOSS 傷害：打王才吃
        public float AttackDamageBonus;    // 普攻傷害：男主普攻
        public float SkillDamageBonus;     // 技能傷害：男主主動技能
        public float AttackSpeedMul = 1f;
        public int ExtraShots;
        public int ExtraPierce;
        public float RangeBonus;
        public float BurnPower;          // 普攻附加的持續傷害

        // ---- 生存 ----
        public float MoveSpeedMul = 1f;
        public float MaxHpMul = 1f;
        public int FlatDefense;
        public float Lifesteal;          // 普攻吸血（只有男主普攻）
        public float OmniLifesteal;      // 全能吸血（普攻＋技能＋毒血）
        public float LowHpLifestealMul = 1f;   // 血量 30% 以下時吸血倍率（絕境渴血）
        public float OverhealShield;     // 滿血時溢出的吸血轉護盾的比例（鮮血盈溢）
        public int AttackBleed;          // 普攻附加流血層數（血刃）
        public float Thorns;             // 受傷時反彈給接觸者的比例
        public float RegenPercent;       // 每秒回最大血的幾成
        public float ShieldPerTick;      // 每 6 秒生成多少護盾
        public bool Revive;              // 死亡時復活一次

        // ---- 場面 ----
        public float PickupBonus;
        public float XpMul = 1f;
        public float GoldMul = 1f;
        public float LootMul = 1f;
        public float SlowAura;           // 周圍敵人的減速幅度
        public float ExplodeChance;
        public int ExplodePower;
        public int StartLevel = 1;

        // ---- 隊伍與技能 ----
        public float CompanionPowerMul = 1f;
        public float CompanionCooldownMul = 1f;
        public float CompanionHpMul = 1f;
        public int CompanionDefense;
        public float SkillCooldownMul = 1f;

        // ---- 條件式傷害 ----
        public float LoneWolfBonus;      // 隊友越少加越多
        public float LowHpBonus;         // 血越低加越多
        public float HalfHpBonus;        // 血量低於一半時，直接跳上去的加成（狂血）
        public float HealPerKill;        // 每擊殺回多少血（血之渴）
        public float SkillDoubleChance;  // 男主主動技能再發動一次的機率（雙重詠唱）

        /// <summary>血越多物理攻擊越高：滿血時的物理加成（照目前血量比例線性給）。</summary>
        public float HighHpPhysicalBonus;

        /// <summary>回血效果倍率（回血量、回血類技能都吃）。</summary>
        public float HealMul = 1f;

        /// <summary>護盾效果倍率。</summary>
        public float ShieldMul = 1f;

        /// <summary>放棄大覺的主動發動，換取其他技能的強化（苦行）。</summary>
        public bool NoAwakenActive;

        public void Clear()
        {
            var fresh = new SVPassiveState();
            Chosen.Clear();

            HpPerKill = AtkPerKill = ShieldPerKill = 0f;
            DoubleAttackChance = CritChance = 0f;
            HighHpPhysicalBonus = 0f;
            HealMul = ShieldMul = 1f;
            NoAwakenActive = false;
            CritMultiplier = fresh.CritMultiplier;
            DamageMul = AttackSpeedMul = 1f;
            GeneralDamageBonus = BossDamageBonus = AttackDamageBonus = SkillDamageBonus = 0f;
            ExtraShots = ExtraPierce = 0;
            RangeBonus = BurnPower = 0f;
            MoveSpeedMul = MaxHpMul = 1f;
            FlatDefense = 0;
            Lifesteal = Thorns = RegenPercent = ShieldPerTick = 0f;
            OmniLifesteal = OverhealShield = 0f;
            LowHpLifestealMul = 1f;
            AttackBleed = 0;
            Revive = false;
            PickupBonus = 0f;
            XpMul = GoldMul = LootMul = 1f;
            SlowAura = ExplodeChance = 0f;
            ExplodePower = 0;
            StartLevel = 1;
            CompanionPowerMul = CompanionCooldownMul = CompanionHpMul = 1f;
            CompanionDefense = 0;
            SkillCooldownMul = 1f;
            LoneWolfBonus = LowHpBonus = HalfHpBonus = 0f;
            HealPerKill = 0f;
            SkillDoubleChance = 0f;
        }

        public void Gain(SVPassive passive)
        {
            if (passive == null) return;
            Chosen.Add(passive);
            passive.Apply?.Invoke(this);
        }

        public bool Has(string id)
        {
            foreach (var p in Chosen) if (p.Id == id) return true;
            return false;
        }

        /// <summary>條件式的傷害倍率，統一在 SVEnemy.TakeDamage 乘進去。</summary>
        public float ConditionalDamage(SVGame game) => ConditionalDamage(game, SVDamageKind.Physical);

        /// <summary>
        /// 條件式的傷害倍率，統一在 SVEnemy.TakeDamage 乘進去。
        /// 有些加成只吃物理（例如「血越多物攻越高」），所以要知道這一下是哪一種傷害。
        /// </summary>
        public float ConditionalDamage(SVGame game, SVDamageKind kind)
        {
            float mul = 1f;
            if (game == null) return mul;

            if (LoneWolfBonus > 0f)
            {
                int alive = 0;
                foreach (var c in SVCompanion.Active) if (c != null && !c.Retired) alive++;
                if (alive == 0) mul += LoneWolfBonus;
                else if (alive == 1) mul += LoneWolfBonus * 0.4f;
            }

            if (game.Player != null && game.Player.MaxHp > 0)
            {
                float ratio = (float)game.Player.Hp / game.Player.MaxHp;

                // 血怒：血越低加越多（漸進）
                if (LowHpBonus > 0f) mul += LowHpBonus * (1f - ratio);

                // 狂血：過了一半這條線就直接跳上去（門檻式），跟血怒的曲線不一樣
                if (HalfHpBonus > 0f && ratio <= 0.5f) mul += HalfHpBonus;

                // 剛毅：血越多物攻越高。只吃物理，而且是照「目前血量」——
                // 受傷就會掉，所以它跟血怒剛好是相反的玩法
                if (HighHpPhysicalBonus > 0f && kind == SVDamageKind.Physical)
                    mul += HighHpPhysicalBonus * ratio;
            }

            return mul;
        }
    }

    public class SVPassive
    {
        public string Id;
        public string Name;
        public string Desc;
        public Color Tint = new Color(0.65f, 0.9f, 1f);
        public System.Action<SVPassiveState> Apply;
    }

    public static class SVPassives
    {
        /// <summary>每一次抽幾個出來選。</summary>
        public const int OfferCount = 3;

        /// <summary>這個委託等級可以選幾個祝福。C=1 B=2 A=3 S=4。</summary>
        public static int PicksFor(int mapRank) => Mathf.Clamp(mapRank, 1, 4);

        static readonly Color Growth = new Color(0.7f, 1f, 0.75f);
        static readonly Color Offense = new Color(1f, 0.72f, 0.55f);
        static readonly Color Defense = new Color(0.6f, 0.85f, 1f);
        static readonly Color Utility = new Color(0.9f, 0.85f, 1f);
        static readonly Color Team = new Color(1f, 0.8f, 0.95f);

        public static List<SVPassive> BuildCatalog()
        {
            var list = new List<SVPassive>();

            void Add(string id, string name, string desc, Color tint, System.Action<SVPassiveState> apply)
                => list.Add(new SVPassive { Id = id, Name = name, Desc = desc, Tint = tint, Apply = apply });

            // ---------------- 隨擊殺成長 ----------------
            Add("kill_hp", "屠戮成長", "每擊殺 2 隻，最大生命 +1（並回復同量）", Growth,
                s => s.HpPerKill += 0.5f);
            Add("kill_atk", "磨刀", "每擊殺 20 隻，普攻傷害 +1", Growth,
                s => s.AtkPerKill += 0.05f);
            Add("kill_shield", "聚魂盾", "每擊殺 4 隻，護盾 +1", Growth,
                s => s.ShieldPerKill += 0.25f);
            Add("kill_all", "戰場學徒", "每擊殺同時累積少量生命、攻擊與護盾", Growth,
                s => { s.HpPerKill += 0.22f; s.AtkPerKill += 0.02f; s.ShieldPerKill += 0.1f; });

            // ---------------- 普攻流派 ----------------
            Add("double", "連擊", "每次普攻有 30% 機率打兩次", Offense,
                s => s.DoubleAttackChance += 0.30f);
            Add("crit", "致命", "20% 機率爆擊，傷害兩倍", Offense,
                s => { s.CritChance += 0.20f; s.CritMultiplier = Mathf.Max(s.CritMultiplier, SVCrit.DefaultMultiplier); });
            Add("crit_heavy", "一擊必殺", "10% 機率爆擊，傷害四倍", Offense,
                s => { s.CritChance += 0.10f; s.CritMultiplier = Mathf.Max(s.CritMultiplier, SVCrit.DefaultMultiplier + 1.3f); });
            Add("frenzy", "狂暴", "攻擊速度 +30%", Offense,
                s => s.AttackSpeedMul *= 0.70f);
            Add("heavy", "重擊", "普攻傷害 +40%，攻擊速度 −15%", Offense,
                s => { s.AttackDamageBonus += 0.40f; s.AttackSpeedMul *= 1.15f; });
            Add("spread", "散彈", "普攻多 2 發，普攻傷害 −25%", Offense,
                s => { s.ExtraShots += 2; s.AttackDamageBonus -= 0.25f; });
            Add("pierce", "貫穿之矢", "普攻多穿透 2 隻", Offense,
                s => s.ExtraPierce += 2);
            Add("eagle", "鷹眼", "射程 +4，普攻傷害 +15%", Offense,
                s => { s.RangeBonus += 4f; s.AttackDamageBonus += 0.15f; });
            Add("burn", "灼燒", "普攻附加持續傷害", Offense,
                s => s.BurnPower += 6f);
            Add("lone", "孤狼", "沒有隊友在場時傷害 +80%", Offense,
                s => s.LoneWolfBonus += 0.80f);
            Add("berserk", "血怒", "血量越低傷害越高，最多 +70%", Offense,
                s => s.LowHpBonus += 0.70f);

            // 原本是大覺，改成被動之後數值同步下修
            Add("blood_rage", "狂血", "血量低於一半時，全隊傷害 +50%", Offense,
                s => s.HalfHpBonus += 0.50f);
            Add("double_cast", "雙重詠唱", "男主主動技能有 40% 機率連發兩次。", Offense,
                s => s.SkillDoubleChance += 0.40f);

            // 四種傷害加成的專屬祝福（數值先暫定）
            Add("slayer", "屠龍者", "BOSS 傷害 +40%", Offense,
                s => s.BossDamageBonus += 0.40f);
            Add("armor_break", "破甲專精", "BOSS 傷害 +25%，普攻傷害 +10%", Offense,
                s => { s.BossDamageBonus += 0.25f; s.AttackDamageBonus += 0.10f; });
            Add("arcana", "術式精通", "技能傷害 +35%", Offense,
                s => s.SkillDamageBonus += 0.35f);
            Add("spellblade", "劍技共鳴", "技能傷害 +20%，普攻傷害 +15%", Offense,
                s => { s.SkillDamageBonus += 0.20f; s.AttackDamageBonus += 0.15f; });
            Add("all_out", "全力以赴", "通用傷害 +15%", Offense,
                s => s.GeneralDamageBonus += 0.15f);

            // ---------------- 生存 ----------------
            Add("vampire", "吸血", "普攻吸血 8%。", Defense,
                s => s.Lifesteal += 0.08f);
            Add("blood_thirst", "血之渴", "普攻吸血 5%，每擊殺回血 2 點。", Defense,
                s => { s.Lifesteal += 0.05f; s.HealPerKill += 2f; });

            // ---- 吸血流派（數值先暫定）----
            Add("blood_pact", "血族契約", "全能吸血 +5%", Defense,
                s => s.OmniLifesteal += 0.05f);
            Add("blood_offering", "血祭", "最大生命 -20%，全能吸血 +10%", Defense,
                s => { s.MaxHpMul *= 0.8f; s.OmniLifesteal += 0.10f; });
            Add("desperate_thirst", "絕境渴血", "生命低於 30% 時吸血效果加倍，普攻吸血 +3%", Defense,
                s => { s.LowHpLifestealMul = Mathf.Max(s.LowHpLifestealMul, 2f); s.Lifesteal += 0.03f; });
            Add("overflow", "鮮血盈溢", "滿血時，多出來的吸血轉為護盾，普攻吸血 +3%", Defense,
                s => { s.OverhealShield += 0.60f; s.Lifesteal += 0.03f; });
            Add("blood_blade", "血刃", "普攻附加 1 層流血，普攻吸血 +4%", Offense,
                s => { s.AttackBleed += 1; s.Lifesteal += 0.04f; });
            Add("thorns", "荊棘", "被碰到時反彈 250% 傷害", Defense,
                s => s.Thorns += 2.5f);
            Add("iron", "鐵壁", "受到的傷害固定減 10", Defense,
                s => s.FlatDefense += 10);
            Add("vitality", "壯碩", "最大生命 +60%", Defense,
                s => s.MaxHpMul *= 1.60f);
            Add("regen", "再生", "每秒回復 1% 最大生命", Defense,
                s => s.RegenPercent += 0.01f);
            Add("barrier", "護盾生成", "每 6 秒生成 30 點護盾", Defense,
                s => s.ShieldPerTick += 30f);
            Add("phoenix", "不死鳥", "死亡時滿血復活一次", Defense,
                s => s.Revive = true);
            Add("swift", "疾風", "移動速度 +20%", Defense,
                s => s.MoveSpeedMul *= 1.20f);

            // ---------------- 場面與資源 ----------------
            Add("magnet", "磁力場", "能量拾取範圍 +150%", Utility,
                s => s.PickupBonus += 1.5f);
            Add("scholar", "學者", "獲得的能量 +40%", Utility,
                s => s.XpMul *= 1.40f);
            Add("greed", "掠奪者", "帶回的金幣 +60%", Utility,
                s => s.GoldMul *= 1.60f);
            Add("hunter", "尋寶", "掉落機率 +50%", Utility,
                s => s.LootMul *= 1.50f);
            Add("frost", "冰霜光環", "周圍的敵人持續減速", Utility,
                s => s.SlowAura = Mathf.Max(s.SlowAura, 0.35f));
            Add("boom", "爆裂", "敵人死亡時 30% 機率爆炸", Utility,
                s => { s.ExplodeChance += 0.30f; s.ExplodePower += 22; });
            // 先手：拿到的當下隨機送 2 個被動強化（SVGame.TakeBlessing 處理，不再是開場升 3 級）
            Add("head_start", "先手", "隨機獲得 2 個被動強化", Utility,
                s => { });
            Add("focus", "專注", "男主技能冷卻 −30%", Utility,
                s => s.SkillCooldownMul *= 0.70f);

            // ---------------- 隊伍 ----------------
            Add("command", "統帥", "隊友技能威力 +35%", Team,
                s => s.CompanionPowerMul *= 1.35f);
            Add("rally", "號令", "隊友技能冷卻 −25%", Team,
                s => s.CompanionCooldownMul *= 0.75f);
            Add("guardian", "堅陣", "隊友生命 +80%，減傷 +8", Team,
                s => { s.CompanionHpMul *= 1.80f; s.CompanionDefense += 8; });

            // ---------------- 極端流派：拿掉一半換另一半 ----------------
            //
            // 這幾個刻意做成「有明顯代價」的選項。祝福是出發前就決定的，
            // 選了就整局跟著你，所以值得讓玩家做真正的取捨而不是無腦加值。

            Add("glass_cannon", "玻璃大炮", "最大生命 -50%，傷害 +50%", Offense,
                s => { s.MaxHpMul *= 0.5f; s.GeneralDamageBonus += 0.5f; });

            Add("stalwart", "剛毅", "生命越高，物理傷害越高，最高 +40%。", Offense,
                s => s.HighHpPhysicalBonus += 0.40f);

            Add("close_quarters", "近身搏殺",
                "射程 -40%；普攻傷害 +35%、生命 +25%、全能吸血 +10%。", Offense,
                s =>
                {
                    s.RangeBonus -= 2.8f;
                    s.AttackDamageBonus += 0.35f;
                    s.MaxHpMul *= 1.25f;
                    s.OmniLifesteal += 0.10f;
                });

            Add("ascetic", "苦行",
                "不能選大覺。\n技能冷卻 -50%，護盾、回血 +30%。", Utility,
                s =>
                {
                    s.NoAwakenActive = true;
                    s.SkillCooldownMul *= 0.5f;
                    s.HealMul *= 1.3f;
                    s.ShieldMul *= 1.3f;
                });

            return list;
        }

        /// <summary>抽 count 個不重複的出來。已經選過的不會再出現。</summary>
        public static List<SVPassive> Draw(List<SVPassive> catalog, int count, SVPassiveState taken)
        {
            var pool = new List<SVPassive>();
            foreach (var p in catalog)
                if (taken == null || !taken.Has(p.Id)) pool.Add(p);

            var picked = new List<SVPassive>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int index = Random.Range(0, pool.Count);
                picked.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return picked;
        }
    }
}
