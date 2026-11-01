// 從本篇（RMZ）進小遊戲時帶進來的「委託書」，以及打完之後帶回去的結果。
//
// 這一支刻意只放純資料，而且放在 RMZS 這邊 —— 方向是「本篇填、小遊戲讀」，
// 單向依賴。小遊戲自己單獨按 Play 也能跑，那時 Pending 是 null，走沙盒預設值。
using System;
using System.Collections.Generic;

namespace RMZS
{
    /// <summary>玩家這趟冒險是用什麼身分出門的。對應 RM 的變數 42/43/44。</summary>
    public enum SVPartyMode
    {
        /// <summary>獨自組隊：隊伍是自己的，指揮權在玩家手上。</summary>
        Solo = 0,
        /// <summary>加入別人的隊伍：人家不聽你的，玩家只能顧好自己。</summary>
        JoinOther = 1,
        /// <summary>花錢僱傭：錢付了，當然指揮得動。</summary>
        Hire = 2,
    }

    public class SVBriefing
    {
        public bool Tutorial;
        public SVPartyMode Mode = SVPartyMode.Solo;

        /// <summary>冒險地點：1=C 2=B 3=A 4=S。對應 RM 變數 117。</summary>
        public int MapRank = 1;

        /// <summary>玩家戰力（RM 變數 9），用來換算血量與傷害。</summary>
        public int PlayerPower = 100;

        // ---- 裝備加成。本篇的 RMZEquipment 算好之後填進來 ----
        public int EquipAtk;    // 物攻
        public int EquipMAtk;   // 魔攻
        public int EquipDef;    // 物防
        public int EquipMDef;   // 魔防
        public int EquipHp;

        /// <summary>手上拿的是法杖（普攻走魔法），不是劍。</summary>
        public bool UsingStaff;

        /// <summary>前一晚沒去酒館（變數 678）：傷害、血量、道具掉落 +10%。</summary>
        public bool Rested;

        /// <summary>這趟帶了誰。null 或空的話用 SVGame 的預設五人（獨自組隊例外：空的就是男主一個人）。</summary>
        public List<SVCompanionType> Companions;

        /// <summary>獨自組隊勾選的女主（RM 變數 98～102）。多人羈絆（SVBond）靠這個判斷。</summary>
        public List<SVHeroine> Team = new List<SVHeroine>();

        /// <summary>要活過幾秒才算完成委託。</summary>
        public float SurviveSeconds = 120f;

        /// <summary>這趟跟誰出門（RM 變數 59「選擇的隊伍」）。0 = 獨自。打完王的對話靠它挑台詞。</summary>
        public int Heroine;

        // ---- 技能書（本篇開關 22／23）----
        // 這兩個大覺不是打一打就會出現的，要先在公會跟鈴音的父親學過。
        // 沒學過就完全不進三選一，而且王身上才掉得出對應的技能書。

        /// <summary>已經跟鈴音的父親學過「契約之絆」。</summary>
        public bool LearnedBond;

        /// <summary>已經跟鈴音的父親學過「砸瓦魯多」。</summary>
        public bool LearnedTimeStop;

        /// <summary>打完（不管成敗）時回呼，本篇用它把結果寫回變數。</summary>
        public Action<SVResult> OnFinished;

        /// <summary>加入別人的隊伍時，玩家沒有指揮權。</summary>
        public bool PlayerCommands => Mode != SVPartyMode.JoinOther;
    }

    public class SVResult
    {
        /// <summary>撐過 SurviveSeconds 就算完成。</summary>
        public bool Cleared;

        /// <summary>玩家自己按「返回公會」撤退的（不是被打倒）。</summary>
        public bool Retreated;

        public float Survived;
        public int Kills;
        public int Level;

        /// <summary>實際帶回公會的戰利品（已依結果縮水過）。</summary>
        public SVLootBag Loot;
    }

    /// <summary>本篇與小遊戲之間唯一的交接點。</summary>
    public static class SVSession
    {
        /// <summary>本篇進場前填這個；SVGame 起手會取走並清掉。</summary>
        public static SVBriefing Pending;

        /// <summary>最近一次的結果，方便除錯時查看。</summary>
        public static SVResult Last;
    }
}
