// 武器專屬的強化。
//
// 拿劍跟拿法杖在升級三選一裡看到的東西不一樣：
//   劍（物理）—— 迴避、暴擊、流血。都是「近身互毆」的詞。
//   法杖（魔法）—— 冰、風、毒、火的附魔。命中之後各自留下不同的後續。
// 兩邊互斥，法師抽不到暴擊、劍士也抽不到附魔，
// 這樣「換一把武器」才真的等於換一套玩法，而不只是換個數字。
//
// 這裡只存數值，效果在 SVPlayer（開槍時填進彈道、受傷時判迴避）那邊生效。
using RMZS.GodotPort;

namespace RMZS
{
    public class SVWeaponAffix
    {
        // ---------------- 劍：物理專屬 ----------------

        /// <summary>迴避率。判定在防禦之前，躲掉就是完全不痛。</summary>
        public float Dodge;

        /// <summary>暴擊率與倍率。祝福那邊的暴擊會跟這裡相加。</summary>
        public float Crit;
        public float CritMultiplier = SVCrit.DefaultMultiplier;

        /// <summary>流血：命中後的持續傷害，不看防禦。</summary>
        public float BleedChance;
        public int BleedPower;
        public float BleedSeconds = 4f;

        // ---------------- 法杖：魔法附魔 ----------------
        // 每一層都疊上去，四種可以同時帶

        /// <summary>冰：命中後減速，層數夠了開始有機率直接凍住。</summary>
        public int Frost;

        /// <summary>風：彈道更快、射程更遠，還會多一發。</summary>
        public int Gale;

        /// <summary>毒：命中後的持續傷害，比火弱但久很多。</summary>
        public int Venom;

        /// <summary>火：命中點的小爆炸。</summary>
        public int Flame;

        public bool HasAnyEnchant => Frost > 0 || Gale > 0 || Venom > 0 || Flame > 0;

        // ---- 換算 ----

        public float FrostFactor => Frost <= 0 ? 0f : Mathf.Max(0.35f, 1f - 0.15f * Frost);
        public float FrostSeconds => Frost <= 0 ? 0f : 1.2f + 0.25f * Frost;

        /// <summary>冰凍要第二層才開始有，之後每層 +7%。</summary>
        public float FreezeChance => Frost <= 1 ? 0f : Mathf.Min(0.35f, 0.07f * (Frost - 1));

        public float GaleSpeedMul => 1f + 0.12f * Gale;
        public float GaleRangeBonus => 1.2f * Gale;

        /// <summary>風：多打一發的機率。滿層也不會變成必定，不然畫面會爆掉。</summary>
        public float GaleExtraShotChance => Mathf.Min(0.6f, 0.15f * Gale);

        public int VenomPower => Venom <= 0 ? 0 : 2 + 2 * Venom;
        public float VenomSeconds => Venom <= 0 ? 0f : 4f + 1f * Venom;

        public int FlameDamage => Flame <= 0 ? 0 : 6 + 5 * Flame;
        public float FlameRadius => Flame <= 0 ? 0f : 1.1f + 0.2f * Flame;

        /// <summary>附魔的染色，讓玩家看得出這一發帶了什麼。多重附魔取最後拿到的那個。</summary>
        public Color Tint = Color.white;

        /// <summary>
        /// 「破綻」下一次加多少暴擊率：第一次 25%，之後 10~15% 隨機（整數 %）。
        /// 先抽好存著，選單上顯示的數字才會跟實際加的一樣。
        /// </summary>
        float _nextCritStep = -1f;
        public float NextCritStep
        {
            get
            {
                if (Crit <= 0f) return 0.25f;
                if (_nextCritStep < 0f) _nextCritStep = Mathf.RoundToInt(Random.Range(0.10f, 0.15f) * 100f) / 100f;
                return _nextCritStep;
            }
        }
        public void TakeCritStep() { Crit = Mathf.Min(0.75f, Crit + NextCritStep); _nextCritStep = -1f; }

        public void Clear()
        {
            _nextCritStep = -1f;
            Dodge = Crit = BleedChance = 0f;
            CritMultiplier = SVCrit.DefaultMultiplier;
            BleedPower = 0;
            BleedSeconds = 4f;
            Frost = Gale = Venom = Flame = 0;
            Tint = Color.white;
        }
    }
}
