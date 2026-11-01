// 四個難度的怪物與王。
//
// 委託書的 MapRank 是 1=C 2=B 3=A 4=S，四個級別各有自己的一套敵人與一隻王，
// 不只是同一批怪加血量 —— 換圖、換速度、換掉落，打起來才有「換地圖了」的感覺。
//
// 行走圖的索引是照 MZ 的 8 人圖排的（0-7）。看起來不對就在這裡改編號，
// 或是在 SVGame 的 Inspector 直接編輯 Enemies 清單。
//
// ---- 掉落為什麼掉「道具」而不是武器／防具 ----
// 這個遊戲的裝備是以「道具」形式放在背包裡的（Items.json），
// 再由公共事件 CE40「背包裝備裝備」換成真正的裝備。
// 所以掉落給道具版才進得了背包、才用得到。
//
// Items.json 的結構是很整齊的 5 個系列 × 20 件（5 部位 × 4 品質）：
//   新手（樺木） 2~21    砂岩 23~42    鍛鐵 44~63    鋼鐵 65~84    星空 86~105
//   每個系列裡：長劍 +0~3、胸甲 +4~7、護腿 +8~11、頭盔 +12~15、護靴 +16~19
//   四個品質依序是 粗製 / 精良 / 稀有 / 史詩
// 所以編號可以用算的，不用一個一個查。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    /// <summary>裝備系列。數字就是該系列第一件（長劍・粗製）的道具 ID。</summary>
    public enum SVGearSeries
    {
        Novice = 2,      // 樺木
        Sandstone = 23,  // 砂岩
        Iron = 44,       // 鍛鐵
        Steel = 65,      // 鋼鐵
        Starry = 86,     // 星空
    }

    public enum SVGearSlot { Sword = 0, Chest = 1, Legs = 2, Helm = 3, Boots = 4 }

    public enum SVGearQuality { Crude = 0, Fine = 1, Rare = 2, Epic = 3 }

    public static class SVEnemyTiers
    {
        /// <summary>C=1 B=2 A=3 S=4。超出範圍會被夾住。</summary>
        public static List<SVEnemyType> RosterFor(int mapRank)
        {
            int rank = Mathf.Clamp(mapRank, 1, 4);

            var roster = rank switch
            {
                1 => RankC(),
                2 => RankB(),
                3 => RankA(),
                _ => RankS(),
            };

            // 每一隻雜魚都補上一點點禮物掉落。地圖等級越高機率越好。
            // 統一在這裡加，四張表就不用各自重複寫一次。
            foreach (var type in roster)
            {
                if (type == null || type.IsBoss) continue;
                MobGifts(type.Drops, 1f + (rank - 1) * 0.5f);
            }

            if (rank == 1) EaseRankC(roster);

            return roster;
        }

        // ---------------- C 級整體調整（2026-09-26） ----------------
        // C 級是給玩家熟悉操作的，打起來要輕鬆、但東西不能給太多。
        // 三個倍率統一套在整張 C 級表（含王、含上面補的禮物），要再調改這三個數字就好。

        /// <summary>C 級道具掉落機率倍率：裝備、法杖、捲軸、禮物都算；金幣不算。</summary>
        public const float RankCDropScale = 0.3f;

        /// <summary>C 級怪物（含王）血量倍率。</summary>
        public const float RankCHpScale = 0.75f;

        /// <summary>C 級怪物（含王）傷害倍率。王的衝撞、技能傷害都從 ContactDamage 換算，會一起降。</summary>
        public const float RankCDamageScale = 0.7f;

        static void EaseRankC(List<SVEnemyType> roster)
        {
            foreach (var type in roster)
            {
                if (type == null) continue;

                type.Hp = Mathf.Max(1, Mathf.RoundToInt(type.Hp * RankCHpScale));
                if (type.FinalBossHp > 0)
                    type.FinalBossHp = Mathf.Max(1, Mathf.RoundToInt(type.FinalBossHp * RankCHpScale));
                type.ContactDamage = Mathf.Max(1, Mathf.RoundToInt(type.ContactDamage * RankCDamageScale));

                foreach (var drop in type.Drops)
                    if (drop != null && drop.Kind != SVLootKind.Gold) drop.Chance *= RankCDropScale;
            }
        }

        public static string RankName(int mapRank) => Mathf.Clamp(mapRank, 1, 4) switch
        {
            1 => "C",
            2 => "B",
            3 => "A",
            _ => "S",
        };

        static SVLootEntry Drop(SVLootKind kind, int id, float chance, int min = 1, int max = 1) =>
            new SVLootEntry { Kind = kind, ItemId = id, Chance = chance, Min = min, Max = max };

        /// <summary>算出某系列、某部位、某品質的道具 ID。</summary>
        public static int Gear(SVGearSeries series, SVGearSlot slot, SVGearQuality quality) =>
            (int)series + (int)slot * 4 + (int)quality;

        /// <summary>
        /// 法杖的道具 ID。
        ///
        /// 劍那五個系列每個佔了 20 個連號（五部位 × 四品質），塞不下第六種部位，
        /// 所以法杖另外開一段：Items.json 181~200，順序跟 SVGearSeries 一樣
        /// （樺木 → 砂岩 → 鍛鐵 → 鋼鐵 → 星空），每系列四個品質。
        /// </summary>
        public const int StaffFirstItemId = 181;

        public static int Staff(SVGearSeries series, SVGearQuality quality) =>
            StaffFirstItemId + SeriesIndex(series) * 4 + (int)quality;

        static int SeriesIndex(SVGearSeries series) => series switch
        {
            SVGearSeries.Novice => 0,
            SVGearSeries.Sandstone => 1,
            SVGearSeries.Iron => 2,
            SVGearSeries.Steel => 3,
            _ => 4,
        };

        /// <summary>
        /// 裝備掉落的總倍率。
        ///
        /// 一場探索動輒殺幾百隻，每隻 5~10% 的話一趟就是二三十件裝備，
        /// 背包很快就爆掉、而且撿到的東西完全不值錢。這裡統一打折，
        /// 要調整整體「裝備給得多不多」改這一個數字就好。
        ///
        /// 雜魚的裝備現在只剩原本的十分之一（0.25 → 0.025）——
        /// 裝備要從王身上拿，雜魚掉的是驚喜不是收入。
        /// </summary>
        public const float GearChanceScale = 0.025f;

        /// <summary>王的掉落不打那麼多折 —— 打王本來就是為了裝備。</summary>
        public const float BossGearChanceScale = 0.55f;

        /// <summary>掉一件裝備（道具形式）。</summary>
        static SVLootEntry Gear(SVGearSeries series, SVGearSlot slot, SVGearQuality quality,
                                float chance, int min = 1, int max = 1) =>
            Drop(SVLootKind.Item, Gear(series, slot, quality), chance * GearChanceScale, min, max);

        /// <summary>王專用：折扣比較輕。</summary>
        static SVLootEntry BossGear(SVGearSeries series, SVGearSlot slot, SVGearQuality quality,
                                    float chance, int min = 1, int max = 1) =>
            Drop(SVLootKind.Item, Gear(series, slot, quality),
                 chance * BossGearChanceScale, min, max);

        // ---------------- 好感／墮落道具 ----------------

        // Items.json 141「-----好感墮落道具-----」那一段
        static readonly int[] LoveGifts = { 142, 143, 144, 145, 146 };
        static readonly int[] FallGifts = { 147, 148, 149, 150, 151 };

        // 158「-----特定禮物-----」那一段：大量好感／墮落，很稀有
        static readonly int[] LoveSpecials = { 159, 160, 161, 162, 163 };
        static readonly int[] FallSpecials = { 164, 165, 166, 167, 168 };

        /// <summary>
        /// 把一整組道具各自加一筆低機率的掉落。
        /// 刻意「每一種都各給一次機會」而不是「隨機挑一種」——
        /// 後者會在建表時就固定住，整趟探索只掉得到同一樣東西。
        /// </summary>
        static void GiftPool(List<SVLootEntry> into, int[] ids, float chance)
        {
            foreach (var id in ids) into.Add(Drop(SVLootKind.Item, id, chance));
        }

        /// <summary>雜魚身上偶爾會掉的禮物。機率很低，靠數量堆出來。</summary>
        static void MobGifts(List<SVLootEntry> into, float scale = 1f)
        {
            GiftPool(into, LoveGifts, 0.004f * scale * MobItemScale);
            GiftPool(into, FallGifts, 0.003f * scale * MobItemScale);
        }

        /// <summary>雜魚身上「道具」（禮物、捲軸）的總倍率。裝備走 GearChanceScale。</summary>
        public const float MobItemScale = 0.5f;

        /// <summary>王會把整套五件都掉一輪，所以獨立成一個小工具。</summary>
        static void FullSet(List<SVLootEntry> into, SVGearSeries series, SVGearQuality quality, float chance)
        {
            into.Add(BossGear(series, SVGearSlot.Sword, quality, chance));
            // 法杖跟劍同階同機率 —— 想走魔法流的人不用被逼著去打特定的王
            into.Add(Drop(SVLootKind.Item, Staff(series, quality), chance * BossGearChanceScale));
            into.Add(BossGear(series, SVGearSlot.Chest, quality, chance));
            into.Add(BossGear(series, SVGearSlot.Legs, quality, chance));
            into.Add(BossGear(series, SVGearSlot.Helm, quality, chance));
            into.Add(BossGear(series, SVGearSlot.Boots, quality, chance));
        }

        /// <summary>捲軸 107~113，當常見的消耗品掉落。</summary>
        /// <summary>雜魚掉的捲軸。會吃 MobItemScale。</summary>
        static SVLootEntry Scroll(float chance, int min = 1, int max = 1) =>
            Drop(SVLootKind.Item, Random.Range(107, 114), chance * MobItemScale, min, max);

        /// <summary>王掉的捲軸。不打折 —— 打王本來就是為了這個。</summary>
        static SVLootEntry BossScroll(float chance, int min = 1, int max = 1) =>
            Drop(SVLootKind.Item, Random.Range(107, 114), chance, min, max);

        /// <summary>王的掉落：整套現階裝備 + 少量下一階 + 捲軸。</summary>
        static List<SVLootEntry> BossDrops(SVGearSeries series, SVGearSeries next)
        {
            var list = new List<SVLootEntry>();

            FullSet(list, series, SVGearQuality.Fine, 0.85f);   // 現階精良，幾乎必掉
            FullSet(list, series, SVGearQuality.Rare, 0.45f);   // 現階稀有
            list.Add(BossGear(series, SVGearSlot.Sword, SVGearQuality.Epic, 0.25f));
            list.Add(Drop(SVLootKind.Item, Staff(series, SVGearQuality.Epic),
                          0.25f * BossGearChanceScale));
            list.Add(BossGear(next, SVGearSlot.Chest, SVGearQuality.Crude, 0.35f));
            list.Add(BossGear(next, SVGearSlot.Sword, SVGearQuality.Crude, 0.35f));
            list.Add(BossScroll(1f, 2, 4));

            // 王身上才有的好東西：一般禮物機率不低，特定禮物很稀有
            GiftPool(list, LoveGifts, 0.16f);
            GiftPool(list, FallGifts, 0.14f);
            GiftPool(list, LoveSpecials, 0.025f);
            GiftPool(list, FallSpecials, 0.025f);

            return list;
        }

        // ================= C 級：草原與遺跡走廊 =================
        // 慢、脆、量多。給玩家熟悉操作用的
        static List<SVEnemyType> RankC() => new List<SVEnemyType>
        {
            new SVEnemyType { Name = "史萊姆", SheetName = "Monster", SheetIndex = 0,
                              Hp = 6, Speed = 1.5f, ContactDamage = 5, Xp = 1, FromStage = 0,
                              GoldChance = 0.30f, GoldMin = 2, GoldMax = 6,
                              Drops = { Gear(SVGearSeries.Novice, SVGearSlot.Sword, SVGearQuality.Crude, 0.05f) } },

            new SVEnemyType { Name = "粉史萊姆", SheetName = "$pink slime", SheetIndex = 0,
                              // 這張圖本身畫得比別隻小，照預設縮放看起來像一顆小點，所以放大一倍
                              Scale = 2f, Radius = 0.5f,
                              Hp = 9, Speed = 1.8f, ContactDamage = 6, Xp = 1, FromStage = 0,
                              GoldChance = 0.32f, GoldMin = 3, GoldMax = 7,
                              Drops = { Gear(SVGearSeries.Novice, SVGearSlot.Boots, SVGearQuality.Crude, 0.06f) } },

            new SVEnemyType { Name = "哥布林", SheetName = "$goblin", SheetIndex = 0,
                              Hp = 12, Speed = 2.4f, ContactDamage = 7, Xp = 1, FromStage = 1,
                              GoldChance = 0.38f, GoldMin = 3, GoldMax = 9,
                              Drops = { Gear(SVGearSeries.Novice, SVGearSlot.Sword, SVGearQuality.Crude, 0.07f),
                                        Gear(SVGearSeries.Novice, SVGearSlot.Helm, SVGearQuality.Crude, 0.04f) } },

            new SVEnemyType { Name = "野獸", SheetName = "Nature", SheetIndex = 0,
                              Hp = 16, Speed = 2.7f, ContactDamage = 8, Xp = 2, FromStage = 2,
                              GoldChance = 0.42f, GoldMin = 4, GoldMax = 11,
                              Drops = { Gear(SVGearSeries.Novice, SVGearSlot.Chest, SVGearQuality.Crude, 0.05f) } },

            new SVEnemyType { Name = "遺跡守衛", SheetName = "Monster", SheetIndex = 4,
                              Hp = 26, Speed = 1.7f, ContactDamage = 11, Xp = 3, FromStage = 3,
                              GoldChance = 0.5f, GoldMin = 6, GoldMax = 15,
                              Drops = { Gear(SVGearSeries.Novice, SVGearSlot.Legs, SVGearQuality.Crude, 0.08f),
                                        Gear(SVGearSeries.Novice, SVGearSlot.Chest, SVGearQuality.Fine, 0.04f),
                                        Scroll(0.06f) } },

            // ---- 王：岩甲巨獸 ----
            // 四個難度各用一張不同的怪物圖，體型都比雜魚大一圈，掉落也豐厚得多
            new SVEnemyType { Name = "岩甲巨獸", SheetName = "!$Big_Monster", SheetIndex = 0,
                              Hp = 1700, FinalBossHp = 2000,
                              Speed = 1.4f, ContactDamage = 24, Xp = 60,
                              Radius = 1.15f, Scale = 1.7f, IsBoss = true,
                              GoldChance = 1f, GoldMin = 180, GoldMax = 380,
                              Drops = BossDrops(SVGearSeries.Novice, SVGearSeries.Sandstone) },
        };

        // ================= B 級：森林與洞窟 =================
        // 開始有速度差，混編逼玩家分辨威脅
        static List<SVEnemyType> RankB() => new List<SVEnemyType>
        {
            new SVEnemyType { Name = "洞窟史萊姆", SheetName = "玻璃中的史萊姆", SheetIndex = 0,
                              Hp = 22, Speed = 1.7f, ContactDamage = 9, Xp = 2, FromStage = 0,
                              GoldChance = 0.4f, GoldMin = 5, GoldMax = 12,
                              Drops = { Gear(SVGearSeries.Sandstone, SVGearSlot.Chest, SVGearQuality.Crude, 0.07f) } },

            new SVEnemyType { Name = "獸人", SheetName = "獸族奴隸", SheetIndex = 1,
                              Hp = 34, Speed = 2.3f, ContactDamage = 13, Xp = 3, FromStage = 0,
                              GoldChance = 0.45f, GoldMin = 7, GoldMax = 16,
                              Drops = { Gear(SVGearSeries.Sandstone, SVGearSlot.Sword, SVGearQuality.Crude, 0.06f),
                                        Gear(SVGearSeries.Sandstone, SVGearSlot.Legs, SVGearQuality.Crude, 0.05f) } },

            new SVEnemyType { Name = "森林狼", SheetName = "Nature", SheetIndex = 1,
                              Hp = 26, Speed = 3.3f, ContactDamage = 12, Xp = 3, FromStage = 1,
                              GoldChance = 0.45f, GoldMin = 6, GoldMax = 15,
                              Drops = { Gear(SVGearSeries.Sandstone, SVGearSlot.Boots, SVGearQuality.Crude, 0.08f) } },

            new SVEnemyType { Name = "毒蛛", SheetName = "Monster", SheetIndex = 2,
                              Hp = 30, Speed = 2.8f, ContactDamage = 15, Xp = 4, FromStage = 2,
                              GoldChance = 0.5f, GoldMin = 8, GoldMax = 19,
                              Drops = { Gear(SVGearSeries.Sandstone, SVGearSlot.Helm, SVGearQuality.Crude, 0.08f),
                                        Gear(SVGearSeries.Sandstone, SVGearSlot.Chest, SVGearQuality.Fine, 0.04f),
                                        Scroll(0.08f) } },

            new SVEnemyType { Name = "石魔", SheetName = "Monster", SheetIndex = 5,
                              Hp = 70, Speed = 1.4f, ContactDamage = 20, Xp = 6, FromStage = 3,
                              GoldChance = 0.6f, GoldMin = 12, GoldMax = 28,
                              Drops = { Gear(SVGearSeries.Sandstone, SVGearSlot.Chest, SVGearQuality.Fine, 0.07f),
                                        Gear(SVGearSeries.Sandstone, SVGearSlot.Sword, SVGearQuality.Fine, 0.05f) } },

            // ---- 王：獸王 加爾姆 ----
            new SVEnemyType { Name = "獸王 加爾姆", SheetName = "獸族奴隸", SheetIndex = 4,
                              Hp = 4200, FinalBossHp = 6000,
                              Speed = 3.0f, ContactDamage = 34, Xp = 130,
                              Radius = 1.0f, Scale = 2.1f, IsBoss = true,
                              GoldChance = 1f, GoldMin = 450, GoldMax = 850,
                              Drops = BossDrops(SVGearSeries.Sandstone, SVGearSeries.Iron) },
        };

        // ================= A 級：魔域 =================
        // 又快又硬，開始需要靠隊友的控場
        static List<SVEnemyType> RankA() => new List<SVEnemyType>
        {
            new SVEnemyType { Name = "惡魔", SheetName = "Evil", SheetIndex = 0,
                              Hp = 70, Speed = 2.4f, ContactDamage = 20, Xp = 5, FromStage = 0,
                              GoldChance = 0.5f, GoldMin = 12, GoldMax = 26,
                              Drops = { Gear(SVGearSeries.Iron, SVGearSlot.Legs, SVGearQuality.Crude, 0.09f) } },

            new SVEnemyType { Name = "亡靈", SheetName = "灰燼", SheetIndex = 0,
                              Hp = 55, Speed = 3.0f, ContactDamage = 18, Xp = 5, FromStage = 0,
                              GoldChance = 0.5f, GoldMin = 11, GoldMax = 24,
                              Drops = { Gear(SVGearSeries.Iron, SVGearSlot.Boots, SVGearQuality.Crude, 0.07f) } },

            new SVEnemyType { Name = "拉米亞", SheetName = "拉米亚", SheetIndex = 0,
                              Hp = 95, Speed = 2.7f, ContactDamage = 24, Xp = 7, FromStage = 1,
                              GoldChance = 0.6f, GoldMin = 16, GoldMax = 36,
                              Drops = { Gear(SVGearSeries.Iron, SVGearSlot.Helm, SVGearQuality.Crude, 0.10f),
                                        Gear(SVGearSeries.Iron, SVGearSlot.Sword, SVGearQuality.Fine, 0.05f),
                                        Scroll(0.10f) } },

            new SVEnemyType { Name = "魔導兵", SheetName = "Evil", SheetIndex = 3,
                              Hp = 130, Speed = 2.0f, ContactDamage = 28, Xp = 9, FromStage = 2,
                              GoldChance = 0.65f, GoldMin = 20, GoldMax = 44,
                              Drops = { Gear(SVGearSeries.Iron, SVGearSlot.Chest, SVGearQuality.Fine, 0.07f),
                                        Gear(SVGearSeries.Iron, SVGearSlot.Sword, SVGearQuality.Rare, 0.03f) } },

            new SVEnemyType { Name = "影魔", SheetName = "灰燼", SheetIndex = 4,
                              Hp = 100, Speed = 3.6f, ContactDamage = 26, Xp = 9, FromStage = 3,
                              GoldChance = 0.65f, GoldMin = 18, GoldMax = 42,
                              Drops = { Gear(SVGearSeries.Iron, SVGearSlot.Boots, SVGearQuality.Fine, 0.08f),
                                        Gear(SVGearSeries.Iron, SVGearSlot.Legs, SVGearQuality.Rare, 0.03f) } },

            // ---- 王：拉米亞女王 ----
            new SVEnemyType { Name = "拉米亞女王", SheetName = "拉米亚", SheetIndex = 4,
                              Hp = 10000, FinalBossHp = 18000,
                              Speed = 3.0f, ContactDamage = 46, Xp = 260,
                              Radius = 1.1f, Scale = 2.2f, IsBoss = true,
                              GoldChance = 1f, GoldMin = 1100, GoldMax = 2000,
                              Drops = BossDrops(SVGearSeries.Iron, SVGearSeries.Steel) },
        };

        // ================= S 級：深淵 =================
        // 沒有雜魚，每一隻都會痛。撤退是正當選項
        static List<SVEnemyType> RankS() => new List<SVEnemyType>
        {
            new SVEnemyType { Name = "深淵獸", SheetName = "Nature", SheetIndex = 4,
                              Hp = 190, Speed = 2.9f, ContactDamage = 34, Xp = 12, FromStage = 0,
                              GoldChance = 0.6f, GoldMin = 30, GoldMax = 64,
                              Drops = { Gear(SVGearSeries.Steel, SVGearSlot.Legs, SVGearQuality.Crude, 0.12f) } },

            new SVEnemyType { Name = "高階惡魔", SheetName = "Evil", SheetIndex = 1,
                              Hp = 240, Speed = 2.6f, ContactDamage = 40, Xp = 14, FromStage = 0,
                              GoldChance = 0.65f, GoldMin = 36, GoldMax = 78,
                              Drops = { Gear(SVGearSeries.Steel, SVGearSlot.Chest, SVGearQuality.Crude, 0.10f) } },

            new SVEnemyType { Name = "亡靈騎士", SheetName = "灰燼", SheetIndex = 1,
                              Hp = 300, Speed = 2.3f, ContactDamage = 46, Xp = 16, FromStage = 1,
                              GoldChance = 0.7f, GoldMin = 44, GoldMax = 96,
                              Drops = { Gear(SVGearSeries.Steel, SVGearSlot.Sword, SVGearQuality.Fine, 0.08f),
                                        Gear(SVGearSeries.Steel, SVGearSlot.Helm, SVGearQuality.Fine, 0.07f),
                                        Scroll(0.12f) } },

            new SVEnemyType { Name = "魔將", SheetName = "Evil", SheetIndex = 5,
                              Hp = 420, Speed = 2.1f, ContactDamage = 55, Xp = 20, FromStage = 2,
                              GoldChance = 0.75f, GoldMin = 60, GoldMax = 130,
                              Drops = { Gear(SVGearSeries.Steel, SVGearSlot.Boots, SVGearQuality.Rare, 0.06f),
                                        Gear(SVGearSeries.Steel, SVGearSlot.Sword, SVGearQuality.Rare, 0.06f) } },

            new SVEnemyType { Name = "虛空影", SheetName = "灰燼", SheetIndex = 5,
                              Hp = 280, Speed = 4.2f, ContactDamage = 50, Xp = 20, FromStage = 3,
                              GoldChance = 0.75f, GoldMin = 55, GoldMax = 120,
                              Drops = { Gear(SVGearSeries.Starry, SVGearSlot.Sword, SVGearQuality.Crude, 0.04f),
                                        Gear(SVGearSeries.Steel, SVGearSlot.Chest, SVGearQuality.Rare, 0.06f) } },

            // ---- 王：深淵魔王 ----
            new SVEnemyType { Name = "深淵魔王", SheetName = "Evil", SheetIndex = 4,
                              Hp = 24000, FinalBossHp = 50000,
                              Speed = 2.6f, ContactDamage = 78, Xp = 600,
                              Radius = 1.25f, Scale = 2.6f, IsBoss = true,
                              GoldChance = 1f, GoldMin = 3000, GoldMax = 5500,
                              Drops = BossDrops(SVGearSeries.Steel, SVGearSeries.Starry) },
        };
    }
}
