// 鏡頭跟隨與縮放，對應 GALV_CamControlMZ。
//
// GALV 的捲動公式有一個容易踩到的陷阱：
//   sx = |screenX - camWidth| / camSpeed
// 其中 screenX 是「像素」，但算出來的 sx 直接交給 Game_Map.scrollLeft()，
// 而 _displayX 的單位是「格」。所以實際每幀移動的比例是 48 / camSpeed。
//
// Godot 版：用 Camera2D 的 Zoom（放大倍率）取代 Unity 的 orthographicSize，
// 兩者是倒數關係 —— zoom 2 = 看到的範圍變一半 = 放大兩倍，剛好等於 GALV 的 zScale。
using Godot;

namespace RMZ
{
    public partial class RMZCameraFollow : Camera2D
    {
        public Node2D Target;

        /// <summary>關掉就不會被地圖邊界卡住，等同於 GALV 聚焦角色時的行為</summary>
        public bool ClampToMap = true;

        /// <summary>GALV 的 camSpeed。0 = 立刻貼齊；數值越大越慢。專案裡多半是 300</summary>
        public float FollowSpeed = 0f;

        /// <summary>畫面高度顯示幾格（未縮放時）。MZ 的 720p 約 15 格</summary>
        public float BaseTilesHigh = 15f;

        /// <summary>未對齊像素前的真實位置。對齊後的誤差不能累積回來，否則鏡頭會走不動。</summary>
        Vector2 _exact;
        bool _exactInitialised;

        // ---- 縮放 ----
        float _zoom = 1f;
        float _zoomFrom = 1f;
        float _zoomTarget = 1f;
        float _zoomDuration;
        float _zoomTotal;

        public bool IsZooming => _zoomDuration > 0f;

        bool _snapNext;
        /// <summary>下一幀直接貼齊目標（場所移動時，對應 MZ 的 Game_Player.center）。</summary>
        public void SnapToTarget() => _snapNext = true;

        public override void _Ready()
        {
            // Unity 的 LateUpdate：角色都算完了鏡頭才跟上，不然會晃
            ProcessPriority = 200;
            ApplyZoom();
        }

        /// <param name="scale">GALV 的 zScale：2 = 放大兩倍</param>
        /// <param name="durationFrames">0 或 1 代表立即</param>
        public void SetZoom(float scale, float durationFrames)
        {
            scale = Mathf.Max(scale, 0.01f);
            _zoomFrom = _zoom;
            _zoomTarget = scale;
            _zoomTotal = _zoomDuration = Mathf.Max(durationFrames, 0f);

            if (_zoomDuration <= 1f)
            {
                _zoom = scale;
                _zoomDuration = 0f;
                ApplyZoom();
            }
        }

        /// <summary>
        /// 視窗高度 720、一格 48，未縮放時剛好 15 格，所以 Zoom = 1。
        /// 若之後改視窗大小，這裡會自動換算回「畫面高度顯示 BaseTilesHigh 格」。
        /// </summary>
        void ApplyZoom()
        {
            float viewH = GetViewportRect().Size.Y;
            float wanted = viewH / (BaseTilesHigh * RMZConfig.TileHeight);
            Zoom = new Vector2(wanted * _zoom, wanted * _zoom);
        }

        void UpdateZoom(float frames)
        {
            if (_zoomDuration <= 0f) return;

            _zoomDuration = Mathf.Max(_zoomDuration - frames, 0f);
            float t = _zoomTotal <= 0f ? 1f : 1f - _zoomDuration / _zoomTotal;
            _zoom = Mathf.Lerp(_zoomFrom, _zoomTarget, t);

            if (_zoomDuration <= 0f) _zoom = _zoomTarget;
            ApplyZoom();
        }

        public override void _Process(double delta)
        {
            float frames = (float)delta * 60f;
            UpdateZoom(frames);

            if (Target == null) return;

            if (!_exactInitialised)
            {
                _exact = GlobalPosition;
                _exactInitialised = true;
            }

            Vector2 desired = Target.GlobalPosition;

            var map = RMZGameMap.Instance;
            if (ClampToMap && map != null && map.Data != null)
            {
                // 可視範圍的一半（像素）
                float halfH = GetViewportRect().Size.Y / 2f / Zoom.Y;
                float halfW = GetViewportRect().Size.X / 2f / Zoom.X;

                float mapW = map.Width * RMZConfig.TileWidth;
                float mapH = map.Height * RMZConfig.TileHeight;

                if (!map.LoopHorizontal)
                {
                    if (mapW <= halfW * 2f) desired.X = mapW / 2f;
                    else desired.X = Mathf.Clamp(desired.X, halfW, mapW - halfW);
                }
                if (!map.LoopVertical)
                {
                    if (mapH <= halfH * 2f) desired.Y = mapH / 2f;
                    else desired.Y = Mathf.Clamp(desired.Y, halfH, mapH - halfH);
                }
            }

            if (_snapNext)
            {
                _snapNext = false;
                _exact = desired;
            }
            else if (FollowSpeed > 0f)
            {
                // 每幀移動剩餘距離的 (48 / camSpeed)。speed 300 約等於 16%/幀。
                float rate = Mathf.Clamp(RMZConfig.TileWidth / FollowSpeed * frames, 0f, 1f);
                _exact += (desired - _exact) * rate;
            }
            else
            {
                _exact = desired;
            }

            // 鏡頭也要落在整數像素上，否則角色再怎麼對齊，相對位置還是半個像素
            GlobalPosition = RMZConfig.Snap(_exact);
        }
    }
}
