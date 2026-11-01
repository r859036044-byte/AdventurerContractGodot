// 成就系統，對應 RM 的 HC_Achievements（＋ HC_AchievementsIconPatch、CE89 的圖示替換）。
//
//   成就清單：rmz/data/achievements.json（從 RM 專案 js/plugins.js 的 HC_Achievements 參數轉出來，共 88 個）
//   條件：switch_on（開關開著）、variable_at_least（變數 ≥ 目標值）；同一個成就的條件要全部成立
//   檢查頻率：跟原插件一樣每 20 幀檢查一次
//   存放：跟原插件一樣存在每個存檔裡（RMZSaveData.Achievements），新遊戲從零開始
//   解鎖提示：右下角滑出來的小卡片（{成就解鎖}／{隱藏成就}：名稱），音效 Item1／Item3
//   成就頁：插件指令 HC_Achievements / OpenAchievementBrowser（遊戲內選單的成就鈕）
using System.Collections.Generic;
using System.IO;
using Godot;
using Newtonsoft.Json;

namespace RMZ
{
    public class RMZAchievementCondition
    {
        public string type = "";
        public int id;
        public int value;
    }

    public class RMZAchievementData
    {
        public string id = "";
        public string name = "";
        public string description = "";
        public int icon;
        public bool hidden;
        public bool rare;
        public List<RMZAchievementCondition> conditions = new List<RMZAchievementCondition>();
    }

    public static class RMZAchievements
    {
        const int CheckInterval = 20;          // 幀
        public const int UnlockedIcon = 87;    // CE89 的圖示替換：已解鎖
        public const int LockedIcon = 195;     //                  未解鎖

        static List<RMZAchievementData> _all;
        static readonly HashSet<string> Unlocked = new HashSet<string>();
        static readonly List<string> UnlockOrder = new List<string>();
        static float _frames;

        public static List<RMZAchievementData> All
        {
            get
            {
                if (_all != null) return _all;
                try
                {
                    string path = ProjectSettings.GlobalizePath("res://rmz/data/achievements.json");
                    _all = File.Exists(path)
                        ? JsonConvert.DeserializeObject<List<RMZAchievementData>>(File.ReadAllText(path))
                        : new List<RMZAchievementData>();
                }
                catch (System.Exception e)
                {
                    GD.PushWarning("[RMZ] 讀不到成就清單：" + e.Message);
                    _all = new List<RMZAchievementData>();
                }
                return _all;
            }
        }

        public static bool IsUnlocked(string id) => Unlocked.Contains(id);
        public static int UnlockedCount => Unlocked.Count;

        static bool Met(RMZAchievementData a)
        {
            if (a.conditions == null || a.conditions.Count == 0) return false;
            foreach (var c in a.conditions)
            {
                switch (c.type)
                {
                    case "switch_on": if (!RMZGameState.Switch(c.id)) return false; break;
                    case "variable_at_least": if (RMZGameState.Variable(c.id) < c.value) return false; break;
                    default: return false;   // battle_count：這個專案沒用到
                }
            }
            return true;
        }

        /// <summary>每幀呼叫；每 20 幀檢查一次有沒有新成就。</summary>
        public static void Tick(float frames)
        {
            _frames += frames;
            if (_frames < CheckInterval) return;
            _frames = 0f;

            // 標題畫面（還沒開始遊戲）不檢查
            if (RMZBootstrap.Instance == null || RMZBootstrap.Instance.CurrentMapId == RMZBootstrap.Instance.TitleMapId) return;

            foreach (var a in All)
            {
                if (Unlocked.Contains(a.id) || !Met(a)) continue;
                Unlock(a);
            }
        }

        public static void Unlock(string id)
        {
            foreach (var a in All) if (a.id == id) { Unlock(a); return; }
        }

        static void Unlock(RMZAchievementData a)
        {
            if (!Unlocked.Add(a.id)) return;
            UnlockOrder.Add(a.id);
            RMZAchievementPopup.Show(a);
        }

        // ---------------- 存讀檔 ----------------

        public static void Reset()
        {
            Unlocked.Clear();
            UnlockOrder.Clear();
            _frames = 0f;
        }

        public static void Capture(List<string> into)
        {
            into.Clear();
            into.AddRange(UnlockOrder);
        }

        public static void Restore(List<string> saved)
        {
            Reset();
            if (saved == null) return;
            foreach (var id in saved)
                if (!string.IsNullOrEmpty(id) && Unlocked.Add(id)) UnlockOrder.Add(id);
        }
    }

    /// <summary>每幀推一次成就檢查。RMZBootstrap 建立。</summary>
    public partial class RMZAchievementDriver : Node
    {
        public override void _Process(double delta) => RMZAchievements.Tick((float)delta * 60f);
    }

    /// <summary>解鎖提示：右下角的小卡片，最多同時 3 張，往上疊。</summary>
    public partial class RMZAchievementPopup : CanvasLayer
    {
        const float Width = 420f, Height = 86f, Spacing = 10f, Margin = 24f;
        const float FadeIn = 14f, FadeOut = 24f, Duration = 150f, Slide = 28f;   // 幀
        const int MaxVisible = 3;

        static RMZAchievementPopup _instance;
        static readonly Queue<RMZAchievementData> Pending = new Queue<RMZAchievementData>();

        class Card { public Control Node; public float Age; }
        readonly List<Card> _cards = new List<Card>();
        Texture2D _icons;

        public static void Show(RMZAchievementData a)
        {
            if (!GodotObject.IsInstanceValid(_instance))
            {
                _instance = new RMZAchievementPopup { Layer = 120 };   // 蓋在對話框、圖片、選單上面
                RMZBootstrap.Instance?.GetTree().Root.AddChild(_instance);
            }
            Pending.Enqueue(a);
        }

        public override void _Ready()
        {
            var iconPath = ProjectSettings.GlobalizePath("res://rmz/img/system/IconSet.png");
            if (File.Exists(iconPath)) _icons = ImageTexture.CreateFromImage(Image.LoadFromFile(iconPath));
        }

        public override void _Process(double delta)
        {
            float frames = (float)delta * 60f;

            if (Pending.Count > 0 && _cards.Count < MaxVisible)
            {
                var a = Pending.Dequeue();
                _cards.Add(new Card { Node = Build(a) });
                RMZAudio.Instance?.PlaySeByName(a.rare ? "Item3" : "Item1", 90);
            }

            for (int i = _cards.Count - 1; i >= 0; i--)
            {
                var c = _cards[i];
                c.Age += frames;
                float alpha = c.Age < FadeIn ? c.Age / FadeIn
                            : c.Age > Duration - FadeOut ? Mathf.Max(0f, (Duration - c.Age) / FadeOut) : 1f;
                c.Node.Modulate = new Color(1f, 1f, 1f, alpha);
                if (c.Age >= Duration) { c.Node.QueueFree(); _cards.RemoveAt(i); }
            }

            // 由下往上排；剛出現的從右邊滑進來
            var size = GetViewport().GetVisibleRect().Size;
            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
                float slide = c.Age < FadeIn ? (1f - c.Age / FadeIn) * Slide : 0f;
                c.Node.Position = new Vector2(size.X - Width - Margin + slide,
                                              size.Y - Margin - Height - i * (Height + Spacing));
            }
        }

        Control Build(RMZAchievementData a)
        {
            var accent = a.rare ? new Color(1f, 0.765f, 0f, 0.98f) : new Color(0f, 0.627f, 1f, 0.95f);
            var panel = new PanelContainer { Size = new Vector2(Width, Height), MouseFilter = Control.MouseFilterEnum.Ignore };
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(20 / 255f, 20 / 255f, 20 / 255f, 0.9f),
                BorderColor = accent, BorderWidthLeft = 6,
                CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
                ContentMarginLeft = 16, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 8,
            });
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 12);
            panel.AddChild(row);

            int iconIndex = a.icon > 0 ? a.icon : RMZAchievements.UnlockedIcon;
            if (_icons != null)
                row.AddChild(new TextureRect
                {
                    Texture = new AtlasTexture { Atlas = _icons, Region = new Rect2((iconIndex % 16) * 32, (iconIndex / 16) * 32, 32, 32) },
                    CustomMinimumSize = new Vector2(48, 48), StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                });

            var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddChild(col);
            string head = RMZLocalization.Translate(a.rare ? "{隱藏成就}" : "{成就解鎖}");
            col.AddChild(Label(head + "：" + RMZLocalization.Translate(a.name), 20, Colors.White));
            col.AddChild(Label(RMZLocalization.Translate(a.description), 16, new Color(0.8f, 0.82f, 0.86f)));

            AddChild(panel);
            panel.Modulate = new Color(1, 1, 1, 0);
            return panel;
        }

        static Label Label(string text, int size, Color color)
        {
            var l = new Label { Text = text, ClipText = true, MouseFilter = Control.MouseFilterEnum.Ignore };
            l.AddThemeFontOverride("font", RMZFonts.Main);
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", color);
            return l;
        }
    }

    /// <summary>成就頁（插件指令 OpenAchievementBrowser）。</summary>
    public partial class RMZAchievementBrowser : CanvasLayer
    {
        static RMZAchievementBrowser _instance;
        public static bool IsOpen => GodotObject.IsInstanceValid(_instance);
        public static ulong ClosedFrame;

        public static void Open()
        {
            if (IsOpen) return;
            _instance = new RMZAchievementBrowser { Layer = 92 };
            RMZBootstrap.Instance.GetTree().Root.AddChild(_instance);
            RMZAudio.Instance?.PlaySeByName("Decision2");
        }

        Texture2D _icons;

        public override void _Ready()
        {
            var iconPath = ProjectSettings.GlobalizePath("res://rmz/img/system/IconSet.png");
            if (File.Exists(iconPath)) _icons = ImageTexture.CreateFromImage(Image.LoadFromFile(iconPath));

            var screen = new Control { Theme = new Theme { DefaultFont = RMZFonts.Main, DefaultFontSize = 20 } };
            AddChild(screen); screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var dim = new ColorRect { Color = new Color(0, 0, 0, .7f) };
            screen.AddChild(dim); dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.10f, 0.08f, 0.07f, 0.96f), BorderColor = new Color("cbaa58"),
                BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
                CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
                ContentMarginLeft = 20, ContentMarginRight = 20, ContentMarginTop = 16, ContentMarginBottom = 16,
            });
            screen.AddChild(panel); panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            panel.OffsetLeft = 120; panel.OffsetRight = -120; panel.OffsetTop = 40; panel.OffsetBottom = -40;

            var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10); panel.AddChild(column);
            var header = new HBoxContainer(); column.AddChild(header);
            header.AddChild(new Label
            {
                Text = $"{RMZLocalization.Translate("{成就}")}　{RMZAchievements.UnlockedCount} / {RMZAchievements.All.Count}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            });
            var close = new Button { Text = "關閉 ×" }; close.Pressed += Close; header.AddChild(close);

            var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            column.AddChild(scroll);
            var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            list.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(list);

            foreach (var a in RMZAchievements.All)
            {
                bool on = RMZAchievements.IsUnlocked(a.id);
                bool masked = a.hidden && !on;

                var row = new PanelContainer { CustomMinimumSize = new Vector2(0, 84) };
                row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
                {
                    BgColor = on ? new Color(0.18f, 0.15f, 0.10f, 0.9f) : new Color(0.12f, 0.12f, 0.13f, 0.9f),
                    BorderColor = on ? (a.rare ? new Color(1f, 0.77f, 0f) : new Color(0f, 0.63f, 1f)) : new Color(0.3f, 0.3f, 0.32f),
                    BorderWidthLeft = 5, CornerRadiusTopLeft = 6, CornerRadiusBottomLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomRight = 6,
                    ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 6, ContentMarginBottom = 6,
                });
                list.AddChild(row);

                var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 14); row.AddChild(h);
                var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
                h.AddChild(text);
                var name = new Label { Text = masked ? "???" : RMZLocalization.Translate(a.name) };
                name.AddThemeColorOverride("font_color", on ? Colors.White : new Color(0.65f, 0.65f, 0.68f));
                text.AddChild(name);
                var desc = new Label { Text = masked ? "解鎖以查看詳情。" : RMZLocalization.Translate(a.description), AutowrapMode = TextServer.AutowrapMode.WordSmart };
                desc.AddThemeFontSizeOverride("font_size", 16);
                desc.AddThemeColorOverride("font_color", new Color(0.75f, 0.76f, 0.8f));
                text.AddChild(desc);

                // 右邊：已解鎖／未解鎖的圖示（跟 RM 的 CE89 替換一樣用 87／195）
                int icon = on ? RMZAchievements.UnlockedIcon : RMZAchievements.LockedIcon;
                if (_icons != null)
                    h.AddChild(new TextureRect
                    {
                        Texture = new AtlasTexture { Atlas = _icons, Region = new Rect2((icon % 16) * 32, (icon / 16) * 32, 32, 32) },
                        CustomMinimumSize = new Vector2(40, 40), StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                    });
            }
        }

        void Close()
        {
            RMZAudio.Instance?.PlaySeByName("Cancel2");
            QueueFree();
        }

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
}
