// 場景進入點：在執行期把整個遊戲世界組出來。
// 場景裡只要放一個 Node2D 掛上這支腳本就能跑（Scenes/Main.tscn）。
//
// [移植進度] 載資料 → 畫地圖 → 生事件 → 主角能走 → 事件直譯器 → 對話框 → 場所移動。
// Unity 版的 RMZBootstrap（538 行）還負責初始裝備、內容分級、對話配色、快速劇情等等，
// 那些等對應系統移植過來之後再逐段搬回來。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public partial class RMZBootstrap : Node2D
    {
        public static RMZBootstrap Instance { get; private set; }

        // ---- 起始位置 ----

        /// <summary>勾選時一律從標題畫面地圖進場，忽略 System.json 的設定。</summary>
        [Export] public bool ForceTitleMap = true;
        [Export] public int TitleMapId = 3;
        [Export] public int TitleX = 17;
        [Export] public int TitleY = 54;

        /// <summary>不強制標題時的起點；-1 代表沿用 System.json</summary>
        [Export] public int StartMapId = -1;
        [Export] public int StartX = -1;
        [Export] public int StartY = -1;

        // ---- 鏡頭 ----

        [Export] public float CameraTilesHigh = 15f;
        [Export] public bool ClampCameraToMap = true;

        // ---- 除錯 ----

        [Export] public bool SpawnEvents = true;

        /// <summary>印出事件的等待、移動路線、氣泡與經過時間，用來跟 RM 對照演出節奏</summary>
        [Export] public bool LogEvents;

        /// <summary>測試用：無視 99 號「不能移動」開關</summary>
        [Export] public bool IgnoreInputLock;

        /// <summary>整段腳本認得的「清空所有開關／變數」要不要真的執行。移植期間想保留現場可以先關掉</summary>
        [Export] public bool AllowScriptResetAll = true;

        /// <summary>監看這個開關：有人改它就在 Console 印出是哪個事件改的。0 = 不監看</summary>
        [Export] public int WatchSwitchId = 99;

        public RMZSolidObjectSettings SolidObjects { get; } = new RMZSolidObjectSettings();

        /// <summary>對話框配色：底色透明度與依說話者上色。</summary>
        public RMZDialogueStyle Dialogue { get; } = new RMZDialogueStyle { Speakers = RMZDialogueStyle.Default() };

        /// <summary>快速劇情：按住左鍵／右鍵／空白／Enter／Ctrl 快轉。</summary>
        public RMZFastForwardSettings FastForward { get; } = new RMZFastForwardSettings();

        public RMZGameMap GameMap { get; private set; }
        public RMZTilemapRenderer Tilemap { get; private set; }
        public RMZEventRunner Events { get; private set; }
        public RMZScreen Screen { get; private set; }
        public RMZPlayer Player { get; private set; }
        public RMZCameraFollow Camera { get; private set; }

        Node2D _world;
        Node2D _eventRoot;
        readonly List<RMZEventCharacter> _eventNodes = new List<RMZEventCharacter>();

        // ---- 場所移動 ----

        bool _transferReserved;
        int _transferMapId, _transferX, _transferY, _transferDirection;

        /// <summary>場所移動排隊中。直譯器會在這期間停下來等。</summary>
        public bool IsTransferring => _transferReserved;

        // ---- 場所移動的淡出淡入（對應 MZ 場所移動的「淡入淡出：黑／白／無」）----
        // 先淡出到全黑，再換地圖，換完再淡入。畫面已經被事件弄成全黑（221 淡出）時就不再疊一次。
        const float TransferFadeSeconds = 0.3f;
        int _transferFadeType;        // 0 黑、1 白、2 無
        float _transferFade;          // 0 = 看得見，1 = 全蓋住
        bool _transferFadingIn;
        CanvasLayer _transferFadeLayer;
        ColorRect _transferFadeRect;

        void EnsureTransferFade()
        {
            if (_transferFadeRect != null) return;
            _transferFadeLayer = new CanvasLayer { Layer = 30 };   // 對話框(10)之上、存讀檔(46)／選單(50)之下
            AddChild(_transferFadeLayer);
            _transferFadeRect = new ColorRect { Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
            _transferFadeRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _transferFadeLayer.AddChild(_transferFadeRect);
        }

        void ApplyTransferFade()
        {
            EnsureTransferFade();
            var c = _transferFadeType == 1 ? new Color(1f, 1f, 1f) : new Color(0f, 0f, 0f);
            c.A = Mathf.Clamp(_transferFade, 0f, 1f);
            _transferFadeRect.Color = c;
            _transferFadeRect.Visible = c.A > 0.001f;
        }

        bool ScreenAlreadyBlack =>
            Screen != null && (Screen.FadeOpacity >= 250f || Screen.Tone.X <= -250f);

        /// <summary>還在淡出就回傳 true（這一幀先不換地圖）。</summary>
        bool UpdateTransferFadeOut(float dt)
        {
            if (_transferFadeType == 2 || (_transferFade <= 0f && ScreenAlreadyBlack)) return false;
            if (_transferFade >= 1f) return false;
            _transferFade = Mathf.Min(1f, _transferFade + dt / TransferFadeSeconds);
            ApplyTransferFade();
            return _transferFade < 1f;
        }

        /// <summary>目前在哪張地圖（讀檔時要記下來）。</summary>
        public int CurrentMapId => GameMap?.MapId ?? 0;

        /// <summary>讀檔用：場所移動排的是整數格，這裡另外記半格制的精確落點，換完地圖後套回去。</summary>
        public Vector2? PendingPreciseLanding;

        /// <summary>讀檔用：場所移動完成後要蓋回去的音樂（存檔當下正在放的那首）。</summary>
        public AudioFile PendingBgm;

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            RMZDebug.LogEvents = LogEvents;
            RMZDebug.Reset();
            RMZEventRunner.RegisterInputActions();

            RMZLocalization.Load("CN");
            RMZDatabase.LoadCore();
            if (!RMZDatabase.Loaded)
            {
                GD.PushError($"[RMZ] 資料讀不到，遊戲無法啟動。請確認 {RMZConfig.DataPath} 存在。");
                return;
            }

            // 世界節點：地圖與角色都掛在這底下
            _world = new Node2D { Name = "World" };
            AddChild(_world);

            GameMap = new RMZGameMap { Name = "GameMap" };
            AddChild(GameMap);

            Events = new RMZEventRunner { Name = "EventRunner" };
            AddChild(Events);

            Screen = new RMZScreen { Name = "Screen" };
            AddChild(Screen);

            AddChild(new RMZAudio { Name = "Audio" });
            AddChild(new RMZScreenView { Name = "ScreenView" });
            AddChild(new RMZTextWindowLayer { Name = "TextWindowLayer" });
            AddChild(new RMZMessageWindow { Name = "MessageWindow" });
            AddChild(new RMZInputWindow { Name = "InputWindow" });
            AddChild(new RMZFastForwardDriver { Name = "FastForward" });
            AddChild(new RMZOptionsScene { Name = "OptionsScene" });
            AddChild(new RMZSaveScene { Name = "SaveScene" });
            AddChild(new RMZSkillCodex { Name = "SkillCodex" });
            AddChild(new RMZSystemMenu { Name = "SystemMenu" });

            // 視差背景／圖片地圖（奴隸區就是一整張圖）
            _world.AddChild(new RMZParallax { Name = "Parallax" });

            Tilemap = new RMZTilemapRenderer { Name = "Tilemap" };
            _world.AddChild(Tilemap);

            _eventRoot = new Node2D { Name = "Events" };
            _world.AddChild(_eventRoot);

            Player = new RMZPlayer { Name = "Player" };
            _world.AddChild(Player);

            // 事件指令 212「顯示動畫」：RM 的 Effekseer 特效（addons/effekseer）
            _world.AddChild(new RMZAnimationPlayer { Name = "Animations" });

            // 成就（HC_Achievements）：每 20 幀檢查一次條件
            AddChild(new RMZAchievementDriver { Name = "Achievements" });

            Camera = new RMZCameraFollow
            {
                Name = "Camera",
                Target = Player,
                BaseTilesHigh = CameraTilesHigh,
                ClampToMap = ClampCameraToMap,
            };
            _world.AddChild(Camera);
            Camera.MakeCurrent();

            ApplyLeaderGraphic();

            // 設定要等音訊與玩家節點都建好再套用（Apply 會去推音量與觸控設定）
            RMZOptions.Load();

            int mapId = ForceTitleMap ? TitleMapId
                      : StartMapId >= 0 ? StartMapId
                      : RMZDatabase.System?.startMapId ?? 1;

            int x = ForceTitleMap ? TitleX : StartX >= 0 ? StartX : RMZDatabase.System?.startX ?? 0;
            int y = ForceTitleMap ? TitleY : StartY >= 0 ? StartY : RMZDatabase.System?.startY ?? 0;

            LoadMap(mapId, x, y);
        }

        /// <summary>玩家的行走圖取自隊伍第一位角色，跟 MZ 一樣。</summary>
        void ApplyLeaderGraphic()
        {
            int leaderId = (RMZDatabase.System?.partyMembers != null && RMZDatabase.System.partyMembers.Length > 0)
                ? RMZDatabase.System.partyMembers[0] : 1;

            var actor = RMZDatabase.Actor(leaderId);
            Player.CharacterName = actor != null && !string.IsNullOrEmpty(actor.characterName)
                ? actor.characterName : "player";
            Player.CharacterIndex = actor?.characterIndex ?? 0;
        }

        public override void _Process(double delta)
        {
            // 場所移動排在幀尾處理：事件在指令中途要求換地圖時，
            // 這一幀剩下的邏輯先跑完，跟 MZ 的 performTransfer 一樣。
            float dt = (float)delta;
            if (_transferReserved)
            {
                if (!UpdateTransferFadeOut(dt))
                {
                    PerformTransfer();
                    if (_transferFade > 0f) _transferFadingIn = true;
                }
            }
            else if (_transferFadingIn)
            {
                _transferFade = Mathf.Max(0f, _transferFade - dt / TransferFadeSeconds);
                ApplyTransferFade();
                if (_transferFade <= 0f) _transferFadingIn = false;
            }

            // 畫面震動：MZ 是整個畫面在抖，這裡抖鏡頭
            if (Screen != null && Camera != null)
                Camera.Offset = Screen.ShakeOffset;
        }

        /// <summary>事件指令 201。跟 MZ 一樣先預約，等這一幀跑完再真的換。</summary>
        public void ReserveTransfer(int mapId, int x, int y, int direction, int fadeType = 0)
        {
            _transferReserved = true;
            _transferFadeType = fadeType;
            _transferFadingIn = false;
            _transferMapId = mapId;
            _transferX = x;
            _transferY = y;
            _transferDirection = direction;
        }

        void PerformTransfer()
        {
            // 舊存檔若位於未解鎖的區域，回公會辦理，避免困在室內。
            if(_transferMapId>=39&&_transferMapId<=43&&!RMZDistrict.CanEnter){
                _transferMapId=2;_transferX=19;_transferY=26;PendingPreciseLanding=null;PendingBgm=null;
            }
            else if(_transferMapId>=40&&_transferMapId<=43){
                var room=RMZDatabase.LoadMap(_transferMapId);
                bool outside=room==null||_transferX<0||_transferY<0||_transferX>=room.width||_transferY>=room.height;
                if(outside||(room.data[(2*room.height+_transferY)*room.width+_transferX]==169)){
                    _transferX=_transferMapId==42?11:12;_transferY=_transferMapId==40?12:13;PendingPreciseLanding=null;
                }
            }
            _transferReserved = false;

            if (_transferMapId != GameMap.MapId)
            {
                LoadMap(_transferMapId, _transferX, _transferY);
            }
            else
            {
                Player.Locate(_transferX, _transferY);

                // 同地圖傳送（例如「認真探索」結束後傳回同一張地圖）也要重新整理事件頁面，
                // 不然像「自動執行、條件是某個變數 == 某值」這種事件，剛剛才被設好的
                // 變數不會被重新判定，劇情就卡住不會往下推——LoadMap 那條路徑本來就會呼叫
                // Events.RequestRefresh()，這裡漏掉同一件事。
                Events.RequestRefresh();
            }

            if (_transferDirection > 0) Player.SetDirection(_transferDirection);

            // MZ 場所移動會把鏡頭直接置中在主角身上（GALV 聚焦別人時例外），不會從舊位置滑過去
            if (Camera != null && (Camera.Target == null || Camera.Target == Player)) Camera.SnapToTarget();
            Player.ClearDestination();

            // 讀檔：整數格落地之後，補回半格制的精確座標
            if (PendingPreciseLanding.HasValue)
            {
                var p = PendingPreciseLanding.Value;
                Player.Locate(p.X, p.Y);
                PendingPreciseLanding = null;
            }

            if (PendingBgm != null)
            {
                RMZAudio.Instance?.PlayBgm(PendingBgm);
                PendingBgm = null;
            }
        }

        public void LoadMap(int mapId, int x, int y)
        {
            var data = RMZDatabase.LoadMap(mapId);
            if (data == null) return;
            RMZAnimationPlayer.Instance?.Clear();

            var tileset = RMZDatabase.Tileset(data.tilesetId);

            GameMap.Setup(mapId, data, tileset);
            RMZGameState.NoteMapEntered(mapId);   // 晚上去過酒館 → 隔天沒有休息加成
            Tilemap.Build(data, tileset);
            RMZParallax.Instance?.Build(data);

            SpawnEventNodes(mapId, data);
            Events.Setup(mapId, _eventNodes);
            Events.RequestRefresh();

            Player.Locate(x, y);
            GameMap.Register(Player);

            GD.Print($"[RMZ] 進入地圖 {mapId}「{RMZDatabase.MapName(mapId)}」" +
                     $"（{data.width}x{data.height}，事件 {data.events?.Count ?? 0} 個），主角在 ({x},{y})");
        }

        void SpawnEventNodes(int mapId, MapData data)
        {
            foreach (var node in _eventNodes)
                if (IsInstanceValid(node)) node.QueueFree();
            _eventNodes.Clear();

            if (!SpawnEvents || data.events == null) return;

            foreach (var ev in data.events)
            {
                if (ev == null) continue;

                var node = new RMZEventCharacter { Name = $"Event{ev.id}_{ev.name}" };
                _eventRoot.AddChild(node);
                node.Setup(mapId, ev);
                _eventNodes.Add(node);
            }
        }
    }
}
