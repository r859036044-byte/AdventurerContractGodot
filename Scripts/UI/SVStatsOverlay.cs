// 倖存者戰鬥右上角「技能」鈕打開的面板：這一局拿到的東西與效果
//   （羈絆、主動技能、大覺醒、祝福、強化、卡片）。男主能力值改在 HUD 左下角常駐（SVHud.BuildStatsBox）。
//
// 用 Godot 原生元件做，疊在最上層（跟 RMZTutorialOverlay 同一個做法），
// 不塞進 SVHud 那套仿 Unity 的介面。開著的時候 SVGame.Paused 會是 true。
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;
using RMZS;

namespace RMZ
{
    public partial class SVStatsOverlay : CanvasLayer
    {
        static SVStatsOverlay _active;
        public static bool IsOpen => GodotObject.IsInstanceValid(_active);

        public static void Toggle()
        {
            if (IsOpen) _active.Close();
            else Open();
        }

        public static void Open()
        {
            var game = SVGame.Instance;
            if (IsOpen || game == null || game.Player == null) return;
            var node = new SVStatsOverlay { Layer = 185 };
            _active = node;
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(node);
        }

        const float RefW = 1280f, RefH = 720f;
        const float PanelX = 140f, PanelY = 56f, PanelW = 1000f, PanelH = 608f;

        static readonly Color Gold = new Color(1f, 0.86f, 0.5f);
        static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);

        Control _stage;
        float _age;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;

            // 半透明黑底，擋住底下的點擊
            var blocker = new ColorRect { Color = new Color(0f, 0f, 0f, 0.62f), MouseFilter = Control.MouseFilterEnum.Stop };
            AddChild(blocker);
            blocker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            _stage = new Control { Size = new Vector2(RefW, RefH), MouseFilter = Control.MouseFilterEnum.Ignore };
            blocker.AddChild(_stage);

            Build();
            Layout();
        }

        public override void _Process(double delta)
        {
            _age += (float)delta;
            Layout();
        }

        public override void _Input(InputEvent e)
        {
            if (_age < 0.15f) return;
            bool close =
                (e is InputEventKey k && k.Pressed && !k.Echo && (k.Keycode == Key.Escape)) ||
                (e is InputEventMouseButton m && m.Pressed && m.ButtonIndex == MouseButton.Right);
            if (close) { GetViewport().SetInputAsHandled(); Close(); }
        }

        void Close()
        {
            if (_active == this) _active = null;
            QueueFree();
        }

        public override void _ExitTree() { if (_active == this) _active = null; }

        void Layout()
        {
            var view = GetViewport().GetVisibleRect().Size;
            float s = Mathf.Min(view.X / RefW, view.Y / RefH);
            if (s <= 0f) s = 1f;
            _stage.Scale = Vector2.One * s;
            _stage.Position = (view - new Vector2(RefW, RefH) * s) / 2f;
        }

        // ---------------- 版面 ----------------
        // 冒險者主題：皮革棕底、古銅框、羊皮紙字（跟存檔畫面同一套），每個技能一張小卡，兩欄排。

        static readonly Color Leather = new Color(0.14f, 0.10f, 0.07f, 0.92f);
        static readonly Color CardFill = new Color(0.22f, 0.16f, 0.10f, 0.85f);
        static readonly Color Bronze = new Color(0.60f, 0.46f, 0.26f, 1f);
        static readonly Color Parchment = new Color(0.95f, 0.89f, 0.76f);
        const float CardW = 452f;

        static StyleBoxFlat Rounded(Color fill, Color border, int width, int radius) => new StyleBoxFlat
        {
            BgColor = fill, BorderColor = border,
            BorderWidthTop = width, BorderWidthBottom = width, BorderWidthLeft = width, BorderWidthRight = width,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 9,
        };

        static Label NewLabel(string text, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
        {
            var label = new Label
            {
                Text = text,
                HorizontalAlignment = align,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            RMZWindowSkin.StyleLabel(label, size, color);
            return label;
        }

        void Build()
        {
            var game = SVGame.Instance;

            var panel = new Panel { Position = new Vector2(PanelX, PanelY), Size = new Vector2(PanelW, PanelH), MouseFilter = Control.MouseFilterEnum.Stop };
            panel.AddThemeStyleboxOverride("panel", Rounded(Leather, Bronze, 3, 14));
            _stage.AddChild(panel);

            var title = NewLabel("技能一覽", 30, Gold, HorizontalAlignment.Center);
            title.Position = new Vector2(0f, 12f);
            title.Size = new Vector2(PanelW, 40f);
            panel.AddChild(title);

            var line = new ColorRect { Color = new Color(Bronze.R, Bronze.G, Bronze.B, 0.6f), Position = new Vector2(40f, 58f), Size = new Vector2(PanelW - 80f, 2f), MouseFilter = Control.MouseFilterEnum.Ignore };
            panel.AddChild(line);

            var close = new Button { Text = "✕", Position = new Vector2(PanelW - 54f, 12f), Size = new Vector2(40f, 38f), Flat = true, FocusMode = Control.FocusModeEnum.None };
            close.AddThemeFontOverride("font", RMZFonts.Main);
            close.AddThemeFontSizeOverride("font_size", 22);
            close.AddThemeColorOverride("font_color", Parchment);
            close.Pressed += Close;
            panel.AddChild(close);

            var scroll = new ScrollContainer
            {
                Position = new Vector2(34f, 72f),
                Size = new Vector2(PanelW - 56f, PanelH - 116f),
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            };
            panel.AddChild(scroll);

            var list = new VBoxContainer { CustomMinimumSize = new Vector2(CardW * 2f + 14f, 0f) };
            list.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(list);

            bool any = false;
            any |= Section(list, "羈絆", BondEntries(game), new Color(1f, 0.62f, 0.72f));
            any |= Section(list, "主動技能", SkillEntries(game.Player), new Color(0.55f, 0.82f, 1f));
            any |= Section(list, "大覺醒", AwakenEntries(game), new Color(1f, 0.55f, 0.35f));
            any |= Section(list, "祝福", BlessingEntries(game), new Color(0.6f, 0.92f, 0.7f));
            any |= Section(list, "強化", UpgradeEntries(game, false), Gold);
            any |= Section(list, "卡片", UpgradeEntries(game, true), new Color(0.82f, 0.7f, 1f));
            if (!any) list.AddChild(NewLabel("這一局還沒有拿到任何技能或強化。", 20, Parchment));

            var hint = NewLabel("Esc／右鍵／✕ 關閉　滾輪捲動", 16, new Color(Parchment.R, Parchment.G, Parchment.B, 0.7f), HorizontalAlignment.Center);
            hint.Position = new Vector2(0f, PanelH - 38f);
            hint.Size = new Vector2(PanelW, 26f);
            panel.AddChild(hint);
        }

        /// <summary>一個區塊：彩色標題 ＋ 兩欄小卡（名稱＋說明）。沒有東西就不畫。</summary>
        static bool Section(VBoxContainer list, string header, List<(string name, string desc)> entries, Color accent)
        {
            if (entries.Count == 0) return false;

            var head = NewLabel($"◆ {header}（{entries.Count}）", 22, accent);
            head.CustomMinimumSize = new Vector2(0f, 32f);
            list.AddChild(head);

            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 14);
            grid.AddThemeConstantOverride("v_separation", 10);
            list.AddChild(grid);

            foreach (var (name, desc) in entries)
            {
                var card = new PanelContainer { CustomMinimumSize = new Vector2(CardW, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
                card.AddThemeStyleboxOverride("panel", Rounded(CardFill, new Color(accent.R, accent.G, accent.B, 0.55f), 2, 10));
                grid.AddChild(card);

                var v = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                v.AddThemeConstantOverride("separation", 2);
                card.AddChild(v);

                v.AddChild(NewLabel(name, 19, accent));
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    var d = NewLabel(desc, 15, Parchment);
                    d.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    d.CustomMinimumSize = new Vector2(CardW - 24f, 0f);
                    d.VerticalAlignment = VerticalAlignment.Top;
                    v.AddChild(d);
                }
            }
            return true;
        }

        static string Clean(string s) => string.IsNullOrEmpty(s) ? "" : Tags.Replace(s, "").Trim();

        // ---------------- 內容 ----------------

        static List<(string, string)> BondEntries(SVGame g)
        {
            var list = new List<(string, string)>();
            if (g.Bond == null || !g.Bond.Active) return list;
            foreach (var line in g.Bond.Summary.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                int colon = line.IndexOf('：');
                if (colon > 0) list.Add((line.Substring(0, colon), line.Substring(colon + 1)));
                else list.Add((line, ""));
            }
            return list;
        }

        static List<(string, string)> SkillEntries(SVPlayer p)
        {
            var list = new List<(string, string)>();
            foreach (var sk in p.Skills)
                if (sk != null) list.Add(($"{sk.Name}　Lv.{sk.Level}", Clean(sk.Desc)));
            return list;
        }

        static List<(string, string)> AwakenEntries(SVGame g)
        {
            var list = new List<(string, string)>();
            foreach (var a in g.Awakenings.Skills)
            {
                if (a == null) continue;
                int n = g.Awakenings.Stacks(a.Kind);
                list.Add((n > 1 ? $"{a.Name}　×{n}" : a.Name, Clean(a.Desc)));
            }
            return list;
        }

        static List<(string, string)> BlessingEntries(SVGame g)
        {
            // 同一個祝福可能拿好幾次，合併成「名稱 ×N」
            var order = new List<string>();
            var count = new Dictionary<string, int>();
            var byId = new Dictionary<string, SVPassive>();
            foreach (var b in g.Passives.Chosen)
            {
                if (b == null) continue;
                string id = b.Id ?? b.Name;
                if (!count.ContainsKey(id)) { order.Add(id); count[id] = 0; byId[id] = b; }
                count[id]++;
            }

            var list = new List<(string, string)>();
            foreach (var id in order)
            {
                var b = byId[id];
                list.Add((count[id] > 1 ? $"{b.Name}　×{count[id]}" : b.Name, Clean(b.Desc)));
            }
            return list;
        }

        static List<(string, string)> UpgradeEntries(SVGame g, bool cards)
        {
            var list = new List<(string, string)>();
            foreach (var (u, stacks, isCard) in g.TakenUpgradeList())
                if (isCard == cards)
                    list.Add((stacks > 1 ? $"{u.Name}　×{stacks}" : u.Name, Clean(u.Desc)));
            return list;
        }
    }
}
