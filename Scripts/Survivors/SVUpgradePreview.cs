using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using RMZS.GodotPort;
namespace RMZS
{
    // 只建立預覽資料，不對本局角色呼叫 Apply，避免選單預覽改到能力。
    public static class SVUpgradePreview
    {
        static string N(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        static string Pair(string label,float before,float after,string unit="") => $"{label} {N(before)} → {N(after)}{unit}";
        public static string Describe(SVUpgrade upgrade, SVGame game)
        {
            int stacks=game.Stacks(upgrade.Id);
            if(upgrade.Id=="sw_crit"&&game.Player!=null)
            {
                // 破綻：顯示這次加多少，以及現在 → 拿了之後的暴擊率
                var p=game.Player;float step=p.Affix.NextCritStep;
                float now=p.CritChance*100f, after=Mathf.Min(p.CritChance+Mathf.Min(step,0.75f-p.Affix.Crit),1f)*100f;
                return $"暴擊率 +{Mathf.RoundToInt(step*100f)}%\n<size=17><color=#FFD98A>暴擊率 {Mathf.RoundToInt(now)}% → {Mathf.RoundToInt(after)}%</color></size>"+"\n<size=14>（劍專屬）</size>";
            }
            if(upgrade.Id=="a_rune_guard"||upgrade.Id=="a_frost_echo"||upgrade.Id=="s_star_hunt"||upgrade.Id=="s_soul_tide") {
                string value=upgrade.Id switch {
                    "a_rune_guard" => Pair("護盾回復",12*stacks,12*(stacks+1))+"\n"+Pair("補充上限",36*stacks,36*(stacks+1)),
                    "a_frost_echo" => Pair("每次傷害",24*stacks,24*(stacks+1)),
                    "s_star_hunt" => Pair("每名目標傷害",65*stacks,65*(stacks+1)),
                    _ => Pair("傷害",36*stacks,36*(stacks+1))+"\n"+Pair("回復上限",15*stacks,15*(stacks+1))
                };
                return upgrade.Desc+"\n<size=17><color=#FFD98A>"+value+"</color></size>";
            }
            bool special=upgrade.Id.StartsWith("pskill_")||upgrade.Id.StartsWith("awaken_");
            if(stacks<=0)
            {
                // 第一次拿：一般強化也先讓玩家看到「現在 → 選後」的實際數值
                string first=special?null:StatPreview(upgrade.Id,game);
                return first==null?upgrade.Desc:upgrade.Desc+"\n<size=17><color=#FFD98A>"+first+"</color></size>";
            }
            var lines=new List<string>();
            if(upgrade.Id.StartsWith("pskill_") && Enum.TryParse<SVPlayerSkillKind>(upgrade.Id.Substring(7), out var kind))
            {
                int level=game.Player?.SkillLevel(kind) ?? stacks;
                var before=SVPlayerSkill.Create(kind); before.Level=level;
                var after=SVPlayerSkill.Create(kind); after.Level=level+1;
                if(before.PowerPerLevel!=0)lines.Add(Pair(kind==SVPlayerSkillKind.Barrier?"基礎治療":"基礎威力",before.Power,after.Power));
                lines.Add(Pair("技能冷卻",before.Cooldown,after.Cooldown," 秒"));
                if(kind==SVPlayerSkillKind.Barrier)lines.Add(Pair("無敵",1.2f+level*.2f,1.2f+(level+1)*.2f," 秒"));
                else lines.Add(Pair(kind==SVPlayerSkillKind.Warlord||kind==SVPlayerSkillKind.ShieldBash?"衝刺距離":"作用距離",before.Range,after.Range));
                if(kind==SVPlayerSkillKind.StarFall)lines.Add(Pair("隕石數",3+level,4+level));
                if(kind==SVPlayerSkillKind.Chain)lines.Add(Pair("命中目標數",3+level,4+level));
                if(kind==SVPlayerSkillKind.PoisonCloud)lines.Add("毒層效果不變，範圍擴大。");
                if(kind==SVPlayerSkillKind.SwordRing)lines.Add(Pair("飛劍數",Mathf.Min(6,3+level/2),Mathf.Min(6,3+(level+1)/2)));
                if(kind==SVPlayerSkillKind.Judgment)lines.Add(Pair("鎖定數",2+level,3+level));
                if(kind==SVPlayerSkillKind.FrostNova)lines.Add(Pair("凍結",1.2f+level*.15f,1.2f+(level+1)*.15f," 秒"));
                if(kind==SVPlayerSkillKind.AbsoluteZero)lines.Add(Pair("凍結",2f+level*.2f,2f+(level+1)*.2f," 秒"));
                lines.Add("威力未含角色攻擊、防禦結算。");
            }
            else if(upgrade.Id.StartsWith("awaken_") && Enum.TryParse<SVAwakenKind>(upgrade.Id.Substring(7),out var awaken))
            {
                var a=new SVAwakenState();var b=new SVAwakenState();
                for(int i=0;i<stacks;i++){a.Gain(awaken);b.Gain(awaken);}b.Gain(awaken);
                lines.Add(Pair("技能冷卻",a.SkillOf(awaken).Cooldown,b.SkillOf(awaken).Cooldown," 秒"));
                switch(awaken)
                {
                    case SVAwakenKind.BladeRain:
                        lines.Add(Pair("每劍基礎傷害",a.BladeRainDamage,b.BladeRainDamage));
                        lines.Add(Pair("每秒落劍",a.BladeRainRate,b.BladeRainRate," 把"));
                        lines.Add("範圍擴大。");break;
                    case SVAwakenKind.Rocket:
                        lines.Add(Pair("火箭數",a.RocketCount,b.RocketCount));
                        lines.Add(Pair("每發基礎傷害",a.RocketDamage,b.RocketDamage));
                        lines.Add(Pair("爆炸半徑",a.RocketSplashRadius,b.RocketSplashRadius));break;
                    case SVAwakenKind.Blade:
                        lines.Add(Pair("基礎傷害",a.BladeDamage,b.BladeDamage));
                        lines.Add(Pair("劍氣寬度",a.BladeWidth,b.BladeWidth));break;
                    case SVAwakenKind.StaffMeteor:
                        lines.Add(Pair("隕石數",a.StaffMeteorCount,b.StaffMeteorCount));
                        lines.Add(Pair("每顆基礎傷害",a.StaffMeteorDamage,b.StaffMeteorDamage));
                        lines.Add(Pair("爆炸半徑",a.StaffMeteorRadius,b.StaffMeteorRadius));break;
                    case SVAwakenKind.Collector:
                        lines.Add(Pair("處決血量門檻",a.ExecuteHpThreshold,b.ExecuteHpThreshold));
                        lines.Add(Pair("處決機率",a.ExecuteChance*100,b.ExecuteChance*100,"%"));
                        lines.Add(Pair("BOSS 機率",a.ExecuteBossChance*100,b.ExecuteBossChance*100,"%"));break;
                    case SVAwakenKind.PoisonBurst:
                        lines.Add(Pair("每層毒基礎傷害",a.PoisonBurstPerStack,b.PoisonBurstPerStack));break;
                    case SVAwakenKind.Detonate:
                        lines.Add(Pair("死亡爆炸基礎傷害",a.DetonatePower,b.DetonatePower));
                        lines.Add(Pair("死亡爆炸半徑",a.DetonateRadius,b.DetonateRadius));break;
                    case SVAwakenKind.Adrenaline:
                        a.SkillOf(awaken).ActiveLeft=1;b.SkillOf(awaken).ActiveLeft=1;
                        lines.Add(Pair("每秒回血",a.AdrenalineRegen,b.AdrenalineRegen));
                        lines.Add("持續 5 秒，加速效果不變。");break;
                    case SVAwakenKind.Overdrive:
                        lines.Add("攻速維持 10 倍，持續 4 秒。");break;
                    case SVAwakenKind.Berserk:
                        lines.Add(Pair("發動傷害倍率",2f+.25f*(stacks-1),2f+.25f*stacks," 倍"));
                        lines.Add(Pair("殘血傷害倍率",1.5f+.15f*(stacks-1),1.5f+.15f*stacks," 倍"));break;
                }
                if(Array.IndexOf(SVAwakenState.RepeatDamageKinds,awaken)>=0)
                    lines.Add("<color=#FFD98A>"+Pair("全域傷害加成",a.RepeatDamageBonus*100,b.RepeatDamageBonus*100,"%")+"</color>");
                lines.Add("取得後重置此大覺的冷卻。");
            }
            else
            {
                lines.Add("本次追加："+upgrade.Desc);
                // 現在多少 → 選了之後多少
                string now=StatPreview(upgrade.Id,game)??Cumulative(upgrade,stacks);
                if(now!=null)lines.Add("<color=#FFD98A>"+now+"</color>");
                if(upgrade.Id=="heal"||upgrade.Id=="b_ward"||upgrade.Id=="a_lastward")lines.Add("立即生效，不是永久升級。");
            }
            return string.Join("\n",lines);
        }

        /// <summary>男主自己的強化：直接拿目前的實際數值算「現在 → 選後」。算法跟 SVUpgrades 裡的 Apply 一致。</summary>
        static string StatPreview(string id,SVGame game)
        {
            var p=game.Player; if(p==null)return null;
            switch(id)
            {
                case "atk": return Pair("普攻傷害",p.AttackDamage,Mathf.Max(p.AttackDamage+1,Mathf.RoundToInt(p.AttackDamage*1.10f)));
                case "aspd": return Pair("每秒攻擊",1f/Mathf.Max(0.01f,p.AttackInterval),1f/Mathf.Max(0.1f,p.AttackInterval*0.88f)," 次");
                case "multi": return Pair("普攻發數",p.AttackShots,p.AttackShots+1);
                case "pierce": return Pair("穿透",p.AttackPierce,p.AttackPierce+1);
                case "range": return Pair("射程",p.AttackRange,p.AttackRange+2f);
                case "speed": return Pair("移動速度",p.MoveSpeed,p.MoveSpeed*1.08f);
                case "hp": return Pair("血量上限",p.MaxHp,p.MaxHp+25);
                case "b_vitality": return Pair("血量上限",p.MaxHp,p.MaxHp+45);
                case "magnet": return Pair("拾取範圍",game.PickupRadius,game.PickupRadius+0.5f);
                case "b_armor": return Pair("物理防禦",p.Defense,p.Defense+8);
                case "b_wardrobe": return Pair("魔法防禦",p.MagicDefense,p.MagicDefense+8);
                case "a_bulwark": return Pair("物防",p.Defense,p.Defense+14)+"\n"+Pair("魔防",p.MagicDefense,p.MagicDefense+14)+"\n"+Pair("血量上限",p.MaxHp,p.MaxHp+60);
                case "mg_focus": return Pair("魔攻",p.MagicPower,p.MagicPower+12);
                case "sw_might": return Pair("物攻",p.PhysicalPower,p.PhysicalPower+12);
                case "heal": return Pair("目前血量",p.Hp,Mathf.Min(p.MaxHp,p.Hp+p.MaxHp/2));
                case "b_ward": return Pair("護盾",p.Shield,p.Shield+Mathf.RoundToInt(p.MaxHp*0.2f));
                case "a_lastward": return Pair("護盾",p.Shield,p.Shield+Mathf.RoundToInt(p.MaxHp*0.35f));
            }
            return null;
        }

        static readonly Regex PercentStep=new Regex(@"([+\-])\s*(\d+(?:\.\d+)?)%",RegexOptions.Compiled);

        /// <summary>
        /// 隊友與全隊強化：說明裡寫的是「每次 ±N%」，實際是乘算（PowerMul *= 1.3 那種），
        /// 所以累計照乘算算：(1+N%)^次數 − 1。說明裡沒有百分比的（多一發、多一把刀）只顯示次數。
        /// </summary>
        static string Cumulative(SVUpgrade u,int stacks)
        {
            var m=PercentStep.Match(u.Desc??"");
            if(!m.Success)return $"已選 {stacks} 次 → {stacks+1} 次";
            float step=float.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture)/100f*(m.Groups[1].Value=="-"?-1f:1f);
            string label=u.Desc.Substring(0,m.Index).Trim();
            float before=((float)Math.Pow(1f+step,stacks)-1f)*100f;
            float after=((float)Math.Pow(1f+step,stacks+1)-1f)*100f;
            return $"{label} 累計 {Signed(before)}% → {Signed(after)}%";
        }

        static string Signed(float v)=>(v>=0?"+":"")+v.ToString("0.#",CultureInfo.InvariantCulture);
    }
}
