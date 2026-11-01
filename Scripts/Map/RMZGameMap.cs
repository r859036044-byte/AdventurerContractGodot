// 地圖執行期狀態：通行判定、循環地圖、角色清單。
// 對應 MZ 的 Game_Map，座標一律用 float，因為 HalfMove 讓角色可以站在半格上。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public partial class RMZGameMap : Node
    {
        public static RMZGameMap Instance { get; private set; }

        public MapData Data { get; private set; }
        public TilesetData Tileset { get; private set; }
        public int MapId { get; private set; }

        int[] _flags;
        readonly List<RMZCharacterBase> _characters = new List<RMZCharacterBase>();

        public int Width => Data?.width ?? 0;
        public int Height => Data?.height ?? 0;
        public IReadOnlyList<RMZCharacterBase> Characters => _characters;

        /// <summary>scrollType: 0=不循環 1=縱向 2=橫向 3=雙向</summary>
        public bool LoopHorizontal => Data != null && (Data.scrollType == 2 || Data.scrollType == 3);
        public bool LoopVertical => Data != null && (Data.scrollType == 1 || Data.scrollType == 3);

        public override void _EnterTree() => Instance = this;

        public void Setup(int mapId, MapData data, TilesetData tileset)
        {
            MapId = mapId;
            Data = data;
            Tileset = tileset;
            _flags = tileset?.flags;
            _characters.Clear();
        }

        public void Register(RMZCharacterBase c) { if (!_characters.Contains(c)) _characters.Add(c); }
        public void Unregister(RMZCharacterBase c) => _characters.Remove(c);

        // ---------------- 座標 ----------------

        public bool IsValid(float x, float y) => x >= 0 && x < Width && y >= 0 && y < Height;

        public float RoundXf(float x) => LoopHorizontal ? Mod(x, Width) : x;
        public float RoundYf(float y) => LoopVertical ? Mod(y, Height) : y;

        /// <param name="unit">整格移動是 1，半格移動是 0.5</param>
        public float XWithDirection(float x, int d, float unit = 1f) =>
            x + (d == 6 ? unit : d == 4 ? -unit : 0f);

        public float YWithDirection(float y, int d, float unit = 1f) =>
            y + (d == 2 ? unit : d == 8 ? -unit : 0f);

        public float RoundXWithDirection(float x, int d, float unit = 1f) => RoundXf(XWithDirection(x, d, unit));
        public float RoundYWithDirection(float y, int d, float unit = 1f) => RoundYf(YWithDirection(y, d, unit));

        // HalfMove 的輔助：固定移動半格 / 固定移動整格
        public float RoundHalfXWithDirection(float x, int d) =>
            RoundXf(x + (d % 3 == 0 ? HalfMoveSettings.TileUnit : d % 3 == 1 ? -HalfMoveSettings.TileUnit : 0f));

        public float RoundHalfYWithDirection(float y, int d) =>
            RoundYf(y + (d <= 3 ? HalfMoveSettings.TileUnit : d >= 7 ? -HalfMoveSettings.TileUnit : 0f));

        public float RoundNoHalfXWithDirection(float x, int d) => RoundXf(XWithDirection(x, d, 1f));
        public float RoundNoHalfYWithDirection(float y, int d) => RoundYf(YWithDirection(y, d, 1f));

        static float Mod(float a, int n) => ((a % n) + n) % n;

        // ---------------- 圖塊查詢 ----------------

        public int TileId(int x, int y, int z)
        {
            if (Data?.data == null || x < 0 || x >= Width || y < 0 || y >= Height) return 0;
            int i = (z * Height + y) * Width + x;
            return (i >= 0 && i < Data.data.Length) ? Data.data[i] : 0;
        }

        readonly int[] _layered = new int[4];

        /// <summary>由上而下的四層圖塊：z3, z2, z1, z0。回傳的是共用陣列，用完就要處理掉。</summary>
        int[] LayeredTiles(int x, int y)
        {
            for (int z = 0; z < 4; z++) _layered[z] = TileId(x, y, 3 - z);
            return _layered;
        }

        // ---------------- 通行判定 ----------------

        /// <summary>bit: 下=1 左=2 右=4 上=8</summary>
        public bool CheckPassage(int x, int y, int bit)
        {
            if (_flags == null) return true;

            var tiles = LayeredTiles(x, y);
            for (int i = 0; i < 4; i++)
            {
                int tile = tiles[i];
                if (tile < 0 || tile >= _flags.Length) continue;
                int flag = _flags[tile];

                if ((flag & 0x10) != 0) continue;      // ☆ 不影響通行
                if ((flag & bit) == 0) return true;    // ○ 可通行
                if ((flag & bit) == bit) return false; // × 不可通行
            }
            return false;
        }

        /// <summary>d 為 MZ 方向鍵碼：2=下 4=左 6=右 8=上</summary>
        public bool IsPassable(float x, float y, int d) =>
            CheckPassage(Mathf.FloorToInt(x), Mathf.FloorToInt(y), (1 << (d / 2 - 1)) & 0x0f);

        bool HasFlag(float x, float y, int mask)
        {
            if (_flags == null) return false;
            var tiles = LayeredTiles(Mathf.FloorToInt(x), Mathf.FloorToInt(y));
            for (int i = 0; i < 4; i++)
            {
                int tile = tiles[i];
                if (tile >= 0 && tile < _flags.Length && (_flags[tile] & mask) != 0) return true;
            }
            return false;
        }

        /// <summary>
        /// 櫃檯（旗標 0x80）。MZ 允許隔著櫃檯跟後面的人講話，
        /// 滑鼠點櫃檯要能點到櫃檯後面的人也是靠這個。
        /// </summary>
        public bool IsCounter(float x, float y) => HasFlag(x, y, 0x80);

        /// <summary>四個方向都走不進去的格子（牆、櫃檯、吧台）。滑鼠點擊判定用。</summary>
        public bool IsBlockedTile(float x, float y) =>
            !IsPassable(x, y, 2) && !IsPassable(x, y, 4) &&
            !IsPassable(x, y, 6) && !IsPassable(x, y, 8);

        /// <summary>草叢：踩上去下半身會半透明（旗標 0x40）。</summary>
        public bool IsBush(float x, float y) => HasFlag(x, y, 0x40);

        /// <summary>梯子（旗標 0x20）：站上去角色一律面向上方。</summary>
        public bool IsLadder(float x, float y) => HasFlag(x, y, 0x20);

        public bool IsDamageFloor(float x, float y) => HasFlag(x, y, 0x100);

        /// <summary>
        /// 該位置是否被會擋路的角色佔住。半格移動下角色框是 1x1，所以用重疊判定。
        /// HalfMove 的 EventThrough：從側面（Y 不同）接觸事件時可以半格閃身過去。
        /// </summary>
        public bool IsCollided(float x, float y, RMZCharacterBase ignore)
        {
            for (int i = 0; i < _characters.Count; i++)
            {
                var c = _characters[i];
                if (c == null || c == ignore) continue;
                if (c.Through || !c.IsNormalPriority()) continue;
                if (RMZSolidObjectSettings.Blocks(c, x, y)) return true;
            }
            return false;
        }

        public RMZEventCharacter EventAt(float x, float y)
        {
            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i] is RMZEventCharacter ev && HalfMoveSettings.Overlaps(ev.X, ev.Y, x, y))
                    return ev;
            }
            return null;
        }
    }
}
