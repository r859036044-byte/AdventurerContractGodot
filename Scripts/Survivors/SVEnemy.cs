// 敵人：朝玩家直線靠過去，碰到就扣血，死掉掉經驗。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    [System.Serializable]
    public class SVEnemyType
    {
        public string Name = "史萊姆";
        public string SheetName = "Monster";
        public int SheetIndex = 0;
        public int Hp = 6;
        public float Speed = 1.6f;
        public int ContactDamage = 6;
        public int Xp = 1;

        /// <summary>
        /// 物防／魔防。扣的是固定值，但至少會吃到一成五（見 SVDamageMath）。
        /// 不填的話 SVEnemy 會照血量抓一個小值，所以舊的敵人資料不用一個個補。
        /// </summary>
        public int Def = -1;
        public int MDef = -1;

        public float Radius = 0.4f;
        public float Scale = 1f;
        /// <summary>從第幾階難度才開始出現（一階 = SVConfig.DifficultyStep 秒）。</summary>
        public int FromStage = 0;
        public bool IsBoss = false;

        /// <summary>
        /// 當成「委託結尾王」登場時的絕對血量。
        /// 設了就不再套 BossHpScale 與難度階成長 —— 王的血量是玩家最在意的數字，
        /// 用一個看得懂的常數比一串倍率好調太多。0 = 沿用倍率算法。
        /// </summary>
        public int FinalBossHp = 0;

        [Header("掉落")]
        [Tooltip("掉金幣的機率 0~1")]
        [Range(0f, 1f)] public float GoldChance = 0.35f;
        public int GoldMin = 3;
        public int GoldMax = 8;

        [Tooltip("除了金幣以外的掉落表。每一筆各自擲骰")]
        public List<SVLootEntry> Drops = new List<SVLootEntry>();
    }

    public class SVEnemy : SVUnit
    {
        public static readonly List<SVEnemy> Active = new List<SVEnemy>();

        public SVEnemyType Type;
        public int ContactDamage = 6;
        public int XpValue = 1;

        /// <summary>物防／魔防。生出來的時候從 Type 抄過來。</summary>
        public int Def;
        public int MDef;

        /// <summary>委託的結尾王。打倒牠才算完成，HUD 會另外畫一條血條。</summary>
        public bool IsFinalBoss;

        /// <summary>移動速度倍率，預設 1。兩關制結尾王的突進技能會暫時調高這個再調回來。</summary>
        public float SpeedMultiplier = 1f;

        // ---- 遠程攻擊（兩關制地圖限定）----
        // C／B 級跟單機測試完全不會設這個，維持原本純近戰的手感。
        bool _ranged;
        int _rangedDamage;
        float _rangedRange;
        float _rangedCooldown;
        float _rangedTimer;

        Vector2 _push;
        float _slowTimer;
        float _slowFactor = 1f;
        float _contactCooldown;

        protected override void Awake()
        {
            base.Awake();
            Active.Add(this);
        }

        void OnDestroy() => Active.Remove(this);

        public void Setup(SVEnemyType type, float hpScale, float damageScale)
        {
            Type = type;
            DisplayName = type.Name;
            SheetName = type.SheetName;
            SheetIndex = type.SheetIndex;
            MaxHp = Hp = Mathf.Max(1, Mathf.RoundToInt(type.Hp * hpScale));
            MoveSpeed = type.Speed;
            ContactDamage = Mathf.Max(1, Mathf.RoundToInt(type.ContactDamage * damageScale));
            XpValue = type.Xp;

            // 沒特別填防禦的敵人，照血量抓一個小值。
            // 減傷是 LoL 那套除法（傷害 × 100/(100+防禦)），所以防禦 60 = 少吃約 37%，
            // 不會有免疫，也不會像以前那樣小數字打王只剩地板的 1 點。
            int auto = Mathf.Clamp(Mathf.RoundToInt(MaxHp * (type.IsBoss ? 0.020f : 0.012f)), 0, 60);
            Def = type.Def >= 0 ? type.Def : auto;
            MDef = type.MDef >= 0 ? type.MDef : auto;

            Radius = type.Radius;
            transform.localScale = Vector3.one * type.Scale;
            _push = Vector2.zero;
            _slowTimer = 0f;
            _slowFactor = 1f;
            _burnLeft = 0f;
            _burnTick = 0f;
            PoisonStacks = 0;
            BleedStacks = 0;
            SpeedMultiplier = 1f;

            // 兩關制地圖：小怪數量少了，但挑一部分改成會放冷箭，
            // 逼玩家別一直站在障礙物後面不動——碰撞傷害同時打個折，換這個回來。
            var game = SVGame.Instance;
            bool twoStage = game != null && game.TwoStageMode;
            if (twoStage && !type.IsBoss)
            {
                ContactDamage = Mathf.Max(1, Mathf.RoundToInt(ContactDamage * 0.7f));
                _ranged = Random.value < 0.35f;
                if (_ranged)
                {
                    _rangedDamage = Mathf.Max(1, Mathf.RoundToInt(ContactDamage * 1.6f));
                    _rangedRange = 7.5f;
                    _rangedCooldown = Random.Range(2.1f, 3.1f);
                    _rangedTimer = Random.Range(0.4f, _rangedCooldown);
                }
            }
            else
            {
                _ranged = false;
            }
        }

        void Update()
        {
            var game = SVGame.Instance;
            if (game == null || game.Player == null || game.Paused) { Velocity = Vector2.zero; return; }

            float dt = Time.deltaTime;

            // 時停之刻：凍住的時候完全不動，也不會碰傷人
            if (game.EnemiesFrozen) { Velocity = Vector2.zero; return; }

            if (_slowTimer > 0f) { _slowTimer -= dt; if (_slowTimer <= 0f) _slowFactor = 1f; }
            UpdateBurn(dt);

            if (_frozenTimer > 0f)
            {
                _frozenTimer -= dt;

                // 凍結中染成冰藍色。受傷閃紅的優先權比較高，那邊會自己蓋回去
                if (Renderer != null)
                    Renderer.color = _frozenTimer > 0f
                        ? new Color(0.55f, 0.8f, 1f)
                        : Color.white;
            }
            if (_contactCooldown > 0f) _contactCooldown -= dt;

            var target = game.Player.Pos;

            if (_ranged && !Fleeing)
            {
                _rangedTimer -= dt;
                if (_rangedTimer <= 0f && (target - Pos).sqrMagnitude <= _rangedRange * _rangedRange)
                {
                    _rangedTimer = _rangedCooldown;
                    FireRanged(target);
                }
            }

            // 首領被打倒之後全部作鳥獸散：往場外跑、不再攻擊，
            // 但還打得死（也還會掉東西），所以玩家可以選擇追殺或收工。
            if (Fleeing)
            {
                var away = ((Vector2)Pos - target).normalized;
                if (away.sqrMagnitude < 0.001f) away = Random.insideUnitCircle.normalized;

                Velocity = away * MoveSpeed * FleeSpeedMul * _slowFactor + _push;
                Pos += Velocity * dt;
                _push = Vector2.Lerp(_push, Vector2.zero, 8f * dt);

                // 跑出場地就當作逃掉了，直接離場
                if (SVConfig.OutsideArena(Pos, 3f)) Escape();
                return;
            }

            var dir = (target - Pos).normalized;

            Velocity = dir * MoveSpeed * _slowFactor * SpeedMultiplier + _push;
            Pos = SVObstacles.ResolveMove(Pos + Velocity * dt, Radius);

            _push = Vector2.Lerp(_push, Vector2.zero, 8f * dt);

            // 碰到玩家
            if (_contactCooldown <= 0f && Overlaps(target, game.Player.Radius))
            {
                int dealtToPlayer = game.Player.TakeDamage(ContactDamage);
                if (dealtToPlayer > 0)
                {
                    game.Player.Retaliate(this, dealtToPlayer);   // 荊棘
                    _contactCooldown = 0.5f;
                    return;
                }
            }

            // 擋在路上的隊友也會被咬。隊友血量歸零就退場，所以站位是有代價的
            if (_contactCooldown > 0f) return;

            for (int i = SVCompanion.Active.Count - 1; i >= 0; i--)
            {
                var c = SVCompanion.Active[i];
                if (c == null || c.Retired) continue;
                if (!Overlaps(c.Pos, c.Radius)) continue;

                // 魔導護盾：只有王打得進去。雜魚撞上去只會被彈開
                if (!c.CanBeHurtBy(this))
                {
                    c.FlashShield();
                    Push(((Vector2)Pos - c.Pos).normalized * 3f);
                    _contactCooldown = 0.35f;
                    break;
                }

                // 走 TakeDamageFrom，護盾才知道打人的是不是王
                if (c.TakeDamageFrom(ContactDamage, this) > 0) _contactCooldown = 0.5f;
                break;
            }
        }

        void FireRanged(Vector2 target)
        {
            var game = SVGame.Instance;
            var dir = (target - Pos).normalized;
            var shot = SVProjectile.Spawn(game.ShotRoot, Pos, dir, new Color(0.85f, 0.35f, 0.3f), 0.6f);
            shot.Team = SVProjTeam.Enemy;
            shot.Damage = _rangedDamage;
            shot.DamageKind = SVDamageKind.Physical;
            shot.Speed = 8.5f;
            shot.Life = 1.6f;
            shot.Radius = 0.3f;
        }

        // ---------------- 逃跑 ----------------

        /// <summary>正在逃跑。往場外跑、不再造成接觸傷害。</summary>
        public bool Fleeing { get; private set; }

        /// <summary>逃跑時的速度倍率。比平常快一點，才追得有壓力。</summary>
        public const float FleeSpeedMul = 1.35f;

        public void StartFleeing()
        {
            if (IsDead) return;
            Fleeing = true;

            // 解掉凍結與減速，不然會定在原地不動，看起來像卡住
            _frozenTimer = 0f;
            _slowTimer = 0f;
            _slowFactor = 1f;
            if (Renderer != null) Renderer.color = Color.white;
        }

        /// <summary>跑掉了。不算擊殺、不掉東西，直接從場上消失。</summary>
        void Escape()
        {
            Active.Remove(this);
            Destroy(gameObject);
        }

        /// <summary>讓全場的雜魚開始逃。回傳有幾隻在跑。</summary>
        public static int FleeAll()
        {
            int n = 0;
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var e = Active[i];
                if (e == null || e.IsDead) continue;
                e.StartFleeing();
                n++;
            }
            return n;
        }

        public void Push(Vector2 force) => _push += force;

        // ---------------- 持續傷害（毒／流血／燃燒）----------------
        //
        // 三種規則刻意不一樣，才有各自的用法：
        //   燃燒：不疊層、只有 5 秒，但傷害是「目標最大生命的 2%」——
        //         血越厚燒得越痛，是拿來對付肉的。有每秒上限免得王被燒穿。
        //   毒　：可以無限疊層，而且不會自己退，疊到死為止。單層很小，靠堆量。
        //   流血：可以無限疊層，而且會暴擊 —— 唯一會跳暴擊字的持續傷害。
        //
        // 三種都吃 SVGame.DotMultiplier（持續傷害強化）。

        /// <summary>燃燒每秒造成目標最大生命的幾成。</summary>
        public const float BurnHpRatio = 0.02f;

        /// <summary>燃燒每秒的傷害上限。沒有上限的話，血越高的王被燒得越誇張。</summary>
        public const int BurnPerSecondCap = 50;

        /// <summary>燃燒的持續時間。</summary>
        public const float BurnSeconds = 5f;

        /// <summary>毒每層每秒的傷害。</summary>
        public const float PoisonPerStack = 2f;

        /// <summary>流血每層每秒的傷害。</summary>
        public const float BleedPerStack = 3f;

        /// <summary>持續傷害的結算間隔。</summary>
        const float DotTick = 0.5f;

        float _burnLeft, _burnTick;

        /// <summary>目前的毒層數。不會自己退，疊到死為止。</summary>
        public int PoisonStacks { get; private set; }

        /// <summary>目前的流血層數。可以暴擊。</summary>
        public int BleedStacks { get; private set; }

        /// <summary>燃燒中。</summary>
        public bool Burning => _burnLeft > 0f;

        /// <summary>
        /// 點燃。不疊層 —— 重複點只會把時間刷新，傷害固定照最大生命算。
        /// power 參數留著是為了相容舊呼叫點，現在只當「要不要燒」的旗標。
        /// </summary>
        public void Burn(int power, float duration)
        {
            if (power <= 0) return;
            _burnLeft = Mathf.Max(_burnLeft, duration > 0f ? duration : BurnSeconds);
        }

        /// <summary>點燃，用預設的 5 秒。</summary>
        public void Ignite() => _burnLeft = Mathf.Max(_burnLeft, BurnSeconds);

        /// <summary>下毒。可以無限疊，而且不會自己退。</summary>
        public void Poison(int stacks = 1)
        {
            if (stacks > 0) PoisonStacks += stacks;
        }

        /// <summary>造成流血。可以無限疊，會暴擊。</summary>
        public void Bleed(int stacks = 1)
        {
            if (stacks > 0) BleedStacks += stacks;
        }

        /// <summary>
        /// 套用卡片的技能附魔（流血／毒／燃燒）。
        /// 由 DamageInRadius 統一呼叫 —— 男主與隊友的範圍技能都走那條路，
        /// 所以一張卡就能讓整套技能都帶上同一種持續傷害。
        /// </summary>
        public void ApplySkillEnchants()
        {
            var game = SVGame.Instance;
            if (game == null) return;

            if (game.SkillBleedStacks > 0) Bleed(game.SkillBleedStacks);
            if (game.SkillPoisonStacks > 0) Poison(game.SkillPoisonStacks);
            if (game.SkillIgnites) Ignite();
        }

        /// <summary>把身上的毒清掉並回報清掉幾層。毒爆用這個。</summary>
        public int ConsumePoison()
        {
            int had = PoisonStacks;
            PoisonStacks = 0;
            return had;
        }

        void UpdateBurn(float dt)
        {
            if (_burnLeft > 0f) _burnLeft -= dt;

            // 毒與流血不會自己退，所以只要身上有層數就得一直跑
            if (_burnLeft <= 0f && PoisonStacks <= 0 && BleedStacks <= 0) return;

            _burnTick -= dt;
            if (_burnTick > 0f) return;
            _burnTick = DotTick;

            float bonus = SVGame.Instance != null ? SVGame.Instance.DotMultiplier : 1f;

            // ---- 燃燒：最大生命的 2%／秒，有每秒上限 ----
            if (_burnLeft > 0f)
            {
                float perSecond = Mathf.Min(BurnPerSecondCap, MaxHp * BurnHpRatio);
                TakeDamage(Mathf.Max(1, Mathf.RoundToInt(perSecond * DotTick * bonus)), SVDamageKind.True);
                if (IsDead) return;
            }

            // ---- 毒：每層一點點，靠疊層堆出來 ----
            if (PoisonStacks > 0)
            {
                SVHit.Next = SVHitSource.Dot;   // 毒：全能吸血吃得到
                TakeDamage(Mathf.Max(1, Mathf.RoundToInt(PoisonStacks * PoisonPerStack * DotTick * bonus)),
                           SVDamageKind.True);
                if (IsDead) return;
            }

            // ---- 流血：每層一點點，而且會暴擊 ----
            if (BleedStacks > 0)
            {
                float raw = BleedStacks * BleedPerStack * DotTick * bonus;

                var player = SVGame.Instance?.Player;
                bool crit = player != null && player.RollCrit();
                if (crit) raw *= player.CritMultiplier;

                SVHit.Next = SVHitSource.Dot;   // 流血：全能吸血吃得到
                TakeDamage(Mathf.Max(1, Mathf.RoundToInt(raw)), SVDamageKind.True);

                if (crit && !IsDead)
                {
                    SVCrit.Popup(Pos, new Color(1f, 0.4f, 0.45f));
                    SVCritShake.Play(gameObject);
                }
            }
        }

        public void Slow(float factor, float duration)
        {
            _slowFactor = Mathf.Min(_slowFactor, factor);
            _slowTimer = Mathf.Max(_slowTimer, duration);
        }

        float _frozenTimer;

        /// <summary>凍結：完全停住，而且會被染成冰藍色。</summary>
        public void Freeze(float duration)
        {
            _frozenTimer = Mathf.Max(_frozenTimer, duration);
            Slow(0f, duration);
        }

        public bool IsFrozen => _frozenTimer > 0f;

        bool _exploding;

        public override int TakeDamage(int amount, SVDamageKind kind = SVDamageKind.Physical)
        {
            var game = SVGame.Instance;
            var src = SVHit.Take();   // 這一下是普攻／技能／其他（見 SVDamageBonus.cs）
            bool boss = Type != null && Type.IsBoss;
            int raw = amount;
            var bonus = default(SVDamageBonuses);
            if (game != null && game.Passives != null)
            {
                // 四種傷害加成統一在這裡乘：同類相加、不同類相乘。
                // 通用與 BOSS 全部都吃（普攻、男主技能、隊友技能、持續傷害）；普攻、技能只吃自己那一類
                bonus = SVDamageBonus.For(game, kind, boss, src);
                float mul = Mathf.Max(0f, bonus.Multiplier);
                if (!Mathf.Approximately(mul, 1f)) amount = Mathf.RoundToInt(amount * mul);
            }
            int afterBonus = amount;

            // 防禦。真傷（灼燒、毒、場地）直接跳過這一段
            if (kind == SVDamageKind.Physical) amount = SVDamageMath.Apply(amount, Def);
            else if (kind == SVDamageKind.Magic) amount = SVDamageMath.Apply(amount, MDef);

            int dealt = base.TakeDamage(amount, kind);
            if (dealt > 0 && game != null) game.OnDamageDealtToEnemy(dealt, src);
            if (dealt > 0)
            {
                SVDamageLog.Record(this, src, SVHit.AttackerFor(src), kind, raw, bonus, afterBonus, dealt);
                if (boss) SVDamageNumber.Spawn(Pos, dealt, SVHit.LastCrit, kind);
            }
            SVHit.LastCrit = false;

            // 收集者：這一下沒打死，但打進處決門檻的話有機率直接收掉
            if (dealt > 0 && !IsDead) TryExecute(game);

            return dealt;
        }

        /// <summary>
        /// 收集者的處決。
        ///
        /// 一般敵人看絕對血量（低於 50 就進入範圍），王看百分比（30% 以下）——
        /// 王的血是好幾千，用絕對值判斷的話永遠碰不到，那條規則等於不存在。
        /// 暴擊的那一下機率再乘 1.5。
        /// </summary>
        void TryExecute(SVGame game)
        {
            if (game == null || game.Awakenings == null || !game.Awakenings.Collecting) return;

            bool boss = Type != null && Type.IsBoss;

            bool inRange = boss
                ? Hp <= MaxHp * SVAwakenState.ExecuteBossHpRatio
                : Hp <= game.Awakenings.ExecuteHpThreshold;
            if (!inRange) return;

            float chance = boss ? game.Awakenings.ExecuteBossChance : game.Awakenings.ExecuteChance;

            var player = game.Player;
            if (player != null && player.RollCrit()) chance *= SVAwakenState.ExecuteCritBonus;

            if (Random.value >= chance) return;

            game.SpawnPulse(Pos, 2.2f, new Color(1f, 0.35f, 0.4f, 0.85f), 0.3f);
            SVCrit.Popup(Pos, new Color(1f, 0.3f, 0.35f));
            TakeDamage(Mathf.Max(1, Hp), SVDamageKind.True);
        }

        // 教學關卡的掉落砍到一小部分：機率乘 0.35，數量乘 0.5（至少 1），
        // 不然教學關殺沒幾隻怪就滿地是東西，反而干擾在教「撿東西」這個概念本身。
        const float TutorialLootChanceMul = 0.35f;
        const float TutorialLootAmountMul = 0.5f;

        void DropLoot(SVGame game)
        {
            if (game == null || Type == null) return;

            // 露娜教學：怪物不掉道具也不掉金幣，報酬只有結算時的初心者 5 件套＋1000G
            if (game.IsTutorial) return;

            float bonus = game.LootChanceMultiplier;
            float amountMul = game.LootAmountMultiplier;
            if (game.IsTutorial)
            {
                bonus *= TutorialLootChanceMul;
                amountMul *= TutorialLootAmountMul;
            }

            if (Type.GoldMax > 0 && Random.value < Type.GoldChance * bonus)
            {
                int gold = Random.Range(Type.GoldMin, Mathf.Max(Type.GoldMin, Type.GoldMax) + 1);
                gold = Mathf.Max(1, Mathf.RoundToInt(gold * amountMul * game.Bond.GoldMul));   // 羈絆：克萊爾
                // 金幣不再掉在地上（滿地金幣很吃效能），擊殺時直接入袋
                game.Loot.Add(SVLootKind.Gold, 0, gold);
            }

            if (Type.Drops == null) return;

            for (int i = 0; i < Type.Drops.Count; i++)
            {
                var d = Type.Drops[i];
                if (d == null || d.Kind == SVLootKind.Gold) continue;
                if (Random.value >= d.Chance * bonus * game.Bond.ItemDropMul * game.RestMul) continue;   // 羈絆：尤梨卡

                int amount = game.IsTutorial ? Mathf.Max(1, Mathf.RoundToInt(d.Roll() * amountMul)) : d.Roll();
                SVLootDrop.Spawn(game.GemRoot, Pos, d.Kind, d.ItemId, amount);
            }
        }

        protected override void OnDeath()
        {
            var game = SVGame.Instance;
            DropLoot(game);

            // 技能書只有王身上掉得出來
            if (game != null && Type != null && Type.IsBoss) game.TryDropSkillBook(Pos);

            // 卡片：雜魚偶爾掉，週期王必掉。
            // 尾王不掉 —— 牠一死就進結算，卡片撿了也沒地方用，只會擋在收尾流程前面
            if (game != null && !IsFinalBoss)
                game.TryDropCard(Pos, Type != null && Type.IsBoss);

            // 爆裂連鎖。加旗標避免連環爆炸無限遞迴
            if (game != null && !_exploding && game.Awakenings.DetonatePower > 0)
            {
                _exploding = true;
                game.SpawnPulse(Pos, game.Awakenings.DetonateRadius,
                                new Color(1f, 0.55f, 0.25f, 0.7f), 0.3f);
                DamageInRadius(Pos, game.Awakenings.DetonateRadius,
                               game.Awakenings.DetonatePower, 2f, this);
            }

            game?.OnEnemyKilled(this);

            // 王倒下不要直接消失：留一個殘影，閃白 → 變暗紅 → 淡出
            if (Type != null && Type.IsBoss && Renderer != null && Renderer.sprite != null)
                SVDeathFade.Spawn(this, Renderer);

            Destroy(gameObject);
        }

        public static SVEnemy NearestTo(Vector2 point, float maxDistance = float.MaxValue)
        {
            SVEnemy best = null;
            float bestSq = maxDistance * maxDistance;
            for (int i = 0; i < Active.Count; i++)
            {
                var e = Active[i];
                if (e == null || e.IsDead) continue;
                float sq = ((Vector2)e.Pos - point).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = e; }
            }
            return best;
        }

        /// <summary>範圍傷害，回傳打到幾隻。</summary>
        /// <summary>
        /// 範圍傷害，回傳打到幾隻。
        /// bossMultiplier 只作用在王身上 —— 有些角色是專門用來拆王的。
        /// </summary>
        /// <summary>同 DamageInRadius，但標明傷害來源（男主技能用 SVHitSource.Skill）。</summary>
        public static int DamageInRadiusAs(SVHitSource source, Vector2 center, float radius, int damage,
                                           float knockback = 0f, SVEnemy ignore = null,
                                           float bossMultiplier = 1f,
                                           SVDamageKind kind = SVDamageKind.Physical)
        {
            _areaSource = source;
            try { return DamageInRadius(center, radius, damage, knockback, ignore, bossMultiplier, kind); }
            finally { _areaSource = SVHitSource.Other; }
        }
        static SVHitSource _areaSource = SVHitSource.Other;

        public static int DamageInRadius(Vector2 center, float radius, int damage,
                                         float knockback = 0f, SVEnemy ignore = null,
                                         float bossMultiplier = 1f,
                                         SVDamageKind kind = SVDamageKind.Physical)
        {
            int count = 0;
            // 擊殺可能觸發連鎖爆炸，同一批目標必須使用快照。
            foreach (var e in Active.ToArray())
            {
                if (e == null || e.IsDead || e == ignore) continue;
                if (!e.Overlaps(center, radius)) continue;
                int dealt = damage;
                if (bossMultiplier != 1f && e.Type != null && e.Type.IsBoss)
                    dealt = Mathf.RoundToInt(damage * bossMultiplier);

                SVHit.Next = _areaSource;
                e.TakeDamage(dealt, kind);

                // 技能附魔：卡片給的流血／毒／燃燒，對所有走這條路的技能一起生效。
                // 真傷（持續傷害自己的跳）不再附加，不然毒會自己養毒、無限增生
                if (kind != SVDamageKind.True && !e.IsDead) e.ApplySkillEnchants();

                if (knockback > 0f) e.Push(((Vector2)e.Pos - center).normalized * knockback);
                count++;
            }
            return count;
        }
    }

    /// <summary>王的死亡殘影：複製當下的樣子，閃一下白、轉成暗紅，邊往上飄邊淡出。</summary>
    public class SVDeathFade : MonoBehaviour
    {
        public const float Duration = 1.6f;
        SpriteRenderer _sr;
        float _t;
        Vector3 _start;

        public static void Spawn(SVEnemy from, SpriteRenderer source)
        {
            var go = new GameObject("BossDeathFade");
            go.transform.SetParent(from.transform.parent, false);
            go.transform.position = from.transform.position;
            go.transform.localScale = from.transform.localScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = source.sprite;
            sr.flipX = source.flipX;
            sr.visualOffset = source.visualOffset;
            sr.visualScale = source.visualScale;
            sr.sortingOrder = source.sortingOrder;
            sr.color = Color.white;

            var fx = go.AddComponent<SVDeathFade>();
            fx._sr = sr;
            fx._start = go.transform.position;
        }

        void Update()
        {
            var game = SVGame.Instance;
            if (game != null && game.Paused && !game.Talking) return;   // 暫停選單時停住；王倒下的對話期間照樣淡出
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / Duration);

            // 0~0.2：閃白（亮到發光）；之後轉暗紅；後半段淡出
            Color c;
            if (k < 0.12f) c = Color.Lerp(Color.white, new Color(2f, 2f, 2f, 1f), k / 0.12f);
            else c = Color.Lerp(new Color(1.6f, 0.6f, 0.6f, 1f), new Color(0.35f, 0.1f, 0.2f, 1f), (k - 0.12f) / 0.88f);
            c.a = k < 0.45f ? 1f : 1f - (k - 0.45f) / 0.55f;
            _sr.color = c;

            // 慢慢往上飄、稍微縮小
            transform.position = _start + new Vector3(0f, k * 0.6f, 0f);
            _sr.visualScale = new Vector2(1f - k * 0.15f, 1f - k * 0.15f);

            if (_t >= Duration) Destroy(gameObject);
        }
    }
}
