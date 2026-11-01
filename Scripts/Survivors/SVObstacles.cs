// 場上固定不會動的障礙物：石堆、樹叢。
//
// 跟其他東西一樣不用 Unity 物理，純距離判斷 —— 障礙物是圓形，
// 跟 SVUnit.Overlaps() 那套圓對圓判定完全一致，人跟子彈都用同一支方法擋。
// 只有 A／S 級的兩關制地圖會擺東西進來，C／B 級跟單機測試這張表永遠是空的，
// 所以其他地方呼叫 ResolveMove / Blocks 都是零成本（空迴圈）。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public static class SVObstacles
    {
        public struct Obstacle
        {
            public Vector2 Pos;
            public float Radius;
        }

        public static readonly List<Obstacle> Active = new List<Obstacle>();

        static Transform _root;
        static readonly List<SpriteRenderer> _visuals = new List<SpriteRenderer>();

        public static void Clear()
        {
            Active.Clear();
            foreach (var v in _visuals) if (v != null) Object.Destroy(v.gameObject);
            _visuals.Clear();
            _root = null;
        }

        /// <summary>擺一批固定障礙物，順便畫出來。座標是相對場地中心。</summary>
        public static void Spawn(Transform parent, IEnumerable<(Vector2 pos, float radius)> layout)
        {
            Clear();
            _root = new GameObject("Obstacles").transform;
            _root.SetParent(parent, false);

            foreach (var (pos, radius) in layout)
            {
                Active.Add(new Obstacle { Pos = pos, Radius = radius });

                var go = new GameObject("Obstacle");
                go.transform.SetParent(_root, false);
                go.transform.position = pos;

                var sr = go.AddComponent<SpriteRenderer>();
                // 沒有專門的石頭／樹叢圖，先用柔邊圓墊個深色量體，之後有美術再換圖即可
                sr.sprite = SVShapes.Soft();
                sr.color = new Color(0.22f, 0.20f, 0.16f, 0.92f);
                sr.transform.localScale = Vector3.one * radius * 2.3f;
                sr.sortingOrder = SVConfig.SortingOrderFor(pos.y) - 1;

                var rim = new GameObject("Rim");
                rim.transform.SetParent(go.transform, false);
                var rimSr = rim.AddComponent<SpriteRenderer>();
                rimSr.sprite = SVShapes.Circle();
                rimSr.color = new Color(0.35f, 0.32f, 0.24f, 1f);
                rim.transform.localScale = Vector3.one * radius * 1.85f;
                rimSr.sortingOrder = sr.sortingOrder;

                _visuals.Add(sr);
                _visuals.Add(rimSr);
            }
        }

        /// <summary>把一個要移動到 pos 的圓形單位，從所有障礙物裡面推出去。</summary>
        public static Vector2 ResolveMove(Vector2 pos, float unitRadius)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                var o = Active[i];
                float minDist = o.Radius + unitRadius;
                var diff = pos - o.Pos;
                float distSq = diff.sqrMagnitude;
                if (distSq < minDist * minDist)
                {
                    float dist = Mathf.Sqrt(distSq);
                    var normal = dist > 0.0001f ? diff / dist : Vector2.up;
                    pos = o.Pos + normal * minDist;
                }
            }
            return pos;
        }

        /// <summary>這個點（可以帶半徑）有沒有卡進障礙物裡——子彈用這個判斷要不要被擋下來。</summary>
        public static bool Blocks(Vector2 point, float radius = 0f)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                float minDist = Active[i].Radius + radius;
                if ((point - Active[i].Pos).sqrMagnitude <= minDist * minDist) return true;
            }
            return false;
        }
    }
}
