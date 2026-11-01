// 玩家角色，對應 MZ 的 Game_Player。
// 鍵盤與滑鼠都能操作：滑鼠點地圖會尋路過去，跟 MZ 的觸控移動一樣。
//
// 移植備註：Unity 版用 InputSystem 的 Keyboard.current / Mouse.current，
// 這裡改用 Godot 的 Input 單例，判斷邏輯一模一樣。
using Godot;

namespace RMZ
{
    public partial class RMZPlayer : RMZCharacterBase
    {
        public static RMZPlayer Instance { get; private set; }

        public bool DashEnabled = true;

        /// <summary>滑鼠點地圖就自動尋路過去</summary>
        public bool MouseMoveEnabled = true;

        /// <summary>滑鼠點 NPC（或 NPC 前面的櫃檯）就跟他說話</summary>
        public bool MouseTalkEnabled = true;

        /// <summary>隔著櫃檯最多幾格外還講得到話。公會的櫃檯有兩格厚，所以預設 3</summary>
        public int CounterReach = 3;

        /// <summary>Input_Lock.js 的鎖定開關。打開時玩家不能自己移動，但事件仍可控制主角</summary>
        public int InputLockSwitchId = 99;

        bool _dashing;

        // 滑鼠指定的目的地（對應 MZ 的 $gameTemp destination）
        int _destinationX, _destinationY;
        bool _hasDestination;

        // 滑鼠點到人之後，走過去要講話的對象
        int _talkEventId = -1;
        int _talkX, _talkY;
        float _talkUntil;

        static float Now => Time.GetTicksMsec() / 1000f;

        public override void _Ready()
        {
            base._Ready();
            Instance = this;
            PriorityType = 1;
        }

        public override bool IsDashing() => _dashing;

        // 只有玩家自己按方向鍵走路時，才允許卡角自動改斜向
        protected override bool AllowCornerAvoidance => !RouteForcing;

        /// <summary>對應 Input_Lock.js：指定開關打開時，玩家無法自行移動。</summary>
        public bool IsInputLocked =>
            InputLockSwitchId > 0
            && RMZGameState.Switch(InputLockSwitchId)
            && !(RMZBootstrap.Instance != null && RMZBootstrap.Instance.IgnoreInputLock);

        /// <summary>不能動的話，這裡會寫著原因。給除錯用的。</summary>
        public string MoveBlockReason { get; private set; } = "";

        public bool CanMove()
        {
            if(RMZTutorialOverlay.IsOpen){MoveBlockReason="教學提示中";return false;}
            if(RMZGuildPanels.IsOpen) { MoveBlockReason = "背包／人物狀態開啟中"; return false; }
            if(RMZHeroineGearPanel.IsOpen) { MoveBlockReason = "女主換裝視窗開啟中"; return false; }
            if(RMZAchievementBrowser.IsOpen) { MoveBlockReason = "成就頁開啟中"; return false; }
            // [移植待辦] Unity 版這裡還會檢查這些畫面有沒有開著：
            //   RMZSystemMenu / RMZSaveScene / RMZTemptWindow / RMZUnionWindow /
            //   RMZSlaveShopWindow / RMZSlaveWorkWindow / RMZDevPanel /
            //   RMZOptionsScene / RMZEnhanceWindow / RMZTavernWindow
            // 那些 UI 還沒移植，等各自那一階段做完再把對應的判斷加回來。

            if (RouteForcing) { MoveBlockReason = "事件正在強制移動主角（移動路線）"; return false; }
            if (IsInputLocked) { MoveBlockReason = $"{InputLockSwitchId} 號開關是開的（Input_Lock）"; return false; }

            var runner = RMZEventRunner.Instance;
            if (runner != null && runner.EventsPaused)
            {
                MoveBlockReason = "UI 開啟中（Hendrix pauseEvents）";
                return false;
            }

            if (runner != null && runner.IsBusy)
            {
                MoveBlockReason = "事件執行中";
                return false;
            }

            if (RMZBootstrap.Instance != null && RMZBootstrap.Instance.IsTransferring)
            {
                MoveBlockReason = "場所移動進行中";
                return false;
            }

            MoveBlockReason = "";
            return true;
        }

        /// <summary>
        /// 把主角身上「事件留下來的殘留狀態」清乾淨。
        /// 事件如果在「開了穿透、加了速」之後被打斷，主角會維持穿牆 + 跑很快，
        /// 而且沒有任何東西會自動把它改回來。這裡就是那個「改回來」的按鈕。
        /// </summary>
        public void ClearEventResidue()
        {
            Through = false;
            MoveSpeed = DefaultMoveSpeed;
            DirectionFix = false;
            WalkAnime = true;
            Opacity = 1f;
            BlendMode = 0;
            ClearDestination();
            CancelForcedRoute();
        }

        /// <summary>主角的標準移動速度。移動路線改過之後靠這個復原。</summary>
        public const int DefaultMoveSpeed = 4;

        public void SetDestination(int x, int y)
        {
            _destinationX = x;
            _destinationY = y;
            _hasDestination = true;
        }

        public void ClearDestination() => _hasDestination = false;

        public override void _Process(double delta)
        {
            UpdateDashing();
            UpdateMouseInput();
            UpdatePendingTalk();
            if (IsStopping) MoveByInput();
            base._Process(delta);
        }

        /// <summary>
        /// 對應 MZ 的 Game_Player.updateDashing，規則照抄：
        ///   1. 一步還沒走完就不重算（不然半途變速會抖）
        ///   2. 不能自己動的時候一律不跑
        ///   3. 「始終跑步」跟 Shift 互斥
        ///   4. 滑鼠點地圖尋路時也算跑步
        /// </summary>
        void UpdateDashing()
        {
            if (!IsStopping) return;

            if (!DashEnabled || !CanMove() || (RMZGameMap.Instance?.Data?.disableDashing ?? false))
            {
                _dashing = false;
                return;
            }

            bool shift = Input.IsKeyPressed(Key.Shift);
            bool button = RMZOptions.AlwaysDash ? !shift : shift;

            _dashing = button || _hasDestination;
        }

        void UpdateMouseInput()
        {
            if (!MouseMoveEnabled && !MouseTalkEnabled) return;
            if (!_mouseClickedThisFrame) return;
            _mouseClickedThisFrame = false;

            // 點在畫面上的介面圖片（選單／道具／裝備那排按鈕、左上角狀態欄…）不算點地圖。
            // 那些是 RM 的圖片，不是 Godot 的介面元件，點擊會穿過去被當成「走到這裡」。
            if (ClickIsOnUiPicture()) return;

            if (!CanMove()) return;

            var map = RMZGameMap.Instance;
            if (map == null) return;

            // Godot：滑鼠的世界座標直接問節點，不用像 Unity 那樣自己做 ScreenToWorldPoint
            Vector2 world = GetGlobalMousePosition();
            int tileX = Mathf.FloorToInt(world.X / RMZConfig.TileWidth);
            int tileY = Mathf.FloorToInt(world.Y / RMZConfig.TileHeight);

            if (!map.IsValid(tileX, tileY)) return;

            _talkEventId = -1;

            if (MouseTalkEnabled)
            {
                var target = FindClickTarget(tileX, tileY);
                if (target != null)
                {
                    // 站的位置已經按得到就直接開講，不用先走過去
                    int dir = TalkDirectionTo(target.TileX, target.TileY);
                    if (dir > 0) { StartTalk(target, dir); return; }

                    // 按不到就先走過去，到得了再講（八秒內沒到就算了）
                    _talkEventId = target.Event.id;
                    _talkX = target.TileX;
                    _talkY = target.TileY;
                    _talkUntil = Now + 8f;
                }
            }

            if (MouseMoveEnabled) SetDestination(tileX, tileY);
        }

        bool _mouseClickedThisFrame;

        static bool ClickIsOnUiPicture()
        {
            int id = RMZPictureInput.Hovered;
            if (id < 0) return false;
            if (RMZPictureInput.IsInteractive(id)) return true;   // 事件有在問它有沒有被點 = 按鈕
            var pic = RMZScreen.Instance?.Picture(id);
            return pic != null && !string.IsNullOrEmpty(pic.Name) &&
                   pic.Name.StartsWith("UI button", System.StringComparison.OrdinalIgnoreCase);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                _mouseClickedThisFrame = true;
        }

        /// <summary>
        /// 玩家點的那一格代表誰。
        ///   1. 直接點在人（或櫃檯上那個隱形的觸發事件）身上
        ///   2. 點在走不上去的格子上 —— 公會的櫃檯有兩格厚，往遠離玩家的方向再找幾格
        /// </summary>
        RMZEventCharacter FindClickTarget(int tx, int ty)
        {
            var runner = RMZEventRunner.Instance;
            var map = RMZGameMap.Instance;
            if (runner == null || map == null) return null;

            var hit = runner.ActionEventAt(tx, ty);
            if (hit != null) return hit;

            // 空地不做延伸，不然點 NPC 旁邊的地板會誤觸
            if (!map.IsBlockedTile(tx, ty) && !map.IsCounter(tx, ty)) return null;

            int dx = tx - TileX, dy = ty - TileY;
            // 只往主要的那一軸延伸，斜著點不做猜測
            if (Mathf.Abs(dx) >= Mathf.Abs(dy)) dy = 0; else dx = 0;
            dx = System.Math.Sign(dx);
            dy = System.Math.Sign(dy);
            if (dx == 0 && dy == 0) return null;

            for (int step = 1; step <= CounterReach; step++)
            {
                int cx = tx + dx * step, cy = ty + dy * step;
                if (!map.IsValid(cx, cy)) break;

                var e = runner.ActionEventAt(cx, cy);
                if (e != null) return e;

                // 穿過去的路上只能是櫃檯／牆這種走不進去的東西
                if (!map.IsBlockedTile(cx, cy) && !map.IsCounter(cx, cy)) break;
            }
            return null;
        }

        /// <summary>
        /// 從現在站的位置能不能「按確定鍵」按到 (tx, ty)。回傳要面向的方向，0 = 按不到。
        /// 中間隔著櫃檯（或任何走不進去的格子）也算按得到 —— 公會櫃檯就是這種。
        /// </summary>
        int TalkDirectionTo(int tx, int ty)
        {
            var map = RMZGameMap.Instance;
            if (map == null) return 0;

            int px = TileX, py = TileY;
            if (px == tx && py == ty) return Direction;    // 站在事件上（櫃檯前的觸發格）

            int[] dirs = { Down, Left, Right, Up };
            foreach (int d in dirs)
            {
                int dx = d == Left ? -1 : d == Right ? 1 : 0;
                int dy = d == Up ? -1 : d == Down ? 1 : 0;

                for (int step = 1; step <= Mathf.Max(1, CounterReach); step++)
                {
                    int cx = px + dx * step, cy = py + dy * step;
                    if (!map.IsValid(cx, cy)) break;
                    if (cx == tx && cy == ty) return d;

                    // 中間是走得過去的空地，就該自己走過去而不是隔空喊話
                    if (!map.IsBlockedTile(cx, cy) && !map.IsCounter(cx, cy)) break;
                }
            }
            return 0;
        }

        void StartTalk(RMZEventCharacter target, int direction)
        {
            _talkEventId = -1;
            ClearDestination();
            SetDirection(direction);
            RMZEventRunner.Instance?.TriggerByClick(target);
        }

        /// <summary>點了遠處的人之後，走到講得到話的位置就自動開口。</summary>
        void UpdatePendingTalk()
        {
            if (_talkEventId < 0) return;

            var runner = RMZEventRunner.Instance;
            if (runner == null) { _talkEventId = -1; return; }

            // 中途被別的事件打斷、或走太久都放棄
            if (runner.IsBusy || Now > _talkUntil) { _talkEventId = -1; return; }
            if (!IsStopping) return;

            int dir = TalkDirectionTo(_talkX, _talkY);
            if (dir <= 0)
            {
                // 已經停下來又沒有目的地，代表走不過去了
                if (!_hasDestination) _talkEventId = -1;
                return;
            }

            var target = runner.EventById(_talkEventId);
            if (target == null) { _talkEventId = -1; return; }
            StartTalk(target, dir);
        }

        void MoveByInput()
        {
            if (!CanMove())
            {
                ClearDestination();
                return;
            }

            int d = InputDirection();

            if (d > 0)
            {
                ClearDestination();          // 一按方向鍵就放棄滑鼠的目的地
                _talkEventId = -1;           // 連帶放棄「走過去找他講話」
            }
            else if (_hasDestination)
            {
                if (TileX == _destinationX && TileY == _destinationY)
                {
                    ClearDestination();
                    return;
                }
                d = FindDirectionTo(_destinationX, _destinationY);
                if (d == 0) { ClearDestination(); return; }
            }

            if (d > 0) MoveStraight(d);

            // 尋路走不動就別再原地卡著
            if (_hasDestination && !MovementSuccess) ClearDestination();
        }

        /// <summary>
        /// HalfMove 的 Direction8Move 有開，所以要支援斜向。
        /// 回傳的是 MZ 的數字鍵盤方向碼：1~9。
        /// </summary>
        static int InputDirection()
        {
            bool down = Input.IsKeyPressed(Key.Down) || Input.IsKeyPressed(Key.S);
            bool left = Input.IsKeyPressed(Key.Left) || Input.IsKeyPressed(Key.A);
            bool right = Input.IsKeyPressed(Key.Right) || Input.IsKeyPressed(Key.D);
            bool up = Input.IsKeyPressed(Key.Up) || Input.IsKeyPressed(Key.W);

            // 同時按到相反方向就當作沒按
            if (down && up) { down = up = false; }
            if (left && right) { left = right = false; }

            if (HalfMoveSettings.Direction8)
            {
                if (down && left) return LowerLeft;
                if (down && right) return LowerRight;
                if (up && left) return UpperLeft;
                if (up && right) return UpperRight;
            }

            if (down) return Down;
            if (left) return Left;
            if (right) return Right;
            if (up) return Up;
            return 0;
        }

        /// <summary>撞到東西時，照 MZ 的 checkEventTriggerTouchFront 檢查面前有沒有碰觸型事件。</summary>
        protected override void OnMoveBlocked(int d)
        {
            base.OnMoveBlocked(d);

            var map = RMZGameMap.Instance;
            var runner = RMZEventRunner.Instance;
            if (map == null || runner == null) return;

            // 先看半格前，再看整格前
            float unit = StepUnit;
            if (runner.TryTriggerTouch(map.RoundXWithDirection(X, d, unit),
                                       map.RoundYWithDirection(Y, d, unit))) return;

            runner.TryTriggerTouch(map.RoundXWithDirection(X, d, 1f),
                                   map.RoundYWithDirection(Y, d, 1f));
        }

        protected override void IncreaseSteps()
        {
            base.IncreaseSteps();

            // 走上去就觸發的那種（地板開關、換場點）
            RMZEventRunner.Instance?.TryTriggerTouch(X, Y, normalPriority: false);
            // TODO: 之後接步數計算、地圖傷害、遇敵計數
        }
    }
}
