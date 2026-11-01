// 冒險（倖存者）戰鬥的畫質分級。設定畫面的「冒險畫質」選：高／中／低／極低。
//
// 後期會卡主要是「東西太多」：場上怪物、沒撿的能量球、大片半透明特效、跳字與光圈。
// 這裡只調「同時存在的數量」與「純裝飾的東西要不要畫」，不改數值平衡 ——
// 能量球超過上限時是併進舊的球，能量一點都不會少。
namespace RMZS
{
    public static class SVQuality
    {
        public enum Level { High = 0, Medium = 1, Low = 2, VeryLow = 3 }

        public static readonly string[] Names = { "高", "中", "低", "極低" };

        public static Level Current => (Level)System.Math.Clamp(RMZ.RMZOptions.SurvivorQuality, 0, 3);

        static int I => (int)Current;

        /// <summary>場上怪物上限（再多就先不刷）。</summary>
        public static int MaxEnemies => new[] { 400, 300, 220, 150 }[I];

        /// <summary>地上能量球上限。超過就把新的能量併進附近的舊球。</summary>
        public static int MaxGems => new[] { 800, 500, 300, 160 }[I];

        /// <summary>同時播放的技能特效上限。</summary>
        public static int MaxEffects => new[] { 36, 24, 12, 6 }[I];

        /// <summary>暴擊跳字。</summary>
        public static bool CritPopups => Current != Level.VeryLow;

        /// <summary>小型光圈（命中、撿東西那類，半徑 3 以下）。大型的（王、大招）一律照畫。</summary>
        public static bool MinorPulses => Current <= Level.Medium;

        /// <summary>雜魚頭上的血條。王的血條一律顯示。</summary>
        public static bool MobHpBars => Current != Level.VeryLow;
    }
}
