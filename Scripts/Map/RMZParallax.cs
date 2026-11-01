// 視差背景（parallax），對應 Unity 版的 RMZParallax。
//
// 兩種用法：
//   1. 循環背景（勾了 parallaxLoopX/Y）：照原始像素平鋪並捲動，例如雲。
//   2. 圖片地圖（沒勾循環）：地圖本身就是一張圖，圖塊層全空，這張圖直接拉滿整張地圖，
//      擋路只靠 B 層的「無形牆」。奴隸區（Map036，SlaveDistrict.png）就是這種。
//
// 上層圖：同名加 _Upper 的圖存在（例如 SlaveDistrict_Upper）時，另外畫一張蓋在角色上面，
// 用來做屋頂、棚子、鐵柵這種「人要走到後面」的東西。
// 晝夜切換：插件指令 RMZ_UnityBridge / Parallax（name = SlaveDistrict_Night）執行中換圖。
using Godot;

namespace RMZ
{
    public partial class RMZParallax : Node2D
    {
        public static RMZParallax Instance { get; private set; }

        const string UpperSuffix = "_Upper";

        Sprite2D _base, _upper;
        MapData _map;
        string _current = "";
        bool _loopX, _loopY;
        float _sx, _sy, _ox, _oy;

        public override void _EnterTree() => Instance = this;
        public override void _ExitTree() { if (Instance == this) Instance = null; }

        public override void _Ready()
        {
            // 最底層：比下層圖塊（-4000）還低
            _base = new Sprite2D { Centered = false, ZAsRelative = false, ZIndex = -4096, Visible = false,
                                   TextureFilter = TextureFilterEnum.Nearest };
            // 上層：蓋在角色（最高約 2900）與上層圖塊（3000 起）上面，氣泡、動畫（4000）之下
            _upper = new Sprite2D { Centered = false, ZAsRelative = false, ZIndex = 3900, Visible = false,
                                    TextureFilter = TextureFilterEnum.Nearest };
            AddChild(_base);
            AddChild(_upper);
        }

        /// <summary>換地圖時呼叫（RMZBootstrap.LoadMap）。</summary>
        public void Build(MapData map)
        {
            _map = map;
            _ox = _oy = 0f;
            if (map == null) { Hide(); return; }
            _loopX = map.parallaxLoopX;
            _loopY = map.parallaxLoopY;
            _sx = map.parallaxSx;
            _sy = map.parallaxSy;
            _current = "";
            Show(map.parallaxShow ? (map.parallaxName=="DistrictTown_Day" && RMZGameState.Variable(28)>=2 ? "DistrictTown_Night" : map.parallaxName) : "");
        }

        /// <summary>執行中換圖（晝夜切換用）。傳空字串就是關掉。</summary>
        public void Show(string name)
        {
            if (_base == null) return;
            if (string.IsNullOrEmpty(name) || _map == null) { Hide(); return; }

            var tex = RMZImageCache.LoadParallax(name);
            if (tex == null) { Hide(); return; }
            _current = name;

            float mapW = _map.width * RMZConfig.TileWidth, mapH = _map.height * RMZConfig.TileHeight;
            Setup(_base, tex, mapW, mapH);

            var upperPath = System.IO.Path.Combine(RMZConfig.ParallaxImgPath,name + UpperSuffix + ".png");
            var up = System.IO.File.Exists(upperPath) ? RMZImageCache.LoadParallax(name + UpperSuffix) : null;
            if (up != null) Setup(_upper, up, mapW, mapH);
            else _upper.Visible = false;
        }

        void Setup(Sprite2D s, Texture2D tex, float mapW, float mapH)
        {
            s.Texture = tex;
            s.Position = Vector2.Zero;
            if (!_loopX && !_loopY)
            {
                // 圖片地圖：整張圖剛好蓋滿地圖
                s.RegionEnabled = false;
                s.TextureRepeat = TextureRepeatEnum.Disabled;
                s.Scale = new Vector2(mapW / tex.GetWidth(), mapH / tex.GetHeight());
            }
            else
            {
                // 循環背景：原始大小平鋪
                s.Scale = Vector2.One;
                s.TextureRepeat = TextureRepeatEnum.Enabled;
                s.RegionEnabled = true;
                s.RegionRect = new Rect2(0, 0, _loopX ? mapW : tex.GetWidth(), _loopY ? mapH : tex.GetHeight());
            }
            s.Visible = true;
        }

        public void Hide()
        {
            _current = "";
            if (_base != null) _base.Visible = false;
            if (_upper != null) _upper.Visible = false;
        }

        public override void _Process(double delta)
        {
            if (_map?.parallaxName=="DistrictTown_Day") {
                string desired=RMZGameState.Variable(28)>=2?"DistrictTown_Night":"DistrictTown_Day";
                if(_current!=desired)Show(desired);
            }
            if (_base == null || !_base.Visible || (!_loopX && !_loopY)) return;
            // MZ：每幀捲 sx/8 像素
            float frames = (float)delta * 60f;
            _ox += _sx / 8f * frames;
            _oy += _sy / 8f * frames;
            var r = _base.RegionRect;
            _base.RegionRect = new Rect2(_loopX ? _ox : 0f, _loopY ? _oy : 0f, r.Size);
        }
    }
}
