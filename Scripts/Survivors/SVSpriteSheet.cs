// RM 行走圖的載入與切格，獨立於 RMZ 那份實作（故意不共用，避免兩邊互相牽動）。
//
// MZ 的規則：
//   檔名含 "$" -> 單人圖，3 格 x 4 向
//   檔名含 "!" -> 物件圖（這裡只影響是否上移，小遊戲用不到）
//   其餘       -> 8 人圖，12 格 x 8 列，一個角色佔 3 格 x 4 列
using System.Collections.Generic;
using System.IO;
using RMZS.GodotPort;

namespace RMZS
{
    public static class SVSpriteSheet
    {
        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        public static Texture2D Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Textures.TryGetValue(name, out var cached)) return cached;

            string path = Path.Combine(SVConfig.CharacterImgPath, name + ".png");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[倖存者] 找不到行走圖：{path}");
                Textures[name] = null;
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.LoadImage(File.ReadAllBytes(path), true);
            tex.name = name;
            Textures[name] = tex;
            return tex;
        }

        public static bool IsSingle(string name) => !string.IsNullOrEmpty(name) && name.Contains("$");

        /// <summary>一格的寬高。</summary>
        public static Vector2Int FrameSize(string name)
        {
            var tex = Load(name);
            if (tex == null) return new Vector2Int(48, 48);
            return IsSingle(name)
                ? new Vector2Int(tex.width / 3, tex.height / 4)
                : new Vector2Int(tex.width / 12, tex.height / 8);
        }

        /// <summary>
        /// 取一格圖。index 是 8 人圖裡的第幾個人（0-7，單人圖忽略），
        /// dir 用 MZ 的數字鍵方向（2 下 / 4 左 / 6 右 / 8 上），pattern 0-2。
        /// </summary>
        public static Sprite Get(string name, int index, int dir, int pattern)
        {
            var tex = Load(name);
            if (tex == null) return null;

            var size = FrameSize(name);
            bool single = IsSingle(name);

            int blockX = single ? 0 : (index % 4) * 3;
            int blockY = single ? 0 : (index / 4) * 4;

            int sx = (blockX + Mathf.Clamp(pattern, 0, 2)) * size.x;
            int sy = (blockY + Mathf.Clamp((dir - 2) / 2, 0, 3)) * size.y;

            string key = $"{name}|{sx}|{sy}|{size.x}|{size.y}";
            if (Sprites.TryGetValue(key, out var cachedSprite) && cachedSprite != null) return cachedSprite;

            // Unity 的貼圖原點在左下，RM 的在左上，所以 y 要翻過來
            var rect = new Rect(sx, tex.height - sy - size.y, size.x, size.y);
            if (rect.xMax > tex.width || rect.y < 0) return null;

            var sprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.15f), SVConfig.PixelsPerUnit,
                                       0, SpriteMeshType.FullRect);
            sprite.name = key;
            Sprites[key] = sprite;
            return sprite;
        }

        public static void Clear()
        {
            Sprites.Clear();
            foreach (var t in Textures.Values) if (t != null) Object.Destroy(t);
            Textures.Clear();
        }
    }
}
