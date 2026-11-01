// 快速劇情：對話時按住左鍵／右鍵／空白／Enter／Ctrl，
// 文字瞬間顯示、自動往下推、演出也一起加速。
//
// Godot 版差異：Unity 的 Time.timeScale 對應 Engine.TimeScale，
// 由 RMZFastForwardDriver（Bootstrap 建立的節點）負責套用與還原。
using Godot;

namespace RMZ
{
    public class RMZFastForwardSettings
    {
        /// <summary>關掉就完全沒有快轉功能</summary>
        public bool Enabled = true;

        // ---- 觸發按鍵 ----

        /// <summary>按住滑鼠左鍵。左鍵本來就是推進對話的鍵，靠 EngageDelay 區分短按與長按</summary>
        public bool UseLeftMouse = true;
        public bool UseRightMouse = true;
        public bool UseSpaceKey = true;

        /// <summary>按住 Enter。跟左鍵一樣是推進鍵，短按不算快轉</summary>
        public bool UseEnterKey = true;

        /// <summary>按住 Ctrl（MZ 內建的跳過鍵）</summary>
        public bool UseCtrlKey = true;

        /// <summary>要按住多久才開始快轉。太小的話正常點左鍵／按 Enter 推進對話會誤觸</summary>
        public float EngageDelay = 0.25f;

        // ---- 速度 ----

        /// <summary>每段對話停留幾秒才自動翻頁。越小越快，0 = 完全不停</summary>
        public float HoldSeconds = 0.12f;

        /// <summary>快轉時文字直接整段顯示，不要逐字</summary>
        public bool InstantText = true;

        /// <summary>上面沒勾時，快轉的逐字速度（每秒幾個字）</summary>
        public float FastCharsPerSecond = 600f;

        /// <summary>事件裡「等待」指令與演出的加速倍率</summary>
        public float WaitSpeed = 6f;

        /// <summary>整體時間加速倍率。角色走位、鏡頭運鏡也會跟著變快。1 = 不加速</summary>
        public float TimeScale = 3f;

        // ---- 安全 ----

        /// <summary>出現選項時停止快轉，免得手一滑選錯</summary>
        public bool StopAtChoices = true;

        /// <summary>只有事件正在跑的時候才加速。關掉的話平常走路按住也會變快</summary>
        public bool OnlyDuringEvents = true;
    }

    public static class RMZFastForward
    {
        static readonly RMZFastForwardSettings Fallback = new RMZFastForwardSettings();

        public static RMZFastForwardSettings Settings =>
            RMZBootstrap.Instance != null && RMZBootstrap.Instance.FastForward != null
                ? RMZBootstrap.Instance.FastForward
                : Fallback;

        static float Now => Time.GetTicksMsec() / 1000f;

        /// <summary>按鍵有沒有被按住（不管現在該不該快轉）。</summary>
        public static bool Held
        {
            get
            {
                var s = Settings;
                if (!s.Enabled) return false;

                if (s.UseLeftMouse && Input.IsMouseButtonPressed(MouseButton.Left)) return true;
                if (s.UseRightMouse && Input.IsMouseButtonPressed(MouseButton.Right)) return true;
                if (s.UseSpaceKey && Input.IsKeyPressed(Key.Space)) return true;
                if (s.UseCtrlKey && Input.IsKeyPressed(Key.Ctrl)) return true;
                if (s.UseEnterKey && (Input.IsKeyPressed(Key.Enter) || Input.IsKeyPressed(Key.KpEnter))) return true;

                return false;
            }
        }

        static float _heldSince = -1f;

        /// <summary>
        /// 按住的時間夠久了沒。
        /// 左鍵／Enter／空白本來就是推進對話的鍵，短按不能算快轉，
        /// 否則正常點一下對話就會被整段跳掉。
        /// </summary>
        public static bool Engaged
        {
            get
            {
                if (!Held) { _heldSince = -1f; return false; }
                if (_heldSince < 0f) _heldSince = Now;
                return Now - _heldSince >= Mathf.Max(0f, Settings.EngageDelay);
            }
        }

        /// <summary>現在是不是真的在快轉。</summary>
        public static bool Active
        {
            get
            {
                if (!Engaged) return false;
                if (RMZTutorialOverlay.IsOpen || RMZGuildPanels.IsOpen || RMZSystemMenu.IsOpen) return false;   // 選單開著時按著滑鼠不算快轉

                var s = Settings;
                if (s.StopAtChoices && RMZMessage.Instance.HasChoices) return false;

                // 事件暫停地圖（Hendrix pauseEvents）時通常是圖片 UI 在等點擊，
                // 這種畫面按住左鍵不該變成快轉。
                var runner = RMZEventRunner.Instance;
                if (runner != null && runner.EventsPaused) return false;

                if (s.OnlyDuringEvents)
                {
                    if (runner == null || !runner.IsBusy) return false;
                }
                return true;
            }
        }

        /// <summary>delta 已由 Engine.TimeScale 加速；不可再縮短等待，否則會超前人物走位。</summary>
        public static float WaitMultiplier => 1f;

        /// <summary>逐字顯示的速度（每秒幾個字）。快轉且 InstantText 時回傳極大值＝整段直接出現。</summary>
        public static float CharsPerSecond
        {
            get
            {
                if (!Active) return 60f;                       // MZ 的一幀一字
                var s = Settings;
                return s.InstantText ? float.PositiveInfinity : Mathf.Max(60f, s.FastCharsPerSecond);
            }
        }
    }

    /// <summary>把 Engine.TimeScale 接上快轉。由 Bootstrap 建立。</summary>
    public partial class RMZFastForwardDriver : Node
    {
        bool _applied;

        public override void _Process(double delta)
        {
            bool want = RMZFastForward.Active && RMZFastForward.Settings.TimeScale > 1f;

            if (want)
            {
                Engine.TimeScale = RMZFastForward.Settings.TimeScale;
                _applied = true;
            }
            else if (_applied)
            {
                Engine.TimeScale = 1f;
                _applied = false;
            }
        }

        public override void _ExitTree()
        {
            if (!_applied) return;
            Engine.TimeScale = 1f;
            _applied = false;
        }
    }
}
