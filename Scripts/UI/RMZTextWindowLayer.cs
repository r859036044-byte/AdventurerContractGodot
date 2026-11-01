// Hendrix Pictures UI Creator 的「文字視窗」(ShowText / EraseText / EraseAllText)。
//
// 遊戲的標題選單、日期、金錢、等級、戰力、裝備欄位…這些疊在圖片 UI 上的字
// 全都走這個指令，沒做的話 UI 會只剩空框。
//
// 版面來源有兩層：
//   1. 插件指令自己帶的 sizePosition（"寬, 高, x, y"）
//   2. RM 專案的 showTextPositions.json —— 作者在遊戲內拖曳排版後存下來的座標，
//      有這一筆就以它為準（rmz/ui/showTextPositions.json）。
//
// [移植狀態] Godot 重寫版。已做：版面、字級、顏色、描邊、對齊、跟隨圖片、
// 自動更新、\V[n] \N[n] \G[] 控制碼。未做：\I[n] 圖示（要先移植 RMZIconSet）。
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Godot;
using Newtonsoft.Json.Linq;

namespace RMZ
{
    public partial class RMZTextWindowLayer : CanvasLayer
    {
        public static RMZTextWindowLayer Instance { get; private set; }

        public const float RefWidth = 1280f;
        public const float RefHeight = 720f;

        class Entry
        {
            public string Id;
            public string RawText = "";
            public bool AutoUpdate;
            public int AttachedPicture;     // 0 = 不跟隨
            public bool AbsoluteBox;        // 版面存檔裡的座標是絕對值，不再加圖片位移
            public bool FollowFromShow;     // 絕對座標，但之後圖片移動多少就跟著移多少（起床的股票羊皮紙）
            public Vector2 AttachOrigin;    // 顯示文字那一刻，附著的圖片在哪裡
            public Rect2 Box;
            public RMZIconLabel Label;
            public string LastShown;
        }

        readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        Control _root;

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            Layer = 7;               // 圖片層(5) 之上、對話框(10) 之下
            ProcessPriority = 260;

            _root = new Control { Name = "Texts", MouseFilter = Control.MouseFilterEnum.Ignore };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_root);

            RMZTextLayout.Load();
        }

        // ---------------- 對外 ----------------

        public void Show(string id, string rawText, string sizePosition, string attachedToPicture,
                         bool autoUpdate, string textSettingsJson)
        {
            if (string.IsNullOrEmpty(id)) return;

            if (!_entries.TryGetValue(id, out var e))
            {
                e = new Entry { Id = id };
                Build(e);
                _entries[id] = e;
            }

            e.RawText = rawText ?? "";
            e.AutoUpdate = autoUpdate;
            e.AttachedPicture = ParseInt(attachedToPicture, 0);

            var box = ParseSizePosition(sizePosition);

            int fontSize = 22;
            // RM 畫字時預設就有 3px 描邊；不畫的話 HUD 的字疊在花紋地板上會糊成一片
            float outlineSize = 3f;
            var color = Colors.White;
            var outlineColor = Colors.Black;
            var align = HorizontalAlignment.Center;
            var valign = VerticalAlignment.Center;

            ApplyTextSettings(textSettingsJson, ref fontSize, ref color, ref outlineColor, ref align, ref valign);

            // 作者在遊戲內排好、存進 showTextPositions.json 的版面優先
            var saved = RMZTextLayout.Find(id);
            e.AbsoluteBox = false;
            if (saved != null)
            {
                if (saved.HasBox)
                {
                    box = saved.Box;
                    // 拖曳排版存下來的是螢幕絕對座標，不能再加一次 attachedToPicture 的位移。
                    // 但文字顯示之後圖片如果移動（起床的股票羊皮紙從畫面外 y=680 滑到 y=0），
                    // 文字要跟著移同樣的距離 —— 跟 Hendrix 一樣，記下顯示當下圖片的位置，之後只加「位移量」。
                    e.AbsoluteBox = true;
                    e.FollowFromShow = false;
                    if (e.AttachedPicture > 0)
                    {
                        var pic0 = RMZScreen.Instance?.Picture(e.AttachedPicture);
                        if (pic0 != null) { e.FollowFromShow = true; e.AttachOrigin = new Vector2(pic0.X, pic0.Y); }
                    }
                }
                if (saved.FontSize > 0) fontSize = saved.FontSize;
                if (saved.OutlineSize > 0f) outlineSize = saved.OutlineSize;
            }

            // 圖片上的固定欄位以實際框寬排版，不再依靠空白字元推開數值。
            e.Label.ColumnSplit = -1;
            e.Label.CenterColumns = false;
            e.Label.IconSize = 32;
            bool fixedRow = false;
            int statusRow = id == "player level" ? 0 : id == "player power level" ? 1 : id == "player money" ? 2 : id == "player exp" ? 3 : -1;
            if(statusRow >= 0) {
                box = new Rect2(28, 27 + statusRow * 46.2f, 376, 38);
                e.RawText = Regex.Replace(e.RawText, @"\s{2,}", "\t");
                e.Label.ColumnSplit = 132; e.Label.CenterColumns = true; fixedRow = true;
            }
            var card = Regex.Match(id, @"^heroine ([1-5]) (Love|Fall|power)$");
            if(card.Success) {
                int index = int.Parse(card.Groups[1].Value)-1;
                string field = card.Groups[2].Value;
                box = new Rect2(64 + index * 160, field == "Love" ? 490 : field == "Fall" ? 528 : 606, 128, 32);
                fontSize = 18; e.Label.IconSize = 24; e.Label.ColumnSplit = 88;
                e.RawText = Regex.Replace(e.RawText, @"[:：]\s*", "\t");
                fixedRow = true;
            }
            int lootRow = id == "adventure equipment" ? 0 : id == "adventure scroll" ? 1 : id == "adventure item" ? 2 : -1;
            if(lootRow >= 0) {
                box = new Rect2(1010, 284+lootRow*40, 152, 36);
                e.Label.ColumnSplit = 110; e.Label.IconSize = 28;
                e.RawText = Regex.Replace(e.RawText, @"[:：]\s*", "\t"); fixedRow = true;
            }
            if(id == "adventure player battle power") { box=new Rect2(54,0,114,44);align=HorizontalAlignment.Center;fixedRow=true; }
            if(id == "player money team") { box=new Rect2(1008,146,140,34);align=HorizontalAlignment.Center;fixedRow=true;e.Label.IconSize=24; }
            if(id == "house money day" || id == "how much house money") {
                box=new Rect2(532,id == "house money day"?84:110,220,26);fontSize=18;
                e.Label.ColumnSplit=180;e.RawText=Regex.Replace(e.RawText,@"[:：]\s*","\t");fixedRow=true;
            }
            var statusCard = Regex.Match(id, @"^heroine status (Love|Fall) ([1-5])$");
            if(statusCard.Success) {
                int index=int.Parse(statusCard.Groups[2].Value)-1;
                box=new Rect2(252+index*160,statusCard.Groups[1].Value=="Love"?510:550,136,32);
                fontSize=18;e.Label.IconSize=24;e.Label.ColumnSplit=94;
                e.RawText=Regex.Replace(e.RawText,@"[:：]\s*","\t");fixedRow=true;
            }
            if(id=="heroine status name" || id=="heroine status years" || id=="heroine status rank") {
                int row=id.EndsWith("name")?0:id.EndsWith("years")?1:2;
                box=new Rect2(432,216+row*48,390,40);fontSize=24;align=HorizontalAlignment.Left;fixedRow=true;
            }
            var intro=Regex.Match(id,@"^heroine status introduction ([1-3])$");
            if(intro.Success) {box=new Rect2(432,420+(int.Parse(intro.Groups[1].Value)-1)*48,736,40);fontSize=24;align=HorizontalAlignment.Left;fixedRow=true;}
            if(id=="heroine status like" || id=="heroine status fall") {
                box=new Rect2(900,id.EndsWith("like")?218:266,232,40);fontSize=24;e.Label.IconSize=28;e.Label.ColumnSplit=160;
                e.RawText=Regex.Replace(e.RawText,@"[:：]\s*","\t");fixedRow=true;
            }
            // 僱傭的佣金：原本跟著圖片 240（組隊頁籤）的左上角，會壓在頁籤上、到了地圖頁又被清掉。
            // 固定放在右邊金幣底下，內容由 HireFeeText 即時算。
            if(id == "money on board") { box=new Rect2(1008,184,140,34);align=HorizontalAlignment.Center;fixedRow=true;e.Label.IconSize=24; }
            if(fixedRow){e.AbsoluteBox=true;e.FollowFromShow=false;valign=VerticalAlignment.Center;}
            // 戰力框會隨組隊模式上下移動，數字跟隨圖片內的空白欄位。
            if(id == "adventure player battle power") { e.AbsoluteBox=false; e.AttachedPicture=243; }
            e.Box = box;

            var label = e.Label;
            label.AddThemeFontOverride("font", RMZFonts.Main);
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", color);
            label.HorizontalAlignment = align;
            label.VerticalAlignment = valign;

            // RM 的 outlineWidth 是真正的 stroke，有一半壓在字身裡面，
            // 換成 Godot 的外描邊大約要乘二才等寬
            label.AddThemeConstantOverride("outline_size", Mathf.RoundToInt(Mathf.Max(0f, outlineSize) * 2f));
            label.AddThemeColorOverride("font_outline_color", outlineColor);

            label.Visible = true;
            e.LastShown = null;   // 逼它重畫一次
            Refresh(e);
        }

        public void Erase(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!_entries.TryGetValue(id, out var e)) return;
            if (IsInstanceValid(e.Label)) e.Label.QueueFree();
            _entries.Remove(id);
        }

        public void EraseAll()
        {
            foreach (var e in _entries.Values)
                if (IsInstanceValid(e.Label)) e.Label.QueueFree();
            _entries.Clear();
        }

        // ---------------- 每幀 ----------------

        public override void _Process(double delta)
        {
            foreach (var e in _entries.Values)
            {
                if (!IsInstanceValid(e.Label)) continue;
                if (e.AutoUpdate || e.AttachedPicture > 0) Refresh(e);
            }
        }

        void Refresh(Entry e)
        {
            var box = e.Box;

            if (e.AttachedPicture > 0 && !e.AbsoluteBox)
            {
                var pic = RMZScreen.Instance?.Picture(e.AttachedPicture);
                if (pic != null) box.Position += new Vector2(pic.X, pic.Y);
            }
            else if (e.FollowFromShow)
            {
                var pic = RMZScreen.Instance?.Picture(e.AttachedPicture);
                if (pic != null) box.Position += new Vector2(pic.X, pic.Y) - e.AttachOrigin;
            }

            // 版面座標是以 1280x720 為基準，畫面被拉伸時要跟著縮放
            float scale = _root.Size.Y / RefHeight;
            e.Label.Position = box.Position * scale;
            e.Label.Size = box.Size;
            e.Label.Scale = Vector2.One * scale;

            string raw = e.RawText;
            // RM 新版：選「僱傭」時，每張女主卡片的「好感度」那一列改成顯示她的佣金（變數 669～673）。
            // 舊版的「money on board」單一佣金欄就不顯示了。
            if (e.Id == "money on board") { e.Label.Visible = false; return; }
            var loveCard = Regex.Match(e.Id, @"^heroine ([1-5]) Love$");
            if (loveCard.Success)
            {
                bool hire = RMZGameState.Variable(43) == 1;
                if (hire) raw = "{佣金}\t" + RMZGameState.Variable(668 + int.Parse(loveCard.Groups[1].Value));
            }

            string shown = ConvertCodes(Icon.Replace(raw, m => "\uE000" + m.Groups[1].Value + "\uE001"));
            if (shown == e.LastShown) return;
            e.LastShown = shown;
            e.Label.SetIconText(shown);
        }

        /// <summary>
        /// 僱傭的佣金（CE48／CE49 的算法）：
        ///   選女主那一頁：變數 676 = 選到的女主的基本佣金（依好感度）
        ///   選地圖那一頁：變數 675 = 基本佣金 ÷ 地圖等級（C ÷4、B ÷3、A ÷2、S 全額），滑鼠移到地圖上才會算
        /// 原本的文字只顯示 675，在選女主那一頁一直是 0 或上一次的值。
        /// </summary>
        static string HireFeeText()
        {
            bool mapPage = RMZScreen.Instance?.Picture(256) != null;
            int fee = mapPage && RMZGameState.Variable(675) > 0 ? RMZGameState.Variable(675) : RMZGameState.Variable(676);
            if (RMZGameState.Variable(59) <= 0) return "{佣金}：-";
            return "{佣金}：" + fee;
        }

        void Build(Entry e)
        {
            e.Label = new RMZIconLabel
            {
                Name = "Text_" + e.Id,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ClipText = false,
            };
            _root.AddChild(e.Label);
        }

        // ---------------- 參數解析 ----------------

        /// <summary>Hendrix 的 sizePosition 是 "寬, 高, x, y"。</summary>
        static Rect2 ParseSizePosition(string s)
        {
            var box = new Rect2(0f, 0f, 300f, 60f);
            if (string.IsNullOrWhiteSpace(s)) return box;

            var parts = s.Split(',');
            float w = parts.Length > 0 ? ParseFloat(parts[0], 300f) : 300f;
            float h = parts.Length > 1 ? ParseFloat(parts[1], 60f) : 60f;
            float x = parts.Length > 2 ? ParseFloat(parts[2], 0f) : 0f;
            float y = parts.Length > 3 ? ParseFloat(parts[3], 0f) : 0f;
            return new Rect2(x, y, w, h);
        }

        static void ApplyTextSettings(string json, ref int fontSize, ref Color color,
                                      ref Color outlineColor, ref HorizontalAlignment align,
                                      ref VerticalAlignment valign)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            JObject o;
            try { o = JObject.Parse(json); }
            catch { return; }

            if (o["fontSize"] != null && int.TryParse(o["fontSize"].ToString(), out var fs) && fs > 0) fontSize = fs;
            if (o["textColor"] != null) color = ParseColor(o["textColor"].ToString(), color);
            if (o["color"] != null) color = ParseColor(o["color"].ToString(), color);
            if (o["outlineColor"] != null) outlineColor = ParseColor(o["outlineColor"].ToString(), outlineColor);

            string a = o["align"]?.ToString()?.ToLowerInvariant() ?? "";
            if (a.Contains("left")) align = HorizontalAlignment.Left;
            else if (a.Contains("right")) align = HorizontalAlignment.Right;
            else if (a.Contains("center")) align = HorizontalAlignment.Center;

            if (a.Contains("top")) valign = VerticalAlignment.Top;
            else if (a.Contains("bottom")) valign = VerticalAlignment.Bottom;
        }

        /// <summary>吃 "#ffffff"、"rgba(255,255,255,1)" 與 CSS 色名。</summary>
        static Color ParseColor(string s, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            s = s.Trim();

            if (s.StartsWith("#")) return new Color(s);

            var m = Regex.Match(s, @"rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*(?:,\s*([\d.]+)\s*)?\)");
            if (m.Success)
            {
                float a = m.Groups[4].Success ? ParseFloat(m.Groups[4].Value, 1f) : 1f;
                return new Color(int.Parse(m.Groups[1].Value) / 255f,
                                 int.Parse(m.Groups[2].Value) / 255f,
                                 int.Parse(m.Groups[3].Value) / 255f, a);
            }

            try { return Color.FromString(s, fallback); }
            catch { return fallback; }
        }

        static float ParseFloat(string s, float fallback) =>
            float.TryParse(s?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        static int ParseInt(string s, int fallback) =>
            int.TryParse(s?.Trim(), out var v) ? v : fallback;

        // ---------------- 文字控制碼 ----------------
        //
        // Hendrix 的文字有時候不寫反斜線（寫成 v[12] 而不是 \V[12]），所以兩種都要吃。

        static readonly Regex Variable = new Regex(@"\\?[Vv]\[(\d+)\]", RegexOptions.Compiled);
        static readonly Regex ActorName = new Regex(@"\\?[Nn]\[(\d+)\]", RegexOptions.Compiled);
        static readonly Regex Icon = new Regex(@"\\?[Ii]\[(\d+)\]", RegexOptions.Compiled);
        static readonly Regex Gold = new Regex(@"\\?[Gg]\[\]", RegexOptions.Compiled);
        static readonly Regex OtherCodes = new Regex(@"\\[A-Za-z]+\[[^\]]*\]|\\[.|^$>< !]", RegexOptions.Compiled);

        public static string ConvertCodes(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            string s = Icon.Replace(raw, "");   // 純文字呼叫端移除圖示；顯示欄位在 Refresh 先轉為 RMZIconLabel 標記。
            s = Variable.Replace(s, m => RMZGameState.Variable(int.Parse(m.Groups[1].Value)).ToString());
            s = ActorName.Replace(s, m => RMZDatabase.Actor(int.Parse(m.Groups[1].Value))?.name ?? "");
            s = Gold.Replace(s, RMZParty.Gold.ToString());
            s = RMZLocalization.Translate(s);
            s = OtherCodes.Replace(s, "");
            return s;
        }
    }
}
