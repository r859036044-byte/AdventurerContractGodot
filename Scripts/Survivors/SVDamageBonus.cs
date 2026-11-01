// 傷害加成的四種類別、傷害來源、傷害 LOG、王的傷害跳字。
//
// ---- 四種加成 ----
//   通用傷害：所有傷害都吃（男主、隊友、持續傷害、大覺）
//   BOSS 傷害：打王的時候才吃（一樣全部都吃）
//   普攻傷害：只有男主的普攻吃
//   技能傷害：只有男主的主動技能吃
// 同一類的加成「相加」，不同類之間「相乘」：
//   最終 = 基礎 × (1 + 通用合計) × (1 + BOSS合計) × (1 + 普攻合計) × (1 + 技能合計)
// 算完才扣防禦（跟原本一樣）。
//
// ---- 傷害來源 ----
// SVEnemy.TakeDamage 的參數沒辦法加，所以打之前先把來源放在 SVHit.Next，
// TakeDamage 一開頭就拿走（拿走後馬上歸回 Other，連鎖觸發的傷害不會誤用）。
// 沒標來源的一律當 Other：只吃通用＋BOSS。
using System.Collections.Generic;
using System.Text;
using Godot;
using V = RMZS.GodotPort.Vector2;

namespace RMZS
{
    public enum SVHitSource { Other, Attack, Skill, Dot }   // Dot = 毒、流血的每跳

    public static class SVHit
    {
        public static SVHitSource Next = SVHitSource.Other;
        /// <summary>
        /// 現在正在跑誰的 Update／協程（SVRuntime 每次呼叫前填進來）。
        /// 傷害 LOG 用它分辨是哪個角色打的。
        /// </summary>
        public static GodotPort.MonoBehaviour Owner;

        /// <summary>目前這段程式是誰在出手：男主、隊友名字、子彈記下的發射者；認不出來回傳 null。</summary>
        public static string CurrentActor()
        {
            switch (Owner)
            {
                case SVPlayer _: return "男主";
                case SVCompanion c: return c.Type != null && !string.IsNullOrEmpty(c.Type.Name) ? c.Type.Name : "隊友";
                case SVProjectile p: return p.Attacker;
                case SVGame _: return "大覺/場地";
                default: return null;
            }
        }

        /// <summary>這一下是誰打的（LOG 用）。男主的普攻／技能直接看來源。</summary>
        public static string AttackerFor(SVHitSource src) =>
            src == SVHitSource.Attack || src == SVHitSource.Skill ? "男主"
            : src == SVHitSource.Dot ? "毒/流血"
            : CurrentActor() ?? "其他";

        /// <summary>這一下是不是暴擊（王的傷害跳字用）。</summary>
        public static bool LastCrit;

        /// <summary>拿走這一下的來源（拿完歸回 Other）。</summary>
        public static SVHitSource Take()
        {
            var s = Next;
            Next = SVHitSource.Other;
            return s;
        }

        /// <summary>男主技能直接打單體：SVHit.Skill(e).TakeDamage(...)</summary>
        public static SVEnemy Skill(SVEnemy e) { Next = SVHitSource.Skill; return e; }

        public static string Name(SVHitSource s) => s == SVHitSource.Attack ? "普攻" : s == SVHitSource.Skill ? "技能" : s == SVHitSource.Dot ? "毒血" : "其他";
    }

    /// <summary>這一下吃到的四種加成（都是「加成合計」，0.2 = +20%）。</summary>
    public struct SVDamageBonuses
    {
        public float General, Boss, Attack, Skill;
        public float Multiplier => (1f + General) * (1f + Boss) * (1f + Attack) * (1f + Skill);
    }

    public static class SVDamageBonus
    {
        /// <summary>通用傷害合計：大覺（狂暴、重複傷害）＋力量藥水＋全隊傷害（同心）＋祝福（孤狼、血怒、狂血、剛毅、玻璃大炮）。</summary>
        public static float General(SVGame game, SVDamageKind kind)
        {
            if (game == null) return 0f;
            float sum = 0f;
            if (game.Awakenings != null && game.Player != null) sum += game.Awakenings.DamageMultiplier(game.Player) - 1f;
            sum += game.DamageBuff - 1f;
            sum += game.TeamDamageBonus;   // 同心等「全隊傷害」
            if (game.Passives != null)
            {
                sum += game.Passives.ConditionalDamage(game, kind) - 1f;
                sum += game.Passives.GeneralDamageBonus;
            }
            return sum;
        }

        public static float Boss(SVGame game) => game?.Passives != null ? game.Passives.BossDamageBonus : 0f;
        public static float Attack(SVGame game) => game?.Passives != null ? game.Passives.AttackDamageBonus : 0f;
        public static float Skill(SVGame game) => game?.Passives != null ? game.Passives.SkillDamageBonus : 0f;

        public static SVDamageBonuses For(SVGame game, SVDamageKind kind, bool boss, SVHitSource src) => new SVDamageBonuses
        {
            General = General(game, kind),
            Boss = boss ? Boss(game) : 0f,
            Attack = src == SVHitSource.Attack ? Attack(game) : 0f,
            Skill = src == SVHitSource.Skill ? Skill(game) : 0f,
        };
    }

    /// <summary>
    /// 吸血分兩種：
    ///   普攻吸血：只有男主普攻打出的傷害會回血
    ///   全能吸血：男主普攻、主動技能、毒與流血的每一跳都會回血（比較強）
    /// 普攻打出的傷害兩種都吃（相加）。隊友、大覺主動攻擊、燃燒不吸血。
    /// </summary>
    public static class SVLifesteal
    {
        public static float Attack(SVGame game) => game?.Passives != null ? game.Passives.Lifesteal : 0f;

        /// <summary>全能吸血：祝福＋大覺「血之渴」（平常每層 10%，發動中 50%）。</summary>
        public static float Omni(SVGame game) =>
            (game?.Passives != null ? game.Passives.OmniLifesteal : 0f) + (game?.Awakenings != null ? game.Awakenings.Lifesteal : 0f);

        /// <summary>這一下的吸血比例（含「絕境渴血」低血量加倍）。</summary>
        public static float For(SVGame game, SVHitSource src)
        {
            if (game == null) return 0f;
            float r = src == SVHitSource.Attack ? Attack(game) + Omni(game)
                    : src == SVHitSource.Skill || src == SVHitSource.Dot ? Omni(game)
                    : 0f;
            var p = game.Player;
            if (r > 0f && game.Passives != null && game.Passives.LowHpLifestealMul > 1f &&
                p != null && p.MaxHp > 0 && p.Hp <= p.MaxHp * 0.3f)
                r *= game.Passives.LowHpLifestealMul;
            return r;
        }
    }

    /// <summary>
    /// 傷害統計（右上角，F8 開關）。
    ///   上半：這一局每個角色的累計傷害、佔比，以及最近 5 秒的每秒傷害（DPS）
    ///   下半：最近幾下打王的明細（實際傷害、基礎值、乘了哪些加成）
    /// </summary>
    public static class SVDamageLog
    {
        /// <summary>要隱藏就改成 false（F8 也可以切換）。</summary>
        public static bool Enabled = true;
        const int MaxBossLines = 6;
        const float DpsWindow = 5f;

        static readonly Dictionary<string, long> Totals = new Dictionary<string, long>();
        static readonly Queue<(float time, int amount)> Recent = new Queue<(float, int)>();
        static readonly List<string> BossLines = new List<string>();
        static long _grand;
        static float _clock, _redrawTimer;
        static Label _label;
        static PanelContainer _panel;
        static bool _dirty;

        public static void Clear()
        {
            Totals.Clear(); Recent.Clear(); BossLines.Clear(); _grand = 0; _clock = 0f; _dirty = true;
        }

        static string Pct(float v) => (v >= 0f ? "+" : "") + Mathf.RoundToInt(v * 100f) + "%";

        public static void Record(SVEnemy target, SVHitSource src, string attacker, SVDamageKind kind, int raw,
                                  SVDamageBonuses b, int afterBonus, int dealt)
        {
            if (dealt <= 0) return;
            string who = src == SVHitSource.Attack || src == SVHitSource.Skill ? attacker + SVHit.Name(src) : attacker;
            Totals.TryGetValue(who, out long t);
            Totals[who] = t + dealt;
            _grand += dealt;
            Recent.Enqueue((_clock, dealt));
            _dirty = true;

            if (target == null || target.Type == null || !target.Type.IsBoss) return;
            // 打王的明細：實際傷害（基礎 × 加成，扣防禦後）
            var bonus = new List<string>();
            if (b.General != 0f) bonus.Add("通用" + Pct(b.General));
            if (b.Boss != 0f) bonus.Add("王" + Pct(b.Boss));
            if (b.Attack != 0f) bonus.Add("普攻" + Pct(b.Attack));
            if (b.Skill != 0f) bonus.Add("技能" + Pct(b.Skill));
            string line = $"{who}　{dealt}（基礎 {raw}" + (bonus.Count > 0 ? "，" + string.Join(" ", bonus) : "") + "）";
            BossLines.Add(line);
            while (BossLines.Count > MaxBossLines) BossLines.RemoveAt(0);
        }

        /// <summary>SVGame 每幀呼叫。</summary>
        public static void Tick(float dt)
        {
            bool f8 = Godot.Input.IsKeyPressed(Key.F8);
            if (f8 && !_f8Held) { Enabled = !Enabled; _dirty = true; }
            _f8Held = f8;

            _clock += dt;
            while (Recent.Count > 0 && _clock - Recent.Peek().time > DpsWindow) { Recent.Dequeue(); _dirty = true; }

            // 數字一直在變，每 0.25 秒重畫一次就好
            _redrawTimer -= dt;
            if (_dirty && _redrawTimer <= 0f) { _redrawTimer = 0.25f; Redraw(); }
        }
        static bool _f8Held;

        static string Num(long n) => n.ToString("#,0");

        static void Redraw()
        {
            _dirty = false;
            var rt = SVRuntime.Instance;
            if (rt == null || rt.UILayer == null) return;
            if (_panel == null || !GodotObject.IsInstanceValid(_panel))
            {
                _panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                var sb = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.6f) };
                sb.SetContentMarginAll(8);
                _panel.AddThemeStyleboxOverride("panel", sb);
                _label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.Off };
                _label.AddThemeFontOverride("font", RMZ.RMZFonts.Main);
                _label.AddThemeFontSizeOverride("font_size", 12);
                _label.AddThemeColorOverride("font_color", new Color(0.92f, 0.95f, 1f));
                _panel.AddChild(_label);
                rt.UILayer.AddChild(_panel);
            }
            _panel.Visible = Enabled && _grand > 0;
            if (!_panel.Visible) return;

            long recent = 0;
            foreach (var r in Recent) recent += r.amount;
            float window = Mathf.Min(DpsWindow, Mathf.Max(1f, _clock));

            var sbText = new StringBuilder();
            sbText.Append($"傷害統計（F8 開關）\n總傷害 {Num(_grand)}　每秒 {Num((long)(recent / window))}\n");
            var list = new List<KeyValuePair<string, long>>(Totals);
            list.Sort((a, c) => c.Value.CompareTo(a.Value));
            foreach (var kv in list)
            {
                float share = _grand > 0 ? kv.Value * 100f / _grand : 0f;
                int bars = Mathf.RoundToInt(share / 10f);
                sbText.Append($"{kv.Key,-6} {Num(kv.Value),8}  {share,3:0}% {new string('|', bars)}\n");
            }
            if (BossLines.Count > 0)
            {
                sbText.Append("── 最近打王 ──\n");
                sbText.Append(string.Join("\n", BossLines));
            }
            _label.Text = sbText.ToString().TrimEnd();
            var view = _panel.GetViewportRect().Size;
            _panel.ResetSize();
            _panel.Position = new Vector2(view.X - _panel.Size.X - 12f, 76f);
        }
    }

    /// <summary>打王時頭上跳的傷害數字。同一時間最多 30 個，太多就不跳。</summary>
    public partial class SVDamageNumber : Label
    {
        static int _alive;
        float _age;
        const float Life = 0.8f;

        public static void Spawn(V at, int amount, bool crit, SVDamageKind kind)
        {
            var rt = SVRuntime.Instance;
            if (rt == null || rt.World == null || amount <= 0 || _alive >= 30) return;
            var n = new SVDamageNumber
            {
                Text = amount.ToString(),
                ZIndex = 4000, ZAsRelative = false,
                MouseFilter = MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center,
                Size = new Vector2(120f, 30f),
            };
            var color = kind == SVDamageKind.True ? new Color(1f, 0.6f, 0.35f)
                      : kind == SVDamageKind.Magic ? new Color(0.65f, 0.8f, 1f)
                      : new Color(1f, 1f, 1f);
            if (crit) color = new Color(1f, 0.85f, 0.25f);
            n.AddThemeFontOverride("font", RMZ.RMZFonts.Main);
            n.AddThemeFontSizeOverride("font_size", crit ? 24 : kind == SVDamageKind.True ? 12 : 18);
            n.AddThemeColorOverride("font_color", color);
            n.AddThemeConstantOverride("outline_size", 4);
            n.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
            float jitter = (float)GD.RandRange(-24.0, 24.0);
            n.Position = new Vector2(at.x * 48f - 60f + jitter, -at.y * 48f - 80f);
            _alive++;
            rt.World.AddChild(n);
        }

        public override void _Process(double delta)
        {
            _age += (float)delta;
            Position += new Vector2(0f, -40f * (float)delta);
            Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(1f - (_age - Life * 0.5f) / (Life * 0.5f), 0f, 1f));
            if (_age >= Life) { _alive--; QueueFree(); }
        }

        public override void _ExitTree() { if (_age < Life) { _age = Life; _alive--; } }
    }
}
