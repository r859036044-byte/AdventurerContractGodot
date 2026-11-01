// 戰利品：金幣、道具、裝備。
//
// 敵人死掉會掉在地上，玩家撿起來累積在「戰利品袋」裡。
// 一局結束時依結果決定帶回公會多少：
//   自己組隊／僱傭：完成保留全部，撤退保留八成。
//   加入他人隊伍：完成分得三成，撤退分得兩成；結算只套用一次比例。
//
// 這裡只負責「掉什麼、撿了多少」，實際寫回 RM 的道具欄位由 RMZSurvivorsBridge 處理。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public enum SVLootKind
    {
        Gold,
        Item,
        Weapon,
        Armor,
    }

    /// <summary>掉落表的一筆。</summary>
    [System.Serializable]
    public class SVLootEntry
    {
        [Tooltip("掉什麼")]
        public SVLootKind Kind = SVLootKind.Gold;

        [Tooltip("RM 的道具／武器／防具 ID。金幣不用填")]
        public int ItemId;

        [Tooltip("顯示用的名字。留空會去 RM 資料庫查")]
        public string Name = "";

        [Tooltip("掉落機率 0~1")]
        [Range(0f, 1f)] public float Chance = 0.2f;

        [Tooltip("數量下限（金幣就是金額）")]
        public int Min = 1;

        [Tooltip("數量上限")]
        public int Max = 1;

        public int Roll() => Random.Range(Min, Mathf.Max(Min, Max) + 1);
    }

    /// <summary>一局累積下來的戰利品。</summary>
    public class SVLootBag
    {
        public int Gold;
        public readonly Dictionary<int, int> Items = new Dictionary<int, int>();
        public readonly Dictionary<int, int> Weapons = new Dictionary<int, int>();
        public readonly Dictionary<int, int> Armors = new Dictionary<int, int>();

        public void Clear()
        {
            Gold = 0;
            Items.Clear();
            Weapons.Clear();
            Armors.Clear();
        }

        public void Add(SVLootKind kind, int id, int count)
        {
            if (count <= 0) return;

            switch (kind)
            {
                case SVLootKind.Gold: Gold += count; break;
                case SVLootKind.Item: Bump(Items, id, count); break;
                case SVLootKind.Weapon: Bump(Weapons, id, count); break;
                case SVLootKind.Armor: Bump(Armors, id, count); break;
            }
        }

        static void Bump(Dictionary<int, int> bag, int id, int count)
        {
            if (id <= 0) return;
            bag[id] = (bag.TryGetValue(id, out var n) ? n : 0) + count;
        }

        public int TotalPieces
        {
            get
            {
                int n = 0;
                foreach (var v in Items.Values) n += v;
                foreach (var v in Weapons.Values) n += v;
                foreach (var v in Armors.Values) n += v;
                return n;
            }
        }

        /// <summary>
        /// 依比例縮水後的新袋子。
        /// 數量用無條件捨去，但只要原本有拿到、比例又不是 0，至少留 1 個 ——
        /// 「辛苦打到的稀有裝備因為只有一件就整個消失」對玩家太傷。
        /// </summary>
        public SVLootBag Scaled(float ratio)
        {
            if (ratio >= 0.999f) return this;

            var bag = new SVLootBag { Gold = Mathf.FloorToInt(Gold * ratio) };
            ScaleInto(Items, bag.Items, ratio);
            ScaleInto(Weapons, bag.Weapons, ratio);
            ScaleInto(Armors, bag.Armors, ratio);
            return bag;
        }

        static void ScaleInto(Dictionary<int, int> from, Dictionary<int, int> to, float ratio)
        {
            if (ratio <= 0f) return;

            foreach (var pair in from)
            {
                int n = Mathf.FloorToInt(pair.Value * ratio);
                if (n <= 0) n = 1;
                to[pair.Key] = n;
            }
        }

        /// <summary>結算畫面用的條列。</summary>
        public List<string> Describe()
        {
            var lines = new List<string>();
            if (Gold > 0) lines.Add($"金幣 {Gold} G");

            foreach (var pair in Items) lines.Add($"{SVLootNames.Item(pair.Key)} ×{pair.Value}");
            foreach (var pair in Weapons) lines.Add($"{SVLootNames.Weapon(pair.Key)} ×{pair.Value}");
            foreach (var pair in Armors) lines.Add($"{SVLootNames.Armor(pair.Key)} ×{pair.Value}");

            if (lines.Count == 0) lines.Add("（什麼都沒撿到）");
            return lines;
        }
    }

    /// <summary>
    /// 查 RM 資料庫拿名字。小遊戲不直接依賴本篇的類別，
    /// 所以用一個可以被外面塞進來的委派，單獨跑小遊戲時就顯示 id。
    /// </summary>
    public static class SVLootNames
    {
        public static System.Func<int, string> ItemName;
        public static System.Func<int, string> WeaponName;
        public static System.Func<int, string> ArmorName;

        public static string Item(int id) => ItemName?.Invoke(id) ?? $"道具{id}";
        public static string Weapon(int id) => WeaponName?.Invoke(id) ?? $"武器{id}";
        public static string Armor(int id) => ArmorName?.Invoke(id) ?? $"防具{id}";
    }
}
