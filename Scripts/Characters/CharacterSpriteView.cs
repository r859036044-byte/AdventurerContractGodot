// 角色圖的切格與顯示，對應 MZ 的 Sprite_Character。
//
// MZ 的角色圖規則：
//   檔名含 "$" -> 單人圖，3 格 x 4 向
//   檔名含 "!" -> 物件圖，不做 6px 上移
//   其餘       -> 8 人圖，12 格 x 8 列
//
// 事件也可以直接用「圖塊」當外觀（image.tileId > 0），那條路徑走 tileset 圖集。
//
// Unity 版是 SpriteRenderer + Sprite.Create 切圖；
// Godot 這邊用 Sprite2D + AtlasTexture，一樣是「同一張貼圖切區塊」，不會多佔記憶體。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public partial class CharacterSpriteView : Sprite2D
    {
        static readonly Dictionary<string, AtlasTexture> SpriteCache = new Dictionary<string, AtlasTexture>();

        RMZCharacterBase _character;
        Texture2D _texture;
        string _loadedKey = null;
        int _lastSx = -1, _lastSy = -1;

        public bool IsBigCharacter { get; private set; }
        public bool IsObjectCharacter { get; private set; }
        public int SortingOrder { get; private set; } = int.MinValue;

        /// <summary>角色圖一格的高度（像素）。氣泡要靠這個定位。</summary>
        public float HeightInPixels { get; private set; } = RMZConfig.TileHeight;

        public void Bind(RMZCharacterBase c)
        {
            _character = c;
            Centered = true;
            TextureFilter = TextureFilterEnum.Nearest;
        }

        // Unity 的 LateUpdate：等角色這一幀的狀態都算完再更新外觀。
        // Godot 沒有 LateUpdate，用比較大的 ProcessPriority 達成同樣效果（數字大的後跑）。
        public override void _Ready() => ProcessPriority = 100;

        public override void _Process(double delta)
        {
            if (_character == null) return;

            string key = _character.TileIdGraphic > 0
                ? "tile:" + _character.TileIdGraphic
                : "char:" + _character.CharacterName;

            if (_loadedKey != key) Reload(key);
            if (_texture == null) { Texture = null; return; }

            int pw = PatternWidth();
            int ph = PatternHeight();
            HeightInPixels = ph;

            // 錨點在腳底：Sprite2D 預設以中心對齊，往上位移半格高就等於「底邊貼齊節點座標」
            Offset = new Vector2(0f, -ph / 2f);

            int sx, sy;
            if (_character.TileIdGraphic > 0)
            {
                int tileId = _character.TileIdGraphic;
                sx = ((tileId / 128 % 2) * 8 + tileId % 8) * pw;
                sy = (tileId % 256 / 8 % 16) * ph;
            }
            else
            {
                sx = (BlockX() + _character.Pattern) * pw;
                sy = (BlockY() + (_character.Direction - 2) / 2) * ph;
            }

            // 只有切到不同圖框時才重新查表，省掉每幀的字串組合與字典查詢
            if (sx != _lastSx || sy != _lastSy || Texture == null)
            {
                _lastSx = sx;
                _lastSy = sy;
                Texture = GetRegion(sx, sy, pw, ph);
            }

            float alpha = _character.Transparent ? 0f : _character.Opacity;
            if (!Mathf.IsEqualApprox(Modulate.A, alpha))
                Modulate = new Color(1f, 1f, 1f, alpha);
        }

        void Reload(string key)
        {
            _loadedKey = key;
            _lastSx = _lastSy = -1;
            IsBigCharacter = false;

            if (_character.TileIdGraphic > 0)
            {
                // 圖塊當外觀：跟一般圖塊一樣不做 6px 上移
                IsObjectCharacter = true;
                _texture = LoadTilesetFor(_character.TileIdGraphic);
                return;
            }

            _texture = RMZImageCache.LoadCharacter(_character.CharacterName);

            string sign = Prefix(_character.CharacterName);
            IsBigCharacter = sign.Contains("$");
            IsObjectCharacter = sign.Contains("!");
        }

        static Texture2D LoadTilesetFor(int tileId)
        {
            var tileset = RMZGameMap.Instance?.Tileset;
            if (tileset?.tilesetNames == null) return null;

            int setNumber = TileId.IsA5(tileId) ? 4 : 5 + tileId / 256;
            if (setNumber < 0 || setNumber >= tileset.tilesetNames.Length) return null;

            return RMZImageCache.LoadTileset(tileset.tilesetNames[setNumber]);
        }

        static string Prefix(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            int i = 0;
            while (i < name.Length && (name[i] == '!' || name[i] == '$')) i++;
            return name.Substring(0, i);
        }

        int PatternWidth() =>
            _character.TileIdGraphic > 0 ? RMZConfig.TileWidth
            : IsBigCharacter ? _texture.GetWidth() / 3 : _texture.GetWidth() / 12;

        int PatternHeight() =>
            _character.TileIdGraphic > 0 ? RMZConfig.TileHeight
            : IsBigCharacter ? _texture.GetHeight() / 4 : _texture.GetHeight() / 8;

        int BlockX() => IsBigCharacter ? 0 : (_character.CharacterIndex % 4) * 3;
        int BlockY() => IsBigCharacter ? 0 : (_character.CharacterIndex / 4) * 4;

        AtlasTexture GetRegion(int sx, int sy, int w, int h)
        {
            string key = $"{_loadedKey}|{_texture.GetRid()}|{sx}|{sy}|{w}|{h}";
            if (SpriteCache.TryGetValue(key, out var cached) && cached != null) return cached;

            // Godot 的貼圖原點就在左上角，跟 MZ 一致，不用像 Unity 那樣翻 Y
            var atlas = new AtlasTexture
            {
                Atlas = _texture,
                Region = new Rect2(sx, sy, w, h),
                FilterClip = true,
            };
            SpriteCache[key] = atlas;
            return atlas;
        }

        /// <summary>
        /// 依 Y 排序，讓下方的角色蓋住上方的角色。
        /// Unity 的 sortingOrder 是 short（±32767），Godot 的 ZIndex 只有 ±4096，
        /// 所以三段的間距要縮小，但「下方 / 同層 / 上方」互不重疊的原則不變。
        /// </summary>
        public void UpdateSortingOrder(float realY)
        {
            if (_character == null) return;

            int baseOrder = _character.PriorityType == 0 ? -3000     // 角色下方
                          : _character.PriorityType == 2 ? 1000      // 角色上方
                          : -1000;                                   // 同層（含玩家）

            int order = baseOrder + Mathf.Clamp(Mathf.RoundToInt(realY * 4f), 0, 1900);
            if (order == SortingOrder) return;

            SortingOrder = order;
            ZIndex = order;
        }

        public static void ClearCache() => SpriteCache.Clear();
    }
}
