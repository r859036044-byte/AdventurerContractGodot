// 各條線的隊伍名單。
//
// 「加入別人的隊伍」時出場的是那位女主自己的班底，不是五位女主全上 ——
// 尤梨卡帶著凱，露娜是老公塔克那一小隊，公主克萊爾有四個護衛，鈴音一個人，
// 雪瑤雪霜本來就是兩個人。行走圖與編號都對著 RM 事件上實際掛的圖。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    /// <summary>RM 變數 59「選擇的隊伍」的編號。</summary>
    public enum SVHeroine
    {
        Yurika = 1,     // 尤梨卡
        Luna = 2,       // 露娜
        Claire = 3,     // 克萊爾（公主）
        WolfSisters = 4,// 雪瑤雪霜
        Suzune = 5,     // 鈴音
    }

    public static class SVParties
    {
        // ---------------- 女主本人 ----------------

        public static SVCompanionType Yurika() => new SVCompanionType {
            Name = "尤梨卡", SheetName = "小人1", SheetIndex = 2,
            Hp = 90, Defense = 8, Skill = SVSkillKind.Homing, Power = 7, Cooldown = 1.1f, Range = 8f,
            SkillName = "追蹤劍氣", SkillDesc = "一次三發粉紅劍氣，自動鎖不同的敵人",
            SlashSprite = "Yurika_attack", SlashSize = 0.85f,
        };

        /// <summary>
        /// 露娜：奶媽。定期回血，另外也會替男主張一層護盾 ——
        /// 她的護盾比鈴音薄但補得勤，兩個人都在的時候會互相補位。
        /// </summary>
        public static SVCompanionType Luna() => new SVCompanionType {
            Name = "露娜", SheetName = "小人1", SheetIndex = 6,
            Hp = 80, Defense = 6, Skill = SVSkillKind.Heal, Power = 9, Cooldown = 3.2f, Range = 0f,
            PlayerShieldPower = 30, PlayerShieldInterval = 6f,
            SkillName = "聖光", SkillDesc = "定期回復男主的血量，並固定為他張一層護盾",
        };

        public static SVCompanionType Claire() => new SVCompanionType {
            Name = "克萊爾", SheetName = "小人1", SheetIndex = 4,
            Hp = 100, Defense = 12, Skill = SVSkillKind.Frost, Power = 5, Cooldown = 1.6f, Range = 3.2f,
            SkillName = "霜刃獵殺", SkillDesc = "以寒霜封住退路，連續刺傷身旁敵人。範圍減速並持續造成傷害。",
        };

        /// <summary>
        /// 雪瑤：法系的那個。身邊常駐兩把繞圈的刀（雪瑤之力），
        /// 另外輪流放連鎖雷電與冰風暴。刀的傷害獨立於法術威力，各調各的。
        /// </summary>
        public static SVCompanionType Xueyao() => new SVCompanionType {
            Name = "雪瑤", SheetName = "小人3", SheetIndex = 0,
            Hp = 210, Defense = 20,
            Skill = SVSkillKind.Storm, Power = 26, Cooldown = 3.4f, Range = 5.5f,
            OrbitBlades = true, BladePower = 28,
            SkillName = "雪瑤之力", SkillDesc = "兩道常駐環繞的魔力刃，另外輪流施放連鎖雷電與冰風暴",
        };

        /// <summary>
        /// 雪霜：近戰的那個。輪流放盾衝擊（撞開一條路）與劍刃（面前一片）。
        /// 血跟防都比姊姊高，站在前面擋。
        /// </summary>
        public static SVCompanionType Xueshuang() => new SVCompanionType {
            Name = "雪霜", SheetName = "小人3", SheetIndex = 4,
            Hp = 260, Defense = 26,
            Skill = SVSkillKind.Bash, Power = 34, Cooldown = 2.8f, Range = 2.6f,
            SkillName = "盾衝擊／劍刃", SkillDesc = "輪流用盾撞開一條路，再一刀掃過面前",
        };

        /// <summary>
        /// 舊呼叫點的相容殼：只回傳雪瑤。
        /// 兩姊妹現在是兩個獨立隊友，正式的組隊請走 AloneParty()／PartyOf()。
        /// </summary>
        public static SVCompanionType WolfSisters() => Xueyao();

        /// <summary>
        /// 鈴音：大魔法師。遠距離往敵人最密的地方轟，範圍大、傷害高、冷卻長。
        /// 另外帶「魔導護盾」—— 雜魚完全打不動她，只有王能造成傷害。
        /// </summary>
        public static SVCompanionType Suzune() => new SVCompanionType {
            Name = "鈴音", SheetName = "小人1", SheetIndex = 0,
            Hp = 150, Defense = 30, BossDamageMultiplier = 3f,
            Skill = SVSkillKind.Arcane,
            Power = 85, Cooldown = 3.0f, Range = 11f,
            CastTime = 0.7f, BlastRadius = 4.5f,
            ShieldBossOnly = true,
            PlayerShieldPower = 60, PlayerShieldInterval = 8f,
            SkillName = "四元魔導",
            SkillDesc = "星辰／落雷／凍結／星隕輪流施放。星隕的火場敵我不分，別站在裡面",
            Elements = SuzuneElements(),
        };

        /// <summary>
        /// 鈴音的三個元素，照順序輪流放。
        /// 特效檔名對應 RM 專案 effects/ 底下那批 Effekseer 特效。
        ///
        /// 三者的定位刻意分開：
        ///   星辰 = 單點最大傷害　落雷 = 分散清場　凍結 = 控場
        /// </summary>
        public static List<SVArcaneElement> SuzuneElements() => new List<SVArcaneElement>
        {
            new SVArcaneElement {
                Name = "星辰墜落", Effect = "LightAll2",
                Strikes = 1, DamageMul = 1.25f, RadiusMul = 1f, Knockback = 5f,
                WarnColor = new Color(0.80f, 0.60f, 1f, 0.55f),
                PulseColor = new Color(0.85f, 0.60f, 1f, 0.75f),
            },

            new SVArcaneElement {
                // 落雷：一次劈三個不同的敵群，單發傷害低一點但總量高，還會短暫麻痺
                // 兩個都用 ThunderOne1：這是實際匯入成 .asset 的那顆，
                // 每一道落點各劈一發，比整片的 ThunderAll1 更看得出「劈下來」
                Name = "落雷", Effect = "ThunderOne1", StrikeEffect = "ThunderOne1",
                MarkerImage = "Suzuka_Lightning",
                Strikes = 3, DamageMul = 0.75f, RadiusMul = 0.75f, Knockback = 2f,
                SlowFactor = 0.35f, SlowSeconds = 1.2f,
                WarnColor = new Color(1f, 0.95f, 0.45f, 0.6f),
                PulseColor = new Color(1f, 0.98f, 0.6f, 0.8f),
            },

            new SVArcaneElement {
                // 凍結：傷害最低，但把整群怪定在原地，給隊友輸出的空檔
                Name = "凍結", Effect = "IceAll2", MarkerImage = "Suzuka_Ice",
                Strikes = 1, DamageMul = 0.6f, RadiusMul = 1.35f, Knockback = 0f,
                FreezeSeconds = 2.2f,
                WarnColor = new Color(0.55f, 0.85f, 1f, 0.6f),
                PulseColor = new Color(0.65f, 0.9f, 1f, 0.8f),
            },

            new SVArcaneElement {
                // 星隕：兩顆隕石砸下來，落點會留火。
                // 這是唯一會誤傷男主的一招 —— 火場清得很快，但你不能站在裡面。
                // 兩發都用 FireOne2（單體版）。
                // 原本第一發放 FireAll2 —— 那是 RM「燒全體」的動畫，
                // 整片火加煙霧鋪滿畫面，再被半徑放大兩倍多，一放就掉幀。
                // 單體版是「一顆隕石砸在這裡」，落在兩個點上其實更像隕石雨。
                Name = "星隕", Effect = "FireOne2", StrikeEffect = "FireOne2",
                MarkerImage = "Suzuka_Fire",
                EffectScale = 0.65f,     // 再縮一點：施放當幀的填充率就是卡頓的來源
                Strikes = 2, DamageMul = 1.1f, RadiusMul = 1.1f, Knockback = 6f,

                BurnSeconds = 10f,       // 火場時間砍半：夠久到要繞路，但不會整場鋪滿
                BurnRadiusMul = 0.9f,
                BurnDamageMul = 0.20f,   // 每 0.5 秒燒掉本體傷害的兩成
                BurnPlayerDamage = 2,    // 男主每 0.5 秒只掉 2，站三秒也才 12
                // 有實際的燃燒地面貼圖之後，這裡只當「整體亮度與最大不透明度」用，
                // 不再是染色 —— 染太重會把圖本身的橘紅蓋掉
                BurnColor = new Color(1f, 0.92f, 0.88f, 0.85f),

                WarnColor = new Color(1f, 0.55f, 0.25f, 0.6f),
                PulseColor = new Color(1f, 0.65f, 0.3f, 0.85f),
            },
        };

        // ---------------- 隨行的班底 ----------------

        // 近戰站前面，血厚；射手站後面，血薄
        static SVCompanionType Melee(string name, string sheet, int index,
                                     int power, float cooldown, string skillName, int hp = 110)
            => new SVCompanionType {
                Name = name, SheetName = sheet, SheetIndex = index, Hp = hp, Defense = 10,
                Skill = SVSkillKind.Melee, Power = power, Cooldown = cooldown, Range = 1.6f,
                SkillName = skillName, SkillDesc = "貼上去砍，會把敵人推開",
            };

        static SVCompanionType Archer(string name, string sheet, int index,
                                      int power, float cooldown, string skillName, int hp = 70)
            => new SVCompanionType {
                Name = name, SheetName = sheet, SheetIndex = index, Hp = hp, Defense = 4,
                Skill = SVSkillKind.Arrow, Power = power, Cooldown = cooldown, Range = 9f,
                SkillName = skillName, SkillDesc = "遠遠射一發，能穿過一隻",
            };

        /// <summary>凱：尤梨卡的搭檔。近戰，揮的是黃色劍氣。</summary>
        public static SVCompanionType Kai()
        {
            var kai = Melee("凱", "yellow mao", 0, 12, 1.4f, "重斬", 160);
            kai.SlashSprite = "Kai_attack";
            kai.SlashSize = 1.25f;
            kai.SkillDesc = "貼上去揮一道黃色劍氣，會把敵人推開";
            return kai;
        }
        public static SVCompanionType Tark() => Melee("塔克", "luna husband", 0, 14, 1.6f, "盾擊", 190);

        // ---------------- 隊伍表 ----------------

        /// <summary>加入某位女主的隊伍時，實際跟你出門的是誰。</summary>
        public static List<SVCompanionType> PartyOf(SVHeroine who)
        {
            switch (who)
            {
                case SVHeroine.Yurika:
                    return new List<SVCompanionType> { Yurika(), Kai() };

                case SVHeroine.Luna:
                    // 露娜的老公小隊：塔克 + 公會登記的四名隊員
                    return new List<SVCompanionType> {
                        Luna(), Tark(),
                        Archer("露娜隊員", "Actor1", 6, 8, 1.3f, "射擊"),
                        Melee ("露娜隊員", "Actor1", 4, 9, 1.5f, "劈砍"),
                        Melee ("露娜隊員", "Actor1", 0, 9, 1.5f, "劈砍"),
                        Archer("露娜隊員", "Actor1", 3, 8, 1.3f, "射擊"),
                    };

                case SVHeroine.Claire:
                    // 公主出門一定帶四個護衛
                    return new List<SVCompanionType> {
                        Claire(),
                        Melee("護衛", "Actor3", 0, 10, 1.4f, "槍突"),
                        Melee("護衛", "Actor3", 0, 10, 1.4f, "槍突"),
                        Melee("護衛", "Actor3", 0, 10, 1.4f, "槍突"),
                        Melee("護衛", "Actor3", 0, 10, 1.4f, "槍突"),
                    };

                case SVHeroine.WolfSisters:
                    // 兩姊妹現在是兩個獨立隊友：一起上場，血條與技能各自分開
                    return new List<SVCompanionType> { Xueyao(), Xueshuang() };

                case SVHeroine.Suzune:
                    return new List<SVCompanionType> { Suzune() };
            }

            return new List<SVCompanionType>();
        }

        /// <summary>僱傭／自己組隊時只有她本人跟你走，班底不算在內。</summary>
        /// <summary>
        /// 單獨帶這位女主出門時，場上實際會有誰。
        /// 雪瑤雪霜是一組兩個人 —— 一起行動，但血條與技能各自獨立。
        /// </summary>
        public static List<SVCompanionType> AloneParty(SVHeroine who)
        {
            if (who == SVHeroine.WolfSisters)
                return new List<SVCompanionType> { Xueyao(), Xueshuang() };

            var one = Alone(who);
            return one != null ? new List<SVCompanionType> { one } : null;
        }

        public static SVCompanionType Alone(SVHeroine who)
        {
            switch (who)
            {
                case SVHeroine.Yurika: return Yurika();
                case SVHeroine.Luna: return Luna();
                case SVHeroine.Claire: return Claire();
                case SVHeroine.WolfSisters: return WolfSisters();
                case SVHeroine.Suzune: return Suzune();
            }
            return null;
        }

        public static bool IsValid(int id) => id >= 1 && id <= 5;
    }
}
