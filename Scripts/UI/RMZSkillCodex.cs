// 技能圖鑑：Esc 選單裡的一頁，把 Survivors 小遊戲的強化卡全部攤開來給玩家看。
//
// 版面是卡片牆 —— 用 Godot 的 GridContainer 排成一排五張，外面包一層 ScrollContainer
// （原生支援滾輪與拖曳捲動，不用像 Unity 版那樣自己算捲動量）。
// 分類不是在這裡硬編的：跟 Unity 版一樣，資料來自 SVUpgrades / SVAwakenings / SVPassives
// 的目錄，之後在那幾支的 BuildCatalog() 加新卡，圖鑑就自動歸到那一頁。
//
// [移植待辦] 目前卡片只顯示名字/說明/解鎖條件（圖鑑要的東西），
// 還沒接到 Survivors 小遊戲本體的抽選/套用邏輯（那些等小遊戲本體移植時再接）。
// 還沒解鎖的卡壓成黑色、名字變問號，效果不顯示，但留著解鎖條件。
using System;
using System.Collections.Generic;
using Godot;
using RMZS;

namespace RMZ
{
    public partial class RMZSkillCodex : CanvasLayer
    {
        public static RMZSkillCodex Instance { get; private set; }

        /// <summary>圖鑑開著。系統選單靠這個知道 Esc 現在是誰的。</summary>
        public static bool IsOpen => Instance != null && Instance._open;

        // ---------------- 解鎖判定 ----------------

        /// <summary>冒險者等級旗標：C=變數10 B=11 A=12 S=13。</summary>
        const int RankVarBase = 9;

        /// <summary>技能書學會與否的開關（跟 Survivors 小遊戲本體是同一組）。</summary>
        const int SwitchLearnedBond = 22;
        const int SwitchLearnedTimeStop = 23;

        /// <summary>某個冒險者等級到了沒。1=C 2=B 3=A 4=S。</summary>
        public static bool RankReached(int rank)
        {
            rank = Mathf.Clamp(rank, 1, 4);
            if (rank <= 1) return true; // C 級是出道就有的起手包
            return RMZGameState.Variable(RankVarBase + rank) == 1;
        }

        /// <summary>DEV 用：把圖鑑裡所有東西一次解鎖。</summary>
        public static void UnlockEverything()
        {
            for (int rank = 1; rank <= 4; rank++) RMZGameState.SetVariable(RankVarBase + rank, 1);
            RMZGameState.SetSwitch(SwitchLearnedBond, true);
            RMZGameState.SetSwitch(SwitchLearnedTimeStop, true);
            Instance?.Refresh();
        }

        static string RankName(int rank) => rank switch { 2 => "B", 3 => "A", 4 => "S", _ => "C" };

        // ---------------- 條目 ----------------

        class Card
        {
            public string Name;
            public string Icon;
            public string Desc;
            public string Note;
            public Color Tint = Colors.White;
            public Func<bool> Unlocked;
            public bool IsUnlocked => Unlocked == null || Unlocked();
        }

        class Section
        {
            public string Title;
            public Color Tint;
            public string Group;
            public readonly List<Card> Cards = new List<Card>();
        }

        const string GroupRune = "強化符文　（能量條升級時三選一）";
        const string GroupBless = "公會祝福　（出發前就帶著上場）";

        readonly List<Section> _sections = new List<Section>();

        Section NewSection(string title, Color tint, string group)
        {
            var s = new Section { Title = title, Tint = tint, Group = group };
            _sections.Add(s);
            return s;
        }

        void BuildData()
        {
            _sections.Clear();

            var catalog = SVUpgrades.BuildCatalog() ?? new List<SVUpgrade>();
            var awakens = SVAwakenings.BuildCatalog() ?? new List<SVUpgrade>();

            var awakenSec = NewSection("大覺", new Color(1f, 0.62f, 0.88f), GroupRune);
            foreach (var u in awakens)
            {
                if (u == null) continue;

                Func<bool> gate = null;
                string note = null;
                if (u.Id == "awaken_" + SVAwakenKind.Bond)
                {
                    gate = () => RMZGameState.Switch(SwitchLearnedBond);
                    note = "技能書・找鈴音的父親";
                }
                else if (u.Id == "awaken_" + SVAwakenKind.TimeStop)
                {
                    gate = () => RMZGameState.Switch(SwitchLearnedTimeStop);
                    note = "技能書・找鈴音的父親";
                }
                else if (u.UnlockRank > 1)
                {
                    int rank = u.UnlockRank;
                    gate = () => RankReached(rank);
                    note = RankName(rank) + " 級解鎖";
                }

                awakenSec.Cards.Add(new Card { Icon = u.Icon, Name = u.Name, Desc = u.Desc, Note = note, Tint = u.Tint, Unlocked = gate });
            }

            AddCatalogSection(catalog, SVUpgradeCategory.Basic, "普攻強化", new Color(1f, 0.85f, 0.45f));
            AddCatalogSection(catalog, SVUpgradeCategory.SkillUp, "技能強化", new Color(0.85f, 0.75f, 1f));
            AddCatalogSection(catalog, SVUpgradeCategory.Team, "團隊強化", new Color(0.75f, 0.8f, 1f));
            AddCatalogSection(catalog, SVUpgradeCategory.PlayerSkill, "個人技能", new Color(1f, 0.78f, 0.5f));
            AddCatalogSection(catalog, SVUpgradeCategory.Defense, "防禦技能", new Color(0.55f, 0.78f, 0.6f));

            var passiveSec = NewSection("祝福", new Color(0.65f, 0.9f, 1f), GroupBless);
            var passives = SVPassives.BuildCatalog();
            if (passives != null)
                foreach (var p in passives)
                {
                    if (p == null) continue;
                    passiveSec.Cards.Add(new Card { Icon = "blessing_" + p.Id, Name = p.Name, Desc = p.Desc, Tint = p.Tint });
                }
        }

        void AddCatalogSection(List<SVUpgrade> catalog, SVUpgradeCategory cat, string title, Color tint)
        {
            var sec = NewSection(title, tint, GroupRune);
            foreach (var u in catalog)
            {
                if (u == null || u.Category != cat) continue;

                int rank = Mathf.Max(1, u.UnlockRank);
                sec.Cards.Add(new Card
                {
                    Icon = u.Icon, Name = u.Name,
                    Desc = u.Desc,
                    Note = rank > 1 ? RankName(rank) + " 級解鎖" : null,
                    Tint = u.Tint,
                    Unlocked = rank <= 1 ? null : () => RankReached(rank),
                });
            }
        }

        // ---------------- 版面 ----------------

        const int Columns = 5;
        const float PanelWidth = 1180f;
        const float PanelHeight = 640f;
        const float CardWidth = 214f;
        const float CardHeight = 132f;
        const float CardGap = 10f;
        const float RefHeight = 720f;

        Control _root;
        Panel _panel;
        Label _title;
        Label _hint;
        ScrollContainer _scroll;
        VBoxContainer _content;

        class Slot
        {
            public Card Card;
            public Panel Bg;
            public ColorRect Stripe;
            public Label Name;
            public TextureRect Icon;
            public Label Desc;
            public Label Note;
        }

        readonly List<Slot> _slots = new List<Slot>();

        bool _open;

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            Layer = 48; // 系統選單(50)之下、設置(45)之上
            ProcessPriority = 400;
            ProcessMode = ProcessModeEnum.Always;
            BuildData();
            Build();
            _root.Visible = false;
        }

        public static void Open()
        {
            if (Instance == null) return;
            Instance.Show();
        }

        void Show()
        {
            _open = true;
            Layout();
            _root.Visible = true;
            Refresh();
            if (_scroll != null) _scroll.ScrollVertical = 0;
        }

        public void Close()
        {
            _open = false;
            if (_root != null) _root.Visible = false;
        }

        void Build()
        {
            _root = new Control { Name = "Root" };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_root);

            var dim = new ColorRect { Name = "Dim", Color = new Color(0f, 0f, 0f, 0.80f) };
            dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(dim);

            _panel = new Panel { Name = "Panel" };
            _panel.AddThemeStyleboxOverride("panel", RMZWindowSkin.PanelStyle(new Color(0.07f, 0.08f, 0.12f, 0.98f)));
            _root.AddChild(_panel);

            _title = NewLabel("技能圖鑑", RMZFonts.DefaultSize + 2, new Color(1f, 0.9f, 0.6f));
            _panel.AddChild(_title);

            _scroll = new ScrollContainer { Name = "Scroll" };
            _scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            _panel.AddChild(_scroll);

            _content = new VBoxContainer { Name = "Content" };
            _content.AddThemeConstantOverride("separation", 8);
            _scroll.AddChild(_content);

            string currentGroup = null;
            foreach (var sec in _sections)
            {
                if (sec.Cards.Count == 0) continue;

                if (sec.Group != currentGroup)
                {
                    currentGroup = sec.Group;
                    var band = new Panel();
                    band.AddThemeStyleboxOverride("panel", RMZWindowSkin.PanelStyle(new Color(0.86f, 0.74f, 0.42f, 0.22f), new Color(0.86f, 0.74f, 0.42f, 0.4f)));
                    band.CustomMinimumSize = new Vector2(0f, 40f);
                    _content.AddChild(band);

                    var bandText = NewLabel(currentGroup, RMZFonts.DefaultSize, new Color(1f, 0.92f, 0.68f));
                    bandText.HorizontalAlignment = HorizontalAlignment.Left;
                    bandText.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                    bandText.AddThemeConstantOverride("margin_left", 14);
                    band.AddChild(bandText);
                }

                var head = new Panel();
                head.AddThemeStyleboxOverride("panel", RMZWindowSkin.PanelStyle(new Color(sec.Tint.R, sec.Tint.G, sec.Tint.B, 0.16f), new Color(sec.Tint.R, sec.Tint.G, sec.Tint.B, 0.5f)));
                head.CustomMinimumSize = new Vector2(0f, 34f);
                _content.AddChild(head);

                var headText = NewLabel(sec.Title + "　（" + sec.Cards.Count + "）", RMZFonts.DefaultSize - 2, sec.Tint);
                headText.HorizontalAlignment = HorizontalAlignment.Left;
                headText.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                head.AddChild(headText);

                var grid = new GridContainer { Columns = Columns };
                grid.AddThemeConstantOverride("h_separation", (int)CardGap);
                grid.AddThemeConstantOverride("v_separation", (int)CardGap);
                _content.AddChild(grid);

                foreach (var card in sec.Cards)
                {
                    var bg = new Panel { CustomMinimumSize = new Vector2(CardWidth, CardHeight) };
                    grid.AddChild(bg);

                    var stripe = new ColorRect { Color = card.Tint };
                    stripe.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
                    stripe.Size = new Vector2(4f, CardHeight - 12f);
                    stripe.Position = new Vector2(6f, 6f);
                    bg.AddChild(stripe);

                    var icon=new TextureRect{Texture=SVArt.SkillIcon(card.Icon)?.Native,TextureFilter=CanvasItem.TextureFilterEnum.Nearest,Position=new Vector2(16,8),Size=new Vector2(34,34),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore};bg.AddChild(icon);
                    var name = NewLabel(card.Name, RMZFonts.DefaultSize - 4, card.Tint);
                    name.HorizontalAlignment = HorizontalAlignment.Left;
                    name.VerticalAlignment = VerticalAlignment.Top;
                    name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    name.Position = new Vector2(56f, 6f);
                    name.Size = new Vector2(CardWidth - 66f, 44f);
                    bg.AddChild(name);

                    var desc = NewLabel(card.Desc, RMZFonts.DefaultSize - 8, new Color(0.78f, 0.80f, 0.86f));
                    desc.HorizontalAlignment = HorizontalAlignment.Left;
                    desc.VerticalAlignment = VerticalAlignment.Top;
                    desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    desc.Position = new Vector2(16f, 46f);
                    desc.Size = new Vector2(CardWidth - 26f, CardHeight - 68f);
                    bg.AddChild(desc);

                    var note = NewLabel(card.Note ?? "", RMZFonts.DefaultSize - 9, new Color(0.55f, 0.58f, 0.66f));
                    note.HorizontalAlignment = HorizontalAlignment.Left;
                    note.VerticalAlignment = VerticalAlignment.Bottom;
                    note.Position = new Vector2(16f, CardHeight - 22f);
                    note.Size = new Vector2(CardWidth - 26f, 18f);
                    bg.AddChild(note);

                    _slots.Add(new Slot { Card = card, Icon = icon, Bg = bg, Stripe = stripe, Name = name, Desc = desc, Note = note });
                }
            }

            _hint = NewLabel("滾輪捲動　　Esc 或右鍵 關閉", RMZFonts.DefaultSize - 9, RMZWindowSkin.TextDisabled);
            _panel.AddChild(_hint);
        }

        static Label NewLabel(string text, int size, Color color)
        {
            var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
            RMZWindowSkin.StyleLabel(label, size, color);
            return label;
        }

        void Layout()
        {
            var view = _root.Size;
            float s = view.Y / RefHeight;
            if (s <= 0f) s = 1f;

            var size = new Vector2(PanelWidth, PanelHeight) * s;
            _panel.Size = size;
            _panel.Position = (view - size) / 2f;

            _title.Position = new Vector2(16f * s, 12f * s);
            _title.Size = new Vector2(size.X - 32f * s, 32f * s);

            _scroll.Position = new Vector2(16f * s, 52f * s);
            _scroll.Size = new Vector2(size.X - 32f * s, size.Y - 88f * s);
            // 卡片是固定原生尺寸（CardWidth/CardHeight），視窗目前固定 1280x720 不會縮放，
            // 這裡只保留 ScrollContainer 的可視範圍隨面板一起變動即可。
            _content.CustomMinimumSize = new Vector2(_scroll.Size.X, 0f);

            _hint.Position = new Vector2(16f * s, size.Y - 26f * s);
            _hint.Size = new Vector2(size.X - 32f * s, 20f * s);
        }

        public void Refresh()
        {
            int unlocked = 0;
            foreach (var slot in _slots)
            {
                var card = slot.Card;
                bool on = card.IsUnlocked;
                if (on) unlocked++;
                slot.Icon.Visible=on;

                if (on)
                {
                    slot.Bg.AddThemeStyleboxOverride("panel", RMZWindowSkin.PanelStyle(new Color(0.13f, 0.15f, 0.20f, 0.95f)));
                    slot.Stripe.Color = card.Tint;
                    slot.Name.Text = card.Name;
                    slot.Name.AddThemeColorOverride("font_color", card.Tint);
                    slot.Desc.Text = card.Desc;
                    slot.Note.Text = card.Note ?? "";
                }
                else
                {
                    slot.Bg.AddThemeStyleboxOverride("panel", RMZWindowSkin.PanelStyle(new Color(0.02f, 0.02f, 0.03f, 0.95f)));
                    slot.Stripe.Color = new Color(0.16f, 0.16f, 0.19f, 1f);
                    slot.Name.Text = "？？？";
                    slot.Name.AddThemeColorOverride("font_color", new Color(0.24f, 0.24f, 0.28f));
                    slot.Desc.Text = "";
                    slot.Note.Text = card.Note ?? "";
                }
            }

            _title.Text = "技能圖鑑　" + unlocked + " / " + _slots.Count;
        }

        // ---------------- 操作 ----------------

        bool _lastRightClick;

        public override void _Process(double delta)
        {
            if (!_open) return;

            bool rightNow = Input.IsMouseButtonPressed(MouseButton.Right);
            bool rightClicked = rightNow && !_lastRightClick;
            _lastRightClick = rightNow;

            if (Input.IsActionJustPressed("ui_cancel") || rightClicked)
            {
                RMZAudio.Instance?.PlaySeByName("Cancel2");
                Close();
            }
        }
    }
}
