// 角色移動的共通邏輯，對應 MZ 的 Game_CharacterBase + HalfMove.js。
//
// 座標是 float，因為半格移動下角色會停在 x.5 的位置。
// 需要「這一格是哪個圖塊」時用 TileX / TileY（無條件捨去）。
using Godot;

namespace RMZ
{
    public partial class RMZCharacterBase : Node2D
    {
        // MZ 方向鍵碼（偶數為四方向，奇數為斜向）
        public const int Down = 2, Left = 4, Right = 6, Up = 8;
        public const int LowerLeft = 1, LowerRight = 3, UpperLeft = 7, UpperRight = 9;

        /// <summary>實心物件（門、箱子…）。不套用 HalfMove 的側面閃身，避免被半格穿身。</summary>
        public bool SolidObject;

        public float X;
        public float Y;

        public float RealX { get; protected set; }
        public float RealY { get; protected set; }

        public int TileX => Mathf.FloorToInt(X);
        public int TileY => Mathf.FloorToInt(Y);

        public string CharacterName = "";
        public int CharacterIndex;

        /// <summary>大於 0 時改用「圖塊」當外觀（事件的 image.tileId）。</summary>
        public int TileIdGraphic;
        public int Direction = Down;
        public float Opacity = 1f;
        public bool Transparent;
        public int BlendMode;

        public int MoveSpeed = 4;      // 1=最慢 … 6=最快，4 = 標準
        public int MoveFrequency = 3;
        public bool Through;
        public bool WalkAnime = true;
        public bool StepAnime;
        public bool DirectionFix;
        public int PriorityType = 1;   // 0=角色下方 1=同層 2=角色上方

        /// <summary>個別角色可以關掉半格移動（對應 HalfMove 的 _halfDisable）</summary>
        public bool HalfMoveDisabled;

        // 跳躍狀態（對應 MZ 的 _jumpPeak / _jumpCount）
        float _jumpPeak;
        float _jumpCount;
        float _jumpTotal;
        float _jumpStartX, _jumpStartY;

        protected int _pattern = 1;
        protected int _originalPattern = 1;
        protected float _animationCount;
        protected float _stopCount;
        protected bool _movementSuccess = true;

        public CharacterSpriteView View { get; private set; }
        public RMZBalloonView Balloon { get; private set; }

        /// <summary>顯示頭上的氣泡圖示。balloonId 對應 MZ 的 1~15。</summary>
        public void RequestBalloon(int balloonId)
        {
            if (Balloon == null) Balloon = RMZBalloonView.Attach(this);
            Balloon.Play(balloonId);
        }

        public bool IsBalloonPlaying => Balloon != null && Balloon.IsPlaying;

        public bool IsMoving => !HalfMoveSettings.Same(RealX, X) || !HalfMoveSettings.Same(RealY, Y);
        public bool IsJumping => _jumpCount > 0f;
        public bool IsStopping => !IsMoving && !IsJumping;
        public bool MovementSuccess => _movementSuccess;

        /// <summary>
        /// 跟 HalfMove.js 一樣：總開關（HALF_MOVE_ENABLE／DISABLE）開著、自己沒被關才半格走；
        /// 但站在半格位置上時一律還是半格走，走回整數格為止（不然會永遠卡在 .5）。
        /// </summary>
        public virtual bool IsHalfMove =>
            (HalfMoveSettings.Enabled && !HalfMoveDisabled) ||
            HalfMoveSettings.IsHalfPos(X) || HalfMoveSettings.IsHalfPos(Y);

        /// <summary>一次移動指令走多遠。半格移動時是 0.5 格。</summary>
        public float StepUnit => IsHalfMove ? HalfMoveSettings.TileUnit : 1f;

        public virtual bool IsNormalPriority() => PriorityType == 1;
        public virtual bool IsDashing() => false;
        public virtual bool IsDebugThrough() => false;

        public int RealMoveSpeed => MoveSpeed + (IsDashing() ? 1 : 0);
        public float DistancePerFrame => Mathf.Pow(2f, RealMoveSpeed) / 256f;
        public float AnimationWait => (9 - RealMoveSpeed) * 3f;

        public override void _Ready()
        {
            // Unity 是「同一個 GameObject 掛兩個 Component」，Godot 改成子節點。
            View = GetNodeOrNull<CharacterSpriteView>("View");
            if (View == null)
            {
                View = new CharacterSpriteView { Name = "View" };
                AddChild(View);
            }
            View.Bind(this);

            RMZGameMap.Instance?.Register(this);
        }

        public override void _ExitTree() => RMZGameMap.Instance?.Unregister(this);

        public virtual void Locate(float x, float y)
        {
            X = x; Y = y;
            RealX = x; RealY = y;
            _stopCount = 0;
            ApplyTransform();
        }

        double _movementFrames;

        public override void _Process(double delta)
        {
            RMZPerf.Begin("角色");

            // MZ 是固定 60fps 的邏輯，這裡換算成以秒為單位，掉幀也不會走位
            // 路線指令也要一起按 60Hz 推進；只放大插值距離會在快轉或
            // 低幀率時漏掉下一步，讓劇情等待先結束、人物卻還沒走到定位。
            _movementFrames += delta * 60.0;
            while (_movementFrames >= 1.0 - 0.000001)
            {
                _movementFrames = System.Math.Max(0, _movementFrames - 1.0);
                UpdateAnimation(1f);
                if (IsJumping) UpdateJump(1f);
                else if (IsMoving) UpdateMove(1f);
                else UpdateStop(1f);
            }

            ApplyTransform();

            RMZPerf.End();
        }

        Vector2 _lastAppliedPosition = new Vector2(float.NaN, float.NaN);

        protected void ApplyTransform()
        {
            // 角色錨點在腳底：該格底邊中央。
            // Godot 的 Y 往下為正（跟 RM 一致），所以「往上位移」是減。
            float shift = View != null && View.IsObjectCharacter ? 0f : RMZConfig.CharacterShiftY;
            float jump = JumpHeight;
            var pos = RMZConfig.Snap(new Vector2(
                (RealX + 0.5f) * RMZConfig.TileWidth,
                (RealY + 1f) * RMZConfig.TileHeight - shift - jump));

            // 47 個事件每幀都寫一次座標是白費工，沒動就別碰
            if (pos != _lastAppliedPosition)
            {
                _lastAppliedPosition = pos;
                Position = pos;
            }

            if (View != null) View.UpdateSortingOrder(RealY);
        }

        protected virtual void UpdateMove(float frames)
        {
            float step = DistancePerFrame * frames;

            if (X < RealX) RealX = Mathf.Max(RealX - step, X);
            if (X > RealX) RealX = Mathf.Min(RealX + step, X);
            if (Y < RealY) RealY = Mathf.Max(RealY - step, Y);
            if (Y > RealY) RealY = Mathf.Min(RealY + step, Y);
        }

        protected virtual void UpdateStop(float frames)
        {
            _stopCount += frames;

            // 跟 MZ 一樣：只有「強制移動路線」進行中才推進路線。
            // 如果改成「只要 Route 不是 null 就跑」，一旦還原到先前那條
            // repeat=true 的路線（例如原地跳躍），角色就會永遠跳下去。
            if (RouteForcing) UpdateRoutineMove(frames);
        }

        /// <summary>MZ 的 stopCountThreshold：移動頻率越高，等待越短。</summary>
        public float StopCountThreshold => 30f * (5 - MoveFrequency);
        public bool CheckStop(float threshold) => _stopCount > threshold;
        public void ResetStopCount() => _stopCount = 0f;

        protected virtual void UpdateAnimation(float frames)
        {
            if (IsMoving && WalkAnime) _animationCount += 1.5f * frames;
            else if (StepAnime || !IsOriginalPattern()) _animationCount += frames;

            if (_animationCount >= AnimationWait)
            {
                UpdatePattern();
                _animationCount = 0f;
            }
        }

        protected virtual void UpdatePattern()
        {
            if (!StepAnime && _stopCount > 0) ResetPattern();
            else _pattern = (_pattern + 1) % 4;
        }

        public bool IsOriginalPattern() => _pattern == _originalPattern;
        public void ResetPattern() => _pattern = _originalPattern;

        /// <summary>MZ 的圖框順序是 0,1,2,1。</summary>
        public int Pattern => _pattern < 3 ? _pattern : 1;

        // ---------------- 方向 ----------------

        public virtual void SetDirection(int d)
        {
            // 斜向不改變顯示方向，只有四方向會
            if (!DirectionFix && d > 0 && d % 2 == 0) Direction = d;
            else if (DirectionFix && d > 0 && d % 2 == 0 && d != Direction) _blockedTurns++;
            _stopCount = 0;
        }

        /// <summary>因為「朝向固定」而被忽略掉的轉向次數。用來抓「角色卡在同一個方向」的問題。</summary>
        public int BlockedTurns => _blockedTurns;
        int _blockedTurns;

        public static int ReverseDir(int d) => 10 - d;

        // ---------------- 通行判定 ----------------

        public virtual bool CanPass(float x, float y, int d)
        {
            if (HalfMoveSettings.IsDiagonal(d))
            {
                HalfMoveSettings.Divide(d, out int h, out int v);
                return CanPassDiagonally(x, y, h, v);
            }

            var map = RMZGameMap.Instance;
            if (map == null) return false;

            float unit = StepUnit;
            float x2 = map.RoundXf(map.XWithDirection(x, d, unit));
            float y2 = map.RoundYf(map.YWithDirection(y, d, unit));

            if (!map.IsValid(x2, y2)) return false;
            if (Through || IsDebugThrough()) return true;
            if (!IsMapPassable(x, y, d)) return false;
            if (map.IsCollided(x2, y2, this)) return false;
            return true;
        }

        public bool CanPassDiagonally(float x, float y, int horz, int vert)
        {
            var map = RMZGameMap.Instance;
            float unit = StepUnit;
            float x2 = map.RoundXf(map.XWithDirection(x, horz, unit));
            float y2 = map.RoundYf(map.YWithDirection(y, vert, unit));

            return (CanPass(x, y, vert) && CanPass(x, y2, horz))
                || (CanPass(x, y, horz) && CanPass(x2, y, vert));
        }

        /// <summary>MZ 原本的地圖通行判定（整格步幅），HalfMove 稱之為 alias。</summary>
        bool AliasMapPassable(float x, float y, int d)
        {
            var map = RMZGameMap.Instance;
            float x2 = map.RoundNoHalfXWithDirection(x, d);
            float y2 = map.RoundNoHalfYWithDirection(y, d);
            return map.IsPassable(x, y, d) && map.IsPassable(x2, y2, ReverseDir(d));
        }

        /// <summary>
        /// HalfMove 的通行判定，逐行照移。
        /// 重點：位於半格位置時，牆壁的檢查會延到「踏回整格」那一步才做。
        /// </summary>
        public bool IsMapPassable(float x, float y, int d)
        {
            var map = RMZGameMap.Instance;
            bool hx = HalfMoveSettings.IsHalfPos(x);
            bool hy = HalfMoveSettings.IsHalfPos(y);
            bool result = true;

            if (!IsHalfMove)
            {
                if (hx) x = map.RoundHalfXWithDirection(x, d);
                if (hy) y = map.RoundHalfYWithDirection(y, d);
                return AliasMapPassable(x, y, d);
            }

            if (hx && hy)
            {
                if (d == Up)
                {
                    float y1 = map.RoundHalfYWithDirection(y, d);
                    float xLeft = map.RoundHalfXWithDirection(x, Left);
                    float xRight = map.RoundHalfXWithDirection(x, Right);
                    float y3 = map.RoundHalfYWithDirection(y, Down);

                    if (AliasMapPassable(xLeft, y1, ReverseDir(d)) && AliasMapPassable(xRight, y1, ReverseDir(d)))
                        result = AliasMapPassable(xLeft, y1, Right) || AliasMapPassable(xRight, y1, Left);

                    result = result && AliasMapPassable(xLeft, y3, d) && AliasMapPassable(xRight, y3, d);
                }
            }
            else if (hx)
            {
                if (d == Down)
                {
                    float y2 = map.RoundNoHalfYWithDirection(y, d);
                    float xLeft = map.RoundHalfXWithDirection(x, Left);
                    float xRight = map.RoundHalfXWithDirection(x, Right);

                    if (AliasMapPassable(xLeft, y2, ReverseDir(d)) && AliasMapPassable(xRight, y2, ReverseDir(d)))
                        result = AliasMapPassable(xLeft, y2, Right) || AliasMapPassable(xRight, y2, Left);

                    result = result && AliasMapPassable(xLeft, y, d) && AliasMapPassable(xRight, y, d);
                }
            }
            else if (hy)
            {
                if (d != Down)
                {
                    float y4 = map.RoundHalfYWithDirection(y, Down);
                    result = AliasMapPassable(x, y4, d);
                }
            }
            else
            {
                if (d != Up)
                    result = AliasMapPassable(x, y, d);
                else
                    result = map.IsPassable(x, y, Down) || map.IsPassable(x, y, Left)
                          || map.IsPassable(x, y, Right) || map.IsPassable(x, y, Up);
            }

            return result;
        }

        // ---------------- 移動 ----------------

        public virtual void MoveStraight(int d)
        {
            if (HalfMoveSettings.IsDiagonal(d))
            {
                HalfMoveSettings.Divide(d, out int dh, out int dv);
                MoveDiagonally(dh, dv);
                return;
            }

            var map = RMZGameMap.Instance;
            _movementSuccess = CanPass(X, Y, d);

            if (_movementSuccess)
            {
                SetDirection(d);
                float unit = StepUnit;

                X = map.RoundXf(map.XWithDirection(X, d, unit));
                Y = map.RoundYf(map.YWithDirection(Y, d, unit));

                // real 座標從「反方向那半格」開始補間，循環地圖繞回去也不會瞬移
                int back = ReverseDir(d);
                RealX = map.XWithDirection(X, back, unit);
                RealY = map.YWithDirection(Y, back, unit);

                IncreaseSteps();
            }
            else
            {
                SetDirection(d);

                // AvoidCorner：直行被轉角卡住時，改用斜向再試一次。
                // 只給玩家操作用 —— 演出用的移動路線如果自己改成斜走，站位就全亂了。
                if (HalfMoveSettings.AvoidCorner && IsHalfMove && AllowCornerAvoidance) TryAvoidCorner(d);
                if (!_movementSuccess) OnMoveBlocked(d);
            }
        }

        void TryAvoidCorner(int d)
        {
            int[] alternatives = d == Up ? new[] { UpperLeft, UpperRight }
                               : d == Down ? new[] { LowerLeft, LowerRight }
                               : d == Left ? new[] { UpperLeft, LowerLeft }
                               : new[] { UpperRight, LowerRight };

            foreach (int alt in alternatives)
            {
                HalfMoveSettings.Divide(alt, out int h, out int v);
                if (!CanPassDiagonally(X, Y, h, v)) continue;

                MoveDiagonally(h, v);
                if (_movementSuccess) return;
            }
        }

        public void MoveDiagonally(int horz, int vert)
        {
            var map = RMZGameMap.Instance;
            float unit = StepUnit;

            _movementSuccess = CanPassDiagonally(X, Y, horz, vert);

            if (_movementSuccess)
            {
                X = map.RoundXf(map.XWithDirection(X, horz, unit));
                Y = map.RoundYf(map.YWithDirection(Y, vert, unit));
                RealX = map.XWithDirection(X, ReverseDir(horz), unit);
                RealY = map.YWithDirection(Y, ReverseDir(vert), unit);
                IncreaseSteps();
            }

            if (!DirectionFix)
            {
                switch (HalfMoveSettings.DiagonalTurn)
                {
                    case HalfMoveSettings.DiagonalFacing.Horizontal:
                        SetDirection(horz);
                        break;

                    case HalfMoveSettings.DiagonalFacing.Vertical:
                        SetDirection(vert);
                        break;

                    default:
                        // MZ 原本的規則
                        if (Direction == ReverseDir(horz)) SetDirection(horz);
                        if (Direction == ReverseDir(vert)) SetDirection(vert);
                        break;
                }
            }
        }

        public virtual void MoveRandom()
        {
            int d = 2 * (GD.RandRange(0, 3) + 1);
            if (CanPass(X, Y, d)) MoveStraight(d);
        }

        public virtual void MoveTowardCharacter(RMZCharacterBase target)
        {
            float sx = DeltaXFrom(target.X);
            float sy = DeltaYFrom(target.Y);

            if (Mathf.Abs(sx) > Mathf.Abs(sy))
            {
                MoveStraight(sx > 0 ? Left : Right);
                if (!_movementSuccess && !Mathf.IsZeroApprox(sy)) MoveStraight(sy > 0 ? Up : Down);
            }
            else if (!Mathf.IsZeroApprox(sy))
            {
                MoveStraight(sy > 0 ? Up : Down);
                if (!_movementSuccess && !Mathf.IsZeroApprox(sx)) MoveStraight(sx > 0 ? Left : Right);
            }
        }

        public float DeltaXFrom(float x) => X - x;
        public float DeltaYFrom(float y) => Y - y;

        // ---------------- 跳躍 ----------------

        /// <summary>
        /// 對應 MZ 的 Game_CharacterBase.jump。
        /// 注意「原地跳躍」（0,0）在 MZ 也是看得見的：會彈起約 18 像素。
        /// </summary>
        public void Jump(float xPlus, float yPlus)
        {
            if (Mathf.Abs(xPlus) > Mathf.Abs(yPlus))
            {
                if (!Mathf.IsZeroApprox(xPlus)) SetDirection(xPlus < 0 ? Left : Right);
            }
            else
            {
                if (!Mathf.IsZeroApprox(yPlus)) SetDirection(yPlus < 0 ? Up : Down);
            }

            _jumpStartX = X;
            _jumpStartY = Y;
            X += xPlus;
            Y += yPlus;

            float distance = Mathf.Round(Mathf.Sqrt(xPlus * xPlus + yPlus * yPlus));
            _jumpPeak = 10f + distance - MoveSpeed;
            _jumpTotal = _jumpPeak * 2f;
            _jumpCount = _jumpTotal;

            _stopCount = 0;
            Straighten();
        }

        void UpdateJump(float frames)
        {
            _jumpCount = Mathf.Max(_jumpCount - frames, 0f);

            float t = _jumpTotal <= 0f ? 1f : 1f - _jumpCount / _jumpTotal;
            RealX = Mathf.Lerp(_jumpStartX, X, t);
            RealY = Mathf.Lerp(_jumpStartY, Y, t);

            if (_jumpCount <= 0f)
            {
                RealX = X;
                RealY = Y;
            }
        }

        /// <summary>MZ 的拋物線公式，單位是像素。</summary>
        public float JumpHeight
        {
            get
            {
                if (_jumpCount <= 0f) return 0f;
                float d = Mathf.Abs(_jumpCount - _jumpPeak);
                return (_jumpPeak * _jumpPeak - d * d) / 2f;
            }
        }

        public void Straighten()
        {
            if (WalkAnime || StepAnime) _pattern = _originalPattern;
            _animationCount = 0f;
        }

        protected virtual void IncreaseSteps() => _stopCount = 0;
        protected virtual void OnMoveBlocked(int d) { }

        /// <summary>是否允許卡角時自動改走斜向。預設關閉，只有玩家自己操作時開。</summary>
        protected virtual bool AllowCornerAvoidance => false;
    }
}
