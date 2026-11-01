// 角色頭上的氣泡圖示(！、？、愛心…)，對應 MZ 的 Sprite_Balloon。
//
// img/system/Balloon.png 是標準 MZ 格式：橫向 8 格(動畫格數)、縱向 15 格(氣泡種類，
// balloonId 1~15 對應第 1~15 列)，每格 48x48。播放節奏照 MZ 抄：
//   前 8 格「長出來」的動畫，每格停 8 個邏輯幀；停在最後一格「完整顯示」再多停一段時間；
//   時間到就整個消失。
using Godot;

namespace RMZ
{
    public partial class RMZBalloonView : Node2D
    {
        const int Cols = 8;
        const int Rows = 15;

        // 跟 MZ 的 Sprite_Balloon 對齊：speed()=8(每格動畫停幾個邏輯幀)、waitTime()=12(完整顯示後再停幾格)。
        const float FramesPerStep = 8f;
        const float GrowDuration = Cols * FramesPerStep;   // 64
        const float HoldDuration = 12f;                    // 跟 MZ 一樣：完整顯示後只停 12 幀就消失（原本 96 幀太久，角色都去做下一個動作了氣泡還在）
        const float TotalDuration = GrowDuration + HoldDuration;

        RMZCharacterBase _owner;
        Sprite2D _sprite;
        int _balloonId;
        float _elapsed;
        bool _playing;
        double _accum;

        public bool IsPlaying => _playing;

        public static RMZBalloonView Attach(RMZCharacterBase owner)
        {
            var view = new RMZBalloonView { Name = "Balloon" };
            owner.AddChild(view);
            view._owner = owner;
            return view;
        }

        public override void _Ready()
        {
            _sprite = new Sprite2D
            {
                Name = "Icon",
                Centered = true,
                TextureFilter = TextureFilterEnum.Nearest,
                Visible = false,
                ZAsRelative = false,
                ZIndex = 4000,   // 蓋在角色跟大部分東西上面，跟 MZ 的氣泡一樣浮在最上層
            };
            AddChild(_sprite);
        }

        public void Play(int balloonId)
        {
            if (balloonId <= 0) return;
            var tex = RMZImageCache.LoadSystem("Balloon");
            if (tex == null) return;

            _balloonId = balloonId;
            _elapsed = 0f;
            _accum = 0.0;
            _playing = true;
            _sprite.Visible = true;
            UpdateFrame(tex);
        }

        public void Stop()
        {
            _playing = false;
            _sprite.Visible = false;
        }

        public override void _Process(double delta)
        {
            if (!_playing) return;

            // 跟角色移動用同一套「固定 60Hz 邏輯幀」換算，快轉／掉幀都不會走音。
            _accum += delta * 60.0;
            bool dirty = false;
            while (_accum >= 1.0 - 0.000001)
            {
                _accum -= 1.0;
                _elapsed += 1f;
                dirty = true;

                if (_elapsed >= TotalDuration)
                {
                    Stop();
                    return;
                }
            }

            // 浮在角色頭上正中央；角色圖的錨點在腳底，所以往上位移「整張圖的高度」再加一點留白。
            float height = _owner?.View?.HeightInPixels ?? RMZConfig.TileHeight;
            Position = new Vector2(0f, -height - 24f);

            if (dirty)
            {
                var tex = RMZImageCache.LoadSystem("Balloon");
                if (tex != null) UpdateFrame(tex);
            }
        }

        void UpdateFrame(Texture2D tex)
        {
            int cellW = tex.GetWidth() / Cols;
            int cellH = tex.GetHeight() / Rows;

            // 前 GrowDuration 幀是 0~7 格的長出動畫，之後固定停在最後一格(完整顯示)。
            int frame = _elapsed < GrowDuration
                ? Mathf.Clamp(Mathf.FloorToInt(_elapsed / FramesPerStep), 0, Cols - 1)
                : Cols - 1;
            int row = Mathf.Clamp(_balloonId - 1, 0, Rows - 1);

            _sprite.Texture = new AtlasTexture
            {
                Atlas = tex,
                Region = new Rect2(frame * cellW, row * cellH, cellW, cellH),
                FilterClip = true,
            };
        }
    }
}
