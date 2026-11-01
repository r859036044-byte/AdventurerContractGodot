// 半格移動的設定，對應 HalfMove.js（トリアコンタン）。
//
// 你的專案有啟用 HalfMove，所以「所有角色都是半格移動」：
// 一次「向左移動」只走 0.5 格，不是 1 格。
// 這會影響每一段移動路線的實際距離，沒做的話所有演出的站位都會跑掉。
//
// 這裡的預設值是照你 plugins.js 裡 HalfMove 的參數設的。
using Godot;

namespace RMZ
{
    public static class HalfMoveSettings
    {
        /// <summary>HalfMove 的一格單位 = 0.5</summary>
        public const float TileUnit = 0.5f;

        /// <summary>半格移動總開關（對應 $gameSystem.canHalfMove()）</summary>
        public static bool Enabled = true;

        /// <summary>Direction8Move：允許八方向移動</summary>
        public static bool Direction8 = true;

        /// <summary>EventThrough：側面接觸事件時可以半格閃身過去</summary>
        public static bool EventThrough = true;

        /// <summary>AvoidCorner：直行卡到轉角時改用斜向再試一次</summary>
        public static bool AvoidCorner = true;

        public enum DiagonalFacing
        {
            /// <summary>MZ 原本的規則：只有「正對反方向」才會轉，所以斜走時多半維持原本的朝向。</summary>
            KeepUnlessReversed,
            /// <summary>斜走時看水平那一半（左／右）。</summary>
            Horizontal,
            /// <summary>斜走時看垂直那一半（上／下）。</summary>
            Vertical,
        }

        /// <summary>
        /// 斜向移動時要怎麼決定朝向。
        ///
        /// MZ 原本的 moveDiagonally 只在「目前正對著反方向」時才轉向，
        /// 所以開了八方向之後，玩家斜著跑會一直維持進入斜走前的那個朝向 ——
        /// 看起來就像「角色卡在同一個方向」。
        /// </summary>
        public static DiagonalFacing DiagonalTurn = DiagonalFacing.Horizontal;

        const float Epsilon = 0.001f;

        /// <summary>座標的小數部分是不是剛好 0.5</summary>
        public static bool IsHalfPos(float v) => Mathf.Abs(v - Mathf.Floor(v) - TileUnit) < Epsilon;

        public static bool Same(float a, float b) => Mathf.Abs(a - b) < Epsilon;

        /// <summary>兩個 1x1 的角色框是否重疊</summary>
        public static bool Overlaps(float ax, float ay, float bx, float by) =>
            Mathf.Abs(ax - bx) < 1f - Epsilon && Mathf.Abs(ay - by) < 1f - Epsilon;

        /// <summary>把八方向拆成水平與垂直兩個方向（MZ 的 divideDirection）</summary>
        public static void Divide(int d, out int horizon, out int vertical)
        {
            horizon = (d / 3f) <= 1f ? d + 3 : d - 3;
            vertical = d % 3 == 0 ? d - 1 : d + 1;
        }

        public static bool IsDiagonal(int d) => d % 2 != 0;
    }
}
