// 掉在地上的戰利品。跟經驗球一樣會被玩家吸過去，撿到就進戰利品袋。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public class SVLootDrop : MonoBehaviour
    {
        public static readonly List<SVLootDrop> Active = new List<SVLootDrop>();

        public SVLootKind Kind;
        public int ItemId;
        public int Count = 1;

        SpriteRenderer _renderer;
        bool _pulled;
        float _bob;

        /// <summary>
        /// 金幣圖的縮放。圖是 256px、PPU 100，所以 1 倍等於 2.5 格 —— 太大了。
        /// 0.16 大約是半格，跟男主的腳差不多寬。
        /// </summary>
        public const float CoinScale = 0.16f;

        public static SVLootDrop Spawn(Transform parent, Vector2 pos, SVLootKind kind, int itemId, int count)
        {
            var go = new GameObject("Loot");
            go.transform.SetParent(parent, false);

            // 散開一點，免得一堆東西疊在同一格看不出來
            go.transform.position = pos + Random.insideUnitCircle * 0.35f;

            var drop = go.AddComponent<SVLootDrop>();
            drop.Kind = kind;
            drop.ItemId = itemId;
            drop.Count = Mathf.Max(1, count);

            bool gold = kind == SVLootKind.Gold;
            var coin = gold ? SVArt.Coin : null;

            // 圖放在子物件上。上下浮動改的是子物件的 localPosition，
            // 根物件的座標留給吸取邏輯用 —— 兩邊混在同一個 transform 上，
            // 浮動會把掉落物的位置一起改掉（原本就是這個 bug，東西會飄到場地中央）。
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(go.transform, false);

            var sr = iconGo.AddComponent<SpriteRenderer>();
            sr.sprite = coin != null ? coin : (gold ? SVShapes.Circle() : SVShapes.Square());

            // 用真的金幣圖時不要再染色，不然金屬的層次會被蓋掉
            sr.color = coin != null ? Color.white : ColorFor(kind);
            sr.sortingOrder = 120;
            drop._renderer = sr;

            iconGo.transform.localScale = Vector3.one *
                (gold ? (coin != null ? CoinScale : 0.22f) : 0.3f);

            // 金幣底下墊一層柔邊光暈。地圖是手繪的土黃色，
            // 金幣直接放上去會糊成一團，墊一層就跳出來了。
            if (coin != null)
            {
                var haloGo = new GameObject("Halo");
                haloGo.transform.SetParent(go.transform, false);
                haloGo.transform.localScale = Vector3.one * 1.9f;

                var halo = haloGo.AddComponent<SpriteRenderer>();
                halo.sprite = SVShapes.Soft();
                halo.color = new Color(1f, 0.85f, 0.35f, 0.30f);
                halo.sortingOrder = sr.sortingOrder - 1;
            }

            return drop;
        }

        static Color ColorFor(SVLootKind kind) => kind switch
        {
            SVLootKind.Gold => new Color(1f, 0.85f, 0.25f),
            SVLootKind.Weapon => new Color(1f, 0.55f, 0.45f),
            SVLootKind.Armor => new Color(0.6f, 0.8f, 1f),
            _ => new Color(0.7f, 1f, 0.7f),
        };

        /// <summary>浮動的速度與高度。整批一起改就改這兩個。</summary>
        public const float BobSpeed = 3.2f;
        public const float BobHeight = 0.14f;

        float _bobPhase;

        void Awake()
        {
            // 每一顆的相位錯開，不然滿地的金幣會整齊劃一地一起上下跳
            _bobPhase = Random.value * Mathf.PI * 2f;
            Active.Add(this);
        }
        void OnDestroy() => Active.Remove(this);

        void Update()
        {
            var game = SVGame.Instance;
            if (game == null || game.Player == null || game.Paused) return;

            // 沒被吸走之前上下浮動，比較看得見
            _bob += Time.deltaTime * BobSpeed;
            if (_renderer != null)
                _renderer.transform.localPosition =
                    new Vector3(0f, Mathf.Sin(_bob + _bobPhase) * BobHeight, 0f);

            var player = game.Player;
            float dist = Vector2.Distance(transform.position, player.Pos);

            // 戰利品的拾取範圍比經驗球大一點，撿東西不該變成苦差事
            if (!_pulled && dist <= game.PickupRadius * 1.4f) _pulled = true;

            if (!_pulled) return;

            transform.position = Vector2.MoveTowards(transform.position, player.Pos,
                                                     Mathf.Max(7f, dist * 9f) * Time.deltaTime);

            if (dist < 0.4f)
            {
                game.CollectLoot(Kind, ItemId, Count);
                Destroy(gameObject);
            }
        }

        public static void PullAll()
        {
            for (int i = 0; i < Active.Count; i++) Active[i]._pulled = true;
        }

        /// <summary>撤退／結束時把還在地上的東西一次收走（走都走了，順手撿）。</summary>
        public static void CollectAllRemaining()
        {
            var game = SVGame.Instance;
            if (game == null) return;

            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var d = Active[i];
                if (d == null) continue;
                game.CollectLoot(d.Kind, d.ItemId, d.Count);
                Destroy(d.gameObject);
            }
            Active.Clear();
        }
    }
}
