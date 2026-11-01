// 程式生成的簡單圖形（圓、方塊），給子彈、光環、經驗寶石用。
// 不做成圖片資產，省得多一堆檔案要管。
using RMZS.GodotPort;

namespace RMZS
{
    public static class SVShapes
    {
        static Sprite _circle;
        static Sprite _square;
        static Sprite _ring;
        static Sprite _vignette;
        static Sprite _soft;
        static Sprite _fogCircle;
        static Sprite _fogRect;

        public static Sprite Circle() => _circle != null ? _circle : (_circle = Make(32, Kind.Disc));
        public static Sprite Square() => _square != null ? _square : (_square = Make(32, Kind.Box));
        public static Sprite Ring() => _ring != null ? _ring : (_ring = Make(128, Kind.Ring));

        /// <summary>
        /// 場地外的黑霧：場地內（含邊界）全透明，邊界外實心黑——讓玩家一眼看出
        /// 「這裡走不過去」，不用自己去試撞邊界在哪。圓形場地用這張。
        /// 貼圖比場地大上 FogSpan 倍，四個方向都蓋得到攝影機看得到的範圍，
        /// 不會只有斜角蓋到、直角露餡。套用時記得跟 Ring() 一樣照場地半徑縮放。
        /// </summary>
        public static Sprite FogCircle() => _fogCircle != null ? _fogCircle : (_fogCircle = MakeFog(false));

        /// <summary>同上，但邊界是矩形（兩關制場地用）。</summary>
        public static Sprite FogRect() => _fogRect != null ? _fogRect : (_fogRect = MakeFog(true));

        /// <summary>貼圖半徑相對場地半徑的倍數，套用時 localScale 要乘這個數字。</summary>
        public const float FogSpan = 3f;

        /// <summary>受傷時的血色暈染：中間全透明，越靠邊越濃。</summary>
        public static Sprite Vignette() =>
            _vignette != null ? _vignette : (_vignette = Make(128, Kind.Vignette));

        /// <summary>
        /// 柔邊光暈：中心實心、往外平滑收到透明。
        /// 拿來墊在圖示底下，效果等同「圖示後面有一層高斯模糊的底」，
        /// 但不用真的做全螢幕模糊（那在 URP 2D 要另外開 render pass，成本高很多）。
        /// </summary>
        public static Sprite Soft() => _soft != null ? _soft : (_soft = Make(128, Kind.Soft));

        // ---------------- 圓角面板（UI 用）----------------
        //
        // 跟本篇的 RMZWindowSkin 是同一套做法，但這邊刻意重寫一份 ——
        // 小遊戲不引用 RMZ 的任何東西（單向依賴），這樣它自己按 Play 也跑得起來。

        static readonly System.Collections.Generic.Dictionary<int, Sprite> Rounded =
            new System.Collections.Generic.Dictionary<int, Sprite>();
        static readonly System.Collections.Generic.Dictionary<int, Sprite> Rings =
            new System.Collections.Generic.Dictionary<int, Sprite>();

        /// <summary>實心圓角矩形的 9-slice 圖。</summary>
        public static Sprite RoundedBox(int radius)
        {
            radius = Mathf.Max(1, radius);
            if (Rounded.TryGetValue(radius, out var c) && c != null) return c;
            var sp = BakeRounded($"SVRound{radius}", radius, 0);
            Rounded[radius] = sp;
            return sp;
        }

        /// <summary>空心圓角外框。中間透空，所以底色的透明度是真的會透。</summary>
        public static Sprite RoundedRing(int radius, int thickness)
        {
            radius = Mathf.Max(2, radius);
            thickness = Mathf.Clamp(thickness, 1, radius - 1);

            int key = radius * 100 + thickness;
            if (Rings.TryGetValue(key, out var c) && c != null) return c;
            var sp = BakeRounded($"SVRoundRing{radius}_{thickness}", radius, thickness);
            Rings[key] = sp;
            return sp;
        }

        /// <summary>把一張 Image 換成圓角。</summary>
        public static void ApplyRounded(RMZS.GodotPort.UI.Image img, int radius)
        {
            if (img == null) return;
            img.sprite = RoundedBox(radius);
            img.type = RMZS.GodotPort.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
        }

        public static void ApplyRoundedRing(RMZS.GodotPort.UI.Image img, int radius, int thickness)
        {
            if (img == null) return;
            img.sprite = RoundedRing(radius, thickness);
            img.type = RMZS.GodotPort.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
        }

        /// <summary>thickness = 0 是實心，大於 0 就只留那一圈。</summary>
        static Sprite BakeRounded(string name, int radius, int thickness)
        {
            int n = radius * 2 + 2;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
            };

            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float a = RoundedCoverage(x, y, n, radius, thickness);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }

            tex.SetPixels32(px);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f),
                                       100f, 0, SpriteMeshType.FullRect,
                                       new Vector4(radius, radius, radius, radius));
            sprite.name = name;
            return sprite;
        }

        /// <summary>3x3 超取樣，圓角在大面板上才不會有階梯。</summary>
        static float RoundedCoverage(float px, float py, int n, int radius, float thickness)
        {
            const int S = 3;
            int hit = 0;

            for (int sy = 0; sy < S; sy++)
                for (int sx = 0; sx < S; sx++)
                {
                    float x = px + (sx + 0.5f) / S, y = py + (sy + 0.5f) / S;
                    bool inside = InsideRounded(x, y, n, radius, 0f);
                    if (thickness > 0f && inside && InsideRounded(x, y, n, radius, thickness)) inside = false;
                    if (inside) hit++;
                }

            return hit / (float)(S * S);
        }

        static bool InsideRounded(float x, float y, int n, int radius, float inset)
        {
            float lo = inset, hi = n - inset;
            if (x < lo || x > hi || y < lo || y > hi) return false;

            float r = radius - inset;
            if (r <= 0f) return true;

            float cx = Mathf.Clamp(x, lo + r, hi - r);
            float cy = Mathf.Clamp(y, lo + r, hi - r);
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        enum Kind { Disc, Box, Ring, Vignette, Soft }

        /// <summary>rect=false 是圓形黑霧、rect=true 是矩形黑霧，見 FogCircle()/FogRect()。</summary>
        static Sprite MakeFog(bool rect)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            // d = 1 對應場地邊界（跟 Ring() 用同一個換算慣例：套用時 localScale = 場地直徑）。
            // 貼圖整體卻蓋到 FogSpan 倍半徑，所以邊界只落在貼圖的 1/FogSpan 處，
            // 從那裡開始起霧，起霧到邊界外一點點（+8%）就全黑，霧牆不會太厚也不會太銳利。
            float holeStart = 1f / FogSpan;
            float holeEnd = holeStart * 1.08f;

            float c = (size - 1) * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x - c) / c, ny = (y - c) / c;
                float d = rect ? Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny))
                               : Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01((d - holeStart) / (holeEnd - holeStart));
                px[y * size + x] = new Color(0f, 0f, 0f, a);
            }

            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size,
                                 0, SpriteMeshType.FullRect);
        }

        static Sprite Make(int size, Kind kind)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float c = (size - 1) * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                float a = kind switch
                {
                    Kind.Box  => 1f,
                    Kind.Disc => Mathf.Clamp01((1f - d) * 6f),

                    // smoothstep 的平方，邊緣收得比線性漸層更軟，看起來就像糊開的一團
                    Kind.Soft => Mathf.Pow(Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d)), 2f),

                    // 中心 45% 完全透明，往外用平方曲線收，邊緣不會有一圈生硬的界線
                    Kind.Vignette => Mathf.Pow(Mathf.Clamp01((d - 0.45f) / 0.75f), 2f),
                    _         => Mathf.Clamp01((1f - Mathf.Abs(d - 0.88f) * 12f)),
                };
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }

            tex.SetPixels(px);
            tex.Apply(false, true);
            // FullRect：這些圖是漸層 alpha，用 Tight 網格邊緣會被切掉
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size,
                                 0, SpriteMeshType.FullRect);
        }
    }
}
