// 字型。遊戲原本用的是像素字型，System.json 裡指定的是
// fusion-pixel-12px-monospaced-kr-latin-400-normal.woff。
//
// Godot 讀不了 .woff，用的是同一套的 .ttf（Fonts/PixelFont.ttf，
// 從 Unity 專案的 Assets/Resources/RMZ/PixelFont.ttf 複製過來）。
//
// 像素字型有兩個關鍵設定，少一個就會糊：
//   1. 關掉字型平滑（Antialiasing = None、SubpixelPositioning = Disabled）
//   2. 字級要用原生大小 12px 的整數倍
using Godot;

namespace RMZ
{
    public static class RMZFonts
    {
        /// <summary>字型原生大小是 12px，用它的整數倍才不會糊。</summary>
        public const int NativeSize = 12;

        /// <summary>對話與選項用的字級（12 的兩倍）。</summary>
        public const int DefaultSize = 24;

        static Font _pixel;
        static Font _fallback;
        static bool _tried;

        /// <summary>像素字型。載不到時退回 NotoSansTC，至少中文不會變方框。</summary>
        public static Font Main
        {
            get
            {
                if (_tried) return _pixel ?? Fallback;
                _tried = true;

                var file = GD.Load<FontFile>("res://Fonts/PixelFont.ttf");
                if (file == null)
                {
                    GD.PushWarning("[RMZ] 找不到 Fonts/PixelFont.ttf，改用 NotoSansTC。");
                    return Fallback;
                }

                // 點陣字型不能做平滑，否則放大會糊掉
                file.Antialiasing = TextServer.FontAntialiasing.None;
                file.SubpixelPositioning = TextServer.SubpixelPositioning.Disabled;
                file.Hinting = TextServer.Hinting.None;
                file.MultichannelSignedDistanceField = false;

                _pixel = file;
                return _pixel;
            }
        }

        static Font Fallback
        {
            get
            {
                _fallback ??= GD.Load<Font>("res://Fonts/NotoSansTC-ExtraBold.ttf");
                return _fallback;
            }
        }

        /// <summary>把字級對齊到像素字型的整數倍，避免半格縮放造成模糊。</summary>
        public static int SnapSize(int size)
        {
            int multiple = Mathf.Max(1, Mathf.RoundToInt(size / (float)NativeSize));
            return multiple * NativeSize;
        }
    }
}
