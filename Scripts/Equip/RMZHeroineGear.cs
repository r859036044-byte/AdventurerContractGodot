// 女主的裝備。每位女主身上有自己的 5 格（武器／上衣／褲子／頭部／鞋子）。
//
// ---- 初始裝備（照冒險者等級）----
//   B 級 = 鍛鐵、A 級 = 鋼鐵、S 級 = 星空；品質用「精良」當標準
//   尤梨卡 B：鍛鐵精良（劍）
//   露娜   A：鋼鐵精良（法杖）
//   克萊爾 A：鋼鐵精良（劍）　※H 狀態欄寫的冒險者等級是 A
//   雪瑤雪霜 B~A：鍛鐵史詩（劍）—— 兩人共用一套
//   鈴音   S（有 SS 實力）：星空史詩（法杖）
//   凱、塔克、露娜隊的其他成員、克萊爾的護衛不能換裝，數值就是她們原本的平衡
//
// ---- 裝備怎麼影響戰鬥 ----
// 跟男主一樣：攻擊（物攻＋魔攻）→ 技能威力、防禦（物防＋魔防）→ 減傷、HP → 血量。
// 但以「初始裝備」為基準換算成倍率：穿初始裝備時倍率 = 1，數值跟現在的平衡一模一樣；
// 換更好的就照比例變強、脫掉就變弱。直接把裝備數值加上去的話，
// 尤梨卡血量會從 90 變 370、每下減傷 48，C 級的怪完全打不動她。
//
// ---- 換裝 ----
// 女主身上的裝備不在男主背包裡（Owned），換下來才放回背包；
// 從背包拿去給女主穿，就從背包移走（男主身上穿著的會先脫下來）。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public static class RMZHeroineGear
    {
        /// <summary>好感度到這個數字，H 狀態欄才開放更換裝備。</summary>
        public const int UnlockLove = 60;

        // 換算倍率時的「底」：沒有這個底的話，一點點裝備差就會變成好幾倍
        const float AtkBase = 30f, DefBase = 20f, HpBase = 150f;

        /// <summary>初始裝備（Items.json 的道具編號）：武器、上衣、褲子、頭部、鞋子。</summary>
        static readonly Dictionary<int, int[]> Defaults = new Dictionary<int, int[]>
        {
            { 1, new[] { 45, 49, 53, 57, 61 } },      // 尤梨卡：鍛鐵精良
            { 2, new[] { 194, 70, 74, 78, 82 } },     // 露娜：鋼鐵法杖精良＋鋼鐵精良
            { 3, new[] { 66, 70, 74, 78, 82 } },      // 克萊爾：鋼鐵精良
            { 4, new[] { 47, 51, 55, 59, 63 } },      // 雪瑤雪霜：鍛鐵史詩
            { 5, new[] { 200, 93, 97, 101, 105 } },   // 鈴音：星空法杖史詩＋星空史詩
        };

        static readonly Dictionary<int, RMZEquipInstance[]> Gear = new Dictionary<int, RMZEquipInstance[]>();

        // 初始裝備的合計（倍率的分母），第一次用到時算好
        static readonly Dictionary<int, (int atk, int def, int hp)> DefaultTotals = new Dictionary<int, (int, int, int)>();

        public static bool IsHeroine(int heroine) => Defaults.ContainsKey(heroine);

        public static bool Unlocked(int heroine) => IsHeroine(heroine) && RMZGifts.Love(heroine) >= UnlockLove;

        static RMZEquipInstance[] Slots(int heroine)
        {
            if (!IsHeroine(heroine)) return null;
            if (Gear.TryGetValue(heroine, out var slots)) return slots;

            slots = new RMZEquipInstance[6];
            foreach (int itemId in Defaults[heroine])
            {
                var inst = RMZEquipment.CreateLoose(itemId);
                if (inst == null) continue;
                inst.Owner = heroine;
                slots[inst.Slot] = inst;
            }
            Gear[heroine] = slots;
            return slots;
        }

        public static RMZEquipInstance WornAt(int heroine, int slot)
        {
            var slots = Slots(heroine);
            return slots != null && slot >= 1 && slot <= 5 ? slots[slot] : null;
        }

        /// <summary>把背包裡的一件拿給女主穿。原本那件回到背包。</summary>
        public static void Equip(int heroine, RMZEquipInstance inst)
        {
            var slots = Slots(heroine);
            if (slots == null || inst == null || inst.Slot < 1 || inst.Slot > 5) return;

            if (RMZEquipment.IsWorn(inst)) RMZEquipment.Unequip(inst.Slot);
            RMZEquipment.Owned.Remove(inst);

            var old = slots[inst.Slot];
            if (old != null) { old.Owner = 0; RMZEquipment.Owned.Add(old); }

            inst.Owner = heroine;
            slots[inst.Slot] = inst;
        }

        /// <summary>脫下來放回背包。</summary>
        public static void Unequip(int heroine, int slot)
        {
            var slots = Slots(heroine);
            if (slots == null || slot < 1 || slot > 5 || slots[slot] == null) return;
            var old = slots[slot];
            old.Owner = 0;
            RMZEquipment.Owned.Add(old);
            slots[slot] = null;
        }

        // ---------------- 數值 ----------------

        public static (int atk, int def, int hp) Totals(int heroine)
        {
            var slots = Slots(heroine);
            int atk = 0, def = 0, hp = 0;
            if (slots == null) return (0, 0, 0);
            for (int s = 1; s <= 5; s++)
            {
                var e = slots[s];
                if (e == null) continue;
                atk += e.Atk + e.MAtk;
                def += e.Def + e.MDef;
                hp += e.MaxHp;
            }
            return (atk, def, hp);
        }

        static (int atk, int def, int hp) Baseline(int heroine)
        {
            if (DefaultTotals.TryGetValue(heroine, out var t)) return t;
            int atk = 0, def = 0, hp = 0;
            foreach (int itemId in Defaults[heroine])
            {
                var e = RMZEquipment.CreateLoose(itemId);   // 不放進任何地方，只拿來算數字
                if (e == null) continue;
                atk += e.Atk + e.MAtk; def += e.Def + e.MDef; hp += e.MaxHp;
            }
            t = (atk, def, hp);
            DefaultTotals[heroine] = t;
            return t;
        }

        /// <summary>技能威力倍率。穿初始裝備 = 1。</summary>
        public static float PowerMul(int heroine) => Ratio(heroine, x => x.atk, AtkBase);
        /// <summary>減傷（防禦）倍率。</summary>
        public static float DefenseMul(int heroine) => Ratio(heroine, x => x.def, DefBase);
        /// <summary>血量倍率。</summary>
        public static float HpMul(int heroine) => Ratio(heroine, x => x.hp, HpBase);

        static float Ratio(int heroine, System.Func<(int atk, int def, int hp), int> pick, float floor)
        {
            if (!IsHeroine(heroine)) return 1f;
            float now = pick(Totals(heroine)), start = pick(Baseline(heroine));
            return (floor + now) / (floor + start);
        }

        /// <summary>冒險隊友的名字對應到哪位女主（0 = 不是能換裝的女主）。</summary>
        public static int HeroineOfCompanion(string name) => name switch
        {
            "尤梨卡" => 1,
            "露娜" => 2,
            "克萊爾" => 3,
            "雪瑤" or "雪霜" => 4,
            "鈴音" => 5,
            _ => 0,
        };

        /// <summary>出發前把女主的裝備倍率乘到隊友資料上（每趟都是新的一份資料，不會累積）。</summary>
        public static void ApplyTo(List<RMZS.SVCompanionType> companions)
        {
            if (companions == null) return;
            foreach (var c in companions)
            {
                if (c == null) continue;
                int h = HeroineOfCompanion(c.Name);
                if (h == 0) continue;
                float p = PowerMul(h), d = DefenseMul(h), hp = HpMul(h);
                if (Mathf.IsEqualApprox(p, 1f) && Mathf.IsEqualApprox(d, 1f) && Mathf.IsEqualApprox(hp, 1f)) continue;
                c.Power = Mathf.Max(1, Mathf.RoundToInt(c.Power * p));
                if (c.BladePower > 0) c.BladePower = Mathf.Max(1, Mathf.RoundToInt(c.BladePower * p));
                if (c.PlayerShieldPower > 0) c.PlayerShieldPower = Mathf.Max(1, Mathf.RoundToInt(c.PlayerShieldPower * p));
                c.Defense = Mathf.Max(0, Mathf.RoundToInt(c.Defense * d));
                c.Hp = Mathf.Max(1, Mathf.RoundToInt(c.Hp * hp));
            }
        }

        // ---------------- 存讀檔 ----------------

        public static void Clear()
        {
            Gear.Clear();
        }

        public static void Capture(List<RMZEquipInstance> into)
        {
            into.Clear();
            foreach (var kv in Gear)
            {
                bool any = false;
                for (int s = 1; s <= 5; s++)
                    if (kv.Value[s] != null) { kv.Value[s].Owner = kv.Key; into.Add(kv.Value[s]); any = true; }
                // 全部脫光也要記下來，不然讀檔時會被當成「沒存過」又送一套初始裝備
                if (!any) into.Add(new RMZEquipInstance { Owner = kv.Key, Slot = 0 });
            }
        }

        public static void Restore(List<RMZEquipInstance> saved)
        {
            Gear.Clear();
            if (saved == null || saved.Count == 0) return;   // 舊存檔：第一次用到時給初始裝備
            foreach (var e in saved)
            {
                if (e == null || !IsHeroine(e.Owner) || e.Slot < 0 || e.Slot > 5) continue;
                if (!Gear.TryGetValue(e.Owner, out var slots)) { slots = new RMZEquipInstance[6]; Gear[e.Owner] = slots; }
                if (e.Slot == 0) continue;   // 「全部脫光」的記號
                slots[e.Slot] = e;
                RMZEquipment.ReserveUid(e.Uid);
            }
        }
    }
}
