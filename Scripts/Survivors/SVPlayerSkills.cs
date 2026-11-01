// 男主自己的主動技能。
//
// 原本男主只有普攻，強化清單裡也只有加數值的項目，打起來沒什麼存在感。
// 這裡提供依委託等級逐步開放的技能，用升級三選一解鎖，之後再選到同一個就升等。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public enum SVPlayerSkillKind
    {
        Whirlwind,  // 迴旋斬：周身一圈，把貼上來的推開
        Shockwave,  // 震盪波：朝面向打一發能穿透的波
        Barrier,    // 守護結界：短暫無敵並回一點血
        Chain,      // 連鎖雷：同時劈好幾隻

        // 以下三個要地圖等級夠才會出現在三選一裡
        BloodEdge,  // A 級解鎖　血刃：範圍斬 + 依傷害吸血
        Warlord,    // S 級解鎖　破軍突刺：往前衝，路徑上全部撞飛
        StarFall,   // S 級解鎖　星隕：天上連續砸下隕石

        // 法杖專屬（拿劍的抽不到）
        DarkMatter,   // B 級　暗物質：兩顆黑球繞一圈再射出去
        Thunderstorm, // A 級　雷電風暴：留下一團會自己亂跑的雷
        BlackHole,    // S 級　黑洞：把周圍的敵人吸過來

        PoisonCloud,  // 毒霧：原地留一團毒氣，站在裡面的每 2.5 秒中一層毒
        ShieldBash,   // 盾擊：向前衝並推開所有人，衝的過程幾乎免傷，還會反傷

        // 2026-09-27 新增（B／A／S 各一劍一杖）
        BladeDance,    // B　疾風連斬：朝前方連砍三刀，最後一刀擊退
        FrostNova,     // B　冰霜新星（法杖）：周身爆開寒氣，凍住雜魚
        SwordRing,     // A　劍陣：數把飛劍繞身旋轉數秒，碰到就割
        Judgment,      // A　聖光審判（法杖）：鎖定血最多的幾隻，延遲後光柱落下
        Earthshatter,  // S　天崩地裂：三道一圈比一圈大的地震波，擊退並震暈
        AbsoluteZero,  // S　絕對零度（法杖）：大範圍冰封，王改為大幅減速
    }

    /// <summary>某個技能要幾級的地圖才會出現在強化選單裡。1=C 2=B 3=A 4=S。</summary>
    public static class SVSkillUnlock
    {
        public static int RankFor(SVPlayerSkillKind kind) => kind switch
        {
            // C：基本近戰、直線攻擊與魔法連鎖。
            SVPlayerSkillKind.Whirlwind => 1,
            SVPlayerSkillKind.Shockwave => 1,
            SVPlayerSkillKind.Chain => 1,
            // B：生存與持續範圍攻擊。
            SVPlayerSkillKind.Barrier => 2,
            SVPlayerSkillKind.PoisonCloud => 2,
            SVPlayerSkillKind.DarkMatter => 2,
            // A：吸血、強力反傷與持續雷暴。
            SVPlayerSkillKind.BloodEdge => 3,
            SVPlayerSkillKind.ShieldBash => 3,
            SVPlayerSkillKind.Thunderstorm => 3,
            // S：高威力突刺、大範圍轟炸與聚怪。
            SVPlayerSkillKind.Warlord => 4,
            SVPlayerSkillKind.StarFall => 4,
            SVPlayerSkillKind.BlackHole => 4,
            SVPlayerSkillKind.BladeDance => 2,
            SVPlayerSkillKind.FrostNova => 2,
            SVPlayerSkillKind.SwordRing => 3,
            SVPlayerSkillKind.Judgment => 3,
            SVPlayerSkillKind.Earthshatter => 4,
            SVPlayerSkillKind.AbsoluteZero => 4,
            _ => 1,
        };
    }

    public class SVPlayerSkill
    {
        public SVPlayerSkillKind Kind;
        public int Level = 1;
        public float CooldownLeft;

        public string Name;
        public string Desc;

        /// <summary>技能圖示（Resources/Survivors/Icon/ 底下的檔名）。留空就顯示空框。</summary>
        public string Icon => "pskill_" + Kind;

        /// <summary>
        /// 走物理還是魔法。斬擊類算物理、雷與隕石算魔法，
        /// 會分別吃男主的物攻／魔攻加成，也分別被敵人的物防／魔防擋。
        /// </summary>
        public SVDamageKind DamageKind = SVDamageKind.Physical;

        public float BaseCooldown;
        public int BasePower;
        public int PowerPerLevel;
        public float BaseRange;
        public float RangePerLevel;

        // 全域倍率來自卡片（SVGame.SkillPowerMul 那一組），對所有技能一起生效
        static float GlobalPowerMul => SVGame.Instance != null ? SVGame.Instance.SkillPowerMul : 1f;
        static float GlobalCooldownMul => SVGame.Instance != null ? SVGame.Instance.SkillCooldownMul : 1f;

        /// <summary>每升一級冷卻縮 8%，再乘上卡片給的全域冷卻倍率。</summary>
        public float Cooldown =>
            Mathf.Max(0.25f, BaseCooldown * Mathf.Pow(0.92f, Level - 1) * GlobalCooldownMul);

        public int Power =>
            Mathf.RoundToInt((BasePower + PowerPerLevel * (Level - 1)) * GlobalPowerMul);

        public float Range => BaseRange + RangePerLevel * (Level - 1);

        public float CooldownNormalized =>
            Cooldown <= 0f ? 1f : Mathf.Clamp01(1f - CooldownLeft / Cooldown);

        public static SVPlayerSkill Create(SVPlayerSkillKind kind)
        {
            switch (kind)
            {
                case SVPlayerSkillKind.Whirlwind:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "迴旋斬", Desc = "周身一圈斬擊，把貼上來的推開",
                        BaseCooldown = 3.0f, BasePower = 14, PowerPerLevel = 7,
                        BaseRange = 2.6f, RangePerLevel = 0.25f,
                    };

                case SVPlayerSkillKind.Shockwave:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "震盪波", Desc = "朝面向打出一道會穿透的衝擊波",
                        BaseCooldown = 4.0f, BasePower = 22, PowerPerLevel = 10,
                        BaseRange = 9f, RangePerLevel = 1f,
                    };

                case SVPlayerSkillKind.Barrier:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "守護結界", Desc = "短暫無敵，並回復一些血量",
                        BaseCooldown = 14f, BasePower = 10, PowerPerLevel = 6,
                        BaseRange = 1.4f, RangePerLevel = 0.15f,
                    };

                case SVPlayerSkillKind.Chain:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "連鎖雷", DamageKind = SVDamageKind.Magic, Desc = "同時劈中好幾隻敵人",
                        BaseCooldown = 5f, BasePower = 16, PowerPerLevel = 7,
                        BaseRange = 8f, RangePerLevel = 0.6f,
                    };

                // ---------------- B / A / S 級才會出現 ----------------

                case SVPlayerSkillKind.BloodEdge:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "血刃", Desc = "橫掃一片，並把打出去的傷害吸回來一部分",
                        BaseCooldown = 6f, BasePower = 26, PowerPerLevel = 12,
                        BaseRange = 3.4f, RangePerLevel = 0.3f,
                    };

                case SVPlayerSkillKind.Warlord:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "破軍突刺", Desc = "向前衝一段，路徑上的敵人全部被撞飛",
                        BaseCooldown = 8f, BasePower = 34, PowerPerLevel = 16,
                        BaseRange = 6.5f, RangePerLevel = 0.5f,
                    };

                case SVPlayerSkillKind.StarFall:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "星隕", DamageKind = SVDamageKind.Magic, Desc = "天上連續砸下隕石，落點跟著敵群跑",
                        BaseCooldown = 12f, BasePower = 40, PowerPerLevel = 18,
                        BaseRange = 9f, RangePerLevel = 0.8f,
                    };

                // ---------------- 法杖專屬 ----------------

                case SVPlayerSkillKind.DarkMatter:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "暗物質", DamageKind = SVDamageKind.Magic,
                        Desc = "身邊浮出兩顆黑球，繞一圈之後往外射出去",
                        BaseCooldown = 7f, BasePower = 30, PowerPerLevel = 14,
                        BaseRange = 3f, RangePerLevel = 0.2f,
                    };

                case SVPlayerSkillKind.Thunderstorm:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "雷電風暴", DamageKind = SVDamageKind.Magic,
                        Desc = "留下一團會自己亂跑的雷，持續劈周圍的敵人",
                        BaseCooldown = 13f, BasePower = 22, PowerPerLevel = 10,
                        BaseRange = 2.6f, RangePerLevel = 0.25f,
                    };

                case SVPlayerSkillKind.PoisonCloud:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "毒霧", DamageKind = SVDamageKind.Magic,
                        Desc = "原地留下一團毒氣，站在裡面的敵人每 2.5 秒中一層毒。毒不會退，疊到死為止",
                        BaseCooldown = 10f, BasePower = 0, PowerPerLevel = 0,
                        BaseRange = 3.2f, RangePerLevel = 0.3f,
                    };

                case SVPlayerSkillKind.ShieldBash:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "盾擊",
                        Desc = "向前衝一段並推開路上所有敵人。衝的過程減傷 95%，期間受到的傷害會反彈給附近的敵人",
                        BaseCooldown = 9f, BasePower = 30, PowerPerLevel = 15,
                        BaseRange = 7f, RangePerLevel = 0.5f,
                    };

                case SVPlayerSkillKind.BlackHole:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "黑洞", DamageKind = SVDamageKind.Magic,
                        Desc = "召喚一個黑洞，把周圍的敵人拖進去並持續絞碎",
                        BaseCooldown = 20f, BasePower = 26, PowerPerLevel = 14,
                        BaseRange = 7f, RangePerLevel = 0.5f,
                    };

                // ---------------- 2026-09-27 新增 ----------------

                case SVPlayerSkillKind.BladeDance:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "疾風連斬", Desc = "朝前方連砍三刀，最後一刀把敵人擊退",
                        BaseCooldown = 5f, BasePower = 12, PowerPerLevel = 6,
                        BaseRange = 2.2f, RangePerLevel = 0.15f,
                    };

                case SVPlayerSkillKind.FrostNova:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "冰霜新星", DamageKind = SVDamageKind.Magic,
                        Desc = "周身爆開一圈寒氣，凍住附近的雜魚；王改為減速",
                        BaseCooldown = 9f, BasePower = 18, PowerPerLevel = 8,
                        BaseRange = 3.2f, RangePerLevel = 0.3f,
                    };

                case SVPlayerSkillKind.SwordRing:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "劍陣", Desc = "召出數把飛劍繞著自己旋轉，碰到的敵人持續受傷",
                        BaseCooldown = 12f, BasePower = 10, PowerPerLevel = 5,
                        BaseRange = 2.2f, RangePerLevel = 0.1f,
                    };

                case SVPlayerSkillKind.Judgment:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "聖光審判", DamageKind = SVDamageKind.Magic,
                        Desc = "鎖定範圍內血量最多的幾隻敵人，片刻後光柱落下重創牠們，王也吃滿傷害",
                        BaseCooldown = 11f, BasePower = 55, PowerPerLevel = 22,
                        BaseRange = 10f, RangePerLevel = 0.5f,
                    };

                case SVPlayerSkillKind.Earthshatter:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "天崩地裂", Desc = "重擊地面，三道一圈比一圈大的地震波擴散出去，擊退並短暫震暈",
                        BaseCooldown = 14f, BasePower = 38, PowerPerLevel = 16,
                        BaseRange = 8f, RangePerLevel = 0.5f,
                    };

                case SVPlayerSkillKind.AbsoluteZero:
                    return new SVPlayerSkill {
                        Kind = kind, Name = "絕對零度", DamageKind = SVDamageKind.Magic,
                        Desc = "大範圍冰封：範圍內的雜魚凍結數秒，王大幅減速",
                        BaseCooldown = 24f, BasePower = 30, PowerPerLevel = 14,
                        BaseRange = 9f, RangePerLevel = 0.6f,
                    };
            }

            return new SVPlayerSkill { Kind = kind, Name = "?", Desc = "", BaseCooldown = 5f, BasePower = 10 };
        }
    }
}
