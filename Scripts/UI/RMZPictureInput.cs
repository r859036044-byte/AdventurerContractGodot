// 圖片按鈕的滑鼠狀態，對應 Hendrix_Pictures_UI_Creator 的 pictureAction()。
//
// 標題選單（開始遊戲／繼續／設定／語言／結束）就是靠這個運作的：
// 事件用「條件分歧 → 腳本 → pictureAction(11, 'click')」來問「11 號圖片被點了嗎」。
//
// Godot 版差異：Unity 的 InputSystem 有 wasPressedThisFrame，
// 這裡改用 Input.IsActionJustPressed 與自己記邊緣的方式。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public static class RMZPictureInput
    {
        /// <summary>游標底下最上層的圖片編號，-1 表示沒有。</summary>
        public static int Hovered { get; private set; } = -1;

        /// <summary>這一幀按下左鍵時，游標底下的圖片編號。</summary>
        public static int Clicked { get; private set; } = -1;

        public static int RightClicked { get; private set; } = -1;

        static ulong _frame = ulong.MaxValue;

        public static void Update(RMZScreenView layer, Vector2 mousePos, bool leftPressed, bool rightPressed)
        {
            // 一幀只算一次
            ulong frame = Engine.GetProcessFrames();
            if (_frame == frame) return;
            _frame = frame;

            Clicked = -1;
            RightClicked = -1;
            Hovered = -1;

            if (layer == null) return;

            // 女主換裝視窗開著（或剛關掉那一幀）、滑鼠停在「更換裝備」鈕上：底下的 RM 圖片按鈕不吃點擊
            if (RMZHeroineGearPanel.IsOpen || frame <= RMZHeroineGearPanel.ClosedFrame + 1 || RMZHeroineGearButton.Hovered
                || RMZAchievementBrowser.IsOpen || frame <= RMZAchievementBrowser.ClosedFrame + 1)
                return;

            Hovered = layer.PictureAt(mousePos);

            if (Hovered >= 0)
            {
                if (leftPressed) Clicked = Hovered;
                if (rightPressed) RightClicked = Hovered;
            }

            // ESC／右鍵 = 按下畫面上的「離開」按鈕。
            // RM 那邊的事件只判斷點擊那張圖片，沒有按鍵判定，
            // 所以這裡把取消鍵轉譯成「點了離開鈕」，行為才符合直覺。
            if (Clicked < 0 && (rightPressed || RMZEventRunner.CancelPressed()))
            {
                int exit = layer.FindPictureByName(ExitButtonKeyword);
                if(exit < 0) exit = layer.FindPictureByName("back button");
                // 右上角的圖片選單（CE90：存／讀／設置／離開）開著時，ESC 是「收起選單」
                // （等於再點一次選單鈕 90），不是去按裡面那顆「離開遊戲」
                if (exit == MenuExitPicture) exit = MenuButtonPicture;
                if (exit >= 0) Clicked = exit;
            }
        }

        const int MenuButtonPicture = 90, MenuExitPicture = 96;

        /// <summary>
        /// 畫面上有 RM 圖片做的介面（有離開鈕或返回鈕）開著。
        /// 這時 ESC 要先關那個介面，系統選單不要跟著跳出來。
        /// </summary>
        public static bool PictureScreenOwnsEscape()
        {
            var screen = RMZScreen.Instance;
            if (screen == null) return false;
            foreach (var kv in screen.Pictures)
            {
                var pic = kv.Value;
                if (pic == null || pic.Opacity <= 1f || string.IsNullOrEmpty(pic.Name)) continue;
                string n = pic.Name.ToLowerInvariant();
                if (n.Contains(ExitButtonKeyword) || n.Contains("back button")) return true;
            }
            return false;
        }

        /// <summary>離開鈕的圖片檔名關鍵字。這個專案用的是 "UI button/Exit button"。</summary>
        public const string ExitButtonKeyword = "exit button";

        // ---------------- 哪些圖片是「按鈕」 ----------------
        //
        // RM 的資料裡沒有任何地方標記某張圖是按鈕，只有事件會去問
        // pictureAction(id, 'click')。所以反過來用：被問過的就是按鈕。

        static readonly Dictionary<int, float> Interactive = new Dictionary<int, float>();

        /// <summary>被問過之後多久還算按鈕。並行事件每幀都會問一輪，留半秒很夠。</summary>
        const float InteractiveWindow = 0.5f;

        static float Now => Time.GetTicksMsec() / 1000f;

        public static bool IsInteractive(int pictureId) =>
            Interactive.TryGetValue(pictureId, out var t) && Now - t <= InteractiveWindow;

        public static void ClearInteractive() => Interactive.Clear();

        // 同一個圖片編號常常在畫面切換前後被兩段不同的並行公共事件拿來當按鈕用
        // （例如組隊畫面的「下一頁」跟選地圖畫面的「開始冒險」共用同一個按鈕編號 244）。
        // 前一段這一幀處理完點擊、換了畫面之後，同一幀輪到後一段還是會看到「這一幀有
        // 點擊」，於是同一次點擊又被當成點了新畫面上同編號的按鈕 —— 等於沒選地圖就被
        // 判定成按了「開始冒險」。
        //
        // 修法：點擊一旦被「某個呼叫者（ownerId）」吃到，這一幀內其他呼叫者就不能再吃
        // 同一個編號；但同一個呼叫者自己重複問（同一段事件裡常常會用 if/elif 問好幾次）
        // 還是照樣看得到，不受影響。ownerId 用「公共事件 id 的負數」或地圖事件 id 區分。
        static readonly Dictionary<int, int> _clickedBy = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _rightClickedBy = new Dictionary<int, int>();
        static ulong _consumeFrame = ulong.MaxValue;

        static void ResetConsumptionIfNewFrame()
        {
            ulong frame = Engine.GetProcessFrames();
            if (_consumeFrame == frame) return;
            _consumeFrame = frame;
            _clickedBy.Clear();
            _rightClickedBy.Clear();
        }

        public static bool Action(int pictureId, string action, int ownerId = 0)
        {
            Interactive[pictureId] = Now;
            ResetConsumptionIfNewFrame();

            switch (action)
            {
                case "click":
                {
                    if (Clicked != pictureId) return false;
                    if (_clickedBy.TryGetValue(pictureId, out var owner) && owner != ownerId) return false;
                    _clickedBy[pictureId] = ownerId;
                    return true;
                }
                case "rightclick":
                {
                    if (RightClicked != pictureId) return false;
                    if (_rightClickedBy.TryGetValue(pictureId, out var owner) && owner != ownerId) return false;
                    _rightClickedBy[pictureId] = ownerId;
                    return true;
                }
                case "hover":
                case "mouseover": return Hovered == pictureId;
                default: return false;
            }
        }
    }
}
