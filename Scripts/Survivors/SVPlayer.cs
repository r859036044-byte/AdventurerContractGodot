// 男主。玩家只負責「走位」，攻擊全自動 —— 這是這類遊戲的核心手感。
using System.Collections.Generic;
using RMZS.GodotPort;
using RMZS.GodotPort.InputSystem;

namespace RMZS
{
    public partial class SVPlayer : SVUnit
    {
        [Header("普攻")]
        public int AttackDamage = 4;
        public float AttackInterval = 0.55f;
        public float AttackRange = 7f;
        public float ShotSpeed = 11f;
        [Tooltip("一次打出幾發")] public int AttackShots = 1;
        [Tooltip("每發能穿過幾隻")] public int AttackPierce = 0;

        [Header("物理／魔法")]
        [Tooltip("普攻算物理還是魔法。拿法杖出門就是魔法")]
        public SVDamageKind AttackKind = SVDamageKind.Physical;

        [Tooltip("裝備的物攻。物理技能會吃到一部分")]
        public int PhysicalPower;

        [Tooltip("裝備的魔攻。魔法技能會吃到一部分")]
        public int MagicPower;

        [Tooltip("技能吃到多少攻擊力。0.2 = 物攻 100 讓物理技能 +20")]
        public float SkillPowerRatio = 0.2f;

        /// <summary>
        /// 武器專屬的強化。拿劍就是迴避／暴擊／流血，拿法杖就是冰風毒火的附魔。
        /// 升級三選一那邊會照 AttackKind 只給對應的那一組。
        /// </summary>
        public readonly SVWeaponAffix Affix = new SVWeaponAffix();

        /// <summary>拿的是法杖（普攻走魔法）。升級選項靠這個分流。</summary>
        public bool IsMage => AttackKind == SVDamageKind.Magic;

        [Header("普攻外觀（劍氣）")]
        [Tooltip("劍氣大小。圖是 Resources/Survivors/Effect/player_attack")]
        public float SlashSize = 1.1f;

        [Tooltip("劍氣的染色。白色 = 用圖片原本的顏色")]
        public Color SlashColor = Color.white;

        [Tooltip("圖片本身朝右，這裡補角度差。整把劍氣歪掉的話調這個")]
        public float SlashAngleOffset = 0f;

        [Tooltip("關掉就退回原本的黃色光球")]
        public bool UseSlashSprite = true;

        [Tooltip("普攻用哪張圖（Resources/Survivors/Effect/）。拿法杖出門時會被換成 player_staff")]
        public string SlashSprite = "player_attack";

        [Header("成長")]
        public int Level = 1;
        public int Xp;
        public int XpToNext => ExperienceRequired(Level);

        public float Invincible { get; private set; }

        /// <summary>裝備給的固定減傷（物理）。</summary>
        public int Defense;

        /// <summary>裝備給的固定減傷（魔法）。</summary>
        public int MagicDefense;

        /// <summary>技能實際打出去的傷害：技能自己的威力 + 對應攻擊力的一部分。</summary>
        public int SkillPower(SVPlayerSkill skill)
        {
            if (skill == null) return 0;
            int stat = skill.DamageKind == SVDamageKind.Magic ? MagicPower : PhysicalPower;
            return skill.Power + Mathf.RoundToInt(stat * SkillPowerRatio);
        }

        // ---- 公會的祝福。基準值另外記著，選了新的祝福要從基準重算 ----
        SVPassiveState _passives;
        int _baseAtk; float _baseInterval; int _baseShots, _basePierce;
        float _baseRange, _baseSpeed; int _baseMaxHp;
        bool _baseCaptured;
        bool _reviveUsed;

        /// <summary>
        /// 套用祝福。會從「還沒有祝福時」的基準值重算，
        /// 所以高等級委託選好幾個也不會越算越歪。
        /// </summary>
        /// <summary>苦行的全域加成已經套過了。ApplyPassives 會重跑，這個只准生效一次。</summary>
        bool _asceticApplied;

        public void ApplyPassives(SVPassiveState passives)
        {
            if (passives == null) return;

            if (!_baseCaptured)
            {
                _baseCaptured = true;
                _baseAtk = AttackDamage;
                _baseInterval = AttackInterval;
                _baseShots = AttackShots;
                _basePierce = AttackPierce;
                _baseRange = AttackRange;
                _baseSpeed = MoveSpeed;
                _baseMaxHp = MaxHp;
            }

            _passives = passives;

            // 羈絆 DEBUFF（尤梨卡＋克萊爾不和）也會壓到男主
            // 昨晚沒去酒館的 +10% 也一起乘
            float bondMul = SVGame.Instance != null ? SVGame.Instance.Bond.TeamAttackMul * SVGame.Instance.RestMul : 1f;
            // 祝福的普攻加成不再乘在這裡，改成命中時在 SVEnemy.TakeDamage 以「普攻傷害」類別乘（傷害 LOG 才看得到）
            AttackDamage = Mathf.Max(1, Mathf.RoundToInt(_baseAtk * bondMul));
            AttackInterval = Mathf.Max(0.08f, _baseInterval * passives.AttackSpeedMul);
            AttackShots = _baseShots + passives.ExtraShots;
            AttackPierce = _basePierce + passives.ExtraPierce;
            AttackRange = _baseRange + passives.RangeBonus;
            MoveSpeed = _baseSpeed * passives.MoveSpeedMul;

            int newMax = Mathf.Max(1, Mathf.RoundToInt(_baseMaxHp * passives.MaxHpMul));
            int delta = newMax - MaxHp;
            MaxHp = newMax;
            if (delta > 0) Hp += delta;
            Hp = Mathf.Min(Hp, MaxHp);

            Defense += passives.FlatDefense;

            // 苦行：放棄大覺的主動發動，換技能傷害與冷卻。
            // 這一段是「乘進全域倍率」，而 ApplyPassives 會被重複呼叫
            // （卡片給新祝福時也會再跑一次），所以要自己記得只套一次
            var game = SVGame.Instance;
            if (game != null && passives.NoAwakenActive && !_asceticApplied)
            {
                _asceticApplied = true;
                game.SkillPowerMul *= 1.3f;
                game.SkillCooldownMul *= passives.SkillCooldownMul;
            }

            if (passives.StartLevel > Level)
            {
                int gain = passives.StartLevel - Level;
                Level = passives.StartLevel;
                for (int i = 0; i < gain; i++) SVGame.Instance?.OnLevelUp();
            }
        }

        /// <summary>護盾。先扣這個再扣血，鈴音會定期補上。</summary>
        public int Shield { get; private set; }

        /// <summary>護盾的上限，用來畫比例條。取「拿過的最高值」，條才不會亂跳。</summary>
        public int ShieldMax { get; private set; }

        public void AddShield(int amount)
        {
            if (amount <= 0) return;

            // 苦行之類的祝福會放大護盾效果
            if (_passives != null && _passives.ShieldMul != 1f)
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * _passives.ShieldMul));

            // 羈絆：露娜在場，護盾量加成
            if (SVGame.Instance != null && SVGame.Instance.Bond.ShieldMul != 1f)
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * SVGame.Instance.Bond.ShieldMul));

            Shield += amount;
            ShieldMax = Mathf.Max(ShieldMax, Shield);
        }

        public void ClearShield()
        {
            Shield = 0;
            ShieldMax = 0;
            UpdateShieldRing();
        }

        // ---------------- 護盾的外觀 ----------------

        /// <summary>套在小人身上的那圈護盾。只有有盾的時候才顯示。</summary>
        SpriteRenderer _shieldRing;

        /// <summary>身體中心離腳底多高。血條掛在 0.95（頭頂上），所以身體中段抓 0.45。</summary>
        const float BodyCenterY = 0.45f;

        /// <summary>破盾之後給的短暫無敵，讓玩家有時間脫離。</summary>
        public const float ShieldBreakInvincible = 0.8f;

        void EnsureShieldRing()
        {
            if (_shieldRing != null) return;

            var go = new GameObject("ShieldRing");
            go.transform.SetParent(transform, false);

            // 角色貼圖的樞紐在腳底附近（SVSpriteSheet 用 0.5, 0.15），
            // 所以 localPosition 0 是「腳下」而不是身體中心 ——
            // 光環要往上抬到身體中段，不然會變成套在腳踝上
            go.transform.localPosition = new Vector3(0f, BodyCenterY, 0f);

            _shieldRing = go.AddComponent<SpriteRenderer>();
            _shieldRing.sprite = SVShapes.Ring();
            _shieldRing.sortingOrder = 18000;      // 疊在小人上面，但低於爆發特效
            _shieldRing.enabled = false;
        }

        /// <summary>依目前盾量開關那圈光環，並讓它輕輕呼吸。</summary>
        void UpdateShieldRing()
        {
            if (Shield <= 0)
            {
                if (_shieldRing != null) _shieldRing.enabled = false;
                return;
            }

            EnsureShieldRing();
            _shieldRing.enabled = true;

            // 盾越薄越閃，快破的時候看得出來
            float ratio = ShieldMax > 0 ? Mathf.Clamp01((float)Shield / ShieldMax) : 1f;
            float blink = 1f + (1f - ratio) * 3.5f;
            float pulse = 0.72f + 0.28f * Mathf.Sin(Time.time * (3f + blink));

            _shieldRing.color = new Color(0.55f, 0.85f, 1f, Mathf.Lerp(0.35f, 0.85f, ratio) * pulse);

            float scale = Radius * 2f * (1.55f + 0.06f * Mathf.Sin(Time.time * 4.2f));
            _shieldRing.transform.localScale = Vector3.one * scale;
        }

        /// <summary>
        /// 盾剛好被打破的那一下：碎裂演出 ＋ 短暫無敵。
        /// 無敵是刻意給的 —— 破盾的當下通常正被圍毆，沒有這一下喘息就等於盾白拿。
        /// </summary>
        void OnShieldBroken()
        {
            ShieldMax = 0;
            if (_shieldRing != null) _shieldRing.enabled = false;

            GrantInvincible(ShieldBreakInvincible);

            var game = SVGame.Instance;
            if (game == null) return;

            // 兩圈一快一慢，看起來像玻璃碎開
            var at = Pos + new Vector2(0f, BodyCenterY);
            game.SpawnPulse(at, Radius * 2.4f, new Color(0.75f, 0.93f, 1f, 0.9f), 0.18f);
            game.SpawnPulse(at, Radius * 4.2f, new Color(0.45f, 0.8f, 1f, 0.55f), 0.42f);

            StartCoroutine(ShieldShards());
        }

        /// <summary>碎片：幾塊小方塊往外噴再淡出。純裝飾，數量刻意壓少。</summary>
        System.Collections.IEnumerator ShieldShards()
        {
            const int count = 7;
            var game = SVGame.Instance;
            var parent = game != null ? game.ShotRoot : transform.parent;

            var shards = new List<Transform>(count);
            var dirs = new List<Vector2>(count);
            var srs = new List<SpriteRenderer>(count);

            for (int i = 0; i < count; i++)
            {
                float ang = (360f / count) * i + Random.Range(-14f, 14f);
                var dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));

                var go = new GameObject("ShieldShard");
                go.transform.SetParent(parent, false);
                go.transform.position = Pos + new Vector2(0f, BodyCenterY) + dir * (Radius * 0.9f);
                go.transform.localScale = Vector3.one * Random.Range(0.12f, 0.2f);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, ang);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SVShapes.Square();
                sr.color = new Color(0.8f, 0.94f, 1f, 0.95f);
                sr.sortingOrder = 18500;

                shards.Add(go.transform);
                dirs.Add(dir);
                srs.Add(sr);
            }

            const float life = 0.35f;
            float t = 0f;
            while (t < life)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / life);

                for (int i = 0; i < shards.Count; i++)
                {
                    if (shards[i] == null) continue;
                    shards[i].position += (Vector3)(dirs[i] * (7f * Time.deltaTime * (1f - k * 0.6f)));
                    var c = srs[i].color;
                    c.a = 0.95f * (1f - k);
                    srs[i].color = c;
                }
                yield return null;
            }

            foreach (var sh in shards) if (sh != null) Destroy(sh.gameObject);
        }

        /// <summary>回血。祝福的回血倍率統一在這裡乘，各處呼叫端不用各自處理。</summary>
        public override int Heal(int amount)
        {
            if (amount > 0 && _passives != null && _passives.HealMul != 1f)
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * _passives.HealMul));

            return base.Heal(amount);
        }

        /// <summary>補給品給的短時間無敵。取比較長的那個，不會被短的蓋掉。</summary>
        public void GrantInvincible(float seconds) =>
            Invincible = Mathf.Max(Invincible, seconds);

        /// <summary>男主自己的主動技能。用升級三選一解鎖，選到同一個就升等。</summary>
        public readonly List<SVPlayerSkill> Skills = new List<SVPlayerSkill>();

        /// <summary>技能清單有沒有變過。HUD 靠它決定要不要重排版面。</summary>
        public int SkillsVersion { get; private set; }

        float _attackTimer;
        int _facing = 1;   // 面向，震盪波要用

        public void Setup()
        {
            DisplayName = "男主";
            SheetName = "男主1";
            SheetIndex = 3;
            MoveSpeed = SVConfig.PlayerSpeed;
            MaxHp = Hp = SVConfig.PlayerMaxHp;
            Radius = 0.35f;
            Skills.Clear();
            SkillsVersion++;
            ClearShield();
            Defense = 0;
            _passives = null;
            _baseCaptured = false;
            _reviveUsed = false;

            SVDev.Apply(this);   // 除錯面板改過血量／攻擊力的話，開局就吃進去
        }

        /// <summary>沒學過就學起來，學過就升一級。</summary>
        public void UnlockOrLevel(SVPlayerSkillKind kind)
        {
            var found = Skills.Find(s => s.Kind == kind);
            if (found != null) { found.Level++; return; }

            var skill = SVPlayerSkill.Create(kind);
            skill.CooldownLeft = skill.Cooldown * 0.4f;   // 剛學會不要馬上放
            Skills.Add(skill);
            SkillsVersion++;
        }

        public bool HasSkill(SVPlayerSkillKind kind) => Skills.Exists(s => s.Kind == kind);

        public int SkillLevel(SVPlayerSkillKind kind)
        {
            var found = Skills.Find(s => s.Kind == kind);
            return found != null ? found.Level : 0;
        }

        void Update()
        {
            var game = SVGame.Instance;
            if (game == null || game.Paused) { Velocity = Vector2.zero; return; }

            float dt = Time.deltaTime;
            if (Invincible > 0f) Invincible -= dt;

            UpdateShieldRing();
            UpdateAdrenaline(dt);

            Move(dt);
            Attack(dt);
            UpdateSkills(dt);
        }

        /// <summary>腎上腺素的持續回血。小數留著累積，不然每幀四捨五入會全被吃掉。</summary>
        float _adrenalineHeal;

        void UpdateAdrenaline(float dt)
        {
            var aw = SVGame.Instance?.Awakenings;
            float perSecond = aw != null ? aw.AdrenalineRegen : 0f;
            if (perSecond <= 0f) { _adrenalineHeal = 0f; return; }

            _adrenalineHeal += perSecond * dt;
            int whole = Mathf.FloorToInt(_adrenalineHeal);
            if (whole <= 0) return;

            _adrenalineHeal -= whole;
            Heal(whole);
        }

        // ---------------- 主動技能 ----------------

        /// <summary>
        /// 男主的技能是手動的：只在這裡轉冷卻，實際發動要玩家自己按
        /// （畫面下方的按鈕，或鍵盤 1~4）。
        /// </summary>
        void UpdateSkills(float dt)
        {
            for (int i = 0; i < Skills.Count; i++)
                if (Skills[i].CooldownLeft > 0f) Skills[i].CooldownLeft -= dt;

            var kb = Keyboard.current;
            if (kb == null) return;

            // 技能鍵照「選項 → 按鍵設定」（預設 Q W E）
            if (RMZ.RMZKeyBindings.JustPressed("skill1")) TryCast(0);
            else if (RMZ.RMZKeyBindings.JustPressed("skill2")) TryCast(1);
            else if (RMZ.RMZKeyBindings.JustPressed("skill3")) TryCast(2);
        }

        /// <summary>放第 index 個技能。還在冷卻或沒學過就回 false。</summary>
        public bool TryCast(int index)
        {
            var game = SVGame.Instance;
            if (game == null || game.Paused) return false;
            if (index < 0 || index >= Skills.Count) return false;

            var skill = Skills[index];
            if (skill.CooldownLeft > 0f) return false;

            skill.CooldownLeft = skill.Cooldown *
                (_passives != null ? _passives.SkillCooldownMul : 1f);
            Cast(game, skill);

            // 雙重詠唱：同一發再來一次，中間隔一點點才看得出來是兩下
            // 雙重詠唱：大覺版是必定兩下，祝福版是機率
            bool twice = game.Awakenings.DoubleCast ||
                         (_passives != null && _passives.SkillDoubleChance > 0f &&
                          Random.value < _passives.SkillDoubleChance);

            if (twice) StartCoroutine(CastAgain(game, skill));
            return true;
        }

        System.Collections.IEnumerator CastAgain(SVGame game, SVPlayerSkill skill)
        {
            yield return new WaitForSeconds(0.18f);
            if (this != null && !IsDead && game != null && !game.IsOver) Cast(game, skill);
        }

        void Cast(SVGame game, SVPlayerSkill skill)
        {
            SVEffects.Play(skill.Kind.ToString(), Pos, Mathf.Min(skill.Range, 3f), .5f);
            switch (skill.Kind)
            {
                case SVPlayerSkillKind.Whirlwind:
                    SVEnemy.DamageInRadiusAs(SVHitSource.Skill, Pos, skill.Range, SkillPower(skill), 5f,
                                           null, 1f, skill.DamageKind);
                    game.SpawnPulse(Pos, skill.Range, new Color(1f, 0.85f, 0.4f, 0.6f), 0.28f);
                    break;

                case SVPlayerSkillKind.Shockwave:
                {
                    var target = SVEnemy.NearestTo(Pos, skill.Range);
                    var dir = target != null
                        ? ((Vector2)target.Pos - Pos).normalized
                        : new Vector2(_facing, 0f);

                    var shot = SVProjectile.Spawn(game.ShotRoot, Pos + Vector2.up * 0.4f, dir,
                                                  new Color(1f, 0.7f, 0.35f), 1.1f);
                    shot.Damage = SkillPower(skill);
                    shot.Source = SVHitSource.Skill;
                    shot.DamageKind = skill.DamageKind;
                    shot.Speed = 10f;
                    shot.Life = skill.Range / 10f + 0.3f;
                    shot.Radius = 0.85f;
                    shot.Knockback = 4f;
                    shot.Pierce = 99;
                    break;
                }

                case SVPlayerSkillKind.Barrier:
                    Invincible = Mathf.Max(Invincible, 1.2f + skill.Level * 0.2f);
                    Heal(SkillPower(skill));
                    game.SpawnPulse(Pos, skill.Range * 2f, new Color(0.6f, 0.9f, 1f, 0.7f), 0.5f);
                    break;

                // ---------------- B 級解鎖 ----------------

                case SVPlayerSkillKind.BloodEdge:
                {
                    int hit = SVEnemy.DamageInRadiusAs(SVHitSource.Skill, Pos, skill.Range, SkillPower(skill), 3f,
                                                     null, 1f, skill.DamageKind);
                    game.SpawnPulse(Pos, skill.Range, new Color(0.9f, 0.2f, 0.3f, 0.65f), 0.3f);

                    // 打到越多吸越多，但一次最多吸回上限的兩成，不然會無敵
                    if (hit > 0)
                    {
                        int drain = Mathf.Clamp(hit * (2 + skill.Level), 0, MaxHp / 5);
                        if (drain > 0) Heal(drain);
                    }
                    break;
                }

                // ---------------- A 級解鎖 ----------------

                case SVPlayerSkillKind.Warlord:
                {
                    var target = SVEnemy.NearestTo(Pos, skill.Range * 1.5f);
                    var dir = target != null
                        ? ((Vector2)target.Pos - Pos).normalized
                        : new Vector2(_facing, 0f);

                    var from = Pos;
                    var to = from + dir * skill.Range;

                    // 路徑上分段判定，不然衝太快會穿過去沒打到
                    int steps = Mathf.CeilToInt(skill.Range / 0.8f);
                    for (int i = 1; i <= steps; i++)
                    {
                        var p = Vector2.Lerp(from, to, i / (float)steps);
                        SVEnemy.DamageInRadiusAs(SVHitSource.Skill, p, 1.2f, SkillPower(skill), 7f,
                                               null, 1f, skill.DamageKind);
                    }

                    // 衝過去。留在場地內，不要衝出邊界
                    Pos = SVConfig.ClampToArena(to, 1f);
                    GrantInvincible(0.35f);   // 衝的過程不吃接觸傷害

                    game.SpawnPulse(from, 1.6f, new Color(1f, 0.75f, 0.35f, 0.6f), 0.25f);
                    game.SpawnPulse(Pos, 2.0f, new Color(1f, 0.6f, 0.25f, 0.7f), 0.3f);
                    break;
                }

                // ---------------- S 級解鎖 ----------------

                case SVPlayerSkillKind.StarFall:
                {
                    // 每一級多一顆。落點挑敵人最近的幾隻，沒有敵人就砸自己周圍
                    int rocks = 3 + skill.Level;
                    var taken = new HashSet<SVEnemy>();

                    for (int i = 0; i < rocks; i++)
                    {
                        SVEnemy next = null;
                        float bestSq = skill.Range * skill.Range;
                        foreach (var e in SVEnemy.Active)
                        {
                            if (e == null || e.IsDead || taken.Contains(e)) continue;
                            float sq = ((Vector2)e.Pos - Pos).sqrMagnitude;
                            if (sq < bestSq) { bestSq = sq; next = e; }
                        }

                        Vector2 spot = next != null
                            ? (Vector2)next.Pos
                            : Pos + Random.insideUnitCircle * skill.Range;

                        if (next != null) taken.Add(next);

                        StartCoroutine(DropMeteor(game, spot, SkillPower(skill), 0.12f * i,
                                                  skill.DamageKind));
                    }
                    break;
                }

                case SVPlayerSkillKind.PoisonCloud:
                    StartCoroutine(PoisonCloudField(game, skill));
                    break;

                case SVPlayerSkillKind.ShieldBash:
                    StartCoroutine(ShieldBashDash(game, skill));
                    break;

                // ---------------- 法杖專屬 ----------------

                case SVPlayerSkillKind.DarkMatter:
                    StartCoroutine(DarkMatterOrbs(game, skill));
                    break;

                case SVPlayerSkillKind.Thunderstorm:
                    StartCoroutine(ThunderstormCloud(game, skill));
                    break;

                case SVPlayerSkillKind.BlackHole:
                {
                    var near = SVEnemy.NearestTo(Pos, skill.Range);
                    Vector2 at = near != null ? (Vector2)near.Pos : Pos + AimDir() * 4f;
                    StartCoroutine(BlackHoleField(game, skill, at));
                    break;
                }

                // ---------------- 2026-09-27 新增 ----------------

                case SVPlayerSkillKind.BladeDance:
                    StartCoroutine(BladeDanceCombo(game, skill));
                    break;

                case SVPlayerSkillKind.FrostNova:
                {
                    float r = skill.Range;
                    foreach (var e in SVEnemy.Active.ToArray())
                    {
                        if (e == null || e.IsDead || !e.Overlaps(Pos, r)) continue;
                        SVHit.Skill(e).TakeDamage(SkillPower(skill), skill.DamageKind);
                        if (e.IsDead) continue;
                        if (e.Type != null && e.Type.IsBoss) e.Slow(0.5f, 2f);
                        else e.Freeze(1.2f + skill.Level * 0.15f);
                    }
                    game.SpawnPulse(Pos, r, new Color(0.6f, 0.9f, 1f, 0.7f), 0.35f);
                    break;
                }

                case SVPlayerSkillKind.SwordRing:
                    StartCoroutine(SwordRingOrbit(game, skill));
                    break;

                case SVPlayerSkillKind.Judgment:
                    StartCoroutine(JudgmentStrike(game, skill));
                    break;

                case SVPlayerSkillKind.Earthshatter:
                    StartCoroutine(EarthshatterWaves(game, skill, Pos));
                    break;

                case SVPlayerSkillKind.AbsoluteZero:
                {
                    float r = skill.Range;
                    float freeze = 2f + skill.Level * 0.2f;
                    foreach (var e in SVEnemy.Active.ToArray())
                    {
                        if (e == null || e.IsDead || !e.Overlaps(Pos, r)) continue;
                        SVHit.Skill(e).TakeDamage(SkillPower(skill), skill.DamageKind);
                        if (e.IsDead) continue;
                        if (e.Type != null && e.Type.IsBoss) e.Slow(0.35f, 3f);   // 王凍住就太無腦了
                        else e.Freeze(freeze);
                    }
                    game.SpawnPulse(Pos, r, new Color(0.7f, 0.92f, 1f, 0.75f), 0.9f);
                    SVEffects.Play("IceAll2", Pos, r * 0.6f, 0.9f);
                    break;
                }

                case SVPlayerSkillKind.Chain:
                {
                    // 從最近的往外劈，每一級多劈一隻
                    int count = 3 + skill.Level;
                    var hit = new HashSet<SVEnemy>();
                    var from = Pos;

                    for (int i = 0; i < count; i++)
                    {
                        SVEnemy next = null;
                        float bestSq = skill.Range * skill.Range;
                        foreach (var e in SVEnemy.Active)
                        {
                            if (e == null || e.IsDead || hit.Contains(e)) continue;
                            float sq = ((Vector2)e.Pos - from).sqrMagnitude;
                            if (sq < bestSq) { bestSq = sq; next = e; }
                        }
                        if (next == null) break;

                        hit.Add(next);
                        SVHit.Skill(next).TakeDamage(SkillPower(skill), skill.DamageKind);
                        game.SpawnPulse(next.Pos, 1.1f, new Color(0.75f, 0.9f, 1f, 0.8f), 0.22f);
                        from = next.Pos;
                    }
                    break;
                }
            }
        }

        // ---------------- 滑鼠點擊移動 ----------------
        Vector2? _mouseDest;
        bool _mouseHeld;
        const float MouseArriveDistance = 0.15f;

        Vector2 MouseMoveInput()
        {
            var game = SVGame.Instance;
            var mouse = Mouse.current;
            if (game == null || mouse == null || game.Cam == null) return Vector2.zero;

            var world = (Vector2)game.Cam.ScreenToWorldPoint(mouse.position.ReadValue());

            if (mouse.leftButton.wasPressedThisFrame)
            {
                // 點在介面（HUD 按鈕、選項卡、面板）上、或點在隊友身上給指揮系統用，都不算走路
                bool onUi = SVRuntime.Instance != null &&
                            SVRuntime.Instance.GetViewport().GuiGetHoveredControl() != null;
                _mouseHeld = !onUi && !SVCommandController.WantsClick(world);
                if (_mouseHeld) _mouseDest = world;
            }
            else if (_mouseHeld && mouse.leftButton.isPressed) _mouseDest = world;
            else if (!mouse.leftButton.isPressed) _mouseHeld = false;

            if (_mouseDest == null) return Vector2.zero;

            var to = _mouseDest.Value - Pos;
            if (to.magnitude <= MouseArriveDistance)
            {
                if (!_mouseHeld) _mouseDest = null;
                return Vector2.zero;
            }
            return to.normalized;
        }

        void Move(float dt)
        {
            var kb = Keyboard.current;
            var input = Vector2.zero;
            if (kb != null)
            {
                // 移動照按鍵設定（預設方向鍵；WASD 不再移動，W 給技能用）
                if (RMZ.RMZKeyBindings.Held("left")) input.x -= 1f;
                if (RMZ.RMZKeyBindings.Held("right")) input.x += 1f;
                if (RMZ.RMZKeyBindings.Held("up")) input.y += 1f;
                if (RMZ.RMZKeyBindings.Held("down")) input.y -= 1f;
            }

            // 滑鼠：點空地走過去，按住左鍵就一直跟著游標。按方向鍵立刻交回鍵盤。
            if (input.sqrMagnitude > 0.01f) { _mouseDest = null; _mouseHeld = false; }
            else input = MouseMoveInput();

            if (Mathf.Abs(input.x) > 0.01f) _facing = input.x > 0f ? 1 : -1;

            // 腎上腺素：發動期間跑得比較快
            float speed = MoveSpeed;
            var aw = SVGame.Instance?.Awakenings;
            if (aw != null) speed *= aw.MoveSpeedMultiplier;

            Velocity = input.sqrMagnitude > 1f ? input.normalized * speed : input * speed;
            var next = Pos + Velocity * dt;

            // 場地邊界（圓形／長方形視 SVConfig.RectArena 而定）
            next = SVConfig.ClampToArena(next);
            next = SVObstacles.ResolveMove(next, Radius);

            Pos = next;
        }

        void Attack(float dt)
        {
            _attackTimer -= dt;
            if (_attackTimer > 0f) return;

            var target = SVEnemy.NearestTo(Pos, AttackRange);
            if (target == null) return;

            // 砸瓦魯多：時停期間攻速 ×3（+200%）。
            // 打出去的劍氣會停在半空，所以這兩秒等於在空中排好一整排傷害
            float interval = SVTimeStop.Stopped
                ? AttackInterval / SVTimeStop.HasteMultiplier
                : AttackInterval;

            // 極速連擊：發動期間普攻間隔直接除以十
            var awaken = SVGame.Instance?.Awakenings;
            if (awaken != null) interval /= Mathf.Max(0.01f, awaken.AttackSpeedMultiplier);

            _attackTimer = Mathf.Max(0.02f, interval);

            FaceTowards(target.Pos);
            BeginWeaponAttack((target.Pos-Pos).normalized,interval);

            // 連擊：這一次多打一輪
            int volleys = 1;
            if (_passives != null && _passives.DoubleAttackChance > 0f &&
                Random.value < _passives.DoubleAttackChance) volleys = 2;

            // 風的附魔也會多打一輪（法杖專屬）
            if (IsMage && Affix.Gale > 0 && Random.value < Affix.GaleExtraShotChance) volleys++;

            for (int v = 0; v < volleys; v++) FireVolley(target);
        }

        void FireVolley(SVEnemy target)
        {

            var baseDir = (target.Pos - (IsMage ? WeaponShotOrigin : Pos)).normalized;
            int shots = Mathf.Max(1, AttackShots);

            for (int i = 0; i < shots; i++)
            {
                // 多發時左右散開，中間那發保持直線
                float spread = shots == 1 ? 0f : (i - (shots - 1) * 0.5f) * 11f;
                var dir = (Quaternion.Euler(0f, 0f, spread) * baseDir).normalized;

                // 劍氣改用程式內建的像素圖（SVPixelArt.Slash），不再讀 player_attack.png。
                // 拿法杖時打出去的是魔法球，不走這裡。
                var slash = IsMage ? null : UseSlashSprite ? SVPixelArt.Slash(false) : null;

                var shot = SVProjectile.Spawn(
                    SVGame.Instance.ShotRoot,
                    WeaponShotOrigin,
                    dir,
                    IsMage ? Color.white : slash != null ? SlashColor : new Color(1f, 0.95f, 0.6f),
                    IsMage ? .75f : slash != null ? SlashSize : 0.5f,
                    slash,
                    true);

                shot.SpriteAngleOffset = SlashAngleOffset;
                shot.ApplyRotation();   // 角度修正要立刻生效，不然第一幀會是歪的
                shot.Damage = AttackDamage;
                shot.Source = SVHitSource.Attack;
                shot.DamageKind = AttackKind;
                shot.Speed = ShotSpeed * (IsMage ? Affix.GaleSpeedMul : 1f);
                shot.Life = (AttackRange + (IsMage ? Affix.GaleRangeBonus : 0f)) / shot.Speed + 0.3f;
                shot.Radius = 0.28f;
                shot.Knockback = 1.5f;
                shot.Pierce = AttackPierce;

                // 爆擊與灼燒。暴擊率／倍率抽成共用屬性，流血那邊也讀同一份
                float crit = CritChance;
                float critMul = CritMultiplier;

                if (crit > 0f && Random.value < crit)
                {
                    shot.Damage = Mathf.RoundToInt(shot.Damage * critMul);
                    shot.IsCrit = true;   // 命中時敵人頭上會跳「暴擊」並震一下
                    shot.Tint(new Color(1f, 0.55f, 0.35f));
                }
                if (_passives != null) shot.BurnPower = Mathf.RoundToInt(_passives.BurnPower);

                // 卡片給的普攻附魔
                var game = SVGame.Instance;
                if (game != null && game.AttackBleedStacks > 0)
                    shot.BleedStacks += game.AttackBleedStacks;
                if (_passives != null && _passives.AttackBleed > 0)
                    shot.BleedStacks += _passives.AttackBleed;   // 血刃

                ApplyAffix(shot);
            }
        }

        /// <summary>
        /// 灼燒。刻意不吃無敵幀也不吃裝備防禦 ——
        /// 這是「不要站在火裡」的提醒，不是一次性的重擊，
        /// 所以數字很小但一定扣得到，護盾還是擋得住。
        /// </summary>
        public void TakeBurn(int amount)
        {
            if (amount <= 0 || IsDead) return;

            if (Shield > 0)
            {
                int absorbed = Mathf.Min(Shield, amount);
                Shield -= absorbed;
                amount -= absorbed;
                if (Shield <= 0) OnShieldBroken();
                if (amount <= 0) return;
            }

            int dealt = base.TakeDamage(amount);
            if (dealt > 0) SVGame.Instance?.FlashHurt(dealt);
        }

        /// <summary>
        /// 毒霧：原地留一團毒氣，站在裡面的每 2.5 秒中一層毒。
        /// 毒本身不會退，所以這招的價值是「讓敵人一直待在裡面」，
        /// 傷害不在霧上，在之後那一路上疊起來的層數。
        /// </summary>
        System.Collections.IEnumerator PoisonCloudField(SVGame game, SVPlayerSkill skill)
        {
            const float life = 6f;
            const float applyInterval = 2.5f;

            float radius = skill.Range;
            Vector2 at = Pos;

            var go = new GameObject("PoisonCloud");
            go.transform.SetParent(game.ShotRoot, false);
            go.transform.position = at;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SVShapes.Soft();
            sr.color = new Color(0.45f, 0.85f, 0.35f, 0.34f);
            sr.sortingOrder = -16000;   // 貼在地上，不擋視線
            sr.enabled = false;
            var mist = new SVPoisonMist { Radius = radius * 48f, ZIndex = -1800, ZAsRelative = false };
            go.Node.AddChild(mist);

            float t = 0f;
            float apply = 0f;   // 一放下去就先中一層，之後每 2.5 秒再一層
            while (t < life)
            {
                if (game == null || game.IsOver) break;
                if (!game.Paused)
                {
                    float dt = Time.deltaTime;
                    t += dt;

                    mist.Age = t; mist.QueueRedraw();
                    // 慢慢淡出，讓玩家看得出來快沒了
                    float fade = Mathf.Clamp01((life - t) / 1.2f);
                    sr.color = new Color(0.45f, 0.85f, 0.35f, 0.34f * fade);

                    apply -= dt;
                    if (apply <= 0f)
                    {
                        apply = applyInterval;
                        foreach (var e in SVEnemy.Active)
                        {
                            if (e == null || e.IsDead || !e.Overlaps(at, radius)) continue;
                            e.Poison(1);
                        }
                        game.SpawnPulse(at, radius, new Color(0.5f, 0.95f, 0.4f, 0.35f), 0.3f);
                    }
                }
                yield return null;
            }

            if (go != null) Destroy(go);
        }

        /// <summary>衝刺中的減傷（0.05 = 只吃 5% 傷害）。0 以外的值同時代表「正在衝」。</summary>
        public float DashDamageScale { get; private set; } = 1f;

        /// <summary>衝刺中受到傷害時，反彈給附近敵人的倍率。</summary>
        float _dashReflect;

        /// <summary>
        /// 盾擊：向前衝一段，路上的全部撞開。
        ///
        /// 衝的過程減傷 95%，而且擋下來的傷害會反彈給旁邊的敵人 ——
        /// 所以這招不只是位移，是「故意衝進人堆裡吃一波再還回去」。
        /// </summary>
        System.Collections.IEnumerator ShieldBashDash(SVGame game, SVPlayerSkill skill)
        {
            const float dashTime = 0.35f;

            var dir = AimDir();
            Vector2 from = Pos;
            Vector2 to = SVConfig.ClampToArena(from + dir * skill.Range, 0.6f);

            int power = SkillPower(skill);
            var hit = new HashSet<SVEnemy>();

            DashDamageScale = 0.05f;   // 減傷 95%
            _dashReflect = 3f;         // 擋下來的每 1 點傷害，往外還 3 點

            try
            {
                float t = 0f;
                while (t < dashTime)
                {
                    if (game == null || game.IsOver) break;
                    if (!game.Paused)
                    {
                        t += Time.deltaTime;
                        Pos = SVObstacles.ResolveMove(Vector2.Lerp(from, to, t / dashTime), Radius);
    
                        foreach (var e in SVEnemy.Active.ToArray())
                        {
                            if (e == null || e.IsDead || hit.Contains(e)) continue;
                            if (!e.Overlaps(Pos, Radius + 1.0f)) continue;
    
                            hit.Add(e);
                            SVHit.Skill(e).TakeDamage(power, skill.DamageKind);
                            e.Push(((Vector2)e.Pos - Pos).normalized * 12f);
                        }
                    }
                    yield return null;
                }
    
            }
            finally
            {
                DashDamageScale = 1f;
                _dashReflect = 0f;
            }

            if (game != null) game.SpawnPulse(Pos, 2.4f, new Color(0.8f, 0.9f, 1f, 0.6f), 0.3f);
        }

        /// <summary>目前的施法方向：最近的敵人優先，其次面向。</summary>
        Vector2 AimDir()
        {
            var target = SVEnemy.NearestTo(Pos, 30f);
            if (target != null)
            {
                var d = (Vector2)target.Pos - Pos;
                if (d.sqrMagnitude > 0.0001f) return d.normalized;
            }
            if (Velocity.sqrMagnitude > 0.01f) return Velocity.normalized;
            return Vector2.right;
        }

        /// <summary>
        /// 暗物質：兩顆黑球在身邊轉一圈，然後往外射出去。
        /// 轉的那一圈本身也會打到人，所以貼身時等於先蹭兩下再送出去。
        /// </summary>
        System.Collections.IEnumerator DarkMatterOrbs(SVGame game, SVPlayerSkill skill)
        {
            const float spinTime = 0.9f;
            const float spinRadius = 1.6f;
            int power = SkillPower(skill);

            var orbs = new List<Transform>(2);
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("DarkOrb");
                go.transform.SetParent(game.ShotRoot, false);
                go.transform.localScale = Vector3.one * 0.55f;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SVShapes.Soft();
                sr.color = new Color(0.32f, 0.16f, 0.5f, 0.95f);
                sr.sortingOrder = 17000;

                orbs.Add(go.transform);
            }

            float t = 0f;
            var hitThisSpin = new HashSet<SVEnemy>();
            while (t < spinTime)
            {
                if (this == null || IsDead || game == null || game.IsOver) break;
                if (!game.Paused)
                {
                    t += Time.deltaTime;
                    float ang = t * 720f * Mathf.Deg2Rad;

                    for (int i = 0; i < orbs.Count; i++)
                    {
                        if (orbs[i] == null) continue;
                        float a = ang + i * Mathf.PI;
                        Vector2 at = Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * spinRadius;
                        orbs[i].position = at;

                        // 轉的過程也會刮到人，但同一隻一輪只吃一次
                        foreach (var e in SVEnemy.Active.ToArray())
                        {
                            if (e == null || e.IsDead || hitThisSpin.Contains(e)) continue;
                            if (!e.Overlaps(at, 0.6f)) continue;
                            hitThisSpin.Add(e);
                            SVHit.Skill(e).TakeDamage(Mathf.Max(1, power / 3), skill.DamageKind);
                        }
                    }
                }
                yield return null;
            }

            // 射出去：兩顆往相反方向，帶爆炸
            var dir = AimDir();
            for (int i = 0; i < orbs.Count; i++)
            {
                if (orbs[i] != null) Destroy(orbs[i].gameObject);
                if (this == null || game == null || game.IsOver) continue;

                float a = i == 0 ? 0f : Mathf.PI;
                var shotDir = new Vector2(
                    dir.x * Mathf.Cos(a) - dir.y * Mathf.Sin(a),
                    dir.x * Mathf.Sin(a) + dir.y * Mathf.Cos(a));

                var shot = SVProjectile.Spawn(game.ShotRoot, Pos, shotDir,
                                              new Color(0.55f, 0.35f, 0.95f, 1f), 0.6f);
                if (shot == null) continue;

                shot.Damage = power;
                shot.Source = SVHitSource.Skill;
                shot.DamageKind = skill.DamageKind;
                shot.Speed = 9f;
                shot.Life = 2.2f;
                shot.Radius = 0.55f;
                shot.Pierce = 3;
                shot.SplashRadius = 2.2f;
                shot.SplashDamage = Mathf.Max(1, Mathf.RoundToInt(power * 0.6f));
            }
        }

        /// <summary>
        /// 雷電風暴：留一團雷在場上自己亂飄，每隔一下劈周圍一圈。
        /// 它不跟著玩家跑 —— 放在敵人多的地方就是一塊持續的傷害區。
        /// </summary>
        // ---------------- 2026-09-27 新增技能的持續效果 ----------------

        /// <summary>等 seconds 秒（暫停時不算）。回傳 false 代表這局已經結束，別再繼續。</summary>
        System.Collections.IEnumerator WaitUnpaused(SVGame game, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                if (this == null || game == null || game.IsOver) yield break;
                if (!game.Paused) t += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>疾風連斬：朝面向（有敵人就朝最近的）連砍三刀，最後一刀擊退。</summary>
        System.Collections.IEnumerator BladeDanceCombo(SVGame game, SVPlayerSkill skill)
        {
            for (int i = 0; i < 3; i++)
            {
                if (this == null || IsDead || game == null || game.IsOver) yield break;
                var near = SVEnemy.NearestTo(Pos, skill.Range + 3f);
                Vector2 dir = near != null ? ((Vector2)near.Pos - Pos).normalized : AimDir();
                Vector2 at = Pos + dir * (skill.Range * 0.6f);
                bool last = i == 2;
                SVEnemy.DamageInRadiusAs(SVHitSource.Skill, at, skill.Range, SkillPower(skill) * (last ? 2 : 1), last ? 6f : 1f,
                                       null, 1f, skill.DamageKind);
                SVEffects.Play("Blade", at, skill.Range * 0.8f, 0.35f);
                game.SpawnPulse(at, skill.Range, new Color(1f, 0.9f, 0.6f, 0.55f), 0.18f);
                yield return WaitUnpaused(game, 0.14f);
            }
        }

        /// <summary>劍陣：飛劍繞身旋轉，每 0.25 秒割一次碰到的敵人。</summary>
        System.Collections.IEnumerator SwordRingOrbit(SVGame game, SVPlayerSkill skill)
        {
            int count = Mathf.Min(6, 3 + skill.Level / 2);
            float life = 4f + skill.Level * 0.4f;
            int power = SkillPower(skill);

            var blades = new List<GameObject>();
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("SwordRing");
                go.transform.SetParent(game.ShotRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SVShapes.Soft();
                sr.color = new Color(0.85f, 0.95f, 1f, 0.9f);
                sr.sortingOrder = 19000;
                go.transform.localScale = Vector3.one * 0.9f;
                blades.Add(go);
            }

            float t = 0f, tick = 0f, spin = 0f;
            while (t < life)
            {
                if (this == null || IsDead || game == null || game.IsOver) break;
                if (!game.Paused)
                {
                    float dt = Time.deltaTime;
                    t += dt; tick -= dt; spin += dt * 4.2f;
                    for (int i = 0; i < count; i++)
                    {
                        float a = spin + i * Mathf.PI * 2f / count;
                        var p = Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * skill.Range;
                        if (blades[i] != null) blades[i].transform.position = p;
                        if (tick <= 0f) SVEnemy.DamageInRadiusAs(SVHitSource.Skill, p, 0.9f, power, 1.2f, null, 1f, skill.DamageKind);
                    }
                    if (tick <= 0f) tick = 0.25f;
                }
                yield return null;
            }

            foreach (var b in blades) if (b != null) Destroy(b);
        }

        /// <summary>聖光審判：鎖定範圍內血最多的幾隻，0.7 秒後光柱落下。</summary>
        System.Collections.IEnumerator JudgmentStrike(SVGame game, SVPlayerSkill skill)
        {
            int count = 2 + skill.Level;
            var targets = new List<SVEnemy>();
            foreach (var e in SVEnemy.Active)
                if (e != null && !e.IsDead && e.Overlaps(Pos, skill.Range)) targets.Add(e);
            targets.Sort((a, b) => b.Hp.CompareTo(a.Hp));
            if (targets.Count > count) targets.RemoveRange(count, targets.Count - count);

            foreach (var e in targets) game.SpawnPulse(e.Pos, 1.2f, new Color(1f, 0.95f, 0.6f, 0.8f), 0.7f);
            yield return WaitUnpaused(game, 0.7f);

            int power = SkillPower(skill);
            foreach (var e in targets)
            {
                if (e == null || e.IsDead) continue;
                SVHit.Skill(e).TakeDamage(power, skill.DamageKind);
                SVEffects.Play("Judgment", e.Pos, 1.6f, 0.8f);
                SVEnemy.DamageInRadiusAs(SVHitSource.Skill, e.Pos, 1.4f, power / 3, 2f, e, 1f, skill.DamageKind);   // 旁邊的也吃一點
            }
        }

        /// <summary>天崩地裂：從施放點擴散三道地震波（一圈比一圈大），每道只打那一圈上的敵人。</summary>
        System.Collections.IEnumerator EarthshatterWaves(SVGame game, SVPlayerSkill skill, Vector2 at)
        {
            float prev = 0f;
            int power = SkillPower(skill);
            for (int w = 1; w <= 3; w++)
            {
                if (this == null || game == null || game.IsOver) yield break;
                float r = skill.Range * w / 3f;
                foreach (var e in SVEnemy.Active.ToArray())
                {
                    if (e == null || e.IsDead) continue;
                    float d = Vector2.Distance(e.Pos, at);
                    if (d > r + 0.4f || d < prev - 0.4f) continue;
                    SVHit.Skill(e).TakeDamage(power, skill.DamageKind);
                    if (e.IsDead) continue;
                    var away = ((Vector2)e.Pos - at);
                    e.Push((away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.right) * 5f);
                    if (e.Type == null || !e.Type.IsBoss) e.Freeze(0.5f);   // 短暫震暈
                }
                game.SpawnPulse(at, r, new Color(0.9f, 0.7f, 0.4f, 0.7f), 0.35f);
                prev = r;
                yield return WaitUnpaused(game, 0.25f);
            }
        }

        System.Collections.IEnumerator ThunderstormCloud(SVGame game, SVPlayerSkill skill)
        {
            float life = 6f + skill.Level * 0.6f;
            float radius = skill.Range;
            int power = SkillPower(skill);

            Vector2 at = Pos + AimDir() * 3f;
            Vector2 drift = Random.insideUnitCircle.normalized * 1.6f;

            var go = new GameObject("Thunderstorm");
            go.transform.SetParent(game.ShotRoot, false);
            go.transform.position = at;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SVShapes.Soft();
            sr.color = new Color(0.55f, 0.7f, 1f, 0.35f);
            sr.sortingOrder = -15000;   // 畫在腳底下，不擋視線
            go.transform.localScale = Vector3.one * radius * 2.2f;

            float t = 0f, tick = 0f;
            while (t < life)
            {
                if (this == null || game == null || game.IsOver) break;
                if (!game.Paused)
                {
                    float dt = Time.deltaTime;
                    t += dt;

                    // 亂飄：每隔一陣子換方向，並且被場地邊界拉回來
                    drift = Vector2.Lerp(drift, Random.insideUnitCircle.normalized * 1.6f, dt * 0.6f);
                    at = SVConfig.ClampToArena(at + drift * dt, 1.5f);
                    go.transform.position = at;

                    tick -= dt;
                    if (tick <= 0f)
                    {
                        tick = 0.55f;
                        SVEnemy.DamageInRadiusAs(SVHitSource.Skill, at, radius, power, 1.5f, null, 1f, skill.DamageKind);
                        game.SpawnPulse(at, radius, new Color(0.7f, 0.85f, 1f, 0.55f), 0.22f);
                    }
                }
                yield return null;
            }

            if (go != null) Destroy(go);
        }

        /// <summary>
        /// 黑洞：定點展開，把範圍內的敵人往中心拖，並持續絞。
        /// 拖的力道對王減半 —— 不然王會被鎖在原地，變成無腦解。
        /// </summary>
        System.Collections.IEnumerator BlackHoleField(SVGame game, SVPlayerSkill skill, Vector2 at)
        {
            float life = 3.5f + skill.Level * 0.35f;
            float radius = 6f + skill.Level * 0.4f;
            int power = SkillPower(skill);

            var go = new GameObject("BlackHole");
            go.transform.SetParent(game.ShotRoot, false);
            go.transform.position = at;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SVShapes.Soft();
            sr.color = new Color(0.08f, 0.02f, 0.16f, 0.92f);
            sr.sortingOrder = -14000;
            go.transform.localScale = Vector3.one * 2.4f;

            var rim = new GameObject("Rim");
            rim.transform.SetParent(go.transform, false);
            var rimSr = rim.AddComponent<SpriteRenderer>();
            rimSr.sprite = SVShapes.Ring();
            rimSr.color = new Color(0.65f, 0.4f, 1f, 0.5f);
            rimSr.sortingOrder = -13999;
            rim.transform.localScale = Vector3.one * (radius * 2f / 2.4f);

            float t = 0f, tick = 0f;
            while (t < life)
            {
                if (game == null || game.IsOver) break;
                if (!game.Paused)
                {
                    float dt = Time.deltaTime;
                    t += dt;

                    rim.transform.Rotate(0f, 0f, 90f * dt);

                    // 牽引：越靠近吸得越輕（免得全部黏成一點），王的力道減半
                    foreach (var e in SVEnemy.Active)
                    {
                        if (e == null || e.IsDead) continue;
                        var diff = at - (Vector2)e.Pos;
                        float dist = diff.magnitude;
                        if (dist > radius || dist < 0.35f) continue;

                        float pull = Mathf.Lerp(2.5f, 9f, dist / radius) * dt;
                        if (e.Type != null && e.Type.IsBoss) pull *= 0.5f;
                        e.Pos = SVObstacles.ResolveMove((Vector2)e.Pos + diff.normalized * pull, e.Radius);
                    }

                    tick -= dt;
                    if (tick <= 0f)
                    {
                        tick = 0.5f;
                        SVEnemy.DamageInRadiusAs(SVHitSource.Skill, at, radius * 0.55f, power, 0f, null, 1f, skill.DamageKind);
                    }
                }
                yield return null;
            }

            // 收束：最後一發集中爆
            if (game != null && !game.IsOver)
            {
                SVEnemy.DamageInRadiusAs(SVHitSource.Skill, at, radius * 0.8f, power * 2, 8f, null, 1f, skill.DamageKind);
                game.SpawnPulse(at, radius * 0.9f, new Color(0.7f, 0.45f, 1f, 0.8f), 0.4f);
            }

            if (go != null) Destroy(go);
        }

        // ---------------- 暴擊 ----------------
        //
        // 抽出來給普攻與流血共用 —— 流血是唯一會暴擊的持續傷害，
        // 兩邊各算一份的話，玩家堆了暴擊卻只有普攻吃得到，看起來像壞掉。

        /// <summary>目前的暴擊率。祝福的暴擊率跟劍的暴擊率相加（法杖沒有劍的那份）。</summary>
        public float CritChance => (_passives?.CritChance ?? 0f) + (IsMage ? 0f : Affix.Crit);

        /// <summary>目前的暴擊倍率。取祝福與武器兩邊比較高的那個。</summary>
        public float CritMultiplier => Mathf.Max(_passives?.CritMultiplier ?? SVCrit.DefaultMultiplier,
                                                 IsMage ? 0f : Affix.CritMultiplier);

        /// <summary>擲一次暴擊。</summary>
        public bool RollCrit()
        {
            float chance = CritChance;
            return chance > 0f && Random.value < chance;
        }

        /// <summary>星隕的一顆。先在地上畫預兆圈，delay 之後才炸。</summary>
        System.Collections.IEnumerator DropMeteor(SVGame game, Vector2 spot, int power, float delay,
                                                  SVDamageKind kind = SVDamageKind.Magic)
        {
            var marker = game.SpawnMarker(spot, 1.8f, new Color(1f, 0.55f, 0.25f, 0.5f));

            yield return new WaitForSeconds(0.35f + delay);

            if (marker != null) Destroy(marker);

            SVEnemy.DamageInRadiusAs(SVHitSource.Skill, spot, 1.8f, power, 6f, null, 1f, kind);

            // 星隕會點燃：燃燒是「最大生命的 2%／秒」，所以砸在肉身上特別有感
            foreach (var e in SVEnemy.Active)
                if (e != null && !e.IsDead && e.Overlaps(spot, 1.8f)) e.Ignite();

            game.SpawnPulse(spot, 2.2f, new Color(1f, 0.7f, 0.35f, 0.85f), 0.3f);
            SVEffects.Play("EarthOne2", spot, 2f, 1.5f);
        }

        /// <summary>
        /// 把武器專屬的效果貼到這一發上。
        /// 拿劍只有流血，拿法杖是冰風毒火 —— 兩邊不會同時發生。
        /// </summary>
        void ApplyAffix(SVProjectile shot)
        {
            if (shot == null) return;

            if (!IsMage)
            {
                // 劍：流血。不看防禦的持續傷害，跟灼燒共用同一套機制，取比較痛的那個
                if (Affix.BleedPower > 0 && Random.value < Affix.BleedChance)
                {
                    // 改走新的流血系統：可以無限疊層，而且每一跳都能暴擊。
                    // 詞綴的 BleedPower 換算成層數，威力越高一次上越多層
                    shot.BleedStacks += Mathf.Max(1, Mathf.RoundToInt(Affix.BleedPower / 4f));
                    shot.Tint(new Color(1f, 0.35f, 0.4f));
                }
                return;
            }

            // 法杖：附魔。四種可以同時帶，各自獨立生效
            if (Affix.Frost > 0)
            {
                shot.SlowFactor = Affix.FrostFactor;
                shot.SlowSeconds = Affix.FrostSeconds;
                shot.FreezeChance = Affix.FreezeChance;
            }

            if (Affix.Venom > 0 && Affix.VenomPower > shot.BurnPower)
            {
                shot.BurnPower = Affix.VenomPower;
                shot.BurnSeconds = Affix.VenomSeconds;
            }

            if (Affix.Flame > 0)
            {
                shot.SplashRadius = Affix.FlameRadius;
                shot.SplashDamage = Affix.FlameDamage;
            }

            if (Affix.HasAnyEnchant && Affix.Tint != Color.white) shot.Tint(Affix.Tint);
        }

        public override int TakeDamage(int amount, SVDamageKind kind = SVDamageKind.Physical)
        {
            if (Invincible > 0f) return 0;

            // 迴避（劍專屬）。躲掉就是完全不痛，也不吃無敵幀 ——
            // 判定放在防禦之前，不然「破不了防」跟「躲掉」看起來會一樣
            if (!IsMage && Affix.Dodge > 0f && Random.value < Affix.Dodge)
            {
                SVGame.Instance?.SpawnPulse(Pos, 1.2f, new Color(1f, 1f, 1f, 0.35f), 0.15f);
                return 0;
            }

            // 裝備的防禦先扣掉：物理看物防、魔法看魔防，真傷兩個都不看。
            // 這裡是純減法（不套 SVDamageMath 的地板），
            // 男主的防禦本來就是「小怪打不動我」的那種手感，跟敵人不一樣。
            // 跟敵人走同一套除法：防禦是邊際遞減，不會出現「防夠高就完全免疫」
            if (kind == SVDamageKind.Physical) amount = SVDamageMath.Apply(amount, Defense);
            else if (kind == SVDamageKind.Magic) amount = SVDamageMath.Apply(amount, MagicDefense);
            if (amount <= 0) return 0;

            // 盾擊衝刺中：只吃 5%，而且擋下來的部分往外還給附近的敵人
            if (DashDamageScale < 1f)
            {
                int blocked = amount;
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * DashDamageScale));
                blocked -= amount;

                if (blocked > 0 && _dashReflect > 0f)
                {
                    int back = Mathf.Max(1, Mathf.RoundToInt(blocked * _dashReflect));
                    SVEnemy.DamageInRadius(Pos, 3.2f, back, 4f, null, 1f, SVDamageKind.True);
                    SVGame.Instance?.SpawnPulse(Pos, 3.2f, new Color(0.85f, 0.92f, 1f, 0.55f), 0.25f);
                }
            }

            // 護盾先擋。擋掉的部分不算受傷，所以也不會觸發無敵幀 ——
            // 不然一層薄盾就能撐過整波攻擊。
            if (Shield > 0 && amount > 0)
            {
                int absorbed = Mathf.Min(Shield, amount);
                Shield -= absorbed;
                amount -= absorbed;

                if (Shield <= 0) OnShieldBroken();
                else SVGame.Instance?.SpawnPulse(Pos, 1.6f, new Color(0.55f, 0.85f, 1f, 0.55f), 0.2f);

                if (amount <= 0) return 0;
            }

            int dealt = base.TakeDamage(amount, kind);
            if (dealt > 0)
            {
                Invincible = SVConfig.PlayerInvincibleTime;
                SVGame.Instance?.FlashHurt(dealt);   // 畫面邊緣泛紅
            }
            return dealt;
        }

        /// <summary>荊棘：反彈給碰到你的那一隻。</summary>
        public void Retaliate(SVEnemy source, int damage)
        {
            if (_passives == null || _passives.Thorns <= 0f || source == null || damage <= 0) return;

            int back = Mathf.Max(1, Mathf.RoundToInt(damage * _passives.Thorns));
            source.TakeDamage(back);
            SVGame.Instance?.SpawnPulse(Pos, 1.2f, new Color(1f, 0.8f, 0.4f, 0.5f), 0.18f);
        }

        protected override void OnDeath()
        {
            // 不死鳥：一局一次，滿血站起來
            if (_passives != null && _passives.Revive && !_reviveUsed)
            {
                _reviveUsed = true;
                Hp = MaxHp;
                Invincible = 2.5f;
                SVGame.Instance?.SpawnPulse(Pos, 7f, new Color(1f, 0.75f, 0.35f, 0.85f), 1f);
                return;
            }

            SVGame.Instance?.GameOver();
        }

        /// <summary>升到第幾級之後，下一級要的能量就不再往上加。</summary>
        public const int LevelCurveCap = 5;

        /// <summary>先算原始曲線，再統一提高 30%，不把加成逐級複利。</summary>
        public static int ExperienceRequired(int level)
        {
            int original = 5;
            for (int nextLevel = 2; nextLevel <= System.Math.Min(level, LevelCurveCap - 1); nextLevel++)
                original = Mathf.RoundToInt(original * 1.35f) + 2;
            int required = (original * 130 + 99) / 100; // 經驗為整數，向上取整。

            // 10 級之後不再是固定門檻：後期能量球給得多，固定 28 會變成幾秒就升一級。
            // 從 10 級起每一級需求 ×1.15（複利）：10→11 約 32、15 級約 65、20 級約 131、25 級約 262。
            if (level >= LateCurveStart)
                required = Mathf.CeilToInt(required * Mathf.Pow(LateCurveGrowth, level - LateCurveStart + 1));
            return required;
        }

        /// <summary>從幾級開始，升級需求改成逐級變難。</summary>
        public const int LateCurveStart = 10;

        /// <summary>10 級之後每一級的需求倍率（複利）。</summary>
        public const float LateCurveGrowth = 1.15f;

        public void GainXp(int amount)
        {
            if(SVGame.Instance?.IsTutorial == true) { if(!SVGame.Instance.TutorialXpEnabled || Level >= 2) return; amount = Mathf.Min(amount, XpToNext - Xp); }
            // 委託結束之後就不再累積能量：那時候升級也選不了東西，
            // 只會在收尾（自動吸光地上掉落物）中間跳出三選一擋路。
            // EndingStarted 只涵蓋「王關打死尾王」，時間到過關／撤退／強制撤退／失敗
            // 都只會把 IsOver 設起來，所以兩個都要擋。
            var game = SVGame.Instance;
            if (game != null && (game.EndingStarted || game.IsOver)) return;

            if (_passives != null && _passives.XpMul != 1f)
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * _passives.XpMul));

            Xp += amount;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;

                // XpToNext 依新等級重算，沿用原曲線的高等級固定門檻。

                SVGame.Instance?.OnLevelUp();
            }
        }
    }
}
