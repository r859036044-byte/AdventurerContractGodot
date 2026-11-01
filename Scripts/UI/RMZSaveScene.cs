// 存檔／讀檔畫面，對應 MZ 的 Scene_Save / Scene_Load。
//
// 不是真的場景，只是蓋在最上層的一層 CanvasLayer；開著的時候地圖與事件都停住
// （RMZEventRunner／RMZSystemMenu 都看 IsOpen）。
//
// 版面（以 1280×720 為基準，整塊跟著視窗等比縮放）：
//   普通模式：左邊一張大卡是自動存檔（主角立繪＋小人），手動存檔不能覆蓋它；
//             右邊 4×2 的方塊是手動存檔，每格都有存檔當下主角的小人，可以翻頁。
//   挑戰模式（開關 83）：只有一格挑戰存檔。自動存檔寫這格，也可以手動覆蓋；
//             和普通模式的存檔完全分開，兩邊互相看不到。
//
// 事件的接法：
//   指令 352「開啟存檔畫面」        → Open(Mode.Save)（RMZInterpreter）
//   Hendrix callScene Scene_Load    → Open(Mode.Load)（RMZPluginCommands）
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public partial class RMZSaveScene : CanvasLayer
    {
        public enum Mode { Save, Load }

        public static RMZSaveScene Instance { get; private set; }

        /// <summary>畫面開著。直譯器靠這個等玩家做完選擇。</summary>
        public static bool IsOpen => Instance != null && Instance._open;

        const float RefW = 1280f, RefH = 720f;
        const int Cols = 4, Rows = 2, PageSize = Cols * Rows;
        const int BigIndex = -1;
        const int TitleMapId = 3;                      // 標題畫面是 Map003                       // 選到左邊（或挑戰模式唯一）那張大卡

        // ---- 冒險者主題配色：皮革棕底、古銅金框、羊皮紙色字；面板與方塊 85% 不透明 ----
        const float Opacity = 0.85f;
        static readonly Color Leather = new Color(0.16f, 0.11f, 0.08f, Opacity);       // 方塊底
        static readonly Color LeatherLit = new Color(0.30f, 0.21f, 0.11f, Opacity);    // 選中的方塊底
        static readonly Color Bronze = new Color(0.55f, 0.42f, 0.24f, 1f);             // 手動存檔框
        static readonly Color GoldLine = new Color(0.91f, 0.76f, 0.42f, 1f);           // 選中框
        static readonly Color AutoLine = new Color(0.36f, 0.74f, 0.52f, 1f);           // 自動存檔框（翡翠綠）
        static readonly Color ChallengeLine = new Color(0.78f, 0.33f, 0.28f, 1f);      // 挑戰存檔框（暗紅）
        static readonly Color Parchment = new Color(0.95f, 0.89f, 0.76f);              // 內文
        static readonly Color TitleGold = new Color(1f, 0.86f, 0.52f);
        static readonly Color Dim = new Color(0.62f, 0.55f, 0.45f);
        static readonly Color Warn = new Color(1f, 0.72f, 0.5f);

        class Card
        {
            public Panel Box;
            public Label Head, Info, Note;
            public TextureRect Portrait, Chibi;
        }

        Control _root, _stage;
        Label _title, _hint, _pageLabel;
        Panel _prev, _next;
        Card _big;
        readonly List<Card> _tiles = new List<Card>();

        Mode _mode;
        bool _open, _challenge;
        int _page, _sel;
        RMZSaveHeader _bigHeader;
        List<RMZSaveHeader> _pageHeaders = new List<RMZSaveHeader>();

        static int PageCount => (RMZSaveSystem.ManualCount + PageSize - 1) / PageSize;

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            Layer = 46; // 圖片、對話框之上；系統選單(50)、技能圖鑑(48)之下
            ProcessPriority = 400;
            ProcessMode = ProcessModeEnum.Always;
            Build();
            _root.Visible = false;
        }

        public static void Open(Mode mode)
        {
            if (Instance == null) return;
            Instance.Show(mode);
        }

        // ---------------- 版面 ----------------

        void Build()
        {
            _root = new Control { Name = "Root" };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_root);

            // 半透明黑底，同時擋住滑鼠（MouseFilter 預設 Stop），點不到底下的地圖
            var dim = new ColorRect { Name = "Dim", Color = new Color(0.05f, 0.03f, 0.02f, 0.55f) };
            dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(dim);

            _stage = new Control { Name = "Stage", Size = new Vector2(RefW, RefH), MouseFilter = Control.MouseFilterEnum.Ignore };
            _root.AddChild(_stage);

            _title = AddLabel(_stage, "", new Rect2(0, 26, RefW, 48), 34, TitleGold);
            _hint = AddLabel(_stage, "", new Rect2(0, 682, RefW, 28), 18, Parchment);

            // 左邊大卡（普通模式＝自動存檔；挑戰模式＝唯一那格，會搬到正中間）
            _big = new Card { Box = AddPanel(_stage, new Rect2(70, 96, 360, 548)) };
            _big.Head = AddLabel(_big.Box, "", new Rect2(0, 12, 360, 36), 26, TitleGold);
            _big.Chibi = AddImage(_big.Box, new Rect2(84, 78, 192, 192), true);
            _big.Info = AddLabel(_big.Box, "", new Rect2(16, 300, 328, 130), 22, Parchment);
            _big.Note = AddLabel(_big.Box, "", new Rect2(10, 490, 340, 30), 18, Warn);

            // 右邊 4×2 手動存檔
            const float gx = 460f, gy = 96f, tw = 178f, th = 254f, gapX = 12f, gapY = 20f;
            for (int i = 0; i < PageSize; i++)
            {
                int c = i % Cols, r = i / Cols;
                var card = new Card { Box = AddPanel(_stage, new Rect2(gx + c * (tw + gapX), gy + r * (th + gapY), tw, th)) };
                card.Head = AddLabel(card.Box, "", new Rect2(0, 8, tw, 28), 20, TitleGold);
                card.Chibi = AddImage(card.Box, new Rect2((tw - 96) / 2, 40, 96, 96), true);
                card.Info = AddLabel(card.Box, "", new Rect2(6, 142, tw - 12, 104), 17, Parchment);
                _tiles.Add(card);
            }

            // 翻頁列
            _prev = AddPanel(_stage, new Rect2(744, 638, 56, 38));
            AddLabel(_prev, "◀", new Rect2(0, 0, 56, 38), 22, Parchment);
            _pageLabel = AddLabel(_stage, "", new Rect2(800, 638, 68, 38), 20, Parchment);
            _next = AddPanel(_stage, new Rect2(868, 638, 56, 38));
            AddLabel(_next, "▶", new Rect2(0, 0, 56, 38), 22, Parchment);
        }

        static Label AddLabel(Control parent, string text, Rect2 rect, int size, Color color)
        {
            var label = new Label
            {
                Text = text,
                Position = rect.Position,
                Size = rect.Size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.Off,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            RMZWindowSkin.StyleLabel(label, size, color);
            parent.AddChild(label);
            return label;
        }

        static Panel AddPanel(Control parent, Rect2 rect)
        {
            var panel = new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
            panel.AddThemeStyleboxOverride("panel", TileStyle(false, true, Bronze));
            parent.AddChild(panel);
            return panel;
        }

        static TextureRect AddImage(Control parent, Rect2 rect, bool pixel)
        {
            var img = new TextureRect
            {
                Position = rect.Position,
                Size = rect.Size,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                // 小人是點陣圖，放大要用最近鄰，不然會糊
                TextureFilter = pixel ? CanvasItem.TextureFilterEnum.Nearest : CanvasItem.TextureFilterEnum.Linear,
            };
            parent.AddChild(img);
            return img;
        }

        /// <summary>方塊樣式。line 是這一類存檔的框色；選中時換成亮金色、加粗。</summary>
        static StyleBoxFlat TileStyle(bool selected, bool enabled, Color line) => new StyleBoxFlat
        {
            BgColor = selected ? LeatherLit : Leather,
            BorderColor = selected ? GoldLine : (enabled ? line : new Color(line.R * 0.45f, line.G * 0.45f, line.B * 0.45f, 1f)),
            BorderWidthTop = selected ? 4 : 3, BorderWidthBottom = selected ? 4 : 3,
            BorderWidthLeft = selected ? 4 : 3, BorderWidthRight = selected ? 4 : 3,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
        };

        void Layout()
        {
            var view = GetViewport().GetVisibleRect().Size;
            float s = Mathf.Min(view.X / RefW, view.Y / RefH);
            if (s <= 0f) s = 1f;
            _stage.Scale = Vector2.One * s;
            _stage.Position = (view - new Vector2(RefW, RefH) * s) / 2f;
        }

        // ---------------- 圖 ----------------

        /// <summary>行走圖的「面朝下、中間那一格」。支援 RM 的 $（一張圖只有一個角色）前綴。</summary>
        static Texture2D ChibiOf(string name, int index)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var tex = RMZImageCache.LoadCharacter(name);
            if (tex == null) return null;

            int k = 0;
            while (k < name.Length && (name[k] == '!' || name[k] == '$')) k++;
            bool single = name.Substring(0, k).Contains('$');

            float w = tex.GetWidth() / (single ? 3f : 12f);
            float h = tex.GetHeight() / (single ? 4f : 8f);
            float bx = single ? 0f : (index % 4) * 3f * w;
            float by = single ? 0f : (index / 4) * 4f * h;
            return new AtlasTexture { Atlas = tex, Region = new Rect2(bx + w, by, w, h) };
        }

        static Texture2D ChibiFor(RMZSaveHeader h)
        {
            if (h != null && h.Exists && !string.IsNullOrEmpty(h.CharName)) return ChibiOf(h.CharName, h.CharIndex);
            // 舊存檔沒記行走圖：用資料庫裡主角的
            var db = RMZDatabase.Actor(1);
            return db == null ? null : ChibiOf(db.characterName, db.characterIndex);
        }

        // ---------------- 開關 ----------------

        void Show(Mode mode)
        {
            _mode = mode;
            _open = true;
            // 挑戰模式中（開關 83 開著）存或讀都只看挑戰那一格。
            // 標題畫面（Map003）一律是普通模式：從挑戰模式回標題時開關 83 可能還開著，
            // 挑戰存檔要從標題的「挑戰」按鈕讀，不從這裡。
            _challenge = RMZSaveSystem.IsChallenge && (RMZBootstrap.Instance?.CurrentMapId ?? 0) != TitleMapId;
            _page = 0;
            _sel = BigIndex;

            if (!_challenge) PickDefault();

            Layout();
            _root.Visible = true;
            Reload();
        }

        /// <summary>讀檔：停在最近存的那一格；存檔：停在第一個空格。</summary>
        void PickDefault()
        {
            if (_mode == Mode.Load)
            {
                // 翻到「最後一次手動存檔」的那一頁、停在那一格。
                // 自動存檔每天都會寫，拿它來比的話幾乎永遠停在第一頁，所以頁數只看手動存檔；
                // 一個手動存檔都沒有才停在自動存檔。時間用檔案修改時間比（SavedAt 只到分鐘）。
                ulong best = 0;
                bool found = false;
                for (int n = 1; n <= RMZSaveSystem.ManualCount; n++)
                {
                    ulong t = RMZSaveSystem.ModifiedTime(RMZSaveSystem.ManualSlot(n));
                    if (t == 0 || t < best) continue;
                    best = t;
                    found = true;
                    _page = (n - 1) / PageSize;
                    _sel = (n - 1) % PageSize;
                }
                if (!found) { _page = 0; _sel = BigIndex; }
            }
            else
            {
                _sel = 0;
                for (int n = 1; n <= RMZSaveSystem.ManualCount; n++)
                    if (!RMZSaveSystem.Exists(RMZSaveSystem.ManualSlot(n)))
                    {
                        _page = (n - 1) / PageSize;
                        _sel = (n - 1) % PageSize;
                        break;
                    }
            }
        }

        public void Close()
        {
            _open = false;
            if (_root != null) _root.Visible = false;
        }

        // ---------------- 操作 ----------------

        bool _lastClick, _lastRightClick, _lastDelete, _lastPageKey;
        Vector2 _lastMouse = new Vector2(-1f, -1f);

        public override void _Process(double delta)
        {
            if (!_open) return;
            Layout();

            if (Input.IsActionJustPressed("ui_cancel")) { Cancel(); return; }
            if (Input.IsActionJustPressed("ui_left")) Step(-1, 0);
            if (Input.IsActionJustPressed("ui_right")) Step(1, 0);
            if (Input.IsActionJustPressed("ui_up")) Step(0, -1);
            if (Input.IsActionJustPressed("ui_down")) Step(0, 1);
            if (Input.IsActionJustPressed("ui_accept")) Confirm();

            // Q／E、PageUp／PageDown 翻頁
            bool prevKey = Input.IsPhysicalKeyPressed(Key.Q) || Input.IsPhysicalKeyPressed(Key.Pageup);
            bool nextKey = Input.IsPhysicalKeyPressed(Key.E) || Input.IsPhysicalKeyPressed(Key.Pagedown);
            if ((prevKey || nextKey) && !_lastPageKey) TurnPage(prevKey ? -1 : 1);
            _lastPageKey = prevKey || nextKey;

            // 讀檔畫面按 Delete 刪掉選到的手動存檔（自動存檔、挑戰存檔不給刪）
            bool deleteNow = Input.IsPhysicalKeyPressed(Key.Delete);
            if (deleteNow && !_lastDelete && _mode == Mode.Load && !_challenge && _sel != BigIndex)
            {
                var h = _sel < _pageHeaders.Count ? _pageHeaders[_sel] : null;
                if (h != null && h.Exists) { RMZSaveSystem.Delete(h.Slot); Reload(); }
            }
            _lastDelete = deleteNow;

            bool rightNow = Input.IsMouseButtonPressed(MouseButton.Right);
            if (rightNow && !_lastRightClick) { _lastRightClick = true; Cancel(); return; }
            _lastRightClick = rightNow;

            // 滑鼠：移上去就選，點一下確認；點箭頭翻頁
            var mouse = _root.GetGlobalMousePosition();
            int hovered = HitCard(mouse);
            // 滑鼠有動才跟著選，不然滑鼠停在某格上時，方向鍵移開會被拉回去
            bool moved = mouse.DistanceSquaredTo(_lastMouse) > 1f;
            _lastMouse = mouse;
            if (moved && hovered != int.MinValue && hovered != _sel) { _sel = hovered; Refresh(); }

            bool click = Input.IsMouseButtonPressed(MouseButton.Left);
            if (click && !_lastClick)
            {
                if (!_challenge && _prev.Visible && _prev.GetGlobalRect().HasPoint(mouse)) TurnPage(-1);
                else if (!_challenge && _next.Visible && _next.GetGlobalRect().HasPoint(mouse)) TurnPage(1);
                else if (hovered != int.MinValue) { _sel = hovered; Confirm(); }
            }
            _lastClick = click;
        }

        public override void _Input(InputEvent e)
        {
            // 滾輪翻頁
            if (!_open || _challenge || e is not InputEventMouseButton mb || !mb.Pressed) return;
            if (mb.ButtonIndex == MouseButton.WheelUp) TurnPage(-1);
            else if (mb.ButtonIndex == MouseButton.WheelDown) TurnPage(1);
        }

        int HitCard(Vector2 mouse)
        {
            if (_big.Box.Visible && _big.Box.GetGlobalRect().HasPoint(mouse)) return BigIndex;
            if (_challenge) return int.MinValue;
            for (int i = 0; i < _tiles.Count; i++)
                if (_tiles[i].Box.Visible && _tiles[i].Box.GetGlobalRect().HasPoint(mouse)) return i;
            return int.MinValue;
        }

        void Step(int dx, int dy)
        {
            if (_challenge) return;

            if (_sel == BigIndex)
            {
                if (dx > 0) { _sel = 0; Refresh(); }
                return;
            }

            int c = _sel % Cols, r = _sel / Cols;
            if (dy != 0)
            {
                r = Mathf.Clamp(r + dy, 0, Rows - 1);
            }
            else if (dx < 0)
            {
                if (c > 0) c--;
                else if (_page > 0) { _page--; c = Cols - 1; LoadPage(); }
                else { _sel = BigIndex; Refresh(); return; }
            }
            else if (dx > 0)
            {
                if (c < Cols - 1) c++;
                else if (_page < PageCount - 1) { _page++; c = 0; LoadPage(); }
            }

            _sel = Mathf.Min(r * Cols + c, Mathf.Max(0, _pageHeaders.Count - 1));
            Refresh();
        }

        void TurnPage(int delta)
        {
            if (_challenge) return;
            int page = Mathf.Clamp(_page + delta, 0, PageCount - 1);
            if (page == _page) return;
            _page = page;
            RMZAudio.Instance?.PlaySeByName("Cursor1");
            LoadPage();
            if (_sel != BigIndex) _sel = Mathf.Min(_sel, Mathf.Max(0, _pageHeaders.Count - 1));
            Refresh();
        }

        void Confirm()
        {
            RMZSaveHeader header;
            int slot;

            if (_challenge || _sel == BigIndex)
            {
                header = _bigHeader;
                slot = _challenge ? RMZSaveSystem.ChallengeSlot : RMZSaveSystem.AutoSlot;

                // 普通模式的自動存檔不能手動覆蓋
                if (_mode == Mode.Save && !_challenge)
                {
                    RMZAudio.Instance?.PlaySeByName("Buzzer1");
                    return;
                }
            }
            else
            {
                if (_sel < 0 || _sel >= _pageHeaders.Count) return;
                header = _pageHeaders[_sel];
                slot = header.Slot;
            }

            if (_mode == Mode.Save)
            {
                RMZAudio.Instance?.PlaySeByName("Save");
                RMZSaveSystem.Save(slot);
                Close();
                return;
            }

            if (header == null || !header.Exists)
            {
                RMZAudio.Instance?.PlaySeByName("Buzzer1");
                return;
            }

            RMZAudio.Instance?.PlaySeByName("Load");

            // 先關畫面再套存檔：Apply() 會把畫面與直譯器整個清掉，
            // 這個視窗要是還開著，RMZEventRunner 就會一直以為玩家還在選格子。
            Close();
            if (!RMZSaveSystem.LoadInto(slot))
                GD.PushError($"[RMZ] 第 {slot} 格讀不起來，存檔可能壞了。");
        }

        void Cancel()
        {
            RMZAudio.Instance?.PlaySeByName("Cancel2");
            Close();
        }

        // ---------------- 內容 ----------------

        void Reload()
        {
            _bigHeader = RMZSaveSystem.Header(_challenge ? RMZSaveSystem.ChallengeSlot : RMZSaveSystem.AutoSlot);
            LoadPage();
            Refresh();
        }

        void LoadPage()
        {
            _pageHeaders = _challenge ? new List<RMZSaveHeader>() : RMZSaveSystem.ManualHeaders(_page, PageSize);
        }

        static string ShortTime(string savedAt) =>
            !string.IsNullOrEmpty(savedAt) && savedAt.Length >= 16 ? savedAt.Substring(5) : (savedAt ?? "");

        void Refresh()
        {
            bool save = _mode == Mode.Save;

            _title.Text = (save ? "保存" : "讀取") + (_challenge ? "　－　挑戰模式" : "");

            // ---- 大卡 ----
            _big.Box.Position = _challenge ? new Vector2((RefW - 360f) / 2f, 96f) : new Vector2(70f, 96f);
            _big.Head.Text = _challenge ? "挑戰模式存檔" : "自動存檔";
            _big.Chibi.Texture = ChibiFor(_bigHeader);

            var bh = _bigHeader;
            _big.Info.Text = bh != null && bh.Exists
                ? $"{bh.Name}　Lv.{bh.Level}\n第 {bh.Day} 天　　{bh.PlaytimeText}\n{bh.SavedAt}"
                : (_challenge ? "還沒有挑戰存檔" : "還沒有自動存檔");
            _big.Info.AddThemeColorOverride("font_color", bh != null && bh.Exists ? Parchment : Dim);
            _big.Chibi.Modulate = bh != null && bh.Exists ? Colors.White : new Color(1f, 1f, 1f, 0.25f);

            bool bigLocked = save && !_challenge;   // 普通模式存檔時，自動存檔不能選
            _big.Note.Text = bigLocked ? "自動存檔無法手動覆蓋" : (_challenge ? "自動存檔與手動存檔共用這一格" : "");
            _big.Note.AddThemeColorOverride("font_color", bigLocked ? Warn : Dim);
            _big.Box.AddThemeStyleboxOverride("panel", TileStyle(_sel == BigIndex, !bigLocked, _challenge ? ChallengeLine : AutoLine));

            // ---- 手動存檔方塊 ----
            for (int i = 0; i < _tiles.Count; i++)
            {
                var t = _tiles[i];
                bool has = !_challenge && i < _pageHeaders.Count;
                t.Box.Visible = has;
                if (!has) continue;

                var h = _pageHeaders[i];
                int number = _page * PageSize + i + 1;
                t.Head.Text = $"No.{number}";
                t.Chibi.Texture = ChibiFor(h);
                t.Chibi.Modulate = h.Exists ? Colors.White : new Color(1f, 1f, 1f, 0.22f);
                t.Info.Text = h.Exists
                    ? $"Lv.{h.Level}　第 {h.Day} 天\n{h.PlaytimeText}\n{ShortTime(h.SavedAt)}"
                    : "—— 空 ——";
                t.Info.AddThemeColorOverride("font_color", h.Exists ? Parchment : Dim);
                t.Box.AddThemeStyleboxOverride("panel", TileStyle(_sel == i, true, Bronze));
            }

            // ---- 翻頁列 ----
            _prev.Visible = _next.Visible = _pageLabel.Visible = !_challenge;
            _pageLabel.Text = $"{_page + 1} / {PageCount}";
            _prev.Modulate = _page > 0 ? Colors.White : new Color(1f, 1f, 1f, 0.3f);
            _next.Modulate = _page < PageCount - 1 ? Colors.White : new Color(1f, 1f, 1f, 0.3f);

            // ---- 說明 ----
            bool any = _challenge ? (bh != null && bh.Exists) : RMZSaveSystem.AnyExists();
            if (!save && !any)
            {
                _hint.Text = "還沒有任何存檔　Esc / 右鍵返回";
                _hint.AddThemeColorOverride("font_color", Warn);
            }
            else
            {
                _hint.Text = _challenge
                    ? (save ? "挑戰模式只有這一格存檔　Esc / 右鍵取消" : "讀取挑戰模式存檔　Esc / 右鍵取消")
                    : (save ? "點選要存的格子　Q／E 或滾輪翻頁　Esc / 右鍵取消"
                            : "點選要讀的存檔　Q／E 或滾輪翻頁　Delete 刪除　Esc / 右鍵取消");
                _hint.AddThemeColorOverride("font_color", Parchment);
            }
        }
    }
}
