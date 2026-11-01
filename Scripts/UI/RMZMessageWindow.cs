// 對話框。對應 MZ 的 Window_Message + Window_ChoiceList。
//
// 版面與配色照 Unity 版（RMZMessageWindow 706 行 + RMZWindowSkin）對過：
//   一般訊息：寬 1280-48、高 180，依 PositionType 放上／中／下
//   有選項時：問句縮成貼合內容的小框，跟選項框一起置中、整組往下挪 96px
//   配色：金框 + 外緣暗線 + 深藍底
//
// [移植狀態] 已做：逐字顯示、臉圖、名字框、選項（鍵盤＋滑鼠）、三種背景、控制碼。
// 未做：\C[n] 換色、\I[n] 圖示、依說話者上色、showTextPositions 的逐句微調。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public partial class RMZMessageWindow : CanvasLayer
    {
        public static RMZMessageWindow Instance { get; private set; }

        const float RefWidth = 1280f;
        const float RefHeight = 720f;
        const float MessageHeight = 180f;
        const float ChoiceLineHeight = 46f;
        const float ChoiceGroupDropY = 26f;   // 有選項時整組往下挪多少（原本 96，往上移約 50px 改成 46，再往上 20px 改成 26）
        // 像素字型的原生大小是 12px，用整數倍才不會糊
        const int FontSize = RMZFonts.DefaultSize;        // 24
        const int NameFontSize = RMZFonts.DefaultSize;

        // 配色與樣式一律取自 RMZWindowSkin，兩邊才不會各自漂移
        static Color SkinFill => RMZWindowSkin.Fill;
        static Color SkinText => RMZWindowSkin.Text;
        static Color SkinTextSelected => RMZWindowSkin.TextSelected;

        Control _root;
        Panel _window;
        Label _text;
        Panel _namePanel;
        Label _nameLabel;
        TextureRect _face;
        Panel _choicePanel;
        readonly List<Panel> _choiceRows = new List<Panel>();
        readonly List<Label> _choiceLabels = new List<Label>();
        // 每一列按下去的結果（一般選項 = 自己的編號；多加的「取消」列 = ChoiceCancel），以及能不能選（分隔線不能選）
        readonly List<int> _choiceResults = new List<int>();
        readonly List<bool> _choiceSelectable = new List<bool>();
        static readonly System.Text.RegularExpressions.Regex SeparatorRow =
            new System.Text.RegularExpressions.Regex(@"^[\s\-－—─━_＿=＝]{3,}$");

        int _version = -1;
        int _choiceIndex;
        float _revealed;
        string _fullText = "";
        bool _waitingConfirm;
        // 逐行排版（VisuMZ 的 <Center>、\MFB、\HexColor… 或信件／人物介紹那種多行頁面）
        bool _lineMode;
        Control _lines;
        readonly List<Label> _lineLabels = new List<Label>();
        readonly List<int> _lineLengths = new List<int>();
        bool _fitMode;   // \FIT：置中＋外框貼齊內容＋整段瞬間顯示（給難度說明那種短提示用）
        bool _lastMouseLeft;
        ulong _appliedFrame;

        static ulong _escapeFrame = ulong.MaxValue;
        /// <summary>這一幀 ESC 已經被對話框拿去取消選項了（系統選單不要再開）。</summary>
        public static bool EscapeUsedThisFrame => _escapeFrame == Engine.GetProcessFrames();
        Vector2 _lastChoiceMousePosition;

        public bool IsBusy => RMZMessage.Instance.IsBusy;

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            Layer = 10;
            ProcessPriority = 300;   // 事件都跑完了再更新畫面

            _root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_root);

            _window = new Panel { Name = "Window", MouseFilter = Control.MouseFilterEnum.Ignore };
            _window.AddThemeStyleboxOverride("panel", PanelStyle(SkinFill));
            _root.AddChild(_window);

            _face = new TextureRect
            {
                Name = "Face",
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _window.AddChild(_face);

            _text = new Label
            {
                Name = "Text",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            StyleText(_text, FontSize, SkinText);
            _window.AddChild(_text);

            // 逐行排版的字放這裡（不放進 _window：信件頁面的對話框底是隱藏的）
            _lines = new Control { Name = "Lines", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            _root.AddChild(_lines);

            _namePanel = new Panel { Name = "NameBox", MouseFilter = Control.MouseFilterEnum.Ignore };
            _namePanel.AddThemeStyleboxOverride("panel", PanelStyle(SkinFill));
            _root.AddChild(_namePanel);

            _nameLabel = new Label
            {
                Name = "NameLabel",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            StyleText(_nameLabel, NameFontSize, SkinTextSelected);
            _namePanel.AddChild(_nameLabel);

            _choicePanel = new Panel { Name = "Choices", MouseFilter = Control.MouseFilterEnum.Ignore };
            _choicePanel.AddThemeStyleboxOverride("panel", PanelStyle(SkinFill));
            _root.AddChild(_choicePanel);

            SetShown(false);
        }

        static void StyleText(Label label, int size, Color color) =>
            RMZWindowSkin.StyleLabel(label, size, color);

        static StyleBoxFlat PanelStyle(Color fill) => RMZWindowSkin.PanelStyle(fill);

        static StyleBoxFlat RowStyle(bool selected) => RMZWindowSkin.RowStyle(selected);

        void SetShown(bool on)
        {
            _root.Visible = on;
            if (!on)
            {
                _choicePanel.Visible = false;
                _namePanel.Visible = false;
            }
        }

        public override void _Process(double delta)
        {
            var msg = RMZMessage.Instance;

            if (!msg.IsBusy)
            {
                if (_root.Visible) SetShown(false);
                _version = -1;
                return;
            }

            // 事件常常「關掉一段訊息、同一幀馬上開下一段」，所以要看 Version 而不是 IsBusy
            if (_version != msg.Version) Apply(msg);

            UpdateReveal((float)delta);
            UpdateMagic();
            UpdateInput(msg);
        }

        // ---------------- 套用內容 ----------------

        void Apply(RMZMessage msg)
        {
            _version = msg.Version;
            _choiceIndex = Mathf.Max(0, msg.ChoiceDefault);
            _revealed = 0f;
            _waitingConfirm = false;
            // 新選單保留預設項目，不讓原本停在選單區域的游標搶走選擇。
            _lastChoiceMousePosition = _root.GetGlobalMousePosition();
            // 打開這段訊息的那一下點擊／確定鍵不能算成「選了選項」：
            // 例如點信封開出選單，同一個點擊會直接把第一個選項選下去（跳過股票那一層）
            _lastMouseLeft = Input.IsMouseButtonPressed(MouseButton.Left);
            _appliedFrame = Engine.GetProcessFrames();

            string raw = msg.AllText();
            _fitMode = RMZTextCodes.HasFit(raw);
            _lineMode = !_fitMode && !msg.HasChoices && (RMZMessageCore.IsPage || RMZTextCodes.HasStyleCodes(raw));
            if (_lineMode) BuildLines(raw);
            else _fullText = RMZTextCodes.Convert(raw, RMZMessageCore.WordWrap);
            _text.Visible = !_lineMode;
            _lines.Visible = _lineMode;

            if (_fitMode)
            {
                // \FIT：不逐字跑，直接整段出現
                _text.Text = _fullText;
                _revealed = _fullText.Length;
                _waitingConfirm = true;
                _confirmSince = Now;
            }
            else
            {
                SetRevealed(0);
            }

            ApplyFace(msg);
            ApplySpeaker(msg);
            ApplyDialogueStyle(msg);

            BuildChoices(msg);
            Layout(msg);
            SetShown(true);
        }

        /// <summary>
        /// 對話框配色。底色是半透明的（RMZDialogueStyle.FillOpacity），
        /// 再依說話者混入該角色的顏色；鈴音那種還會自己指定底色與框色。
        /// </summary>
        void ApplyDialogueStyle(RMZMessage msg)
        {
            var style = RMZDialogueStyle.Current;
            var (fill, border, nameFill, speaker) = RMZWindowSkin.DialogueColors(msg.SpeakerName, _nameLabel.Text, msg.Background);
            _window.Visible = msg.Background != 2;

            // 鈴音：跟戰後互動視窗同一個魔力外框（藍紫框＋會呼吸的光暈＋亮晶晶）
            bool magic = msg.Background == 0 && RMZMagicFrame.IsSuzune(msg.SpeakerName, _nameLabel.Text);
            if (magic)
            {
                border = RMZMagicFrame.Border; nameFill = RMZMagicFrame.NameFill; speaker = RMZMagicFrame.Speaker;
                _magicBox = RMZMagicFrame.Style(fill, RMZWindowSkin.DialogRadius);
                _window.AddThemeStyleboxOverride("panel", _magicBox);
                if (_magicSparks == null) _magicSparks = RMZMagicFrame.Sparkles(_window, _window.Size, 24);
                _magicSparks.Visible = true;
            }
            else
            {
                _magicBox = null;
                if (_magicSparks != null) _magicSparks.Visible = false;
                _window.AddThemeStyleboxOverride("panel", RMZWindowSkin.PanelStyle(fill, border));
            }

            // 名字框：底色跟著角色走，字用該角色的顏色比較好認人
            _namePanel.AddThemeStyleboxOverride("panel", RMZWindowSkin.PanelStyle(nameFill, border));
            _nameLabel.AddThemeColorOverride("font_color", speaker);

            if (style.TintText) _text.AddThemeColorOverride("font_color", speaker);
            else _text.AddThemeColorOverride("font_color", SkinText);

            // 選項框套同一組底色
            if (style.ApplyToChoiceWindow)
                _choicePanel.AddThemeStyleboxOverride("panel", RMZWindowSkin.PanelStyle(fill, border));
            else
                _choicePanel.AddThemeStyleboxOverride("panel", RMZWindowSkin.PanelStyle());
        }

        StyleBoxFlat _magicBox;
        CpuParticles2D _magicSparks;

        /// <summary>鈴音說話時：光暈一脹一縮，粒子範圍跟著對話框大小。</summary>
        void UpdateMagic()
        {
            if (_magicBox == null) return;
            _magicBox.ShadowSize = RMZMagicFrame.GlowSize();
            RMZMagicFrame.Fit(_magicSparks, _window.Size);
        }

        void ApplyFace(RMZMessage msg)
        {
            bool hasFace = !string.IsNullOrEmpty(msg.FaceName);
            _face.Visible = hasFace;
            if (!hasFace) return;

            var tex = RMZImageCache.LoadFace(msg.FaceName);
            if (tex == null) { _face.Visible = false; return; }

            // MZ 的臉圖是 4x2 格
            int cell = tex.GetWidth() / 4;
            _face.Texture = new AtlasTexture
            {
                Atlas = tex,
                Region = new Rect2((msg.FaceIndex % 4) * cell, (msg.FaceIndex / 4) * cell, cell, cell),
                FilterClip = true,
            };
        }

        void ApplySpeaker(RMZMessage msg)
        {
            string speaker = RMZTextCodes.Convert(msg.SpeakerName);
            _namePanel.Visible = !string.IsNullOrEmpty(speaker);
            _nameLabel.Text = speaker;
        }

        // ---------------- 版面 ----------------

        float Scale => _root.Size.Y / RefHeight;

        void Layout(RMZMessage msg)
        {
            float s = Scale;
            var view = _root.Size;

            if (msg.HasChoices)
            {
                LayoutChoiceGroup(msg, s, view);
                return;
            }

            if (_fitMode)
            {
                LayoutFitMessage(msg, s, view);
                return;
            }

            if (_lineMode)
            {
                LayoutLines(msg, s, view);
                return;
            }

            // MessageCore 改過寬度的（例如確認視窗寬 800）照它的寬度；位移照加
            float w = (RMZMessageCore.Width != RMZMessageCore.DefaultWidth ? RMZMessageCore.Width : RefWidth - 48f) * s;
            float h = MessageHeight * s;

            // PositionType：0=上 1=中 2=下
            float y = msg.PositionType == 0 ? 16f * s
                    : msg.PositionType == 1 ? (view.Y - h) / 2f
                    : view.Y - h - 16f * s;

            _window.Position = new Vector2((view.X - w) / 2f + RMZMessageCore.OffsetX * s, y + RMZMessageCore.OffsetY * s);
            _window.Size = new Vector2(w, h);
            _window.Visible = msg.Background != 2;

            float pad = 18f * s;
            float faceRoom = 0f;
            if (_face.Visible)
            {
                float fs = Mathf.Min(144f * s, h - pad * 2f);
                _face.Position = new Vector2(pad, (h - fs) / 2f);
                _face.Size = new Vector2(fs, fs);
                faceRoom = fs + pad;
            }

            _text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _text.ClipText = false;
            _text.HorizontalAlignment = HorizontalAlignment.Left;
            _text.VerticalAlignment = VerticalAlignment.Top;
            _text.Position = new Vector2(pad + faceRoom, pad);
            _text.Size = new Vector2(w - pad * 2f - faceRoom, h - pad * 2f);

            LayoutNameBox(s);
        }

        // ---------------- 逐行排版（信件、人物介紹）----------------

        /// <summary>
        /// 每一行一個 Label，各自有對齊、字級、顏色、外框。
        /// 行高照 MZ：字級 + 10（RM 字級 26 → 行高 36），這裡字級 24，所以用字級 + 12。
        /// </summary>
        void BuildLines(string raw)
        {
            foreach (var l in _lineLabels) if (IsInstanceValid(l)) l.QueueFree();
            _lineLabels.Clear();
            _lineLengths.Clear();

            bool page = RMZMessageCore.IsPage;
            // 寫在圖片上的字照 RM 預設：白字＋半透明黑框
            var baseColor = page ? Colors.White : SkinText;
            var baseOutline = new Color(0f, 0f, 0f, page ? 0.6f : 0.85f);
            string styled = RMZTextCodes.ConvertKeepStyle(raw, RMZMessageCore.WordWrap);
            var lines = RMZTextCodes.ParseStyledLines(styled, FontSize, baseColor, 4, baseOutline);

            var sb = new System.Text.StringBuilder();
            foreach (var line in lines)
            {
                var label = new Label
                {
                    Text = line.Text,
                    HorizontalAlignment = line.Align,
                    VerticalAlignment = VerticalAlignment.Center,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    VisibleCharactersBehavior = TextServer.VisibleCharactersBehavior.CharsAfterShaping,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                label.AddThemeFontOverride("font", RMZFonts.Main);
                label.AddThemeFontSizeOverride("font_size", line.FontSize);
                label.AddThemeColorOverride("font_color", line.Color);
                label.AddThemeConstantOverride("outline_size", line.OutlineSize);
                label.AddThemeColorOverride("font_outline_color", line.OutlineColor);
                // 自動換行時，行與行的間距也照「字級 + 12」
                float fh = RMZFonts.Main.GetHeight(line.FontSize);
                label.AddThemeConstantOverride("line_spacing", Mathf.Max(0, Mathf.RoundToInt(line.FontSize + 12 - fh)));
                _lines.AddChild(label);
                _lineLabels.Add(label);
                _lineLengths.Add(line.Text.Length);
                sb.Append(line.Text);
            }
            _fullText = sb.ToString();   // 逐行模式只拿來算「總共幾個字」
        }

        /// <summary>
        /// 版面照 MZ＋VisuMZ：寬 = Width、高 = Rows × 36 + 24，水平置中後加 OffsetX，
        /// PositionType 決定上／中／下（貼齊畫面邊緣）再加 OffsetY；字從內距 12px 開始排。
        /// 行數比預設多的（信件、人物介紹）不畫對話框底與名字框，字直接疊在圖片上。
        /// </summary>
        void LayoutLines(RMZMessage msg, float s, Vector2 view)
        {
            bool page = RMZMessageCore.IsPage;
            float w, h, x, y;
            if (RMZMessageCore.IsCustom)
            {
                w = RMZMessageCore.Width;
                h = RMZMessageCore.Rows * 36f + 24f;
                x = (RefWidth - w) / 2f + RMZMessageCore.OffsetX;
                y = msg.PositionType == 0 ? 0f : msg.PositionType == 1 ? (RefHeight - h) / 2f : RefHeight - h;
                y += RMZMessageCore.OffsetY;
            }
            else
            {
                w = RefWidth - 48f;
                h = MessageHeight;
                x = 24f;
                y = msg.PositionType == 0 ? 16f : msg.PositionType == 1 ? (RefHeight - h) / 2f : RefHeight - h - 16f;
            }

            _window.Position = new Vector2(x, y) * s;
            _window.Size = new Vector2(w, h) * s;
            _window.Visible = !page && msg.Background != 2;

            float pad = page ? 12f : 18f;
            float faceRoom = 0f;
            if (page) { _face.Visible = false; _namePanel.Visible = false; }
            else if (_face.Visible)
            {
                float fs = Mathf.Min(144f, h - pad * 2f);
                _face.Position = new Vector2(pad, (h - fs) / 2f) * s;
                _face.Size = new Vector2(fs, fs) * s;
                faceRoom = fs + pad;
            }

            float innerW = (w - pad * 2f - faceRoom) * s;
            _lines.Position = new Vector2(x + pad + faceRoom, y + pad) * s;
            _lines.Size = new Vector2(innerW, (h - pad * 2f) * s);

            float cy = 0f;
            foreach (var label in _lineLabels)
            {
                int size = label.GetThemeFontSize("font_size");
                float row = size + 12f;
                label.Position = new Vector2(0f, cy);
                label.Size = new Vector2(innerW, row);
                int count = Mathf.Max(1, label.GetLineCount());
                label.Size = new Vector2(innerW, row * count);
                cy += row * count;
            }

            LayoutNameBox(s);
        }

        /// <summary>顯示到第 n 個字（逐行模式會把字數分給每一行）。</summary>
        void SetRevealed(int n)
        {
            if (!_lineMode)
            {
                _text.Text = _fullText.Substring(0, Mathf.Clamp(n, 0, _fullText.Length));
                return;
            }
            int offset = 0;
            for (int i = 0; i < _lineLabels.Count; i++)
            {
                int len = _lineLengths[i];
                int vis = Mathf.Clamp(n - offset, 0, len);
                _lineLabels[i].VisibleCharacters = vis >= len ? -1 : vis;
                offset += len;
            }
        }

        /// <summary>
        /// \FIT 專用版面：外框依內容自動貼齊（不是固定的整排寬度），文字整塊置中。
        /// PositionType 照舊決定上／中／下，只是框變小了。
        /// </summary>
        void LayoutFitMessage(RMZMessage msg, float s, Vector2 view)
        {
            // 注意：字型是固定像素字級（FontSize，不會跟著 s 縮放），
            // 所以這裡量出來的寬高「不能」再乘 s，不然視窗縮小了字卻沒縮小，會爆出框外。
            // 置中用的 view 本來就是目前實際的畫布大小，不需要另外換算。
            const float maxWidth = 1100f;
            const float padX = 56f;
            const float padY = 30f;

            var font = RMZFonts.Main;
            var lines = _fullText.Split('\n');
            float lineHeight = font.GetHeight(FontSize) + 4f;   // 用字型自己回報的行高，不要用猜的常數

            float inner = 0f;
            foreach (var line in lines)
                inner = Mathf.Max(inner, font.GetStringSize(line, HorizontalAlignment.Left, -1, FontSize).X);

            float w = Mathf.Min(inner + padX, maxWidth);
            float h = lines.Length * lineHeight + padY;

            float y = msg.PositionType == 0 ? 16f
                    : msg.PositionType == 1 ? (view.Y - h) / 2f
                    : view.Y - h - 16f;

            _window.Position = new Vector2((view.X - w) / 2f, y);
            _window.Size = new Vector2(w, h);
            _window.Visible = msg.Background != 2;

            _face.Visible = false;
            _text.AutowrapMode = TextServer.AutowrapMode.Off;
            _text.ClipText = true;
            _text.HorizontalAlignment = HorizontalAlignment.Center;
            _text.VerticalAlignment = VerticalAlignment.Center;
            _text.Position = new Vector2(16f, 8f);
            _text.Size = _window.Size - new Vector2(32f, 16f);

            LayoutNameBox(s);
        }

        /// <summary>有選項時：問句縮成貼合內容的小框，放在選項框正上方，兩個一起置中。</summary>
        void LayoutChoiceGroup(RMZMessage msg, float s, Vector2 view)
        {
            const float gap = 10f;
            const float maxWidth = 760f;
            const float padX = 40f;
            const float padY = 22f;

            float choiceWidth = _choicePanel.Size.X / s;
            float choiceHeight = _choicePanel.Size.Y / s;

            bool hasText = !string.IsNullOrEmpty(_fullText);
            float messageWidth = 0f, messageHeight = 0f;

            if (hasText)
            {
                // 這裡跟 LayoutFitMessage 用同一套量法：AutowrapMode 是關的，
                // 所以「文字會自動換行、行數要重算」這種假設是錯的——真正的行數
                // 就是 \n 切出來的行數，量寬度也要一行一行量，不能把整段多行文字
                // 丟給 GetStringSize 當一行量（那樣量出來的寬度沒意義，還會反過來
                // 把正確的行數蓋掉，造成框太矮、文字疊在選項上）。
                var font = RMZFonts.Main;
                float lineHeight = font.GetHeight(FontSize) + 4f;
                var lines = _fullText.Split('\n');

                float inner = 0f;
                foreach (var line in lines)
                    inner = Mathf.Max(inner, font.GetStringSize(line, HorizontalAlignment.Left, -1, FontSize).X);

                messageWidth = Mathf.Clamp(Mathf.Min(inner + padX, maxWidth), choiceWidth, maxWidth);
                messageHeight = lines.Length * lineHeight + padY;
            }

            float total = (hasText ? messageHeight + gap : 0f) + choiceHeight;
            float top = view.Y / 2f - (total / 2f - ChoiceGroupDropY) * s;

            if (hasText)
            {
                _window.Visible = msg.Background != 2;
                _window.Size = new Vector2(messageWidth * s, messageHeight * s);
                _window.Position = new Vector2((view.X - _window.Size.X) / 2f, top);

                _face.Visible = false;
                // 關掉自動換行 + 開 ClipText：雙保險，不管 Godot 內部怎麼算「最小高度」，
                // 都不准它把 Label 撐得比我們給的框還大。
                _text.AutowrapMode = TextServer.AutowrapMode.Off;
                _text.ClipText = true;
                _text.HorizontalAlignment = HorizontalAlignment.Center;
                _text.VerticalAlignment = VerticalAlignment.Center;
                _text.Position = new Vector2(16f * s, 8f * s);
                _text.Size = _window.Size - new Vector2(32f * s, 16f * s);
            }
            else _window.Visible = false;

            _choicePanel.Position = new Vector2(
                (view.X - _choicePanel.Size.X) / 2f,
                top + (hasText ? (messageHeight + gap) * s : 0f));

            LayoutNameBox(s);
        }

        void LayoutNameBox(float s)
        {
            if (!_namePanel.Visible) return;

            var size = RMZFonts.Main.GetStringSize(_nameLabel.Text, HorizontalAlignment.Left, -1, NameFontSize);
            float w = (size.X + 32f) * s;
            float h = 40f * s;

            _namePanel.Size = new Vector2(w, h);
            _namePanel.Position = new Vector2(_window.Position.X + 16f * s, _window.Position.Y - h + 4f * s);
            _nameLabel.Position = Vector2.Zero;
            _nameLabel.Size = _namePanel.Size;
        }

        void BuildChoices(RMZMessage msg)
        {
            foreach (var row in _choiceRows) if (IsInstanceValid(row)) row.QueueFree();
            _choiceRows.Clear();
            _choiceLabels.Clear();
            _choiceResults.Clear();
            _choiceSelectable.Clear();

            _choicePanel.Visible = msg.HasChoices;
            if (!msg.HasChoices) return;

            float s = Scale;
            var font = RMZFonts.Main;
            float width = 0f;

            // 列表：原本的選項＋（取消類型是「分支」時）多一列「取消」，不然只能按 ESC 才取消得了
            var labels = new List<string>();
            for (int i = 0; i < msg.Choices.Count; i++)
            {
                // VisuMZ 的 <Disable>：選項照樣顯示但變暗、選不了（教學時用來鎖住某個選項）
                string raw = msg.Choices[i] ?? "";
                bool disabled = raw.IndexOf("<Disable>", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (disabled) raw = System.Text.RegularExpressions.Regex.Replace(raw, "<Disable>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                string text = RMZTextCodes.Convert(raw);
                labels.Add(text);
                _choiceResults.Add(i);
                _choiceSelectable.Add(!disabled && !SeparatorRow.IsMatch(text.Trim()));
            }
            if (msg.ChoiceCancel == -2)
            {
                labels.Add("取消");
                _choiceResults.Add(-2);
                _choiceSelectable.Add(true);
            }

            for (int i = 0; i < labels.Count; i++)
            {
                string label = labels[i];
                width = Mathf.Max(width, font.GetStringSize(label, HorizontalAlignment.Left, -1, FontSize).X + 96f);

                var row = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
                row.AddThemeStyleboxOverride("panel", RowStyle(false));
                _choicePanel.AddChild(row);

                var text = new Label
                {
                    Text = label,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                StyleText(text, FontSize, SkinText);
                row.AddChild(text);

                _choiceRows.Add(row);
                _choiceLabels.Add(text);
            }

            float height = labels.Count * ChoiceLineHeight + 24f;
            _choicePanel.Size = new Vector2(width * s, height * s);

            for (int i = 0; i < _choiceRows.Count; i++)
            {
                var row = _choiceRows[i];
                row.Position = new Vector2(8f * s, (12f + i * ChoiceLineHeight) * s);
                row.Size = new Vector2(_choicePanel.Size.X - 16f * s, (ChoiceLineHeight - 8f) * s);
                _choiceLabels[i].Position = Vector2.Zero;
                _choiceLabels[i].Size = row.Size;
            }

            // 預設停在分隔線上的話，往下找第一個能選的
            if (_choiceIndex >= _choiceRows.Count || !_choiceSelectable[_choiceIndex]) _choiceIndex = StepChoice(_choiceIndex, 1, true);
            HighlightChoice();
        }

        /// <summary>往上／下移一格，跳過分隔線。fromInvalid = 目前那格本身也要檢查。</summary>
        int StepChoice(int from, int dir, bool fromInvalid = false)
        {
            int count = _choiceRows.Count;
            if (count == 0) return 0;
            int i = Mathf.Clamp(from, 0, count - 1);
            if (fromInvalid && _choiceSelectable[i]) return i;
            for (int n = 0; n < count; n++)
            {
                i = (i + dir + count) % count;
                if (_choiceSelectable[i]) return i;
            }
            return from;
        }

        void HighlightChoice()
        {
            for (int i = 0; i < _choiceRows.Count; i++)
            {
                bool on = i == _choiceIndex;
                bool sep = !_choiceSelectable[i];
                // 分隔線：沒有底、字變暗，看起來就是一條線
                _choiceRows[i].AddThemeStyleboxOverride("panel", sep ? new StyleBoxEmpty() : RowStyle(on));
                _choiceLabels[i].AddThemeColorOverride("font_color",
                    sep ? new Color(SkinText.R, SkinText.G, SkinText.B, 0.35f) : on ? SkinTextSelected : SkinText);
            }
        }

        // ---------------- 逐字顯示 ----------------

        void UpdateReveal(float delta)
        {
            if (_revealed >= _fullText.Length)
            {
                if (!_waitingConfirm) _confirmSince = Now;
                _waitingConfirm = true;
                return;
            }

            float speed = RMZFastForward.CharsPerSecond;   // 平常 60（MZ 的一幀一字），快轉時更快或直接整段
            _revealed = float.IsPositiveInfinity(speed)
                ? _fullText.Length
                : Mathf.Min(_revealed + speed * delta, _fullText.Length);

            SetRevealed((int)_revealed);

            if (_revealed >= _fullText.Length)
            {
                _waitingConfirm = true;
                _confirmSince = Now;
            }
        }

        static float Now => Time.GetTicksMsec() / 1000f;
        float _confirmSince;

        // ---------------- 輸入 ----------------

        void UpdateInput(RMZMessage msg)
        {
            // 剛開出來的那一幀不吃輸入（觸發事件的那一下按鍵還在）
            if (Engine.GetProcessFrames() == _appliedFrame)
            {
                _lastMouseLeft = Input.IsMouseButtonPressed(MouseButton.Left);
                return;
            }

            bool ok = RMZEventRunner.ActionPressed();
            bool cancel = RMZEventRunner.CancelPressed();

            bool mouseNow = Input.IsMouseButtonPressed(MouseButton.Left);
            bool mouseClick = mouseNow && !_lastMouseLeft;
            _lastMouseLeft = mouseNow;
            var mousePosition = _root.GetGlobalMousePosition();
            bool mouseMoved = mousePosition != _lastChoiceMousePosition;
            _lastChoiceMousePosition = mousePosition;

            // 文字還沒跑完，按確定（或點畫面）就整段跳出來
            if (!_waitingConfirm)
            {
                if (ok || mouseClick) { _revealed = _fullText.Length; SetRevealed(_fullText.Length); _waitingConfirm = true; }
                return;
            }

            // 快轉：整段顯示完、停留 HoldSeconds 之後自動翻頁（有選項時不自動選）
            if (RMZFastForward.Active && !msg.HasChoices &&
                Now - _confirmSince >= Mathf.Max(0f, RMZFastForward.Settings.HoldSeconds))
            {
                msg.Clear();
                return;
            }

            if (msg.HasChoices)
            {
                bool down = Input.IsActionJustPressed("ui_down");
                bool up = Input.IsActionJustPressed("ui_up");
                if (down)
                { _choiceIndex = StepChoice(_choiceIndex, 1); HighlightChoice(); }
                else if (up)
                { _choiceIndex = StepChoice(_choiceIndex, -1); HighlightChoice(); }

                // 只有實際移動或點擊才切換；同幀鍵盤操作優先於懸停。分隔線不能選
                int hovered = ChoiceAtMouse();
                if (hovered >= 0 && !_choiceSelectable[hovered]) hovered = -1;
                if (hovered >= 0 && hovered != _choiceIndex &&
                    (mouseClick || (mouseMoved && !down && !up && !ok && !cancel)))
                {
                    _choiceIndex = hovered;
                    HighlightChoice();
                }

                if ((ok || (mouseClick && hovered >= 0)) && _choiceIndex < _choiceResults.Count && _choiceSelectable[_choiceIndex])
                {
                    int result = _choiceResults[_choiceIndex];
                    msg.ChoiceResult = result;
                    msg.OnChoice?.Invoke(result);
                    msg.Clear();
                }
                // 取消：-1 = 不允許；-2 = 走「取消」分支；其他 = 當成選了那一項（跟 MZ 一樣）
                else if (cancel && msg.ChoiceCancel != -1)
                {
                    _escapeFrame = Engine.GetProcessFrames();
                    msg.ChoiceResult = msg.ChoiceCancel;
                    msg.OnChoice?.Invoke(msg.ChoiceCancel);
                    msg.Clear();
                }
                return;
            }

            if (ok || mouseClick) msg.Clear();
        }

        int ChoiceAtMouse()
        {
            if (!_choicePanel.Visible) return -1;
            var mouse = _root.GetGlobalMousePosition();

            for (int i = 0; i < _choiceRows.Count; i++)
            {
                var row = _choiceRows[i];
                var rect = new Rect2(_choicePanel.Position + row.Position, row.Size);
                if (rect.HasPoint(mouse)) return i;
            }
            return -1;
        }
    }
}
