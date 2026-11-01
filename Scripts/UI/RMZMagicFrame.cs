// 鈴音專用的「魔力外框」：藍紫色框＋外發光（一脹一縮）＋周圍飄亮晶晶的魔力粒子。
// 戰後互動視窗、劇情對話框、冒險對話都用這一套，看起來才一致。
using Godot;

namespace RMZ
{
    public static class RMZMagicFrame
    {
        public static readonly Color Border = new(0.60f, 0.50f, 1.00f);
        public static readonly Color Glow = new(0.55f, 0.40f, 1.00f, 0.55f);
        public static readonly Color NameFill = new(0.14f, 0.10f, 0.32f, 0.92f);
        public static readonly Color Speaker = new(0.80f, 0.74f, 1f);

        /// <summary>說話的人是不是鈴音。</summary>
        public static bool IsSuzune(params string[] names)
        {
            foreach (var n in names) if (!string.IsNullOrEmpty(n) && n.Contains("鈴音")) return true;
            return false;
        }

        /// <summary>藍紫框＋外發光。要會呼吸就另外呼叫 Pulse（或自己每幀設 ShadowSize）。</summary>
        public static StyleBoxFlat Style(Color fill, int radius = 12, int borderWidth = 2)
        {
            return new StyleBoxFlat
            {
                BgColor = fill, BorderColor = Border,
                BorderWidthTop = borderWidth, BorderWidthBottom = borderWidth, BorderWidthLeft = borderWidth, BorderWidthRight = borderWidth,
                CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
                ShadowColor = Glow, ShadowSize = 10,
            };
        }

        /// <summary>讓外發光一脹一縮（tween 掛在 owner 上，owner 消失就停）。</summary>
        public static void Pulse(Node owner, StyleBoxFlat sb)
        {
            var tw = owner.CreateTween().SetLoops();
            tw.TweenProperty(sb, "shadow_size", 18, 1.2f).SetTrans(Tween.TransitionType.Sine);
            tw.TweenProperty(sb, "shadow_size", 8, 1.2f).SetTrans(Tween.TransitionType.Sine);
        }

        /// <summary>依時間算外發光大小（不用 tween 的地方每幀呼叫）。</summary>
        public static int GlowSize() => 13 + Mathf.RoundToInt(5f * Mathf.Sin(Time.GetTicksMsec() / 1000f * Mathf.Pi / 1.2f));

        static Texture2D _spark;

        /// <summary>四角星的小亮點（程式畫的，不用另外出圖）。</summary>
        public static Texture2D SparkTexture()
        {
            if (_spark != null) return _spark;
            const int n = 15; var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs(x - c), dy = Mathf.Abs(y - c);
                float star = Mathf.Max(Mathf.Clamp(1f - dx / 1.2f, 0, 1) * Mathf.Clamp(1f - dy / c, 0, 1), Mathf.Clamp(1f - dy / 1.2f, 0, 1) * Mathf.Clamp(1f - dx / c, 0, 1));
                float core = Mathf.Clamp(1f - Mathf.Sqrt(dx * dx + dy * dy) / 2.5f, 0, 1);
                img.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp(Mathf.Max(star, core), 0, 1)));
            }
            return _spark = ImageTexture.CreateFromImage(img);
        }

        /// <summary>在一塊矩形上加魔力粒子：從框的範圍冒出亮點，慢慢往上飄、閃一下就消失。</summary>
        public static CpuParticles2D Sparkles(Control parent, Vector2 size, int amount)
        {
            var p = new CpuParticles2D
            {
                Texture = SparkTexture(), Amount = amount, Lifetime = 1.8f, Preprocess = 1.8f,
                EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
                Direction = new Vector2(0, -1), Spread = 40, Gravity = new Vector2(0, -14),
                InitialVelocityMin = 4, InitialVelocityMax = 18, ScaleAmountMin = 0.35f, ScaleAmountMax = 1.1f,
                ZIndex = 5,
            };
            var ramp = new Gradient();
            ramp.SetColor(0, new Color(1f, 1f, 1f, 0f));
            ramp.AddPoint(0.2f, new Color(0.95f, 0.90f, 1f, 1f));
            ramp.AddPoint(0.6f, new Color(0.62f, 0.55f, 1f, 0.8f));
            ramp.SetColor(ramp.GetPointCount() - 1, new Color(0.45f, 0.35f, 1f, 0f));
            p.ColorRamp = ramp;
            Fit(p, size);
            parent.AddChild(p);
            return p;
        }

        /// <summary>框的大小變了，粒子範圍跟著調。</summary>
        public static void Fit(CpuParticles2D p, Vector2 size)
        {
            if (p == null) return;
            p.EmissionRectExtents = size / 2f;
            p.Position = size / 2f;
        }
    }
}
