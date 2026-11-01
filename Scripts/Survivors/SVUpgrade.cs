// 升級時的三選一強化。
//
// 每一項就是「一個名字 + 一段說明 + 一個 Apply」。要加新的強化，
// 在 SVUpgrades.BuildCatalog() 裡多寫一筆就好，不用動別的地方。
using System;
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    /// <summary>
    /// 強化的分類。只給技能圖鑑分頁用，不影響抽選 —— 抽選看的是 Available 與權重。
    /// 在 BuildCatalog 裡是一段一段蓋章的，新增卡片只要放進對的段落就會自動歸位。
    /// </summary>
    public enum SVUpgradeCategory
    {
        Basic,        // 普攻強化
        Defense,      // 防禦強化
        PlayerSkill,  // 個人技能（男主自己的主動技能）
        Team,         // 團隊強化
        SkillUp,      // 技能強化（各隊友的招式強化）
        Awaken,       // 大覺
    }

    public class SVUpgrade
    {
        /// <summary>技能圖鑑分在哪一頁。</summary>
        public SVUpgradeCategory Category = SVUpgradeCategory.Basic;

        /// <summary>
        /// 要幾級的地圖才拿得到（1=C 2=B 3=A 4=S）。
        /// 跟 Available 是同一件事的兩種寫法：Available 是抽選當下的判斷，
        /// 這個是「給人看的」條件，圖鑑靠它顯示「B 級解鎖」並決定要不要壓黑。
        /// </summary>
        public int UnlockRank = 1;

        public string Id;
        public string Name;
        public string Desc;
        /// <summary>最多能拿幾次。</summary>
        public int MaxStacks = 99;
        /// <summary>抽選權重，越大越常出現。</summary>
        public float Weight = 1f;
        /// <summary>額外的出現條件（例如某位隊友在場）。null 就是永遠可選。</summary>
        public Func<SVGame, bool> Available;
        public Action<SVGame> Apply;

        public Color Tint = new Color(0.85f, 0.72f, 0.35f);

        /// <summary>
        /// 這是一個要玩家自己按的主動技能（不是被動加成）。
        /// 選擇畫面會在說明底下標一行「（主動技能）」。
        /// </summary>
        public bool ActiveSkill;

        /// <summary>
        /// 技能圖示（Resources/Survivors/Icon/ 底下的檔名，不含副檔名）。
        /// 留空就只顯示一個空的圖示框，之後補圖不用動程式。
        /// </summary>
        string _icon; public string Icon { get => string.IsNullOrEmpty(_icon) ? Id : _icon; set => _icon = value; }

        /// <summary>
        /// 這個強化是給誰的。選擇畫面會在上面顯示這個人的頭像。
        /// 留空 = 男主自己。
        /// </summary>
        public string OwnerSheet = "";
        public int OwnerIndex;
    }

    public static class SVUpgrades
    {
        public static List<SVUpgrade> BuildCatalog()
        {
            var list = new List<SVUpgrade>();

            // 從上次蓋章之後新增的那幾張，一次標上分類與解鎖等級
            int marked = 0;
            void Stamp(SVUpgradeCategory cat, int unlockRank = 1)
            {
                for (int i = marked; i < list.Count; i++)
                {
                    list[i].Category = cat;
                    if (list[i].UnlockRank < unlockRank) list[i].UnlockRank = unlockRank;
                }
                marked = list.Count;
            }

            // ---------------- 男主 ----------------
            var playerTint = new Color(1f, 0.85f, 0.45f);

            list.Add(new SVUpgrade {
                Id = "atk", Name = "鍛鍊", Desc = "普攻傷害 +10%", MaxStacks = 12,
                Weight = 1.2f, Tint = playerTint, Icon = "Power_1",
                // 改成乘算：後期普攻基數大的時候，+3 幾乎等於沒加
                Apply = g => g.Player.AttackDamage =
                    Mathf.Max(g.Player.AttackDamage + 1, Mathf.RoundToInt(g.Player.AttackDamage * 1.10f)),
            });
            list.Add(new SVUpgrade {
                Id = "aspd", Name = "速射", Desc = "普攻間隔 -12%", MaxStacks = 8, Tint = playerTint, Icon = "Power_2",
                Apply = g => g.Player.AttackInterval = Mathf.Max(0.1f, g.Player.AttackInterval * 0.88f),
            });
            list.Add(new SVUpgrade {
                Id = "multi", Name = "散射", Desc = "普攻多一發（散開）", MaxStacks = 4, Weight = 0.7f, Tint = playerTint, Icon = "Power_3",
                Apply = g => g.Player.AttackShots += 1,
            });
            list.Add(new SVUpgrade {
                Id = "pierce", Name = "貫穿", Desc = "普攻多穿透 1 隻", MaxStacks = 4, Weight = 0.8f, Tint = playerTint, Icon = "Power_4",
                Apply = g => g.Player.AttackPierce += 1,
            });
            list.Add(new SVUpgrade {
                Id = "range", Name = "遠望", Desc = "普攻射程 +2", MaxStacks = 5, Tint = playerTint, Icon = "Power_5",
                Apply = g => g.Player.AttackRange += 2f,
            });
            list.Add(new SVUpgrade {
                Id = "speed", Name = "健步", Desc = "移動速度 +8%", MaxStacks = 6, Tint = playerTint, Icon = "Power_6",
                Apply = g => g.Player.MoveSpeed *= 1.08f,
            });
            list.Add(new SVUpgrade {
                Id = "hp", Name = "體魄", Desc = "血量上限 +25，並回復 25", Tint = playerTint,
                Apply = g => { g.Player.MaxHp += 25; g.Player.Heal(25); },
            });
            list.Add(new SVUpgrade {
                Id = "magnet", Name = "磁石", Desc = "能量拾取範圍 +50%", MaxStacks = 5, Tint = playerTint,
                Apply = g => g.AddPickupRadius(0.5f),
            });
            list.Add(new SVUpgrade {
                Id = "heal", Name = "急救", Desc = "立刻回復一半血量", Weight = 0.8f, Tint = new Color(0.5f, 1f, 0.6f),
                Available = g => g.Player.Hp < g.Player.MaxHp,
                Apply = g => g.Player.Heal(g.Player.MaxHp / 2),
            });

            Stamp(SVUpgradeCategory.Basic);

            // ---------------- B 級開始解鎖的基礎卡：防禦、血量、護盾 ----------------
            // C 級只看得到上面那幾張最基本的。B／A／S 級地圖打起來比較痛，
            // 三選一裡才多出這一批偏防禦向的選項，海克斯池子跟著地圖等級一起變大。
            var defTint = new Color(0.55f, 0.78f, 0.6f);

            list.Add(new SVUpgrade {
                Id = "b_armor", Name = "厚甲", Desc = "物理防禦 +8", MaxStacks = 6, Tint = defTint,
                Available = g => g.MapRank >= 2,
                Apply = g => g.Player.Defense += 8,
            });
            list.Add(new SVUpgrade {
                Id = "b_wardrobe", Name = "法抗織紋", Desc = "魔法防禦 +8", MaxStacks = 6, Tint = defTint,
                Available = g => g.MapRank >= 2,
                Apply = g => g.Player.MagicDefense += 8,
            });
            list.Add(new SVUpgrade {
                Id = "b_vitality", Name = "強韌體魄", Desc = "血量上限 +45，並回復 45", MaxStacks = 5, Tint = defTint,
                Available = g => g.MapRank >= 2,
                Apply = g => { g.Player.MaxHp += 45; g.Player.Heal(45); },
            });
            list.Add(new SVUpgrade {
                Id = "b_ward", Name = "護體結晶", Desc = "獲得最大生命 20% 的護盾。",
                MaxStacks = 4, Weight = 0.9f, Tint = defTint,
                Available = g => g.MapRank >= 2,
                Apply = g => g.Player.AddShield(Mathf.RoundToInt(g.Player.MaxHp * 0.2f)),
            });

            Stamp(SVUpgradeCategory.Defense, 2);

            // ---------------- A 級開始再多一批，同一套防禦系統上再疊一層 ----------------
            list.Add(new SVUpgrade {
                Id = "a_bulwark", Name = "堡壘之心", Desc = "物防、魔防各 +14，血量上限 +60",
                MaxStacks = 3, Weight = 0.85f, Tint = defTint,
                Available = g => g.MapRank >= 3,
                Apply = g => { g.Player.Defense += 14; g.Player.MagicDefense += 14; g.Player.MaxHp += 60; g.Player.Heal(60); },
            });
            list.Add(new SVUpgrade {
                Id = "a_lastward", Name = "破魂護盾", Desc = "獲得最大生命 35% 的護盾。",
                MaxStacks = 3, Weight = 0.75f, Tint = defTint,
                Available = g => g.MapRank >= 3,
                Apply = g => g.Player.AddShield(Mathf.RoundToInt(g.Player.MaxHp * 0.35f)),
            });

            Stamp(SVUpgradeCategory.Defense, 3);

            // ---------------- 男主的主動技能 ----------------
            AddPlayerSkill(list, SVPlayerSkillKind.Whirlwind, "迴旋斬", "斬擊周圍敵人並擊退。",
                           new Color(1f, 0.78f, 0.4f));
            AddPlayerSkill(list, SVPlayerSkillKind.Shockwave, "震盪波", "向前發射穿透衝擊波。",
                           new Color(1f, 0.65f, 0.35f));
            AddPlayerSkill(list, SVPlayerSkillKind.Barrier, "守護結界", "短暫無敵並回血。",
                           new Color(0.6f, 0.9f, 1f));
            AddPlayerSkill(list, SVPlayerSkillKind.Chain, "連鎖雷", "落雷同時擊中多名敵人。",
                           new Color(0.78f, 0.9f, 1f));

            // ---------------- B / A / S 級地圖才會出現的技能 ----------------
            AddPlayerSkill(list, SVPlayerSkillKind.BloodEdge, "血刃",
                           "橫掃敵人並吸血。",
                           new Color(0.95f, 0.35f, 0.42f));
            AddPlayerSkill(list, SVPlayerSkillKind.Warlord, "破軍突刺",
                           "向前突刺，擊退路徑上的敵人。",
                           new Color(1f, 0.72f, 0.3f));
            AddPlayerSkill(list, SVPlayerSkillKind.StarFall, "星隕",
                           "連續隕石轟擊敵群。",
                           new Color(1f, 0.55f, 0.85f));

            AddPlayerSkill(list, SVPlayerSkillKind.PoisonCloud, "毒霧",
                           "留下毒霧，每 2.5 秒疊毒。毒層不消退。",
                           new Color(0.6f, 0.95f, 0.5f));
            AddPlayerSkill(list, SVPlayerSkillKind.ShieldBash, "盾擊",
                           "衝鋒擊退；期間減傷 95%，反彈擋下的傷害。",
                           new Color(0.8f, 0.9f, 1f));

            // ---------------- 法杖專屬的主動技能 ----------------
            AddPlayerSkill(list, SVPlayerSkillKind.DarkMatter, "暗物質",
                           "兩顆黑球環繞後向外射出。",
                           new Color(0.72f, 0.55f, 1f), true);
            AddPlayerSkill(list, SVPlayerSkillKind.Thunderstorm, "雷電風暴",
                           "召喚移動雷雲，持續電擊附近敵人。",
                           new Color(0.75f, 0.85f, 1f), true);
            AddPlayerSkill(list, SVPlayerSkillKind.BlackHole, "黑洞",
                           "黑洞吸引附近敵人並持續傷害。",
                           new Color(0.6f, 0.45f, 0.9f), true);

            // ---------------- 2026-09-27 新增：B／A／S 各一劍一杖 ----------------
            AddPlayerSkill(list, SVPlayerSkillKind.BladeDance, "疾風連斬",
                           "向前連砍三刀，最後一刀擊退。",
                           new Color(0.95f, 0.85f, 0.55f));
            AddPlayerSkill(list, SVPlayerSkillKind.FrostNova, "冰霜新星",
                           "周身爆開寒氣，凍住雜魚、王減速。",
                           new Color(0.6f, 0.9f, 1f), true);
            AddPlayerSkill(list, SVPlayerSkillKind.SwordRing, "劍陣",
                           "飛劍繞身旋轉，持續切割碰到的敵人。",
                           new Color(0.8f, 0.9f, 1f));
            AddPlayerSkill(list, SVPlayerSkillKind.Judgment, "聖光審判",
                           "鎖定血最多的敵人，光柱延遲落下重創。",
                           new Color(1f, 0.92f, 0.55f), true);
            AddPlayerSkill(list, SVPlayerSkillKind.Earthshatter, "天崩地裂",
                           "三道擴散地震波，擊退並震暈。",
                           new Color(0.85f, 0.65f, 0.4f));
            AddPlayerSkill(list, SVPlayerSkillKind.AbsoluteZero, "絕對零度",
                           "大範圍冰封雜魚，王大幅減速。",
                           new Color(0.55f, 0.85f, 1f), true);

            Stamp(SVUpgradeCategory.PlayerSkill);

            // ---------------- 武器專屬（劍 / 法杖 二選一的池子）----------------
            AddWeaponAffixes(list);
            Stamp(SVUpgradeCategory.Basic);

            // ---------------- 全隊 ----------------
            var teamTint = new Color(0.75f, 0.8f, 1f);

            list.Add(new SVUpgrade {
                // 全隊（含男主）都吃，所以算在「通用傷害」裡，左下面板看得到
                Id = "team_power", Name = "同心", Desc = "全隊傷害 +15%", MaxStacks = 10, Weight = 1.2f, Tint = teamTint,
                Apply = g => g.TeamDamageBonus += 0.15f,
            });
            list.Add(new SVUpgrade {
                Id = "team_cd", Name = "默契", Desc = "全隊技能冷卻 -10%", MaxStacks = 8, Tint = teamTint,
                Apply = g => { foreach (var c in SVCompanion.Active) c.CooldownMul *= 0.90f; },
            });

            Stamp(SVUpgradeCategory.Team);

            // ---------------- 各別隊友 ----------------
            AddSkillUpgrades(list);
            Stamp(SVUpgradeCategory.SkillUp);

            AddHighRankUpgrades(list);
            return list;
        }

        /// <summary>
        /// 武器專屬的強化。
        ///
        /// 拿劍才會抽到迴避／暴擊／流血，拿法杖才會抽到冰風毒火的附魔 ——
        /// 兩池互斥，靠 Available 擋掉，所以法師整局都不會看到暴擊，反之亦然。
        /// 這是「換武器 = 換玩法」的地方，不是換個數字而已。
        /// </summary>
        static void AddHighRankUpgrades(List<SVUpgrade> list)
        {
            void Add(string id,string name,string desc,int rank,SVUpgradeCategory category,Action<SVGame> apply,string icon){
                list.Add(new SVUpgrade {Id=id,Name=name,Desc=desc,UnlockRank=rank,Category=category,MaxStacks=3,Weight=1.3f,
                    Icon=icon,Tint=rank==4?new Color(.9f,.65f,1f):new Color(.55f,.85f,1f),Available=g=>g.MapRank>=rank,Apply=apply});
            }
            Add("a_rune_guard","符文護衛","每 8 秒補 12 護盾，最多補至 36。重複提高一倍基礎量。",3,SVUpgradeCategory.Defense,g=>{},"b_ward");
            Add("a_frost_echo","寒霜迴響","每 8 秒對周圍造成 24 魔傷並減速 2 秒。重複 +24 傷害。",3,SVUpgradeCategory.SkillUp,g=>{},"mg_frost");
            Add("a_breakthrough","破陣訓練","普攻穿透 +1、物攻 +18。每次重複同樣增加。",3,SVUpgradeCategory.Basic,g=>{g.Player.AttackPierce++;g.Player.PhysicalPower+=18;},"pierce");
            Add("a_arcane_reach","奧術遠征","魔攻 +20、普攻射程 +1。每次重複同樣增加。",3,SVUpgradeCategory.Basic,g=>{g.Player.MagicPower+=20;g.Player.AttackRange+=1;},"mg_focus");
            Add("s_star_hunt","星芒追獵","每 8 秒攻擊附近血量最高的 3 名敵人，各 65 魔傷。重複 +65 傷害。",4,SVUpgradeCategory.SkillUp,g=>{},"pskill_Judgment");
            Add("s_soul_tide","靈魂潮汐","每 8 秒近身吸取：36 魔傷，每命中一敵回血 3（最多 15）。重複提高一倍基礎量。",4,SVUpgradeCategory.SkillUp,g=>{},"pskill_BloodEdge");
            Add("s_aegis","星界壁壘","生命上限 +80、雙防各 +12，回復 80 生命。每次重複同樣增加。",4,SVUpgradeCategory.Defense,g=>{g.Player.MaxHp+=80;g.Player.Defense+=12;g.Player.MagicDefense+=12;g.Player.Heal(80);},"a_bulwark");
            Add("s_overload","雙極超載","物攻、魔攻各 +30，普攻多一發。每次重複同樣增加。",4,SVUpgradeCategory.Basic,g=>{g.Player.PhysicalPower+=30;g.Player.MagicPower+=30;g.Player.AttackShots++;},"multi");
        }

        static void AddWeaponAffixes(List<SVUpgrade> list)
        {
            // ---- 劍：物理專屬 ----
            var swordTint = new Color(1f, 0.72f, 0.42f);

            void Sword(string id, string name, string desc, int max, float weight,
                       System.Action<SVWeaponAffix> apply)
            {
                list.Add(new SVUpgrade
                {
                    Id = id, Name = name, Desc = desc + "\n<size=14>（劍專屬）</size>",
                    MaxStacks = max, Weight = weight, Tint = swordTint,
                    Available = g => g.Player != null && !g.Player.IsMage,
                    Apply = g => apply(g.Player.Affix),
                });
            }

            Sword("sw_dodge", "身法", "迴避率 +6%（最多 45%）", 8, 1.0f,
                  a => a.Dodge = Mathf.Min(0.45f, a.Dodge + 0.06f));

            // 第一次 +25%，之後每次 +10~15%（上限 75%）；實際數字在選單上由 SVUpgradePreview 顯示
            Sword("sw_crit", "破綻", "暴擊率 +25%", 8, 1.1f,
                  a => a.TakeCritStep());

            Sword("sw_critdmg", "致命", "暴擊傷害 +40%", 6, 0.8f,
                  a => a.CritMultiplier += 0.4f);

            Sword("sw_bleed", "見血", "普攻 35% 機率造成流血，持續傷害不看防禦", 6, 1.0f,
                  a =>
                  {
                      a.BleedChance = Mathf.Min(0.85f, a.BleedChance + 0.35f);
                      a.BleedPower += 4;
                  });

            // ---- 法杖：魔法附魔 ----
            var staffTint = new Color(0.6f, 0.78f, 1f);

            void Staff(string id, string name, string desc, int max, float weight, Color tint,
                       System.Action<SVWeaponAffix> apply)
            {
                list.Add(new SVUpgrade
                {
                    Id = id, Name = name, Desc = desc + "\n<size=14>（法杖附魔）</size>",
                    MaxStacks = max, Weight = weight, Tint = tint,
                    Available = g => g.Player != null && g.Player.IsMage,
                    Apply = g => { apply(g.Player.Affix); g.Player.Affix.Tint = tint; },
                });
            }

            Staff("mg_frost", "冰霜附魔", "命中減速；2 層起有機率凍結。", 5, 1.1f,
                  new Color(0.62f, 0.88f, 1f), a => a.Frost++);

            Staff("mg_gale", "疾風附魔", "彈速與射程提升，並有機率多打一發", 5, 1.1f,
                  new Color(0.7f, 1f, 0.82f), a => a.Gale++);

            Staff("mg_venom", "劇毒附魔", "命中附加長效中毒，無視防禦。", 5, 1.0f,
                  new Color(0.72f, 1f, 0.5f), a => a.Venom++);

            Staff("mg_flame", "烈焰附魔", "命中點小範圍爆炸，波及旁邊的敵人", 5, 1.0f,
                  new Color(1f, 0.65f, 0.35f), a => a.Flame++);

            // 純魔法的兩個數值項，讓法師也有「單純變強」的選擇
            list.Add(new SVUpgrade {
                Id = "mg_focus", Name = "凝集", Desc = "魔攻 +12\n<size=14>（法杖專屬）</size>",
                MaxStacks = 10, Weight = 1.1f, Tint = staffTint,
                Available = g => g.Player != null && g.Player.IsMage,
                Apply = g => g.Player.MagicPower += 12,
            });

            list.Add(new SVUpgrade {
                Id = "sw_might", Name = "重擊", Desc = "物攻 +12\n<size=14>（劍專屬）</size>",
                MaxStacks = 10, Weight = 1.1f, Tint = swordTint,
                Available = g => g.Player != null && !g.Player.IsMage,
                Apply = g => g.Player.PhysicalPower += 12,
            });
        }

        /// <summary>
        /// 男主的主動技能。第一次選到是「習得」，之後再選到是升級 ——
        /// 用同一個 Id 讓 MaxStacks 自然變成等級上限。
        /// </summary>
        static void AddPlayerSkill(List<SVUpgrade> list, SVPlayerSkillKind kind,
                                   string name, string desc, Color tint, bool staffOnly = false)
        {
            int needRank = SVSkillUnlock.RankFor(kind);

            list.Add(new SVUpgrade
            {
                Id = "pskill_" + kind,
                ActiveSkill = true,
                MaxStacks = 6,

                // 高階地圖限定的技能出現率再高一點 —— 好不容易打到 S 級，
                // 抽不到新東西會很掃興
                Weight = needRank > 1 ? 1.8f : 1.4f,
                Tint = tint,
                Name = (needRank > 1 ? $"{name}　<size=15>[{RankName(needRank)} 級]</size>" : name)
                     + (staffOnly ? "\n<size=14>（法杖專屬）</size>" : ""),
                Desc = desc,

                // 地圖等級不夠、或拿的不是法杖，就完全不進抽選池
                UnlockRank = needRank,
                // 技能最多 3 個（Q W E）：已經有 3 個之後，只會抽到手上那幾個的升級
                Available = WithSkillCap(kind, BuildSkillGate(needRank, staffOnly)),

                Apply = g => g.Player.UnlockOrLevel(kind),
            });
        }

        /// <summary>把「地圖等級」與「法杖限定」兩個條件併成一個。都沒有就回 null（永遠可抽）。</summary>
        public const int MaxPlayerSkills = 3;

        static Func<SVGame, bool> WithSkillCap(SVPlayerSkillKind kind, Func<SVGame, bool> gate) =>
            g => (gate == null || gate(g)) && g.Player != null &&
                 (g.Player.Skills.Exists(s => s.Kind == kind) || g.Player.Skills.Count < MaxPlayerSkills);

        static Func<SVGame, bool> BuildSkillGate(int needRank, bool staffOnly)
        {
            if (needRank <= 1 && !staffOnly) return null;
            if (!staffOnly) return g => g.MapRank >= needRank;
            if (needRank <= 1) return g => g.Player != null && g.Player.IsMage;
            return g => g.MapRank >= needRank && g.Player != null && g.Player.IsMage;
        }

        static string RankName(int rank) => rank switch
        {
            2 => "B", 3 => "A", 4 => "S", _ => "C",
        };

        static void AddSkillUpgrades(List<SVUpgrade> list)
        {
            // 鈴音
            var arcane = new Color(0.8f, 0.55f, 1f);
            list.Add(Skill("suzune_pow", "鈴音・增幅魔力", "鈴音的傷害 +30%", SVSkillKind.Arcane, 8,
                           c => c.PowerMul *= 1.30f, arcane));
            list.Add(Skill("suzune_blast", "鈴音・爆炸擴散", "星辰墜落的爆炸半徑 +20%", SVSkillKind.Arcane, 5,
                           c => c.Type.BlastRadius *= 1.20f, arcane));
            list.Add(Skill("suzune_cd", "鈴音・連環詠唱", "鈴音的冷卻 -18%", SVSkillKind.Arcane, 6,
                           c => c.CooldownMul *= 0.82f, arcane));
            list.Add(Skill("suzune_cast", "鈴音・瞬發", "星辰墜落的詠唱時間 -35%", SVSkillKind.Arcane, 3,
                           c => c.Type.CastTime *= 0.65f, arcane));
            list.Add(Skill("suzune_range", "鈴音・遠見", "鈴音的施法距離 +25%", SVSkillKind.Arcane, 4,
                           c => c.RangeMul *= 1.25f, arcane));

            // 露娜
            list.Add(Skill("luna_pow", "露娜・聖光強化", "露娜的治療量 +40%", SVSkillKind.Heal, 8,
                           c => c.PowerMul *= 1.40f, new Color(0.55f, 1f, 0.65f)));
            list.Add(Skill("luna_cd", "露娜・速唱", "露娜的治療間隔 -20%", SVSkillKind.Heal, 6,
                           c => c.CooldownMul *= 0.80f, new Color(0.55f, 1f, 0.65f)));

            // 尤梨卡
            list.Add(Skill("yurika_shot", "尤梨卡・增彈", "尤梨卡每次多一發追蹤彈", SVSkillKind.Homing, 5,
                           c => c.ExtraShots += 1, new Color(1f, 0.65f, 0.9f)));
            list.Add(Skill("yurika_pow", "尤梨卡・貫心", "尤梨卡的傷害 +30%", SVSkillKind.Homing, 8,
                           c => c.PowerMul *= 1.30f, new Color(1f, 0.65f, 0.9f)));
            list.Add(Skill("yurika_cd", "尤梨卡・連射", "尤梨卡的冷卻 -18%", SVSkillKind.Homing, 6,
                           c => c.CooldownMul *= 0.82f, new Color(1f, 0.65f, 0.9f)));

            // 克萊爾
            list.Add(Skill("claire_rng", "克萊爾・追獵", "霜刃獵殺的範圍 +30%", SVSkillKind.Frost, 5,
                           c => c.RangeMul *= 1.30f, new Color(0.6f, 0.88f, 1f)));
            list.Add(Skill("claire_pow", "克萊爾・致命霜刃", "克萊爾的刺殺傷害 +35%", SVSkillKind.Frost, 8,
                           c => c.PowerMul *= 1.35f, new Color(0.6f, 0.88f, 1f)));

            // 雪瑤：常駐的刀（雪瑤之力）與法術
            var yaoTint = new Color(0.8f, 0.95f, 1f);
            list.Add(Skill("twin_blade", "雪瑤・魔刃增生", "雪瑤之力多凝聚一道魔力刃", SVSkillKind.Storm, 4,
                           c => c.SetBladeCount(c.BladeCount + 1), yaoTint));
            list.Add(Skill("twin_pow", "雪瑤・魔力增幅", "雪瑤的傷害 +35%", SVSkillKind.Storm, 8,
                           c => c.PowerMul *= 1.35f, yaoTint));
            list.Add(Skill("twin_rng", "雪瑤・法陣展開", "雪瑤之力的環繞半徑 +25%", SVSkillKind.Storm, 5,
                           c => c.RangeMul *= 1.25f, yaoTint));
            list.Add(Skill("yao_cd", "雪瑤・急詠", "雪瑤的冷卻 -18%", SVSkillKind.Storm, 6,
                           c => c.CooldownMul *= 0.82f, yaoTint));

            // 雪霜：盾與劍
            var shuangTint = new Color(0.7f, 0.85f, 1f);
            list.Add(Skill("frost_pow", "雪霜・力量", "雪霜的傷害 +35%", SVSkillKind.Bash, 8,
                           c => c.PowerMul *= 1.35f, shuangTint));
            list.Add(Skill("frost_cd", "雪霜・迅盾", "雪霜的冷卻 -18%", SVSkillKind.Bash, 6,
                           c => c.CooldownMul *= 0.82f, shuangTint));
            list.Add(Skill("frost_rng", "雪霜・長刃", "雪霜的攻擊範圍 +25%", SVSkillKind.Bash, 5,
                           c => c.RangeMul *= 1.25f, shuangTint));
        }

        static SVUpgrade Skill(string id, string name, string desc, SVSkillKind kind,
                               int maxStacks, Action<SVCompanion> apply, Color tint)
        {
            return new SVUpgrade
            {
                Id = id, Name = name, Desc = desc, MaxStacks = maxStacks, Tint = tint,
                Available = g => SVCompanion.Find(kind) != null,
                Apply = g => { var c = SVCompanion.Find(kind); if (c != null) apply(c); },
            };
        }
    }
}
