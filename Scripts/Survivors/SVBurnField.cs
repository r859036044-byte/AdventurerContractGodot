// 灼燒地帶：留在地上的一團火，每隔一段時間對站在裡面的人造成傷害。
//
// 敵我不分 —— 鈴音的星隕落點連男主一起燒，只是對男主的傷害刻意壓得很低，
// 目的是逼玩家「不要站在落點上」，而不是真的把他燒死。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public class SVBurnField : MonoBehaviour
    {
        public float Radius = 3f;
        public float Life = 4f;
        public float TickInterval = 0.5f;

        /// <summary>每一跳對敵人的傷害。</summary>
        public int EnemyDamage = 8;

        /// <summary>每一跳對男主的傷害。0 = 不燒自己人。</summary>
        public int PlayerDamage = 2;

        /// <summary>王身上的傷害倍率。</summary>
        public float BossMultiplier = 1f;

        /// <summary>
        /// 場上同時最多幾塊火。星隕一次留兩塊、又燒 10 秒，
        /// 不設上限的話整場會鋪滿半透明的大圓，白白吃掉填充率。
        /// </summary>
        public const int MaxFields = 4;

        public static readonly List<SVBurnField> Active = new List<SVBurnField>();

        float _left;
        float _tick;

        SpriteRenderer _fire;
        Color _baseColor;
        float _seed;
        float _scaleBasis;

        /// <summary>
        /// 燒到最後還剩幾成傷害。
        /// 火場會慢慢燒盡 —— 剛落地最痛，之後遞減到這個比例。
        /// </summary>
        public const float BurnoutRatio = 0.25f;

        /// <summary>目前的傷害倍率（1 = 剛點著，BurnoutRatio = 快熄了）。</summary>
        public float Intensity => Mathf.Lerp(BurnoutRatio, 1f, Mathf.Clamp01(_left / Mathf.Max(0.01f, Life)));

        public static SVBurnField Spawn(Transform parent, Vector2 pos, float radius, float life,
                                        int enemyDamage, int playerDamage, Color color)
        {
            var go = new GameObject("BurnField");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            // 超過上限就把最舊的那塊收掉
            for (int i = Active.Count - 1; i >= 0; i--)
                if (Active[i] == null) Active.RemoveAt(i);

            while (Active.Count >= MaxFields)
            {
                var oldest = Active[0];
                Active.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }

            var field = go.AddComponent<SVBurnField>();
            Active.Add(field);
            field.Radius = radius;
            field.Life = life;
            field.EnemyDamage = enemyDamage;
            field.PlayerDamage = playerDamage;
            field.Setup(color);
            return field;
        }

        void OnDestroy() { Active.Remove(this); }

        void Setup(Color color)
        {
            _left = Life;
            _baseColor = color;
            _seed = Random.value * 100f;

            var art = SVArt.BurnGround;

            // 讀不到圖的時候退回一個「暗橘色的圓」，而不是照著貼圖用的近白色去染 ——
            // 近白色配上程式生成的柔邊圓盤，畫面上會變成一團刺眼的白霧。
            if (art == null) _baseColor = new Color(1f, 0.45f, 0.15f, 0.34f);

            var go = new GameObject("Fire");
            go.transform.SetParent(transform, false);

            _fire = go.AddComponent<SpriteRenderer>();
            _fire.sprite = art != null ? art : SVShapes.Circle();
            _fire.sortingOrder = -20000;   // 畫在角色底下，不擋視線

            // 圖已經裁成圓盤的外接方框，所以「直徑 = 圖寬」，縮放直接照半徑算
            _scaleBasis = art != null
                ? Radius * 2f / Mathf.Max(0.001f, art.bounds.size.x)
                : Radius * 2f;

            // 每一塊火的角度隨機一次就定下來，之後不再轉 ——
            // 地面上的火在轉會很怪，但每塊的花紋不一樣才不會看出是同一張圖
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            go.transform.localScale = Vector3.one * _scaleBasis;
        }

        void Update()
        {
            var game = SVGame.Instance;
            if (game == null) { Destroy(gameObject); return; }
            if (game.Paused) return;

            float dt = Time.deltaTime;
            _left -= dt;

            if (_left <= 0f) { Destroy(gameObject); return; }

            UpdateLook(dt);

            _tick -= dt;
            if (_tick > 0f) return;
            _tick = TickInterval;

            // 火會慢慢燒盡：傷害隨剩餘時間遞減，最後只剩兩成半。
            // 站在剛落地的火裡最痛，拖到後面就只是煩人而已
            float intensity = Intensity;

            int enemyHit = Mathf.RoundToInt(EnemyDamage * intensity);
            if (EnemyDamage > 0 && enemyHit > 0)
                SVEnemy.DamageInRadius(transform.position, Radius, enemyHit, 0f, null, BossMultiplier);

            // 男主站在火裡也會被燒，但傷害是另外一個（很小的）數字
            var player = game.Player;
            if (PlayerDamage > 0 && player != null && !player.IsDead)
            {
                int playerHit = Mathf.Max(1, Mathf.RoundToInt(PlayerDamage * intensity));
                float d = Vector2.Distance(player.Pos, (Vector2)transform.position);
                if (d <= Radius + player.Radius) player.TakeBurn(playerHit);
            }
        }

        /// <summary>
        /// 燒起來的樣子：不旋轉（地上的火在轉很怪），只有呼吸與閃爍。
        /// 閃爍用兩個不同頻率的正弦疊起來，才不會看出固定的節拍。
        /// 亮度跟著傷害一起衰減，所以玩家看得出來這塊火快沒力了。
        /// </summary>
        void UpdateLook(float dt)
        {
            // 剛落地的半秒竄一下，看起來像被點著
            float ignite = Mathf.Clamp01((Life - _left) / 0.45f);

            // 最後一段整團淡出，玩家看得出來還剩多久
            float fade = Mathf.Clamp01(_left / Mathf.Max(0.01f, Life * 0.35f));

            float t = Time.time + _seed;
            float flicker = 0.78f + 0.22f * Mathf.Sin(t * 6.1f)
                                  * (0.65f + 0.35f * Mathf.Sin(t * 2.7f));

            // Intensity 從 1 掉到 BurnoutRatio，直接拿來當亮度的底，
            // 畫面上的「快熄了」跟實際傷害是同一件事
            float strength = Mathf.Lerp(0.45f, 1f, Intensity);
            float alpha = _baseColor.a * ignite * fade * flicker * strength;

            _fire.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b,
                                    Mathf.Clamp01(alpha));

            // 呼吸：只有大小，不轉
            _fire.transform.localScale =
                Vector3.one * _scaleBasis * (1f + 0.03f * Mathf.Sin(t * 3.4f));
        }
    }
}
