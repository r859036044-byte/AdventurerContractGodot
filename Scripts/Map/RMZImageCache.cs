// 執行期從 rmz/img 載入 PNG。
// 這些圖不進 Godot 的匯入流程（rmz/ 底下有 .gdignore），
// 所以用 Image.LoadFromFile 直讀，RM 那邊改圖不用重匯。
using System.Collections.Generic;
using System.IO;
using Godot;

namespace RMZ
{
    public static class RMZImageCache
    {
        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        static string ResolvePath(string folder, string name)
        {
            string path = Path.Combine(folder, name + ".png");
            // 人物介紹使用可縮放的原創公會檔案框，保留 RM 原 PNG 作為備援。
            if (folder == RMZConfig.PictureImgPath && name == "UI button/introduce back") {
                string frame = Path.Combine(folder, name + ".svg");
                if (File.Exists(frame)) return frame;
            }
            // 移植素材把百分號存成底線，原 RM 事件仍使用 100%／50%／20%。
            if(!File.Exists(path) && name.Contains("%")) {
                string migrated = Path.Combine(folder, name.Replace("%", "_") + ".png");
                if(File.Exists(migrated)) return migrated;
            }
            return path;
        }

        public static Texture2D Load(string folder, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            string path = ResolvePath(folder, name);
            if (Textures.TryGetValue(path, out var cached)) return cached;

            if (!File.Exists(path))
            {
                GD.PushWarning($"[RMZ] 找不到圖片：{path}");
                Textures[path] = null;
                return null;
            }

            var img = LoadImage(path);
            if (img == null)
            {
                GD.PushWarning($"[RMZ] 圖片解碼失敗：{path}");
                Textures[path] = null;
                return null;
            }

            // 舊 RM 狀態框只有三列；沿用原圖拼接第四列，讓舊存檔也能顯示經驗欄。
            if (folder == RMZConfig.PictureImgPath && name == "UI button/player status")
                img = ExtendPlayerStatus(img);

            var tex = ImageTexture.CreateFromImage(img);
            tex.ResourceName = name;
            Textures[path] = tex;
            return tex;
        }

        static Image ExtendPlayerStatus(Image source)
        {
            // 只處理原版 365 × 166 圖；作者日後自行換成四列底圖時不再延伸。
            if (source.GetWidth() != 365 || source.GetHeight() != 166) return source;
            const int rowHeight = 42, bottom = 146;
            var result = Image.CreateEmpty(365, 208, false, source.GetFormat());
            result.BlitRect(source, new Rect2I(0, 0, 365, bottom), Vector2I.Zero);
            result.BlitRect(source, new Rect2I(0, 104, 365, rowHeight), new Vector2I(0, bottom));
            result.BlitRect(source, new Rect2I(0, bottom, 365, 20), new Vector2I(0, bottom + rowHeight));
            // 經驗沿用等級欄的金邊與明暗，只把綠色內側轉成藍色。
            result.BlitRect(source, new Rect2I(154, 20, 200, 36), new Vector2I(154, 146));
            for (int y = 146; y < 182; y++)
            for (int x = 154; x < 354; x++)
            {
                var c = result.GetPixel(x, y);
                if (c.G > c.R * 1.1f && c.G > c.B * 1.2f)
                    result.SetPixel(x, y, new Color(c.B, c.R, c.G, c.A));
            }
            return result;
        }

        static Image LoadImage(string path)
        {
            if (!path.EndsWith(".svg", System.StringComparison.OrdinalIgnoreCase))
                return Image.LoadFromFile(path);
            var image = new Image();
            return image.LoadSvgFromString(File.ReadAllText(path)) == Error.Ok ? image : null;
        }

        public static bool IsLoaded(string folder, string name) =>
            Textures.ContainsKey(ResolvePath(folder, name));

        public static Texture2D LoadTileset(string name) => Load(RMZConfig.TilesetImgPath, name);
        public static Texture2D LoadCharacter(string name) => Load(RMZConfig.CharacterImgPath, name);
        public static Texture2D LoadParallax(string name) => Load(RMZConfig.ParallaxImgPath, name);
        public static Texture2D LoadPicture(string name) => Load(RMZConfig.PictureImgPath, name);
        public static Texture2D LoadSystem(string name) => Load(RMZConfig.SystemImgPath, name);
        public static Texture2D LoadFace(string name) => Load(RMZConfig.FaceImgPath, name);

        // ---------------- 透明遮罩（給圖片按鈕做精準命中判定用）----------------
        //
        // 判斷「游標是不是壓在真的有畫東西的地方」。
        // 抽成一張低解析度的 bool 遮罩存起來 —— 只有被當成按鈕的圖才會做，一張只做一次。

        const int MaskSize = 128;

        static readonly Dictionary<string, bool[]> Masks = new Dictionary<string, bool[]>();

        /// <summary>
        /// (u,v) 是圖片內的相對座標，左上角 (0,0)、右下角 (1,1)。
        /// 讀不到遮罩時一律回傳 true —— 寧可多判到，也不要讓按鈕整個點不動。
        /// </summary>
        public static bool IsOpaqueAt(string folder, string name, float u, float v)
        {
            if (string.IsNullOrEmpty(name)) return true;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return false;

            var mask = MaskFor(folder, name);
            if (mask == null) return true;

            int x = Mathf.Clamp((int)(u * MaskSize), 0, MaskSize - 1);
            int y = Mathf.Clamp((int)(v * MaskSize), 0, MaskSize - 1);
            return mask[y * MaskSize + x];
        }

        static bool[] MaskFor(string folder, string name)
        {
            string path = ResolvePath(folder, name);
            if (Masks.TryGetValue(path, out var cached)) return cached;

            bool[] mask = null;
            if (File.Exists(path))
            {
                var img = Image.LoadFromFile(path);
                if (img != null && img.GetWidth() > 0 && img.GetHeight() > 0)
                {
                    int w = img.GetWidth(), h = img.GetHeight();
                    mask = new bool[MaskSize * MaskSize];
                    for (int y = 0; y < MaskSize; y++)
                    for (int x = 0; x < MaskSize; x++)
                    {
                        // Godot 的 Image 原點就在左上角，跟遮罩一致，不用翻 v
                        int px = Mathf.Clamp((int)((x + 0.5f) / MaskSize * w), 0, w - 1);
                        int py = Mathf.Clamp((int)((y + 0.5f) / MaskSize * h), 0, h - 1);
                        mask[y * MaskSize + x] = img.GetPixel(px, py).A > 0.15f;
                    }
                }
            }

            Masks[path] = mask;
            return mask;
        }

        public static void Clear()
        {
            Textures.Clear();
            Masks.Clear();
        }
    }
}
