// 系統選單：任何時候按 Esc 都叫得出來。
//
// 為什麼不用 Engine.TimeScale = 0 來暫停：
//   快轉那支（RMZFastForwardDriver）每幀都會寫 TimeScale，會跟這裡打架。
//   所以改成掛旗標 —— 事件、玩家移動都看 RMZSystemMenu.IsOpen。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public partial class RMZSystemMenu : CanvasLayer
    {
        public static RMZSystemMenu Instance { get; private set; }

        /// <summary>選單開著。事件、玩家移動、快轉都靠這個停下來。</summary>
        public static bool IsOpen => Instance != null && Instance._open;

        enum Item { Resume = 0, Codex = 1, Options = 2, Title = 3, Quit = 4 }

        static readonly string[] Labels = { "繼續遊戲", "技能圖鑑", "設置", "回到標題畫面", "退出遊戲" };

        const float PanelWidth = 340f;
        const float RowHeight = 52f;
        const float RefHeight = 720f;

        Control _root;
        ColorRect _dim;
        Panel _panel;
        Label _title;
        readonly List<Panel> _rows = new List<Panel>();
        readonly List<Label> _rowLabels = new List<Label>();

        Panel _confirm;
        Label _confirmText;
        readonly List<Panel> _confirmRows = new List<Panel>();
        readonly List<Label> _confirmLabels = new List<Label>();

        bool _open;
        int _index;
        Item _pending;
        bool _confirming;

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            Layer = 50;              // 蓋在所有東西上面
            ProcessPriority = 400;
            ProcessMode = ProcessModeEnum.Always;
            Build();
            _root.Visible = false;
        }

        // ---------------- 版面 ----------------

        void Build()
        {
            _root = new Control { Name = "Root" };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_root);

            // 全螢幕暗底，順便擋掉底下的點擊
            _dim = new ColorRect { Name = "Dim", Color = new Color(0f, 0f, 0f, 0.66f) };
            _dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(_dim);

            _panel = new Panel { Name = "Panel" };
            _panel.AddThemeStyleboxOverride("panel", RMZWindowSkin.PanelStyle(new Color(0.09f, 0.08f, 0.10f, 0.98f)));
            _root.AddChild(_panel);

            _title = NewLabel("系統選單", RMZFonts.DefaultSize, new Color(1f, 0.9f, 0.7f));
            _panel.AddChild(_title);

            for (int i = 0; i < Labels.Length; i++)
            {
                var row = new Panel();
                row.AddThemeStyleboxOverride("panel", RMZWindowSkin.RowStyle(false));
                _panel.AddChild(row);
                _rows.Add(row);

                var label = NewLabel(Labels[i], RMZFonts.DefaultSize, RMZWindowSkin.Text);
                row.AddChild(label);
                _rowLabels.Add(label);
            }

            // ---- 確認框（回標題／退出遊戲）----
            _confirm = new Panel { Name = "Confirm", Visible = false };
            _confirm.AddThemeStyleboxOverride("panel", RMZWindowSkin.PanelStyle(new Color(0.09f, 0.08f, 0.10f, 0.98f)));
            _root.AddChild(_confirm);

            _confirmText = NewLabel("", RMZFonts.DefaultSize, RMZWindowSkin.Text);
            _confirmText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _confirm.AddChild(_confirmText);

            foreach (var text in new[] { "確定", "取消" })
            {
                var row = new Panel();
                row.AddThemeStyleboxOverride("panel", RMZWindowSkin.RowStyle(false));
                _confirm.AddChild(row);
                _confirmRows.Add(row);

                var label = NewLabel(text, RMZFonts.DefaultSize, RMZWindowSkin.Text);
                row.AddChild(label);
                _confirmLabels.Add(label);
            }
        }

        static Label NewLabel(string text, int size, Color color)
        {
            var label = new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            RMZWindowSkin.StyleLabel(label, size, color);
            return label;
        }

        void Layout()
        {
            float s = _root.Size.Y / RefHeight;
            if (s <= 0f) s = 1f;
            var view = _root.Size;

            var size = new Vector2(PanelWidth, RowHeight * Labels.Length + 92f) * s;
            _panel.Size = size;
            _panel.Position = (view - size) / 2f;

            _title.Position = new Vector2(12f * s, 14f * s);
            _title.Size = new Vector2(size.X - 24f * s, 32f * s);

            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Position = new Vector2(16f * s, (52f + i * RowHeight) * s);
                _rows[i].Size = new Vector2(size.X - 32f * s, (RowHeight - 8f) * s);
                _rowLabels[i].Position = Vector2.Zero;
                _rowLabels[i].Size = _rows[i].Size;
            }

            var csize = new Vector2(420f, 240f) * s;
            _confirm.Size = csize;
            _confirm.Position = (view - csize) / 2f;

            _confirmText.Position = new Vector2(20f * s, 20f * s);
            _confirmText.Size = new Vector2(csize.X - 40f * s, csize.Y - 100f * s);

            for (int i = 0; i < _confirmRows.Count; i++)
            {
                float w = (csize.X - 60f * s) / 2f;
                _confirmRows[i].Position = new Vector2(20f * s + i * (w + 20f * s), csize.Y - 68f * s);
                _confirmRows[i].Size = new Vector2(w, 48f * s);
                _confirmLabels[i].Position = Vector2.Zero;
                _confirmLabels[i].Size = _confirmRows[i].Size;
            }
        }

        // ---------------- 開關 ----------------

        bool _fromButton;

        /// <summary>
        /// 右上角圖片選單的「離開」鈕（RM 是 callScene Scene_GameEnd）：
        /// 打開這個系統選單，游標直接停在「回到標題畫面」，下面一格就是「退出遊戲」。
        /// </summary>
        public void ShowFromExitButton()
        {
            Show();
            _fromButton = true;
            _index = (int)Item.Title;
            _lastClick = true;           // 按離開鈕的那一下不要順便點到選單
            Refresh();
        }

        public void Show()
        {
            _open = true;
            _confirming = false;
            _index = 0;
            Layout();
            _root.Visible = true;
            Refresh();
            RMZAudio.Instance?.PlaySeByName("Cursor1");
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            _confirming = false;
            _fromButton = false;
            _root.Visible = false;
        }

        void AskConfirm(Item item, string text)
        {
            _pending = item;
            _confirming = true;
            _index = 1;                  // 預設停在「取消」，免得手滑
            _confirmText.Text = text;
            Refresh();
        }

        void Refresh()
        {
            _confirm.Visible = _confirming;
            _panel.Visible = !_confirming;

            for (int i = 0; i < _rows.Count; i++)
            {
                bool on = !_confirming && i == _index;
                _rows[i].AddThemeStyleboxOverride("panel", RMZWindowSkin.RowStyle(on));
                _rowLabels[i].AddThemeColorOverride("font_color",
                    on ? RMZWindowSkin.TextSelected : RMZWindowSkin.Text);
            }

            for (int i = 0; i < _confirmRows.Count; i++)
            {
                bool on = _confirming && i == _index;
                _confirmRows[i].AddThemeStyleboxOverride("panel", RMZWindowSkin.RowStyle(on));
                _confirmLabels[i].AddThemeColorOverride("font_color",
                    on ? RMZWindowSkin.TextSelected : RMZWindowSkin.Text);
            }
        }

        // ---------------- 輸入 ----------------

        public override void _Process(double delta)
        {
            if (!_open)
            {
                if (Input.IsActionJustPressed("ui_cancel") && !SomethingElseOwnsEscape()) Show();
                return;
            }

            // 設置與圖鑑是從這裡開出去的，開著時先把自己藏起來
            // （_open 維持著，暫停不會鬆掉）
            bool sub = RMZOptionsScene.IsOpen || RMZSkillCodex.IsOpen;
            if (_root.Visible == sub) _root.Visible = !sub;
            if (sub) return;

            // 從離開鈕打開的時候，畫面上的圖片選單還開著，不能因為它就不理按鍵
            if (!_fromButton && SomethingElseOwnsEscape()) return;

            if (Input.IsActionJustPressed("ui_cancel"))
            {
                if (_confirming) { _confirming = false; _index = (int)_pending; Refresh(); }
                else Close();
                return;
            }

            int max = _confirming ? 1 : Labels.Length - 1;
            if (Input.IsActionJustPressed("ui_down")) { _index = Mathf.Min(max, _index + 1); Refresh(); }
            if (Input.IsActionJustPressed("ui_up")) { _index = Mathf.Max(0, _index - 1); Refresh(); }

            if (_confirming)
            {
                if (Input.IsActionJustPressed("ui_left")) { _index = 0; Refresh(); }
                if (Input.IsActionJustPressed("ui_right")) { _index = 1; Refresh(); }
            }

            // 滑鼠：移到哪一列就選哪一列，點一下就是確定
            int hovered = RowAtMouse();
            if (hovered >= 0 && hovered != _index) { _index = hovered; Refresh(); }

            bool click = Input.IsMouseButtonPressed(MouseButton.Left);
            bool mouseConfirm = click && !_lastClick && hovered >= 0;
            _lastClick = click;

            if (Input.IsActionJustPressed("ui_accept") || mouseConfirm)
            {
                if (_confirming) ConfirmChoice(_index == 0);
                else Activate();
            }
        }

        bool _lastClick;

        int RowAtMouse()
        {
            var mouse = _root.GetGlobalMousePosition();
            var rows = _confirming ? _confirmRows : _rows;
            var origin = _confirming ? _confirm.Position : _panel.Position;

            for (int i = 0; i < rows.Count; i++)
                if (new Rect2(origin + rows[i].Position, rows[i].Size).HasPoint(mouse)) return i;
            return -1;
        }

        /// <summary>別的視窗開著時，Esc 是它的，不要順手把選單也叫出來／關掉。</summary>
        static bool SomethingElseOwnsEscape() =>
            RMZMessage.Instance.IsBusy
            // 對話框這一幀剛用 ESC 取消了選項（它比選單早處理，訊息已經清掉了）
            || RMZMessageWindow.EscapeUsedThisFrame
            // 跟 MZ 一樣：劇情事件在跑的時候不能開選單（開頭選難度、跳不跳劇情那段）
            || (RMZEventRunner.Instance != null && RMZEventRunner.Instance.IsBusy)
            || (RMZInputWindow.Instance != null && RMZInputWindow.Instance.IsBusy)
            || RMZSaveScene.IsOpen
            || RMZItemShop.IsOpen
            || RMZDevPanel.IsOpen
            || RMZEnhanceWindow.IsOpen
            || RMZTavernWindow.IsOpen
            || RMZSlaveWorkWindow.IsOpen
            || RMZSlaveShopWindow.IsOpen
            || RMZTemptWindow.IsOpen
            || RMZUnionWindow.IsOpen
            || RMZGuildPanels.IsOpen
            // 移植版自己的視窗：換裝、成就、結算、教學提示
            || RMZHeroineGearPanel.IsOpen
            || RMZAchievementBrowser.IsOpen
            || RMZLootReport.IsOpen
            || RMZTutorialOverlay.IsOpen
            // RM 圖片做的介面（背包、H 狀態、道具、選隊伍、右上角選單…）有離開／返回鈕的時候
            || RMZPictureInput.PictureScreenOwnsEscape();

        void Activate()
        {
            var item = (Item)Mathf.Clamp(_index, 0, Labels.Length - 1);
            RMZAudio.Instance?.PlaySeByName("Decision1");

            switch (item)
            {
                case Item.Resume:
                    Close();
                    return;

                case Item.Codex:
                    // 圖鑑自己會用 Esc 關掉，關掉之後選單還在
                    RMZSkillCodex.Open();
                    return;

                case Item.Options:
                    RMZOptionsScene.Open();
                    return;

                case Item.Title:
                    AskConfirm(item, "回到標題畫面？\n\n這一輪的進度會結束，沒存到檔的部分會消失。");
                    return;

                case Item.Quit:
                    AskConfirm(item, "離開遊戲？\n\n沒存到檔的部分會消失。");
                    return;
            }
        }

        void ConfirmChoice(bool yes)
        {
            _confirming = false;

            if (!yes)
            {
                _index = (int)_pending;
                Refresh();
                return;
            }

            if (_pending == Item.Quit)
            {
                GetTree().Quit();
                return;
            }

            // 回標題：把跨場景的狀態清乾淨再重載場景
            Close();
            RMZGameState.Clear();
            RMZActors.Clear();
            RMZEquipment.Clear();
            RMZParty.ClearBags();
            RMZImageCache.Clear();
            CharacterSpriteView.ClearCache();
            RMZDatabase.ClearMapCache();
            GetTree().ReloadCurrentScene();
        }
    }
}
