// 移動路線的執行，對應 MZ 的 Game_Character.processMoveCommand。
// 事件指令 205（設定移動路線）跟事件頁的「自訂移動路線」都走這裡。
using Godot;

namespace RMZ
{
    public partial class RMZCharacterBase
    {
        // MZ 的移動路線指令碼
        const int RouteEnd = 0, RouteMoveDown = 1, RouteMoveLeft = 2, RouteMoveRight = 3, RouteMoveUp = 4,
                  RouteMoveLowerL = 5, RouteMoveLowerR = 6, RouteMoveUpperL = 7, RouteMoveUpperR = 8,
                  RouteMoveRandom = 9, RouteMoveToward = 10, RouteMoveAway = 11,
                  RouteMoveForward = 12, RouteMoveBackward = 13, RouteJump = 14, RouteWait = 15,
                  RouteTurnDown = 16, RouteTurnLeft = 17, RouteTurnRight = 18, RouteTurnUp = 19,
                  RouteTurn90dR = 20, RouteTurn90dL = 21, RouteTurn180d = 22, RouteTurn90dRL = 23,
                  RouteTurnRandom = 24, RouteTurnToward = 25, RouteTurnAway = 26,
                  RouteSwitchOn = 27, RouteSwitchOff = 28, RouteChangeSpeed = 29, RouteChangeFreq = 30,
                  RouteWalkAnimeOn = 31, RouteWalkAnimeOff = 32, RouteStepAnimeOn = 33, RouteStepAnimeOff = 34,
                  RouteDirFixOn = 35, RouteDirFixOff = 36, RouteThroughOn = 37, RouteThroughOff = 38,
                  RouteTransparentOn = 39, RouteTransparentOff = 40,
                  RouteChangeImage = 41, RouteChangeOpacity = 42, RouteChangeBlendMode = 43,
                  RoutePlaySe = 44, RouteScript = 45;

        public MoveRoute Route { get; private set; }
        public bool RouteForcing { get; private set; }

        int _routeIndex;
        float _routeWait;
        static int _blockedWarnings;
        MoveRoute _originalRoute;
        int _originalRouteIndex;

        // 不可跳過（skippable=false）的路線被永久擋住時，用來避免玩家卡死的逃生閥。
        // 讀檔／場所移動之後，劇情演出用的強制路線如果剛好卡在一個現在走不通的
        // 格子（例如存檔當下的座標跟劇本假設的起點對不上），RM 本身的行為就是
        // 整段停在那裡不動 —— 這裡多加一個逾時，逾時後照 skippable 的方式跳過
        // 那一步，讓玩家至少拿得回操控權，不會被鎖死在讀檔畫面後面。
        float _routeStallTime;
        const float RouteStallTimeout = 4f;

        // 門穿越專用的短逃生閥：強制路線推著玩家「前進」卡住，側身也救不了時，
        // 不用真的等滿 4 秒才放行——那 4 秒本來是留給「讀檔/場所移動後，演出
        // 路線假設的起點跟存檔座標對不上」這種情況用的，門穿越卡住多半只是
        // 門的 Through 生效那一幀跟玩家的前進判定差一點點沒對上，等一小段確認
        // 不是瞬間的判定誤差之後，直接把這步走過去就好，才不會每次過門都要
        // 卡足 4 秒。
        const float DoorForceThroughGrace = 0.35f;

        // 這一步執行的是哪個移動指令，以及這條路線有沒有已經幫忙轉過一次方向。
        // 只用在下面的「門卡住」自救，平常不影響任何行為。
        int _lastRouteCode = -1;
        bool _unstickTried;

        public bool IsRouteFinished => Route == null || (!Route.repeat && _routeIndex >= Route.list.Count - 1);

        public void SetMoveRoute(MoveRoute route)
        {
            Route = route;
            _routeIndex = 0;
            _routeWait = 0f;
            _routeStallTime = 0f;
            _unstickTried = false;
            RouteForcing = false;
        }

        public void ForceMoveRoute(MoveRoute route)
        {
            // 原始路線可以是 null；以強制狀態判斷是否首次接管，
            // 避免連續替換劇情路線時，把上一段強制路線當成自主路線。
            if (!RouteForcing)
            {
                _originalRoute = Route;
                _originalRouteIndex = _routeIndex;
            }
            Route = route;
            _routeIndex = 0;
            _routeWait = 0f;
            _routeStallTime = 0f;
            _unstickTried = false;
            RouteForcing = true;
        }

        /// <summary>
        /// 立刻中止強制移動路線，回到原本那條。
        /// 事件被打斷、留下一條跑不完的路線時用得上。
        /// </summary>
        public void CancelForcedRoute()
        {
            if (RouteForcing) RestoreMoveRoute();
        }

        void RestoreMoveRoute()
        {
            Route = _originalRoute;
            _routeIndex = _originalRouteIndex;
            _originalRoute = null;
            RouteForcing = false;
        }

        float _lastFrames;

        protected void UpdateRoutineMove(float frames)
        {
            _lastFrames = frames;

            if (_routeWait > 0f) { _routeWait -= frames; return; }
            if (Route?.list == null || Route.list.Count == 0) return;

            _movementSuccess = true;
            var command = Route.list[Mathf.Clamp(_routeIndex, 0, Route.list.Count - 1)];
            _lastRouteCode = command.code;
            ProcessMoveCommand(command);
            AdvanceRouteIndex();
        }

        /// <summary>
        /// 「前進」被擋住時的自救：只處理主角、只處理面向上／下、而且只在真的
        /// 撞上去之後才轉。轉成左右之後這一幀不推進索引，下一幀會用新方向重試同一步。
        /// 一條路線只會幫忙轉一次，轉了還是走不過去就交給上面的逾時逃生閥。
        /// </summary>
        bool TryUnstickForwardStep()
        {
            if (_unstickTried) return false;
            if (_lastRouteCode != RouteMoveForward) return false;
            if (!ReferenceEquals(RMZPlayer.Instance, this)) return false;
            if (Direction != Up && Direction != Down) return false;

            int side = CanPass(X, Y, Left) ? Left
                     : CanPass(X, Y, Right) ? Right
                     : 0;
            if (side == 0) return false;

            int was = Direction;
            _unstickTried = true;
            SetDirection(side);
            GD.PushWarning($"[RMZ] {Name} 的「前進」被擋住（原本面向 {was} 走不通），" +
                             $"改成面向 {side} 再試一次 —— 門的開門演出多半只支援左右。");
            return true;
        }

        void AdvanceRouteIndex()
        {
            if (!_movementSuccess)
            {
                // 走不過去。skippable 的話這一步會被跳掉，站位就會跟 RM 對不上，所以記一筆。
                RMZDebug.Log(() => $"{Name} 移動被擋住於 ({X},{Y}) 方向{Direction}" +
                                   (Route.skippable ? "（skippable，這步跳過）" : "（原地重試）"));

                if (!Route.skippable)
                {
                    _routeStallTime += _lastFrames / 60f;

                    // 不可跳過的路線被擋住 = 整段演出會永遠停在這裡，一定要讓人看到
                    if (_blockedWarnings < 5)
                    {
                        _blockedWarnings++;
                        GD.PushWarning($"[RMZ] {Name} 的移動路線卡住了：位置 ({X},{Y}) 方向 {Direction}，" +
                                         $"這段不可跳過，事件會停在這裡不動。原因：{DescribeBlocker(Direction)}");
                    }

                    // 門的專屬自救：MZ 的門模板是「轉向門 → 開門 → 前進穿過去」，
                    // 但門圖多半只畫了左右開門幀、門後也只有左右能走。玩家面向上／下
                    // 碰到門時，那步不可跳過的「前進」會撞上門後的牆，於是永遠重試。
                    // 這裡只在「真的已經被擋住」的當下轉向，一般走路與對話都不會經過這裡。
                    if (TryUnstickForwardStep()) return;

                    // 門穿越：側身救不了、卡住也才一下下，直接把這步走過去。
                    // 只用在「強制路線 + 玩家 + 前進」——一般走路操控不會走到這裡，
                    // 這種前進本來就是門的腳本自己安排好、認定一定走得過去的那一步。
                    if (_lastRouteCode == RouteMoveForward && RouteForcing &&
                        ReferenceEquals(RMZPlayer.Instance, this) &&
                        _routeStallTime >= DoorForceThroughGrace)
                    {
                        ForceStepForward();
                        _routeStallTime = 0f;
                        _unstickTried = false;
                    }
                    else if (_routeStallTime < RouteStallTimeout) return;
                    else
                    {
                        // 逾時逃生閥：卡了超過 RouteStallTimeout 秒還是走不過去，
                        // 多半是存檔當下的座標跟這段劇本假設的起點對不上（讀檔／場所
                        // 移動之後常見）。與其讓玩家永遠卡死、連系統選單都叫不出來，
                        // 這裡改成照 skippable 的方式跳過這一步，讓演出（跟操控權）能繼續。
                        GD.PushWarning($"[RMZ] {Name} 的移動路線卡住超過 {RouteStallTimeout:0} 秒，" +
                                         "強制跳過這一步以避免卡死。");
                        _routeStallTime = 0f;
                    }
                }
                else
                {
                    _routeStallTime = 0f;
                }
            }
            else
            {
                _routeStallTime = 0f;
                _unstickTried = false;
            }

            if (_routeIndex < Route.list.Count - 1) { _routeIndex++; return; }

            if (Route.repeat) _routeIndex = 0;
            else if (RouteForcing) RestoreMoveRoute();
        }

        /// <summary>被擋住時查一下到底是圖塊還是角色，省得每次都要翻資料對。</summary>
        string DescribeBlocker(int d)
        {
            var map = RMZGameMap.Instance;
            if (map == null) return "地圖尚未載入";

            float unit = StepUnit;
            float x2 = map.RoundXf(map.XWithDirection(X, d, unit));
            float y2 = map.RoundYf(map.YWithDirection(Y, d, unit));

            if (!map.IsValid(x2, y2)) return "目標超出地圖範圍";
            if (!IsMapPassable(X, Y, d)) return $"圖塊不可通行（目標 {x2},{y2}）";

            foreach (var c in map.Characters)
            {
                if (c == null || c == this || c.Through || !c.IsNormalPriority()) continue;
                if (!RMZSolidObjectSettings.Blocks(c, x2, y2)) continue;
                return $"被 {c.Name} 擋住（它在 {c.X},{c.Y}{(c.SolidObject ? "，實心物件" : "")}）";
            }

            return "不明";
        }

        void ProcessMoveCommand(MoveCommand command)
        {
            var p = command.parameters;

            switch (command.code)
            {
                case RouteEnd: break;
                case RouteMoveDown: MoveStraight(Down); break;
                case RouteMoveLeft: MoveStraight(Left); break;
                case RouteMoveRight: MoveStraight(Right); break;
                case RouteMoveUp: MoveStraight(Up); break;
                case RouteMoveLowerL: MoveDiagonally(Left, Down); break;
                case RouteMoveLowerR: MoveDiagonally(Right, Down); break;
                case RouteMoveUpperL: MoveDiagonally(Left, Up); break;
                case RouteMoveUpperR: MoveDiagonally(Right, Up); break;

                case RouteMoveRandom: MoveRandom(); break;
                case RouteMoveToward: MoveTowardPlayer(); break;
                case RouteMoveAway: MoveAwayFromPlayer(); break;
                case RouteMoveForward: MoveForward(); break;
                case RouteMoveBackward: MoveBackward(); break;

                case RouteJump:
                    Jump(Num(p, 0), Num(p, 1));
                    break;

                case RouteWait: _routeWait = Num(p, 0); break;

                case RouteTurnDown: SetDirection(Down); break;
                case RouteTurnLeft: SetDirection(Left); break;
                case RouteTurnRight: SetDirection(Right); break;
                case RouteTurnUp: SetDirection(Up); break;
                case RouteTurn90dR: TurnRight90(); break;
                case RouteTurn90dL: TurnLeft90(); break;
                case RouteTurn180d: SetDirection(ReverseDir(Direction)); break;
                case RouteTurn90dRL: if (GD.Randf() < 0.5f) TurnRight90(); else TurnLeft90(); break;
                case RouteTurnRandom: SetDirection(2 * (GD.RandRange(0, 3) + 1)); break;
                case RouteTurnToward: TurnTowardPlayer(); break;
                case RouteTurnAway: TurnAwayFromPlayer(); break;

                case RouteSwitchOn: RMZGameState.SetSwitch(Num(p, 0), true); break;
                case RouteSwitchOff: RMZGameState.SetSwitch(Num(p, 0), false); break;

                case RouteChangeSpeed: MoveSpeed = Num(p, 0); break;
                case RouteChangeFreq: MoveFrequency = Num(p, 0); break;
                case RouteWalkAnimeOn: WalkAnime = true; break;
                case RouteWalkAnimeOff: WalkAnime = false; break;
                case RouteStepAnimeOn: StepAnime = true; break;
                case RouteStepAnimeOff: StepAnime = false; break;
                case RouteDirFixOn: DirectionFix = true; break;
                case RouteDirFixOff: DirectionFix = false; break;
                case RouteThroughOn: Through = true; break;
                case RouteThroughOff: Through = false; break;
                case RouteTransparentOn: Transparent = true; break;
                case RouteTransparentOff: Transparent = false; break;

                case RouteChangeImage:
                    CharacterName = Str(p, 0);
                    CharacterIndex = Num(p, 1);
                    break;

                case RouteChangeOpacity: Opacity = Mathf.Clamp(Num(p, 0) / 255f, 0f, 1f); break;
                case RouteChangeBlendMode: BlendMode = Num(p, 0); break;

                case RoutePlaySe:
                    RMZAudio.Instance?.PlaySe(ToAudioFile(p, 0));
                    break;

                case RouteScript:
                {
                    string script = Str(p, 0);
                    int eventId = this is RMZEventCharacter ev && ev.Event != null ? ev.Event.id : 0;
                    if (!RMZScriptShims.TryRun(script, eventId, RMZGameMap.Instance?.MapId ?? 0))
                        GD.Print($"[RMZ] 移動路線腳本未執行：{script}");
                    break;
                }
            }
        }

        // ---------------- 移動輔助 ----------------

        public void MoveForward() => MoveStraight(Direction);

        /// <summary>不管撞不撞得到，直接把「前進」這一步走過去。只給強制路線的門穿越托底用。</summary>
        void ForceStepForward()
        {
            var map = RMZGameMap.Instance;
            if (map == null) return;

            float unit = StepUnit;
            SetDirection(Direction);
            X = map.RoundXf(map.XWithDirection(X, Direction, unit));
            Y = map.RoundYf(map.YWithDirection(Y, Direction, unit));

            int back = ReverseDir(Direction);
            RealX = map.XWithDirection(X, back, unit);
            RealY = map.YWithDirection(Y, back, unit);

            IncreaseSteps();
            _movementSuccess = true;

            GD.PushWarning($"[RMZ] {Name} 的門穿越前進卡住又救不了側身，直接推過去到 ({X},{Y})。");
        }
        public void MoveBackward()
        {
            bool lastFix = DirectionFix;
            DirectionFix = true;
            MoveStraight(ReverseDir(Direction));
            DirectionFix = lastFix;
        }

        public void MoveTowardPlayer()
        {
            if (RMZPlayer.Instance != null) MoveTowardCharacter(RMZPlayer.Instance);
        }

        public void MoveAwayFromPlayer()
        {
            var t = RMZPlayer.Instance;
            if (t == null) return;

            float sx = DeltaXFrom(t.X);
            float sy = DeltaYFrom(t.Y);

            if (Mathf.Abs(sx) > Mathf.Abs(sy))
            {
                MoveStraight(sx > 0 ? Right : Left);
                if (!_movementSuccess && !Mathf.IsZeroApprox(sy)) MoveStraight(sy > 0 ? Down : Up);
            }
            else if (!Mathf.IsZeroApprox(sy))
            {
                MoveStraight(sy > 0 ? Down : Up);
                if (!_movementSuccess && !Mathf.IsZeroApprox(sx)) MoveStraight(sx > 0 ? Right : Left);
            }
        }



        public void TurnRight90() => SetDirection(Direction == Up ? Right : Direction == Right ? Down : Direction == Down ? Left : Up);
        public void TurnLeft90() => SetDirection(Direction == Up ? Left : Direction == Left ? Down : Direction == Down ? Right : Up);

        public void TurnTowardCharacter(RMZCharacterBase target)
        {
            float sx = DeltaXFrom(target.X);
            float sy = DeltaYFrom(target.Y);
            if (Mathf.Abs(sx) > Mathf.Abs(sy)) SetDirection(sx > 0 ? Left : Right);
            else if (!Mathf.IsZeroApprox(sy)) SetDirection(sy > 0 ? Up : Down);
        }

        public void TurnAwayFromCharacter(RMZCharacterBase target)
        {
            float sx = DeltaXFrom(target.X);
            float sy = DeltaYFrom(target.Y);
            if (Mathf.Abs(sx) > Mathf.Abs(sy)) SetDirection(sx > 0 ? Right : Left);
            else if (!Mathf.IsZeroApprox(sy)) SetDirection(sy > 0 ? Down : Up);
        }

        public void TurnTowardPlayer() { if (RMZPlayer.Instance != null) TurnTowardCharacter(RMZPlayer.Instance); }
        public void TurnAwayFromPlayer() { if (RMZPlayer.Instance != null) TurnAwayFromCharacter(RMZPlayer.Instance); }

        // ---------------- 參數取值 ----------------

        internal static int Num(System.Collections.Generic.List<object> p, int i)
        {
            if (p == null || i >= p.Count || p[i] == null) return 0;
            return System.Convert.ToInt32(p[i] is double d ? d : System.Convert.ToDouble(p[i]));
        }

        internal static string Str(System.Collections.Generic.List<object> p, int i)
        {
            if (p == null || i >= p.Count || p[i] == null) return "";
            return p[i].ToString();
        }

        internal static AudioFile ToAudioFile(System.Collections.Generic.List<object> p, int i)
        {
            if (p == null || i >= p.Count || p[i] == null) return null;
            var token = p[i] as Newtonsoft.Json.Linq.JObject;
            return token?.ToObject<AudioFile>();
        }
    }
}
