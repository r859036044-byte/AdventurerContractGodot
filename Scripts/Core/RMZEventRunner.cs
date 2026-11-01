// 事件的排程與觸發，對應 MZ 的 Game_Map.updateInterpreter + Game_Player 的觸發判定。
//
// 執行順序跟 MZ 一樣：
//   1. 主直譯器（自動執行／玩家觸發的事件）一次只能跑一個
//   2. 並行處理的事件與公共事件各自有獨立的直譯器，同時跑
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public partial class RMZEventRunner : Node
    {
        public static RMZEventRunner Instance { get; private set; }

        readonly RMZInterpreter _main = new RMZInterpreter();
        readonly Dictionary<int, RMZInterpreter> _parallelEvents = new Dictionary<int, RMZInterpreter>();
        readonly Dictionary<int, RMZInterpreter> _parallelCommons = new Dictionary<int, RMZInterpreter>();

        readonly List<RMZEventCharacter> _events = new List<RMZEventCharacter>();
        readonly List<RMZEventCharacter> _snapshot = new List<RMZEventCharacter>();
        readonly HashSet<int> _erased = new HashSet<int>();

        int _mapId;
        bool _needRefresh;
        bool _eventsPaused;

        /// <summary>每次換地圖 +1。走訪事件清單時用來偵測「清單已經被換掉」。</summary>
        int _generation;

        /// <summary>事件正在跑或訊息還開著，這段期間玩家不能動。</summary>
        public bool IsBusy => _main.IsRunning || RMZMessage.Instance.IsBusy;

        /// <summary>診斷用：目前佔住畫面的那個直譯器（自動執行／玩家觸發）。</summary>
        public RMZInterpreter Main => _main;
        public bool EventsPaused => _eventsPaused;

        public override void _EnterTree() => Instance = this;

        public void Setup(int mapId, IEnumerable<RMZEventCharacter> events)
        {
            _mapId = mapId;

            // 這裡刻意「不」清掉 _main。
            //
            // MZ 的 Game_Map.setup() 並不會動 $gameMap._interpreter，所以事件在「場所移動」
            // 之後還可以繼續跑後面的指令 —— 這個遊戲大量依賴這個行為，例如「跳過開頭劇情」
            // 那一段，解鎖移動的「開關99 = OFF」和一堆初始變數全都寫在場所移動的後面。
            // 之前在這裡 Clear() 掉，那些指令就永遠不會執行，結果就是進了地圖卻不能動。
            if (_main.IsRunning)
            {
                GD.Print($"[RMZ] 換地圖到 {mapId}，事件 {_main.EventId} 還在執行，" +
                          $"照 MZ 的行為讓它接著跑（第 {_main.CurrentIndex}/{_main.CommandCount} 條）。");
            }
            else
            {
                _eventsPaused = false;
            }

            // 這些是綁在地圖上的，換地圖要重來
            _parallelEvents.Clear();
            _parallelCommons.Clear();
            _erased.Clear();

            _events.Clear();
            _events.AddRange(events);
            _generation++;
        }

        public void RequestRefresh() => _needRefresh = true;
        public void SetEventsPaused(bool paused) => _eventsPaused = paused;

        /// <summary>這張地圖上的所有事件（劇情腳本「全部事件穿透」那種用）。</summary>
        public IReadOnlyList<RMZEventCharacter> AllEvents => _events;

        public RMZEventCharacter EventById(int id)
        {
            foreach (var e in _events) if (e != null && e.Event != null && e.Event.id == id) return e;
            return null;
        }

        /// <summary>
        /// 丟掉某個事件正在跑的指令。
        /// 只有「事件被消除」才該這樣做 —— 換頁不算（MZ 換頁時舊指令會跑完）。
        /// </summary>
        public void ResetEventInterpreter(int id)
        {
            _parallelEvents.Remove(id);
            if (_main.IsRunning && _main.EventId == id) _main.Clear();
        }

        /// <summary>
        /// 讀檔專用：把所有正在跑的指令全部丟掉。
        ///
        /// Setup() 刻意不清 _main（MZ 的場所移動不會中斷事件），但讀檔不一樣 ——
        /// 標題畫面那條並行事件手上還抓著一堆指令，不清掉的話它會在新地圖上
        /// 接著跑標題的後半段，畫面就亂了。
        /// </summary>
        public void ResetForLoad()
        {
            _main.Clear();
            _parallelEvents.Clear();
            _parallelCommons.Clear();
            _erased.Clear();
            _eventsPaused = false;
            _needRefresh = true;
            ReservedCommonEvent = 0;
        }

        public void EraseEvent(int id)
        {
            _erased.Add(id);
            ResetEventInterpreter(id);
            var ev = EventById(id);
            if (ev != null) ev.SetEnabled(false);
        }

        public override void _Process(double delta)
        {
            RMZPerf.Begin("事件");
            UpdateInternal((float)delta);
            RMZPerf.End();
        }

        void UpdateInternal(float delta)
        {
            if(RMZTutorialOverlay.IsOpen) return;
            float frames = delta * 60f;
            WatchBlackScreen();

            if (_needRefresh)
            {
                _needRefresh = false;
                _snapshot.Clear();
                _snapshot.AddRange(_events);
                foreach (var e in _snapshot) if (e != null) e.RefreshPage();
            }

            // 奴隸區外派：盯著遊戲天數，跳了就結算前一天
            RMZSlaveWork.AutoResolve();

            // 系統選單開著就整個停住 —— 連並行事件也不跑，
            // 不然背景那些偵測點擊的公共事件會在選單底下亂動
            if (RMZGuildPanels.IsOpen || RMZSystemMenu.IsOpen) return;

            // pauseEvents 凍結的是「地圖」：事件不再自律移動、玩家不能走、不接受新的觸發。
            // 直譯器（含並行）一定要繼續跑 —— 開 UI 的那條事件本身，以及負責偵測
            // 點擊的並行公共事件（例如 CE48 冒險隊友選擇）都靠它，停掉的話整個 UI 就死了。
            // 場所移動排隊中（含換地圖的淡出那 0.3 秒）連並行事件都不跑，等地圖換好再說。
            // 不然舊地圖的並行事件會在淡出期間繼續跑 —— 例如讀檔時，標題畫面的開頭動畫事件
            // 會跑到它自己的「場所移動到標題地圖」，把讀檔排好的移動蓋掉，結果又回到主畫面。
            if (RMZBootstrap.Instance != null && RMZBootstrap.Instance.IsTransferring) return;

            UpdateParallel(frames);

            if (_main.IsRunning) { _main.Update(frames); return; }

            // 存檔／讀檔畫面開著時不接受新的觸發（正在等它的那條事件已經在上面跑完了）
            if (RMZSaveScene.IsOpen) return;
            if (RMZDevPanel.IsOpen) return;   // DEV 面板開著時不接受新的觸發
            if (RMZOptionsScene.IsOpen) return;
            if (RMZEnhanceWindow.IsOpen) return;
            if (RMZTavernWindow.IsOpen) return;
            if (RMZSlaveWorkWindow.IsOpen) return;

            if (_eventsPaused) return;   // 暫停中不接受新的觸發

            if (TrySetupAutorun()) { _main.Update(frames); return; }
            if (TrySetupPlayerTrigger()) _main.Update(frames);
        }

        void UpdateParallel(float frames)
        {
            int generation = _generation;

            // 走訪期間事件可能觸發換地圖，所以先取快照，並在每次執行後確認清單還是同一份
            // （用重複使用的 List，不要每幀 ToArray，那會一直產生垃圾）
            _snapshot.Clear();
            _snapshot.AddRange(_events);

            // 地圖上的並行事件
            foreach (var e in _snapshot)
            {
                if (e == null || e.Page == null || e.Trigger != 4) continue;
                if (_erased.Contains(e.Event.id)) continue;
                if (e.Page.list == null || e.Page.list.Count <= 1) continue;

                // 重用同一個直譯器物件。標題的按鈕輪詢事件每幀都會跑完並結束，
                // 每幀 new 一個直譯器（含內部字典）純粹是在餵 GC。
                if (!_parallelEvents.TryGetValue(e.Event.id, out var interp))
                {
                    interp = new RMZInterpreter();
                    _parallelEvents[e.Event.id] = interp;
                }
                if (!interp.IsRunning) interp.Setup(e.Page.list, e.Event.id, _mapId);

                interp.Update(frames);
                if (_generation != generation) return;
            }

            // 並行處理的公共事件（trigger 2，開關打開時執行）
            if (RMZDatabase.CommonEvents == null) return;

            foreach (var ce in RMZDatabase.CommonEvents)
            {
                if (_generation != generation) return;
                if (ce == null || ce.trigger != 2 || ce.list == null || ce.list.Count <= 1) continue;
                if (!RMZGameState.Switch(ce.switchId)) { _parallelCommons.Remove(ce.id); continue; }

                if (!_parallelCommons.TryGetValue(ce.id, out var interp))
                {
                    interp = new RMZInterpreter();
                    _parallelCommons[ce.id] = interp;
                }
                // 用負的公共事件 id 當 EventId，跟地圖事件（一定是正數）與其他並行公共事件
                // 分開，這樣「某張圖片按鈕的點擊只能被第一個問到的人吃到」才分得清
                // 是不是同一個公共事件在問。
                if (!interp.IsRunning) interp.Setup(ce.list, -ce.id, _mapId);

                interp.Update(frames);
            }
        }

        float _blackSince = -1f;
        bool _blackReported;

        /// <summary>
        /// 畫面全黑又沒有任何事件在跑，多半是某頁把色調染黑之後就沒人染回來。
        /// 這種狀況玩家只看得到一片黑，Console 至少要說一聲，不然無從查起。
        /// </summary>
        void WatchBlackScreen()
        {
            var screen = RMZScreen.Instance;
            bool black = screen != null &&
                         (screen.Tone.X <= -200f || screen.FadeOpacity >= 200f);
            bool idle = !_main.IsRunning && !_eventsPaused && !RMZSaveScene.IsOpen;

            if (!black || !idle)
            {
                _blackSince = -1f;
                _blackReported = false;
                return;
            }

            if (_blackSince < 0f) { _blackSince = Now; return; }
            if (_blackReported || Now - _blackSince < 3f) return;

            _blackReported = true;
            GD.PushWarning(
                $"[RMZ] 畫面全黑但沒有事件在跑（色調 {screen.Tone}、淡出 {screen.FadeOpacity:0}）。" +
                $"地圖 {_mapId}，多半是某一頁染黑之後沒有把色調復原。");
        }

        /// <summary>
        /// 排好要跑的公共事件（MZ 的 $gameTemp.reserveCommonEvent）。
        /// 讀檔後用來重開地圖 UI（公共事件 89「玩家數值打開」）—— 存檔沒有存圖片，讀回來 UI 會不見。
        /// </summary>
        public int ReservedCommonEvent;

        bool TrySetupAutorun()
        {
            if (ReservedCommonEvent > 0)
            {
                var reserved = RMZDatabase.CommonEvent(ReservedCommonEvent);
                ReservedCommonEvent = 0;
                if (reserved?.list != null && reserved.list.Count > 1)
                {
                    _main.Setup(reserved.list, 0, _mapId);
                    return true;
                }
            }

            // 地圖上的自動執行事件
            _snapshot.Clear();
            _snapshot.AddRange(_events);

            foreach (var e in _snapshot)
            {
                if (e == null || e.Page == null || e.Trigger != 3) continue;
                if (_erased.Contains(e.Event.id)) continue;
                if (e.Page.list == null || e.Page.list.Count <= 1) continue;

                _main.Setup(e.Page.list, e.Event.id, _mapId);
                return true;
            }

            // 自動執行的公共事件
            if (RMZDatabase.CommonEvents != null)
            {
                foreach (var ce in RMZDatabase.CommonEvents)
                {
                    if (ce == null || ce.trigger != 1 || ce.list == null || ce.list.Count <= 1) continue;
                    if (!RMZGameState.Switch(ce.switchId)) continue;

                    _main.Setup(ce.list, 0, _mapId);
                    return true;
                }
            }

            return false;
        }

        bool TrySetupPlayerTrigger()
        {
            var player = RMZPlayer.Instance;
            if (player == null || player.IsMoving) return false;
            if (!ActionPressed()) return false;

            var map = RMZGameMap.Instance;
            if (map == null) return false;

            // 先看腳下，再看面前那一步（半格移動時是半格）
            float unit = player.StepUnit;
            var hit = FindTriggerEvent(player.X, player.Y)
                   ?? FindTriggerEvent(map.RoundXWithDirection(player.X, player.Direction, unit),
                                       map.RoundYWithDirection(player.Y, player.Direction, unit))
                   ?? FindTriggerEvent(map.RoundXWithDirection(player.X, player.Direction, 1f),
                                       map.RoundYWithDirection(player.Y, player.Direction, 1f))
                   ?? FindTriggerAcrossCounter(player, map)
                   ?? FindTriggerNearFront(player);

            if (hit == null) return false;

            hit.TurnTowardPlayer();
            _main.Setup(hit.Page.list, hit.Event.id, _mapId);
            return true;
        }

        /// <summary>
        /// 碰觸觸發（MZ 的 trigger 1「玩家碰觸」／2「事件碰觸」）。
        /// 門就是靠這個開的：以前玩家可以直接走進門裡才誤打誤撞會觸發，
        /// 門變成實心之後就必須照 MZ 的做法，在「移動被擋下來」的當下檢查前方。
        /// </summary>
        public bool TryTriggerTouch(float x, float y, bool normalPriority = true)
        {
            if (_main.IsRunning || _eventsPaused) return false;
            if (RMZMessage.Instance.IsBusy) return false;
            if (RMZBootstrap.Instance != null && RMZBootstrap.Instance.IsTransferring) return false;

            var e = FindTouchEvent(x, y, normalPriority);
            if (e == null) return false;

            RMZDebug.Log(() => $"碰觸觸發事件 {e.Event.id}「{e.Event.name}」");
            _main.Setup(e.Page.list, e.Event.id, _mapId);
            return true;
        }

        RMZEventCharacter FindTouchEvent(float x, float y, bool normalPriority)
        {
            foreach (var e in _events)
            {
                if (e == null || e.Page == null) continue;
                if (e.Trigger != 1 && e.Trigger != 2) continue;
                if (_erased.Contains(e.Event.id)) continue;
                if (e.Page.list == null || e.Page.list.Count <= 1) continue;
                // 碰到前方人物／門：同層；踩到腳下換場點：非同層。
                if (e.IsNormalPriority() != normalPriority) continue;
                if (HalfMoveSettings.Overlaps(e.X, e.Y, x, y)) return e;
            }
            return null;
        }

        /// <summary>某一格上有沒有「確定鍵觸發」的事件。滑鼠點擊用。</summary>
        public RMZEventCharacter ActionEventAt(float x, float y) => FindTriggerEvent(x, y);

        /// <summary>
        /// 滑鼠點出來的觸發。跟按確定鍵走的是同一條路
        /// （TrySetupPlayerTrigger），只是對象是玩家點的那個事件。
        /// </summary>
        public bool TriggerByClick(RMZEventCharacter e)
        {
            if (e == null || e.Page == null || e.Event == null) return false;
            if (e.Page.list == null || e.Page.list.Count <= 1) return false;
            if (e.Trigger != 0) return false;
            if (_erased.Contains(e.Event.id)) return false;

            if (_main.IsRunning || _eventsPaused) return false;
            if (RMZMessage.Instance.IsBusy) return false;
            if (RMZGuildPanels.IsOpen || RMZSaveScene.IsOpen || RMZDevPanel.IsOpen || RMZOptionsScene.IsOpen ||
                RMZEnhanceWindow.IsOpen || RMZTavernWindow.IsOpen ||
                RMZSlaveWorkWindow.IsOpen) return false;
            if (RMZBootstrap.Instance != null && RMZBootstrap.Instance.IsTransferring) return false;

            RMZDebug.Log(() => $"滑鼠點擊觸發事件 {e.Event.id}「{e.Event.name}」");
            e.TurnTowardPlayer();
            _main.Setup(e.Page.list, e.Event.id, _mapId);
            return true;
        }

        /// <summary>
        /// 隔著櫃檯／桌子說話（MZ 的「櫃檯」屬性，再放寬到任何走不進去的格子）：
        /// 面前是櫃檯或桌子的話，沿著面向再往前找最多 3 格。
        /// </summary>
        RMZEventCharacter FindTriggerAcrossCounter(RMZPlayer player, RMZGameMap map)
        {
            int d = player.Direction;
            float dx = d == 4 ? -1f : d == 6 ? 1f : 0f, dy = d == 8 ? -1f : d == 2 ? 1f : 0f;
            if (dx == 0f && dy == 0f) return null;
            for (int step = 1; step <= 3; step++)
            {
                float cx = player.X + dx * step, cy = player.Y + dy * step;
                if (step > 1)
                {
                    var e = FindTriggerEvent(cx, cy);
                    if (e != null) return e;
                }
                // 路上只能是櫃檯／桌子這種走不進去的格子，空地就該自己走過去
                int tx = Mathf.RoundToInt(cx), ty = Mathf.RoundToInt(cy);
                if (!map.IsValid(tx, ty)) break;
                if (!map.IsBlockedTile(tx, ty) && !map.IsCounter(tx, ty)) break;
            }
            return null;
        }

        /// <summary>
        /// 半格移動的補救：人站在半格位置時，面前那一步常常剛好錯開 NPC。
        /// 改成「面前 1.5 格內、左右偏半格以內」的可對話事件都算，取最近的那個。
        /// 讓所有 NPC 的對話判定都等於佔滿一整格。
        /// </summary>
        RMZEventCharacter FindTriggerNearFront(RMZPlayer player)
        {
            int d = player.Direction;
            float fx = d == 4 ? -1f : d == 6 ? 1f : 0f, fy = d == 8 ? -1f : d == 2 ? 1f : 0f;
            if (fx == 0f && fy == 0f) return null;

            RMZEventCharacter best = null;
            float bestDist = float.MaxValue;
            foreach (var e in _events)
            {
                if (e == null || e.Page == null || e.Trigger != 0) continue;
                if (_erased.Contains(e.Event.id)) continue;
                if (e.Page.list == null || e.Page.list.Count <= 1) continue;
                float rx = e.X - player.X, ry = e.Y - player.Y;
                float forward = rx * fx + ry * fy;          // 往面向前進多少
                float side = Mathf.Abs(rx * fy - ry * fx);   // 左右偏多少
                if (forward <= 0.01f || forward > 1.5f || side > 0.75f) continue;
                float dist = forward + side * 0.5f;
                if (dist < bestDist) { bestDist = dist; best = e; }
            }
            return best;
        }

        RMZEventCharacter FindTriggerEvent(float x, float y)
        {
            foreach (var e in _events)
            {
                if (e == null || e.Page == null || e.Trigger != 0) continue;
                if (_erased.Contains(e.Event.id)) continue;
                if (e.Page.list == null || e.Page.list.Count <= 1) continue;
                if (HalfMoveSettings.Overlaps(e.X, e.Y, x, y)) return e;
            }
            return null;
        }

        static float Now => Time.GetTicksMsec() / 1000f;

        // Unity 的 InputSystem 有 wasPressedThisFrame，Godot 的 Input 只對「動作」提供
        // IsActionJustPressed，所以啟動時把 RM 的確定／取消鍵註冊成兩個動作。
        public const string OkAction = "rmz_ok";
        public const string CancelAction = "rmz_cancel";

        public static void RegisterInputActions()
        {
            AddAction(OkAction, Key.Z, Key.Space, Key.Enter, Key.KpEnter);
            AddAction(CancelAction, Key.X, Key.Escape);
        }

        static void AddAction(string name, params Key[] keys)
        {
            if (InputMap.HasAction(name)) return;
            InputMap.AddAction(name);
            foreach (var k in keys)
                InputMap.ActionAddEvent(name, new InputEventKey { PhysicalKeycode = k });
        }

        public static bool ActionPressed() => Input.IsActionJustPressed(OkAction);
        public static bool CancelPressed() => Input.IsActionJustPressed(CancelAction);
    }
}
