// 地圖上會隨機出現的補給品。走過去就吃到。
//
// 跟「戰利品」(SVLootDrop) 的差別：
//   戰利品是打死怪掉的，撿了帶回公會換錢
//   補給品是場上定時刷的，當下就生效，不會帶走
//
// 五種：血包、攻擊加成、全隊護盾、全隊無敵、磁力（把全場的經驗與戰利品吸過來）
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public enum SVPickupKind
    {
        Heal,        // 血包
        AttackUp,    // 攻擊力加成（限時）
        Shield,      // 所有人護盾
        Invincible,  // 所有人短時間無敵
        Magnet,      // 吸取全場經驗球與戰利品
        Card,        // 卡片：撿到就開一次三選一（全域技能加成）
    }

    [System.Serializable]
    public class SVPickupType
    {
        public SVPickupKind Kind = SVPickupKind.Heal;
        public string Name = "血包";

        /// <summary>撿到時接在名字後面顯示的效果說明。</summary>
        public string Effect = "";

        [Tooltip("抽選權重，越大越常出現")]
        public float Weight = 1f;

        [Tooltip("主要數值。血包=回血量　攻擊=加成百分比　護盾=盾量　無敵/磁力用不到")]
        public float Power = 30f;

        [Tooltip("持續幾秒。血包與磁力是瞬間生效，填 0")]
        public float Duration = 0f;

        public Color Color = Color.white;
    }

    public class SVPickup : MonoBehaviour
    {
        public static readonly List<SVPickup> Active = new List<SVPickup>();

        public SVPickupType Type;

        SpriteRenderer _renderer;
        SpriteRenderer _glow;
        float _bob;
        float _life;

        /// <summary>沒被撿走的話多久自己消失。太久沒撿會讓畫面越積越多。</summary>
        public float Lifetime = 30f;

        public static SVPickup Spawn(Transform parent, Vector2 pos, SVPickupType type)
        {
            var go = new GameObject("Pickup_" + type.Kind);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var p = go.AddComponent<SVPickup>();
            p.Type = type;

            // 底下一圈淡光，遠遠就看得到有東西
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(go.transform, false);
            p._glow = glowGo.AddComponent<SpriteRenderer>();
            p._glow.sprite = SVShapes.Ring();
            p._glow.color = new Color(type.Color.r, type.Color.g, type.Color.b, 0.45f);
            p._glow.sortingOrder = 90;
            glowGo.transform.localScale = Vector3.one * 1.1f;

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(go.transform, false);
            p._renderer = iconGo.AddComponent<SpriteRenderer>();

            // 有專屬貼圖就用貼圖（不染色，才看得到原本的顏色層次），
            // 沒有的就維持原本的色球
            // 程式內建的像素圖示（SVPixelArt），沒有對應圖示的才退回色球
            var icon = SVPixelArt.PickupIcon(type.Kind);
            p._renderer.sprite = icon != null ? icon : SVShapes.Circle();
            p._renderer.color = icon != null ? Color.white : type.Color;
            p._renderer.sortingOrder = 130;
            iconGo.transform.localScale = Vector3.one * (icon != null ? 0.9f : 0.42f);

            return p;
        }

        void Awake()
        {
            // 相位錯開，同時出現的補給品才不會整齊劃一地一起跳
            _bob = Random.value * Mathf.PI * 2f;
            Active.Add(this);
        }
        void OnDestroy() => Active.Remove(this);

        void Update()
        {
            var game = SVGame.Instance;
            if (game == null || game.Player == null || game.Paused) return;

            float dt = Time.deltaTime;

            _life += dt;
            if (_life >= Lifetime) { Destroy(gameObject); return; }

            // 上下浮動 + 光圈呼吸。幅度比原本大一點，遠遠就看得出「那是可以撿的」
            _bob += dt * 3.2f;
            if (_renderer != null)
                _renderer.transform.localPosition =
                    new Vector3(0f, 0.14f + Mathf.Sin(_bob) * 0.16f, 0f);
            if (_glow != null)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(_bob * 1.4f);
                _glow.transform.localScale = Vector3.one * Mathf.Lerp(1.0f, 1.25f, k);

                // 快消失的最後五秒閃爍提醒
                float alpha = _life > Lifetime - 5f
                    ? Mathf.PingPong(_life * 6f, 0.5f) + 0.15f
                    : Mathf.Lerp(0.3f, 0.55f, k);
                _glow.color = new Color(Type.Color.r, Type.Color.g, Type.Color.b, alpha);
            }

            // 走到就吃到。半徑放寬一點，不然要對得很準
            if (Vector2.Distance(transform.position, game.Player.Pos) <= game.Player.Radius + 0.75f)
            {
                game.ApplyPickup(Type, transform.position);
                Destroy(gameObject);
            }
        }

        public static void ClearAll()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
                if (Active[i] != null) Destroy(Active[i].gameObject);
            Active.Clear();
        }

        /// <summary>預設的五種補給品。</summary>
        public static List<SVPickupType> DefaultTypes() => new List<SVPickupType>
        {
            new SVPickupType { Kind = SVPickupKind.Heal, Name = "回復藥水", Effect = "全隊回復 35 血量",
                               Weight = 1.4f, Power = 35f, Duration = 0f,
                               Color = new Color(0.45f, 1f, 0.55f) },

            new SVPickupType { Kind = SVPickupKind.AttackUp, Name = "力量藥水", Effect = "全隊攻擊 +60%（15 秒）",
                               Weight = 1.0f, Power = 60f, Duration = 15f,
                               Color = new Color(1f, 0.5f, 0.4f) },

            new SVPickupType { Kind = SVPickupKind.Shield, Name = "護盾徽章", Effect = "全隊護盾 +80",
                               Weight = 1.0f, Power = 80f, Duration = 0f,
                               Color = new Color(0.55f, 0.8f, 1f) },

            new SVPickupType { Kind = SVPickupKind.Invincible, Name = "無敵之星", Effect = "全隊無敵 5 秒",
                               Weight = 0.6f, Power = 0f, Duration = 5f,
                               Color = new Color(1f, 0.9f, 0.4f) },

            new SVPickupType { Kind = SVPickupKind.Magnet, Name = "吸取磁鐵", Effect = "吸回全場能量與掉落物",
                               Weight = 0.9f, Power = 0f, Duration = 0f,
                               Color = new Color(0.9f, 0.6f, 1f) },
        };
    }
}
