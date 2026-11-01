// 小遊戲的主控。場景裡只要有這一支，按 Play 就會自己把整局組出來。
//
// 跟本篇（RMZ 那套）完全沒有引用關係，只有讀同一批行走圖 PNG。
using System.Collections.Generic;
using RMZS.GodotPort;
using RMZS.GodotPort.InputSystem;

namespace RMZS
{
    public partial class SVGame : MonoBehaviour
    {
        public static SVGame Instance { get; private set; }

        [Header("隊友（五位女主）")]
        public List<SVCompanionType> Companions = new List<SVCompanionType>();

        [Header("敵人")]
        public List<SVEnemyType> Enemies = new List<SVEnemyType>();

        [Header("節奏")]
        [Tooltip("一開始每秒生幾隻")] public float BaseSpawnRate = 2.5f;
        [Tooltip("每過一階（30 秒）生成速度乘上這個")] public float SpawnGrowth = 1.35f;
        [Tooltip("每過一階敵人血量乘上這個")] public float HpGrowth = 1.30f;
        [Tooltip("每過一階敵人傷害乘上這個")] public float DamageGrowth = 1.12f;
        public int MaxEnemies = 400;

        public SVPlayer Player { get; private set; }
        public Transform ShotRoot { get; private set; }
        public Transform GemRoot { get; private set; }
        public bool IsOver { get; private set; }

        /// <summary>鏡頭。指揮系統要拿它做座標換算。</summary>
        public Camera Cam => _camera;

        // ---- 本篇帶進來的委託。單獨按 Play 測試時是 null，走沙盒模式。 ----
        SVBriefing _briefing;
        public SVBriefing Briefing => _briefing;

        /// <summary>這一局是本篇派下來的委託（而不是單獨按 Play 的沙盒）。</summary>
        public bool LaunchedFromStory => _briefing != null;

        /// <summary>加入別人的隊伍時玩家沒有指揮權，這裡是 false。</summary>
        public bool PlayerCommands { get; private set; } = true;

        /// <summary>隊友的初始命令。照設計，一律先自由移動。</summary>
        public SVOrder DefaultOrder { get; private set; } = SVOrder.Free;

        /// <summary>撐過這個秒數就算完成委託。0 = 無盡模式。</summary>
        public float SurviveSeconds { get; private set; }

        /// <summary>這一局有沒有達成目標。</summary>
        public bool Cleared { get; private set; }

        bool _reported;

        bool _manualPause;
        /// <summary>選強化、手動暫停、結束都算暫停，場上一切停住。</summary>
        /// <summary>外面（系統選單那類）要求暫停。本篇那邊開選單時會把它打開。</summary>
        public static bool ExternalPause;

        public bool Paused => _manualPause || ExternalPause || SVDev.PanelOpen || RMZ.SVStatsOverlay.IsOpen || RMZ.RMZOptionsScene.IsOpen ||
                              IsOver || Choosing || ChoosingBlessing || Talking || _bossArrival || _tutorialTalking;

        /// <summary>收尾流程的對話中。逃跑階段刻意不算，玩家要能繼續追殺。</summary>
        public bool Talking =>
            EndPhase == SVEndPhase.BossTalk || EndPhase == SVEndPhase.FinalTalk;

        /// <summary>玩家自己按下的暫停（右上角那顆按鈕）。</summary>
        public bool ManualPaused => _manualPause;

        // 新手教學不給暫停：教學是跟著露娜的台詞一步步走的，暫停畫面「按 Esc 繼續」
        // 那段提示跟教學氣氛不搭，乾脆教學期間直接不讓暫停生效。
        public void SetPaused(bool paused) => _manualPause = paused && !IsTutorial;
        public void TogglePause() { if (IsTutorial) return; _manualPause = !_manualPause; }

        // ---------------- 受傷暈染 ----------------

        /// <summary>0~1。HUD 讀這個決定血色暈染多濃。</summary>
        public float HurtFlash { get; private set; }

        [Header("受傷暈染")]
        [Tooltip("挨一下最濃能到多少（0~1）")]
        [Range(0.1f, 1f)] public float HurtFlashMax = 0.75f;

        [Tooltip("退掉的速度。越大退越快")]
        public float HurtFlashFade = 2.6f;

        [Tooltip("血量越低，底噪的紅暈越濃。低於這個比例才開始出現")]
        [Range(0f, 0.9f)] public float LowHpThreshold = 0.3f;

        /// <summary>
        /// 挨打了。傷害佔血量上限的比例越大閃得越紅，
        /// 但至少也給一個下限，免得被小怪磨血時完全沒有回饋。
        /// </summary>
        public void FlashHurt(int damage)
        {
            if (Player == null || Player.MaxHp <= 0) return;

            float ratio = Mathf.Clamp01(damage / (Player.MaxHp * 0.25f));
            float want = Mathf.Lerp(0.35f, 1f, ratio) * HurtFlashMax;
            HurtFlash = Mathf.Max(HurtFlash, want);   // 連續挨打取比較濃的那個
        }

        void UpdateHurtFlash(float dt)
        {
            if (HurtFlash > 0f) HurtFlash = Mathf.Max(0f, HurtFlash - HurtFlashFade * dt);
        }

        /// <summary>殘血時畫面一直帶著一層淡紅，不用低頭看血條也知道快死了。</summary>
        public float LowHpTint
        {
            get
            {
                if (Player == null || Player.MaxHp <= 0 || Player.IsDead) return 0f;

                float hp = (float)Player.Hp / Player.MaxHp;
                if (hp >= LowHpThreshold) return 0f;

                // 越接近 0 越濃，最多 0.35，並且會隨心跳一起脈動
                float depth = 1f - hp / Mathf.Max(0.01f, LowHpThreshold);
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
                return depth * 0.35f * pulse;
            }
        }

        // ---- 三選一強化 ----
        public bool Choosing => _choices != null && _choices.Count > 0;
        public List<SVUpgrade> Choices => _choices;
        public IReadOnlyDictionary<string, int> TakenUpgrades => _taken;

        List<SVUpgrade> _catalog;
        List<SVUpgrade> _awakenCatalog;
        List<SVUpgrade> _choices;

        /// <summary>這一局的隊友，包含已經退場的。HUD 要照原本的順序排。</summary>
        public readonly List<SVCompanion> AllCompanions = new List<SVCompanion>();

        /// <summary>這一局拿到的大覺。</summary>
        public SVAwakenState Awakenings { get; } = new SVAwakenState();

        /// <summary>「公會的祝福」——出發前選的被動，整場生效。</summary>
        public SVPassiveState Passives { get; } = new SVPassiveState();

        /// <summary>還要選幾個祝福。大於 0 的時候整場停住等玩家選。</summary>
        public int BlessingsLeft { get; private set; }

        /// <summary>正在選祝福（HUD 要換標題）。</summary>
        public bool ChoosingBlessing { get; private set; }

        List<SVPassive> _passiveCatalog;

        [Header("地圖補給品")]
        [Tooltip("關掉就不會刷補給品")]
        public bool SpawnPickups = true;

        [Tooltip("每隔幾秒刷一個")]
        public float PickupInterval = 22f;

        [Tooltip("場上最多同時存在幾個")]
        public int MaxPickups = 3;

        [Tooltip("刷在玩家周圍多遠的地方")]
        public float PickupSpawnMin = 6f;
        public float PickupSpawnMax = 14f;

        [Tooltip("補給品的種類與數值。留空會用預設的五種")]
        public List<SVPickupType> Pickups = new List<SVPickupType>();

        /// <summary>攻擊加成剩幾秒（0 = 沒有）。</summary>
        public float AttackBuffLeft { get; private set; }

        /// <summary>攻擊加成的倍率，例如 1.6 就是 +60%。</summary>
        public float AttackBuffMultiplier { get; private set; } = 1f;

        /// <summary>目前生效的補給品加成，HUD 用來顯示。</summary>
        public float DamageBuff => AttackBuffLeft > 0f ? AttackBuffMultiplier : 1f;

        [Header("戰利品")]
        [Tooltip("倒下時只能帶回這個比例的戰利品")]
        [Range(0f, 1f)] public float DefeatKeepRatio = 0.1f;

        // 最終可帶回比例，已包含隊伍分潤，不再乘第二次。
        public float ClearKeepRatio => JoinedOtherTeam ? 0.3f : 1f;
        public float RetreatKeepRatio => JoinedOtherTeam ? 0.2f : 0.8f;

        // ---------------- 卡片給的全域加成 ----------------
        //
        // 這一組跟三選一的強化不同：它們是「對所有技能一起生效」的修正，
        // 所以放在這裡由技能去讀，而不是一張一張技能自己存。

        [Header("卡片：技能全域加成")]
        [Tooltip("所有技能的威力倍率")]
        public float SkillPowerMul = 1f;

        [Tooltip("所有技能的冷卻倍率。越小越快")]
        public float SkillCooldownMul = 1f;

        [Tooltip("所有技能的持續時間倍率")]
        public float SkillDurationMul = 1f;

        [Tooltip("所有技能的「數量」加成：多打幾發、多幾顆、多幾隻")]
        public int SkillCountBonus;

        [Tooltip("技能命中時附加幾層流血")]
        public int SkillBleedStacks;

        [Tooltip("技能命中時附加幾層毒")]
        public int SkillPoisonStacks;

        [Tooltip("技能命中時是否點燃")]
        public bool SkillIgnites;

        [Tooltip("普攻命中時附加幾層流血")]
        public int AttackBleedStacks;

        [Header("持續傷害")]
        [Tooltip("毒、流血、燃燒共用的傷害倍率。卡片與強化會往上加")]
        public float DotMultiplier = 1f;

        [Tooltip("掉落機率的整體倍率")]
        public float LootChanceMultiplier = 1f;

        [Tooltip("金幣數量的整體倍率")]
        public float LootAmountMultiplier = 1f;

        /// <summary>這一局撿到的東西（還沒依結果縮水）。</summary>
        public SVLootBag Loot { get; } = new SVLootBag();

        /// <summary>實際帶回公會的（結算後才有值）。</summary>
        public SVLootBag Carried { get; private set; }

        /// <summary>是不是主動撤退結束的。</summary>
        public bool Retreated { get; private set; }

        /// <summary>這次三選一是不是大覺（HUD 標題要換）。</summary>
        public bool ChoosingAwakening { get; private set; }

        /// <summary>尾王已死，收尾演出接手中。能量累積之類的東西這時候要停下來。</summary>
        public bool EndingStarted => _endingStarted;

        /// <summary>時停之刻生效中。</summary>
        public bool EnemiesFrozen => _freezeLeft > 0f || SVTimeStop.WorldFrozen;

        float _freezeLeft;
        float _bladeAccumulator;
        readonly Dictionary<string, int> _taken = new Dictionary<string, int>();
        int _pendingLevelUps;

        /// <summary>撿到但還沒選的卡片數。</summary>
        int _pendingCards;

        /// <summary>正在選卡片（面板標題會不一樣）。</summary>
        public bool ChoosingCard { get; private set; }

        List<SVUpgrade> _cardCatalog;

        public float Elapsed { get; private set; }
        public int Kills { get; private set; }
        public int Stage => Mathf.FloorToInt(Elapsed / SVConfig.DifficultyStep) + _startStage;
        public float PickupRadius { get; private set; } = SVConfig.PickupRadius;
        public float CompanionPowerScale { get; private set; } = 1f;

        /// <summary>全隊（含男主）傷害加成：「同心」、全部強化拿滿後的通用成長。算在通用傷害裡。</summary>
        public float TeamDamageBonus;

        /// <summary>獨自組隊的多人羈絆（SVBond）。沒有的時候是 SVBond.None，倍率全是 1。</summary>
        public SVBond Bond { get; private set; } = SVBond.None;

        /// <summary>前一晚沒去酒館、好好休息：這趟冒險傷害、血量、道具掉落各 +10%。沒有的時候是 1。</summary>
        public const float RestBonus = 1.1f;
        public float RestMul { get; private set; } = 1f;

        Camera _camera;
        float _targetCameraSize = SVConfig.CameraSize;
        SVHud _hud;
        Transform _unitRoot;
        float _spawnAccumulator;
        float _nextBossTime;

        /// <summary>結尾王。撐過存活時間後登場，打倒牠才算完成委託。</summary>
        public SVEnemy FinalBoss { get; private set; }

        /// <summary>結尾王已經登場，現在是「打王階段」。</summary>
        public bool BossPhase { get; private set; }

        // ---- 王出現之後的倒數 ----
        //
        // 王一出來就開始跑五分鐘。時間到還沒打完就強制撤退：
        // 男主體力不支、女主叫他回去，算「有活著回來」而不是失敗，
        // 戰利品照撤退的比例帶走。目的是不要讓打不動的人卡在場上耗到死。

        [Tooltip("王出現之後還剩多少秒。歸零就強制撤退")]
        public float BossTimeLimit = 300f;

        /// <summary>王出現後剩下的秒數。還沒進王階段是 -1。</summary>
        public float BossTimeLeft { get; private set; } = -1f;

        bool _forcedRetreatStarted;

        void Awake()
        {
            Instance = this;
            if (Companions.Count == 0) Companions = DefaultCompanions();
            if (Enemies.Count == 0) Enemies = SVEnemyTiers.RosterFor(1);
        }

        bool _runStarted;

        void Start()
        {
            // AdoptBriefing() 可能已經先開過一局了，別再開第二次 ——
            // 第二次 ApplyBriefing() 會把 _briefing 洗成 null，結束時就回不了城。
            if (!_runStarted) StartRun();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ---------------- 一局的生命週期 ----------------

        public void StartRun()
        {
            _runStarted = true;

            ClearRun();
            SVDamageLog.Clear();
            ApplyBriefing();

            // 冒險畫質：同時播放的特效上限
            SVEffects.MaxConcurrent = SVQuality.MaxEffects;

            // 獨自組隊的多人羈絆。開場用補給品那條橫幅列出生效的效果
            Bond = SVBond.From(_briefing);
            if (Bond.Active)
            {
                _pickupBanner = Bond.Summary;
                _pickupBannerLeft = 5f;
                _pickupBannerColor = new Color(1f, 0.86f, 0.5f);
                SVDebugLog("羈絆：" + Bond.Summary.Replace("\n", "／"));
            }

            // 前一晚沒去酒館：精神飽滿，開場橫幅多一行
            RestMul = _briefing != null && _briefing.Rested ? RestBonus : 1f;
            if (RestMul != 1f)
            {
                const string rested = "昨晚好好休息：傷害、血量、道具掉落 +10%";
                _pickupBanner = Bond.Active ? _pickupBanner + "\n" + rested : rested;
                _pickupBannerLeft = 5f;
                if (!Bond.Active) _pickupBannerColor = new Color(0.6f, 0.92f, 0.78f);
            }

            Elapsed = 0f;
            Kills = 0;
            IsOver = false;
            _manualPause = false;
            _choices = null;
            _pendingLevelUps = 0;
            _pendingCards = 0;
            ChoosingCard = false;
            _taken.Clear();
            Loot.Clear();
            Carried = null;

            SVPickup.ClearAll();
            if (Pickups == null || Pickups.Count == 0) Pickups = SVPickup.DefaultTypes();
            _nextPickupTime = PickupInterval;
            AttackBuffLeft = 0f;
            AttackBuffMultiplier = 1f;
            Retreated = false;

            _catalog = SVUpgrades.BuildCatalog();
            _awakenCatalog = SVAwakenings.BuildCatalog();
            _cardCatalog = SVCards.BuildCatalog();
            _passiveCatalog = SVPassives.BuildCatalog();
            Passives.Clear();
            ChoosingBlessing = false;
            // 委託等級決定可以選幾個：C 1 個、B 2 個、A 3 個、S 4 個
            ResetHighRank();
            BlessingsLeft = IsTutorial ? 0 : SVPassives.PicksFor(MapRank);
            _tutorialTalking = false; TutorialXpEnabled = false; TutorialHint = "";
            _killHp = _killAtk = _killShield = _killHeal = 0f;
            _regenTimer = _shieldTimer = 0f;
            _goldBoosted = false;
            Awakenings.Clear();
            ChoosingAwakening = false;

            // 武器專屬的強化也是一局一局重來的
            Player?.Affix.Clear();
            _freezeLeft = 0f;
            SVTimeStop.Reset();
            _bladeAccumulator = 0f;
            SVScorchTrail.Clear();
            CompanionPowerScale = 1f;
            TeamDamageBonus = 0f;
            PickupRadius = SVConfig.PickupRadius;
            _spawnAccumulator = 0f;
            _bossTurn = 0;
            _nextBossTime = Mathf.Max(20f, BossInterval);
            FinalBoss = null;
            SkillPowerMul = 1f;
            SkillCooldownMul = 1f;
            SkillDurationMul = 1f;
            SkillCountBonus = 0;
            SkillBleedStacks = 0;
            SkillPoisonStacks = 0;
            SkillIgnites = false;
            AttackBleedStacks = 0;
            DotMultiplier = 1f;

            BossPhase = false;
            BossTimeLeft = -1f;
            _forcedRetreatStarted = false;
            _endingStarted = false;
            RunStage = 1;
            _stage1Boss = null;
            _stage1BossSpawned = false;
            _stageTransitioning = false;
            SVObstacles.Clear();
            EndPhase = SVEndPhase.None;
            Cleared = false;
            _reported = false;

            var watch = System.Diagnostics.Stopwatch.StartNew();

            EnsureCamera();
            long tCamera = watch.ElapsedMilliseconds;

            BuildArena();
            Debug.Log($"[倖存者] 起手耗時：相機 {tCamera} ms、" +
                      $"場地與背景 {watch.ElapsedMilliseconds - tCamera} ms。");

            _unitRoot = new GameObject("Units").transform;
            _unitRoot.SetParent(transform, false);
            ShotRoot = new GameObject("Shots").transform;
            ShotRoot.SetParent(transform, false);
            GemRoot = new GameObject("Gems").transform;
            GemRoot.SetParent(transform, false);

            var playerGo = new GameObject("男主");
            playerGo.transform.SetParent(_unitRoot, false);
            Player = playerGo.AddComponent<SVPlayer>();
            Player.Setup();
            Player.Pos = Vector2.zero;

            // 本篇的戰力換成血量：100 戰力 = 基準血量，開根號讓高戰力不會離譜地肉。
            // 這裡吃的是「男主自己的」戰力（RM 變數 68），不含隊友加成 ——
            // 不然選了戰力高的女主，男主自己也跟著變肉，那不合理。
            if (_briefing != null)
            {
                float ratio = Mathf.Sqrt(Mathf.Max(1f, _briefing.PlayerPower) / 100f);
                Player.MaxHp = Player.Hp =
                    Mathf.Clamp(Mathf.RoundToInt(SVConfig.PlayerMaxHp * ratio), 40, 2000);

                // 裝備是直接加上去的，不進開根號 —— 換裝要有立刻看得到的回饋
                Player.MaxHp += _briefing.EquipHp;
                Player.MaxHp = Mathf.RoundToInt(Player.MaxHp * RestMul);   // 昨晚沒去酒館 +10%
                Player.Hp = Player.MaxHp;

                // 拿劍就吃物攻、普攻算物理；拿法杖就吃魔攻、普攻算魔法。
                // 兩邊的攻擊力都留著，因為技能是各自吃各自那一邊的。
                Player.PhysicalPower = _briefing.EquipAtk;
                Player.MagicPower = _briefing.EquipMAtk;
                Player.AttackKind = _briefing.UsingStaff
                    ? SVDamageKind.Magic : SVDamageKind.Physical;
                Player.AttackDamage += Mathf.RoundToInt(
                    (_briefing.UsingStaff ? _briefing.EquipMAtk : _briefing.EquipAtk) * 0.35f);

                Player.Defense = Mathf.RoundToInt(_briefing.EquipDef * 0.5f);
                Player.MagicDefense = Mathf.RoundToInt(_briefing.EquipMDef * 0.5f);

                // 法杖打出去的不是劍氣，換一張圖（先用劍氣的複製品，之後換掉）
                if (_briefing.UsingStaff) Player.SlashSprite = "player_staff";
            }

            // 羈絆：技能吃的物攻／魔攻、還有防禦，開場直接乘上去
            if (_briefing != null && Bond.Active)
            {
                Player.PhysicalPower = Mathf.RoundToInt(Player.PhysicalPower * Bond.TeamAttackMul);
                Player.MagicPower = Mathf.RoundToInt(Player.MagicPower * Bond.TeamAttackMul);
                Player.Defense = Mathf.RoundToInt(Player.Defense * Bond.DefenseMul);
                Player.MagicDefense = Mathf.RoundToInt(Player.MagicDefense * Bond.DefenseMul);
            }

            // 羈絆的全隊攻擊倍率（不和 DEBUFF、露娜、狼姐妹）要從第一秒就生效。
            // 男主的攻擊力平常要等第一次選強化才經過 ApplyPassives，這裡先跑一次；
            // 沒有 DEBUFF 時不動，維持原本的流程。
            // 昨晚沒去酒館：技能吃的物攻／魔攻 +10%
            if (RestMul != 1f)
            {
                Player.PhysicalPower = Mathf.RoundToInt(Player.PhysicalPower * RestMul);
                Player.MagicPower = Mathf.RoundToInt(Player.MagicPower * RestMul);
            }

            if ((Bond.TeamAttackMul != 1f || RestMul != 1f) && Passives != null) Player.ApplyPassives(Passives);

            PlayRankBgm();

            // 隊友的特效先讀完並預熱。鈴音四個元素、每個還有「逐發」特效，
            // 不先讀的話前四次施法會各頓一下（Resources.Load + 上傳貼圖都擠在同一幀）
            PreloadCompanionEffects();

            AllCompanions.Clear();
            for (int i = 0; i < Companions.Count; i++)
            {
                var go = new GameObject(Companions[i].Name);
                go.transform.SetParent(_unitRoot, false);
                var c = go.AddComponent<SVCompanion>();
                c.Setup(Companions[i], i);
                c.Pos = Vector2.zero;
                AllCompanions.Add(c);
            }

            if (_hud == null)
            {
                var hudGo = new GameObject("HUD");
                hudGo.transform.SetParent(transform, false);
                _hud = hudGo.AddComponent<SVHud>();
            }
            _hud.Rebuild();
            OfferBlessing();
            if(IsTutorial) StartCoroutine(LunaTutorial());

            if (_charPanel == null)
            {
                var panelGo = new GameObject("CharacterPanel");
                panelGo.transform.SetParent(transform, false);
                _charPanel = panelGo.AddComponent<SVCharacterPanel>();
            }

            // 沒有指揮權就連這支都不要建，玩家連選單都叫不出來
            if (PlayerCommands && _commands == null)
            {
                var cmdGo = new GameObject("Commands");
                cmdGo.transform.SetParent(transform, false);
                _commands = cmdGo.AddComponent<SVCommandController>();
            }
            else if (!PlayerCommands && _commands != null)
            {
                Destroy(_commands.gameObject);
                _commands = null;
            }
        }

        SVCommandController _commands;
        SVCharacterPanel _charPanel;

        /// <summary>
        /// 事後補上委託書。
        ///
        /// 場景是用 LoadSceneAsync 疊上來的，SVGame.Start() 什麼時候跑不完全在橋接層的控制內。
        /// 萬一 Start() 比 SVSession.Pending 設好還早，這一局就會變成「沒有委託」的沙盒模式，
        /// 結束時按 R 只會重開一局、回不了城。橋接層載完場景會再確認一次，缺了就用這個補。
        /// </summary>
        public void AdoptBriefing(SVBriefing briefing)
        {
            if (briefing == null || _briefing != null) return;

            SVSession.Pending = briefing;

            if (_runStarted)
            {
                // 已經開局了才發現沒委託書，只好重開一局套用
                Debug.LogWarning("[倖存者] 委託書比場景晚一步到，重新開一局套用。");
                StartRun();
            }
            // 還沒開局的話什麼都不用做，Start() 會取走 Pending
        }

        /// <summary>把本篇填的委託書套進這一局。沒有委託就是沙盒無盡模式。</summary>
        void ApplyBriefing()
        {
            // Pending 是空的就沿用原本那份（重開一局時不該把委託書弄丟）
            var pending = SVSession.Pending;
            SVSession.Pending = null;
            if (pending != null) _briefing = pending;

            if (_briefing == null)
            {
                MapRank = 1;
                if (Enemies == null || Enemies.Count == 0) Enemies = SVEnemyTiers.RosterFor(1);
                PlayerCommands = true;
                DefaultOrder = SVOrder.Free;
                SurviveSeconds = 0f;
                return;
            }

            PlayerCommands = _briefing.PlayerCommands;
            DefaultOrder = SVOrder.Free;
            SurviveSeconds = Mathf.Max(0f, _briefing.SurviveSeconds);

            // 獨自組隊一個都沒勾 = 男主一個人出門，不要退回預設五人
            if (_briefing.Companions != null &&
                (_briefing.Companions.Count > 0 || _briefing.Mode == SVPartyMode.Solo))
                Companions = _briefing.Companions;

            // 地圖等級決定「換一整套敵人」，不只是同一批怪加血量
            MapRank = Mathf.Clamp(_briefing.MapRank, 1, 4);
            Enemies = SVEnemyTiers.RosterFor(MapRank);

            int rank = MapRank - 1;
            BaseSpawnRate = 2.5f * Mathf.Pow(1.35f, rank);
            _startStage = rank;   // 每套敵人自己就已經變強了，階數不用再疊那麼多

            // 高級地圖的王更頻繁，也更早出現
            BossInterval = Mathf.Max(45f, SVConfig.BossInterval - rank * 10f);

            Debug.Log($"[倖存者] {SVEnemyTiers.RankName(MapRank)} 級地圖：" +
                      $"敵人 {Enemies.Count} 種（王 {Enemies.FindAll(e => e.IsBoss).Count} 隻）");
        }

        /// <summary>已經跟鈴音的父親學過「契約之絆」。沒學過就不進大覺三選一。</summary>
        public bool LearnedBond => _briefing != null && _briefing.LearnedBond;

        /// <summary>已經跟鈴音的父親學過「砸瓦魯多」。</summary>
        public bool LearnedTimeStop => _briefing != null && _briefing.LearnedTimeStop;

        /// <summary>技能書的道具編號（Items.json）。</summary>
        public const int BookBondItemId = 201;
        public const int BookTimeStopItemId = 202;

        /// <summary>王身上掉技能書的機率。</summary>
        public const float SkillBookDropChance = 0.5f;

        /// <summary>
        /// 王被打倒時試著掉一本技能書。
        ///
        /// 只掉「還沒學過」的那幾本 —— 學過就沒必要再掉，
        /// 沙盒模式（沒有委託書）不掉，那時本篇根本收不到。
        /// </summary>
        public void TryDropSkillBook(Vector2 pos)
        {
            if(IsTutorial) return;
            if (_briefing == null) return;

            var pool = new List<int>();
            if (!LearnedBond) pool.Add(BookBondItemId);
            if (!LearnedTimeStop) pool.Add(BookTimeStopItemId);
            if (pool.Count == 0) return;

            if (Random.value >= SkillBookDropChance) return;

            int id = pool[Random.Range(0, pool.Count)];
            SVLootDrop.Spawn(GemRoot, pos, SVLootKind.Item, id, 1);
        }

        /// <summary>這一局的地圖等級 1=C 2=B 3=A 4=S。單獨測試時是 1。</summary>
        public int MapRank { get; private set; } = 1;

        [Tooltip("每隔幾秒來一次王。委託書會依地圖等級覆蓋這個值")]
        public float BossInterval = SVConfig.BossInterval;

        [Tooltip("王的血量微調倍率。王的基礎血量已經寫在敵人表裡了，這裡只是整體加減")]
        public float BossHpMultiplier = 1f;

        [Tooltip("王的血量每階成長。\n" +
                 "刻意比雜魚的 Hp Growth 平緩很多 —— 王本來血就厚，" +
                 "再套雜魚那條曲線後期會變成打不動的肉塊")]
        public float BossHpGrowth = 1.08f;

        int _startStage;

        void ClearRun()
        {
            SVProjectile.ClearAll();
            for (int i = SVEnemy.Active.Count - 1; i >= 0; i--)
                if (SVEnemy.Active[i] != null) Destroy(SVEnemy.Active[i].gameObject);
            SVEnemy.Active.Clear();
            for (int i = SVXpGem.Active.Count - 1; i >= 0; i--)
                if (SVXpGem.Active[i] != null) Destroy(SVXpGem.Active[i].gameObject);
            SVXpGem.Active.Clear();

            for (int i = SVLootDrop.Active.Count - 1; i >= 0; i--)
                if (SVLootDrop.Active[i] != null) Destroy(SVLootDrop.Active[i].gameObject);
            SVLootDrop.Active.Clear();

            SVPickup.ClearAll();
            SVCompanion.Active.Clear();

            if (_unitRoot != null) Destroy(_unitRoot.gameObject);
            if (ShotRoot != null) Destroy(ShotRoot.gameObject);
            if (GemRoot != null) Destroy(GemRoot.gameObject);
        }

        void Update()
        {
            if(RMZ.RMZTutorialOverlay.IsOpen) return;
            var kb = Keyboard.current;

            // 視野控制放在所有 return 之前：暫停、選強化、死亡畫面都能調
            UpdateZoom();

            // 傷害 LOG（右上角）：小怪每秒合併一行、F8 開關
            SVDamageLog.Tick(Time.unscaledDeltaTime);

            // 暫停：Esc 暫停；暫停中按鍵盤任意鍵（含 Esc）都能繼續。
            // F8（傷害 LOG 開關）不算，免得想看 LOG 反而把遊戲放開。
            var keysDown = RMZS.GodotPort.InputSystem.PortInput.Down;
            if (RMZ.RMZOptionsScene.IsOpen || Godot.Engine.GetProcessFrames() <= RMZ.RMZOptionsScene.ClosedFrame + 1) { }   // 設置開著：按鍵是設置的，不要順便解除／開啟暫停
            else if (_manualPause)
            {
                foreach (var k in keysDown)
                    if (k != Godot.Key.F8) { _manualPause = false; break; }
            }
            else if (keysDown.Contains(Godot.Key.Escape) && !IsTutorial && !IsOver && !Choosing && !ChoosingBlessing && !ChoosingCard
                     && !RMZ.SVStatsOverlay.IsOpen && !SVDev.PanelOpen)
                _manualPause = true;

            // 暈染在暫停畫面也要繼續退掉，不然一暫停就卡著一片紅
            UpdateHurtFlash(Time.unscaledDeltaTime);

            // 背景大小即時反應 Inspector 的改動（暫停、死亡畫面也照樣可以調）
            if (_background != null && !Mathf.Approximately(BackgroundScale, _appliedBackgroundScale))
                ApplyBackground();

            if (IsOver)
            {
                // 有委託的時候按鍵是「回公會」，沒有委託（單獨測試）才是重來一局
                if (kb != null && (kb.rKey.wasPressedThisFrame ||
                                   kb.enterKey.wasPressedThisFrame ||
                                   kb.numpadEnterKey.wasPressedThisFrame ||
                                   kb.spaceKey.wasPressedThisFrame))
                {
                    if (LaunchedFromStory) ReturnToStory();
                    else StartRun();
                }
                return;
            }

            // 選強化時擋住其他輸入，只收 1/2/3
            if (Choosing)
            {
                if (kb != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Choose(0);
                    else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Choose(1);
                    else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Choose(2);
                }
                return;
            }

            float dt = Time.deltaTime;

            if (kb != null)
            {
                // 大覺：照按鍵設定（預設 R T Y），照拿到的順序排
                if (RMZ.RMZKeyBindings.JustPressed("awaken1")) TriggerAwakening(0);
                else if (RMZ.RMZKeyBindings.JustPressed("awaken2")) TriggerAwakening(1);
                else if (RMZ.RMZKeyBindings.JustPressed("awaken3")) TriggerAwakening(2);

                // 撤退：按住 Backspace 一秒（避免手滑直接離場）
                UpdateRetreatHold(kb, dt);
            }

            if (Paused) return;

            Elapsed += dt;
            if(IsTutorial) { FollowCamera(dt); return; }

            // 時間到 → 結尾王登場（不是直接通關）。
            // 原本這裡直接 Clear()，排在 UpdateSpawning 前面，
            // 存活時間又剛好等於王的間隔，所以王永遠差那一幀生不出來。
            //
            // 兩關制（A／S 級）不看存活時間——玩家要自己把第一關的王打死，
            // 才會換到第二關（結尾王關），流程走 UpdateTwoStageFlow()。
            if (!TwoStageMode)
            {
                if (SurviveSeconds > 0f && !BossPhase && Elapsed >= SurviveSeconds) SummonFinalBoss();
            }
            else if (!_stageTransitioning)
            {
                UpdateTwoStageFlow();
            }

            // 王出現之後的五分鐘倒數。歸零就強制撤退（算成功，只是東西帶得少）
            if (BossPhase && BossTimeLeft > 0f && !_endingStarted && !_forcedRetreatStarted)
            {
                BossTimeLeft -= dt;
                if (BossTimeLeft <= 0f)
                {
                    BossTimeLeft = 0f;
                    _forcedRetreatStarted = true;
                    StartCoroutine(ForcedRetreatSequence());
                    return;
                }
            }

            // 打王階段：王死了先跑收尾流程（對話 → 雜魚逃跑 → 再一次對話），
            // 全部走完才 Clear()。Clear() 一叫下去畫面就跳結算，順序不能反。
            if (BossPhase && !_endingStarted && (FinalBoss == null || FinalBoss.IsDead))
            {
                _endingStarted = true;
                StartCoroutine(EndingSequence());
                return;
            }

            if (_endingStarted)
            {
                // 收尾流程接手中，主迴圈的其他判定先停 ——
                // 但鏡頭一定要繼續跟著玩家，不然雜魚逃跑那段人在跑、畫面卻定住，
                // 追出畫面外的戰利品就等於撿不到了
                FollowCamera(dt);
                return;
            }

            UpdateAwakenings(dt);
            UpdatePassives(dt);
            if (_lootFlash > 0f) _lootFlash -= dt;
            if (_pickupBannerLeft > 0f) _pickupBannerLeft -= dt;

            if (AttackBuffLeft > 0f)
            {
                AttackBuffLeft -= dt;
                if (AttackBuffLeft <= 0f) AttackBuffMultiplier = 1f;
            }
            UpdatePowerAura();

            UpdatePickupSpawning(dt);

            if (!SVTimeStop.WorldFrozen) UpdateSpawning(dt);
            FollowCamera(dt);
        }

        /// <summary>滑鼠滾輪拉近拉遠，中鍵回到預設視野。</summary>
        void UpdateZoom()
        {
            if (_camera == null) return;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    // 滾輪一格在不同平台會回傳 1 或 120，先正規化成「格數」
                    float notches = Mathf.Abs(scroll) >= 20f ? scroll / 120f : scroll;

                    // 往上滾 = 拉近 = 視野變小
                    _targetCameraSize = Mathf.Clamp(
                        _targetCameraSize / Mathf.Pow(SVConfig.CameraZoomStep, notches),
                        SVConfig.CameraSizeMin, SVConfig.CameraSizeMax);
                }

                if (mouse.middleButton.wasPressedThisFrame)
                    _targetCameraSize = SVConfig.CameraSize;
            }

            // 用 1-exp(-k*dt) 而不是 k*dt，掉幀時縮放速度才不會跟著變
            float k = 1f - Mathf.Exp(-SVConfig.CameraZoomSmooth * Time.unscaledDeltaTime);
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _targetCameraSize, k);
        }

        // ---------------- 撤退 ----------------

        [Header("撤退")]
        [Tooltip("「返回公會」要按住幾秒才會生效，避免手滑")]
        public float RetreatHoldSeconds = 1f;

        float _retreatHold;
        bool _retreatButtonDown;

        /// <summary>0~1，HUD 拿去畫進度。</summary>
        public float RetreatProgress =>
            RetreatHoldSeconds <= 0f ? 0f : Mathf.Clamp01(_retreatHold / RetreatHoldSeconds);

        /// <summary>HUD 的按鈕按住／放開時呼叫。</summary>
        public void SetRetreatButton(bool down) => _retreatButtonDown = down;

        void UpdateRetreatHold(Keyboard kb, float dt)
        {
            bool holding = _retreatButtonDown ||
                           (kb != null && kb.backspaceKey.isPressed);

            if (!holding) { _retreatHold = 0f; return; }

            _retreatHold += dt;
            if (_retreatHold < RetreatHoldSeconds) return;

            _retreatHold = 0f;
            _retreatButtonDown = false;
            Retreat();
        }

        void FollowCamera(float dt)
        {
            if (_camera == null || Player == null) return;
            var target = new Vector3(Player.Pos.x, Player.Pos.y, -10f);
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, target, 8f * dt);
        }

        // ---------------- 場地與鏡頭 ----------------

        void EnsureCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _camera = go.AddComponent<Camera>();
            }

            _camera.orthographic = true;
            _camera.orthographicSize = SVConfig.CameraSize;
            _targetCameraSize = SVConfig.CameraSize;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.06f, 0.07f, 0.10f);
            _camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        Transform _arenaRoot;
        SpriteRenderer _background;
        SpriteRenderer _floor;
        SpriteRenderer _edge;
        SpriteRenderer _fog;

        // ---- A／S 級兩關制：長方形地圖＋障礙物，第一關王死了才換第二關（王關）----
        [Header("兩關制（A／S 級限定）")]
        [Tooltip("第一關場地半寬高")] public Vector2 Stage1HalfSize = new Vector2(20f, 13f);
        [Tooltip("開局後幾秒，第一關的王才會出現")] public float Stage1BossDelay = 65f;
        [Tooltip("第一關王的血量，相對第二關（結尾王）打七折左右比較好過")] public float Stage1BossHpScale = 0.7f;

        /// <summary>A／S 兩級才走兩關制，C／B 維持原本單一圓形持續生存。</summary>
        public bool TwoStageMode => MapRank >= 3;

        /// <summary>兩關制目前在第幾關。非兩關制永遠是 1，沒有意義。</summary>
        public int RunStage { get; private set; } = 1;

        SVEnemy _stage1Boss;
        bool _stage1BossSpawned;
        bool _stageTransitioning;

        [Header("音樂")]
        [Tooltip("每個委託等級各一首，順序是 C / B / A / S。" +
                 "檔案放 Assets/Resources/Audio/BGM/，填的是不含副檔名的檔名。" +
                 "留空的話沿用前一個有填的等級")]
        public string[] RankBgm = { "Sun_Drenched_Meadow", "Steel_and_Overgrowth", "", "" };

        [Header("地圖背景")]
        [Tooltip("背景圖放在 Assets/Resources/Survivors/Background/，檔名 C / B / A / S，找不到就用 Default")]
        public string BackgroundFolder = "Survivors/Background";

        [Tooltip("背景圖鋪滿整個場地（關掉的話用原尺寸平鋪）")]
        public bool BackgroundStretch = true;

        [Tooltip("維持圖片長寬比，寧可裁掉邊也不要拉變形")]
        public bool BackgroundKeepAspect = true;

        [Tooltip("背景大小。1 = 剛好蓋滿場地，往下拉會從中心縮小，往上拉會放大")]
        [Range(0.2f, 3f)]
        public float BackgroundScale = 1f;

        float _appliedBackgroundScale = -1f;

        [Tooltip("有背景圖的時候還要不要畫程式生成的格線地板")]
        public bool GridFloorWithBackground = false;

        void BuildArena()
        {
            // 場地形狀：A／S 兩關制是長方形，其他維持原本的圓形——
            // 這個旗標要在量地板／邊界之前設好，SVConfig.ClampToArena 那些呼叫點才會抓對。
            SVConfig.RectArena = TwoStageMode;
            SVConfig.RectHalfSize = Stage1HalfSize;

            if (_arenaRoot == null)
            {
                _arenaRoot = new GameObject("Arena").transform;
                _arenaRoot.SetParent(transform, false);

                // 地板：程式生成的格子貼圖，用 Tiled 模式鋪滿整個場地
                var floorGo = new GameObject("Floor");
                floorGo.transform.SetParent(_arenaRoot, false);

                _floor = floorGo.AddComponent<SpriteRenderer>();
                _floor.sprite = MakeFloorSprite();
                _floor.drawMode = SpriteDrawMode.Tiled;
                _floor.color = new Color(0.30f, 0.34f, 0.30f);
                _floor.sortingOrder = -30000;

                // 邊界
                var edgeGo = new GameObject("Edge");
                edgeGo.transform.SetParent(_arenaRoot, false);
                _edge = edgeGo.AddComponent<SpriteRenderer>();
                _edge.sortingOrder = -29000;

                // 黑霧：邊界外走不過去的地方蓋上黑霧，別讓玩家自己去試撞在哪卡住。
                var fogGo = new GameObject("Fog");
                fogGo.transform.SetParent(_arenaRoot, false);
                _fog = fogGo.AddComponent<SpriteRenderer>();
                // 黑霧畫在最前面（角色、特效之上）：怪物走進黑霧就看不見，不會浮在黑色上面
                _fog.sortingOrder = 29000;
            }

            ApplyBackground();
            ApplyArenaShape();

            // 第一關才擺障礙物；C／B 級跟兩關制的第二關都是空場地。
            // 這裡不管是第一次開局還是按 R 重開，都會重新判斷一次，
            // 免得重開之後障礙物沒補回來（_arenaRoot 沒被銷毀，早退路徑以前會漏掉這段）。
            if (TwoStageMode && RunStage == 1)
                SVObstacles.Spawn(_arenaRoot, Stage1ObstacleLayout());
            else
                SVObstacles.Clear();
        }

        /// <summary>把地板／邊界貼圖套成目前的場地形狀跟大小。圓形跟長方形各自的畫法不一樣。</summary>
        void ApplyArenaShape()
        {
            if (SVConfig.RectArena)
            {
                Vector2 span = SVConfig.RectHalfSize * 2f + Vector2.one * 8f;
                _floor.size = span;

                _edge.sprite = SVShapes.RoundedRing(10, 4);
                _edge.drawMode = SpriteDrawMode.Sliced;
                _edge.size = SVConfig.RectHalfSize * 2f;
                _edge.transform.localScale = Vector3.one;
                _edge.color = new Color(0.55f, 0.45f, 0.30f, 0f);   // 棕色外框不畫了，只留黑霧

                _fog.sprite = SVShapes.FogRect();
                _fog.drawMode = SpriteDrawMode.Simple;
                _fog.transform.localScale = new Vector3(
                    SVConfig.RectHalfSize.x * 2f * SVShapes.FogSpan,
                    SVConfig.RectHalfSize.y * 2f * SVShapes.FogSpan, 1f);
                _fog.color = Color.white;
            }
            else
            {
                _floor.size = Vector2.one * (SVConfig.ArenaRadius * 2f + 8f);

                _edge.sprite = SVShapes.Ring();
                _edge.drawMode = SpriteDrawMode.Simple;
                _edge.transform.localScale = Vector3.one * SVConfig.ArenaRadius * 2f;
                _edge.color = new Color(0.55f, 0.45f, 0.30f, 0f);   // 棕色外框不畫了，只留黑霧

                _fog.sprite = SVShapes.FogCircle();
                _fog.drawMode = SpriteDrawMode.Simple;
                _fog.transform.localScale = Vector3.one * SVConfig.ArenaRadius * 2f * SVShapes.FogSpan;
                _fog.color = Color.white;
            }
        }

        /// <summary>第一關固定的障礙物擺法。位置相對場地中心，刻意留出中央跟四角的活動空間。</summary>
        IEnumerable<(Vector2 pos, float radius)> Stage1ObstacleLayout()
        {
            float hx = Stage1HalfSize.x, hy = Stage1HalfSize.y;
            yield return (new Vector2(-hx * 0.45f, hy * 0.35f), 2.0f);
            yield return (new Vector2(hx * 0.45f, hy * 0.35f), 2.0f);
            yield return (new Vector2(-hx * 0.45f, -hy * 0.35f), 2.0f);
            yield return (new Vector2(hx * 0.45f, -hy * 0.35f), 2.0f);
            yield return (new Vector2(0f, hy * 0.75f), 1.5f);
            yield return (new Vector2(0f, -hy * 0.75f), 1.5f);
        }

#if UNITY_EDITOR
        /// <summary>Inspector 拉動背景大小時即時重算，不用重開一局。</summary>
        void OnValidate()
        {
            if (Application.isPlaying && _background != null) ApplyBackground();
        }
#endif

        /// <summary>
        /// 依這一局的地圖等級換背景圖。
        /// 路徑：Assets/Resources/Survivors/Background/C.png（B / A / S 同理），
        /// 找不到對應等級就退回同資料夾的 Default.png，再找不到就維持原本的格線地板。
        /// </summary>
        void ApplyBackground()
        {
            string rank = SVEnemyTiers.RankName(MapRank);

            // 檔名兩種都認：「C rank map.png」和「C.png」
            var sprite = Resources.Load<Sprite>($"{BackgroundFolder}/Redesign/{rank}")
                ?? Resources.Load<Sprite>($"{BackgroundFolder}/{rank} rank map")
                      ?? Resources.Load<Sprite>($"{BackgroundFolder}/{rank}")
                      ?? Resources.Load<Sprite>($"{BackgroundFolder}/Default");

            if (sprite == null)
            {
                if (_background != null) _background.gameObject.SetActive(false);
                if (_floor != null) _floor.gameObject.SetActive(true);
                return;
            }

            if (_background == null)
            {
                var go = new GameObject("Background");
                go.transform.SetParent(_arenaRoot, false);
                _background = go.AddComponent<SpriteRenderer>();
                _background.sortingOrder = -30500;   // 壓在格線地板(-30000)之下
            }

            _background.gameObject.SetActive(true);
            _background.sprite = sprite;
            _background.color = Color.white;
            _background.transform.localScale = Vector3.one;

            _background.transform.localPosition = Vector3.zero;   // 圖的軸心在正中央，縮放才會往中心收

            _appliedBackgroundScale = BackgroundScale;
            float span = (SVConfig.ArenaRadius * 2f + 8f) * Mathf.Max(0.01f, BackgroundScale);

            if (BackgroundStretch)
            {
                // 蓋滿整個場地。用圖自己的大小換算縮放，換圖不用重調數字。
                _background.drawMode = SpriteDrawMode.Simple;
                var size = sprite.bounds.size;
                if (size.x > 0.001f && size.y > 0.001f)
                {
                    if (BackgroundKeepAspect)
                    {
                        // 取比較大的那個倍率 —— 長寬比不同的圖會被裁掉一點邊，
                        // 但不會變形，也不會露出空白。
                        float k = Mathf.Max(span / size.x, span / size.y);
                        _background.transform.localScale = new Vector3(k, k, 1f);
                    }
                    else
                    {
                        _background.transform.localScale = new Vector3(span / size.x, span / size.y, 1f);
                    }
                }
            }
            else
            {
                // 原尺寸平鋪。圖要在 Inspector 設成 Mesh Type = Full Rect、Wrap Mode = Repeat
                _background.drawMode = SpriteDrawMode.Tiled;
                _background.size = Vector2.one * span;
            }

            if (_floor != null) _floor.gameObject.SetActive(GridFloorWithBackground);
        }

        static Sprite _floorSprite;

        static Sprite MakeFloorSprite()
        {
            if (_floorSprite != null) return _floorSprite;

            const int n = 48;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool line = x == 0 || y == 0;
                px[y * n + x] = line ? new Color(1f, 1f, 1f, 0.16f) : new Color(1f, 1f, 1f, 0.06f);
            }
            tex.SetPixels(px);
            tex.Apply();

            // Tiled 繪製模式一定要 FullRect，用 Tight 會跳警告而且鋪不出來
            _floorSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n,
                                         0, SpriteMeshType.FullRect);
            return _floorSprite;
        }

        // ---------------- 生怪 ----------------

        void UpdateSpawning(float dt)
        {
            UpdateHighRank(dt);
            int stage = Stage;
            float rate = BaseSpawnRate * Mathf.Pow(SpawnGrowth, stage);
            float hpScale = Mathf.Pow(HpGrowth, stage);
            float dmgScale = Mathf.Pow(DamageGrowth, stage);

            // 兩關制的地圖小怪比較少——地圖本身有障礙物擋路，人太多反而互相卡
            if (TwoStageMode) rate *= 0.6f;

            _spawnAccumulator += rate * dt;
            while (_spawnAccumulator >= 1f)
            {
                _spawnAccumulator -= 1f;
                // 怪物上限另外受冒險畫質限制（SVQuality）
                if (SVEnemy.Active.Count < Mathf.Min(MaxEnemies, SVQuality.MaxEnemies)) SpawnOne(PickType(stage), hpScale, dmgScale);
            }

            // 兩關制不生週期王——王的登場改成「第一關王」跟「結尾王」兩隻，
            // 由 UpdateTwoStageFlow() 自己排，畫面不要被額外的週期王搶戲。
            // 打王階段（不管哪種模式）也不再加開週期王，留給結尾王。
            if (!TwoStageMode && !BossPhase && Elapsed >= _nextBossTime)
            {
                _nextBossTime += Mathf.Max(20f, BossInterval);
                var boss = NextBoss();
                if (boss != null)
                    SpawnOne(boss, Mathf.Pow(BossHpGrowth, stage) * BossHpMultiplier, dmgScale);
            }
        }

        // ---------------- 兩關制（A／S 級）----------------

        void UpdateTwoStageFlow()
        {
            if (RunStage != 1) return;

            if (!_stage1BossSpawned && Elapsed >= Stage1BossDelay)
            {
                _stage1BossSpawned = true;
                var type = NextBoss();
                if (type != null)
                {
                    int stage = Stage;
                    float hpScale = Mathf.Pow(BossHpGrowth, stage) * BossHpMultiplier * Stage1BossHpScale;
                    float dmgScale = Mathf.Pow(DamageGrowth, stage);
                    _stage1Boss = SpawnOne(type, hpScale, dmgScale);
                    if (_stage1Boss != null) _stage1Boss.DisplayName = type.Name;
                    SpawnPulse(Player.Pos, 16f, new Color(1f, 0.35f, 0.35f, 0.7f), 1.1f);
                }
            }

            if (_stage1BossSpawned && _stage1Boss != null && _stage1Boss.IsDead)
            {
                _stage1Boss = null;
                StartCoroutine(AdvanceToStage2());
            }
        }

        /// <summary>第一關的王死了：黑屏過場，換成第二關（沒有障礙物的長方形場地），王上。</summary>
        System.Collections.IEnumerator AdvanceToStage2()
        {
            _stageTransitioning = true;
            SVProjectile.ClearAll();

            yield return SVScreenFade.FadeOut(0.5f);

            for (int i = SVEnemy.Active.Count - 1; i >= 0; i--)
            {
                var e = SVEnemy.Active[i];
                if (e != null) Destroy(e.gameObject);
            }

            RunStage = 2;
            ApplyArenaShape();
            SVObstacles.Clear();
            Player.Pos = Vector2.zero;
            _spawnAccumulator = 0f;

            yield return new WaitForSeconds(0.3f);
            yield return SVScreenFade.FadeIn(0.5f);

            _stageTransitioning = false;
            SummonFinalBoss();
        }

        /// <summary>存活時間到，結尾王登場。打倒牠才算完成委託。</summary>
        void SummonFinalBoss()
        {
            var type = NextBoss();
            if (type == null)
            {
                // 這一階根本沒有王可以出，那就照舊當成撐完即通關
                Clear();
                return;
            }

            BossPhase = true;
            BossTimeLeft = BossTimeLimit;

            int stage = Stage;

            // 用王自己的平緩曲線（BossHpGrowth），不是雜魚那條 HpGrowth。
            // 之前這裡是 1.30^階 × 14，第 3 階就把 C 級王撐到五萬多，根本打不動。
            float hpScale = Mathf.Pow(BossHpGrowth, stage) * BossHpScale;
            float dmgScale = Mathf.Pow(DamageGrowth, stage) * BossDamageScale;

            FinalBoss = SpawnOne(type, hpScale, dmgScale);

            Debug.Log($"[倖存者] 結尾王「{type.Name}」：基礎 {type.Hp} × " +
                      $"{BossHpGrowth:0.00}^{stage}({Mathf.Pow(BossHpGrowth, stage):0.00}) × " +
                      $"{BossHpScale:0.0} = {(FinalBoss != null ? FinalBoss.MaxHp : 0)}");
            if (FinalBoss != null)
            {
                FinalBoss.IsFinalBoss = true;
                FinalBoss.DisplayName = type.Name;

                // 從畫面外一點的地方進場，讓玩家看得到牠走過來
                FinalBoss.Pos = Player.Pos + SVConfig.RandomOnCircle(SVConfig.CameraSize * 1.6f);

                // 兩關制的結尾王才掛技能——第一關那隻只是「打死才能過關」的門檻，
                // 真正的招式留給玩家會認真打的這一隻。
                if (TwoStageMode) FinalBoss.gameObject.AddComponent<SVBossAI>().Setup(FinalBoss);
            }

            SpawnPulse(Player.Pos, 16f, new Color(1f, 0.35f, 0.35f, 0.7f), 1.1f);
        }

        [Header("結尾王")]
        [Tooltip("結尾王的血量倍率。乘的是王自己的平緩曲線 Boss Hp Growth，" +
                 "不是雜魚的 Hp Growth。\n" +
                 "大約會是同級定時王的兩倍血")]
        public float BossHpScale = 2f;

        [Tooltip("結尾王的傷害倍率")]
        public float BossDamageScale = 1.3f;

        int _bossTurn;

        /// <summary>王輪流出場，不要每次都同一隻。</summary>
        SVEnemyType NextBoss()
        {
            var bosses = Enemies.FindAll(e => e.IsBoss);
            if (bosses.Count == 0) return null;

            var boss = bosses[_bossTurn % bosses.Count];
            _bossTurn++;
            return boss;
        }

        SVEnemyType PickType(int stage)
        {
            var pool = new List<SVEnemyType>();
            foreach (var e in Enemies)
                if (!e.IsBoss && e.FromStage <= stage) pool.Add(e);

            if (pool.Count == 0) return Enemies.Count > 0 ? Enemies[0] : new SVEnemyType();
            return pool[Random.Range(0, pool.Count)];
        }

        bool _bossArrival;
        System.Collections.IEnumerator AnnounceBoss()
        {
            yield return null;
            while (!IsOver && (_bossArrival || _stageTransitioning || Talking || SVTalkWindow.IsOpen || Choosing || ChoosingBlessing)) yield return null;
            if(IsOver) yield break;
            _bossArrival = true;
            try
            {
                var speakers = new HashSet<string>();
                foreach(var companion in AllCompanions.ToArray())
                {
                    if(companion == null || companion.Retired || !speakers.Add(companion.DisplayName)) continue;
                    string who = companion.DisplayName;
                    var line = SVTalk.RetireLine(who);
                    line.Text = who switch {
                        "鈴音" => SVTalk.Love < 35 ? "……BOSS。" : SVTalk.Love < 70 ? "BOSS。退後。" : "BOSS。別離我太遠。",
                        "尤梨卡" => "BOSS 出現了！一起上吧！",
                        "露娜" => "BOSS 來了，大家小心，我會照顧你們。",
                        "克萊爾" => "BOSS 終於現身了。讓我看看你的本事！",
                        "雪瑤" => "BOSS 出現了……請別離隊伍太遠。",
                        "雪霜" => "BOSS！姐姐，待在我後面！",
                        _ => "BOSS 出現了，小心！"
                    };
                    yield return SVTalkWindow.Say(who, line);
                }
                if(speakers.Count == 0) yield return SVTalkWindow.Say(SVTalk.PlayerName, new SVTalkLine { Text = "BOSS 出現了，得小心應付。" });
            }
            finally { SVTalkWindow.CloseNow(); _bossArrival = false; }
        }

        SVEnemy SpawnOne(SVEnemyType type, float hpScale, float dmgScale)
        {
            var go = new GameObject(type.Name);
            go.transform.SetParent(_unitRoot, false);

            var e = go.AddComponent<SVEnemy>();
            e.Setup(type, hpScale, dmgScale);

            // 從畫面外一圈生出來
            var offset = SVConfig.RandomOnCircle(SVConfig.CameraSize * 1.9f);
            e.Pos = Player.Pos + offset;
            if(type.IsBoss && !IsTutorial) StartCoroutine(AnnounceBoss());
            return e;
        }

        // ---------------- 地圖補給品 ----------------

        float _nextPickupTime;

        void UpdatePickupSpawning(float dt)
        {
            if (!SpawnPickups || Pickups == null || Pickups.Count == 0) return;
            if (Elapsed < _nextPickupTime) return;

            _nextPickupTime = Elapsed + Mathf.Max(3f, PickupInterval);
            if (SVPickup.Active.Count >= MaxPickups) return;

            var type = PickPickupType();
            if (type == null) return;

            // 刷在玩家周圍一圈，太近沒有走過去的過程，太遠又懶得撿
            var offset = SVConfig.RandomOnCircle(Random.Range(PickupSpawnMin, PickupSpawnMax));
            var pos = Player.Pos + offset;

            // 別刷到場地外面，也別刷進障礙物裡
            pos = SVConfig.ClampToArena(pos, 1f);
            pos = SVObstacles.ResolveMove(pos, 0.6f);

            SVPickup.Spawn(GemRoot, pos, type);
        }

        SVPickupType PickPickupType()
        {
            float total = 0f;
            foreach (var t in Pickups) if (t != null) total += Mathf.Max(0f, t.Weight);
            if (total <= 0f) return null;

            float roll = Random.value * total;
            foreach (var t in Pickups)
            {
                if (t == null) continue;
                roll -= Mathf.Max(0f, t.Weight);
                if (roll <= 0f) return t;
            }
            return Pickups[Pickups.Count - 1];
        }

        /// <summary>吃到補給品。</summary>
        public void ApplyPickup(SVPickupType type, Vector2 at)
        {
            if (type == null) return;

            SpawnPulse(at, 3f, new Color(type.Color.r, type.Color.g, type.Color.b, 0.8f), 0.5f);
            _pickupBanner = string.IsNullOrEmpty(type.Effect) ? type.Name : $"{type.Name}　{type.Effect}";
            _pickupBannerLeft = 2.5f;
            _pickupBannerColor = type.Color;

            switch (type.Kind)
            {
                case SVPickupKind.Heal:
                {
                    // 男主與所有還在場的隊友一起回血
                    int amount = Mathf.Max(1, Mathf.RoundToInt(type.Power));
                    Player.Heal(amount);
                    foreach (var c in SVCompanion.Active)
                        if (c != null && !c.Retired) c.Heal(amount);
                    break;
                }

                case SVPickupKind.AttackUp:
                    AttackBuffMultiplier = 1f + Mathf.Max(0f, type.Power) / 100f;
                    AttackBuffLeft = Mathf.Max(AttackBuffLeft, type.Duration);
                    break;

                case SVPickupKind.Shield:
                {
                    int shield = Mathf.Max(1, Mathf.RoundToInt(type.Power));
                    Player.AddShield(shield);
                    foreach (var c in SVCompanion.Active)
                        if (c != null && !c.Retired) c.AddShield(shield);
                    break;
                }

                case SVPickupKind.Invincible:
                {
                    float seconds = Mathf.Max(0.5f, type.Duration);
                    Player.GrantInvincible(seconds);
                    foreach (var c in SVCompanion.Active)
                        if (c != null && !c.Retired) c.GrantInvincible(seconds);
                    break;
                }

                case SVPickupKind.Card:
                    OnCardPicked();
                    break;

                case SVPickupKind.Magnet:
                    // 全場的經驗球與戰利品一起飛過來
                    SVXpGem.PullAll();
                    SVLootDrop.PullAll();
                    SpawnPulse(Player.Pos, 20f, new Color(0.9f, 0.6f, 1f, 0.5f), 0.9f);
                    break;
            }
        }

        // ---- 吃到補給品時畫面中央的提示 ----
        string _pickupBanner;
        float _pickupBannerLeft;
        Color _pickupBannerColor = Color.white;

        public string PickupBanner => _pickupBannerLeft > 0f ? _pickupBanner : null;
        public float PickupBannerLeft => _pickupBannerLeft;
        public Color PickupBannerColor => _pickupBannerColor;

        // ---------------- 事件 ----------------

        /// <summary>撿到戰利品。SVLootDrop 撞到玩家時呼叫。</summary>
        public void CollectLoot(SVLootKind kind, int itemId, int count)
        {
            Loot.Add(kind, itemId, count);
            _lootFlash = 0.6f;
        }

        float _lootFlash;
        /// <summary>剛撿到東西的閃爍強度，HUD 用來提示。</summary>
        public float LootFlash => _lootFlash;

        // 擊殺成長是「每 N 隻 +1」，所以要留小數的餘額
        float _killHp, _killAtk, _killShield, _killHeal;
        float _regenTimer, _shieldTimer;
        bool _goldBoosted;

        void UpdatePassives(float dt)
        {
            if (Player == null) return;

            // 再生
            if (Passives.RegenPercent > 0f)
            {
                _regenTimer += dt;
                while (_regenTimer >= 1f)   // 每秒回一次
                {
                    _regenTimer -= 1f;
                    Player.Heal(Mathf.Max(1, Mathf.RoundToInt(Player.MaxHp * Passives.RegenPercent)));
                }
            }

            // 護盾生成
            if (Passives.ShieldPerTick > 0f)
            {
                _shieldTimer += dt;
                while (_shieldTimer >= 6f)
                {
                    _shieldTimer -= 6f;
                    Player.AddShield(Mathf.RoundToInt(Passives.ShieldPerTick));
                }
            }

            // 冰霜光環
            if (Passives.SlowAura > 0f)
            {
                foreach (var e in SVEnemy.Active)
                {
                    if (e == null || e.IsDead) continue;
                    if (e.Overlaps(Player.Pos, 4.5f)) e.Slow(1f - Passives.SlowAura, 0.4f);
                }
            }
        }

        public void OnEnemyKilled(SVEnemy enemy)
        {
            if(IsTutorial && Kills==0) _tutorialDrop=enemy.Pos;
            Kills++;
            SVXpGem.Spawn(GemRoot, enemy.Pos, enemy.XpValue);

            if (Player == null) return;

            // 血之渴：每擊殺直接回一點血
            if (Passives.HealPerKill > 0f)
            {
                _killHeal += Passives.HealPerKill;
                int heal = Mathf.FloorToInt(_killHeal);
                if (heal > 0) { _killHeal -= heal; Player.Heal(heal); }
            }

            // 隨擊殺成長。用小數累積，滿 1 才真的加，所以「每 20 隻 +1」寫成 0.05
            if (Passives.HpPerKill > 0f)
            {
                _killHp += Passives.HpPerKill;
                int add = Mathf.FloorToInt(_killHp);
                if (add > 0) { _killHp -= add; Player.MaxHp += add; Player.Heal(add); }
            }

            if (Passives.AtkPerKill > 0f)
            {
                _killAtk += Passives.AtkPerKill;
                int add = Mathf.FloorToInt(_killAtk);
                if (add > 0) { _killAtk -= add; Player.AttackDamage += add; }
            }

            if (Passives.ShieldPerKill > 0f)
            {
                _killShield += Passives.ShieldPerKill;
                int add = Mathf.FloorToInt(_killShield);
                if (add > 0) { _killShield -= add; Player.AddShield(add); }
            }

            // 爆裂（祝福版，跟大覺那條各自獨立）
            if (Passives.ExplodePower > 0 && Random.value < Passives.ExplodeChance)
            {
                SpawnPulse(enemy.Pos, 2.2f, new Color(1f, 0.6f, 0.3f, 0.7f), 0.3f);
                SVEnemy.DamageInRadius(enemy.Pos, 2.2f, Passives.ExplodePower, 2f, enemy);
            }
        }

        void UpdateAwakenings(float dt)
        {
            if (_freezeLeft > 0f) _freezeLeft -= dt;

            Awakenings.Tick(dt);
            SVScorchTrail.Tick(dt);   // 火箭彈的焦痕

            // 時停之刻的主動發動期間，敵人一直停著
            // 時停現在由 SVTimeStop 全權處理（EnemiesFrozen 那邊已經接上）

            // 劍雨：在玩家周圍隨機落點
            float rate = Awakenings.BladeRainRate;
            if (rate <= 0f) return;

            _bladeAccumulator += rate * dt;
            while (_bladeAccumulator >= 1f)
            {
                _bladeAccumulator -= 1f;
                float radius = Awakenings.BladeRainRadius;
                var spot = Player.Pos +
                           SVConfig.RandomOnCircle(Random.Range(1.5f, Awakenings.BladeRainSpread));
                SVCombatVisuals.Sword(spot, radius, Awakenings.BladeRainDamage);
            }
        }

        // ---- 力量藥水的光環：腳下一圈紅色光暈一脹一縮，隊友腳下也有 ----
        readonly List<SpriteRenderer> _powerAuras = new List<SpriteRenderer>();

        void UpdatePowerAura()
        {
            bool on = AttackBuffLeft > 0f && Player != null && !IsOver;
            var units = new List<SVUnit>();
            if (on)
            {
                units.Add(Player);
                foreach (var c in SVCompanion.Active) if (c != null && !c.Retired) units.Add(c);
            }
            _powerAuras.RemoveAll(x => x == null);
            while (_powerAuras.Count < units.Count)
            {
                var go = new GameObject("PowerAura");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SVShapes.Soft();
                sr.sortingOrder = -19000;   // 在角色腳下
                _powerAuras.Add(sr);
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
            float fade = AttackBuffLeft < 3f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 18f) : 1f;   // 快結束時閃
            for (int i = 0; i < _powerAuras.Count; i++)
            {
                var sr = _powerAuras[i];
                if (sr == null) continue;
                bool show = i < units.Count;
                sr.enabled = show;
                if (!show) continue;
                sr.transform.position = units[i].Pos + new Vector2(0f, 0.15f);
                float size = (i == 0 ? 2.6f : 2.0f) * (1f + 0.15f * pulse);
                float basis = size / Mathf.Max(0.001f, sr.sprite != null ? sr.sprite.bounds.size.x : 1f);
                sr.transform.localScale = Vector3.one * basis;
                sr.color = new Color(1f, 0.25f + 0.2f * pulse, 0.15f, (0.45f + 0.25f * pulse) * fade);
            }
        }

        /// <summary>按下大覺按鈕。回傳有沒有真的發動。</summary>
        public bool TriggerAwakening(int index)
        {
            if (IsOver || Paused) return false;

            // 苦行：放棄大覺的主動發動，換來其他技能的強化。
            // 被動效果（劍雨的落刃、爆裂連鎖那些）照樣生效，只是按不動
            if (Passives != null && Passives.NoAwakenActive) return false;

            var skills = Awakenings.Skills;
            if (index < 0 || index >= skills.Count) return false;

            var skill = skills[index];
            if (!skill.Ready) return false;
            SVEffects.Play(skill.Kind.ToString(), Player.Pos, 4f, 1f);

            // 砸瓦魯多有自己的一整段演出，時間也自己算，
            // 不走大覺共用的 ActiveLeft / Duration
            if (skill.Kind == SVAwakenKind.TimeStop)
            {
                if (SVTimeStop.Active) return false;
                if (!SVTimeStop.Begin(this)) return false;

                skill.CooldownLeft = SVTimeStop.Cooldown;
                OnAwakeningFired(skill);
                return true;
            }

            skill.ActiveLeft = skill.Duration;
            // 沒有持續時間的（瞬發），以及指定「施放就開始冷卻」的（劍雨），都在這裡就起算
            if (skill.Duration <= 0f || skill.CooldownOnCast) skill.CooldownLeft = skill.Cooldown;

            OnAwakeningFired(skill);
            return true;
        }

        /// <summary>
        /// 收集者的起手斬：朝正面砍一刀並造成流血。
        /// 真正的價值在後面那五秒的處決視窗（見 SVEnemy.TryExecute）。
        /// </summary>
        void FireCollectorSlash()
        {
            if (Player == null || Player.IsDead) return;

            var aim = PlayerAim();
            float width = 3.2f;
            int damage = 55 + Awakenings.Stacks(SVAwakenKind.Collector) * 30;

            var hit = new HashSet<SVEnemy>();
            for (int i = 1; i <= 6; i++)
            {
                Vector2 at = Player.Pos + aim * (9f * i / 6f);
                foreach (var e in SVEnemy.Active.ToArray())
                {
                    if (e == null || e.IsDead || hit.Contains(e)) continue;
                    if (!e.Overlaps(at, width)) continue;

                    hit.Add(e);
                    e.TakeDamage(damage, SVDamageKind.Physical);
                    e.Bleed(3);   // 收集者的斬擊會流血
                }
                SpawnPulse(at, width * 0.9f, new Color(1f, 0.5f, 0.55f, 0.5f), 0.28f);
            }
        }

        /// <summary>
        /// 毒爆：把每一隻身上的毒層一次燒掉換成傷害，並濺到旁邊。
        /// 引爆之後毒層歸零 —— 這是「收割」，不是「加成」。
        /// </summary>
        void DetonatePoison()
        {
            int perStack = Awakenings.PoisonBurstPerStack;

            foreach (var e in new List<SVEnemy>(SVEnemy.Active))
            {
                if (e == null || e.IsDead) continue;

                int stacks = e.ConsumePoison();
                if (stacks <= 0) continue;

                int dmg = Mathf.Max(1, Mathf.RoundToInt(stacks * perStack * DotMultiplier));
                var at = e.Pos;

                e.TakeDamage(dmg, SVDamageKind.True);

                // 濺射：旁邊的吃一半，但不會再引爆一次（毒已經被清掉了）
                SVEnemy.DamageInRadius(at, 2.6f, Mathf.Max(1, dmg / 2), 3f, e, 1f, SVDamageKind.True);
                SpawnPulse(at, 2.6f, new Color(0.5f, 0.95f, 0.4f, 0.7f), 0.35f);
            }
        }

        /// <summary>男主現在面對的方向。最近的敵人優先，其次移動方向，都沒有就朝右。</summary>
        Vector2 PlayerAim()
        {
            var target = SVEnemy.NearestTo(Player.Pos, 40f);
            Vector2 aim;
            if (target != null) aim = (Vector2)target.Pos - Player.Pos;
            else if (Player.Velocity.sqrMagnitude > 0.01f) aim = Player.Velocity;
            else aim = Vector2.right;

            return aim.sqrMagnitude < 0.0001f ? Vector2.right : aim.normalized;
        }

        /// <summary>
        /// 劍氣：朝正面轟出一道又寬又長的斬擊。
        /// 判定是沿著中軸線一段一段取圓，等於一條膠囊形的範圍，
        /// 站在斜後方的不會被掃到，但正面整條路徑上的都跑不掉。
        /// </summary>
        void FireBladeWave()
        {
            if (Player == null || Player.IsDead) return;

            var aim = PlayerAim();
            float width = Awakenings.BladeWidth;
            float length = 16f;
            int damage = Awakenings.BladeDamage;

            var hit = new HashSet<SVEnemy>();
            const int steps = 10;
            for (int i = 1; i <= steps; i++)
            {
                Vector2 at = Player.Pos + aim * (length * i / steps);
                foreach (var e in SVEnemy.Active.ToArray())
                {
                    if (e == null || e.IsDead || hit.Contains(e)) continue;
                    if (!e.Overlaps(at, width)) continue;
                    hit.Add(e);
                    e.TakeDamage(damage, SVDamageKind.Physical);
                    e.Push(aim * 7f);
                }
            }

            // 演出：沿著中軸放幾圈，看起來像一道掃出去的白光
            for (int i = 1; i <= 5; i++)
            {
                Vector2 at = Player.Pos + aim * (length * i / 5f);
                SpawnPulse(at, width * 1.15f, new Color(0.85f, 0.95f, 1f, 0.55f), 0.3f);
            }
        }

        /// <summary>
        /// 法杖專屬的星隕：一顆一顆砸在敵人頭上。
        /// 跟鈴音那招的差別就是半徑小一點，而且完全不碰自己人 ——
        /// 這是「法杖玩家的獎勵」，不該還要一邊閃自己的火。
        /// </summary>
        System.Collections.IEnumerator StaffMeteorShower()
        {
            int count = Awakenings.StaffMeteorCount;
            int damage = Awakenings.StaffMeteorDamage;
            float radius = Awakenings.StaffMeteorRadius;

            for (int i = 0; i < count; i++)
            {
                if (Player == null || Player.IsDead || IsOver) yield break;

                var target = SVEnemy.NearestTo(Player.Pos, 22f);
                Vector2 spot = target != null
                    ? (Vector2)target.Pos + Random.insideUnitCircle * 0.8f
                    : Player.Pos + Random.insideUnitCircle * 8f;

                // 預兆圈：讓人看得出來下一顆要砸哪
                SpawnPulse(spot, radius, new Color(1f, 0.6f, 0.3f, 0.35f), 0.25f);
                yield return new WaitForSeconds(0.12f);

                if (IsOver) yield break;

                SVEnemy.DamageInRadius(spot, radius, damage, 4f, null, 1f, SVDamageKind.Magic);
                SpawnPulse(spot, radius * 1.2f, new Color(1f, 0.75f, 0.35f, 0.75f), 0.3f);

                yield return new WaitForSeconds(0.1f);
            }
        }

        /// <summary>
        /// 火箭彈：以男主的正面為準，橫向排開一整排火箭同時射出去，每一發都會炸。
        ///
        /// 瞄準的方向依序找：最近的敵人 → 目前的移動方向 → 右邊。
        /// 一排是「平行前進」而不是扇形 —— 扇形會在遠處散掉，
        /// 平行的話整排能一起打進敵群裡，也比較像齊射。
        /// </summary>
        void FireRocketBarrage(int stacks)
        {
            if (Player == null || Player.IsDead) return;

            stacks = Mathf.Max(1, stacks);

            // ---- 瞄準方向 ----
            Vector2 aim;
            var target = SVEnemy.NearestTo(Player.Pos, 40f);
            if (target != null) aim = (Vector2)target.Pos - Player.Pos;
            else if (Player.Velocity.sqrMagnitude > 0.01f) aim = Player.Velocity;
            else aim = Vector2.right;

            if (aim.sqrMagnitude < 0.0001f) aim = Vector2.right;
            aim = aim.normalized;

            var side = new Vector2(-aim.y, aim.x);   // 跟瞄準方向垂直，用來把一排排開

            int count = Awakenings.RocketCount;
            int damage = Awakenings.RocketDamage;
            float splash = Awakenings.RocketSplashRadius;

            const float spacing = 0.8f;
            float half = (count - 1) * 0.5f;

            var warm = new Color(1f, 0.62f, 0.25f, 1f);

            for (int i = 0; i < count; i++)
            {
                float offset = (i - half) * spacing;

                // 越靠邊的往後站一點，看起來像一排陸續衝出去，而不是一條死板的直線
                float back = Mathf.Abs(i - half) * 0.18f;
                Vector2 pos = Player.Pos + side * offset - aim * back;

                var rocket = SVProjectile.Spawn(ShotRoot, pos, aim, warm, 0.42f,
                                                SVShapes.Square(), true);
                if (rocket == null) continue;

                rocket.Damage = damage;
                rocket.DamageKind = SVDamageKind.Physical;
                rocket.Speed = 15f;
                rocket.Life = 2.4f;
                rocket.Radius = 0.34f;
                rocket.Knockback = 4f;

                // 爆炸：直接命中之外，周圍一圈也吃七成
                rocket.SplashRadius = splash;
                rocket.SplashDamage = Mathf.Max(1, Mathf.RoundToInt(damage * 0.7f));

                // 焦痕：沿路每 0.1 秒留一個，每 0.5 秒燒一次火箭傷害的 12%（真傷），留 3.5 秒
                rocket.ScorchInterval = 0.1f;
                rocket.ScorchDamage = Mathf.Max(1, Mathf.RoundToInt(damage * 0.12f));
            }

            SpawnPulse(Player.Pos, 3.2f, new Color(1f, 0.75f, 0.35f, 0.6f), 0.28f);
        }

        /// <summary>發動當下的一次性效果。持續型的效果由各自的查詢屬性處理。</summary>
        void OnAwakeningFired(SVAwakenSkill skill)
        {
            var color = new Color(1f, 0.6f, 0.9f, 0.8f);
            SpawnPulse(Player.Pos, 7f, color, 0.7f);

            switch (skill.Kind)
            {
                case SVAwakenKind.Detonate:
                {
                    // 原本是「不管多遠、全場每隻都吃一發」，這樣一放等於清全圖，太強。
                    // 改成只炸男主周圍一圈，跟劍雨、震盪波這些主動技能一樣是有範圍的招式。
                    int stacks = Awakenings.Stacks(SVAwakenKind.Detonate);
                    float radius = 8f + stacks * 1.2f;
                    int power = 28 + stacks * 16;
                    foreach (var e in new List<SVEnemy>(SVEnemy.Active))
                    {
                        if (e == null || e.IsDead) continue;
                        if (!e.Overlaps(Player.Pos, radius)) continue;
                        SpawnPulse(e.Pos, 1.8f, new Color(1f, 0.7f, 0.4f, 0.7f), 0.3f);
                        e.TakeDamage(power);
                    }
                    SpawnPulse(Player.Pos, radius, new Color(1f, 0.5f, 0.3f, 0.35f), 0.4f);
                    break;
                }

                case SVAwakenKind.Rocket:
                    FireRocketBarrage(Awakenings.Stacks(SVAwakenKind.Rocket));
                    break;

                case SVAwakenKind.Blade:
                    FireBladeWave();
                    break;

                case SVAwakenKind.Collector:
                    FireCollectorSlash();
                    break;

                case SVAwakenKind.PoisonBurst:
                    DetonatePoison();
                    break;

                case SVAwakenKind.StaffMeteor:
                    StartCoroutine(StaffMeteorShower());
                    break;

                case SVAwakenKind.Adrenaline:
                    Player.Heal(Mathf.RoundToInt(Player.MaxHp * 0.25f));
                    SpawnPulse(Player.Pos, 3.5f, new Color(1f, 0.35f, 0.35f, 0.6f), 0.4f);
                    break;

                case SVAwakenKind.Overdrive:
                    SpawnPulse(Player.Pos, 4f, new Color(1f, 0.9f, 0.4f, 0.55f), 0.35f);
                    break;

                case SVAwakenKind.Bond:
                    // 全隊回滿，倒下的也拉回來
                    foreach (var c in new List<SVCompanion>(SVCompanion.Active))
                        if (c != null) c.Heal(c.MaxHp);
                    Player.Heal(Mathf.RoundToInt(Player.MaxHp * 0.4f));
                    break;

                case SVAwakenKind.TimeStop:
                    SpawnPulse(Player.Pos, 16f, new Color(0.7f, 0.85f, 1f, 0.6f), 0.8f);
                    break;
            }
        }

        public void GainAwakening(SVAwakenKind kind)
        {
            Awakenings.Gain(kind);

            // 契約之絆：已經退場的也叫回來
            if (kind == SVAwakenKind.Bond) RecallCompanions();

            SpawnPulse(Player.Pos, 6f, new Color(1f, 0.55f, 0.85f, 0.8f), 0.8f);
        }

        void RecallCompanions()
        {
            foreach (var c in AllCompanions) if (c != null && c.Retired) c.Revive();
            _hud?.Rebuild();
        }

        public void OnCompanionRetired(SVCompanion companion)
        {
            _hud?.Rebuild();

            // 倒下的人講一句話再退場。不擋操作，戰鬥照跑
            if (companion?.Type != null) SVRetireLine.Show(companion.Type.Name, companion.Type.SheetName, companion.Type.SheetIndex);
        }

        /// <summary>對敵人造成傷害之後。血之渴在這裡結算。</summary>
        public void OnDamageDealtToEnemy(int dealt) => OnDamageDealtToEnemy(dealt, SVHitSource.Other);

        /// <summary>吸血。比例看傷害來源（SVLifesteal）：普攻吸血只吃普攻，全能吸血吃普攻＋技能＋毒血。</summary>
        public void OnDamageDealtToEnemy(int dealt, SVHitSource src)
        {
            float steal = SVLifesteal.For(this, src);
            if (steal <= 0f || Player == null) return;

            _lifestealPool += dealt * steal;
            if (_lifestealPool < 1f) return;

            int heal = Mathf.FloorToInt(_lifestealPool);
            _lifestealPool -= heal;
            int healed = Player.Heal(heal);

            // 鮮血盈溢：滿血時溢出來的吸血轉成護盾（有上限，最多最大生命的一半）
            if (Passives.OverhealShield > 0f && healed < heal)
            {
                _overhealPool += (heal - healed) * Passives.OverhealShield;
                int cap = Mathf.RoundToInt(Player.MaxHp * 0.5f);
                if (_overhealPool >= 1f && Player.Shield < cap)
                {
                    int add = Mathf.Min(Mathf.FloorToInt(_overhealPool), cap - Player.Shield);
                    _overhealPool -= Mathf.FloorToInt(_overhealPool);
                    if (add > 0) Player.AddShield(add);
                }
            }
        }
        float _overhealPool;

        float _lifestealPool;

        [Header("卡片掉落")]
        [Tooltip("一般敵人死亡時掉卡片的機率")]
        [Range(0f, 1f)] public float CardDropChance = 0.012f;

        [Tooltip("王死亡時掉卡片的機率")]
        [Range(0f, 1f)] public float BossCardDropChance = 1f;

        /// <summary>敵人死亡時試著掉一張卡片。</summary>
        public void TryDropCard(Vector2 pos, bool boss)
        {
            if(IsTutorial) return;
            float chance = boss ? BossCardDropChance : CardDropChance;
            if (Random.value >= chance * LootChanceMultiplier) return;

            SVPickup.Spawn(GemRoot, pos, new SVPickupType
            {
                Kind = SVPickupKind.Card,
                Name = "你獲得額外的補給",
                Color = new Color(0.85f, 0.7f, 1f),
            });
        }

        /// <summary>撿到一張卡片。沒有正在選東西的話就馬上開三選一。</summary>
        public void OnCardPicked()
        {
            _pendingCards++;
            SpawnPulse(Player.Pos, 3f, new Color(0.85f, 0.7f, 1f, 0.7f), 0.5f);
            if (!Choosing) OfferChoices();
        }

        public void OnLevelUp()
        {
            SpawnPulse(Player.Pos, 3f, new Color(1f, 0.9f, 0.4f, 0.6f), 0.5f);

            _pendingLevelUps++;
            if (IsTutorial) return; // 教學先講解，再顯示選項，避免兩個視窗重疊。
            if (!Choosing) OfferChoices();
        }

        public void AddPickupRadius(float amount) => PickupRadius += amount;

        /// <summary>抽三個祝福出來讓玩家選。額度用完就直接開打。</summary>
        void OfferBlessing()
        {
            if (BlessingsLeft <= 0) { ChoosingBlessing = false; return; }

            var picked = SVPassives.Draw(_passiveCatalog, SVPassives.OfferCount, Passives);
            if (picked.Count == 0) { BlessingsLeft = 0; ChoosingBlessing = false; return; }

            // 借用同一套三選一的面板，只是換一份資料與標題
            _choices = new List<SVUpgrade>();
            foreach (var passive in picked)
            {
                var p = passive;
                _choices.Add(new SVUpgrade
                {
                    Id = "blessing_" + p.Id,
                    Name = p.Name,
                    Desc = p.Desc,
                    Tint = p.Tint,
                    Apply = g => g.TakeBlessing(p),
                });
            }

            ChoosingBlessing = true;
        }

        /// <summary>選了一個祝福。</summary>
        public void TakeBlessing(SVPassive passive)
        {
            Passives.Gain(passive);

            // 上方橫幅告訴玩家拿到了什麼祝福、效果是什麼。
            // 開場常常連選好幾個（選的時候橫幅先藏著、不會倒數），一行一個累加，選完一次看到全部。
            string line = "獲得祝福：" + StripTags(passive.Name) +
                          (string.IsNullOrWhiteSpace(passive.Desc) ? "" : "　" + StripTags(passive.Desc));
            bool stacking = _pickupBannerLeft > 0f && _pickupBanner != null && _pickupBanner.StartsWith("獲得祝福");
            _pickupBanner = stacking ? _pickupBanner + "\n" + line : line;
            _pickupBannerLeft = 5f;
            _pickupBannerColor = new Color(0.6f, 0.92f, 0.78f);
            BlessingsLeft = Mathf.Max(0, BlessingsLeft - 1);
            ApplyPassivesToRun();

            if (passive.Id == "head_start") GrantRandomPassiveUpgrades(2);
        }

        /// <summary>
        /// 直接送 count 個隨機的被動強化（升級三選一裡「不是主動技能、不是大覺」的那些），
        /// 照抽選權重、跳過已經拿滿或條件不符的。拿到什麼一起寫在上方橫幅。
        /// </summary>
        public void GrantRandomPassiveUpgrades(int count)
        {
            if (_catalog == null || Player == null) return;
            for (int n = 0; n < count; n++)
            {
                var pool = new List<SVUpgrade>();
                float total = 0f;
                foreach (var u in _catalog)
                {
                    if (u.ActiveSkill || u.Category == SVUpgradeCategory.Awaken) continue;
                    if (Stacks(u.Id) >= u.MaxStacks) continue;
                    if (u.Available != null && !u.Available(this)) continue;
                    pool.Add(u); total += u.Weight;
                }
                if (pool.Count == 0) return;

                float roll = Random.value * total;
                var pick = pool[pool.Count - 1];
                foreach (var u in pool) { roll -= u.Weight; if (roll <= 0f) { pick = u; break; } }

                pick.Apply?.Invoke(this);
                _taken[pick.Id] = Stacks(pick.Id) + 1;
                string line = "獲得強化：" + StripTags(pick.Name) +
                              (string.IsNullOrWhiteSpace(pick.Desc) ? "" : "　" + StripTags(pick.Desc).Replace("\n", "　"));
                _pickupBanner = string.IsNullOrEmpty(_pickupBanner) ? line : _pickupBanner + "\n" + line;
                _pickupBannerLeft = 5f;
            }
            _hud?.Rebuild();
        }

        /// <summary>
        /// 把祝福裡「一次性」的修正套到場上。
        /// 每次選完都重跑一次，所以高等級選好幾個也會正確疊加。
        /// </summary>
        void ApplyPassivesToRun()
        {
            if (Player == null) return;

            Player.ApplyPassives(Passives);
            PickupRadius = SVConfig.PickupRadius * (1f + Passives.PickupBonus);
            CompanionPowerScale = Passives.CompanionPowerMul;
            LootChanceMultiplier = Passives.LootMul;

            foreach (var c in AllCompanions)
            {
                if (c == null) continue;
                c.CooldownMul = Passives.CompanionCooldownMul;
                c.ApplyPassives(Passives);
            }
        }

        /// <summary>抽三個還能拿的強化出來給玩家選。</summary>
        void OfferChoices()
        {
            if(IsTutorial) {
                _choices = _catalog.FindAll(u => u.Id == "pskill_Whirlwind" || u.Id == "pskill_Shockwave" || u.Id == "pskill_Chain");
                return;
            }
            // 卡片優先 —— 它是剛撿到的東西，玩家正等著看
            if (_pendingCards > 0)
            {
                ChoosingCard = true;
                if (OfferFrom(_cardCatalog)) return;
                ChoosingCard = false;
                _pendingCards = 0;
            }

            if (_pendingLevelUps <= 0) return;

            // 每 5 級來一次大覺。抽不出東西（都拿滿了）就退回一般強化
            // 苦行：整局都不給大覺（每 5 級那次改成一般強化）
            ChoosingAwakening = Player != null && Player.Level % 5 == 0
                                && !(Passives != null && Passives.NoAwakenActive);
            if (ChoosingAwakening && OfferFrom(_awakenCatalog)) return;
            ChoosingAwakening = false;
            if (OfferFrom(_catalog)) return;

            // 全部拿滿了就不用選，直接給一點通用成長
            _pendingLevelUps = 0;
            TeamDamageBonus += 0.1f;   // 全隊傷害 +10%（算在通用傷害）
        }

        bool OfferFrom(List<SVUpgrade> catalog)
        {
            var pool = new List<SVUpgrade>();
            float totalWeight = 0f;
            foreach (var u in catalog)
            {
                if (Stacks(u.Id) >= u.MaxStacks) continue;
                if (u.Available != null && !u.Available(this)) continue;
                pool.Add(u);
                totalWeight += u.Weight;
            }

            if (pool.Count == 0) return false;

            var picked = new List<SVUpgrade>();
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                float roll = Random.value * totalWeight;
                int index = pool.Count - 1;
                for (int j = 0; j < pool.Count; j++)
                {
                    roll -= pool[j].Weight;
                    if (roll <= 0f) { index = j; break; }
                }

                picked.Add(pool[index]);
                totalWeight -= pool[index].Weight;
                pool.RemoveAt(index);
            }

            _choices = picked;
            return true;
        }

        public void Choose(int index)
        {
            if (_choices == null || index < 0 || index >= _choices.Count) return;

            var u = _choices[index];
            u.Apply?.Invoke(this);
            _taken[u.Id] = Stacks(u.Id) + 1;

            _choices = null;

            // 選祝福不算升級，別去動 _pendingLevelUps
            if (ChoosingBlessing)
            {
                ChoosingBlessing = false;
                _hud?.Rebuild();
                OfferBlessing();      // 還有額度就再抽一輪
                return;
            }

            // 選卡片也不算升級 —— 卡片是撿來的，跟能量條沒關係
            if (ChoosingCard)
            {
                ChoosingCard = false;
                _pendingCards--;
                _hud?.Rebuild();

                // 撿卡片選完之後，上方橫幅告訴玩家拿到了什麼（名稱＋效果）
                _pickupBanner = "獲得卡片：" + StripTags(u.Name) +
                                (string.IsNullOrWhiteSpace(u.Desc) ? "" : "\n" + StripTags(u.Desc));
                _pickupBannerLeft = 4f;
                _pickupBannerColor = new Color(0.85f, 0.7f, 1f);

                SpawnPulse(Player.Pos, 3.5f, new Color(0.85f, 0.7f, 1f, 0.7f), 0.4f);

                // 還有卡片沒選、或本來就有升級待處理，接著問下一個
                if (_pendingCards > 0 || _pendingLevelUps > 0) OfferChoices();
                return;
            }

            ChoosingAwakening = false;
            _pendingLevelUps--;
            _hud?.Rebuild();   // 學到新技能／大覺，下方按鈕要跟著長出來

            SpawnPulse(Player.Pos, 3.5f, new Color(1f, 0.85f, 0.35f, 0.7f), 0.4f);

            // 一次升好幾級的話，接著問下一個
            if (_pendingLevelUps > 0) OfferChoices();
        }

        public int Stacks(string id) => _taken.TryGetValue(id, out var n) ? n : 0;

        static readonly System.Text.RegularExpressions.Regex RichTags =
            new System.Text.RegularExpressions.Regex("<[^>]+>");

        /// <summary>拿掉 &lt;size&gt;、&lt;color&gt; 這類標籤，橫幅只要純文字。</summary>
        static string StripTags(string s) => string.IsNullOrEmpty(s) ? "" : RichTags.Replace(s, "").Replace("\n", " ").Trim();

        /// <summary>
        /// 這一局拿過的強化與卡片，給右上角的能力面板用。
        /// 祝福、主動技能、大覺醒各有自己的清單（Passives.Chosen／Player.Skills／Awakenings.Skills），這裡跳過。
        /// </summary>
        public List<(SVUpgrade Upgrade, int Stacks, bool IsCard)> TakenUpgradeList()
        {
            var list = new List<(SVUpgrade, int, bool)>();
            foreach (var kv in _taken)
            {
                if (kv.Key.StartsWith("blessing_") || kv.Key.StartsWith("awaken_")) continue;
                var u = _catalog?.Find(x => x.Id == kv.Key);
                bool card = false;
                if (u == null) { u = _cardCatalog?.Find(x => x.Id == kv.Key); card = u != null; }
                if (u == null || u.ActiveSkill) continue;
                list.Add((u, kv.Value, card));
            }
            return list;
        }

        // ---------------- 打完王的收尾流程 ----------------

        public enum SVEndPhase
        {
            None,
            BossTalk,   // 王倒下，第一段對話
            Fleeing,    // 雜魚四散奔逃，玩家可以追殺
            FinalTalk,  // 都跑光了，第二段對話 + 互動
        }

        public SVEndPhase EndPhase { get; private set; } = SVEndPhase.None;
        bool _endingStarted;

        [Header("打完王的收尾")]
        [Tooltip("雜魚最多逃這麼久。超過就當作都跑掉了，免得卡在追不到的一隻身上")]
        public float FleeTimeout = 25f;

        /// <summary>逃跑階段還剩幾秒。HUD 拿去顯示。</summary>
        public float FleeLeft { get; private set; }

        /// <summary>收尾流程進行中：不生怪、不判定通關，主迴圈讓路。</summary>
        public bool InEnding => EndPhase != SVEndPhase.None;

        int HeroineId => _briefing != null ? _briefing.Heroine : 0;

        System.Collections.IEnumerator EndingSequence()
        {
            string speaker = SVTalk.NameOf(HeroineId);

            // ① 王倒下的當下
            EndPhase = SVEndPhase.BossTalk;
            SVProjectile.ClearAll();
            yield return SVTalkWindow.Play(speaker, SVTalk.Lines(HeroineId, "boss_down"));

            // ② 全場作鳥獸散。玩家可以繼續追殺，殺掉照樣掉東西
            EndPhase = SVEndPhase.Fleeing;
            SVEnemy.FleeAll();
            FleeLeft = FleeTimeout;

            while (FleeLeft > 0f && SVEnemy.Active.Count > 0)
            {
                if (IsOver) yield break;
                FleeLeft -= Time.deltaTime;
                yield return null;
            }

            // 逾時的話剩下的直接當跑掉了
            for (int i = SVEnemy.Active.Count - 1; i >= 0; i--)
            {
                var e = SVEnemy.Active[i];
                if (e != null) Destroy(e.gameObject);
            }
            SVEnemy.Active.Clear();

            // 地上沒撿的東西像磁鐵一樣全部飛向男主（讓玩家看到都撿到了），最多等 2.5 秒
            if (SVLootDrop.Active.Count > 0 || SVXpGem.Active.Count > 0)
            {
                SVXpGem.PullAll();          // 結束後能量不會再加，只是一起飛過來比較整齊
                SVLootDrop.PullAll();
                SpawnPulse(Player.Pos, 20f, new Color(1f, 0.85f, 0.4f, 0.55f), 0.9f);
                float magnetLeft = 2.5f;
                while (magnetLeft > 0f && (SVLootDrop.Active.Count > 0 || SVXpGem.Active.Count > 0))
                {
                    if (IsOver) yield break;
                    magnetLeft -= Time.deltaTime;
                    yield return null;
                }
            }
            SVLootDrop.CollectAllRemaining();

            // ③ 收工對話 + 一次互動
            EndPhase = SVEndPhase.FinalTalk;
            if (HeroineId > 0 && !IsTutorial)
            {
                SVTalkWindow.CloseNow();
                yield return RMZ.RMZBattleEvents.Play(HeroineId, true);
            }
            else yield return SVTalkWindow.Play(speaker, SVTalk.Lines(HeroineId, "cleared"));

            SVTalkWindow.CloseNow();
            EndPhase = SVEndPhase.None;

            Clear();
        }

        /// <summary>結尾的互動：送禮／聊聊。禮物清單跟實際發放都由本篇處理。</summary>
        System.Collections.IEnumerator Interact(string speaker)
        {
            var menu = SVTalk.InteractMenu();

            while (true)
            {
                int picked = -1;
                yield return SVTalkWindow.Ask(menu, r => picked = r);

                // 最後一項固定是「結束」
                if (picked < 0 || picked >= menu.Count - 1) yield break;

                if (picked == 0)
                {
                    yield return GiveGift(speaker);
                    continue;
                }

                yield return SVTalkWindow.Say(speaker, SVTalk.InteractLine("chat", HeroineId));
            }
        }

        System.Collections.IEnumerator GiveGift(string speaker)
        {
            if (!SVTalkHooks.HasGifts)
            {
                yield return SVTalkWindow.Say(speaker, SVTalk.InteractLine("no_gift"));
                yield break;
            }

            var gifts = SVTalkHooks.ListGifts();
            if (gifts == null || gifts.Count == 0)
            {
                yield return SVTalkWindow.Say(speaker, SVTalk.InteractLine("no_gift"));
                yield break;
            }

            var labels = new List<string>();
            foreach (var g in gifts) labels.Add(g.label);
            labels.Add("算了");

            int picked = -1;
            yield return SVTalkWindow.Ask(labels, r => picked = r);
            if (picked < 0 || picked >= gifts.Count) yield break;

            string message = SVTalkHooks.GiveGift(gifts[picked].id);
            yield return SVTalkWindow.Say(speaker, new SVTalkLine { Face = "", Text = message });
        }

        public void GameOver()
        {
            if(IsTutorial) { if(Player != null) Player.Hp = Player.MaxHp; return; }
            IsOver = true;
            Cleared = false;
            Retreated = false;
            _choices = null;
            SettleLoot(DefeatKeepRatio);
        }

        /// <summary>
        /// 王打太久，時間到了的強制撤退。
        ///
        /// 這不是失敗 —— 人是自己走回公會的，所以算完成，只是戰利品比過關少。
        /// 先讓男主說體力不支，同行的女主接一句，然後才收場。
        /// </summary>
        System.Collections.IEnumerator ForcedRetreatSequence()
        {
            // Paused 是唯讀的計算屬性，要停下來就得走 EndPhase ——
            // 收尾對話那兩段也是這樣暫停的，這裡沿用同一套
            EndPhase = SVEndPhase.FinalTalk;

            yield return SVTalkWindow.Say("男主", new SVTalkLine
            {
                Text = "（撐不住了……體力不支，再打下去會死在這裡。）",
            });

            if (HeroineId > 0)
            {
                string speaker = SVTalk.NameOf(HeroineId);
                yield return SVTalkWindow.Say(speaker, new SVTalkLine
                {
                    Text = HeroineId == 5 ? (SVTalk.Love < 35 ? "……撤。" : "抓緊。回去。") : "太晚了……先回去。命比較重要。",
                });
            }

            EndPhase = SVEndPhase.None;

            IsOver = true;
            Cleared = true;      // 算完成 —— 人有活著回到公會
            Retreated = true;    // 但帶回的東西照撤退算
            _choices = null;

            SVLootDrop.CollectAllRemaining();
            SettleLoot(RetreatKeepRatio);

            SpawnPulse(Player.Pos, 8f, new Color(0.7f, 0.85f, 1f, 0.8f), 0.9f);
        }

        /// <summary>主動撤退：活著走出來，戰利品照 RetreatKeepRatio 帶走。</summary>
        public void Retreat()
        {
            if (IsOver) return;
            if(IsTutorial) { CompleteTutorial(); return; }

            IsOver = true;
            Cleared = false;
            Retreated = true;
            _choices = null;

            // 走都走了，地上還沒撿的順手收走
            SVLootDrop.CollectAllRemaining();
            SettleLoot(RetreatKeepRatio);

            SpawnPulse(Player.Pos, 6f, new Color(0.6f, 0.9f, 1f, 0.8f), 0.8f);
        }

        public bool JoinedOtherTeam => _briefing?.Mode == SVPartyMode.JoinOther;

        /// <summary>
        /// 依委託等級放對應的曲子。該等級留空的話往前找最近一個有填的，
        /// 這樣只填 C 一首也能整個跑起來。
        /// </summary>
        void PlayRankBgm()
        {
            if (RankBgm == null || RankBgm.Length == 0) return;

            int index = Mathf.Clamp(MapRank, 1, 4) - 1;
            string name = "";

            for (int i = Mathf.Min(index, RankBgm.Length - 1); i >= 0; i--)
                if (!string.IsNullOrEmpty(RankBgm[i])) { name = RankBgm[i]; break; }

            if (string.IsNullOrEmpty(name)) return;

            SVMusic.Play(name);
            SVDebugLog($"{SVEnemyTiers.RankName(MapRank)} 級地圖音樂：{name}");
        }

        static void SVDebugLog(string message) => Debug.Log("[倖存者] " + message);

        /// <summary>把這趟會用到的特效先讀進來，避免第一次施法時卡一下。</summary>
        void PreloadCompanionEffects()
        {
            var names = new List<string>();

            foreach (var type in Companions)
            {
                if (type?.Elements == null) continue;
                foreach (var element in type.Elements)
                {
                    if (element == null) continue;
                    if (!string.IsNullOrEmpty(element.Effect)) names.Add(element.Effect);
                    if (!string.IsNullOrEmpty(element.StrikeEffect)) names.Add(element.StrikeEffect);
                }
            }

            if (names.Count > 0) SVEffects.Preload(names);
        }

        void SettleLoot(float ratio)
        {
            // 掠奪者：帶回的金幣加成。結算可能被呼叫不只一次，所以只乘一次
            if (!_goldBoosted && Passives.GoldMul != 1f)
            {
                _goldBoosted = true;
                Loot.Gold = Mathf.RoundToInt(Loot.Gold * Passives.GoldMul);
            }

            Carried = Loot.Scaled(Mathf.Clamp01(ratio));
        }

        /// <summary>撐過委託時間，完成。</summary>
        public void Clear()
        {
            IsOver = true;
            Cleared = true;
            Retreated = false;
            _choices = null;

            SVLootDrop.CollectAllRemaining();
            SettleLoot(ClearKeepRatio);   // 完成委託：帶回 ClearKeepRatio

            SpawnPulse(Player.Pos, 8f, new Color(1f, 0.9f, 0.5f, 0.8f), 0.9f);
        }

        /// <summary>把結果交回本篇。只會送一次。</summary>
        public void ReturnToStory()
        {
            if (_reported || _briefing == null) return;
            _reported = true;

            if (Carried == null)
                SettleLoot(Retreated ? RetreatKeepRatio : Cleared ? ClearKeepRatio : DefeatKeepRatio);

            var result = new SVResult
            {
                Cleared = Cleared,
                Retreated = Retreated,
                Survived = Elapsed,
                Kills = Kills,
                Level = Player != null ? Player.Level : 1,
                Loot = Carried,
            };

            SVSession.Last = result;
            var callback = _briefing.OnFinished;
            _briefing = null;
            callback?.Invoke(result);
        }

        /// <summary>
        /// 放一個停在原地的圈，當「這裡等一下會爆」的預兆。
        /// 呼叫端負責在時間到的時候 Destroy 它。
        /// </summary>
        /// <summary>落點的預兆圈。給了魔法陣的檔名就畫魔法陣，否則畫原本的色圈。</summary>
        public GameObject SpawnMarker(Vector2 pos, float radius, Color color, string image = null)
        {
            var go = new GameObject("Marker");
            go.transform.SetParent(ShotRoot, false);
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 18000;

            var circle = string.IsNullOrEmpty(image) ? null : SVArt.MagicCircle(image);

            if (circle != null)
            {
                sr.sprite = circle;

                // 魔法陣是張長方形的圖，圓盤只佔中間、直徑大約等於圖高。
                // 所以要用圖自己的世界高度去換算，縮放才會剛好蓋住爆炸範圍 ——
                // 直接乘 radius*2 的話會被 PPU 與圖片尺寸影響，大小整個跑掉。
                float h = Mathf.Max(0.001f, circle.bounds.size.y);
                go.transform.localScale = Vector3.one * (radius * 2f / h);

                // 魔法陣本身就有顏色，不要再染色；固定半透明
                sr.color = new Color(1f, 1f, 1f, MarkerImageAlpha);
                go.AddComponent<SVMarkerPulse>().AlphaRange = 0.2f;
            }
            else
            {
                sr.sprite = SVShapes.Ring();
                sr.color = color;
                go.transform.localScale = Vector3.one * radius * 2f;
                go.AddComponent<SVMarkerPulse>();
            }

            return go;
        }

        [Tooltip("落點魔法陣的透明度")]
        [Range(0f, 1f)] public float MarkerImageAlpha = 0.5f;

        /// <summary>放一個會擴散淡出的圓，當技能特效。</summary>
        public void SpawnPulse(Vector2 pos, float radius, Color color, float duration)
        {
            // 冒險畫質低時不畫小型光圈（純裝飾）；王、大招那種大光圈照畫
            if (radius < 3f && !SVQuality.MinorPulses) return;

            var go = new GameObject("Pulse");
            go.transform.SetParent(ShotRoot, false);
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SVShapes.Ring();
            sr.color = color;
            sr.sortingOrder = 19000;

            var fx = go.AddComponent<SVPulse>();
            fx.Radius = radius;
            fx.Duration = duration;
        }

        // ---------------- 預設資料 ----------------

        /// <summary>沙盒模式（直接按 Play）用的預設陣容：五位女主全上。</summary>
        public static List<SVCompanionType> DefaultCompanions() => new List<SVCompanionType>
        {
            SVParties.Suzune(),
            SVParties.Luna(),
            SVParties.Yurika(),
            SVParties.Claire(),
            SVParties.Xueyao(),
            SVParties.Xueshuang(),
        };

        // 掉落用的 ID 對照 RM 資料庫：
        //   武器 2/3/4/5 是樺木長劍的粗製→精良→上等→大師
        //   防具 2/6/10/14 是胸甲／護腿／頭盔／護靴（粗製），+1 +2 +3 就是更高階
        //   道具 2/6/10/14 是開場事件會發的那幾樣消耗品
        static SVLootEntry Drop(SVLootKind kind, int id, float chance, int min = 1, int max = 1) =>
            new SVLootEntry { Kind = kind, ItemId = id, Chance = chance, Min = min, Max = max };

        /// <summary>
        /// 舊的預設敵人表。現在敵人是照地圖等級分四套（見 SVEnemyTiers），
        /// 這裡直接轉呼叫 C 級那一套，免得兩份表各改各的而分岔。
        /// </summary>
        public static List<SVEnemyType> DefaultEnemies() => SVEnemyTiers.RosterFor(1);
    }

    /// <summary>預兆圈的呼吸效果，讓人看得出來「這裡要爆了」。</summary>
    public class SVMarkerPulse : MonoBehaviour
    {
        /// <summary>
        /// 呼吸時透明度上下擺動的幅度。
        /// 純色圈用 0.5（明顯地閃）；魔法陣本來就有圖案，
        /// 擺太大會一直忽明忽暗很吵，所以只留一點。
        /// </summary>
        public float AlphaRange = 0.5f;

        SpriteRenderer _renderer;
        Color _base;
        Vector3 _scale;
        float _t;

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _base = _renderer.color;
            _scale = transform.localScale;
        }

        void Update()
        {
            _t += Time.deltaTime * 6f;
            float k = 0.5f + 0.5f * Mathf.Sin(_t);
            transform.localScale = _scale * Mathf.Lerp(0.92f, 1f, k);
            float lo = Mathf.Clamp01(1f - AlphaRange);
            _renderer.color = new Color(_base.r, _base.g, _base.b, _base.a * Mathf.Lerp(lo, 1f, k));
        }
    }

    /// <summary>技能特效用的擴散圓。</summary>
    public class SVPulse : MonoBehaviour
    {
        public float Radius = 2f;
        public float Duration = 0.3f;

        SpriteRenderer _renderer;
        Color _base;
        float _t;

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _base = _renderer.color;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / Duration);
            transform.localScale = Vector3.one * Mathf.Lerp(Radius * 0.4f, Radius * 2.1f, k);
            _renderer.color = new Color(_base.r, _base.g, _base.b, _base.a * (1f - k));
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
