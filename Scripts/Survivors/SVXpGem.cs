// 經驗寶石。玩家靠近就被吸過去。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public class SVXpGem : MonoBehaviour
    {
        public static readonly List<SVXpGem> Active = new List<SVXpGem>();

        public int Value = 1;

        SpriteRenderer _renderer;
        bool _pulled;

        public static SVXpGem Spawn(Transform parent, Vector2 pos, int value)
        {
            // 地上的能量球太多會卡：超過冒險畫質的上限，就把能量併進最近的一顆，數值一點不少
            if (Active.Count >= SVQuality.MaxGems)
            {
                SVXpGem nearest = null;
                float best = float.MaxValue;
                foreach (var g in Active)
                {
                    if (g == null) continue;
                    float d = ((Vector2)g.transform.position - pos).sqrMagnitude;
                    if (d < best) { best = d; nearest = g; }
                }
                if (nearest != null)
                {
                    nearest.Value += value;
                    if (nearest.Value >= 5)
                    {
                        nearest._renderer.color = new Color(1f, 0.8f, 0.25f);
                        nearest.transform.localScale = Vector3.one * 0.26f;
                    }
                    return nearest;
                }
            }

            var go = new GameObject("Xp");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var gem = go.AddComponent<SVXpGem>();
            gem.Value = value;
            gem._renderer = go.AddComponent<SpriteRenderer>();
            gem._renderer.sprite = SVShapes.Circle();
            gem._renderer.color = value >= 5 ? new Color(1f, 0.8f, 0.25f) : new Color(0.4f, 0.9f, 1f);
            gem._renderer.sortingOrder = 100;
            go.transform.localScale = Vector3.one * (value >= 5 ? 0.26f : 0.18f);
            return gem;
        }

        void Awake() => Active.Add(this);
        void OnDestroy() => Active.Remove(this);

        void Update()
        {
            var game = SVGame.Instance;
            if (game == null || game.Player == null || game.Paused) return;

            var player = game.Player;
            float dist = Vector2.Distance(transform.position, player.Pos);

            if (!_pulled && dist <= game.PickupRadius) _pulled = true;

            if (_pulled)
            {
                transform.position = Vector2.MoveTowards(transform.position, player.Pos,
                                                         Mathf.Max(6f, dist * 8f) * Time.deltaTime);
                if (dist < 0.35f)
                {
                    player.GainXp(Value);
                    Destroy(gameObject);
                }
            }
        }

        public static void PullAll() { foreach (var g in Active) g._pulled = true; }
    }
}
