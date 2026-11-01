// 視窗外框的配色與樣式，對應 Unity 版的 RMZWindowSkin。
// 用純程式生成，不需要額外的圖片資產：金框 + 外緣暗線 + 深藍底 + 圓角。
using Godot;

namespace RMZ
{
    public static class RMZWindowSkin
    {
        public static readonly Color Border = new Color(0.79f, 0.66f, 0.36f, 1f);      // 金
        public static readonly Color BorderDark = new Color(0.15f, 0.12f, 0.08f, 1f);  // 外緣暗線
        public static readonly Color Fill = new Color(0.05f, 0.06f, 0.11f, 0.94f);     // 深藍底
        public static readonly Color Text = new Color(0.94f, 0.93f, 0.88f, 1f);
        public static readonly Color TextSelected = new Color(1f, 0.86f, 0.42f, 1f);
        public static readonly Color TextDisabled = new Color(0.55f, 0.55f, 0.55f, 1f);
        public static readonly Color Highlight = new Color(0.79f, 0.66f, 0.36f, 0.18f);


        public static (Color Fill, Color Border, Color NameFill, Color Speaker) DialogueColors(string speakerName, string displayName, int background = 0)
        {
            var style = RMZDialogueStyle.Current;
            var entry = style.EntryFor(speakerName, displayName);
            var speaker = entry?.Color ?? style.ColorFor(speakerName, displayName);

            // 背景：0=視窗 1=暗色 2=透明

            Color fill;
            if (background == 1)
            {
                fill = new Color(0f, 0f, 0f, Mathf.Clamp(style.DimOpacity, 0f, 1f));
            }
            else
            {
                var baseFill = entry != null && entry.OverrideFill ? entry.Fill : RMZWindowSkin.Fill;

                // 底色混入角色色
                if (style.TintFill && (entry == null || !entry.OverrideFill))
                    baseFill = baseFill.Lerp(speaker, Mathf.Clamp(style.TintStrength, 0f, 1f));

                float opacity = entry != null && entry.OverrideFillOpacity
                    ? entry.FillOpacity
                    : style.FillOpacity;

                fill = new Color(baseFill.R, baseFill.G, baseFill.B, Mathf.Clamp(opacity, 0f, 1f));
            }

            var border = entry != null && entry.OverrideFrame ? entry.Frame
                       : style.TintBorder ? RMZWindowSkin.Border.Lerp(speaker, 0.6f)
                       : RMZWindowSkin.Border;

            var nameFill = style.TintNameBox
                ? new Color(fill.R, fill.G, fill.B, Mathf.Min(1f, fill.A + 0.3f))
                : new Color(RMZWindowSkin.Fill.R, RMZWindowSkin.Fill.G, RMZWindowSkin.Fill.B,
                            Mathf.Min(1f, fill.A + 0.3f));
            return (fill, border, nameFill, speaker);
        }

        /// <summary>對話框的圓角半徑。</summary>
        public const int DialogRadius = 12;

        public static StyleBoxFlat PanelStyle() => PanelStyle(Fill);

        public static StyleBoxFlat PanelStyle(Color fill) => PanelStyle(fill, Border);

        /// <summary>外框色可以另外指定（依說話者上色時會用到）。</summary>
        public static StyleBoxFlat PanelStyle(Color fill, Color border) => new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = border,
            BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusTopLeft = DialogRadius, CornerRadiusTopRight = DialogRadius,
            CornerRadiusBottomLeft = DialogRadius, CornerRadiusBottomRight = DialogRadius,

            // 不要用 StyleBoxFlat 的 Shadow 來畫「外緣暗線」：
            // Godot 的陰影是一整塊圓角矩形畫在方框「底下」，連方框自己那一塊也蓋住，
            // 底色調成半透明時透出來的就是那片不透明的陰影 —— 對話框永遠不會透。
            // 所以這裡不畫陰影；Unity 版那條最外圈的暗線暫時省略（金框已經夠明顯），
            // 之後真的需要再用第二層 Panel 疊出來。
        };

        /// <summary>選單列：選中時是金色淡底 + 金框。</summary>
        public static StyleBoxFlat RowStyle(bool selected) => new StyleBoxFlat
        {
            BgColor = selected ? Highlight : new Color(0f, 0f, 0f, 0f),
            BorderColor = selected ? Border : new Color(0f, 0f, 0f, 0f),
            BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
        };

        /// <summary>統一的文字樣式：像素字型 + 黑描邊（底是半透明的，不描邊會糊在地圖上）。</summary>
        public static void StyleLabel(Label label, int size, Color color)
        {
            label.AddThemeFontOverride("font", RMZFonts.Main);
            label.AddThemeFontSizeOverride("font_size", size);
            label.AddThemeColorOverride("font_color", color);
            label.AddThemeConstantOverride("outline_size", 4);
            label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        }
    }
}
