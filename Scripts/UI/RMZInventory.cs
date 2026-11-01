// 背包的資料層：把 RMZParty 手上的東西，照 RM 備註欄的分類標籤分成四頁。
//
// 分類不是我自己編的 —— 專案的 Items.json 備註欄本來就標好了：
//   <BagEquip>   裝備（100 件，多半還帶 <BindWeapon: n> / <BindArmor: n>）
//   <BagItem1>   強化捲軸
//   <BagItem2>   一般消耗品        <BagItem3>  酒類與投擲物
//   <BagItem4>   禮物              <BagItem5>  高級禮物
//   <BagItem6>   隨機寶箱          <BagItem7>  提示類
// 這裡照使用者要的四頁重新歸併，之後在 RM 加新道具只要標對標籤就會自動歸位。
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RMZ
{
    public enum RMZBagTab
    {
        Equip = 0,   // 裝備
        Scroll = 1,  // 捲軸
        Item = 2,    // 道具
        Other = 3,   // 其他
    }

    /// <summary>背包裡的一格。</summary>
    public struct RMZBagEntry
    {
        public RMZItemKind Kind;
        public int Id;
        public int Count;
        public EquipItemData Data;

        public string Name => Data != null ? RMZLocalization.Translate(Data.name) : $"#{Id}";
        public int IconIndex => Data?.iconIndex ?? 0;
    }

    public enum RMZItemKind { Item, Weapon, Armor }

    public static class RMZInventory
    {
        public static readonly string[] TabNames = { "裝備", "捲軸", "道具", "其他" };

        static readonly Regex NoteTag = new Regex(@"<\s*([A-Za-z0-9_]+)\s*(?::([^>]*))?>", RegexOptions.Compiled);

        // note 每次都重新剖析太浪費，剖過就記起來
        static readonly Dictionary<int, RMZBagTab> ItemTabCache = new Dictionary<int, RMZBagTab>();

        /// <summary>這件道具屬於哪一頁。</summary>
        public static RMZBagTab TabOf(EquipItemData item)
        {
            if (item == null) return RMZBagTab.Other;
            if (ItemTabCache.TryGetValue(item.id, out var cached)) return cached;

            var tab = RMZBagTab.Other;
            foreach (Match m in NoteTag.Matches(item.note ?? ""))
            {
                switch (m.Groups[1].Value)
                {
                    case "BagEquip": tab = RMZBagTab.Equip; break;
                    case "BagItem1": tab = RMZBagTab.Scroll; break;
                    case "BagItem2":
                    case "BagItem3": tab = RMZBagTab.Item; break;
                    default: continue;
                }
                break;
            }

            ItemTabCache[item.id] = tab;
            return tab;
        }

        /// <summary>備註欄的標籤值，例如 &lt;BindWeapon: 12&gt; 取 12。沒有回傳 0。</summary>
        public static int NoteInt(EquipItemData item, string tag)
        {
            if (item == null || string.IsNullOrEmpty(item.note)) return 0;

            foreach (Match m in NoteTag.Matches(item.note))
            {
                if (m.Groups[1].Value != tag) continue;
                return int.TryParse(m.Groups[2].Value.Trim(), out var v) ? v : 0;
            }
            return 0;
        }

        public static bool HasNoteTag(EquipItemData item, string tag)
        {
            if (item == null || string.IsNullOrEmpty(item.note)) return false;
            foreach (Match m in NoteTag.Matches(item.note)) if (m.Groups[1].Value == tag) return true;
            return false;
        }

        /// <summary>某一頁現在有什麼。回傳的順序照 ID，這樣背包看起來才穩定。</summary>
        public static List<RMZBagEntry> Contents(RMZBagTab tab)
        {
            var list = new List<RMZBagEntry>();

            foreach (var kv in RMZParty.AllItems)
            {
                if (kv.Value <= 0) continue;
                var data = RMZDatabase.Item(kv.Key);
                if (data == null || TabOf(data) != tab) continue;
                list.Add(new RMZBagEntry { Kind = RMZItemKind.Item, Id = kv.Key, Count = kv.Value, Data = data });
            }

            // 直接持有的武器防具一律放裝備頁。
            // 多數裝備是以「道具」形式進背包（<BagEquip> + <BindWeapon>），
            // 但事件也會直接給武器防具，那些不能漏掉。
            if (tab == RMZBagTab.Equip)
            {
                foreach (var kv in RMZParty.AllWeapons)
                {
                    if (kv.Value <= 0) continue;
                    var data = RMZDatabase.Weapon(kv.Key);
                    if (data == null) continue;
                    list.Add(new RMZBagEntry { Kind = RMZItemKind.Weapon, Id = kv.Key, Count = kv.Value, Data = data });
                }
                foreach (var kv in RMZParty.AllArmors)
                {
                    if (kv.Value <= 0) continue;
                    var data = RMZDatabase.Armor(kv.Key);
                    if (data == null) continue;
                    list.Add(new RMZBagEntry { Kind = RMZItemKind.Armor, Id = kv.Key, Count = kv.Value, Data = data });
                }
            }

            list.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : a.Id.CompareTo(b.Id));
            return list;
        }

        public static int CountIn(RMZBagTab tab) => Contents(tab).Count;

        /// <summary>滑鼠停在格子上時顯示的說明。</summary>
        public static string Describe(RMZBagEntry entry)
        {
            if (entry.Data == null) return "";

            var lines = new List<string> { entry.Name };

            string desc = RMZLocalization.Translate(entry.Data.description ?? "");
            if (!string.IsNullOrWhiteSpace(desc)) lines.Add(desc);

            if (entry.Kind != RMZItemKind.Item || entry.Data.etypeId > 0)
            {
                int atk = ParamAt(entry.Data, 2);
                int def = ParamAt(entry.Data, 3);
                if (atk != 0) lines.Add($"攻擊 {atk}");
                if (def != 0) lines.Add($"防禦 {def}");
            }

            // 裝備型道具會綁一件真正的武器或防具，把它的數值也帶出來
            int bindWeapon = NoteInt(entry.Data, "BindWeapon");
            int bindArmor = NoteInt(entry.Data, "BindArmor");
            var bound = bindWeapon > 0 ? RMZDatabase.Weapon(bindWeapon)
                      : bindArmor > 0 ? RMZDatabase.Armor(bindArmor) : null;
            if (bound != null)
            {
                int atk = ParamAt(bound, 2);
                int def = ParamAt(bound, 3);
                if (atk != 0) lines.Add($"攻擊 {atk}");
                if (def != 0) lines.Add($"防禦 {def}");
            }

            if (entry.Data.price > 0) lines.Add($"售價 {entry.Data.price}");
            return string.Join("\n", lines);
        }

        static int ParamAt(EquipItemData data, int index) =>
            data?.Params != null && index < data.Params.Length ? data.Params[index] : 0;

        public static void ClearCache() => ItemTabCache.Clear();
    }
}
