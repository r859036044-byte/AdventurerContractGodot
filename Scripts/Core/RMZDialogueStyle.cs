// 對話框的配色：底色透明度 + 依說話者上色。
//
// 設定放在場景的 RMZ World（RMZBootstrap.Dialogue），播放中改馬上看得到。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    [System.Serializable]
    public class RMZSpeakerColor
    {
        public string Match = "";

        public Color Color = Colors.White;

        public bool OverrideFrame;

        public Color Frame = new Color(0.79f, 0.66f, 0.36f);

        public Color FrameDark = new Color(0.15f, 0.12f, 0.08f);

        public bool OverrideFill;

        public Color Fill = new Color(0.4f, 0.75f, 0.9f);

        public bool OverrideFillOpacity;

        public float FillOpacity = 0.18f;
    }

    [System.Serializable]
    public class RMZDialogueStyle
    {
        public float FillOpacity = 0.35f;

        public float DimOpacity = 0.5f;

        public bool TintFill = true;

        public float TintStrength = 0.35f;

        public bool TintBorder = false;

        public bool TintNameBox = false;

        public bool TintText = false;

        public bool ApplyToChoiceWindow = true;

        public List<RMZSpeakerColor> Speakers = new List<RMZSpeakerColor>();

        public Color DefaultColor = new Color(0.45f, 0.52f, 0.75f);

        /// <summary>目前生效的設定。場景裡沒有 RMZ World 時退回預設值。</summary>
        public static RMZDialogueStyle Current =>
            RMZBootstrap.Instance != null && RMZBootstrap.Instance.Dialogue != null
                ? RMZBootstrap.Instance.Dialogue
                : Fallback;

        static readonly RMZDialogueStyle Fallback = new RMZDialogueStyle { Speakers = Default() };

        /// <summary>
        /// 找出這位說話者的顏色。
        /// raw 是事件裡原本的字串（例如 "{尤梨卡}" 或 "\N[1]"），
        /// resolved 是套過在地化與控制碼之後的顯示名（例如玩家取的名字）。
        /// </summary>
        /// <summary>找出這位說話者的整組設定（沒對到回 null）。</summary>
        public RMZSpeakerColor EntryFor(string raw, string resolved)
        {
            if (Speakers == null || Speakers.Count == 0) Speakers = Default();

            foreach (var entry in Speakers)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Match)) continue;
                if (!string.IsNullOrEmpty(raw) && raw.Contains(entry.Match)) return entry;
                if (!string.IsNullOrEmpty(resolved) && resolved.Contains(entry.Match)) return entry;
            }
            return null;
        }

        public Color ColorFor(string raw, string resolved)
        {
            // 場景是舊的、清單還沒填時，自動補上預設表，免得整片都是同一色
            if (Speakers == null || Speakers.Count == 0) Speakers = Default();

            {
                foreach (var entry in Speakers)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Match)) continue;
                    if (!string.IsNullOrEmpty(raw) && raw.Contains(entry.Match)) return entry.Color;
                    if (!string.IsNullOrEmpty(resolved) && resolved.Contains(entry.Match)) return entry.Color;
                }
            }
            return DefaultColor;
        }

        /// <summary>預設的角色顏色表。名單是從事件資料裡出現次數最多的說話者抓的。</summary>
        public static List<RMZSpeakerColor> Default() => new List<RMZSpeakerColor>
        {
            // 男主：事件裡寫成 \N[1]，會換成玩家取的名字，所以比對原字串
            new RMZSpeakerColor { Match = @"\N[1]",  Color = new Color(0.55f, 0.72f, 1.00f) },  // 藍
            new RMZSpeakerColor { Match = "尤梨卡",   Color = new Color(1.00f, 0.80f, 0.90f) },  // 粉紅
            new RMZSpeakerColor { Match = "露娜",     Color = new Color(0.62f, 1.00f, 0.72f) },  // 綠
            new RMZSpeakerColor { Match = "克萊爾",   Color = new Color(0.80f, 0.62f, 1.00f) },  // 紫
            new RMZSpeakerColor { Match = "雪瑤",     Color = new Color(0.62f, 0.92f, 1.00f) },  // 淺藍
            new RMZSpeakerColor { Match = "雪霜",     Color = new Color(0.70f, 0.80f, 1.00f) },  // 冰藍
            // 鈴音：底色用她身上那個藍，外框走黑紫＋金
            new RMZSpeakerColor {
                Match = "鈴音",
                Color = new Color(0.55f, 0.68f, 0.95f),
                OverrideFrame = true,
                Frame = new Color(0.86f, 0.72f, 0.36f),      // 金
                FrameDark = new Color(0.16f, 0.07f, 0.22f),  // 黑紫
                OverrideFill = true,
                Fill = new Color(0.12f, 0.30f, 0.55f),       // 深藍（比原本的水藍沉，接近黑貓商人那種）
                // 她的框刻意比別人透，後面的場景與立繪要看得到
                OverrideFillOpacity = true,
                FillOpacity = 0.30f,
            },
            new RMZSpeakerColor { Match = "凱",       Color = new Color(0.95f, 0.85f, 0.55f) },  // 淡金
            new RMZSpeakerColor { Match = "露米奈",   Color = new Color(1.00f, 0.88f, 0.62f) },
            new RMZSpeakerColor { Match = "艾爾緹翁", Color = new Color(0.85f, 0.70f, 0.55f) },
            new RMZSpeakerColor { Match = "黑貓",     Color = new Color(0.60f, 0.58f, 0.68f) },
            new RMZSpeakerColor { Match = "錢商人",   Color = new Color(0.90f, 0.82f, 0.45f) },
            new RMZSpeakerColor { Match = "櫃檯",     Color = new Color(1.00f, 0.88f, 0.62f) },  // 櫃檯小姐／櫃檯雌小鬼，跟露米奈同一個黃
            new RMZSpeakerColor { Match = "伽露達",   Color = new Color(0.75f, 0.90f, 0.60f) },
            new RMZSpeakerColor { Match = "塔克",     Color = new Color(0.80f, 0.70f, 0.60f) },
            new RMZSpeakerColor { Match = "白紗",     Color = new Color(0.92f, 0.92f, 0.95f) },
        };
    }
}
