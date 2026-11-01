// 火箭彈留在地上的焦痕：一段時間內對踩在上面的敵人造成持續傷害。
//
// 一顆火箭飛過去會沿路留下好幾個小焦點，不另外做成 SVBurnField ——
// 那個有「場上最多 4 塊」的上限，火箭一次齊射就會把其他火場（星隕）擠掉。
// 同一跳裡每隻敵人最多被燒一次，焦點疊在一起也不會傷害翻倍。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public static class SVScorchTrail
    {
        public const float Life = 3.5f;          // 焦痕留多久
        public const float Radius = 0.75f;       // 每個焦點的範圍
        public const float TickInterval = 0.5f;  // 多久燒一次
        public const int MaxPoints = 420;        // 同時最多幾段焦痕（效能）

        class Point { public GameObject Go; public SpriteRenderer Sr, Core; public Vector2 Pos; public float Left; public int Damage; }

        static readonly List<Point> Points = new List<Point>();
        static readonly HashSet<SVEnemy> HitThisTick = new HashSet<SVEnemy>();
        static float _tick;

        /// <summary>
        /// 在 pos 留一段焦痕。dir 是火箭飛的方向：每一段是沿著方向拉長的橢圓，
        /// 一段接一段（間距比長度短）連起來就是一條直直的火線。
        /// 外層橘紅光暈＋中間亮黃的芯，比一顆顆圓點明顯得多。
        /// </summary>
        public static void Drop(Vector2 pos, Vector2 dir, int damage)
        {
            if (damage <= 0) return;
            while (Points.Count >= MaxPoints) Remove(0);

            var go = new GameObject("Scorch");
            go.transform.position = pos;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

            var sr = MakeLayer(go, new Color(1f, 0.35f, 0.08f, 0.75f), 1.6f, 1.25f, -20002);
            var core = MakeLayer(go, new Color(1f, 0.85f, 0.35f, 0.9f), 1.4f, 0.45f, -20001);
            Points.Add(new Point { Go = go, Sr = sr, Core = core, Pos = pos, Left = Life, Damage = damage });
        }

        static SpriteRenderer MakeLayer(GameObject parent, Color color, float length, float width, int order)
        {
            var go = new GameObject("Layer");
            go.transform.SetParent(parent.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SVShapes.Soft();
            sr.color = color;
            sr.sortingOrder = order;   // 畫在角色底下
            float unit = Mathf.Max(0.001f, sr.sprite != null ? sr.sprite.bounds.size.x : 1f);
            go.transform.localScale = new Vector3(length / unit, width / unit, 1f);
            return sr;
        }

        static void Remove(int i)
        {
            var p = Points[i];
            Points.RemoveAt(i);
            if (p.Go != null) Object.Destroy(p.Go);
        }

        public static void Tick(float dt)
        {
            if (Points.Count == 0) return;

            for (int i = Points.Count - 1; i >= 0; i--)
            {
                var p = Points[i];
                p.Left -= dt;
                if (p.Left <= 0f || p.Go == null) { Remove(i); continue; }
                // 後段慢慢淡出、顏色變暗
                float k = Mathf.Clamp01(p.Left / Life);
                float a = Mathf.Clamp01(k * 1.6f);
                float flicker = 0.85f + 0.15f * Mathf.Sin((Time.time + p.Pos.x) * 12f);
                if (p.Sr != null) p.Sr.color = new Color(Mathf.Lerp(0.35f, 1f, k), Mathf.Lerp(0.12f, 0.35f, k), 0.08f, 0.75f * a * flicker);
                if (p.Core != null) p.Core.color = new Color(1f, Mathf.Lerp(0.4f, 0.85f, k), Mathf.Lerp(0.1f, 0.35f, k), 0.9f * a * a * flicker);
            }

            _tick -= dt;
            if (_tick > 0f) return;
            _tick = TickInterval;

            HitThisTick.Clear();
            foreach (var p in Points)
            {
                foreach (var e in SVEnemy.Active.ToArray())
                {
                    if (e == null || e.IsDead || HitThisTick.Contains(e)) continue;
                    if (!e.Overlaps(p.Pos, Radius)) continue;
                    HitThisTick.Add(e);
                    e.TakeDamage(p.Damage, SVDamageKind.True);
                }
            }
        }

        public static void Clear()
        {
            for (int i = Points.Count - 1; i >= 0; i--) Remove(i);
            _tick = 0f;
        }
    }
}
