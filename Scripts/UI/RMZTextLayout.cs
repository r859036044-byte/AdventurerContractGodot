// 讀 RM 專案的 js/showTextPositions.json。
//
// 那是 Hendrix Pictures UI Creator 的「遊戲內拖曳排版」存檔：
// 作者在遊戲裡把每個文字視窗擺好位置，插件就把座標寫進這個檔。
// 事件指令自帶的 sizePosition 幾乎都是預設值 "300, 200, 0, 0"，
// 真正的版面在這裡，所以一定要讀。
//
// 檔案結構：
//   { "commonEvents": { "commonEvent": { "<id>": {x,y,width,height,fontSize,outlineSize} } },
//     "maps": { "map_2": { "<id>": {...} }, ... } }
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Godot;

namespace RMZ
{
    public class RMZTextLayoutEntry
    {
        public bool HasBox;
        public Rect2 Box;
        public int FontSize;
        public float OutlineSize;
    }

    public static class RMZTextLayout
    {
        static readonly Dictionary<string, RMZTextLayoutEntry> Common =
            new Dictionary<string, RMZTextLayoutEntry>();

        // mapId -> (id -> 版面)
        static readonly Dictionary<int, Dictionary<string, RMZTextLayoutEntry>> Maps =
            new Dictionary<int, Dictionary<string, RMZTextLayoutEntry>>();

        static bool _loaded;

        public static string FilePath =>
            Path.Combine(RMZConfig.RootPath, "ui", "showTextPositions.json");

        public static void Load(bool force = false)
        {
            if (_loaded && !force) return;
            _loaded = true;

            Common.Clear();
            Maps.Clear();

            if (!File.Exists(FilePath))
            {
                GD.PushWarning($"[RMZ] 找不到 {FilePath}。UI 文字會退回用事件指令自帶的座標" +
                                 "（多半是預設的 0,0，會全部疊在左上角）。" +
                                 "跑一次 Tools > RPG Maker MZ > 同步 RM 專案資料 就會複製過來。");
                return;
            }

            JObject root;
            try { root = JObject.Parse(File.ReadAllText(FilePath)); }
            catch (System.Exception e)
            {
                GD.PushWarning($"[RMZ] showTextPositions.json 解析失敗：{e.Message}");
                return;
            }

            if (root["commonEvents"]?["commonEvent"] is JObject ce) ReadInto(ce, Common);

            if (root["maps"] is JObject maps)
            {
                foreach (var pair in maps)
                {
                    // key 長這樣："map_2"
                    var digits = pair.Key.Replace("map_", "");
                    if (!int.TryParse(digits, out int mapId)) continue;
                    if (!(pair.Value is JObject obj)) continue;

                    var table = new Dictionary<string, RMZTextLayoutEntry>();
                    ReadInto(obj, table);
                    Maps[mapId] = table;
                }
            }

            GD.Print($"[RMZ] UI 文字版面載入完成：公共 {Common.Count} 筆、地圖 {Maps.Count} 張。");
        }

        static void ReadInto(JObject obj, Dictionary<string, RMZTextLayoutEntry> table)
        {
            foreach (var pair in obj)
            {
                if (!(pair.Value is JObject v)) continue;   // null 代表沒排過版

                var entry = new RMZTextLayoutEntry
                {
                    FontSize = (int)Num(v["fontSize"], 0f),
                    OutlineSize = Num(v["outlineSize"], 0f),
                };

                if (v["x"] != null && v["y"] != null)
                {
                    entry.HasBox = true;
                    entry.Box = new Rect2(Num(v["x"], 0f), Num(v["y"], 0f),
                                         Num(v["width"], 300f), Num(v["height"], 60f));
                }

                table[pair.Key] = entry;
            }
        }

        /// <summary>數字有可能被存成字串（"163"），所以兩種都要吃。</summary>
        static float Num(JToken t, float fallback)
        {
            if (t == null) return fallback;
            if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) return (float)t;
            return float.TryParse(t.ToString(), out var v) ? v : fallback;
        }

        /// <summary>先找目前地圖的版面，沒有再找公共事件那份。</summary>
        public static RMZTextLayoutEntry Find(string id)
        {
            Load();

            int mapId = RMZGameMap.Instance != null ? RMZGameMap.Instance.MapId : 0;
            if (Maps.TryGetValue(mapId, out var table) && table.TryGetValue(id, out var e)) return e;
            if (Common.TryGetValue(id, out var c)) return c;
            return null;
        }
    }
}
