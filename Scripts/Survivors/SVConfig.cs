// 小遊戲「倖存者」的全域設定。
//
// 這個資料夾底下的東西跟 RMZ 那套完全分開：不同命名空間、不共用任何類別，
// 只有「從 StreamingAssets 讀同一批 PNG」這件事是重疊的。
// 之後要接進本篇（例如公會接一個委託進入小遊戲）再另外做橋接層。
using System.IO;
using RMZS.GodotPort;

namespace RMZS
{
    public static class SVConfig
    {
        /// <summary>RM 角色圖一格 48x48，1 格 = 1 Unity 單位。</summary>
        public const float PixelsPerUnit = 48f;

        public static string CharacterImgPath =>
            Path.Combine(Application.streamingAssetsPath, "rmz", "img", "characters");

        // ---- 場地 ----
        /// <summary>競技場半徑（Unity 單位）。玩家跑到邊界會被擋住。</summary>
        public const float ArenaRadius = 26f;

        // ---- 場地形狀（A / S 級兩關制用）----
        //
        // 預設是圓形（沿用 ArenaRadius，C／B 級跟單機測試都是這樣）。
        // 兩關制的地圖改長方形，SVGame.BuildArena() 會在開局時把這兩個切過去，
        // 邊界判定、補給品落點這些地方改呼叫 ClampToArena / OutsideArena 就好，
        // 不用每個呼叫點自己再判斷一次形狀。
        public static bool RectArena = false;
        public static Vector2 RectHalfSize = new Vector2(ArenaRadius, ArenaRadius);

        /// <summary>把座標夾回場地邊界內。margin 是額外再往內縮多少。</summary>
        public static Vector2 ClampToArena(Vector2 pos, float margin = 0f)
        {
            if (!RectArena) return Vector2.ClampMagnitude(pos, Mathf.Max(0.1f, ArenaRadius - margin));

            float hx = Mathf.Max(0.1f, RectHalfSize.x - margin);
            float hy = Mathf.Max(0.1f, RectHalfSize.y - margin);
            return new Vector2(Mathf.Clamp(pos.x, -hx, hx), Mathf.Clamp(pos.y, -hy, hy));
        }

        /// <summary>這個點是不是已經在場地外面（超過邊界 + margin）。</summary>
        public static bool OutsideArena(Vector2 pos, float margin = 0f)
        {
            if (!RectArena) return pos.magnitude > ArenaRadius + margin;
            return Mathf.Abs(pos.x) > RectHalfSize.x + margin || Mathf.Abs(pos.y) > RectHalfSize.y + margin;
        }

        // ---- 玩家 ----
        public const float PlayerSpeed = 5.0f;

        /// <summary>隊友的移動速度，相對於玩家。跑太快會搶戲，慢一點才看得清楚誰在哪。</summary>
        public const float CompanionSpeed = 0.575f;
        public const int PlayerMaxHp = 100;
        public const float PlayerInvincibleTime = 0.6f;
        public const float PickupRadius = 1.6f;

        // ---- 節奏 ----
        /// <summary>每隔幾秒難度往上一階。</summary>
        public const float DifficultyStep = 30f;
        /// <summary>每隔幾秒來一次王。</summary>
        public const float BossInterval = 90f;

        // ---- 顯示 ----
        /// <summary>預設視野。數字是「畫面半高」有幾個單位，越小看得越近。</summary>
        public const float CameraSize = 8.5f;

        /// <summary>滾輪縮放的範圍。</summary>
        public const float CameraSizeMin = 4.5f;
        public const float CameraSizeMax = 18f;

        /// <summary>
        /// 滾一格的縮放倍率。用「乘除」而不是「加減」，
        /// 這樣拉近和拉遠的手感才會一致 —— 加減的話拉近會越滾越猛。
        /// </summary>
        public const float CameraZoomStep = 1.12f;

        /// <summary>縮放的跟隨速度，越大越跟手。</summary>
        public const float CameraZoomSmooth = 14f;

        /// <summary>Y 越小畫越前面，跟 RM 一樣。</summary>
        public static int SortingOrderFor(float y) => Mathf.RoundToInt(-y * 100f);

        public static Vector2 RandomOnCircle(float radius)
        {
            float a = Random.value * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
    }
}
