// 畫面狀態：圖片、畫面色調、閃爍、震動、淡入淡出。對應 MZ 的 Game_Screen。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public partial class RMZScreen : Node
    {
        public static RMZScreen Instance { get; private set; }

        readonly Dictionary<int, RMZPicture> _pictures = new Dictionary<int, RMZPicture>();

        public Vector4 Tone { get; private set; } = new Vector4(0f, 0f, 0f, 0f);
        Vector4 _toneTarget = new Vector4(0f, 0f, 0f, 0f);
        float _toneDuration;

        public Color FlashColor { get; private set; } = new Color(0f, 0f, 0f, 0f);
        float _flashDuration, _flashTotal;

        public Vector2 ShakeOffset { get; private set; }
        float _shakePower, _shakeSpeed, _shakeDuration, _shakeDirection = 1f, _shakeValue;

        /// <summary>0 = 完全看得見，255 = 全黑。</summary>
        public float FadeOpacity { get; private set; }
        float _fadeTarget, _fadeDuration;

        public override void _EnterTree() => Instance = this;

        public IEnumerable<KeyValuePair<int, RMZPicture>> Pictures => _pictures;

        public RMZPicture Picture(int id) => _pictures.TryGetValue(id, out var p) ? p : null;

        public RMZPicture ShowPicture(int id, string name, int origin, float x, float y,
                                      float sx, float sy, float opacity, int blend)
        {
            if (!_pictures.TryGetValue(id, out var p))
            {
                p = new RMZPicture();
                _pictures[id] = p;
            }
            p.Show(name, origin, x, y, sx, sy, opacity, blend);
            return p;
        }

        public void ErasePicture(int id) => _pictures.Remove(id);
        public void EraseAllPictures() => _pictures.Clear();

        public void StartTint(Vector4 tone, float durationFrames)
        {
            _toneTarget = tone;
            _toneDuration = Mathf.Max(durationFrames, 0f);
            if (_toneDuration <= 0f) Tone = tone;
        }

        public void StartFlash(Color color, float durationFrames)
        {
            FlashColor = color;
            _flashDuration = _flashTotal = Mathf.Max(durationFrames, 1f);
        }

        public void StartShake(float power, float speed, float durationFrames)
        {
            _shakePower = power;
            _shakeSpeed = speed;
            _shakeDuration = durationFrames;
        }

        public void StartFadeOut(float durationFrames)
        {
            _fadeTarget = 255f;
            _fadeDuration = Mathf.Max(durationFrames, 1f);
        }

        public void StartFadeIn(float durationFrames)
        {
            _fadeTarget = 0f;
            _fadeDuration = Mathf.Max(durationFrames, 1f);
        }

        public bool IsFading => _fadeDuration > 0f;

        /// <summary>
        /// 讀檔專用的整頁重置。
        ///
        /// MZ 的 Scene_Load 是把整個 $gameScreen 換掉，所以標題畫面上那堆圖片
        /// （logo、五顆按鈕、黑幕）在讀檔之後不會留在畫面上。這個移植版沒有把畫面狀態
        /// 寫進存檔，所以改成讀檔時整個清乾淨 —— 不清的話地圖其實已經換好了，
        /// 但標題的圖還蓋在最上層，玩起來就是「按了繼續遊戲之後畫面卡死」。
        /// </summary>
        public void ClearForLoad()
        {
            Clear();
            FadeOpacity = 0f;
            _fadeTarget = 0f;
            _fadeDuration = 0f;
        }

        public void Clear()
        {
            _pictures.Clear();
            Tone = _toneTarget = new Vector4(0f, 0f, 0f, 0f);
            _toneDuration = 0f;
            FlashColor = new Color(0f, 0f, 0f, 0f);
            _flashDuration = 0f;
            ShakeOffset = Vector2.Zero;
            _shakeDuration = 0f;
        }

        public override void _Process(double delta)
        {
            float frames = (float)delta * 60f;

            foreach (var p in _pictures.Values) p.Update(frames);

            UpdateTone(frames);
            UpdateFlash(frames);
            UpdateShake(frames);
            UpdateFade(frames);
        }

        void UpdateTone(float frames)
        {
            if (_toneDuration <= 0f) return;
            float d = Mathf.Min(frames, _toneDuration);
            Tone += (_toneTarget - Tone) * (d / _toneDuration);
            _toneDuration -= d;
            if (_toneDuration <= 0f) Tone = _toneTarget;
        }

        void UpdateFlash(float frames)
        {
            if (_flashDuration <= 0f) return;
            _flashDuration = Mathf.Max(_flashDuration - frames, 0f);
            var c = FlashColor;
            c.A *= _flashDuration / _flashTotal;
            FlashColor = _flashDuration <= 0f ? new Color(0f, 0f, 0f, 0f) : c;
        }

        void UpdateShake(float frames)
        {
            if (_shakeDuration <= 0f && Mathf.IsZeroApprox(_shakeValue))
            {
                ShakeOffset = Vector2.Zero;
                return;
            }

            float delta = (_shakePower * _shakeSpeed * _shakeDirection) / 10f;
            if (_shakeDuration <= 1f && _shakeValue * (_shakeValue + delta) < 0f) _shakeValue = 0f;
            else _shakeValue += delta * frames;

            if (_shakeValue > _shakePower * 2f) _shakeDirection = -1f;
            if (_shakeValue < -_shakePower * 2f) _shakeDirection = 1f;

            _shakeDuration = Mathf.Max(_shakeDuration - frames, 0f);
            ShakeOffset = new Vector2(_shakeValue, 0f);
        }

        void UpdateFade(float frames)
        {
            if (_fadeDuration <= 0f) return;
            float d = Mathf.Min(frames, _fadeDuration);
            FadeOpacity += (_fadeTarget - FadeOpacity) * (d / _fadeDuration);
            _fadeDuration -= d;
            if (_fadeDuration <= 0f) FadeOpacity = _fadeTarget;
        }
    }
}
