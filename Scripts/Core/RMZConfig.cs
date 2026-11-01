// 全域常數與路徑設定。
// Unity 版：1 圖塊 = 1 Unity 單位、PPU 48、Y 軸向上。
// Godot 版：1 圖塊 = 48 像素、Y 軸向下 —— 剛好跟 RM 的座標系一致，不用翻轉 Y。
using System.IO;
using Godot;

namespace RMZ
{
    public static class RMZConfig
    {
        public const int TileWidth = 48;
        public const int TileHeight = 48;

        /// <summary>非物件角色（檔名沒有 "!"）向上位移 6px，跟 MZ 一致。</summary>
        public const int CharacterShiftY = 6;

        /// <summary>MZ 的圖塊動畫：每 30 幀（0.5 秒）換一格。</summary>
        public const float AnimationInterval = 30f / 60f;

        /// <summary>
        /// 像素對齊。MZ 會把每個精靈的螢幕座標四捨五入到整數像素，畫面才會銳利。
        /// Godot 2D 本來就是像素座標，所以這裡只要 Round 就好。
        /// </summary>
        public static bool PixelSnap = true;

        public static Vector2 Snap(Vector2 v) =>
            PixelSnap ? new Vector2(Mathf.Round(v.X), Mathf.Round(v.Y)) : v;

        // ---------------- 素材路徑 ----------------
        //
        // RM 素材（data / img / audio）不進 Godot 的匯入流程：
        // rmz/ 底下放了一顆 .gdignore，編輯器就會整個跳過那個資料夾，
        // 開專案不用等 280MB 圖檔匯入，RM 那邊更新素材也不用重匯。
        // 執行期改用 FileAccess / Image.LoadFromFile 直讀。

        static string _root;

        /// <summary>
        /// 素材根目錄（絕對路徑）。
        /// 編輯器裡是專案資料夾底下的 rmz/；匯出後是執行檔旁邊的 rmz/。
        /// </summary>
        public static string RootPath
        {
            get
            {
                if (_root != null) return _root;

                string inProject = ProjectSettings.GlobalizePath("res://rmz");
                string nextToExe = Path.Combine(
                    Path.GetDirectoryName(OS.GetExecutablePath()) ?? ".", "rmz");

                _root = Directory.Exists(inProject) ? inProject : nextToExe;
                return _root;
            }
        }

        public static string DataPath => Path.Combine(RootPath, "data");
        public static string ImgPath => Path.Combine(RootPath, "img");
        public static string TilesetImgPath => Path.Combine(ImgPath, "tilesets");
        public static string CharacterImgPath => Path.Combine(ImgPath, "characters");
        public static string ParallaxImgPath => Path.Combine(ImgPath, "parallaxes");
        public static string PictureImgPath => Path.Combine(ImgPath, "pictures");
        public static string SystemImgPath => Path.Combine(ImgPath, "system");
        public static string FaceImgPath => Path.Combine(ImgPath, "faces");
        public static string AudioPath => Path.Combine(RootPath, "audio");
    }
}
