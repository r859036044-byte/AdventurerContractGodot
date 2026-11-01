// 「大覺」—— 每隔幾級出現一次的稀有三選一，抽到的是會改變打法的東西，
// 不是加 15% 那種。一局最多拿幾個由 SVGame 控制。
using System;
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public enum SVAwakenKind
    {
        None = 0,
        Berserk,     // 狂血
        TimeStop,    // 砸瓦魯多（時停）
        BladeRain,   // 劍雨
        Bond,        // 契約之絆
        Vampire,     // 吸血
        Detonate,    // 爆裂連鎖
        DoubleCast,  // 雙重詠唱
        Rocket,      // 火箭彈
        Blade,       // 劍氣（B 級）
        StaffMeteor, // 法杖專屬・星隕（A 級）
        Adrenaline,  // 腎上腺素
        Overdrive,   // 極速連擊（普攻攻速爆發）
        Collector,   // 收集者：前方斬擊，之後五秒內有機率直接處決殘血
        PoisonBurst, // 毒爆：把場上的毒層一次引爆
    }

    public static class SVAwakenings
    {
        public static List<SVUpgrade> BuildCatalog()
        {
            var tint = new Color(1f, 0.55f, 0.85f);
            var list = new List<SVUpgrade>();

            void Add(SVAwakenKind kind, string name, string desc, int maxStacks = 1,
                     Func<SVGame, bool> available = null, int unlockRank = 1)
            {
                // 有地圖等級門檻的，順便把「抽選時真的擋掉」也接上去，
                // 不然圖鑑寫著 B 級解鎖、C 級卻抽得到
                if (unlockRank > 1)
                {
                    var byRank = available;
                    available = byRank == null
                        ? (Func<SVGame, bool>)(g => g.MapRank >= unlockRank)
                        : g => g.MapRank >= unlockRank && byRank(g);
                }

                // 大覺最多拿 3 種：已經有 3 種以後，只會抽到手上那幾種的升級
                var baseAvail = available;
                available = g => (g.Awakenings.Has(kind) || g.Awakenings.DistinctCount < SVAwakenState.MaxDistinct)
                                 && (baseAvail == null || baseAvail(g));

                list.Add(new SVUpgrade
                {
                    Id = "awaken_" + kind,
                    Category = SVUpgradeCategory.Awaken,
                    UnlockRank = unlockRank,
                    ActiveSkill = true,
                    Name = "【大覺】" + name,
                    Desc = desc,
                    MaxStacks = maxStacks,
                    Weight = 1f,
                    Tint = tint,
                    Available = available,
                    Apply = g => g.GainAwakening(kind),
                });
            }

            // 狂血／血之渴／雙重詠唱不在這裡了 —— 它們本質是「一直生效」的東西，
            // 做成有冷卻的大招反而綁手綁腳。改成公會祝福（被動），數值同步下修。
            // 列舉與效果的程式碼保留著，之後想再做成大招隨時可以加回來。

            // 這兩個要先在公會跟鈴音的父親學過（技能書只有王身上掉），
            // 沒學過就完全不進三選一 —— 抽不到才是正常的
            Add(SVAwakenKind.TimeStop, "砸瓦魯多",
                "時間停止 2 秒，攻速 +200%。\n劍氣於結束時射出；冷卻 60 秒。",
                1, g => g.LearnedTimeStop);

            Add(SVAwakenKind.BladeRain, "劍雨",
                "不時落下劍雨；發動時劍雨傾盆。", 3);

            Add(SVAwakenKind.Bond, "契約之絆",
                "隊友滿血復活、不再退場，回魔加快。\n發動時全隊回滿血並短暫無敵。",
                1, g => g.LearnedBond);

            Add(SVAwakenKind.Rocket, "火箭彈",
                "向前齊射爆炸火箭，沿途留下燃燒的痕跡。\n升階增加數量與爆炸範圍。", 3);

            Add(SVAwakenKind.Blade, "劍氣",
                "向前斬出穿透劍氣。\n升階增加範圍與傷害。",
                3, null, 2);

            Add(SVAwakenKind.StaffMeteor, "星隕・法杖",
                "召喚隕石雨，不傷隊友。\n需裝備法杖。",
                3, g => g.Player != null && g.Player.IsMage, 3);

            Add(SVAwakenKind.Adrenaline, "腎上腺素",
                "持續 5 秒回血並加速。", 3);

            Add(SVAwakenKind.Overdrive, "極速連擊",
                "4 秒內普攻速度 ×10。\n升階縮短冷卻，持續時間不變。", 3);

            Add(SVAwakenKind.Collector, "收集者",
                "前方斬擊並流血；5 秒內機率處決殘血。\n暴擊提高機率，對 BOSS 效果較弱。", 3);

            Add(SVAwakenKind.PoisonBurst, "毒爆",
                "引爆所有毒層，波及附近敵人。\n毒層越多，傷害越高。", 3);

            Add(SVAwakenKind.Detonate, "爆裂連鎖",
                "敵人死亡時爆炸。\n發動時炸傷周圍敵人。", 3);

            return list;
        }
    }

    /// <summary>
    /// 大覺的主動型態。
    ///
    /// 原本大覺是「選了就永遠生效」的被動，選完之後就沒事做了，存在感很低。
    /// 改成：選到就多一個可以按的大招，有冷卻、發動後有一段爆發時間，
    /// 被動效果仍然保留（當作底），主動發動時再大幅放大。
    /// </summary>
    public class SVAwakenSkill
    {
        public SVAwakenKind Kind;
        public string Name;
        public string Desc;

        /// <summary>技能圖示（Resources/Survivors/Icon/ 底下的檔名）。留空就顯示空框。</summary>
        public string Icon => "awaken_" + Kind;

        public float BaseCooldown;
        public float Duration;

        public float CooldownLeft;
        public float ActiveLeft;

        /// <summary>
        /// 冷卻從「施放的當下」就開始跑，而不是等效果結束才開始。
        /// 劍雨用這個 —— 它的爆發期本來就是持續輸出，等演完才開始數會讓實際間隔變成
        /// 冷卻＋持續時間，手感很鈍。
        /// </summary>
        public bool CooldownOnCast;

        /// <summary>拿越多層冷卻越短。</summary>
        public int Stacks = 1;
        public float Cooldown => Mathf.Max(4f, BaseCooldown * Mathf.Pow(0.88f, Stacks - 1));

        public bool IsActive => ActiveLeft > 0f;
        public bool Ready => CooldownLeft <= 0f && !IsActive;

        public float CooldownNormalized =>
            Cooldown <= 0f ? 1f : Mathf.Clamp01(1f - CooldownLeft / Cooldown);

        public static SVAwakenSkill Create(SVAwakenKind kind)
        {
            switch (kind)
            {
                case SVAwakenKind.Berserk:
                    return new SVAwakenSkill { Kind = kind, Name = "狂血",
                        Desc = "發動後全隊傷害翻倍", BaseCooldown = 30f, Duration = 8f };

                case SVAwakenKind.TimeStop:
                    // 演出與計時都由 SVTimeStop 自己管，所以 Duration 留 0
                    // （大覺共用的 ActiveLeft 流程不介入），冷卻在那邊也有一份常數
                    return new SVAwakenSkill { Kind = kind, Name = "砸瓦魯多",
                        Desc = "時間停止 2 秒，攻速 +200%",
                        BaseCooldown = SVTimeStop.Cooldown, Duration = 0f };

                case SVAwakenKind.BladeRain:
                    return new SVAwakenSkill { Kind = kind, Name = "劍雨",
                        Desc = "發動期間天上狂落劍刃", BaseCooldown = 22f, Duration = 6f,
                        CooldownOnCast = true };

                case SVAwakenKind.Bond:
                    return new SVAwakenSkill { Kind = kind, Name = "契約之絆",
                        Desc = "全隊立刻回滿，並短暫無敵", BaseCooldown = 36f, Duration = 5f };

                case SVAwakenKind.Rocket:
                    // 瞬發，沒有持續時間 —— 按下去就是一排火箭飛出去
                    return new SVAwakenSkill { Kind = kind, Name = "火箭彈",
                        Desc = "朝正面射出一排會爆炸的火箭", BaseCooldown = 16f, Duration = 0f };

                case SVAwakenKind.Blade:
                    return new SVAwakenSkill { Kind = kind, Name = "劍氣",
                        Desc = "朝正面轟出一道巨大劍氣", BaseCooldown = 14f, Duration = 0f };

                case SVAwakenKind.StaffMeteor:
                    return new SVAwakenSkill { Kind = kind, Name = "星隕・法杖",
                        Desc = "隕石雨砸下來，不會傷到自己人", BaseCooldown = 20f, Duration = 0f };

                case SVAwakenKind.Adrenaline:
                    return new SVAwakenSkill { Kind = kind, Name = "腎上腺素",
                        Desc = "五秒內大量回血並加速", BaseCooldown = 26f, Duration = 5f };

                case SVAwakenKind.Overdrive:
                    return new SVAwakenSkill { Kind = kind, Name = "極速連擊",
                        Desc = "短時間內普攻速度十倍", BaseCooldown = 24f, Duration = 4f };

                case SVAwakenKind.Collector:
                    return new SVAwakenSkill { Kind = kind, Name = "收集者",
                        Desc = "前方一斬，之後五秒內有機率處決殘血",
                        BaseCooldown = 28f, Duration = 5f, CooldownOnCast = true };

                case SVAwakenKind.PoisonBurst:
                    return new SVAwakenSkill { Kind = kind, Name = "毒爆",
                        Desc = "引爆場上所有的毒層", BaseCooldown = 18f, Duration = 0f };

                case SVAwakenKind.Vampire:
                    return new SVAwakenSkill { Kind = kind, Name = "血之渴",
                        Desc = "發動期間傷害有一半回到身上", BaseCooldown = 30f, Duration = 10f };

                case SVAwakenKind.Detonate:
                    return new SVAwakenSkill { Kind = kind, Name = "爆裂連鎖",
                        Desc = "炸傷周圍敵人", BaseCooldown = 25f, Duration = 0f };

                case SVAwakenKind.DoubleCast:
                    return new SVAwakenSkill { Kind = kind, Name = "雙重詠唱",
                        Desc = "發動期間全隊冷卻減半", BaseCooldown = 28f, Duration = 8f };
            }

            return new SVAwakenSkill { Kind = kind, Name = "?", Desc = "", BaseCooldown = 30f, Duration = 5f };
        }
    }

    /// <summary>一局之內拿到的大覺，以及它們的實際效果。</summary>
    public class SVAwakenState
    {
        readonly Dictionary<SVAwakenKind, int> _taken = new Dictionary<SVAwakenKind, int>();

        /// <summary>拿到的大覺各自對應一個可以按的大招。</summary>
        public readonly List<SVAwakenSkill> Skills = new List<SVAwakenSkill>();

        /// <summary>清單變動的版本號，HUD 靠它決定要不要重建按鈕。</summary>
        public int Version { get; private set; }

        public SVAwakenSkill SkillOf(SVAwakenKind kind) => Skills.Find(s => s.Kind == kind);
        public bool IsActive(SVAwakenKind kind) => SkillOf(kind)?.IsActive ?? false;

        public int Stacks(SVAwakenKind kind) => _taken.TryGetValue(kind, out var n) ? n : 0;
        public bool Has(SVAwakenKind kind) => Stacks(kind) > 0;

        /// <summary>大覺最多選幾種。</summary>
        public const int MaxDistinct = 3;

        /// <summary>目前拿了幾種大覺。</summary>
        public int DistinctCount
        {
            get { int n = 0; foreach (var kv in _taken) if (kv.Value > 0) n++; return n; }
        }

        public void Gain(SVAwakenKind kind)
        {
            _taken[kind] = Stacks(kind) + 1;

            var skill = SkillOf(kind);
            if (skill == null)
            {
                skill = SVAwakenSkill.Create(kind);
                Skills.Add(skill);
            }
            skill.Stacks = Stacks(kind);

            // 剛拿到就能立刻用一次，選完馬上有反饋
            skill.CooldownLeft = 0f;
            Version++;
        }

        public void Clear()
        {
            _taken.Clear();
            Skills.Clear();
            Version++;
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < Skills.Count; i++)
            {
                var s = Skills[i];
                if (s.ActiveLeft > 0f)
                {
                    s.ActiveLeft -= dt;

                    // 施放就開始冷卻的：效果演出的同時冷卻照跑，結束時不再重設
                    if (s.CooldownOnCast)
                    {
                        if (s.CooldownLeft > 0f) s.CooldownLeft -= dt;
                    }
                    else if (s.ActiveLeft <= 0f) s.CooldownLeft = s.Cooldown;
                }
                else if (s.CooldownLeft > 0f) s.CooldownLeft -= dt;
            }
        }

        public IEnumerable<SVAwakenKind> All => _taken.Keys;

        // ---- 查詢用的便利屬性 ----

        /// <summary>
        /// 狂血：平常血量低於一半時傷害 1.5 倍（被動的底），
        /// 主動發動期間直接翻倍，而且不看血量。
        /// </summary>
        /// <summary>全域傷害倍率（普攻、男主技能、隊友技能都吃）：狂血 × 大覺重複選擇的加成。</summary>
        public float DamageMultiplier(SVPlayer player) => BerserkMultiplier(player) * (1f + RepeatDamageBonus);

        /// <summary>
        /// 狂血：發動期間 ×2，殘血（一半以下）×1.5。
        /// 重複選擇會加強：每多一階，發動 +0.25、殘血 +0.15。
        /// </summary>
        public float BerserkMultiplier(SVPlayer player)
        {
            int n = Stacks(SVAwakenKind.Berserk);
            if (n <= 0) return 1f;
            if (IsActive(SVAwakenKind.Berserk)) return 2f + 0.25f * (n - 1);

            if (player == null || player.MaxHp <= 0) return 1f;
            return (float)player.Hp / player.MaxHp <= 0.5f ? 1.5f + 0.15f * (n - 1) : 1f;
        }

        /// <summary>重複選擇原本「只縮冷卻、不加傷害」的大覺時，每多一階全域傷害 +10%。</summary>
        public const float RepeatDamageStep = 0.10f;

        /// <summary>這些大覺升階本身不會加傷害，重複選擇改成額外給全域傷害。</summary>
        public static readonly SVAwakenKind[] RepeatDamageKinds =
        {
            SVAwakenKind.Overdrive, SVAwakenKind.TimeStop, SVAwakenKind.Bond, SVAwakenKind.DoubleCast,
            SVAwakenKind.Adrenaline, SVAwakenKind.Vampire, SVAwakenKind.Collector,
        };

        public float RepeatDamageBonus
        {
            get
            {
                int extra = 0;
                foreach (var k in RepeatDamageKinds) extra += Mathf.Max(0, Stacks(k) - 1);
                return extra * RepeatDamageStep;
            }
        }

        /// <summary>血之渴：平常每層 10%，主動發動期間拉到 50%。</summary>
        public float Lifesteal =>
            IsActive(SVAwakenKind.Vampire) ? 0.5f : Stacks(SVAwakenKind.Vampire) * 0.10f;

        /// <summary>
        /// 爆裂連鎖的爆炸威力與半徑。
        /// 半徑刻意壓得比劍雨小很多 —— 它是「每隻死掉都會炸」，
        /// 範圍一大就變成整場自動清怪，其他技能都沒事做了。
        /// </summary>
        public int DetonatePower => Stacks(SVAwakenKind.Detonate) * 12;
        public float DetonateRadius => Stacks(SVAwakenKind.Detonate) > 0
            ? 1.0f + (Stacks(SVAwakenKind.Detonate) - 1) * 0.22f : 0f;

        /// <summary>
        /// 劍雨每秒掉幾把。
        /// 平常只是零星落下（每階 0.5 把／秒），發動期間才下大雨（每秒 6＋2×階）。
        /// 原本平常就每階 3 把／秒、發動 ×6，沒按技能也在清場，太強。
        /// </summary>
        public float BladeRainRate =>
            Stacks(SVAwakenKind.BladeRain) <= 0 ? 0f
            : IsActive(SVAwakenKind.BladeRain) ? 6f + Stacks(SVAwakenKind.BladeRain) * 2f
            : Stacks(SVAwakenKind.BladeRain) * 0.5f;

        /// <summary>單把劍的殺傷半徑。</summary>
        public float BladeRainRadius =>
            2.6f + (Stacks(SVAwakenKind.BladeRain) - 1) * 0.35f;

        /// <summary>劍會落在離玩家多遠的範圍內。</summary>
        public float BladeRainSpread =>
            9.5f + (Stacks(SVAwakenKind.BladeRain) - 1) * 1.2f;

        /// <summary>單把劍的傷害。</summary>
        // ---- 收集者：處決 ----
        //
        // 一般敵人看「絕對血量」，王看「血量百分比」——
        // 王的血是好幾千，用絕對值判斷的話永遠碰不到，用百分比才有意義。

        /// <summary>收集者發動中。</summary>
        public bool Collecting => IsActive(SVAwakenKind.Collector);

        /// <summary>一般敵人：血量低於這個值就進入處決範圍。</summary>
        public int ExecuteHpThreshold => 50 + (Stacks(SVAwakenKind.Collector) - 1) * 25;

        /// <summary>一般敵人的處決機率。</summary>
        public float ExecuteChance => 0.15f + (Stacks(SVAwakenKind.Collector) - 1) * 0.05f;

        /// <summary>王：血量百分比低於這個才吃得到處決。</summary>
        public const float ExecuteBossHpRatio = 0.30f;

        /// <summary>王的處決機率。</summary>
        public float ExecuteBossChance => 0.05f + (Stacks(SVAwakenKind.Collector) - 1) * 0.02f;

        /// <summary>暴擊時處決機率的加成倍率。</summary>
        public const float ExecuteCritBonus = 1.5f;

        /// <summary>毒爆：每層毒引爆時造成多少傷害。</summary>
        public int PoisonBurstPerStack => 14 + Stacks(SVAwakenKind.PoisonBurst) * 8;

        /// <summary>極速連擊發動中，普攻間隔要除以這個。</summary>
        public float AttackSpeedMultiplier => IsActive(SVAwakenKind.Overdrive) ? 10f : 1f;

        /// <summary>腎上腺素發動中的移動加成。</summary>
        public float MoveSpeedMultiplier => IsActive(SVAwakenKind.Adrenaline) ? 1.8f : 1f;

        /// <summary>腎上腺素發動中，每秒回多少血（依層數）。</summary>
        public float AdrenalineRegen =>
            IsActive(SVAwakenKind.Adrenaline) ? 6f + Stacks(SVAwakenKind.Adrenaline) * 4f : 0f;

        /// <summary>劍氣：傷害與寬度。</summary>
        public int BladeDamage => 70 + Stacks(SVAwakenKind.Blade) * 45;
        public float BladeWidth => 2.6f + (Stacks(SVAwakenKind.Blade) - 1) * 0.5f;

        /// <summary>法杖星隕：幾顆、單顆傷害、爆炸半徑。</summary>
        public int StaffMeteorCount => 4 + Stacks(SVAwakenKind.StaffMeteor);
        public int StaffMeteorDamage => 60 + Stacks(SVAwakenKind.StaffMeteor) * 32;
        public float StaffMeteorRadius => 1.9f + (Stacks(SVAwakenKind.StaffMeteor) - 1) * 0.2f;

        /// <summary>火箭彈：一次射幾發。</summary>
        public int RocketCount => 5 + (Stacks(SVAwakenKind.Rocket) - 1) * 2;

        /// <summary>火箭彈：單發直接命中的傷害。</summary>
        public int RocketDamage => 55 + Stacks(SVAwakenKind.Rocket) * 30;

        /// <summary>火箭彈：爆炸半徑。</summary>
        public float RocketSplashRadius => 2.2f + (Stacks(SVAwakenKind.Rocket) - 1) * 0.35f;

        public int BladeRainDamage =>
            32 + Stacks(SVAwakenKind.BladeRain) * 18;   // 原本 48＋28×階

        public bool DoubleCast => Has(SVAwakenKind.DoubleCast);

        /// <summary>雙重詠唱主動發動期間，全隊冷卻減半。</summary>
        public float CompanionCooldownScale => IsActive(SVAwakenKind.DoubleCast) ? 0.5f : 1f;
        public bool CompanionsImmortal => Has(SVAwakenKind.Bond);
        public float CompanionManaScale => Has(SVAwakenKind.Bond) ? 1.35f : 1f;
        public bool TimeStop => Has(SVAwakenKind.TimeStop);

        /// <summary>契約之絆主動發動期間，隊友完全無敵。</summary>
        public bool CompanionsInvincible => IsActive(SVAwakenKind.Bond);
    }
}
