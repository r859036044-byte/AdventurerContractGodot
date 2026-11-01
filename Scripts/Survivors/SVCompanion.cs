// 隊友（五位女主）。跟著男主跑，各自照自己的節奏放技能。
//
// 技能種類寫成 enum + 一張資料表，之後要加人或改數值都在 SVGame 那邊改，
// 不用動這支的邏輯。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public enum SVSkillKind
    {
        Burst,   // 鈴音：周身大範圍爆發斬，傷害最高
        Heal,    // 露娜：定期回復全隊
        Homing,  // 尤梨卡：連續追蹤彈
        Frost,   // 克萊爾：冰結領域，減速兼持續傷害
        Orbit,   // 雪瑤雪霜：雙子旋轉刃
        Melee,   // 凱、塔克、護衛：貼身斬，附帶擊退
        Arrow,   // 露娜隊裡的射手：單發直線箭，能穿一隻
        Arcane,  // 鈴音（大魔法師）：遠距鎖定敵群，落下大範圍高傷法術
        Storm,   // 雪瑤：連鎖雷電與冰風暴輪流放
        Bash,    // 雪霜：盾衝擊與劍刃輪流放
    }

    /// <summary>
    /// 大魔法的一種元素。鈴音會照清單輪流放，每一發的特效、傷害、附加效果都不同。
    /// 特效名字對應 Assets/Resources/RMZEffects 底下的 .efkefc（就是 RM 用的那批）。
    /// </summary>
    [System.Serializable]
    public class SVArcaneElement
    {
        public string Name = "星辰墜落";

        [Tooltip("落點的特效檔名（RM 的 Effekseer 特效）")]
        public string Effect = "LightAll2";

        [Tooltip("多重打擊時，每一發的特效。留空就用上面那個")]
        public string StrikeEffect = "";

        [Tooltip("特效的縮放倍率。RM 的『全體』動畫本來就鋪滿畫面，" +
                 "調小可以省下大量的半透明疊繪")]
        [Range(0.2f, 2f)] public float EffectScale = 1f;

        [Tooltip("施法落點要畫哪張魔法陣（Resources/Survivors/Effect/ 底下的檔名）。" +
                 "留空 = 用原本的色圈")]
        public string MarkerImage = "";

        [Tooltip("一次打幾個不同的落點。1 = 單一大範圍")]
        public int Strikes = 1;

        [Tooltip("傷害倍率")]
        public float DamageMul = 1f;

        [Tooltip("爆炸半徑倍率")]
        public float RadiusMul = 1f;

        [Tooltip("命中的敵人凍結幾秒。0 = 不凍結")]
        public float FreezeSeconds = 0f;

        [Tooltip("命中後的減速倍率（0.4 = 剩四成速度）。1 = 不減速")]
        [Range(0.05f, 1f)] public float SlowFactor = 1f;

        [Tooltip("減速持續幾秒")]
        public float SlowSeconds = 0f;

        [Tooltip("擊退力道")]
        public float Knockback = 5f;

        [Header("灼燒地帶（0 = 沒有）")]
        [Tooltip("落點留下的火燒幾秒")]
        public float BurnSeconds = 0f;

        [Tooltip("火圈半徑相對爆炸半徑的倍率")]
        public float BurnRadiusMul = 0.9f;

        [Tooltip("每 0.5 秒對敵人的傷害，是本體傷害的幾成")]
        public float BurnDamageMul = 0.18f;

        [Tooltip("每 0.5 秒對男主的傷害。刻意寫死成小數字，站在火裡只是痛不是致命")]
        public int BurnPlayerDamage = 2;

        public Color BurnColor = new Color(1f, 0.45f, 0.15f, 0.30f);

        public Color WarnColor = new Color(0.75f, 0.55f, 1f, 0.55f);
        public Color PulseColor = new Color(0.8f, 0.55f, 1f, 0.75f);
    }

    [System.Serializable]
    public class SVCompanionType
    {
        public string Name = "隊友";
        public string SheetName = "小人1";
        public int SheetIndex = 0;
        public SVSkillKind Skill = SVSkillKind.Homing;
        /// <summary>血量。歸零就退出戰場。</summary>
        public int Hp = 60;

        /// <summary>
        /// 固定減傷。敵人的接觸傷害先扣掉這個才進血量，
        /// 所以低階地圖的怪對高手根本破不了防 —— 這比「防禦力百分比」直覺得多。
        /// </summary>
        public int Defense = 0;

        /// <summary>只對王生效的傷害倍率。專門拆王的角色調這個。</summary>
        public float BossDamageMultiplier = 1f;

        [Header("定期給男主的護盾（0 = 不給）")]
        public int PlayerShieldPower = 0;
        public float PlayerShieldInterval = 8f;
        public int Power = 5;
        public float Cooldown = 2f;
        public float Range = 6f;
        public Color Tint = Color.white;
        public string SkillName = "";
        public string SkillDesc = "";

        [Header("劍氣外觀")]
        [Tooltip("Resources/Survivors/Effect/ 底下的檔名。留空 = 不用劍氣，維持原本的光球／光圈")]
        public string SlashSprite = "";

        [Tooltip("劍氣的染色。白色 = 用圖片原本的顏色")]
        public Color SlashColor = Color.white;

        [Tooltip("劍氣大小")]
        public float SlashSize = 1f;

        [Header("護盾")]
        [Tooltip("只有王打得到她。一般雜魚的接觸傷害完全無效")]
        public bool ShieldBossOnly = false;

        [Tooltip("擋下傷害時要不要畫護盾光圈")]
        public bool ShowShield = true;

        [Header("大魔法（Arcane）專用")]
        [Tooltip("詠唱幾秒之後才落下。這段時間地上會有預兆圈")]
        public float CastTime = 0.7f;

        [Tooltip("法術落點的爆炸半徑")]
        public float BlastRadius = 4.5f;

        [Tooltip("元素輪替。每放一次換下一個，留空就只用預設的星辰墜落")]
        public List<SVArcaneElement> Elements = new List<SVArcaneElement>();

        [Header("雙子專用（留空就是單人）")]
        public string PartnerSheetName = "";
        public int PartnerSheetIndex = 0;

        /// <summary>
        /// 身邊帶著兩把繞圈的刀（雪瑤之力）。
        /// 以前這是綁在 Skill == Orbit 上的，但雪瑤現在還要能放別的招，
        /// 所以拆成獨立旗標：刀是常駐的，Skill 那格留給真正要冷卻的技能。
        /// </summary>
        public bool OrbitBlades;

        /// <summary>繞圈刀的單次傷害。0 = 沿用 Power。</summary>
        public int BladePower;
    }

    public class SVCompanion : SVUnit
    {
        public static readonly List<SVCompanion> Active = new List<SVCompanion>();

        public SVCompanionType Type;
        public int Slot;

        // 這幾個是給「強化」改的，一局結束就跟著隊友一起重建
        public float PowerMul = 1f;
        public float CooldownMul = 1f;
        public float RangeMul = 1f;
        public int ExtraShots = 0;

        public int Power => Mathf.Max(1, Mathf.RoundToInt(
            Type.Power * PowerMul * (SVGame.Instance != null
                ? SVGame.Instance.CompanionPowerScale * SVGame.Instance.Bond.CompanionMul(Type) * SVGame.Instance.RestMul   // 羈絆＋昨晚休息
                : 1f)));
        public float Cooldown => Mathf.Max(0.15f, Type.Cooldown * CooldownMul *
            (SVGame.Instance != null ? SVGame.Instance.Awakenings.CompanionCooldownScale : 1f));
        public float Range => Type.Range * RangeMul;

        /// <summary>祝福給的額外減傷。</summary>
        public int BonusDefense { get; private set; }

        /// <summary>套用「公會的祝福」裡跟隊友有關的部分。</summary>
        public void ApplyPassives(SVPassiveState passives)
        {
            if (passives == null || Type == null) return;

            BonusDefense = passives.CompanionDefense;

            float rest = SVGame.Instance != null ? SVGame.Instance.RestMul : 1f;
            int newMax = Mathf.Max(1, Mathf.RoundToInt(Type.Hp * passives.CompanionHpMul * rest));
            int delta = newMax - MaxHp;
            MaxHp = newMax;
            if (delta > 0) Hp = Mathf.Min(MaxHp, Hp + delta);
            Hp = Mathf.Min(Hp, MaxHp);
        }

        /// <summary>對王的傷害倍率。</summary>
        public float BossBonus => Type != null ? Type.BossDamageMultiplier : 1f;

        public float CooldownLeft { get; private set; }

        /// <summary>上一次是空揮（附近沒有目標）。藍條靠它決定要不要顯示滿的。</summary>
        bool _fizzled;

        /// <summary>真的放出技能時，讓角色亮一下。</summary>
        void Flash()
        {
            if (Renderer == null) return;
            StopCoroutine(nameof(FlashRoutine));
            StartCoroutine(nameof(FlashRoutine));
        }

        System.Collections.IEnumerator FlashRoutine()
        {
            if (Renderer == null) yield break;

            Renderer.color = new Color(1f, 1f, 0.75f);
            yield return new WaitForSeconds(0.12f);
            if (Renderer != null) Renderer.color = Color.white;
        }
        public float CooldownNormalized => Type == null || Cooldown <= 0f
            ? 0f : Mathf.Clamp01(1f - CooldownLeft / Cooldown);

        // ---- 指揮（自由移動／定點待命／跟隨玩家）----
        public SVOrder Order { get; private set; } = SVOrder.Follow;
        public Vector2 HoldPoint { get; private set; }
        public bool Dragging { get; private set; }

        Vector2 _dragTarget;
        Transform _holdRing;
        SVUnitBars _bars;
        Transform _badge;

        /// <summary>倒下退場了。物件還在，只是不參戰也不顯示。</summary>
        public bool Retired { get; private set; }

        Transform _shield;
        float _shieldFlash;
        bool _shieldAlwaysOn;

        // ---- 補給品給的護盾與無敵。跟鈴音的「魔導護盾」是兩回事 ----

        /// <summary>吸收傷害的護盾值。先扣這個再扣血。</summary>
        public int Shield { get; private set; }
        public int ShieldMax { get; private set; }

        /// <summary>短時間無敵，剩幾秒。</summary>
        public float Invincible { get; private set; }

        public void AddShield(int amount)
        {
            if (amount <= 0 || Retired) return;
            // 羈絆：露娜在場，護盾量加成
            if (SVGame.Instance != null && SVGame.Instance.Bond.ShieldMul != 1f)
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * SVGame.Instance.Bond.ShieldMul));
            Shield += amount;
            ShieldMax = Mathf.Max(ShieldMax, Shield);
            FlashShield();
        }

        public void GrantInvincible(float seconds)
        {
            if (Retired) return;
            Invincible = Mathf.Max(Invincible, seconds);
            FlashShield();
        }

        /// <summary>有沒有魔導護盾（只有王打得到）。</summary>
        public bool HasShield => Type != null && Type.ShieldBossOnly;

        /// <summary>這個敵人打不打得動她。</summary>
        public bool CanBeHurtBy(SVEnemy enemy)
        {
            // 契約之絆發動期間全隊無敵
            var game = SVGame.Instance;
            if (game != null && game.Awakenings.CompanionsInvincible) return false;

            if (!HasShield) return true;
            return enemy != null && enemy.Type != null && enemy.Type.IsBoss;
        }

        /// <summary>被擋下來的時候閃一下護盾，讓玩家看得出「這下沒進去」。</summary>
        public void FlashShield() => _shieldFlash = 0.25f;

        SVUnit _partner;
        readonly List<Transform> _blades = new List<Transform>();
        Transform _aura;
        float _bladeAngle;
        float _tickTimer;
        float _shieldTimer;

        protected override void Awake()
        {
            base.Awake();
            Active.Add(this);
        }

        void OnDestroy() => Active.Remove(this);

        /// <summary>
        /// 受傷的處理順序：無敵 → 固定減傷 → 護盾 → 血量。跟男主那邊一致。
        ///
        /// 無敵有兩個來源，都在最前面擋掉：
        ///   ・補給品「無敵之星」給的 Invincible
        ///   ・大覺「契約之絆」發動期間的全隊無敵
        /// </summary>
        public override int TakeDamage(int amount, SVDamageKind kind = SVDamageKind.Physical)
        {
            if (amount <= 0) return 0;
            if (Invincible > 0f) return 0;

            var game = SVGame.Instance;
            if (game != null && game.Awakenings.CompanionsInvincible) return 0;

            // 隊友的固定減傷
            // 羈絆：狼姐妹在場，防禦加成
            float defMul = game != null ? game.Bond.DefenseMul : 1f;
            amount -= Mathf.Max(0, Mathf.RoundToInt(((Type != null ? Type.Defense : 0) + BonusDefense) * defMul));
            if (amount <= 0) return 0;

            // 護盾先擋。擋掉的不算受傷
            if (Shield > 0)
            {
                int absorbed = Mathf.Min(Shield, amount);
                Shield -= absorbed;
                amount -= absorbed;

                FlashShield();
                if (amount <= 0) return 0;
            }

            return base.TakeDamage(amount, kind);
        }

        protected override void OnDeath()
        {
            var game = SVGame.Instance;

            // C 級委託：隊友（女主、班底、僱傭兵）都不會倒下，血量留 1。
            // 重傷不退場，打完就好 —— C 級不該讓人丟隊友。
            if (game != null && game.MapRank <= 1)
            {
                Hp = 1;
                return;
            }

            // 契約之絆：拿了就不會再有人倒下
            if (game != null && game.Awakenings.CompanionsImmortal)
            {
                Hp = MaxHp;
                game.SpawnPulse(Pos, 2f, new Color(1f, 0.75f, 0.9f, 0.7f), 0.4f);
                return;
            }

            Retire();
        }

        /// <summary>退出戰場：留著物件但不參戰，一局結束前不會再回來。</summary>
        public void Retire()
        {
            if (Retired) return;
            Retired = true;
            Hp = 0;
            Velocity = Vector2.zero;

            SVGame.Instance?.SpawnPulse(Pos, 1.8f, new Color(0.5f, 0.5f, 0.55f, 0.6f), 0.5f);
            SVGame.Instance?.OnCompanionRetired(this);

            Active.Remove(this);
            if (_badge != null) _badge.gameObject.SetActive(false);
            gameObject.SetActive(false);
            if (_partner != null) _partner.gameObject.SetActive(false);
            if (_holdRing != null) _holdRing.gameObject.SetActive(false);
        }

        /// <summary>
        /// 敵人打隊友走這裡。
        ///
        /// 兩道關卡：
        ///   1. 魔導護盾（ShieldBossOnly）—— 雜魚完全打不動，只有王能造成傷害。
        ///   2. 固定減傷（Defense）—— 扣完不足 1 就是 0，連受擊閃爍都不播，
        ///      「破不了防」的感覺要看得出來。
        /// 所以低階地圖的王對鈴音是完全無傷的，高階才開始痛。
        /// </summary>
        public int TakeDamageFrom(int amount, SVEnemy source)
        {
            bool fromBoss = source != null && source.Type != null && source.Type.IsBoss;
            if (Type != null && Type.ShieldBossOnly && !fromBoss) return 0;
            return TakeDamage(amount);
        }

        /// <summary>頭頂的圓形頭像，讓玩家一眼認得出場上誰是誰。</summary>
        void CreateBadge()
        {
            var ringColor = SVPortrait.RingColorFor(Type.Name);
            var sprite = SVPortrait.Get(Type.SheetName, Type.SheetIndex, ringColor);
            if (sprite == null) return;

            var go = new GameObject("Badge");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            go.transform.localScale = Vector3.one * 0.42f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 26500;   // 血魔條(26000/26001) 之上
            _badge = go.transform;
        }

        /// <summary>契約之絆專用：把退場的人叫回來。</summary>
        public void Revive()
        {
            if (!Retired) return;

            Retired = false;
            Hp = MaxHp;
            gameObject.SetActive(true);
            if (_badge != null) _badge.gameObject.SetActive(true);
            if (_partner != null) _partner.gameObject.SetActive(true);
            if (!Active.Contains(this)) Active.Add(this);

            var game = SVGame.Instance;
            if (game != null && game.Player != null) Pos = game.Player.Pos;
        }

        public static SVCompanion Find(SVSkillKind kind)
        {
            foreach (var c in Active) if (c != null && c.Type != null && c.Type.Skill == kind) return c;
            return null;
        }

        public int BladeCount => _blades.Count;

        public void Setup(SVCompanionType type, int slot)
        {
            Type = type;
            Slot = slot;
            DisplayName = type.Name;
            SheetName = type.SheetName;
            SheetIndex = type.SheetIndex;
            MoveSpeed = SVConfig.PlayerSpeed * SVConfig.CompanionSpeed;
            MaxHp = Hp = Mathf.Max(1, Mathf.RoundToInt(type.Hp * (SVGame.Instance != null ? SVGame.Instance.RestMul : 1f)));
            Radius = 0.35f;
            CooldownLeft = Cooldown * 0.5f;
            _fizzled = false;
            _bars = SVUnitBars.Attach(this, true);
            CreateBadge();

            Order = SVGame.Instance != null ? SVGame.Instance.DefaultOrder : SVOrder.Follow;
            HoldPoint = Pos;

            // 光圈一律建立：沒有魔導護盾的人平常隱形，拿到補給品的護盾／無敵才會亮
            CreateShield();
            _shieldAlwaysOn = type.ShieldBossOnly && type.ShowShield;

            Shield = 0;
            ShieldMax = 0;
            Invincible = 0f;
            if (!string.IsNullOrEmpty(type.PartnerSheetName)) CreatePartner();
            if (type.OrbitBlades || type.Skill == SVSkillKind.Orbit) SetBladeCount(2);
            if (type.Skill == SVSkillKind.Frost) CreateAura();
        }

        void CreateShield()
        {
            var go = new GameObject("Shield");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            go.transform.localScale = Vector3.one * 1.5f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SVShapes.Ring();
            sr.color = new Color(0.65f, 0.8f, 1f, 0.35f);
            sr.sortingOrder = 14000;

            _shield = go.transform;
        }

        void UpdateShield(float dt)
        {
            if (_shield == null) return;

            // 只有「常駐的魔導護盾」或「補給品給的護盾／無敵」才顯示光圈
            bool show = _shieldAlwaysOn || Shield > 0 || Invincible > 0f || _shieldFlash > 0f;
            if (_shield.gameObject.activeSelf != show) _shield.gameObject.SetActive(show);
            if (!show) return;

            _shield.Rotate(0f, 0f, 40f * dt);

            var sr = _shield.GetComponent<SpriteRenderer>();
            if (sr == null) return;

            // 補給品的無敵期間常亮金色，一眼看得出來現在打不掉
            if (Invincible > 0f)
            {
                sr.color = new Color(1f, 0.92f, 0.55f, 0.7f);
                _shield.localScale = Vector3.one * 1.65f;
                return;
            }

            if (_shieldFlash > 0f)
            {
                _shieldFlash -= dt;
                float k = Mathf.Clamp01(_shieldFlash / 0.25f);
                sr.color = new Color(0.95f, 0.98f, 1f, Mathf.Lerp(0.35f, 0.95f, k));
                _shield.localScale = Vector3.one * Mathf.Lerp(1.5f, 1.75f, k);
            }
            else
            {
                float breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * 2f);
                sr.color = new Color(0.65f, 0.8f, 1f, Mathf.Lerp(0.25f, 0.42f, breathe));
                _shield.localScale = Vector3.one * Mathf.Lerp(1.46f, 1.54f, breathe);
            }
        }

        void CreatePartner()
        {
            var go = new GameObject(Type.Name + "（另一位）");
            go.transform.SetParent(transform.parent, false);
            _partner = go.AddComponent<SVUnit>();
            _partner.SheetName = Type.PartnerSheetName;
            _partner.SheetIndex = Type.PartnerSheetIndex;
            _partner.MaxHp = _partner.Hp = 1;
        }

        public void SetBladeCount(int count)
        {
            for (int i = _blades.Count; i < count; i++)
            {
                var go = new GameObject("Blade" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SVShapes.Circle();
                sr.color = new Color(0.7f, 0.92f, 1f, 0.85f);
                sr.sortingOrder = 15000;
                go.transform.localScale = Vector3.one * 0.55f;
                _blades.Add(go.transform);
            }
        }

        void CreateAura()
        {
            var go = new GameObject("Aura");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SVShapes.Ring();
            sr.color = new Color(0.55f, 0.85f, 1f, 0.35f);
            sr.sortingOrder = -5000;
            go.transform.localScale = Vector3.one * Range * 2f;
            _aura = go.transform;
        }

        void Update()
        {
            var game = SVGame.Instance;
            if (game == null || game.Player == null || game.Paused) { Velocity = Vector2.zero; return; }

            // 時停：隊友跟敵人一起定格，只有男主還能動
            if (SVTimeStop.WorldFrozen) { Velocity = Vector2.zero; return; }

            if (Retired) { Velocity = Vector2.zero; return; }

            float dt = Time.deltaTime;
            UpdateMovement(game, dt);

            // 藍條：滿了就自動放技能
            CooldownLeft -= dt * game.Awakenings.CompanionManaScale;

            if (_bars != null)
            {
                // 雙子刃是持續生效的，藍條就一直是滿的，不然看起來像壞掉。
                // 空揮（附近沒有目標）也顯示滿的 —— 那代表「隨時可以打，只是沒東西打」，
                // 顯示成 87% 之類的半滿反而讓人以為卡住了。
                _bars.Mana = (Type.Skill == SVSkillKind.Orbit && !Type.OrbitBlades) || _fizzled
                    ? 1f
                    : CooldownNormalized;
            }

            if (CooldownLeft <= 0f)
            {
                float full = Cooldown;
                CooldownLeft = full;

                Cast(game);

                // 各技能在「沒有目標」時會把 CooldownLeft 覆寫成很短的重試值，
                // 所以剩餘時間明顯比整條短 = 這次是空揮，沒有真的打出去。
                _fizzled = CooldownLeft < full - 0.01f;

                // 真的打出去了才閃一下，玩家看得出隊友有在動作
                if (!_fizzled) Flash();
            }

            UpdatePlayerShield(game, dt);

            if (Type.OrbitBlades || Type.Skill == SVSkillKind.Orbit) UpdateBlades(dt);
            if (Type.Skill == SVSkillKind.Frost) UpdateFrost(dt);
            if (Invincible > 0f) Invincible -= dt;
            UpdateShield(dt);
        }

        // ---------------- 指揮 ----------------

        public void SetOrder(SVOrder order)
        {
            Order = order;
            if (order != SVOrder.Hold) SetHoldRing(false);
        }

        public void HoldAt(Vector2 point)
        {
            Order = SVOrder.Hold;
            HoldPoint = ClampToArena(point);
            SetHoldRing(true);
        }

        public void BeginDrag() => Dragging = true;

        public void DragTo(Vector2 world) => _dragTarget = ClampToArena(world);

        public void EndDrag() => Dragging = false;

        static Vector2 ClampToArena(Vector2 p) => SVConfig.ClampToArena(p, 0.5f);

        void SetHoldRing(bool on)
        {
            if (on && _holdRing == null)
            {
                var go = new GameObject("HoldRing");
                go.transform.SetParent(transform.parent, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SVShapes.Ring();
                sr.color = new Color(1f, 0.85f, 0.4f, 0.45f);
                sr.sortingOrder = -20500;
                go.transform.localScale = Vector3.one * 0.9f;
                _holdRing = go.transform;
            }

            if (_holdRing != null)
            {
                _holdRing.gameObject.SetActive(on);
                if (on) _holdRing.position = HoldPoint;
            }
        }

        void UpdateMovement(SVGame game, float dt)
        {
            // 被拖著走的時候直接跟游標，玩家要看得到自己在擺誰
            if (Dragging)
            {
                MoveTowards(_dragTarget, dt, 1.8f);
                UpdatePartner(Pos + new Vector2(-0.9f, 0f), dt);
                return;
            }

            switch (Order)
            {
                case SVOrder.Hold:
                    if (_holdRing != null) _holdRing.position = HoldPoint;
                    HoldStation(dt);
                    UpdatePartner(Pos + new Vector2(-0.9f, 0f), dt);
                    return;

                case SVOrder.Free:
                    FreeRoam(game, dt);
                    UpdatePartner(Pos + new Vector2(-0.9f, 0f), dt);
                    return;

                default:
                    Follow(game.Player, dt);
                    return;
            }
        }

        /// <summary>守在定點：站上去之後只面向最近的敵人，不追出去。</summary>
        void HoldStation(float dt)
        {
            if ((HoldPoint - Pos).magnitude > 0.2f) { MoveTowards(HoldPoint, dt, 1f); return; }

            Velocity = Vector2.zero;
            var near = SVEnemy.NearestTo(Pos, Mathf.Max(Range, 6f));
            if (near != null) FaceTowards(near.Pos);
        }

        /// <summary>自由移動：自己找最近的敵人靠上去，沒敵人就繞著玩家附近晃。</summary>
        void FreeRoam(SVGame game, float dt)
        {
            var target = SVEnemy.NearestTo(Pos, 14f);
            if (target == null)
            {
                // 場上沒東西打就別跑太遠，不然玩家會找不到人
                Follow(game.Player, dt);
                return;
            }

            // 技能有射程的就停在射程邊緣，近戰的才貼上去
            float keep = Type.Skill == SVSkillKind.Burst || Type.Skill == SVSkillKind.Orbit
                ? 0.8f : Mathf.Max(1.5f, Range * 0.7f);

            var delta = (Vector2)target.Pos - Pos;
            float dist = delta.magnitude;

            if (dist > keep + 0.3f) MoveTowards(target.Pos, dt, 1f);
            else if (dist < keep - 0.6f) MoveTowards(Pos - delta.normalized, dt, 0.6f);
            else { Velocity = Vector2.zero; FaceTowards(target.Pos); }
        }

        void MoveTowards(Vector2 target, float dt, float speedScale)
        {
            var delta = target - Pos;
            float dist = delta.magnitude;

            if (dist <= 0.05f) { Velocity = Vector2.zero; return; }

            Velocity = delta.normalized * Mathf.Min(MoveSpeed * speedScale, dist * 6f);
            Pos = ClampToArena(Pos + Velocity * dt);
        }

        /// <summary>
        /// 定期在男主身上疊一層護盾。鈴音是大魔法師，護男主是她的本分 ——
        /// 這跟她的攻擊技能各走各的計時器，不會互相排擠。
        /// </summary>
        void UpdatePlayerShield(SVGame game, float dt)
        {
            if (Type.PlayerShieldPower <= 0 || game.Player == null) return;

            _shieldTimer -= dt;
            if (_shieldTimer > 0f) return;

            _shieldTimer = Mathf.Max(1f, Type.PlayerShieldInterval);

            // 已經疊著厚盾就不再補，讓它先被打掉
            if (game.Player.Shield >= Type.PlayerShieldPower) return;

            game.Player.AddShield(Type.PlayerShieldPower - game.Player.Shield);
            game.SpawnPulse(game.Player.Pos, 2.4f, new Color(0.6f, 0.85f, 1f, 0.6f), 0.45f);
        }

        void Follow(SVPlayer player, float dt)
        {
            // 圍在玩家後方散開，不要疊在一起
            float angle = Mathf.PI * 2f * Slot / Mathf.Max(1, Active.Count) + Time.time * 0.35f;
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f) * 1.5f;
            var target = player.Pos + offset;

            var delta = target - Pos;
            float dist = delta.magnitude;

            // 離太遠就直接瞬移回來，不然被卡住會掉隊
            if (dist > 12f) { Pos = target; Velocity = Vector2.zero; }
            else if (dist > 0.15f)
            {
                Velocity = delta.normalized * Mathf.Min(MoveSpeed, dist * 6f);
                Pos += Velocity * dt;
            }
            else Velocity = Vector2.zero;

            UpdatePartner(player.Pos - offset, dt);
        }

        /// <summary>雙子的另一位。她沒有自己的腦，只是黏在指定位置上。</summary>
        void UpdatePartner(Vector2 target, float dt)
        {
            if (_partner == null) return;

            var before = _partner.Pos;
            _partner.Pos = Vector2.Lerp(before, target, 10f * dt);
            _partner.Velocity = dt > 0f ? (_partner.Pos - before) / dt : Vector2.zero;
        }

        // ---------------- 技能 ----------------

        void Cast(SVGame game)
        {
            int power = Power;

            switch (Type.Skill)
            {
                case SVSkillKind.Burst:
                {
                    // 鈴音：以自己為中心的爆發斬。沒有敵人就不放，免得浪費 CD
                    var near = SVEnemy.NearestTo(Pos, Range);
                    if (near == null) { CooldownLeft = 0.25f; return; }

                    int hit = SVEnemy.DamageInRadius(Pos, Range, power, 4f, null, BossBonus);
                    game.SpawnPulse(Pos, Range, new Color(1f, 0.45f, 0.55f, 0.55f), 0.3f);
                    if (hit > 0) FaceTowards(near.Pos);
                    break;
                }

                case SVSkillKind.Heal:
                {
                    // 露娜：回血。滿血就不浪費 CD
                    if (game.Player.Hp >= game.Player.MaxHp) { CooldownLeft = 0.5f; return; }
                    game.Player.Heal(power);
                    game.SpawnPulse(game.Player.Pos, 2.2f, new Color(0.5f, 1f, 0.6f, 0.5f), 0.45f);
                    break;
                }

                case SVSkillKind.Homing:
                {
                    // 尤梨卡：一次三發追蹤彈，各自鎖不同的目標
                    int fired = 0;
                    var taken = new HashSet<SVEnemy>();
                    for (int i = 0; i < 3 + ExtraShots; i++)
                    {
                        var t = NearestExcluding(taken, Range);
                        if (t == null) break;
                        taken.Add(t);
                        fired++;

                        var slash = SVArt.Slash(Type.SlashSprite);
                        var shot = SVProjectile.Spawn(
                            game.ShotRoot, Pos + Vector2.up * 0.4f, t.Pos - Pos,
                            slash != null ? Type.SlashColor : new Color(1f, 0.6f, 0.9f),
                            slash != null ? Type.SlashSize : 0.45f,
                            slash, true);
                        shot.Damage = power;
                        shot.BossMultiplier = BossBonus;
                        shot.Speed = 9f;
                        shot.Life = 2.5f;
                        shot.Radius = 0.3f;
                        shot.Homing = t;
                    }
                    if (fired == 0) CooldownLeft = 0.25f;
                    break;
                }

                case SVSkillKind.Frost:
                    // 克萊爾：領域是持續的，這裡只放一次爆傷當節拍
                    SVEnemy.DamageInRadius(Pos, Range, power, 0f, null, BossBonus);
                    game.SpawnPulse(Pos, Range, new Color(0.6f, 0.85f, 1f, 0.4f), 0.35f);
                    break;

                case SVSkillKind.Orbit:
                    // 雪瑤之力是持續傷害，CD 只用來讓刀轉快一點
                    break;

                // ---------------- 雪瑤：連鎖雷電 ／ 冰風暴 輪流 ----------------
                case SVSkillKind.Storm:
                {
                    _stormTurn = (_stormTurn + 1) % 2;

                    if (_stormTurn == 0)
                    {
                        // 連鎖雷電：從最近的開始往外跳，一隻接一隻
                        var hit = new HashSet<SVEnemy>();
                        Vector2 from = Pos;
                        int jumps = 5;

                        for (int i = 0; i < jumps; i++)
                        {
                            SVEnemy next = null;
                            float bestSq = (i == 0 ? Range : Range * 0.7f);
                            bestSq *= bestSq;

                            foreach (var e in SVEnemy.Active)
                            {
                                if (e == null || e.IsDead || hit.Contains(e)) continue;
                                float sq = ((Vector2)e.Pos - from).sqrMagnitude;
                                if (sq < bestSq) { bestSq = sq; next = e; }
                            }

                            if (next == null) break;
                            hit.Add(next);

                            // 越跳越弱，但至少留一半
                            int dmg = Mathf.Max(1, Mathf.RoundToInt(power * Mathf.Lerp(1f, 0.5f, i / (float)jumps)));
                            next.TakeDamage(dmg, SVDamageKind.Magic);
                            game.SpawnPulse(next.Pos, 1.1f, new Color(0.7f, 0.88f, 1f, 0.75f), 0.22f);
                            from = next.Pos;
                        }
                        break;
                    }

                    // 冰風暴：周身一圈，傷害稍低但會減速
                    float stormRadius = Range * 1.25f;
                    foreach (var e in SVEnemy.Active.ToArray())
                    {
                        if (e == null || e.IsDead || !e.Overlaps(Pos, stormRadius)) continue;
                        e.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(power * 0.75f)), SVDamageKind.Magic);
                        e.Slow(0.55f, 2.2f);
                    }
                    game.SpawnPulse(Pos, stormRadius, new Color(0.6f, 0.9f, 1f, 0.45f), 0.4f);
                    break;
                }

                // ---------------- 雪霜：盾衝擊 ／ 劍刃 輪流 ----------------
                case SVSkillKind.Bash:
                {
                    _bashTurn = (_bashTurn + 1) % 2;

                    var target = SVEnemy.NearestTo(Pos, Range * 2.2f);
                    Vector2 dir = target != null
                        ? ((Vector2)target.Pos - Pos).normalized
                        : Vector2.right;

                    if (_bashTurn == 0)
                    {
                        // 盾衝擊：往前撞一段，路徑上的全部被推開
                        Vector2 to = SVConfig.ClampToArena(Pos + dir * (Range * 1.8f), 1f);
                        var hit = new HashSet<SVEnemy>();

                        for (int i = 1; i <= 6; i++)
                        {
                            Vector2 at = Vector2.Lerp(Pos, to, i / 6f);
                            foreach (var e in SVEnemy.Active.ToArray())
                            {
                                if (e == null || e.IsDead || hit.Contains(e)) continue;
                                if (!e.Overlaps(at, 1.1f)) continue;
                                hit.Add(e);
                                e.TakeDamage(power, SVDamageKind.Physical);
                                e.Push(dir * 9f);
                            }
                        }

                        Pos = to;
                        game.SpawnPulse(to, 1.8f, new Color(0.85f, 0.92f, 1f, 0.6f), 0.3f);
                        break;
                    }

                    // 劍刃：面前一片扇形，傷害比盾撞高
                    int bladeDmg = Mathf.RoundToInt(power * 1.35f);
                    foreach (var e in SVEnemy.Active.ToArray())
                    {
                        if (e == null || e.IsDead) continue;
                        var diff = (Vector2)e.Pos - Pos;
                        if (diff.sqrMagnitude > (Range * 1.5f) * (Range * 1.5f)) continue;
                        if (Vector2.Dot(diff.normalized, dir) < 0.25f) continue;   // 只砍面前
                        e.TakeDamage(Mathf.Max(1, bladeDmg), SVDamageKind.Physical);
                        e.Push(dir * 3f);
                    }
                    game.SpawnPulse(Pos + dir * Range * 0.8f, Range * 1.1f,
                                    new Color(0.9f, 0.95f, 1f, 0.55f), 0.28f);
                    break;
                }

                case SVSkillKind.Melee:
                {
                    // 近戰：夠近才砍得到，還沒走到就別空揮
                    var near = SVEnemy.NearestTo(Pos, Range);
                    if (near == null) { CooldownLeft = 0.2f; return; }

                    FaceTowards(near.Pos);
                    var toward = ((Vector2)near.Pos - Pos).normalized;
                    SVEnemy.DamageInRadius(Pos + toward * 0.6f,
                                           Range * 0.8f, power, 3.5f, null, BossBonus);

                    // 有設劍氣的（凱）就揮一道出去，沒設的還是原本的光圈
                    var meleeSlash = SVArt.Slash(Type.SlashSprite);
                    if (meleeSlash != null)
                    {
                        var fx = SVProjectile.Spawn(
                            game.ShotRoot, Pos + toward * 0.5f + Vector2.up * 0.35f,
                            toward, Type.SlashColor, Type.SlashSize, meleeSlash, true);

                        // 純表演：往前帶一點點就消失，傷害早就在上面結算完了
                        fx.Harmless = true;
                        fx.Speed = 4.5f;
                        fx.Life = 0.18f;
                        fx.Radius = 0f;
                    }
                    else game.SpawnPulse(Pos, Range * 0.9f, new Color(1f, 0.85f, 0.55f, 0.45f), 0.2f);
                    break;
                }

                case SVSkillKind.Arrow:
                {
                    var target = SVEnemy.NearestTo(Pos, Range);
                    if (target == null) { CooldownLeft = 0.25f; return; }

                    FaceTowards(target.Pos);
                    var shot = SVProjectile.Spawn(game.ShotRoot, Pos + Vector2.up * 0.4f,
                                                  target.Pos - Pos, new Color(0.9f, 0.95f, 0.7f), 0.4f);
                    shot.Damage = power;
                    shot.BossMultiplier = BossBonus;
                    shot.Speed = 12f;
                    shot.Life = Range / 12f + 0.3f;
                    shot.Radius = 0.26f;
                    shot.Pierce = 1;
                    shot.Knockback = 1f;
                    break;
                }

                case SVSkillKind.Arcane:
                {
                    // 大魔法師：往「敵人最密的地方」丟，而不是最近的那一隻。
                    // 一群怪擠在一起時傷害才吃得滿，這是法師跟近戰最大的差別。
                    var element = NextElement();
                    float radius = Type.BlastRadius * element.RadiusMul;

                    var spots = PickBlastSpots(Range, radius, Mathf.Max(1, element.Strikes));
                    if (spots.Count == 0) { CooldownLeft = 0.3f; return; }

                    FaceTowards(spots[0]);
                    StartCoroutine(CastArcane(game, spots, element, power, radius,
                                              Mathf.Max(0f, Type.CastTime)));
                    break;
                }
            }
        }

        int _elementIndex;

        /// <summary>目前輪到的元素名字，HUD 用來顯示下一發是什麼。</summary>
        public string CurrentElementName =>
            Type != null && Type.Elements != null && Type.Elements.Count > 0
                ? Type.Elements[_elementIndex % Type.Elements.Count].Name
                : "星辰墜落";

        /// <summary>取下一個元素並把指標往前推。清單是空的就給一個預設值。</summary>
        SVArcaneElement NextElement()
        {
            if (Type.Elements == null || Type.Elements.Count == 0) return new SVArcaneElement();

            var e = Type.Elements[_elementIndex % Type.Elements.Count];
            _elementIndex = (_elementIndex + 1) % Type.Elements.Count;
            return e;
        }

        /// <summary>
        /// 挑 count 個落點。每挑一個就把已經被涵蓋的敵人排除，
        /// 落雷那種多重打擊才不會三發全砸在同一堆怪上。
        /// </summary>
        List<Vector2> PickBlastSpots(float range, float radius, int count)
        {
            var spots = new List<Vector2>();
            var used = new HashSet<SVEnemy>();

            for (int n = 0; n < count; n++)
            {
                var spot = BestBlastSpot(range, radius, used);
                if (!spot.HasValue) break;

                spots.Add(spot.Value);

                foreach (var e in SVEnemy.Active)
                    if (e != null && !e.IsDead && e.Overlaps(spot.Value, radius)) used.Add(e);
            }

            return spots;
        }

        /// <summary>
        /// 在射程內找一個「炸下去能打到最多敵人」的落點。
        /// 直接拿每隻敵人的位置當候選圓心，算各自能覆蓋幾隻，取最高的。
        /// 敵人再多也只是 O(n²) 的小迴圈，而且只在放技能的那一幀跑。
        /// </summary>
        Vector2? BestBlastSpot(float range, float blastRadius, HashSet<SVEnemy> exclude = null)
        {
            Vector2? best = null;
            int bestCount = 0;
            float rangeSq = range * range;
            float blastSq = blastRadius * blastRadius;

            var list = SVEnemy.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var center = list[i];
                if (center == null || center.IsDead) continue;
                if (exclude != null && exclude.Contains(center)) continue;
                if (((Vector2)center.Pos - Pos).sqrMagnitude > rangeSq) continue;

                int count = 0;
                for (int j = 0; j < list.Count; j++)
                {
                    var other = list[j];
                    if (other == null || other.IsDead) continue;
                    if (exclude != null && exclude.Contains(other)) continue;
                    if (((Vector2)other.Pos - (Vector2)center.Pos).sqrMagnitude <= blastSq) count++;
                }

                // 同樣數量時偏好比較近的，免得一直往地圖邊緣丟
                if (count > bestCount ||
                    (count == bestCount && best.HasValue &&
                     ((Vector2)center.Pos - Pos).sqrMagnitude < (best.Value - Pos).sqrMagnitude))
                {
                    bestCount = count;
                    best = center.Pos;
                }
            }

            return bestCount > 0 ? best : null;
        }

        /// <summary>詠唱 → 地上出現預兆圈 → 依序落下。</summary>
        System.Collections.IEnumerator CastArcane(SVGame game, List<Vector2> spots, SVArcaneElement element,
                                                  int power, float radius, float castTime)
        {
            // 所有落點的預兆圈同時亮起來，玩家才知道要閃哪裡
            var warnings = new List<GameObject>();
            foreach (var spot in spots)
            {
                var w = game.SpawnMarker(spot, radius, element.WarnColor, element.MarkerImage);
                if (w != null) warnings.Add(w);
            }

            float t = 0f;
            while (t < castTime)
            {
                if (game == null || game.IsOver) break;
                if (!game.Paused) t += Time.deltaTime;
                yield return null;
            }

            foreach (var w in warnings) if (w != null) Destroy(w);
            if (game == null) yield break;

            string strikeEffect = string.IsNullOrEmpty(element.StrikeEffect)
                ? element.Effect : element.StrikeEffect;

            for (int i = 0; i < spots.Count; i++)
            {
                if (game == null || game.IsOver) yield break;

                // 第一發用主特效，之後用「每發」的特效
                Strike(game, spots[i], element, i == 0 ? element.Effect : strikeEffect, power, radius);

                // 多重打擊之間留一點間隔，看起來才像連續落雷而不是一次爆四發
                if (i < spots.Count - 1)
                {
                    float wait = 0.12f;
                    while (wait > 0f)
                    {
                        if (game == null || game.IsOver) yield break;
                        if (!game.Paused) wait -= Time.deltaTime;
                        yield return null;
                    }
                }
            }
        }

        void Strike(SVGame game, Vector2 spot, SVArcaneElement element,
                    string effectName, int power, float radius)
        {
            // RM 的 Effekseer 特效
            // 特效的大小刻意跟爆炸半徑脫鉤 —— 火焰與煙霧是大片半透明貼圖，
            // 照半徑放大就是成倍的 overdraw（星隕特別明顯）。
            // SVEffects 那邊還有一道上限把它壓住。
            // 演出時間砍半（原本 1.6 秒）：大片半透明的火與煙留在畫面上越久，
            // 疊在一起的填充率成本就越高，而且星隕是連續兩發，第二發會疊在第一發上
            if (effectName != null && effectName.IndexOf("Thunder", System.StringComparison.OrdinalIgnoreCase) >= 0)
                SVCombatVisuals.Lightning(spot, radius);
            else SVEffects.Play(effectName, spot, radius * element.EffectScale, 0.8f);

            int dmg = Mathf.Max(1, Mathf.RoundToInt(power * element.DamageMul));

            // 中心全額、邊緣打折 —— 站在正中央的那幾隻會特別痛
            var targets = SVEnemy.Active.ToArray();
            foreach (var e in targets)
            {
                if (e == null || e.IsDead) continue;
                if (!e.Overlaps(spot, radius)) continue;

                float d = Vector2.Distance(e.Pos, spot) / Mathf.Max(0.01f, radius);
                int hitDamage = Mathf.Max(1, Mathf.RoundToInt(dmg * Mathf.Lerp(1f, 0.55f, d)));
                if (Type?.Skill == SVSkillKind.Arcane && e.Type?.IsBoss == true)
                {
                    hitDamage *= 10;
                    // C 級（MapRank 1）的王對鈴音來說根本不夠看 —— 再乘 5，讓等級差距一眼看得出來
                    if (SVGame.Instance != null && SVGame.Instance.MapRank <= 1) hitDamage *= 5;
                }
                e.TakeDamage(hitDamage);

                if (element.FreezeSeconds > 0f) e.Freeze(element.FreezeSeconds);
                else if (element.SlowSeconds > 0f) e.Slow(element.SlowFactor, element.SlowSeconds);

                if (element.Knockback > 0f)
                    e.Push(((Vector2)e.Pos - spot).normalized * element.Knockback);
            }

            game.SpawnPulse(spot, radius, element.PulseColor, 0.45f);

            // 灼燒地帶：留在原地慢慢燒，敵我都燒
            if (element.BurnSeconds > 0f)
            {
                int burn = Mathf.Max(1, Mathf.RoundToInt(dmg * element.BurnDamageMul));
                var field = SVBurnField.Spawn(
                    game.ShotRoot, spot,
                    radius * element.BurnRadiusMul,
                    element.BurnSeconds,
                    burn,
                    element.BurnPlayerDamage,
                    element.BurnColor);

                if (field != null) field.BossMultiplier = BossBonus * (Type?.Skill == SVSkillKind.Arcane ? 10f : 1f);
            }
        }

        SVEnemy NearestExcluding(HashSet<SVEnemy> taken, float range)
        {
            SVEnemy best = null;
            float bestSq = range * range;
            foreach (var e in SVEnemy.Active)
            {
                if (e == null || e.IsDead || taken.Contains(e)) continue;
                float sq = ((Vector2)e.Pos - Pos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = e; }
            }
            return best;
        }

        // 雪瑤／雪霜的兩招輪流放，各自記自己輪到哪一招
        int _stormTurn = -1;
        int _bashTurn = -1;

        void UpdateBlades(float dt)
        {
            _bladeAngle += dt * 200f;
            float radius = Range;

            for (int i = 0; i < _blades.Count; i++)
            {
                float a = (_bladeAngle + i * 360f / _blades.Count) * Mathf.Deg2Rad;
                _blades[i].localPosition = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.7f, 0f);
            }

            _tickTimer -= dt;
            if (_tickTimer > 0f) return;
            _tickTimer = 0.22f;

            // 刀的傷害獨立於 Power —— 雪瑤的 Power 是給連鎖雷電／冰風暴用的，
            // 兩者共用一個數字的話，刀太弱（打王只扣 1）或法術太痛，一定有一邊壞掉
            int power = Type != null && Type.BladePower > 0 ? Type.BladePower : Power;
            power = Mathf.Max(1, Mathf.RoundToInt(power * PowerMul));
            // 刀用自己的 BladePower 時沒經過 Power，羈絆倍率要另外乘
            if (Type != null && Type.BladePower > 0 && SVGame.Instance != null)
                power = Mathf.Max(1, Mathf.RoundToInt(power * SVGame.Instance.Bond.CompanionMul(Type) * SVGame.Instance.RestMul));

            foreach (var blade in _blades)
                SVEnemy.DamageInRadius(blade.position, 0.55f, power, 2f, null, BossBonus);
        }

        void UpdateFrost(float dt)
        {
            if (_aura != null) _aura.localScale = Vector3.one * Range * 2f;

            foreach (var e in SVEnemy.Active)
            {
                if (e == null || e.IsDead) continue;
                if (e.Overlaps(Pos, Range)) e.Slow(0.45f, 0.4f);
            }
        }
    }
}
