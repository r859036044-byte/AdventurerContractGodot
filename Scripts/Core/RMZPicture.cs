// 單張圖片的狀態與補間，對應 MZ 的 Game_Picture。
using Godot;

namespace RMZ
{
    public class RMZPicture
    {
        public string Name = "";
        public int Origin;          // 0=左上 1=中心
        public float X, Y;
        public float ScaleX = 100f, ScaleY = 100f;
        public float Opacity = 255f;
        public int BlendMode;
        public float Angle;

        public Vector4 Tone = new Vector4(0f, 0f, 0f, 0f);        // r,g,b,gray 各 -255~255
        Vector4 _toneTarget = new Vector4(0f, 0f, 0f, 0f);
        float _toneDuration;

        float _targetX, _targetY, _targetScaleX, _targetScaleY, _targetOpacity;
        int _targetOrigin;
        float _moveDuration, _moveTotal;
        int _easingType;
        float _startX, _startY, _startScaleX, _startScaleY, _startOpacity;

        float _rotationSpeed;

        public bool IsMoving => _moveDuration > 0f;
        public bool IsToning => _toneDuration > 0f;

        public void Show(string name, int origin, float x, float y, float sx, float sy, float opacity, int blend)
        {
            Name = name; Origin = origin;
            X = x; Y = y; ScaleX = sx; ScaleY = sy;
            Opacity = opacity; BlendMode = blend;
            Angle = 0f;
            Tone = new Vector4(0f, 0f, 0f, 0f);
            _toneTarget = new Vector4(0f, 0f, 0f, 0f);
            _toneDuration = 0f;
            _moveDuration = 0f;
            _rotationSpeed = 0f;
        }

        public void Move(int origin, float x, float y, float sx, float sy, float opacity, int blend,
                         float durationFrames, int easingType)
        {
            _targetOrigin = origin;
            _targetX = x; _targetY = y;
            _targetScaleX = sx; _targetScaleY = sy;
            _targetOpacity = opacity;
            BlendMode = blend;

            _startX = X; _startY = Y;
            _startScaleX = ScaleX; _startScaleY = ScaleY;
            _startOpacity = Opacity;

            _moveDuration = Mathf.Max(durationFrames, 1f);
            _moveTotal = _moveDuration;
            _easingType = easingType;
        }

        public void Rotate(float speed) => _rotationSpeed = speed;

        public void Tint(Vector4 tone, float durationFrames)
        {
            _toneTarget = tone;
            _toneDuration = Mathf.Max(durationFrames, 1f);
            if (durationFrames <= 0f) Tone = tone;
        }

        /// <param name="frames">經過的幀數（以 60fps 為基準）</param>
        public void Update(float frames)
        {
            UpdateMove(frames);
            UpdateTone(frames);
            Angle += _rotationSpeed * frames / 2f;
        }

        void UpdateMove(float frames)
        {
            if (_moveDuration <= 0f) return;

            _moveDuration = Mathf.Max(_moveDuration - frames, 0f);
            float t = 1f - _moveDuration / _moveTotal;
            float e = ApplyEasing(t, _easingType);

            X = Mathf.Lerp(_startX, _targetX, e);
            Y = Mathf.Lerp(_startY, _targetY, e);
            ScaleX = Mathf.Lerp(_startScaleX, _targetScaleX, e);
            ScaleY = Mathf.Lerp(_startScaleY, _targetScaleY, e);
            Opacity = Mathf.Lerp(_startOpacity, _targetOpacity, e);

            if (_moveDuration <= 0f) Origin = _targetOrigin;
        }

        void UpdateTone(float frames)
        {
            if (_toneDuration <= 0f) return;

            float d = Mathf.Min(frames, _toneDuration);
            Tone += (_toneTarget - Tone) * (d / _toneDuration);
            _toneDuration -= d;
            if (_toneDuration <= 0f) Tone = _toneTarget;
        }

        /// <summary>MZ 的緩動：0=線性 1=慢入 2=慢出 3=慢入慢出</summary>
        static float ApplyEasing(float t, int type)
        {
            switch (type)
            {
                case 1: return t * t * t;
                case 2: { float f = t - 1f; return f * f * f + 1f; }
                case 3: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                default: return t;
            }
        }
    }
}
