// 場上所有會動的東西的共同底層：玩家、隊友、敵人。
// 沒有用 Unity 的物理，碰撞一律用距離判斷 —— 這種遊戲同畫面幾百隻，
// 距離判斷比 Rigidbody 快太多，行為也好預測。
using RMZS.GodotPort;

namespace RMZS
{
    public class SVUnit : MonoBehaviour
    {
        public string DisplayName = "";
        public string SheetName = "小人1";
        public int SheetIndex = 0;

        public float MoveSpeed = 3f;
        public int MaxHp = 10;
        public int Hp = 10;

        /// <summary>碰撞半徑，用來判斷打不打得到。</summary>
        public float Radius = 0.4f;

        public bool IsDead => Hp <= 0;

        protected SpriteRenderer Renderer;
        /// <summary>這一幀的移動量，走路動畫靠它判斷方向與節奏。</summary>
        public Vector2 Velocity;

        int _dir = 2;
        int _pattern = 1;
        public int FacingDirection => _dir;
        public int WalkPattern => _pattern == 3 ? 1 : _pattern;
        float _stepTimer;
        float _flashTimer;

        public Vector2 Pos
        {
            get => transform.position;
            set => transform.position = new Vector3(value.x, value.y, 0f);
        }

        protected virtual void Awake()
        {
            Renderer = gameObject.GetComponent<SpriteRenderer>();
            if (Renderer == null) Renderer = gameObject.AddComponent<SpriteRenderer>();
            Renderer.sortingLayerName = "Default";
        }

        protected virtual void LateUpdate()
        {
            UpdateAnimation(Time.deltaTime);
            Renderer.sortingOrder = SVConfig.SortingOrderFor(transform.position.y);

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                float t = Mathf.PingPong(_flashTimer * 20f, 1f);
                Renderer.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.35f), t);
            }
            else if (Renderer.color != Color.white)
            {
                Renderer.color = Color.white;
            }
        }

        void UpdateAnimation(float dt)
        {
            if(this is SVEnemy enemy && enemy.Type != null && SVVisualAssets.Monster(enemy.Type, _dir == 8) is Sprite monster){
                Renderer.sprite = monster;
                Renderer.flipX = Velocity.x < -0.05f;
                if(Velocity.y > 0.05f) _dir=8; else if(Velocity.y < -0.05f) _dir=2;
                float beat=Mathf.Sin(Time.time*7f + gameObject.Node.GetInstanceId()%97);
                Renderer.visualOffset = new Vector2(0, -2f-Mathf.Abs(beat)*3f);
                Renderer.visualScale = new Vector2(1f+beat*.025f,1f-beat*.035f);
                return;
            }
            float speed = Velocity.magnitude;
            if (speed > 0.05f)
            {
                // 走路方向：橫向優先，跟 RM 的手感一致
                _dir = Mathf.Abs(Velocity.x) > Mathf.Abs(Velocity.y)
                    ? (Velocity.x > 0 ? 6 : 4)
                    : (Velocity.y > 0 ? 8 : 2);

                _stepTimer += dt * speed * 2.2f;
                if (_stepTimer >= 1f)
                {
                    _stepTimer = 0f;
                    _pattern = (_pattern + 1) % 4;
                }
            }
            else
            {
                _stepTimer = 0f;
                _pattern = 1;
            }

            // 0,1,2,1 的循環，MZ 的走路節奏
            int p = _pattern == 3 ? 1 : _pattern;
            var sprite = SVSpriteSheet.Get(SheetName, SheetIndex, _dir, p);
            if (sprite != null) Renderer.sprite = sprite;
        }

        public void FaceTowards(Vector2 target)
        {
            var d = target - Pos;
            _dir = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x > 0 ? 6 : 4) : (d.y > 0 ? 8 : 2);
        }

        /// <summary>
        /// 回傳實際扣掉的血量。
        /// kind 不填就是物理 —— 舊的呼叫點（撞擊、斬擊那些）不用改。
        /// </summary>
        public virtual int TakeDamage(int amount, SVDamageKind kind = SVDamageKind.Physical)
        {
            if (SVGame.Instance?.IsTutorial == true && (this is SVPlayer || this is SVCompanion)) return 0;
            if (IsDead || amount <= 0) return 0;
            int before = Hp;
            Hp = Mathf.Max(0, Hp - amount);
            _flashTimer = 0.18f;
            if (Hp <= 0) OnDeath();
            return before - Hp;
        }

        public virtual int Heal(int amount)
        {
            if (IsDead || amount <= 0) return 0;
            int before = Hp;
            Hp = Mathf.Min(MaxHp, Hp + amount);
            return Hp - before;
        }

        protected virtual void OnDeath() { }

        /// <summary>圓對圓的碰撞判斷。</summary>
        public bool Overlaps(Vector2 point, float radius) =>
            (Pos - point).sqrMagnitude <= (Radius + radius) * (Radius + radius);
    }
}
