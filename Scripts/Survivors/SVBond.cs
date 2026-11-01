// 獨自組隊的多人羈絆（2026-09-26 規格）。
//
// 只有「獨自組隊」（RM 變數 44）會觸發 —— 隊伍是自己挑的，才談得上誰跟誰合不合。
// 加入別人的隊伍、僱傭都只帶一位女主，沒有羈絆；教學關也沒有。
//
//   鈴音「孤獨的大法師」：只帶鈴音時傷害 ×5。
//       有其他人在場，效果剩 30%；狼姐妹在場則剩 50%；
//       露娜在場再多 50 個百分點（最多回到 100%）。
//       打折的是「多出來的那 4 倍」：倍率 = 1 + 4 × 效果百分比
//         只帶鈴音 ×5　帶其他人 ×2.2　狼姐妹 ×3　其他人＋露娜 ×4.2　狼姐妹＋露娜 ×5
//   尤梨卡：道具掉落機率 +50%。
//   克萊爾（公主）：金幣 +50%。
//   尤梨卡＋克萊爾：兩人不和，全隊（含男主）攻擊 -20%。
//   露娜：調和劑，在場時所有 DEBUFF 消失。
//         另外人越多越強：全隊傷害、護盾量每位同行者 +5%
//         （算場上隊友人數，露娜自己也算，狼姐妹算兩人）。
//   狼姐妹：全隊物攻、魔攻、防禦 +10%；鈴音也在場時變成 +20%。
//
// 要調數字改下面那幾個常數就好。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public class SVBond
    {
        public const float SuzuneLoneMul = 5f;        // 只帶鈴音
        public const float SuzuneWithOthers = 0.3f;   // 有其他人：效果剩 30%
        public const float SuzuneWithWolves = 0.5f;   // 狼姐妹在場：效果剩 50%
        public const float LunaBoost = 0.5f;          // 露娜在場：再 +50 個百分點
        public const float YurikaItemMul = 1.5f;      // 尤梨卡：道具掉落 +50%
        public const float ClaireGoldMul = 1.5f;      // 克萊爾：金幣 +50%
        public const float DiscordAttackMul = 0.8f;   // 尤梨卡＋克萊爾：全隊攻擊 -20%
        public const float LunaPerPerson = 0.05f;     // 露娜：每位同行者全隊傷害、護盾 +5%
        public const float WolfBonus = 0.10f;         // 狼姐妹：全隊物攻、魔攻、防禦 +10%
        public const float WolfSuzuneScale = 2f;      // 狼姐妹＋鈴音：上面那個加成 ×2（+20%）

        /// <summary>沒有任何羈絆（非獨自組隊、教學、沙盒）。</summary>
        public static readonly SVBond None = new SVBond();

        public bool Active { get; private set; }

        /// <summary>鈴音（Arcane）傷害倍率。</summary>
        public float SuzuneMul { get; private set; } = 1f;

        /// <summary>道具（非金幣）掉落機率倍率。</summary>
        public float ItemDropMul { get; private set; } = 1f;

        /// <summary>金幣數量倍率。</summary>
        public float GoldMul { get; private set; } = 1f;

        /// <summary>全隊（男主＋隊友）攻擊倍率：不和 DEBUFF × 露娜 × 狼姐妹。</summary>
        public float TeamAttackMul { get; private set; } = 1f;

        /// <summary>全隊（男主＋隊友）防禦倍率：狼姐妹。</summary>
        public float DefenseMul { get; private set; } = 1f;

        /// <summary>全隊（男主＋隊友）拿到的護盾量倍率：露娜。</summary>
        public float ShieldMul { get; private set; } = 1f;

        /// <summary>開場顯示的說明，一行一個效果。</summary>
        public string Summary { get; private set; } = "";

        /// <summary>某位隊友實際吃到的攻擊倍率。</summary>
        public float CompanionMul(SVCompanionType type) =>
            TeamAttackMul * (type != null && type.Skill == SVSkillKind.Arcane ? SuzuneMul : 1f);

        public static SVBond From(SVBriefing b)
        {
            if (b == null || b.Tutorial || b.Mode != SVPartyMode.Solo || b.Team == null || b.Team.Count == 0)
                return None;

            var team = new HashSet<SVHeroine>(b.Team);
            bool suzune = team.Contains(SVHeroine.Suzune);
            bool luna = team.Contains(SVHeroine.Luna);
            bool wolves = team.Contains(SVHeroine.WolfSisters);
            bool yurika = team.Contains(SVHeroine.Yurika);
            bool claire = team.Contains(SVHeroine.Claire);

            var bond = new SVBond { Active = true };
            var lines = new List<string>();

            if (suzune)
            {
                bool others = team.Count > 1;
                float pct = !others ? 1f : (wolves ? SuzuneWithWolves : SuzuneWithOthers);
                if (others && luna) pct = Mathf.Min(1f, pct + LunaBoost);
                bond.SuzuneMul = 1f + (SuzuneLoneMul - 1f) * pct;
                lines.Add($"孤獨的大法師：鈴音傷害 ×{bond.SuzuneMul:0.##}");
            }

            if (yurika)
            {
                bond.ItemDropMul = YurikaItemMul;
                lines.Add("尤梨卡：道具掉落 +50%");
            }

            if (claire)
            {
                bond.GoldMul = ClaireGoldMul;
                lines.Add("克萊爾：金幣 +50%");
            }

            if (yurika && claire)
            {
                if (luna) lines.Add("露娜調和：尤梨卡與克萊爾的不和消失");
                else
                {
                    bond.TeamAttackMul = DiscordAttackMul;
                    lines.Add("尤梨卡與克萊爾不和：全隊攻擊 -20%");
                }
            }

            if (luna)
            {
                int people = b.Companions != null ? b.Companions.Count : team.Count;
                float bonus = LunaPerPerson * people;
                bond.TeamAttackMul *= 1f + bonus;
                bond.ShieldMul = 1f + bonus;
                lines.Add($"露娜：全隊傷害、護盾 +{Mathf.RoundToInt(bonus * 100f)}%（{people} 人）");
            }

            if (wolves)
            {
                float bonus = WolfBonus * (suzune ? WolfSuzuneScale : 1f);
                bond.TeamAttackMul *= 1f + bonus;
                bond.DefenseMul = 1f + bonus;
                lines.Add($"狼姐妹：全隊物攻、魔攻、防禦 +{Mathf.RoundToInt(bonus * 100f)}%" + (suzune ? "（鈴音在場）" : ""));
            }

            bond.Summary = string.Join("\n", lines);
            if (lines.Count == 0) bond.Active = false;
            return bond;
        }
    }
}
