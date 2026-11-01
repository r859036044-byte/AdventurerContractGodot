// 圓形頭像：從 RM 行走圖切出頭部，套上圓形遮罩與外框，當成角色的標識。
//
// SVSpriteSheet 載進來的貼圖是 markNonReadable（省記憶體），取不到像素，
// 所以這裡自己重讀一次 PNG，切完就把暫存那份丟掉。一個角色只做一次。
using System.Collections.Generic;
using System.IO;
using RMZS.GodotPort;

namespace RMZS
{
    public static class SVPortrait
    {
        /// <summary>輸出的頭像邊長（像素）。</summary>
        public const int Size = 72;

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>
        /// 取某個角色的圓形頭像。ringColor 是外框顏色，用來分辨是誰。
        /// 取不到圖時回傳 null，呼叫端要能接受（畫成純色圓圈就好）。
        /// </summary>
        public static Sprite Get(string sheetName, int sheetIndex, Color ringColor)
        {
            if (string.IsNullOrEmpty(sheetName)) return null;

            string key = $"{sheetName}|{sheetIndex}|{ColorUtility.ToHtmlStringRGB(ringColor)}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var sprite = Build(sheetName, sheetIndex, ringColor);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 有些行走圖把角色畫得比別張低，照同一個比例裁出來臉就會沉在圓框下緣。
        /// 這裡按圖檔名補一個偏移：值越大，裁切框往下移，臉在頭像裡就越往上。
        /// </summary>
        static float FaceOffsetFor(string sheetName)
        {
            if (string.IsNullOrEmpty(sheetName)) return 0f;

            // 小人3：雪瑤與雪霜用的那張，人物位置比其他張低
            if (sheetName.Contains("小人3")) return 0.06f;

            return 0f;
        }

        static Sprite Build(string sheetName, int sheetIndex, Color ringColor)
        {
            string path = Path.Combine(SVConfig.CharacterImgPath, sheetName + ".png");
            if (!File.Exists(path)) return null;

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(path)))
            {
                Object.Destroy(source);
                return null;
            }

            bool single = sheetName.Contains("$");
            int frameW = single ? source.width / 3 : source.width / 12;
            int frameH = single ? source.height / 4 : source.height / 8;

            // 朝下、站姿那一格（pattern 1、dir 2）最適合當頭像
            int blockX = single ? 0 : (sheetIndex % 4) * 3;
            int blockY = single ? 0 : (sheetIndex / 4) * 4;
            int frameX = (blockX + 1) * frameW;
            int frameY = blockY * frameH;

            // 頭部大約落在一格的上半、水平置中。切成正方形才好套圓形遮罩
            // （有些圖把人畫得比較低，見 FaceOffsetFor）
            int crop = Mathf.Max(4, Mathf.RoundToInt(frameW * 0.68f));
            int cropX = frameX + (frameW - crop) / 2;
            int cropY = frameY + Mathf.RoundToInt(frameH * (0.04f + FaceOffsetFor(sheetName)));

            var pixels = new Color[Size * Size];
            float center = (Size - 1) * 0.5f;
            float outer = center;
            float inner = center - Mathf.Max(2f, Size * 0.075f);

            var backdrop = new Color(0.10f, 0.11f, 0.15f, 0.92f);

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));

                if (d > outer) { pixels[y * Size + x] = Color.clear; continue; }

                if (d > inner)
                {
                    // 外框。邊緣做一點淡出，不然圓圈會有鋸齒
                    float a = Mathf.Clamp01((outer - d) / 1.5f);
                    pixels[y * Size + x] = new Color(ringColor.r, ringColor.g, ringColor.b, a);
                    continue;
                }

                // 圓圈內：先鋪底色，再把頭部疊上去
                int sx = cropX + Mathf.Clamp(Mathf.RoundToInt(x * crop / (float)Size), 0, crop - 1);

                // 這裡要翻兩次，少翻一次頭就是倒的：
                //   1. SetPixels 的 y=0 是「輸出圖的最下面」，
                //      但 cropY 是照 RM 的左上原點算的，所以要從裁切框的底部往上數。
                //   2. Unity 貼圖本身也是左下為原點，取樣時再翻回來。
                int fromBottom = Mathf.Clamp(Mathf.RoundToInt(y * crop / (float)Size), 0, crop - 1);
                int syTop = cropY + (crop - 1 - fromBottom);
                int sy = source.height - 1 - syTop;

                var head = (sx >= 0 && sx < source.width && sy >= 0 && sy < source.height)
                    ? source.GetPixel(sx, sy) : Color.clear;

                var c = Color.Lerp(backdrop, head, head.a);
                c.a = 1f;
                pixels[y * Size + x] = c;
            }

            Object.Destroy(source);

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "portrait_" + sheetName + sheetIndex,
            };
            tex.SetPixels(pixels);
            tex.Apply(false, true);

            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size,
                                 0, SpriteMeshType.FullRect);
        }

        /// <summary>照名字給一個固定的外框顏色，同一個人每次都一樣。</summary>
        public static Color RingColorFor(string name)
        {
            if (string.IsNullOrEmpty(name)) return new Color(0.85f, 0.72f, 0.38f);

            int hash = 0;
            foreach (char c in name) hash = hash * 31 + c;

            float hue = Mathf.Abs(hash % 360) / 360f;
            return Color.HSVToRGB(hue, 0.45f, 1f);
        }

        public static void Clear() => Cache.Clear();
    }
}
