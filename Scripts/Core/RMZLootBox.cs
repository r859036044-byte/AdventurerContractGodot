// 隨機寶箱的開獎邏輯（原作是公共事件 261~270 裡的 JS，由 CE272「開寶箱事件」依寶箱道具分派）。
//
// 這裡直接讀那幾段 JS 的設定（各層的道具清單、機率、開關條件、重複換錢金額），
// 所以之後在 RM 那邊改機率或道具，Godot 這邊不用跟著改。
//   ・一般寶箱（boxTiers）：抽 1 個，doubleChance% 機率再抽 1 個不重複的
//   ・裝備寶箱（equipTiers）：抽 1 件，已經有同一件就自動換成金錢
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace RMZ;

public static class RMZLootBox
{
    public sealed class Drop
    {
        public int ItemId;
        public int Tier;        // 第幾層（0 = 最普通）
        public int TierCount;   // 這個寶箱一共幾層，給 UI 換算稀有度
        public int Gold;        // >0 表示已擁有、換成了金錢
    }

    sealed class Tier
    {
        public int Index, Chance, Switch, Sell;
        public int[] Items = Array.Empty<int>();
    }

    sealed class Pool
    {
        public bool Equip;
        public int DoubleChance;
        public List<Tier> Tiers = new();
    }

    static readonly Dictionary<int, Pool> Cache = new();
    static readonly Random Rng = new();

    /// <summary>這個道具是不是能開的寶箱。</summary>
    public static bool IsChest(int itemId) => GetPool(itemId) != null;

    /// <summary>開一個寶箱：直接把東西／金錢給玩家，回傳開到什麼。</summary>
    public static List<Drop> Open(int chestId)
    {
        var result = new List<Drop>();
        var pool = GetPool(chestId);
        if (pool == null) return result;

        if (pool.Equip)
        {
            var (id, tier) = DrawOne(pool, -1);
            if (id <= 0) return result;
            var drop = new Drop { ItemId = id, Tier = tier?.Index ?? 0, TierCount = pool.Tiers.Count };
            if (OwnsEquip(id)) { drop.Gold = tier?.Sell > 0 ? tier.Sell : 50; RMZParty.GainGold(drop.Gold); }
            else RMZParty.GainItem(id, 1);
            result.Add(drop);
            return result;
        }

        int count = Rng.Next(100) < pool.DoubleChance ? 2 : 1;
        int first = -1;
        for (int i = 0; i < count; i++)
        {
            var (id, tier) = DrawOne(pool, first);
            if (id <= 0) break;
            first = id;
            RMZParty.GainItem(id, 1);
            result.Add(new Drop { ItemId = id, Tier = tier?.Index ?? 0, TierCount = pool.Tiers.Count });
        }
        return result;
    }

    /// <summary>寶箱裡最好那一層的第一個道具（商店卡片上當徽章圖案用）。</summary>
    public static int SampleItem(int chestId)
    {
        var pool = GetPool(chestId);
        if (pool == null) return 0;
        for (int i = pool.Tiers.Count - 1; i >= 0; i--)
            foreach (var id in pool.Tiers[i].Items) if (RMZDatabase.Item(id) != null) return id;
        return 0;
    }

    static bool OwnsEquip(int itemId)
    {
        foreach (var e in RMZEquipment.Owned) if (e != null && e.ItemId == itemId) return true;
        return false;
    }

    static (int id, Tier tier) DrawOne(Pool pool, int exclude)
    {
        var valid = new List<Tier>();
        int total = 0;
        foreach (var t in pool.Tiers)
        {
            if (t.Items.Length == 0 || t.Chance <= 0) continue;
            if (t.Switch > 0 && !RMZGameState.Switch(t.Switch)) continue;
            valid.Add(t); total += t.Chance;
        }
        if (valid.Count == 0) return (0, null);

        for (int attempt = 0; attempt < 30; attempt++)
        {
            int roll = Rng.Next(total), acc = 0;
            Tier pick = valid[^1];
            foreach (var t in valid) { acc += t.Chance; if (roll < acc) { pick = t; break; } }
            int id = pick.Items[Rng.Next(pick.Items.Length)];
            if (id != exclude && RMZDatabase.Item(id) != null) return (id, pick);
        }
        return (0, null);
    }

    // ---------------- 讀 JS 設定 ----------------

    static Pool GetPool(int chestId)
    {
        if (Cache.TryGetValue(chestId, out var cached)) return cached;
        Pool pool = null;
        int ceId = CommonEventFor(chestId);
        if (ceId > 0) pool = Parse(ScriptOf(ceId));
        Cache[chestId] = pool;
        return pool;
    }

    /// <summary>CE272「開寶箱事件」裡：如果持有某寶箱 → 呼叫哪個公共事件。</summary>
    static int CommonEventFor(int chestId)
    {
        var ce = RMZDatabase.CommonEvent(272);
        if (ce?.list == null) return 0;
        bool inBranch = false;
        foreach (var c in ce.list)
        {
            var p = c.parameters;
            if (c.code == 111 && p != null && p.Count >= 2)
                inBranch = Convert.ToInt32(p[0]) == 8 && Convert.ToInt32(p[1]) == chestId;
            else if (inBranch && c.code == 117 && p != null && p.Count >= 1)
                return Convert.ToInt32(p[0]);
        }
        return 0;
    }

    static string ScriptOf(int ceId)
    {
        var ce = RMZDatabase.CommonEvent(ceId);
        if (ce?.list == null) return "";
        var sb = new StringBuilder();
        foreach (var c in ce.list)
            if ((c.code == 355 || c.code == 655) && c.parameters?.Count > 0) sb.Append(c.parameters[0]).Append('\n');
        return sb.ToString();
    }

    static Pool Parse(string js)
    {
        if (string.IsNullOrEmpty(js) || (!js.Contains("boxTiers") && !js.Contains("equipTiers"))) return null;

        var arrays = new Dictionary<string, int[]>();
        foreach (Match m in Regex.Matches(js, @"const\s+(\w+)\s*=\s*\[([^\]]*)\]"))
        {
            var ids = new List<int>();
            foreach (var part in m.Groups[2].Value.Split(','))
                if (int.TryParse(part.Trim(), out int id)) ids.Add(id);
            arrays[m.Groups[1].Value] = ids.ToArray();
        }
        var ints = new Dictionary<string, int>();
        foreach (Match m in Regex.Matches(js, @"const\s+(\w+)\s*=\s*(\d+)\s*;"))
            ints[m.Groups[1].Value] = int.Parse(m.Groups[2].Value);

        var pool = new Pool { Equip = js.Contains("equipTiers"), DoubleChance = ints.TryGetValue("doubleChance", out var dc) ? dc : 0 };
        foreach (var line in js.Split('\n'))
        {
            var m = Regex.Match(line, @"\{\s*chance:\s*(\d+)\s*,\s*items:\s*(\w+)(?:\s*,\s*sell:\s*(\w+))?\s*\}");
            if (!m.Success || line.TrimStart().StartsWith("//")) continue;
            var sw = Regex.Match(line, @"\$gameSwitches\.value\((\d+)\)");
            var t = new Tier
            {
                Index = pool.Tiers.Count,
                Chance = int.Parse(m.Groups[1].Value),
                Items = arrays.TryGetValue(m.Groups[2].Value, out var arr) ? arr : Array.Empty<int>(),
                Switch = sw.Success ? int.Parse(sw.Groups[1].Value) : 0,
            };
            if (m.Groups[3].Success) t.Sell = ints.TryGetValue(m.Groups[3].Value, out var s) ? s : int.TryParse(m.Groups[3].Value, out var n) ? n : 0;
            pool.Tiers.Add(t);
        }
        return pool.Tiers.Count > 0 ? pool : null;
    }
}
