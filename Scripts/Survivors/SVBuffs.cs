// 把散在各處的加成整理成一份「這個角色現在身上有什麼」的清單，給左邊的角色面板顯示。
//
// 這裡只讀不寫 —— 資料的真正來源還是 SVPlayer / SVCompanion / SVAwakenState，
// 這支只負責翻譯成人看得懂的字。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public struct SVBuffLine
    {
        public string Label;
        public string Value;
        public Color Tint;

        public SVBuffLine(string label, string value, Color tint)
        {
            Label = label; Value = value; Tint = tint;
        }
    }

    public static class SVBuffs
    {
        static readonly Color Neutral = new Color(0.86f, 0.88f, 0.92f);
        static readonly Color Good = new Color(0.65f, 1f, 0.72f);
        static readonly Color Skill = new Color(1f, 0.86f, 0.5f);
        static readonly Color Awaken = new Color(1f, 0.6f, 0.88f);
        static readonly Color Bad = new Color(1f, 0.55f, 0.55f);

        static string Pct(float mul) => $"{(mul - 1f) * 100f:+0;-0}%";

        // ---------------- 男主 ----------------

        public static List<SVBuffLine> For(SVPlayer player, SVGame game)
        {
            var lines = new List<SVBuffLine>();
            if (player == null) return lines;

            lines.Add(new SVBuffLine("等級", $"Lv.{player.Level}", Neutral));
            lines.Add(new SVBuffLine("血量", $"{player.Hp} / {player.MaxHp}", Neutral));
            lines.Add(new SVBuffLine("普攻",
                $"{player.AttackDamage} 傷害　{player.AttackInterval:0.00} 秒" +
                $"　（{(player.IsMage ? "魔法" : "物理")}）", Neutral));

            if (player.PhysicalPower > 0 || player.MagicPower > 0)
                lines.Add(new SVBuffLine("攻擊力",
                    $"物 {player.PhysicalPower}　魔 {player.MagicPower}", Neutral));

            AddAffix(lines, player);

            if (player.AttackShots > 1) lines.Add(new SVBuffLine("散射", $"{player.AttackShots} 發", Good));
            if (player.AttackPierce > 0) lines.Add(new SVBuffLine("貫穿", $"穿 {player.AttackPierce} 隻", Good));
            if (player.Defense > 0 || player.MagicDefense > 0)
                lines.Add(new SVBuffLine("裝備減傷",
                    $"物 {player.Defense}　魔 {player.MagicDefense}", Good));
            if (player.Shield > 0) lines.Add(new SVBuffLine("護盾", $"{player.Shield}", Good));
            if (player.Invincible > 0f) lines.Add(new SVBuffLine("無敵", $"{player.Invincible:0.0} 秒", Good));

            if (game != null && game.AttackBuffLeft > 0f)
                lines.Add(new SVBuffLine("力量藥水",
                    $"{Pct(game.AttackBuffMultiplier)}　{game.AttackBuffLeft:0.0} 秒", Good));

            foreach (var s in player.Skills)
            {
                string state = s.CooldownLeft > 0f ? $"{s.CooldownLeft:0.0} 秒" : "可發動";
                lines.Add(new SVBuffLine(s.Name, $"Lv.{s.Level}　{state}", Skill));
            }

            AddBlessings(lines, game);
            AddAwakenings(lines, game);
            return lines;
        }

        /// <summary>武器專屬的強化。拿劍跟拿法杖看到的是完全不同的兩組。</summary>
        static void AddAffix(List<SVBuffLine> lines, SVPlayer player)
        {
            var a = player.Affix;

            if (!player.IsMage)
            {
                if (a.Dodge > 0f) lines.Add(new SVBuffLine("迴避", Pct(1f + a.Dodge), Good));
                if (a.Crit > 0f)
                    lines.Add(new SVBuffLine("暴擊",
                        $"{Pct(1f + a.Crit)}　×{a.CritMultiplier:0.0}", Good));
                if (a.BleedPower > 0)
                    lines.Add(new SVBuffLine("流血",
                        $"{Pct(1f + a.BleedChance)} 機率　每跳 {a.BleedPower}", Good));
                return;
            }

            if (a.Frost > 0)
                lines.Add(new SVBuffLine("冰霜附魔",
                    a.FreezeChance > 0f ? $"Lv.{a.Frost}　凍結 {Pct(1f + a.FreezeChance)}"
                                        : $"Lv.{a.Frost}　減速", Good));
            if (a.Gale > 0)
                lines.Add(new SVBuffLine("疾風附魔",
                    $"Lv.{a.Gale}　多打一發 {Pct(1f + a.GaleExtraShotChance)}", Good));
            if (a.Venom > 0)
                lines.Add(new SVBuffLine("劇毒附魔",
                    $"Lv.{a.Venom}　每跳 {a.VenomPower}　{a.VenomSeconds:0} 秒", Good));
            if (a.Flame > 0)
                lines.Add(new SVBuffLine("烈焰附魔",
                    $"Lv.{a.Flame}　爆炸 {a.FlameDamage}", Good));
        }

        // ---------------- 隊友 ----------------

        public static List<SVBuffLine> For(SVCompanion c, SVGame game)
        {
            var lines = new List<SVBuffLine>();
            if (c == null || c.Type == null) return lines;

            if (c.Retired)
            {
                lines.Add(new SVBuffLine("狀態", "已退出戰場", Bad));
                return lines;
            }

            lines.Add(new SVBuffLine("血量", $"{c.Hp} / {c.MaxHp}", Neutral));
            lines.Add(new SVBuffLine("命令", OrderName(c.Order), Neutral));
            lines.Add(new SVBuffLine(c.Type.SkillName, $"{c.Power} 傷害　{c.Cooldown:0.00} 秒", Skill));

            if (!Mathf.Approximately(c.PowerMul, 1f))
                lines.Add(new SVBuffLine("威力", Pct(c.PowerMul), Good));
            if (!Mathf.Approximately(c.CooldownMul, 1f))
                lines.Add(new SVBuffLine("冷卻", Pct(c.CooldownMul), Good));
            if (!Mathf.Approximately(c.RangeMul, 1f))
                lines.Add(new SVBuffLine("範圍", Pct(c.RangeMul), Good));
            if (c.ExtraShots > 0)
                lines.Add(new SVBuffLine("增彈", $"+{c.ExtraShots} 發", Good));

            if (c.Shield > 0) lines.Add(new SVBuffLine("護盾", $"{c.Shield}", Good));
            if (c.Invincible > 0f) lines.Add(new SVBuffLine("無敵", $"{c.Invincible:0.0} 秒", Good));

            if (c.Type.Defense > 0) lines.Add(new SVBuffLine("減傷", $"{c.Type.Defense}", Good));
            if (c.Type.ShieldBossOnly) lines.Add(new SVBuffLine("魔導護盾", "雜魚無效", Good));
            if (c.Type.BossDamageMultiplier > 1f)
                lines.Add(new SVBuffLine("對王傷害", $"×{c.Type.BossDamageMultiplier:0.#}", Good));
            if (c.Type.PlayerShieldPower > 0)
                lines.Add(new SVBuffLine("守護男主", $"每 {c.Type.PlayerShieldInterval:0} 秒 {c.Type.PlayerShieldPower} 盾", Good));

            if (game != null && !Mathf.Approximately(game.CompanionPowerScale, 1f))
                lines.Add(new SVBuffLine("隊友威力", Pct(game.CompanionPowerScale), Good));
            if (game != null && game.TeamDamageBonus > 0f)
                lines.Add(new SVBuffLine("全隊傷害", $"+{Mathf.RoundToInt(game.TeamDamageBonus * 100f)}%", Good));

            AddAwakenings(lines, game);
            return lines;
        }

        static void AddBlessings(List<SVBuffLine> lines, SVGame game)
        {
            if (game == null) return;
            foreach (var p in game.Passives.Chosen)
                lines.Add(new SVBuffLine("【祝福】" + p.Name, "", new Color(0.7f, 0.95f, 1f)));
        }

        static void AddAwakenings(List<SVBuffLine> lines, SVGame game)
        {
            if (game == null) return;

            foreach (var kind in game.Awakenings.All)
            {
                int stacks = game.Awakenings.Stacks(kind);
                string value = stacks > 1 ? $"{stacks} 層" : "生效中";
                lines.Add(new SVBuffLine("【大覺】" + AwakenName(kind), value, Awaken));
            }

            float berserk = game.Awakenings.BerserkMultiplier(game.Player);
            if (berserk > 1f)
                lines.Add(new SVBuffLine("狂血發動", $"傷害 ×{berserk:0.##}", Awaken));
            float repeat = game.Awakenings.RepeatDamageBonus;
            if (repeat > 0f)
                lines.Add(new SVBuffLine("大覺重複強化", $"全域傷害 +{Mathf.RoundToInt(repeat * 100f)}%", Awaken));
        }

        public static string OrderName(SVOrder order)
        {
            switch (order)
            {
                case SVOrder.Free: return "自由移動";
                case SVOrder.Hold: return "定點待命";
                default: return "跟隨玩家";
            }
        }

        public static string AwakenName(SVAwakenKind kind)
        {
            switch (kind)
            {
                case SVAwakenKind.Berserk: return "狂血";
                case SVAwakenKind.TimeStop: return "時停之刻";
                case SVAwakenKind.BladeRain: return "劍雨";
                case SVAwakenKind.Bond: return "契約之絆";
                case SVAwakenKind.Vampire: return "血之渴";
                case SVAwakenKind.Detonate: return "爆裂連鎖";
                case SVAwakenKind.DoubleCast: return "雙重詠唱";
                case SVAwakenKind.Rocket: return "火箭彈";
                case SVAwakenKind.Blade: return "劍氣";
                case SVAwakenKind.StaffMeteor: return "星隕・法杖";
                case SVAwakenKind.Adrenaline: return "腎上腺素";
                case SVAwakenKind.Overdrive: return "極速連擊";
                case SVAwakenKind.Collector: return "收集者";
                case SVAwakenKind.PoisonBurst: return "毒爆";
                default: return kind.ToString();
            }
        }
    }
}
