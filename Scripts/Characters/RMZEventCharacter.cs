// 地圖事件，對應 MZ 的 Game_Event。
// 這一版做到：頁面條件判定、外觀、擋路、自律移動。
// 事件指令的執行（對話、選項、開關操作…）留給下一階段的直譯器。
using Godot;

namespace RMZ
{
    public partial class RMZEventCharacter : RMZCharacterBase
    {
        public MapEvent Event { get; private set; }
        public EventPage Page { get; private set; }
        // -2 = 還沒判定過。不能用 -1，因為 -1 是「沒有任何頁面成立」的合法結果，
        // 用 -1 當初始值的話，一開始就沒頁面的事件會被當成「沒變化」而跳過套用。
        public int PageIndex { get; private set; } = -2;

        int _mapId;

        public int Trigger => Page?.trigger ?? -1;

        public void Setup(int mapId, MapEvent ev)
        {
            _mapId = mapId;
            Event = ev;
            Locate(ev.x, ev.y);
            RefreshPage();
            AddMapSign(ev.note);
        }

        /// <summary>依開關／變數／獨立開關重新挑選要顯示的事件頁（由後往前找第一個成立的）。</summary>
        public void RefreshPage()
        {
            int newIndex = -1;
            for (int i = Event.pages.Count - 1; i >= 0; i--)
            {
                if (MeetsConditions(Event.pages[i]))
                {
                    newIndex = i;
                    break;
                }
            }

            // 開場主線尚未完成時，不讓後面的「日期」日常頁蓋掉教學接續。
            // 也照顧上一版已加過天數、存於鐵匠鋪之前的存檔。
            if(_mapId == 2 || _mapId == 35) {
                int adventure = RMZGameState.Variable(2), introduction = RMZGameState.Variable(3), smith = RMZGameState.Variable(4);
                if(Event.id == 22 && Event.pages.Count > 2 && adventure >= 2 && smith <= 1 && (introduction < 2 || adventure < 4)) newIndex = 2;
                if(Event.id == 22 && RMZGameState.Variable(1) < 5) {
                    if(smith == 3) newIndex = 4;
                    else if(smith == 4) newIndex = 5;
                    else if(smith == 5) newIndex = 6;
                }
                if(Event.id == 26 && Event.pages.Count > 4) {
                    if(smith == 0 && adventure >= 4) newIndex = 0;
                    else if(smith == 1) newIndex = Event.pages.Count - 1;
                    else if(smith == 2) newIndex = 1;
                    // 新增的續做頁只在教學中生效，其他時候沿用原頁面順序。
                    else if(newIndex == Event.pages.Count - 1) {
                        newIndex = -1;
                        for(int i=Event.pages.Count-2;i>=0;i--) if(MeetsConditions(Event.pages[i])) {newIndex=i;break;}
                    }
                }
            }
            if (newIndex == PageIndex) return;

            RMZDebug.Log(() => $"事件{Event.id}（地圖{_mapId}）換頁：{PageIndex} → {newIndex}");

            // 換頁「不」打斷正在跑的直譯器 —— MZ 的 Game_Event.refresh() 只換頁面設定，
            // Game_Map._interpreter 手上抓著舊的指令陣列，會照樣跑到結束。
            //
            // 之前這裡會把它砍掉，結果是：某一頁把畫面染黑、演完劇情、
            // 中途改了一個變數導致換頁 —— 剩下的「把色調染回來」就再也沒跑到，
            // 畫面就一直黑著。第一天睡覺之後第二天全黑就是這樣來的。

            PageIndex = newIndex;
            Page = newIndex >= 0 ? Event.pages[newIndex] : null;
            ApplyPage();
        }

        bool MeetsConditions(EventPage page)
        {
            var c = page.conditions;

            if (c.switch1Valid && !RMZGameState.Switch(c.switch1Id)) return false;
            if (c.switch2Valid && !RMZGameState.Switch(c.switch2Id)) return false;
            if (c.variableValid && RMZGameState.Variable(c.variableId) < c.variableValue) return false;
            if (c.selfSwitchValid && !RMZGameState.SelfSwitch(_mapId, Event.id, c.selfSwitchCh)) return false;

            // TODO: actorValid / itemValid 要等隊伍與道具系統做好再接
            return true;
        }

        void ApplyPage()
        {
            if (Page == null)
            {
                // 對應 MZ 的 Game_Event.clearPageSettings()：
                // 沒有任何頁面成立的事件會被設成可穿透，不會擋住玩家。
                CharacterName = "";
                TileIdGraphic = 0;
                Through = true;
                PriorityType = 0;
                SolidObject = false;
                SetMoveRoute(null);
                SetEnabled(false);
                return;
            }

            SetEnabled(true);
            Through = false;
            CharacterName = Page.image.characterName;
            CharacterIndex = Page.image.characterIndex;
            TileIdGraphic = Page.image.tileId;   // 事件也可以直接用圖塊當外觀
            Direction = Page.image.direction;
            _originalPattern = Page.image.pattern;
            _pattern = Page.image.pattern;

            MoveSpeed = Page.moveSpeed;
            MoveFrequency = Page.moveFrequency;
            Through = Page.through;
            WalkAnime = Page.walkAnime;
            StepAnime = Page.stepAnime;
            DirectionFix = Page.directionFix;
            PriorityType = Page.priorityType;

            // 門、箱子這類物件不吃半格側身閃過
            SolidObject = RMZSolidObjectSettings.Current.IsSolid(Event.name, Event.note, CharacterName);

            // 換頁等於換一套設定，舊的自訂移動路線不能留著
            SetMoveRoute(null);
        }

        /// <summary>
        /// Unity 的 gameObject.SetActive 在 Godot 沒有單一對應：
        /// 顯示與否是 Visible，跑不跑邏輯是 SetProcess。兩個都要關。
        /// </summary>
        public void SetEnabled(bool on)
        {
            Visible = on;
            SetProcess(on);
        }

        protected override void UpdateStop(float frames)
        {
            base.UpdateStop(frames);   // 強制路線在這裡推進
            if (!RouteForcing) UpdateSelfMovement(frames);
        }

        void UpdateSelfMovement(float frames)
        {
            if (Page == null || Page.moveType == 0) return;                  // 固定
            var runner = RMZEventRunner.Instance;
            if (runner != null && (runner.IsBusy || runner.EventsPaused)) return;
            if (!CheckStop(StopCountThreshold)) return;

            switch (Page.moveType)
            {
                case 1: MoveTypeRandom(); break;
                case 2: MoveTypeTowardPlayer(); break;
                case 3: MoveTypeCustom(frames); break;
            }
        }

        void MoveTypeRandom()
        {
            switch (GD.RandRange(0, 5))
            {
                case 0: case 1: MoveRandom(); break;
                case 2: case 3: case 4: MoveForward(); break;
                default: ResetStopCount(); break;
            }
        }

        void MoveTypeTowardPlayer()
        {
            var player = RMZPlayer.Instance;
            if (player == null) { MoveRandom(); return; }

            // MZ：距離太遠就改成隨機走，避免整張地圖的事件都黏過來
            float dist = Mathf.Abs(DeltaXFrom(player.X)) + Mathf.Abs(DeltaYFrom(player.Y));
            if (dist >= 20f) { MoveRandom(); return; }

            switch (GD.RandRange(0, 5))
            {
                case 0: case 1: case 2: case 3: MoveTowardCharacter(player); break;
                case 4: MoveRandom(); break;
                default: MoveForward(); break;
            }
        }

        /// <summary>事件頁上設定的自訂移動路線。跟強制路線是分開的兩條路。</summary>
        void MoveTypeCustom(float frames)
        {
            if (Route == null && Page.moveRoute != null) SetMoveRoute(Page.moveRoute);
            if (Route != null) UpdateRoutineMove(frames);
        }

    }
}
