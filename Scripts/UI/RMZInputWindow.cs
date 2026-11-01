// 名字輸入與數值輸入，對應 MZ 的事件指令 303（名稱輸入處理）與 103（數值輸入處理），
// 以及事件腳本裡用 prompt() 讓玩家自己取名的那一套。
//
// 用文字輸入框而不是 MZ 內建的選字盤，理由跟你裝 RealKeyboardInput.js 一樣：
// 內建字盤只能用方向鍵選字、打不了中文。LineEdit 走的是作業系統的輸入法，
// 中文、日文、英數都能直接打。
using Godot;

namespace RMZ
{
    public partial class RMZInputWindow : CanvasLayer
    {
        const float RefWidth = 1280f;
        const float RefHeight = 720f;

        public static RMZInputWindow Instance { get; private set; }

        /// <summary>名字的最大字數上限（中文一字算一個）</summary>
        public int NameMaxLength = 16;

        public string PromptName = "請輸入名字";
        public string PromptNumber = "請輸入數字";
        public static int DisplayVariable;
        public static string DisplayLabel = "";
        public string ConfirmText = "確定";

        enum Mode { None, Name, Number }

        Mode _mode = Mode.None;
        int _actorId;
        int _variableId;
        int _digits;

        Control _root;
        Panel _panel;
        Label _prompt;
        LineEdit _field;
        Button _confirm;

        // 前面劇情常常按著空白鍵快轉，這個視窗一跳出來，那根手指多半還沒放開。
        // 如果直接把焦點交給輸入框，殘留的按鍵事件會被當成打字（多打一個空白）
        // 或直接誤觸「確定」。這裡先不給輸入框焦點，等空白鍵（以及其他快轉鍵）
        // 真的放開一次之後，才真正打開輸入——等於強制中斷一次原本按住的狀態。
        bool _waitingKeyRelease;

        public bool IsBusy => _mode != Mode.None;

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            Layer = 20;              // 壓在對話框（10）之上
            ProcessPriority = 310;
            BuildUI();
            _root.Visible = false;
        }

        void BuildUI()
        {
            _root = new Control { Name = "Root" };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(_root);

            _panel = new Panel { Name = "InputPanel" };
            // 跟對話框「暗色底」（background=1）用同一種黑，不要用預設的深藍底，
            // 不然難度說明是黑的、這個是藍黑色，兩個擺在一起色調會對不起來。
            _panel.AddThemeStyleboxOverride("panel",
                RMZWindowSkin.PanelStyle(new Color(0f, 0f, 0f, RMZDialogueStyle.Current.DimOpacity)));
            _root.AddChild(_panel);

            _prompt = new Label
            {
                Name = "Prompt",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            RMZWindowSkin.StyleLabel(_prompt, RMZFonts.DefaultSize, RMZWindowSkin.Text);
            _panel.AddChild(_prompt);

            _field = new LineEdit
            {
                Name = "Field",
                Alignment = HorizontalAlignment.Center,
                CaretBlink = true,
            };
            _field.AddThemeFontOverride("font", RMZFonts.Main);
            _field.AddThemeFontSizeOverride("font_size", RMZFonts.DefaultSize);
            _field.AddThemeColorOverride("font_color", RMZWindowSkin.Text);
            _field.TextSubmitted += _ => Confirm();
            _panel.AddChild(_field);

            _confirm = new Button { Name = "Confirm", Text = ConfirmText, FocusMode = Control.FocusModeEnum.None };
            _confirm.AddThemeFontOverride("font", RMZFonts.Main);
            _confirm.AddThemeFontSizeOverride("font_size", RMZFonts.DefaultSize);
            _confirm.AddThemeStyleboxOverride("normal", RMZWindowSkin.PanelStyle(new Color(0.13f, 0.15f, 0.20f, 0.95f)));
            _confirm.AddThemeStyleboxOverride("hover", RMZWindowSkin.PanelStyle(new Color(0.79f, 0.66f, 0.36f, 0.22f)));
            _confirm.AddThemeStyleboxOverride("pressed", RMZWindowSkin.PanelStyle(new Color(0.79f, 0.66f, 0.36f, 0.32f)));
            _confirm.AddThemeStyleboxOverride("focus", RMZWindowSkin.PanelStyle(new Color(0.79f, 0.66f, 0.36f, 0.22f)));
            _confirm.AddThemeColorOverride("font_color", RMZWindowSkin.Text);
            _confirm.AddThemeColorOverride("font_hover_color", RMZWindowSkin.TextSelected);
            _confirm.AddThemeColorOverride("font_pressed_color", RMZWindowSkin.TextSelected);
            _confirm.AddThemeColorOverride("font_focus_color", RMZWindowSkin.TextSelected);
            _confirm.Pressed += Confirm;
            _panel.AddChild(_confirm);

            Layout();
        }

        void Layout()
        {
            float s = _root.Size.Y / RefHeight;
            if (s <= 0f) s = 1f;

            var size = new Vector2(560f, 252f) * s;
            _panel.Size = size;
            _panel.Position = (_root.Size - size) / 2f;

            _prompt.Position = new Vector2(20f * s, 18f * s);
            _prompt.Size = new Vector2(size.X - 40f * s, 66f * s);

            _field.Position = new Vector2(40f * s, 100f * s);
            _field.Size = new Vector2(size.X - 80f * s, 52f * s);

            _confirm.Size = new Vector2(160f * s, 48f * s);
            _confirm.Position = new Vector2((size.X - _confirm.Size.X) / 2f, 176f * s);
        }

        // ---------------- 對外 ----------------

        public void OpenName(int actorId, int maxLength) => OpenName(actorId, maxLength, null, null);

        /// <summary>
        /// 名字輸入。prompt 傳 null 就用預設提示；initial 傳 null 表示沿用角色現在的名字，
        /// 傳空字串表示一開始就是空的（事件裡的 prompt() 取名是這種）。
        /// </summary>
        public void OpenName(int actorId, int maxLength, string prompt, string initial)
        {
            var actor = RMZDatabase.Actor(actorId);
            _mode = Mode.Name;
            _actorId = actorId;

            _prompt.Text = string.IsNullOrEmpty(prompt) ? PromptName : prompt;
            _field.MaxLength = Mathf.Clamp(maxLength > 0 ? maxLength : NameMaxLength, 1, NameMaxLength);
            _field.Text = initial ?? (actor != null ? actor.name : "");

            Show();
        }

        public void OpenNumber(int variableId, int digits)
        {
            _mode = Mode.Number;
            _variableId = variableId;
            _digits = Mathf.Clamp(digits, 1, 8);

            _prompt.Text = PromptNumber + "\n" + (DisplayVariable > 0
                ? RMZTextCodes.Convert(DisplayLabel) + RMZGameState.Variable(DisplayVariable).ToString("N0") + " G"
                : "持有金額：" + RMZParty.Gold.ToString("N0") + " G");
            _field.MaxLength = _digits;
            _field.Text = RMZGameState.Variable(variableId).ToString();

            Show();
        }

        void Show()
        {
            Layout();
            _prompt.AddThemeColorOverride("font_color", RMZWindowSkin.Text);
            _root.Visible = true;

            if (RMZFastForward.Held)
            {
                // 空白鍵（或其他快轉鍵）還按著：先鎖住輸入框，等放開再開放打字。
                _waitingKeyRelease = true;
                _field.Editable = false;
                _confirm.Disabled = true;
                _field.ReleaseFocus();
            }
            else
            {
                _waitingKeyRelease = false;
                _field.Editable = true;
                _confirm.Disabled = false;
                _field.GrabFocus();
                _field.SelectAll();
            }
        }

        public override void _Process(double delta)
        {
            if (!_waitingKeyRelease || RMZFastForward.Held) return;

            _waitingKeyRelease = false;
            _field.Editable = true;
            _confirm.Disabled = false;
            _field.GrabFocus();
            _field.SelectAll();
        }

        void Confirm()
        {
            if (_mode == Mode.None) return;

            if (_mode == Mode.Name)
            {
                string name = _field.Text.Trim();

                // 原本事件裡的 JS 是 while 迴圈問到有字為止，這裡照做：空的就不給關
                if (name.Length == 0)
                {
                    _prompt.AddThemeColorOverride("font_color", RMZWindowSkin.TextSelected);
                    _field.GrabFocus();
                    return;
                }

                var actor = RMZDatabase.Actor(_actorId);
                if (actor != null) actor.name = name;
            }
            else
            {
                int max = (int)Mathf.Pow(10, _digits) - 1;
                if (!int.TryParse(_field.Text, out int value) || value < 0 || value > max)
                { _prompt.Text = "請輸入 0～" + max + " 的整數"; _field.GrabFocus(); return; }
                RMZGameState.SetVariable(_variableId, Mathf.Clamp(value, 0, max));
            }

            _mode = Mode.None;
            _field.ReleaseFocus();
            _root.Visible = false;
        }
    }
}
