// 傷害分成物理與魔法兩條線。
//
// 為什麼要分：武器有劍跟法杖兩種，裝備也分物攻／魔攻與物防／魔防。
// 不分家的話「拿法杖」就只是換一張圖，玩家沒有理由去選。
//
// 減傷用 LoL 那套除法：實際傷害 = 傷害 × K /(K + 防禦)。
//
// 之前是「先扣固定值，最低留一成五」的減法，問題是兩頭都失控：
// 攻擊力一旦超過防禦，多加的每一點都是一比一進去，後期滾雪球滾到誇張；
// 反過來小數字打高防又永遠只有地板值（雙子刃打王只扣 1 就是這樣來的）。
// 除法沒有這兩個問題 —— 防禦是「每一點都讓你多活一點」的邊際遞減，
// 攻擊力也永遠是等比例放大，數值再大也不會爆炸。
using RMZS.GodotPort;

namespace RMZS
{
    public enum SVDamageKind
    {
        Physical,   // 物理：前、斬擊、撞擊
        Magic,      // 魔法：法杖、雷、隕石、元素
        True,       // 真傷：灼燒、毒、場地傷害，不看防禦
    }

    public static class SVDamageMath
    {
        /// <summary>
        /// 防禦的換算常數。防禦等於這個值時，傷害剛好減半。
        /// 調小 = 防禦更有用，調大 = 防禦更沒用。
        /// </summary>
        public const float DefenseConstant = 100f;

        /// <summary>減傷後實際會進去的傷害倍率（0~1，負防禦會 &gt;1）。</summary>
        public static float Multiplier(int defense)
        {
            if (defense >= 0) return DefenseConstant / (DefenseConstant + defense);

            // 負防禦（被穿甲穿到負的）反過來變成增傷，跟 LoL 一樣
            return 2f - DefenseConstant / (DefenseConstant - defense);
        }

        /// <summary>扣掉防禦之後實際會進去的傷害。至少留 1，不然高防等於免疫。</summary>
        public static int Apply(int amount, int defense)
        {
            if (amount <= 0) return 0;
            if (defense == 0) return amount;

            return Mathf.Max(1, Mathf.RoundToInt(amount * Multiplier(defense)));
        }

        public static string NameOf(SVDamageKind kind) => kind switch
        {
            SVDamageKind.Magic => "魔法",
            SVDamageKind.True => "真傷",
            _ => "物理",
        };

        /// <summary>技能圖示與傷害數字用的顏色，物理偏橘、魔法偏藍。</summary>
        public static Color ColorOf(SVDamageKind kind) => kind switch
        {
            SVDamageKind.Magic => new Color(0.55f, 0.75f, 1f),
            SVDamageKind.True => new Color(1f, 0.85f, 0.5f),
            _ => new Color(1f, 0.72f, 0.5f),
        };
    }
}
