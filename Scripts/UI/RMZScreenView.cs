// 把 RMZScreen 的狀態畫出來：圖片、畫面色調、閃爍、淡入淡出。
//
// [移植狀態] Unity 版是 RMZPictureLayer（391 行）+ RMZScreen 的材質效果，
// 這裡是精簡重寫版：圖片、淡入淡出、閃爍已可用；畫面色調用一層半透明色近似
// （MZ 的色調是 r/g/b/gray 四通道的濾鏡，要完全對上得寫 shader，之後再補）。
// 震動 ShakeOffset 由鏡頭那邊套用。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public partial class RMZScreenView : CanvasLayer
    {
        Control _root;
        Control _pictureRoot;
        ColorRect _tone;
        ColorRect _flash;
        ColorRect _fade;

        readonly Dictionary<int, TextureRect> _pictures = new Dictionary<int, TextureRect>();
        readonly List<int> _dead = new List<int>();

        public override void _Ready()
        {
            Layer = 5;               // 地圖之上、對話框（10）之下
            ProcessPriority = 250;

            _root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_root);

            _pictureRoot = new Control { Name = "Pictures", MouseFilter = Control.MouseFilterEnum.Ignore };
            _pictureRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(_pictureRoot);

            _tone = MakeOverlay("Tone");
            _flash = MakeOverlay("Flash");
            _fade = MakeOverlay("Fade");
        }

        ColorRect MakeOverlay(string name)
        {
            var rect = new ColorRect
            {
                Name = name,
                Color = new Color(0f, 0f, 0f, 0f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(rect);
            return rect;
        }

        // 滑鼠邊緣偵測：Godot 的 Input 只有「現在有沒有按著」，
        // 圖片按鈕要的是「這一幀剛按下」，所以自己記上一幀的狀態。
        bool _lastLeft, _lastRight;

        public override void _Process(double delta)
        {
            var screen = RMZScreen.Instance;
            if (screen == null) return;

            UpdatePictures(screen);

            bool left = Input.IsMouseButtonPressed(MouseButton.Left);
            bool right = Input.IsMouseButtonPressed(MouseButton.Right);
            RMZPictureInput.Update(this, _root.GetGlobalMousePosition(),
                                   left && !_lastLeft, right && !_lastRight);
            _lastLeft = left;
            _lastRight = right;

            // H 狀態欄詳細頁右下角的「更換裝備」鈕
            RMZHeroineGearButton.Tick();

            // 圖片按鈕的滑鼠變色：RM 事件資料本身沒有 hover 效果（只判斷點擊），
            // 這裡統一在畫面層補上——凡是被事件問過的「按鈕圖」（RMZPictureInput.IsInteractive），
            // 滑鼠移上去就整張提亮一點，不用每個選單另外刻。
            ApplyPictureHover();

            // 色調：負值當成壓暗、正值當成打亮，先求「看得出來」而不是完全一致
            var t = screen.Tone;
            float dark = Mathf.Clamp(-(t.X + t.Y + t.Z) / 3f / 255f, 0f, 1f);
            float light = Mathf.Clamp((t.X + t.Y + t.Z) / 3f / 255f, 0f, 1f);
            _tone.Color = dark > 0f
                ? new Color(0f, 0f, 0f, dark)
                : new Color(1f, 1f, 1f, light * 0.6f);

            _flash.Color = screen.FlashColor;
            _fade.Color = new Color(0f, 0f, 0f, Mathf.Clamp(screen.FadeOpacity / 255f, 0f, 1f));
        }

        const float HoverBrighten = 1.28f;

        /// <summary>目前滑鼠底下的按鈕圖整體提亮；其他圖片維持原色。</summary>
        void ApplyPictureHover()
        {
            int hovered = RMZPictureInput.Hovered;
            foreach (var kv in _pictures)
            {
                var node = kv.Value;
                if (!IsInstanceValid(node)) continue;
                float boost = kv.Key == hovered ? HoverBrighten : 1f;
                var c = node.Modulate;
                node.Modulate = new Color(boost, boost, boost, c.A);
            }
        }

        void UpdatePictures(RMZScreen screen)
        {
            var size = _root.Size;
            float scale = size.Y / 720f;   // MZ 的畫面是 1280x720

            _dead.Clear();
            foreach (var id in _pictures.Keys)
                if (screen.Picture(id) == null) _dead.Add(id);

            foreach (var id in _dead)
            {
                if (IsInstanceValid(_pictures[id])) _pictures[id].QueueFree();
                _pictures.Remove(id);
            }

            foreach (var kv in screen.Pictures)
            {
                var pic = kv.Value;
                if (!_pictures.TryGetValue(kv.Key, out var node) || !IsInstanceValid(node))
                {
                    node = new TextureRect
                    {
                        Name = $"Picture{kv.Key}",
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                        TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                        StretchMode = TextureRect.StretchModeEnum.Scale,
                        // 不設 IgnoreSize 的話，TextureRect 的最小尺寸 = 貼圖原始大小，
                        // 我們指定的 Size 會被夾回去，RM 的縮放百分比就完全失效
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    };
                    _pictureRoot.AddChild(node);
                    _pictures[kv.Key] = node;
                }

                var tex = RMZImageCache.LoadPicture(pic.Name);
                if (node.Texture != tex) node.Texture = tex;
                if (tex == null) { node.Visible = false; continue; }

                node.Visible = true;
                node.Size = new Vector2(tex.GetWidth() * pic.ScaleX / 100f,
                                        tex.GetHeight() * pic.ScaleY / 100f) * scale;

                // Origin：0 = 左上角對齊指定座標，1 = 中心對齊
                var pos = new Vector2(pic.X, pic.Y) * scale;
                node.Position = pic.Origin == 0 ? pos : pos - node.Size / 2f;

                node.PivotOffset = node.Size / 2f;
                node.RotationDegrees = pic.Angle;
                node.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(pic.Opacity / 255f, 0f, 1f));

                // 混合模式 1/2/3 對應 MZ 的加算／乘算／濾色，之後接 CanvasItemMaterial 再補
                node.ZIndex = kv.Key;
            }
        }

        // ---------------- 圖片按鈕的命中判定 ----------------

        /// <summary>
        /// 螢幕座標底下最上層的「按鈕圖片」編號，-1 表示沒有。
        /// 只算被事件問過的那些（見 RMZPictureInput.IsInteractive），
        /// 否則整片背景圖會把底下的按鈕蓋掉。
        /// 透明的地方不算命中 —— 按鈕圖多半是不規則形狀。
        /// </summary>
        public int PictureAt(Vector2 screenPos)
        {
            int best = -1;
            foreach (var kv in _pictures)
            {
                var node = kv.Value;
                if (!IsInstanceValid(node) || !node.Visible || node.Texture == null) continue;
                if (node.Modulate.A <= 0.01f) continue;
                if (!RMZPictureInput.IsInteractive(kv.Key)) continue;

                var rect = new Rect2(node.Position, node.Size);
                if (!rect.HasPoint(screenPos)) continue;

                var pic = RMZScreen.Instance?.Picture(kv.Key);
                if (pic != null && node.Size.X > 0f && node.Size.Y > 0f)
                {
                    float u = (screenPos.X - rect.Position.X) / rect.Size.X;
                    float v = (screenPos.Y - rect.Position.Y) / rect.Size.Y;
                    if (!RMZImageCache.IsOpaqueAt(RMZConfig.PictureImgPath, pic.Name, u, v)) continue;
                }

                // 編號大的畫在上面，取最上層那張
                if (kv.Key > best) best = kv.Key;
            }
            return best;
        }

        /// <summary>用檔名關鍵字找圖片（不分大小寫）。取消鍵要轉成「點了離開鈕」時用。</summary>
        public int FindPictureByName(string keyword)
        {
            var screen = RMZScreen.Instance;
            if (screen == null || string.IsNullOrEmpty(keyword)) return -1;

            foreach (var kv in screen.Pictures)
            {
                string name = kv.Value?.Name;
                if (string.IsNullOrEmpty(name)) continue;
                if (name.ToLowerInvariant().Contains(keyword.ToLowerInvariant())) return kv.Key;
            }
            return -1;
        }
    }
}