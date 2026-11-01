// 尋路，對應 MZ 的 Game_Character.findDirectionTo。
// 是一個有搜尋上限的 A*：走不到目標時，會朝「最接近目標」的方向前進一步。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public partial class RMZCharacterBase
    {
        class PathNode
        {
            public PathNode parent;
            public int x, y;
            public int g;
            public float f;
        }

        /// <summary>MZ 的 searchLimit，預設 12 格。</summary>
        public virtual int SearchLimit => 12;

        /// <summary>
        /// 以「整格」為單位做尋路，回傳下一步該走的方向（MZ 方向碼）。
        /// 半格移動下實際走的還是 0.5 格，只是方向由這裡決定。
        /// </summary>
        public int FindDirectionTo(int goalX, int goalY)
        {
            var map = RMZGameMap.Instance;
            if (map == null) return 0;

            int startX = TileX, startY = TileY;
            if (startX == goalX && startY == goalY) return 0;

            int mapWidth = map.Width;
            var nodeList = new List<PathNode>();
            var open = new HashSet<int>();
            var closed = new HashSet<int>();

            var start = new PathNode { x = startX, y = startY, g = 0 };
            start.f = Distance(startX, startY, goalX, goalY);
            var best = start;

            nodeList.Add(start);
            open.Add(startY * mapWidth + startX);

            while (nodeList.Count > 0)
            {
                int bestIndex = 0;
                for (int i = 1; i < nodeList.Count; i++)
                    if (nodeList[i].f < nodeList[bestIndex].f) bestIndex = i;

                var current = nodeList[bestIndex];
                int x1 = current.x, y1 = current.y;
                int pos1 = y1 * mapWidth + x1;

                nodeList.RemoveAt(bestIndex);
                open.Remove(pos1);
                closed.Add(pos1);

                if (x1 == goalX && y1 == goalY) { best = current; break; }
                if (current.g >= SearchLimit) continue;

                for (int j = 0; j < 4; j++)
                {
                    int direction = 2 + j * 2;
                    int x2 = Mathf.FloorToInt(map.RoundXf(map.XWithDirection(x1, direction, 1f)));
                    int y2 = Mathf.FloorToInt(map.RoundYf(map.YWithDirection(y1, direction, 1f)));
                    int pos2 = y2 * mapWidth + x2;

                    if (closed.Contains(pos2)) continue;
                    if (!CanPassTile(x1, y1, direction)) continue;

                    int g2 = current.g + 1;
                    PathNode neighbor = null;
                    if (open.Contains(pos2))
                    {
                        foreach (var n in nodeList)
                            if (n.x == x2 && n.y == y2) { neighbor = n; break; }
                        if (neighbor != null && g2 >= neighbor.g) continue;
                    }

                    if (neighbor == null)
                    {
                        neighbor = new PathNode();
                        nodeList.Add(neighbor);
                        open.Add(pos2);
                    }

                    neighbor.parent = current;
                    neighbor.x = x2;
                    neighbor.y = y2;
                    neighbor.g = g2;
                    neighbor.f = g2 + Distance(x2, y2, goalX, goalY);

                    if (neighbor.f - neighbor.g < best.f - best.g) best = neighbor;
                }
            }

            // 從最佳節點往回追溯到第一步
            var node = best;
            while (node.parent != null && node.parent != start) node = node.parent;

            int dx = node.x - start.x;
            int dy = node.y - start.y;

            if (dy > 0) return Down;
            if (dx < 0) return Left;
            if (dx > 0) return Right;
            if (dy < 0) return Up;

            // 完全走不到就朝目標的方向硬走一步
            float dx2 = DeltaXFrom(goalX);
            float dy2 = DeltaYFrom(goalY);
            if (Mathf.Abs(dx2) > Mathf.Abs(dy2)) return dx2 > 0 ? Left : Right;
            if (!Mathf.IsZeroApprox(dy2)) return dy2 > 0 ? Up : Down;
            return 0;
        }

        static float Distance(int x1, int y1, int x2, int y2) =>
            Mathf.Abs(x1 - x2) + Mathf.Abs(y1 - y2);

        /// <summary>以整格為單位的通行判定，只給尋路用。</summary>
        public bool CanPassTile(int x, int y, int d)
        {
            var map = RMZGameMap.Instance;
            int x2 = Mathf.FloorToInt(map.RoundXf(map.XWithDirection(x, d, 1f)));
            int y2 = Mathf.FloorToInt(map.RoundYf(map.YWithDirection(y, d, 1f)));

            if (!map.IsValid(x2, y2)) return false;
            if (Through || IsDebugThrough()) return true;
            if (!map.IsPassable(x, y, d) || !map.IsPassable(x2, y2, ReverseDir(d))) return false;
            if (map.IsCollided(x2, y2, this)) return false;
            return true;
        }
    }
}
