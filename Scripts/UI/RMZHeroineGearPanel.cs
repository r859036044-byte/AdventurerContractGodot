// 女主換裝視窗。從 H 狀態欄的「更換裝備」鈕打開（好感度 60 以上）。
// 左邊是她身上的 5 格，中間是男主背包裡同一格的裝備，右邊是說明與換上後的變化。
using System.Collections.Generic;
using System.IO;
using Godot;

namespace RMZ
{
    public partial class RMZHeroineGearPanel : CanvasLayer
    {
        static RMZHeroineGearPanel _instance;
        public static bool IsOpen => GodotObject.IsInstanceValid(_instance);

        /// <summary>關掉的那一幀。RM 的圖片按鈕在這幀之後才恢復，免得 ESC 同時把 H 狀態欄也退掉。</summary>
        public static ulong ClosedFrame;

        public static void Open(int heroine)
        {
            if (IsOpen || !RMZHeroineGear.IsHeroine(heroine)) return;
            _instance = new RMZHeroineGearPanel { _heroine = heroine, Layer = 92 };
            RMZBootstrap.Instance.GetTree().Root.AddChild(_instance);
        }

        int _heroine, _slot = 1, _selected = -1;
        readonly Button[] _slotButtons = new Button[6];
        List<RMZEquipInstance> _bag = new List<RMZEquipInstance>();
        ItemList _list;
        Label _detail, _stats, _notice;
        Button _equip, _unequip;
        Texture2D _icons;

        static StyleBoxFlat Box() => new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.08f, 0.07f, 0.95f), BorderColor = new Color("cbaa58"),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
            ContentMarginLeft = 20, ContentMarginRight = 20, ContentMarginTop = 16, ContentMarginBottom = 16,
        };

        public override void _Ready()
        {
            var screen = new Control { Theme = new Theme { DefaultFont = RMZFonts.Main, DefaultFontSize = 22 } };
            AddChild(screen); screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var dim = new ColorRect { Color = new Color(0, 0, 0, .7f) };
            screen.AddChild(dim); dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", Box());
            screen.AddChild(panel); panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            panel.OffsetLeft = 56; panel.OffsetRight = -56; panel.OffsetTop = 40; panel.OffsetBottom = -40;

            var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 12); panel.AddChild(column);
            var header = new HBoxContainer(); column.AddChild(header);
            header.AddChild(new Label { Text = RMZS.SVTalk.NameOf(_heroine) + "的裝備", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            var close = new Button { Text = "關閉 ×" }; close.Pressed += Close; header.AddChild(close);

            var row = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 18); column.AddChild(row);

            // 左：她身上的 5 格
            var slotsBox = new VBoxContainer { CustomMinimumSize = new Vector2(330, 0) };
            slotsBox.AddThemeConstantOverride("separation", 8); row.AddChild(slotsBox);
            for (int s = 1; s <= 5; s++)
            {
                int slot = s;
                var b = new Button { Alignment = HorizontalAlignment.Left, ClipText = true, CustomMinimumSize = new Vector2(330, 52), ExpandIcon = false };
                b.Pressed += () => { _slot = slot; Refresh(); };
                slotsBox.AddChild(b);
                _slotButtons[s] = b;
            }
            _stats = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            slotsBox.AddChild(_stats);

            // 中：背包裡同一格的裝備
            _list = new ItemList { CustomMinimumSize = new Vector2(330, 200), SizeFlagsVertical = Control.SizeFlags.ExpandFill, FixedIconSize = new Vector2I(32, 32) };
            row.AddChild(_list);
            _list.ItemSelected += n => Select((int)n);

            // 右：說明與按鈕
            var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(right);
            var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            right.AddChild(scroll);
            _detail = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            scroll.AddChild(_detail);
            _equip = new Button { Text = "給她換上" }; _equip.Pressed += DoEquip; right.AddChild(_equip);
            _unequip = new Button { Text = "脫下這格（放回背包）" }; _unequip.Pressed += DoUnequip; right.AddChild(_unequip);

            _notice = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Text = "女主的戰鬥能力以初始裝備為基準：換上更好的就變強，脫掉就變弱。" };
            column.AddChild(_notice);

            var iconPath = ProjectSettings.GlobalizePath("res://rmz/img/system/IconSet.png");
            if (File.Exists(iconPath)) _icons = ImageTexture.CreateFromImage(Image.LoadFromFile(iconPath));

            Refresh();
        }

        Texture2D Icon(int i) => _icons == null ? null : new AtlasTexture { Atlas = _icons, Region = new Rect2((i % 16) * 32, (i / 16) * 32, 32, 32) };

        void Refresh()
        {
            for (int s = 1; s <= 5; s++)
            {
                var worn = RMZHeroineGear.WornAt(_heroine, s);
                var b = _slotButtons[s];
                b.Text = RMZEquipment.SlotNames[s] + "：" + (worn != null ? worn.Name : "（空）");
                b.Icon = worn != null ? Icon(worn.IconIndex) : null;
                b.Modulate = s == _slot ? new Color(1f, 0.9f, 0.55f) : Colors.White;
            }

            _stats.Text = $"技能威力 ×{RMZHeroineGear.PowerMul(_heroine):0.00}\n減傷 ×{RMZHeroineGear.DefenseMul(_heroine):0.00}\n血量 ×{RMZHeroineGear.HpMul(_heroine):0.00}";

            _list.Clear();
            _bag = RMZEquipment.InSlot(_slot);
            _bag.Sort((a, b) => b.Roll.CompareTo(a.Roll));
            foreach (var e in _bag)
                _list.AddItem((RMZEquipment.IsWorn(e) ? "◆ " : "") + e.Name, Icon(e.IconIndex));

            _selected = -1;
            _equip.Disabled = true;
            _unequip.Disabled = RMZHeroineGear.WornAt(_heroine, _slot) == null;
            ShowWorn();
        }

        void ShowWorn()
        {
            var worn = RMZHeroineGear.WornAt(_heroine, _slot);
            _detail.Text = worn == null
                ? $"{RMZEquipment.SlotNames[_slot]}目前沒穿東西。\n\n從中間選一件男主背包裡的裝備給她換上。"
                : $"身上：{worn.Name}\n{Stats(worn)}\n\n從中間選一件背包裡的裝備，可以看換上後的變化。"
                  + (_bag.Count == 0 ? "\n\n（背包裡沒有這一格的裝備）" : "");
        }

        static string Stats(RMZEquipInstance e) =>
            $"物攻 {e.Atk}　魔攻 {e.MAtk}\n物防 {e.Def}　魔防 {e.MDef}\n血量 {e.MaxHp}";

        void Select(int n)
        {
            if (n < 0 || n >= _bag.Count) return;
            _selected = n;
            var e = _bag[n];
            var worn = RMZHeroineGear.WornAt(_heroine, _slot);

            // 換上後的倍率：先暫時算一下總和
            var t = RMZHeroineGear.Totals(_heroine);
            int da = e.Atk + e.MAtk - (worn != null ? worn.Atk + worn.MAtk : 0);
            int dd = e.Def + e.MDef - (worn != null ? worn.Def + worn.MDef : 0);
            int dh = e.MaxHp - (worn != null ? worn.MaxHp : 0);

            _detail.Text = $"{e.Name}（{e.RollName}）\n{Stats(e)}\n\n"
                + $"換上後：攻擊 {Sign(da)}　防禦 {Sign(dd)}　血量 {Sign(dh)}"
                + (RMZEquipment.IsWorn(e) ? "\n\n※ 這件男主正穿著，換給她之後男主這格會空出來。" : "");
            _equip.Disabled = false;
        }

        static string Sign(int v) => v > 0 ? "+" + v : v.ToString();

        void DoEquip()
        {
            if (_selected < 0 || _selected >= _bag.Count) return;
            var e = _bag[_selected];
            RMZHeroineGear.Equip(_heroine, e);
            _notice.Text = $"已經幫{RMZS.SVTalk.NameOf(_heroine)}換上 {e.Name}。";
            RMZEventRunner.Instance?.RequestRefresh();
            Refresh();
        }

        void DoUnequip()
        {
            var worn = RMZHeroineGear.WornAt(_heroine, _slot);
            if (worn == null) return;
            RMZHeroineGear.Unequip(_heroine, _slot);
            _notice.Text = $"{worn.Name} 已放回背包。";
            Refresh();
        }

        void Close() => QueueFree();

        public override void _UnhandledInput(InputEvent input)
        {
            if (input.IsActionPressed("ui_cancel")) { GetViewport().SetInputAsHandled(); Close(); }
        }

        public override void _ExitTree()
        {
            if (_instance == this) _instance = null;
            ClosedFrame = Engine.GetProcessFrames();
        }
    }

    /// <summary>
    /// H 狀態欄看某位女主的詳細頁時，右下角多一顆「更換裝備」鈕。
    /// 好感度未滿 60 時鈕是灰的，寫著幾點開放。RMZScreenView 每幀呼叫 Tick。
    /// </summary>
    public partial class RMZHeroineGearButton : CanvasLayer
    {
        static RMZHeroineGearButton _instance;
        Button _button;
        int _heroine;

        /// <summary>滑鼠停在鈕上：這時不讓底下的 RM 圖片按鈕吃到點擊。</summary>
        public static bool Hovered => GodotObject.IsInstanceValid(_instance) && _instance._button != null
                                      && _instance.Visible && _instance._button.IsHovered();

        const int SelectedHeroineVar = 285;   // CE60：H 狀態欄目前看的是哪位
        const int DetailPicture = 201;        // CE60：詳細頁的底圖

        public static void Tick()
        {
            int h = RMZGameState.Variable(SelectedHeroineVar);
            var pic = RMZScreen.Instance?.Picture(DetailPicture);
            bool show = RMZHeroineGear.IsHeroine(h) && pic != null && pic.Opacity > 1f
                        && pic.Name.Contains("H status") && !RMZHeroineGearPanel.IsOpen;

            if (!show)
            {
                if (GodotObject.IsInstanceValid(_instance)) _instance.Visible = false;
                return;
            }

            if (!GodotObject.IsInstanceValid(_instance))
            {
                _instance = new RMZHeroineGearButton { Layer = 9 };   // 圖片(5)、文字視窗(7) 之上，對話框(10) 之下
                RMZBootstrap.Instance.GetTree().Root.AddChild(_instance);
            }
            _instance.Visible = true;
            _instance.SetHeroine(h);
        }

        public override void _Ready()
        {
            var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore,
                Theme = new Theme { DefaultFont = RMZFonts.Main, DefaultFontSize = 22 } };
            AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _button = new Button { CustomMinimumSize = new Vector2(220, 52) };
            root.AddChild(_button);
            _button.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
            _button.OffsetLeft = -250; _button.OffsetTop = -76; _button.OffsetRight = -30; _button.OffsetBottom = -24;
            _button.Pressed += () => { if (RMZHeroineGear.Unlocked(_heroine)) RMZHeroineGearPanel.Open(_heroine); };
        }

        void SetHeroine(int heroine)
        {
            _heroine = heroine;
            if (_button == null) return;
            bool unlocked = RMZHeroineGear.Unlocked(heroine);
            _button.Disabled = !unlocked;
            _button.Text = unlocked ? "更換裝備" : $"好感度 {RMZHeroineGear.UnlockLove} 開放換裝";
        }
    }
}
