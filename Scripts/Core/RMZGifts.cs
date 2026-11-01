// 送禮的對應表。道具編號與「送給誰、加什麼」直接照 Items.json 的說明欄：
//
//   142~146  一般好感禮物（尤梨卡／露娜／克萊爾／鈴音／雪瑤雪霜）
//   147~151  一般墮落禮物（同上順序）
//   159~163  特定好感禮物（大量、必定成功）
//   164~168  特定墮落禮物（大量、必定成功）
//   127~130  酒類。誰都能送，偏向加墮落，酒精濃度越高加越多
//
// 好感／墮落寫回 RM 原本的變數：好感 82~86、墮落 87~91，
// 上限吃 633~637「上限」那組變數（沒設就當 100）。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZ
{
    public struct RMZGift
    {
        public int ItemId;
        public string Name;

        /// <summary>加多少是隨機的，這是範圍的兩端（含）。</summary>
        public int Min;
        public int Max;

        public bool IsCorruption;
        /// <summary>特定禮物：必定成功，不吃機率。</summary>
        public bool Guaranteed;

        /// <summary>擲一次。每次送禮都重擲，同一樣東西不會永遠加一樣多。</summary>
        public int Roll() => Random.Range(Min, Max + 1);

        /// <summary>給 UI 顯示用的大概值。</summary>
        public int Mid => (Min + Max) / 2;
    }

    public static class RMZGifts
    {
        /// <summary>一般禮物的成功率。失敗只加三分之一，不會完全白送。</summary>
        public const float SuccessChance = 0.75f;

        /// <summary>沒過關時心情不好，送禮有機率被婉拒（不會扣好感，這次單純不加）。</summary>
        public const float RejectChanceAfterFailure = 0.35f;

        // RM 變數
        const int LoveBase = 82 - 1;   // 82 露娜 83 狼姐妹 84 尤梨卡 85 克萊爾 86 鈴音
        const int FallBase = 87 - 1;
        const int CapBase = 633 - 1;   // 633 露娜 …… 637 鈴音

        /// <summary>
        /// 隊伍編號（變數 59）→ 好感變數的偏移。
        /// 兩邊順序不一樣，這裡是唯一要注意的地方：
        ///   隊伍 1 尤梨卡 → 變數 84　　隊伍 2 露娜 → 82
        ///   隊伍 3 克萊爾 → 85　　　　隊伍 4 雪瑤雪霜 → 83
        ///   隊伍 5 鈴音 → 86
        /// </summary>
        static readonly int[] VarOffset = { 0, 3, 1, 4, 2, 5 };

        static int LoveVar(int heroine) => LoveBase + VarOffset[Mathf.Clamp(heroine, 1, 5)];
        static int FallVar(int heroine) => FallBase + VarOffset[Mathf.Clamp(heroine, 1, 5)];
        static int CapVar(int heroine) => CapBase + VarOffset[Mathf.Clamp(heroine, 1, 5)];

        static int Cap(int heroine)
        {
            int cap = RMZGameState.Variable(CapVar(heroine));
            return cap > 0 ? cap : 100;
        }

        public static int Love(int heroine) => RMZGameState.Variable(LoveVar(heroine));

        /// <summary>好感已經頂到目前的上限（鎖定中）。要送禮突破，或鎖定後隔天在 RM 的隨機事件裡突破。</summary>
        public static bool IsLocked(int heroine) => Love(heroine) >= Cap(heroine);
        public static int Fall(int heroine) => RMZGameState.Variable(FallVar(heroine));

        public static void AddLove(int heroine, int amount)
        {
            int v = Mathf.Clamp(Love(heroine) + amount, 0, Cap(heroine));
            RMZGameState.SetVariable(LoveVar(heroine), v);
        }

        public static void AddFall(int heroine, int amount)
        {
            int v = Mathf.Clamp(Fall(heroine) + amount, 0, Cap(heroine));
            RMZGameState.SetVariable(FallVar(heroine), v);
        }

        // ---------------- 對應表 ----------------

        // 索引就是隊伍編號 1~5（0 補位）
        static readonly int[] LoveGift = { 0, 142, 143, 144, 146, 145 };
        static readonly int[] FallGift = { 0, 147, 148, 149, 151, 150 };
        static readonly int[] LoveSpecial = { 0, 159, 160, 161, 163, 162 };
        static readonly int[] FallSpecial = { 0, 164, 165, 166, 168, 167 };

        /// <summary>酒類：誰都能送。度數越高加越多墮落。</summary>
        static readonly (int id, int amount)[] Drinks =
        {
            (127, 2),   // 微醺果酒
            (128, 3),   // 濃醇紅酒
            (129, 4),   // 薄荷白酒
            (130, 6),   // 深水炸彈
        };

        /// <summary>這位女主現在身上帶得出來的禮物。沒有的道具不會列出來。</summary>
        public static List<RMZGift> OwnedFor(int heroine)
        {
            var list = new List<RMZGift>();
            if (heroine < 1 || heroine > 5) return list;

            // 特定禮物在 15 上下浮動，一般禮物在 5 上下
            Add(list, LoveSpecial[heroine], 11, 19, false, true);
            Add(list, FallSpecial[heroine], 11, 19, true, true);
            Add(list, LoveGift[heroine], 3, 8, false, false);
            Add(list, FallGift[heroine], 3, 8, true, false);

            // 酒：以各自的基準值上下各浮動 1
            foreach (var (id, amount) in Drinks)
                Add(list, id, Mathf.Max(1, amount - 1), amount + 1, true, false);

            return list;
        }

        static void Add(List<RMZGift> list, int itemId, int min, int max,
                        bool corruption, bool guaranteed)
        {
            if (itemId <= 0 || RMZParty.NumItems(itemId) <= 0) return;

            var data = RMZDatabase.Item(itemId);
            list.Add(new RMZGift
            {
                ItemId = itemId,
                Name = data != null ? RMZLocalization.Translate(data.name) : $"#{itemId}",
                Min = Mathf.Max(1, min),
                Max = Mathf.Max(min, max),
                IsCorruption = corruption,
                Guaranteed = guaranteed,
            });
        }
    }
}
