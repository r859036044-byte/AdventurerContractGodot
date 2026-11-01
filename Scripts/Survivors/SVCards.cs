// 卡片：敵人身上掉的另一條成長線。
//
// 跟能量條升級的三選一分開：升級是「學新東西」（新技能、新強化），
// 卡片給的是「對所有技能一起生效」的全域修正與附魔 ——
// 技能全部附帶流血、全部多打兩發、全部冷卻減半這種。
// 另外還有兩張特別的：加血上限，以及直接撿到一個沒拿過的公會祝福。
//
// 撿到卡片就開一次三選一，用的是升級那套同一個面板（SVGame 的 OfferChoices）。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public static class SVCards
    {
        /// <summary>一次抽幾張讓玩家選。</summary>
        public const int OfferCount = 3;

        public static List<SVUpgrade> BuildCatalog()
        {
            var list = new List<SVUpgrade>();

            var lifeTint = new Color(0.6f, 1f, 0.65f);
            var skillTint = new Color(0.8f, 0.7f, 1f);
            var dotTint = new Color(0.7f, 1f, 0.5f);
            var atkTint = new Color(1f, 0.8f, 0.45f);
            var blessTint = new Color(0.65f, 0.9f, 1f);

            // ---------------- 技能強化（對所有技能生效）----------------
            list.Add(new SVUpgrade
            {
                Id = "card_skill_power", Name = "技能・威力",
                Desc = "所有技能的傷害 +25%",
                MaxStacks = 99, Tint = skillTint,
                Apply = g => g.SkillPowerMul *= 1.25f,
            });

            list.Add(new SVUpgrade
            {
                Id = "card_skill_count", Name = "技能・增量",
                Desc = "所有技能的發數／顆數 +2",
                MaxStacks = 99, Weight = 0.8f, Tint = skillTint,
                Apply = g => g.SkillCountBonus += 2,
            });

            list.Add(new SVUpgrade
            {
                Id = "card_skill_cd", Name = "技能・急速",
                Desc = "所有技能的冷卻 -20%",
                MaxStacks = 99, Tint = skillTint,
                Apply = g => g.SkillCooldownMul *= 0.80f,
            });

            list.Add(new SVUpgrade
            {
                Id = "card_skill_duration", Name = "技能・延續",
                Desc = "所有技能的持續時間 +30%",
                MaxStacks = 99, Tint = skillTint,
                Apply = g => g.SkillDurationMul *= 1.30f,
            });

            // ---------------- 技能附魔（持續傷害）----------------
            list.Add(new SVUpgrade
            {
                Id = "card_skill_bleed", Name = "技能附魔・流血",
                Desc = "技能附加 1 層流血，可無限疊加、可暴擊。",
                MaxStacks = 99, Tint = dotTint,
                Apply = g => g.SkillBleedStacks += 1,
            });

            list.Add(new SVUpgrade
            {
                Id = "card_skill_poison", Name = "技能附魔・劇毒",
                Desc = "技能附加 1 層毒，毒層不消退。",
                MaxStacks = 99, Tint = dotTint,
                Apply = g => g.SkillPoisonStacks += 1,
            });

            list.Add(new SVUpgrade
            {
                Id = "card_skill_burn", Name = "技能附魔・燃燒",
                Desc = "技能附加燃燒，每秒造成最大生命 2% 傷害。",
                MaxStacks = 1, Tint = dotTint,
                Apply = g => g.SkillIgnites = true,
            });

            // ---------------- 普攻附魔 ----------------
            list.Add(new SVUpgrade
            {
                Id = "card_atk_bleed", Name = "普攻附魔・流血",
                Desc = "普攻命中時附加 1 層流血",
                MaxStacks = 99, Tint = atkTint,
                Apply = g => g.AttackBleedStacks += 1,
            });

            // ---------------- 持續傷害強化 ----------------
            list.Add(new SVUpgrade
            {
                Id = "card_dot", Name = "腐蝕強化",
                Desc = "中毒、流血與燃燒傷害 +50%。",
                MaxStacks = 99, Weight = 0.9f, Tint = dotTint,
                Apply = g => g.DotMultiplier += 0.5f,
            });

            // ---------------- 生命 ----------------
            list.Add(new SVUpgrade
            {
                Id = "card_hp", Name = "生命結晶",
                Desc = "最大生命 +15%，並立刻回復 20% 的血量",
                MaxStacks = 99, Weight = 1.2f, Tint = lifeTint,
                Apply = g =>
                {
                    g.Player.MaxHp = Mathf.RoundToInt(g.Player.MaxHp * 1.15f);
                    g.Player.Heal(Mathf.RoundToInt(g.Player.MaxHp * 0.20f));
                },
            });

            // ---------------- 公會祝福 ----------------
            //
            // 同一個祝福不會給第二次 —— 沒得拿的時候這張卡就不會出現在三選一裡。
            list.Add(new SVUpgrade
            {
                Id = "card_blessing", Name = "公會的餽贈",
                Desc = "隨機獲得一個還沒拿過的公會祝福",
                MaxStacks = 99, Weight = 0.9f, Tint = blessTint,
                Available = HasUntakenBlessing,
                Apply = GrantRandomBlessing,
            });

            return list;
        }

        /// <summary>還有沒拿過的祝福可以給嗎。</summary>
        static bool HasUntakenBlessing(SVGame game)
        {
            if (game?.Passives == null) return false;

            foreach (var p in SVPassives.BuildCatalog())
                if (p != null && !game.Passives.Has(p.Id)) return true;

            return false;
        }

        /// <summary>
        /// 隨機給一個還沒拿過的祝福。
        /// 已經有的絕對不會再給 —— 同一個祝福疊兩份的話，
        /// 「玻璃大炮扣一半血」那種代價會變成扣兩次，整個爛掉。
        /// </summary>
        static void GrantRandomBlessing(SVGame game)
        {
            if (game?.Passives == null) return;

            var pool = new List<SVPassive>();
            foreach (var p in SVPassives.BuildCatalog())
                if (p != null && !game.Passives.Has(p.Id)) pool.Add(p);

            if (pool.Count == 0) return;

            var picked = pool[Random.Range(0, pool.Count)];
            game.Passives.Gain(picked);

            // 祝福多半是改男主身上的數值，套用之後要重新結算一次
            game.Player?.ApplyPassives(game.Passives);

            game.SpawnPulse(game.Player.Pos, 4f, new Color(0.65f, 0.9f, 1f, 0.75f), 0.6f);
            Debug.Log($"[倖存者] 卡片給了祝福「{picked.Name}」。");
        }
    }
}
