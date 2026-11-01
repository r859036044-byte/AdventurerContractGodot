// 飛行道具：普攻彈、尤梨卡的追蹤彈、鈴音的斬擊波都用這一支。
// 用物件池，因為這種遊戲一分鐘會生出上千顆。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    /// <summary>子彈是誰射的。玩家／隊友這邊打的找 SVEnemy.Active，敵人射的打玩家。</summary>
    public enum SVProjTeam { Player, Enemy }

    public class SVProjectile : MonoBehaviour
    {
        public static readonly List<SVProjectile> Active = new List<SVProjectile>();
        static readonly Stack<SVProjectile> Pool = new Stack<SVProjectile>();

        public int Damage = 1;

        /// <summary>誰射的。兩關制地圖裡有些小怪跟王會遠程攻擊，用這個分邊。</summary>
        public SVProjTeam Team = SVProjTeam.Player;

        /// <summary>物理還是魔法。男主拿法杖時普攻會變成魔法。</summary>
        public SVDamageKind DamageKind = SVDamageKind.Physical;
        public float Speed = 10f;
        public float Life = 2f;
        public float Radius = 0.3f;
        public int Pierce = 0;              // 還能穿幾隻
        public float BossMultiplier = 1f;   // 只作用在王身上的傷害倍率
        public SVHitSource Source = SVHitSource.Other;   // 普攻／技能／其他（決定吃哪一類傷害加成）
        public string Attacker;                          // 誰射的（傷害 LOG 用，生成時記下）
        public float ScorchInterval;                     // >0：飛行時每隔這麼久在地上留焦痕（火箭彈）
        public int ScorchDamage;
        float _scorchTimer;
        public int BurnPower = 0;           // 命中後附加的持續傷害
        public float BurnSeconds = 3f;      // 那段持續傷害有多久（毒比火久）
        public float Knockback = 0f;

        // ---- 附魔。法杖的元素強化與劍的流血都走這幾個欄位 ----
        [Tooltip("冰：命中後把移動速度壓到這個比例。0 = 不減速")]
        public float SlowFactor = 0f;
        public float SlowSeconds = 0f;

        [Tooltip("冰：直接凍住的機率")]
        public float FreezeChance = 0f;
        public float FreezeSeconds = 0.8f;

        [Tooltip("火：命中點的小範圍爆炸")]
        /// <summary>這一發是暴擊。命中時敵人頭上會跳「暴擊」並震一下。</summary>
        public bool IsCrit;

        /// <summary>命中時附加幾層毒（不會退，疊到死）。</summary>
        public int PoisonStacks;

        /// <summary>命中時附加幾層流血（會暴擊的持續傷害）。</summary>
        public int BleedStacks;

        /// <summary>命中時是否點燃（燃燒：最大生命 2%／秒，5 秒，不疊層）。</summary>
        public bool Ignites;

        public float SplashRadius = 0f;
        public int SplashDamage = 0;
        public SVUnit Homing;               // 追蹤目標，null 就是直線
        public float HomingTurn = 6f;

        /// <summary>圖片跟著飛行方向轉。劍氣這種有方向性的圖要打開。</summary>
        public bool AlignToDirection;

        /// <summary>純特效，不判定碰撞。近戰的劍氣是這種。</summary>
        public bool Harmless;

        /// <summary>圖片本身朝右時的角度修正（度）。</summary>
        public float SpriteAngleOffset;

        Vector2 _dir;
        float _life;
        SpriteRenderer _renderer;
        bool _usesSpell;
        readonly HashSet<SVUnit> _hit = new HashSet<SVUnit>();

        public static SVProjectile Spawn(Transform parent, Vector2 pos, Vector2 dir, Color color, float size) =>
            Spawn(parent, pos, dir, color, size, null, false);

        /// <summary>
        /// sprite 傳 null 就是原本的光球；傳圖的話 align 決定要不要跟著方向轉。
        /// 圖請畫成「朝右」，其他角度靠旋轉，不用另外出圖。
        /// </summary>
        public static SVProjectile Spawn(Transform parent, Vector2 pos, Vector2 dir, Color color, float size,
                                         Sprite sprite, bool align)
        {
            while(Pool.Count>0 && Pool.Peek()==null)Pool.Pop();
            SVProjectile p = Pool.Count > 0 ? Pool.Pop() : Create(parent);
            if(p.transform.parent!=parent)p.transform.SetParent(parent,false);
            p.gameObject.SetActive(true);
            p.transform.position = pos;
            p._dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            p._life = 0f;
            p._hit.Clear();
            p.Team = SVProjTeam.Player;
            p.Homing = null;
            p.Pierce = 0;
            p.BossMultiplier = 1f;
            p.Source = SVHitSource.Other;
            p.Attacker = SVHit.CurrentActor();
            p.ScorchInterval = 0f;
            p.ScorchDamage = 0;
            p._scorchTimer = 0f;
            p.BurnPower = 0;
            p.BurnSeconds = 3f;
            p.Knockback = 0f;
            p.SlowFactor = p.SlowSeconds = 0f;
            p.FreezeChance = 0f;
            p.FreezeSeconds = 0.8f;
            p.IsCrit = false;
            p.PoisonStacks = 0;
            p.BleedStacks = 0;
            p.Ignites = false;
            p.SplashRadius = 0f;
            p.SplashDamage = 0;
            p._renderer.color = color;
            p._usesSpell = sprite == null;
            p._renderer.sprite = sprite != null ? sprite : SVVisualAssets.Spell("Magic Orb");
            p.transform.localScale = Vector3.one * size;

            p.Harmless = false;
            p.AlignToDirection = align && sprite != null;
            p.SpriteAngleOffset = 0f;
            p.ApplyRotation();

            Active.Add(p);
            return p;
        }

        /// <summary>爆擊之類的要換個顏色，玩家才看得出來。</summary>
        public void Tint(Color color) { if (_renderer != null) _renderer.color = color; }

        static SVProjectile Create(Transform parent)
        {
            var go = new GameObject("Shot");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<SVProjectile>();
            p._renderer = go.AddComponent<SpriteRenderer>();
            p._renderer.sprite = SVShapes.Circle();
            p._renderer.sortingOrder = 20000;
            return p;
        }

        void Update()
        {
            // 時停：停在半空。連壽命都不推進，所以不會在定格期間憑空消失 ——
            // 時間恢復之後它們會從原地繼續飛出去，那一下才是這招最爽的地方
            if (SVTimeStop.WorldFrozen) return;

            if(_usesSpell){_renderer.sprite=SVVisualAssets.Spell(Team==SVProjTeam.Enemy?"Fireball":DamageKind==SVDamageKind.Magic?"Arcane Bolt":"Wind Bolt", Time.time);_renderer.color=Team==SVProjTeam.Enemy?new Color(1f,.45f,.32f):Color.white;}
            float dt = Time.deltaTime;
            _life += dt;
            if (_life >= Life) { Despawn(); return; }

            if (Homing != null && !Homing.IsDead)
            {
                var want = (Homing.Pos - (Vector2)transform.position).normalized;
                _dir = Vector2.Lerp(_dir, want, HomingTurn * dt).normalized;
            }

            transform.position += (Vector3)(_dir * Speed * dt);

            if (ScorchInterval > 0f && ScorchDamage > 0)
            {
                // 照飛過的距離留焦痕：每 0.8 格一段（每段長 1.6 格，前後重疊成一條連續的直線）
                _scorchTimer += Speed * dt;
                while (_scorchTimer >= 0.8f)
                {
                    _scorchTimer -= 0.8f;
                    SVScorchTrail.Drop(transform.position, _dir, ScorchDamage);
                }
            }
            if (AlignToDirection) ApplyRotation();   // 追蹤彈會轉彎，圖也要跟著轉

            if (Harmless) return;   // 純特效，不打人

            // 場地障礙物擋子彈——兩關制地圖裡固定石堆／樹叢會把彈道擋下來
            if (SVObstacles.Blocks(transform.position, Radius)) { Despawn(); return; }

            if (Team == SVProjTeam.Enemy) { UpdateEnemyHit(); return; }

            // 打到誰
            var list = SVEnemy.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (e == null || e.IsDead || _hit.Contains(e)) continue;
                if (!e.Overlaps(transform.position, Radius)) continue;

                _hit.Add(e);
                int dealt = Damage;
                if (BossMultiplier != 1f && e.Type != null && e.Type.IsBoss)
                    dealt = Mathf.RoundToInt(Damage * BossMultiplier);
                SVHit.Next = Source;
                SVHit.LastCrit = IsCrit;
                e.TakeDamage(dealt, DamageKind);

                // 暴擊：頭上跳字＋被打的那隻震一下
                if (IsCrit && !e.IsDead)
                {
                    SVCrit.Popup(e.Pos, new Color(1f, 0.72f, 0.3f));
                    SVCritShake.Play(e.gameObject);
                }

                if (BurnPower > 0) e.Burn(BurnPower, BurnSeconds);
                if (Ignites) e.Ignite();
                if (PoisonStacks > 0) e.Poison(PoisonStacks);
                if (BleedStacks > 0) e.Bleed(BleedStacks);

                // 冰：先減速，再看要不要直接凍住
                if (SlowFactor > 0f && SlowSeconds > 0f) e.Slow(SlowFactor, SlowSeconds);
                if (FreezeChance > 0f && Random.value < FreezeChance) e.Freeze(FreezeSeconds);

                // 火：命中點炸一圈，不會再打到自己剛剛那一隻
                if (SplashRadius > 0f && SplashDamage > 0)
                {
                    SVEnemy.DamageInRadiusAs(Source, transform.position, SplashRadius, SplashDamage,
                                           0f, e, 1f, DamageKind);
                    SVGame.Instance?.SpawnPulse(transform.position, SplashRadius,
                                                new Color(1f, 0.6f, 0.3f, 0.55f), 0.18f);
                }

                if (Knockback > 0f) e.Push(((Vector2)e.Pos - (Vector2)transform.position).normalized * Knockback);

                if (Pierce-- <= 0) { Despawn(); return; }
            }
        }

        /// <summary>敵人／王射出來的子彈只打玩家，簡單一發打中就沒。</summary>
        void UpdateEnemyHit()
        {
            var game = SVGame.Instance;
            var pl = game != null ? game.Player : null;
            if (pl == null || pl.IsDead) return;
            if (!pl.Overlaps(transform.position, Radius)) return;

            pl.TakeDamage(Damage, DamageKind);
            Despawn();
        }

        public void ApplyRotation()
        {
            if (!AlignToDirection) { transform.rotation = Quaternion.identity; return; }

            float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg + SpriteAngleOffset;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        public void Despawn()
        {
            if (!gameObject.activeSelf) return;
            Active.Remove(this);
            gameObject.SetActive(false);
            Pool.Push(this);
        }

        public static void ResetSession(){Active.Clear();Pool.Clear();}

        public static void ClearAll()
        {
            for (int i = Active.Count - 1; i >= 0; i--) Active[i].Despawn();
            Active.Clear();
        }
    }
}
