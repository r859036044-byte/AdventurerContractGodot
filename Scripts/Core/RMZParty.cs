// 隊伍的持有物與金錢，對應 MZ 的 Game_Party。
// 目前只做事件條件判定會用到的部分，戰鬥相關等做戰鬥系統時再補。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public static class RMZParty
    {
        public static int Gold { get; private set; }
        public const int MaxGold = 99999999;

        static readonly Dictionary<int, int> Items = new Dictionary<int, int>();
        static readonly Dictionary<int, int> Weapons = new Dictionary<int, int>();
        static readonly Dictionary<int, int> Armors = new Dictionary<int, int>();

        // 金錢一變就同步到變數 8「玩家金錢」—— 左上角狀態欄顯示的是 v[8]，
        // 原本只有 CE89 打開 UI 那一下才抄一次，之後買賣、拿報酬都不會更新。
        public static void GainGold(int amount) { Gold = Mathf.Clamp(Gold + amount, 0, MaxGold); SyncGoldVariable(); }

        /// <summary>直接指定金錢（事件裡的 $gameParty._gold = n 走這裡）。</summary>
        public static void SetGold(int amount) { Gold = Mathf.Clamp(amount, 0, MaxGold); SyncGoldVariable(); }

        const int GoldVariable = 8;

        /// <summary>初心者 5 件套：樺木長劍、胸甲、護腿、頭盔、護靴（粗製）。</summary>
        public static readonly int[] StarterSet = { 2, 6, 10, 14, 18 };
        static void SyncGoldVariable() => RMZGameState.SetVariable(GoldVariable, Gold);

        /// <summary>清空道具／武器／防具。</summary>
        public static void ClearBags()
        {
            Items.Clear();
            Weapons.Clear();
            Armors.Clear();
        }

        public static int NumItems(int id) => Items.TryGetValue(id, out var n) ? n : 0;
        public static int NumWeapons(int id) => Weapons.TryGetValue(id, out var n) ? n : 0;
        public static int NumArmors(int id) => Armors.TryGetValue(id, out var n) ? n : 0;

        public static bool HasItem(int id) => NumItems(id) > 0;
        public static bool HasWeapon(int id) => NumWeapons(id) > 0;
        public static bool HasArmor(int id) => NumArmors(id) > 0;

        /// <summary>
        /// 拿到道具。標了 &lt;BagEquip&gt; 的不進這個袋子 ——
        /// 它們是裝備，改成在 RMZEquipment 生一件獨立的實例，
        /// 這樣同一件裝備才拿得到第二把（RM 的 &lt;IndependentItem&gt; 在這個專案有問題）。
        /// </summary>
        public static void GainItem(int id, int amount)
        {
            if (amount > 0 && RMZEquipment.IsEquipItem(id))
            {
                var inst = RMZEquipment.Gain(id, amount);

                // 初心者 5 件套（樺木・粗製：長劍／胸甲／護腿／頭盔／護靴）拿到就自動穿上。
                // 只在那個欄位還空著的時候穿 —— 之後 C 級雜魚也會掉這幾件，不能把身上更好的換掉。
                if (inst != null && System.Array.IndexOf(StarterSet, id) >= 0 && RMZEquipment.WornAt(inst.Slot) == null)
                    RMZEquipment.Equip(inst);
                return;
            }

            Add(Items, id, amount);
        }
        public static void GainWeapon(int id, int amount) => Add(Weapons, id, amount);
        public static void GainArmor(int id, int amount) => Add(Armors, id, amount);

        static void Add(Dictionary<int, int> bag, int id, int amount)
        {
            if (id <= 0) return;
            int n = Mathf.Clamp((bag.TryGetValue(id, out var cur) ? cur : 0) + amount, 0, 99);
            if (n <= 0) bag.Remove(id); else bag[id] = n;
        }

        public static void Clear()
        {
            Gold = 0;
            Items.Clear();
            Weapons.Clear();
            Armors.Clear();
            RMZEquipment.Clear();
        }

        // ---------------- 背包瀏覽 ----------------

        public static IEnumerable<KeyValuePair<int, int>> AllItems => Items;
        public static IEnumerable<KeyValuePair<int, int>> AllWeapons => Weapons;
        public static IEnumerable<KeyValuePair<int, int>> AllArmors => Armors;

        // ---------------- 存讀檔 ----------------

        static void Capture(Dictionary<int, int> bag, List<int> ids, List<int> counts)
        {
            ids.Clear(); counts.Clear();
            foreach (var kv in bag) { ids.Add(kv.Key); counts.Add(kv.Value); }
        }

        static void Restore(Dictionary<int, int> bag, List<int> ids, List<int> counts)
        {
            bag.Clear();
            if (ids == null || counts == null) return;
            for (int i = 0; i < ids.Count && i < counts.Count; i++)
                if (counts[i] > 0) bag[ids[i]] = counts[i];
        }

        public static void CaptureItems(List<int> ids, List<int> counts) => Capture(Items, ids, counts);
        public static void CaptureWeapons(List<int> ids, List<int> counts) => Capture(Weapons, ids, counts);
        public static void CaptureArmors(List<int> ids, List<int> counts) => Capture(Armors, ids, counts);

        public static void RestoreItems(List<int> ids, List<int> counts) => Restore(Items, ids, counts);
        public static void RestoreWeapons(List<int> ids, List<int> counts) => Restore(Weapons, ids, counts);
        public static void RestoreArmors(List<int> ids, List<int> counts) => Restore(Armors, ids, counts);
    }
}
