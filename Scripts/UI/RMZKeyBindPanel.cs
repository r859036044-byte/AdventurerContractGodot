// 按鍵設定（選項 → 按鍵設定）。點一列，再按鍵盤上想用的鍵就換好了。
// 換到已經被別的動作用的鍵，兩個會互換。Esc 取消這次換鍵；再按一次 Esc／右鍵關閉。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public partial class RMZKeyBindPanel : CanvasLayer
    {
        static RMZKeyBindPanel _instance;
        public static bool IsOpen => GodotObject.IsInstanceValid(_instance);

        public static void Open(Node host)
        {
            if (IsOpen || host == null) return;
            _instance = new RMZKeyBindPanel { Layer = host is CanvasLayer cl ? cl.Layer + 2 : 47 };
            host.GetTree().Root.AddChild(_instance);
        }

        static readonly Color Gold = new(1f, .9f, .6f);
        Panel _panel;
        readonly List<Button> _keys = new();
        int _waiting = -1;   // 正在等新按鍵的那一列
        Label _hint;
        ulong _openedFrame;

        static StyleBoxFlat Skin(Color fill, Color edge, int r = 4) => new()
        {
            BgColor = fill, BorderColor = edge, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = r, CornerRadiusTopRight = r, CornerRadiusBottomLeft = r, CornerRadiusBottomRight = r,
        };

        static Label MakeLabel(string text, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
        {
            var l = new Label { Text = text, HorizontalAlignment = align, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            l.AddThemeFontOverride("font", RMZFonts.Main); l.AddThemeFontSizeOverride("font_size", size); l.AddThemeColorOverride("font_color", color);
            return l;
        }

        static void Put(Control c, Control p, float x, float y, float w, float h) { p.AddChild(c); c.Position = new Vector2(x, y); c.Size = new Vector2(w, h); }

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            _openedFrame = Engine.GetProcessFrames();
            var root = new Control(); AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var shade = new ColorRect { Color = new Color(0, 0, 0, .6f) }; root.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            var actions = RMZKeyBindings.Actions;
            RMZKeyBindings.Get("skill1");   // 確保讀過設定檔
            float h = 110 + actions.Count * 44 + 60;
            _panel = new Panel(); root.AddChild(_panel); _panel.Size = new Vector2(520, h);
            _panel.AddThemeStyleboxOverride("panel", Skin(new Color(.07f, .08f, .12f, .98f), new Color(.82f, .7f, .38f, .5f), 10));
            Put(MakeLabel("按鍵設定", 26, Gold, HorizontalAlignment.Center), _panel, 12, 12, 496, 34);
            Put(MakeLabel("冒險戰鬥中使用", 15, new Color(.7f, .72f, .78f), HorizontalAlignment.Center), _panel, 12, 46, 496, 22);

            for (int i = 0; i < actions.Count; i++)
            {
                int index = i;
                float y = 80 + i * 44;
                var row = new Panel(); Put(row, _panel, 14, y, 492, 38);
                row.AddThemeStyleboxOverride("panel", Skin(new Color(.13f, .15f, .20f), Colors.Transparent));
                Put(MakeLabel(actions[i].Name, 20, Colors.White), row, 18, 0, 240, 38);
                var key = new Button { FocusMode = Control.FocusModeEnum.None };
                key.AddThemeFontOverride("font", RMZFonts.Main); key.AddThemeFontSizeOverride("font_size", 20);
                key.AddThemeColorOverride("font_color", Gold);
                key.AddThemeStyleboxOverride("normal", Skin(new Color(.2f, .18f, .12f), new Color(.82f, .7f, .38f, .6f)));
                key.AddThemeStyleboxOverride("hover", Skin(new Color(.32f, .27f, .15f), Gold));
                key.AddThemeStyleboxOverride("pressed", Skin(new Color(.4f, .33f, .18f), Gold));
                Put(key, row, 300, 3, 176, 32);
                key.Pressed += () => { _waiting = index; Refresh(); };
                _keys.Add(key);
            }

            float by = 80 + actions.Count * 44 + 10;
            var reset = MakeButton("回復預設", () => { RMZKeyBindings.ResetToDefault(); _waiting = -1; Refresh(); });
            Put(reset, _panel, 30, by, 200, 38);
            var close = MakeButton("關閉", Close);
            Put(close, _panel, 290, by, 200, 38);
            _hint = MakeLabel("", 15, new Color(.78f, .79f, .84f), HorizontalAlignment.Center);
            Put(_hint, _panel, 12, by + 44, 496, 22);
            Refresh();
        }

        Button MakeButton(string text, System.Action onPress)
        {
            var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
            b.AddThemeFontOverride("font", RMZFonts.Main); b.AddThemeFontSizeOverride("font_size", 20);
            b.AddThemeColorOverride("font_color", Gold);
            b.AddThemeStyleboxOverride("normal", Skin(new Color(.13f, .15f, .20f), new Color(.82f, .7f, .38f, .4f)));
            b.AddThemeStyleboxOverride("hover", Skin(new Color(.2f, .22f, .3f), Gold));
            b.AddThemeStyleboxOverride("pressed", Skin(new Color(.25f, .27f, .35f), Gold));
            b.Pressed += onPress;
            return b;
        }

        void Refresh()
        {
            var actions = RMZKeyBindings.Actions;
            for (int i = 0; i < _keys.Count; i++)
                _keys[i].Text = i == _waiting ? "請按下按鍵…" : RMZKeyBindings.KeyName(actions[i].Current);
            _hint.Text = _waiting >= 0 ? "按下想用的鍵；Esc 取消" : "點選要改的按鍵　Esc / 右鍵 關閉";
        }

        void Close()
        {
            RMZAudio.Instance?.PlaySeByName("Cancel2");
            QueueFree();
        }

        public override void _Process(double delta)
        {
            var view = GetViewport().GetVisibleRect().Size;
            float s = Mathf.Min(1f, Mathf.Min((view.X - 24) / _panel.Size.X, (view.Y - 24) / _panel.Size.Y));
            _panel.Scale = Vector2.One * s;
            _panel.Position = (view - _panel.Size * s) / 2f;
        }

        public override void _Input(InputEvent e)
        {
            if (Engine.GetProcessFrames() == _openedFrame) return;
            if (e is InputEventKey k && k.Pressed && !k.Echo)
            {
                var key = k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode;
                GetViewport().SetInputAsHandled();
                if (_waiting >= 0)
                {
                    // Esc 取消；F8（傷害統計）保留不給綁
                    if (key != Key.Escape && key != Key.F8)
                        RMZKeyBindings.Set(RMZKeyBindings.Actions[_waiting].Id, key);
                    _waiting = -1;
                    Refresh();
                }
                else if (key == Key.Escape) Close();
                return;
            }
            if (e is InputEventMouseButton m && m.Pressed && m.ButtonIndex == MouseButton.Right)
            {
                GetViewport().SetInputAsHandled();
                if (_waiting >= 0) { _waiting = -1; Refresh(); } else Close();
            }
        }
    }
}
