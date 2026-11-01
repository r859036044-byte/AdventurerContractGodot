// MZ 圖塊 ID 的判別與自動圖塊（autotile）對照表。
// 這段是從 rmmz_core.js 的 Tilemap 逐行移植的，數值不要動。
namespace RMZ
{
    public static class TileId
    {
        public const int B = 0;
        public const int C = 256;
        public const int D = 512;
        public const int E = 768;
        public const int A5 = 1536;
        public const int A1 = 2048;
        public const int A2 = 2816;
        public const int A3 = 4352;
        public const int A4 = 5888;
        public const int Max = 8192;

        public static bool IsVisible(int id) => id > 0 && id < Max;
        public static bool IsAutotile(int id) => id >= A1;
        public static int AutotileKind(int id) => (id - A1) / 48;
        public static int AutotileShape(int id) => (id - A1) % 48;

        public static bool IsA1(int id) => id >= A1 && id < A2;
        public static bool IsA2(int id) => id >= A2 && id < A3;
        public static bool IsA3(int id) => id >= A3 && id < A4;
        public static bool IsA4(int id) => id >= A4 && id < Max;
        public static bool IsA5(int id) => id >= A5 && id < A1;

        public static bool IsWater(int id)
        {
            if (IsA1(id)) return AutotileKind(id) <= 1 || (id >= A1 + 96 && id < A1 + 192);
            return false;
        }

        public static bool IsWaterfall(int id)
        {
            if (id >= A1 + 192 && id < A2) return AutotileKind(id) % 2 == 1;
            return false;
        }

        public static bool IsWall(int id) => IsWallTop(id) || IsWallSide(id);
        public static bool IsWallTop(int id) => IsA3(id) && AutotileKind(id) % 16 < 8;
        public static bool IsWallSide(int id) =>
            (IsA3(id) || IsA4(id)) && AutotileKind(id) % 16 >= 8;

        /// <summary>0x10 = 「☆」，不擋路而且畫在角色上方。</summary>
        public static bool IsHigher(int[] flags, int id) =>
            id >= 0 && flags != null && id < flags.Length && (flags[id] & 0x10) != 0;

        /// <summary>0x80 的 A2 圖塊是「桌子」，會多畫一段側邊。</summary>
        public static bool IsTable(int[] flags, int id) =>
            IsA2(id) && flags != null && id < flags.Length && (flags[id] & 0x80) != 0;

        // ---- 自動圖塊對照表（shape -> 四個 1/4 塊在圖集中的座標） ----

        public static readonly int[,,] FloorTable = new int[48, 4, 2]
        {
            {{2,4},{1,4},{2,3},{1,3}}, {{2,0},{1,4},{2,3},{1,3}}, {{2,4},{3,0},{2,3},{1,3}}, {{2,0},{3,0},{2,3},{1,3}},
            {{2,4},{1,4},{2,3},{3,1}}, {{2,0},{1,4},{2,3},{3,1}}, {{2,4},{3,0},{2,3},{3,1}}, {{2,0},{3,0},{2,3},{3,1}},
            {{2,4},{1,4},{2,1},{1,3}}, {{2,0},{1,4},{2,1},{1,3}}, {{2,4},{3,0},{2,1},{1,3}}, {{2,0},{3,0},{2,1},{1,3}},
            {{2,4},{1,4},{2,1},{3,1}}, {{2,0},{1,4},{2,1},{3,1}}, {{2,4},{3,0},{2,1},{3,1}}, {{2,0},{3,0},{2,1},{3,1}},
            {{0,4},{1,4},{0,3},{1,3}}, {{0,4},{3,0},{0,3},{1,3}}, {{0,4},{1,4},{0,3},{3,1}}, {{0,4},{3,0},{0,3},{3,1}},
            {{2,2},{1,2},{2,3},{1,3}}, {{2,2},{1,2},{2,3},{3,1}}, {{2,2},{1,2},{2,1},{1,3}}, {{2,2},{1,2},{2,1},{3,1}},
            {{2,4},{3,4},{2,3},{3,3}}, {{2,4},{3,4},{2,1},{3,3}}, {{2,0},{3,4},{2,3},{3,3}}, {{2,0},{3,4},{2,1},{3,3}},
            {{2,4},{1,4},{2,5},{1,5}}, {{2,0},{1,4},{2,5},{1,5}}, {{2,4},{3,0},{2,5},{1,5}}, {{2,0},{3,0},{2,5},{1,5}},
            {{0,4},{3,4},{0,3},{3,3}}, {{2,2},{1,2},{2,5},{1,5}}, {{0,2},{1,2},{0,3},{1,3}}, {{0,2},{1,2},{0,3},{3,1}},
            {{2,2},{3,2},{2,3},{3,3}}, {{2,2},{3,2},{2,1},{3,3}}, {{2,4},{3,4},{2,5},{3,5}}, {{2,0},{3,4},{2,5},{3,5}},
            {{0,4},{1,4},{0,5},{1,5}}, {{0,4},{3,0},{0,5},{1,5}}, {{0,2},{3,2},{0,3},{3,3}}, {{0,2},{1,2},{0,5},{1,5}},
            {{0,4},{3,4},{0,5},{3,5}}, {{2,2},{3,2},{2,5},{3,5}}, {{0,2},{3,2},{0,5},{3,5}}, {{0,0},{1,0},{0,1},{1,1}}
        };

        public static readonly int[,,] WallTable = new int[16, 4, 2]
        {
            {{2,2},{1,2},{2,1},{1,1}}, {{0,2},{1,2},{0,1},{1,1}}, {{2,0},{1,0},{2,1},{1,1}}, {{0,0},{1,0},{0,1},{1,1}},
            {{2,2},{3,2},{2,1},{3,1}}, {{0,2},{3,2},{0,1},{3,1}}, {{2,0},{3,0},{2,1},{3,1}}, {{0,0},{3,0},{0,1},{3,1}},
            {{2,2},{1,2},{2,3},{1,3}}, {{0,2},{1,2},{0,3},{1,3}}, {{2,0},{1,0},{2,3},{1,3}}, {{0,0},{1,0},{0,3},{1,3}},
            {{2,2},{3,2},{2,3},{3,3}}, {{0,2},{3,2},{0,3},{3,3}}, {{2,0},{3,0},{2,3},{3,3}}, {{0,0},{3,0},{0,3},{3,3}}
        };

        public static readonly int[,,] WaterfallTable = new int[4, 4, 2]
        {
            {{2,0},{1,0},{2,1},{1,1}}, {{0,0},{1,0},{0,1},{1,1}},
            {{2,0},{3,0},{2,1},{3,1}}, {{0,0},{3,0},{0,1},{3,1}}
        };
    }
}
