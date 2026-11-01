// MZ 的控制字元處理。
//
// 事件文本裡混著 \V[1]、\N[2]、\C[3] 這類控制字元，直接畫出來會看到一堆反斜線。
// 這一版做到的：變數、角色名、金錢單位、跳過會停頓的那幾個。
// 還沒做的：\C[n] 換色與 \{ \} 改字級 —— 那些要等文字層改用 BBCode 再一起做。
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;

namespace RMZ
{
    public static class RMZTextCodes
    {
        static readonly Regex Variable = new Regex(@"\\V\[(\d+)\]", RegexOptions.IgnoreCase);
        static readonly Regex ActorName = new Regex(@"\\N\[(\d+)\]", RegexOptions.IgnoreCase);
        static readonly Regex PartyName = new Regex(@"\\P\[(\d+)\]", RegexOptions.IgnoreCase);
        static readonly Regex Color = new Regex(@"\\C\[(\d+)\]", RegexOptions.IgnoreCase);
        static readonly Regex Icon = new Regex(@"\\I\[(\d+)\]", RegexOptions.IgnoreCase);
        static readonly Regex Pause = new Regex(@"\\[.|!><^]", RegexOptions.IgnoreCase);
        static readonly Regex Fit = new Regex(@"\\FIT", RegexOptions.IgnoreCase);

        // ---- VisuMZ MessageCore / DKTools 的排版碼（信件、人物介紹那種「字寫在圖片上」的畫面在用）----
        static readonly Regex AlignTag = new Regex(@"<(Left|Center|Right)>", RegexOptions.IgnoreCase);
        static readonly Regex WordWrapTag = new Regex(@"<(?:WORDWRAP|WORD WRAP)>", RegexOptions.IgnoreCase);
        static readonly Regex NoWordWrapTag = new Regex(@"<(?:NOWORDWRAP|NO WORD WRAP)>", RegexOptions.IgnoreCase);
        static readonly Regex BreakTag = new Regex(@"<(?:BR|LINEBREAK)>", RegexOptions.IgnoreCase);
        static readonly Regex StyleCode = new Regex(
            @"<(Left|Center|Right)>|\\(MFB|MFS)|\\OutlineWidth\[(\d+)\]|\\(OutlineHexColor|HexColor)<([^>]*)>",
            RegexOptions.IgnoreCase);

        /// <summary>訊息裡有沒有夾帶 \FIT 這個自訂控制碼（置中＋外框貼齊內容＋整段瞬間顯示，不逐字跑）。</summary>
        public static bool HasFit(string raw) => !string.IsNullOrEmpty(raw) && Fit.IsMatch(raw);

        /// <summary>有沒有排版碼（置中、字級、外框、指定顏色）—— 有的話對話框改用逐行排版。</summary>
        public static bool HasStyleCodes(string raw) =>
            !string.IsNullOrEmpty(raw) && StyleCode.IsMatch(raw);

        /// <summary>
        /// VisuMZ 的換行規則：
        ///   有 &lt;WordWrap&gt;（而且這頁沒有對齊碼）→ 原本的換行全部接起來，只有 &lt;br&gt; 才換行
        ///   沒有 → 保留原本的換行，&lt;br&gt; 也是換行
        /// 中文不需要 VisuMZ 接行時補的空白，這裡直接接起來。
        /// </summary>
        static string ApplyLineBreaks(string text, bool forceWrap)
        {
            bool aligned = AlignTag.IsMatch(text);
            bool wrap = !aligned && !NoWordWrapTag.IsMatch(text) && (forceWrap || WordWrapTag.IsMatch(text));
            text = WordWrapTag.Replace(text, "");
            text = NoWordWrapTag.Replace(text, "");
            if (wrap) text = Regex.Replace(text, @"[\r\n]+", "");
            return BreakTag.Replace(text, "\n");
        }

        public static string Convert(string text) => Convert(text, false);

        /// <summary>一般對話框用：排版碼全部拿掉，只留文字。</summary>
        public static string Convert(string text, bool forceWrap)
        {
            text = ConvertKeepStyle(text, forceWrap);
            return StyleCode.Replace(text, "");
        }

        /// <summary>
        /// 跟 Convert 一樣處理變數、名字、翻譯與換行，但留下排版碼給 ParseStyledLines 用。
        /// </summary>
        public static string ConvertKeepStyle(string text, bool forceWrap)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = ApplyLineBreaks(text, forceWrap);

            // MZ 會先把 \\ 換成一個佔位字元，避免 "\\V[1]" 被當成控制字元
            text = text.Replace("\\\\", "\u001b");

            text = Variable.Replace(text, m => RMZGameState.VariableDisplay(int.Parse(m.Groups[1].Value)));
            text = ActorName.Replace(text, m => ActorNameOf(int.Parse(m.Groups[1].Value)));
            text = PartyName.Replace(text, m => ActorNameOf(int.Parse(m.Groups[1].Value)));
            text = text.Replace("\\G", RMZDatabase.System?.currencyUnit ?? "");

            // 這幾個是演出用的，目前先拿掉：換色、圖示、停頓、等待輸入
            text = Color.Replace(text, "");
            text = Icon.Replace(text, "");
            text = Pause.Replace(text, "");
            text = text.Replace("\\$", "");
            text = Fit.Replace(text, "");

            // 事件文字是寫成 {翻譯鍵} 的形式（例如 {請選擇遊戲難度}）。
            // 這份「瘦身版」的 locales 是空的，Translate 會把大括號去掉、直接顯示鍵本身 ——
            // 鍵本身就是中文，讀起來剛好正確。
            text = RMZLocalization.Translate(text);

            return text.Replace("\u001b", "\\");
        }

        /// <summary>逐行排版用的一行：文字＋這一行的對齊、字級、顏色、外框。</summary>
        public class StyledLine
        {
            public string Text = "";
            public HorizontalAlignment Align = HorizontalAlignment.Left;
            public int FontSize;
            public Godot.Color Color;
            public int OutlineSize;
            public Godot.Color OutlineColor;
        }

        /// <summary>
        /// 把「ConvertKeepStyle 處理過的文字」切成一行一行，套上 VisuMZ／DKTools 的排版碼：
        ///   &lt;Left&gt; &lt;Center&gt; &lt;Right&gt;　對齊（一直生效到換頁）
        ///   \MFB ／ \MFS　　　　　　　 字級 +12 ／ -12（DKTools，等同 MZ 的 \{ \}）
        ///   \OutlineWidth[n]　　　　　外框粗細
        ///   \HexColor&lt;#xxxxxx&gt;、\OutlineHexColor&lt;#xxxxxx&gt;　文字／外框顏色
        /// 資料裡這些碼都寫在行首，所以一行只取一種樣式（行中間改樣式的話從下一行起生效）。
        /// RM 的字級基準 26、這裡 24，加減 12 的規則一樣。
        /// </summary>
        public static List<StyledLine> ParseStyledLines(string text, int baseSize, Godot.Color baseColor,
            int baseOutline, Godot.Color baseOutlineColor)
        {
            var result = new List<StyledLine>();
            var align = HorizontalAlignment.Left;
            int size = baseSize, outline = baseOutline;
            var color = baseColor;
            var outlineColor = baseOutlineColor;

            foreach (var rawLine in (text ?? "").Replace("\r", "").Split('\n'))
            {
                var line = new StyledLine();
                var sb = new System.Text.StringBuilder();
                int pos = 0;
                foreach (Match m in StyleCode.Matches(rawLine))
                {
                    sb.Append(rawLine, pos, m.Index - pos);
                    pos = m.Index + m.Length;
                    if (m.Groups[1].Success)
                        align = m.Groups[1].Value.ToLowerInvariant() switch
                        {
                            "center" => HorizontalAlignment.Center,
                            "right" => HorizontalAlignment.Right,
                            _ => HorizontalAlignment.Left,
                        };
                    else if (m.Groups[2].Success)
                        size = m.Groups[2].Value.ToUpperInvariant() == "MFB" ? Mathf.Min(size + 12, 108) : Mathf.Max(size - 12, 12);
                    else if (m.Groups[3].Success)
                        outline = int.Parse(m.Groups[3].Value) + 1;   // RM 預設 3 ≈ 這裡的 4
                    else if (m.Groups[4].Success)
                    {
                        var c = Godot.Color.FromString(m.Groups[5].Value.Trim(), m.Groups[4].Value.ToLowerInvariant() == "hexcolor" ? color : outlineColor);
                        if (m.Groups[4].Value.ToLowerInvariant() == "hexcolor") color = c; else outlineColor = c;
                    }
                    // 行首的碼決定這一行的樣式；字出現之後才改的，從下一行起算
                    if (sb.ToString().Trim().Length == 0) ApplyStyle(line, align, size, color, outline, outlineColor);
                }
                sb.Append(rawLine, pos, rawLine.Length - pos);
                if (line.FontSize == 0) ApplyStyle(line, align, size, color, outline, outlineColor);
                line.Text = sb.ToString().Trim();
                result.Add(line);
            }
            return result;
        }

        static void ApplyStyle(StyledLine l, HorizontalAlignment a, int size, Godot.Color c, int o, Godot.Color oc)
        {
            l.Align = a; l.FontSize = size; l.Color = c; l.OutlineSize = o; l.OutlineColor = oc;
        }

        static string ActorNameOf(int id)
        {
            var actor = RMZDatabase.Actor(id);
            return actor != null ? RMZLocalization.Translate(actor.name) : "";
        }
    }
}
