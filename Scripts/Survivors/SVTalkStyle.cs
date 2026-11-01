// 小遊戲對話框的配色。
//
// 本篇（RMZ）那邊的對話框是「依說話者換底色」的：鈴音深藍、其他人深藍底混一點角色色，
// 而且底色是半透明的，看得到後面的場景與立繪。小遊戲這邊原本自己寫死一組顏色，
// 所以同一個角色在兩邊長得不一樣。
//
// 這支只放「小遊戲要什麼」，實際顏色由本篇在 RMZSurvivorsBridge 裡掛上來 ——
// RMZS 不能反過來引用 RMZ，所以用委派。沒掛的話就用下面這組預設值，
// 單獨跑倖存者場景也不會變成透明一片。
using System;
using RMZS.GodotPort;

namespace RMZS
{
    public struct SVTalkColors
    {
        public Color Fill;       // 對話框底色（含透明度）
        public Color Frame;      // 內圈框線
        public Color FrameDark;  // 最外圈那條暗線
        public Color NameText;   // 名字牌上的字
    }

    public static class SVTalkStyle
    {
        /// <summary>本篇掛上來的：給說話者名字，回傳這個人的對話框配色。</summary>
        public static Func<string, SVTalkColors> Resolve;

        public static SVTalkColors Default => new SVTalkColors
        {
            Fill = new Color(0.05f, 0.06f, 0.11f, 0.62f),
            Frame = new Color(0.79f, 0.66f, 0.36f, 1f),
            FrameDark = new Color(0.15f, 0.12f, 0.08f, 1f),
            NameText = new Color(1f, 0.90f, 0.60f, 1f),
        };

        public static SVTalkColors For(string speaker)
        {
            if (Resolve == null) return Default;
            try { return Resolve(speaker); }
            catch { return Default; }
        }

        public static void Clear() => Resolve = null;
    }
}
