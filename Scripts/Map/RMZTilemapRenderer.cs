// 把 MZ 的地圖資料畫成網格（mesh）。
//
// 為什麼不用 Godot 的 TileMapLayer：MZ 的自動圖塊是用 4 個 24x24 的 1/4 塊拼出來的，
// 而且同一格會有 4 層 + 陰影 + 桌邊，用 TileSet 資產表達會產生上萬個圖塊定義。
// 直接生 mesh 反而簡單、快，而且跟 rmmz_core.js 的畫法可以逐行對照。
// （Unity 版也是同樣做法，這支是逐段移植過來的。）
//
// 繪製順序（跟 rmmz_core.js 的 Tilemap._paintTiles 一致）：
//   z0 -> z1 -> 陰影 -> 桌邊 -> z2 -> z3
// 旗標有 0x10（☆）的圖塊改畫在角色上方。
//
// Godot 與 Unity 的兩點差異：
//   1. Y 軸往下為正，跟 MZ 一致，所以不用像 Unity 那樣翻 Y。
//   2. ZIndex 只有 ±4096（Unity 的 sortingOrder 是 ±32767），所以分層數字要縮小。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public partial class RMZTilemapRenderer : Node2D
    {
        // 繪製槽位，數字即排序權重
        const int SlotZ0 = 0, SlotZ1 = 1, SlotShadow = 2, SlotTableEdge = 3, SlotZ2 = 4, SlotZ3 = 5;

        const int LowerZBase = -4000;   // 角色最低 -3000，下層圖塊一定在最底下
        const int UpperZBase = 3000;    // 角色最高約 2900，上層圖塊一定壓在最上面

        /// <summary>
        /// 每個網格區塊的邊長（格）。
        /// 切成區塊 Godot 才能把畫面外的整塊剔除掉，不然整張 94x73 的半透明圖塊
        /// 每幀都要填一次色，填色率會爆掉。
        /// </summary>
        const int ChunkSize = 16;

        struct BucketKey
        {
            public bool upper;
            public int slot;
            public int setNumber; // -1 = 陰影
            public int chunk;     // 區塊索引 = cy * chunksX + cx

            public override int GetHashCode() =>
                ((upper ? 1 : 0) * 16 + slot) * 16 + (setNumber + 1) + chunk * 4096;

            public override bool Equals(object o) =>
                o is BucketKey k && k.upper == upper && k.slot == slot
                && k.setNumber == setNumber && k.chunk == chunk;
        }

        class Bucket
        {
            public List<Vector2> verts = new List<Vector2>();
            public List<Vector2> uvs = new List<Vector2>();
            public List<int> tris = new List<int>();
            public ArrayMesh mesh;
            public MeshInstance2D node;
        }

        MapData _map;
        TilesetData _tileset;
        int[] _flags;
        readonly Texture2D[] _setTextures = new Texture2D[9];
        Texture2D _shadowTex;

        readonly Dictionary<BucketKey, Bucket> _static = new Dictionary<BucketKey, Bucket>();
        readonly Dictionary<BucketKey, Bucket> _animated = new Dictionary<BucketKey, Bucket>();

        int _animationFrame = -1;
        float _animationTimer;

        int _chunksX;
        int _currentChunk;

        /// <summary>含有 A1（水面／瀑布）的格子。動畫換格時只需要重畫這些。</summary>
        readonly List<Vector2I> _animatedCells = new List<Vector2I>();

        public MapData Map => _map;
        public int[] Flags => _flags;

        public void Build(MapData map, TilesetData tileset)
        {
            Clear();

            _map = map;
            _tileset = tileset;
            _flags = tileset != null ? tileset.flags : null;

            for (int i = 0; i < 9; i++)
            {
                string n = (tileset != null && tileset.tilesetNames != null && i < tileset.tilesetNames.Length)
                    ? tileset.tilesetNames[i] : "";
                _setTextures[i] = RMZImageCache.LoadTileset(n);
            }

            _shadowTex = MakeShadowTexture();
            _chunksX = Mathf.Max(1, Mathf.CeilToInt(map.width / (float)ChunkSize));
            _animationFrame = 0;
            _animationTimer = 0f;

            PaintStatic();
            PaintAnimated();
            UploadAll();
        }

        public override void _Process(double delta)
        {
            if (_map == null || _animated.Count == 0) return;

            _animationTimer += (float)delta;
            if (_animationTimer < RMZConfig.AnimationInterval) return;

            RMZPerf.Begin("地圖動畫");
            _animationTimer -= RMZConfig.AnimationInterval;
            _animationFrame = (_animationFrame + 1) % 12; // 水面週期 4、瀑布週期 3，最小公倍數 12

            foreach (var b in _animated.Values) { b.verts.Clear(); b.uvs.Clear(); b.tris.Clear(); }
            PaintAnimated();
            foreach (var kv in _animated) Upload(kv.Key, kv.Value);
            RMZPerf.End();
        }

        public void Clear()
        {
            foreach (var b in _static.Values) DestroyBucket(b);
            foreach (var b in _animated.Values) DestroyBucket(b);
            _static.Clear();
            _animated.Clear();
            _map = null;
        }

        void DestroyBucket(Bucket b)
        {
            if (b.node != null && IsInstanceValid(b.node)) b.node.QueueFree();
            b.node = null;
            b.mesh = null;
        }

        // ---------------- 逐格繪製 ----------------

        void PaintStatic()
        {
            _animatedCells.Clear();

            for (int y = 0; y < _map.height; y++)
            for (int x = 0; x < _map.width; x++)
            {
                PaintCell(x, y, false);

                // 順便記下哪些格子有會動的水面，之後每 0.5 秒只要重畫這些就好。
                // 94x73 的地圖整張重掃一次要 27000 次查表，每秒兩次會明顯卡頓。
                if (HasAnimatedTile(x, y)) _animatedCells.Add(new Vector2I(x, y));
            }
        }

        bool HasAnimatedTile(int x, int y)
        {
            for (int z = 0; z < 4; z++)
                if (TileId.IsA1(ReadMapData(x, y, z))) return true;
            return false;
        }

        void PaintAnimated()
        {
            for (int i = 0; i < _animatedCells.Count; i++)
                PaintCell(_animatedCells[i].X, _animatedCells[i].Y, true);
        }

        /// <param name="animatedPass">true 只畫 A1（會動的水/瀑布），false 只畫其餘。</param>
        void PaintCell(int x, int y, bool animatedPass)
        {
            _currentChunk = (y / ChunkSize) * _chunksX + (x / ChunkSize);

            int t0 = ReadMapData(x, y, 0);
            int t1 = ReadMapData(x, y, 1);
            int t2 = ReadMapData(x, y, 2);
            int t3 = ReadMapData(x, y, 3);
            int shadowBits = ReadMapData(x, y, 4);
            int upperT1 = ReadMapData(x, y - 1, 1);

            float dx = x * RMZConfig.TileWidth;
            float dy = y * RMZConfig.TileHeight;

            AddSpotTile(t0, SlotZ0, dx, dy, animatedPass);
            AddSpotTile(t1, SlotZ1, dx, dy, animatedPass);

            if (!animatedPass)
            {
                AddShadow(shadowBits, dx, dy);
                if (TileId.IsTable(_flags, upperT1) && !TileId.IsTable(_flags, t1))
                    AddTableEdge(upperT1, dx, dy);
            }

            AddSpotTile(t2, SlotZ2, dx, dy, animatedPass);
            AddSpotTile(t3, SlotZ3, dx, dy, animatedPass);
        }

        int ReadMapData(int x, int y, int z)
        {
            if (_map?.data == null) return 0;
            if (x < 0 || x >= _map.width || y < 0 || y >= _map.height) return 0;
            int i = (z * _map.height + y) * _map.width + x;
            return (i >= 0 && i < _map.data.Length) ? _map.data[i] : 0;
        }

        void AddSpotTile(int tileId, int slot, float dx, float dy, bool animatedPass)
        {
            if (!TileId.IsVisible(tileId)) return;

            bool isA1 = TileId.IsA1(tileId);
            if (isA1 != animatedPass) return;

            bool upper = TileId.IsHigher(_flags, tileId);
            if (TileId.IsAutotile(tileId)) AddAutotile(tileId, upper, slot, dx, dy);
            else AddNormalTile(tileId, upper, slot, dx, dy);
        }

        void AddNormalTile(int tileId, bool upper, int slot, float dx, float dy)
        {
            int setNumber = TileId.IsA5(tileId) ? 4 : 5 + tileId / 256;

            int w = RMZConfig.TileWidth, h = RMZConfig.TileHeight;
            float sx = ((tileId / 128 % 2) * 8 + tileId % 8) * w;
            float sy = (tileId % 256 / 8 % 16) * h;

            AddRect(upper, slot, setNumber, sx, sy, dx, dy, w, h, w, h, false);
        }

        void AddAutotile(int tileId, bool upper, int slot, float dx, float dy)
        {
            int kind = TileId.AutotileKind(tileId);
            int shape = TileId.AutotileShape(tileId);
            int tx = kind % 8;
            int ty = kind / 8;

            int setNumber = 0;
            int bx = 0, by = 0;
            int[,,] table = TileId.FloorTable;
            bool isTable = false;

            if (TileId.IsA1(tileId))
            {
                int[] waterIndex = { 0, 1, 2, 1 };
                int surf = waterIndex[_animationFrame % 4];
                setNumber = 0;

                if (kind == 0) { bx = surf * 2; by = 0; }
                else if (kind == 1) { bx = surf * 2; by = 3; }
                else if (kind == 2) { bx = 6; by = 0; }
                else if (kind == 3) { bx = 6; by = 3; }
                else
                {
                    bx = tx / 4 * 8;
                    by = ty * 6 + (tx / 2 % 2) * 3;
                    if (kind % 2 == 0) bx += surf * 2;
                    else { bx += 6; table = TileId.WaterfallTable; by += _animationFrame % 3; }
                }
            }
            else if (TileId.IsA2(tileId))
            {
                setNumber = 1;
                bx = tx * 2;
                by = (ty - 2) * 3;
                isTable = TileId.IsTable(_flags, tileId);
            }
            else if (TileId.IsA3(tileId))
            {
                setNumber = 2;
                bx = tx * 2;
                by = (ty - 6) * 2;
                table = TileId.WallTable;
            }
            else if (TileId.IsA4(tileId))
            {
                setNumber = 3;
                bx = tx * 2;
                by = Mathf.FloorToInt((ty - 10) * 2.5f + (ty % 2 == 1 ? 0.5f : 0f));
                if (ty % 2 == 1) table = TileId.WallTable;
            }

            int shapeIndex = Mathf.Clamp(shape, 0, table.GetLength(0) - 1);
            float w1 = RMZConfig.TileWidth / 2f;
            float h1 = RMZConfig.TileHeight / 2f;

            for (int i = 0; i < 4; i++)
            {
                int qsx = table[shapeIndex, i, 0];
                int qsy = table[shapeIndex, i, 1];
                float sx1 = (bx * 2 + qsx) * w1;
                float sy1 = (by * 2 + qsy) * h1;
                float dx1 = dx + (i % 2) * w1;
                float dy1 = dy + (i / 2) * h1;

                if (isTable && (qsy == 1 || qsy == 5))
                {
                    // 桌子：上半換成桌面貼圖，下半疊回原本的 1/4 塊
                    int qsx2 = qsy == 1 ? (4 - qsx) % 4 : qsx;
                    float sx2 = (bx * 2 + qsx2) * w1;
                    float sy2 = (by * 2 + 3) * h1;
                    AddRect(upper, slot, setNumber, sx2, sy2, dx1, dy1, w1, h1, w1, h1, false);
                    AddRect(upper, slot, setNumber, sx1, sy1, dx1, dy1 + h1 / 2f, w1, h1 / 2f, w1, h1 / 2f, false);
                }
                else
                {
                    AddRect(upper, slot, setNumber, sx1, sy1, dx1, dy1, w1, h1, w1, h1, false);
                }
            }
        }

        void AddTableEdge(int tileId, float dx, float dy)
        {
            if (!TileId.IsA2(tileId)) return;

            int kind = TileId.AutotileKind(tileId);
            int shape = TileId.AutotileShape(tileId);
            int tx = kind % 8;
            int ty = kind / 8;
            int bx = tx * 2;
            int by = (ty - 2) * 3;

            float w1 = RMZConfig.TileWidth / 2f;
            float h1 = RMZConfig.TileHeight / 2f;
            int shapeIndex = Mathf.Clamp(shape, 0, 47);

            for (int i = 0; i < 2; i++)
            {
                int qsx = TileId.FloorTable[shapeIndex, 2 + i, 0];
                int qsy = TileId.FloorTable[shapeIndex, 2 + i, 1];
                float sx1 = (bx * 2 + qsx) * w1;
                float sy1 = (by * 2 + qsy) * h1 + h1 / 2f;
                float dx1 = dx + (i % 2) * w1;
                AddRect(false, SlotTableEdge, 1, sx1, sy1, dx1, dy, w1, h1 / 2f, w1, h1 / 2f, false);
            }
        }

        void AddShadow(int shadowBits, float dx, float dy)
        {
            if ((shadowBits & 0x0f) == 0) return;

            float w1 = RMZConfig.TileWidth / 2f;
            float h1 = RMZConfig.TileHeight / 2f;

            for (int i = 0; i < 4; i++)
            {
                if ((shadowBits & (1 << i)) == 0) continue;
                float dx1 = dx + (i % 2) * w1;
                float dy1 = dy + (i / 2) * h1;
                AddRect(false, SlotShadow, -1, 0, 0, dx1, dy1, w1, h1, 1, 1, true);
            }
        }

        // ---------------- 幾何 ----------------

        /// <param name="sx">來源像素 X（左上原點）</param>
        /// <param name="dx">目的地像素 X（左上原點）</param>
        void AddRect(bool upper, int slot, int setNumber,
                     float sx, float sy, float dx, float dy,
                     float dw, float dh, float sw, float sh, bool isShadow)
        {
            var key = new BucketKey { upper = upper, slot = slot, setNumber = isShadow ? -1 : setNumber, chunk = _currentChunk };
            var dict = (setNumber == 0 && !isShadow) ? _animated : _static;

            if (!dict.TryGetValue(key, out var bucket))
            {
                bucket = new Bucket();
                dict[key] = bucket;
            }

            Texture2D tex = isShadow ? _shadowTex : _setTextures[setNumber];
            if (tex == null) return;

            // Godot 的 Y 往下為正，跟 MZ 一致，直接用像素座標
            float x0 = dx, x1 = dx + dw;
            float y0 = dy, y1 = dy + dh;

            float texW = tex.GetWidth(), texH = tex.GetHeight();
            float u0 = sx / texW, u1 = (sx + sw) / texW;
            float v0 = sy / texH, v1 = (sy + sh) / texH;

            int b = bucket.verts.Count;
            bucket.verts.Add(new Vector2(x0, y0));
            bucket.verts.Add(new Vector2(x1, y0));
            bucket.verts.Add(new Vector2(x1, y1));
            bucket.verts.Add(new Vector2(x0, y1));

            bucket.uvs.Add(new Vector2(u0, v0));
            bucket.uvs.Add(new Vector2(u1, v0));
            bucket.uvs.Add(new Vector2(u1, v1));
            bucket.uvs.Add(new Vector2(u0, v1));

            // 逆時針纏繞（Godot 2D 的正面）
            bucket.tris.Add(b); bucket.tris.Add(b + 1); bucket.tris.Add(b + 2);
            bucket.tris.Add(b); bucket.tris.Add(b + 2); bucket.tris.Add(b + 3);
        }

        void UploadAll()
        {
            foreach (var kv in _static) Upload(kv.Key, kv.Value);
            foreach (var kv in _animated) Upload(kv.Key, kv.Value);
        }

        void Upload(BucketKey key, Bucket bucket)
        {
            if (bucket.node == null)
            {
                bucket.mesh = new ArrayMesh();
                bucket.node = new MeshInstance2D
                {
                    Name = $"Tiles_{(key.upper ? "Upper" : "Lower")}_c{key.chunk}_s{key.slot}_t{key.setNumber}",
                    Mesh = bucket.mesh,
                    Texture = key.setNumber < 0 ? _shadowTex : _setTextures[key.setNumber],
                    ZIndex = (key.upper ? UpperZBase : LowerZBase) + key.slot,
                    TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                };
                AddChild(bucket.node);
            }

            bucket.mesh.ClearSurfaces();
            if (bucket.verts.Count == 0) return;

            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = bucket.verts.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = bucket.uvs.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = bucket.tris.ToArray();
            bucket.mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }

        static Texture2D MakeShadowTexture()
        {
            var img = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            img.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.5f));
            return ImageTexture.CreateFromImage(img);
        }
    }
}
