// 打倒結尾王之後那段流程的台詞與立繪。
//
// 台詞放在 StreamingAssets/rmz/survivors_talk.json，改台詞不用重編譯。
// 立繪直接讀 img/pictures/ 底下那批（跟行走圖同一條路，不經過 Resources），
// 所以本篇被 SetActive(false) 冰住的時候這裡照樣拿得到圖。
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using RMZS.GodotPort;

namespace RMZS
{
    /// <summary>一句台詞：一張立繪 + 一段話。</summary>
    public struct SVTalkLine
    {
        /// <summary>身體（底圖）。立繪是「底圖 + 表情」兩張疊起來的。</summary>
        public string Base;
        public string Face;
        public string Text;
    }

    public static class SVTalk
    {
        public const string FileName = "survivors_talk.json";

        static JObject _root;
        static bool _tried;

        public static bool Loaded => Root() != null;

        static string RootPath => Path.Combine(Application.streamingAssetsPath, "rmz");
        public static string PictureRoot => Path.Combine(RootPath, "img", "pictures");

        static JObject Root()
        {
            if (_tried) return _root;
            _tried = true;

            string path = Path.Combine(RootPath, FileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[倖存者] 找不到 {path}，打完王的對話先跳過。");
                return null;
            }

            try { _root = JObject.Parse(File.ReadAllText(path)); }
            catch (System.Exception e) { Debug.LogError($"[倖存者] {FileName} 解析失敗：{e.Message}"); }

            return _root;
        }

        /// <summary>改完 JSON 想立刻看效果就叫這個。</summary>
        public static void Reload() { _tried = false; _root = null; }

        /// <summary>
        /// 這位女主目前的好感（0~100）。本篇進場前透過委託書填進來。
        /// 話少的角色靠這個「隨著好感變得多話」。
        /// </summary>
        public static int Love;

        public const int MidLove = 35;
        public const int HighLove = 70;

        static string Tier => Love >= HighLove ? "high" : Love >= MidLove ? "mid" : "low";

        public static string NameOf(int heroine)
        {
            var block = Root()?[heroine.ToString()] as JObject;
            return block?["name"]?.ToString() ?? "";
        }

        /// <summary>某位女主的某一段台詞。key：boss_down / cleared。</summary>
        public static List<SVTalkLine> Lines(int heroine, string key)
        {
            var list = new List<SVTalkLine>();

            var block = Root()?[heroine.ToString()] as JObject;
            var node = block?[key];

            // 分好感段的寫法：{ "low": [...], "mid": [...], "high": [...] }
            // 沒有那一段就往下退，所以只寫 low 也能跑
            if (node is JObject tiers)
                node = tiers[Tier]
                    ?? (Tier == "high" ? tiers["mid"] : null)
                    ?? tiers["low"] ?? tiers["mid"] ?? tiers["high"];

            if (node is not JArray array) return list;

            string fallback = block?["portrait"]?.ToString() ?? "";

            foreach (var item in array)
            {
                if (item is not JObject o) continue;
                // 提到凱、塔克的台詞，只有他們這趟真的有跟來才用（僱傭／獨自組隊時不會來）
                if (MentionsAbsentCrew(o["text"]?.ToString())) continue;
                string face = o["face"]?.ToString() ?? "";
                string b = o["base"]?.ToString();
                if (string.IsNullOrEmpty(b)) b = fallback;
                if (string.IsNullOrEmpty(b)) b = DefaultBaseFor(face);

                list.Add(new SVTalkLine
                {
                    Base = b,
                    Face = face,
                    Text = o["text"]?.ToString() ?? "",
                });
            }

            return list;
        }

        static bool MentionsAbsentCrew(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var name in new[] { "凱", "塔克" })
            {
                if (!text.Contains(name)) continue;
                bool here = false;
                foreach (var c in SVCompanion.Active)
                    if (c != null && c.Type != null && c.Type.Name == name) { here = true; break; }
                if (!here) return true;
            }
            return false;
        }

        /// <summary>
        /// 表情所在資料夾的預設底圖。對照表寫在 JSON 最上層的 "_底圖預設"。
        /// 雪瑤雪霜這種一段裡兩個人輪流說話的，就靠這個自動配對到各自的身體。
        /// </summary>
        static string DefaultBaseFor(string face)
        {
            if (string.IsNullOrEmpty(face)) return "";

            int slash = face.LastIndexOf('/');
            if (slash <= 0) return "";

            string folder = face.Substring(0, slash);
            var map = Root()?["_底圖預設"] as JObject;
            string name = map?[folder]?.ToString();
            return string.IsNullOrEmpty(name) ? "" : folder + "/" + name;
        }

        /// <summary>
        /// 隊友倒下退場時的一句話。key 是角色名字；沒對到就走 _default（傭兵那種沒有立繪的）。
        /// </summary>
        public static SVTalkLine RetireLine(string who)
        {
            if (Root()?["retire"] is not JObject block) return default;

            var entry = (string.IsNullOrEmpty(who) ? null : block[who] as JObject)
                        ?? block["_default"] as JObject;
            if (entry == null) return default;

            var node = entry["lines"];

            // 分好感段的寫法：{ "low": [...], "mid": [...], "high": [...] }
            // 好感高的角色話會比較多（鈴音），也可以低好感講另一套（雪霜）
            if (node is JObject tiers)
                node = tiers[Tier]
                    ?? (Tier == "high" ? tiers["mid"] : null)
                    ?? tiers["low"] ?? tiers["mid"] ?? tiers["high"];

            if (node is not JArray lines || lines.Count == 0) return default;

            string face = entry["face"]?.ToString() ?? "";
            string text = lines[Random.Range(0, lines.Count)]?.ToString() ?? "";

            return new SVTalkLine
            {
                Base = string.IsNullOrEmpty(face) ? "" : DefaultBaseFor(face),
                Face = face,
                Text = text.Replace("{name}", PlayerName),
            };
        }

        /// <summary>男主的名字。本篇在 RMZSurvivorsBridge 裡填進來，台詞用 {name} 取用。</summary>
        public static string PlayerName = "男主";

        /// <summary>interact 區塊裡的一段（chat / no_gift），隨機一句。</summary>
        public static SVTalkLine InteractLine(string key, int heroine = 0)
        {
            var personal = Lines(heroine, key);
            if (personal.Count > 0) return personal[Random.Range(0, personal.Count)];
            if (Root()?["interact"] is not JObject block) return default;
            if (block[key] is not JArray array || array.Count == 0) return default;

            var o = array[Random.Range(0, array.Count)] as JObject;
            if (o == null) return default;

            return new SVTalkLine
            {
                Face = o["face"]?.ToString() ?? "",
                Text = o["text"]?.ToString() ?? "",
            };
        }

        public static List<string> InteractMenu()
        {
            var menu = new List<string>();

            if (Root()?["interact"] is JObject block && block["menu"] is JArray array)
                foreach (var item in array) menu.Add(item.ToString());

            if (menu.Count == 0) menu.AddRange(new[] { "送禮", "聊聊", "結束" });
            return menu;
        }

        // ---------------- 立繪 ----------------

        static readonly Dictionary<string, Sprite> FaceCache = new Dictionary<string, Sprite>();

        /// <summary>face 寫「資料夾/檔名」，例如「尤梨卡立繪/日常1」。</summary>
        public static Sprite Face(string face)
        {
            if (string.IsNullOrEmpty(face)) return null;
            if (FaceCache.TryGetValue(face, out var cached)) return cached;

            string path = Path.Combine(PictureRoot, face.Replace('/', Path.DirectorySeparatorChar) + ".png");

            if (!File.Exists(path))
            {
                Debug.LogWarning($"[倖存者] 找不到立繪：{path}");
                FaceCache[face] = null;
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            tex.LoadImage(File.ReadAllBytes(path), true);
            tex.name = face;

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                       new Vector2(0.5f, 0f), 100f, 0, SpriteMeshType.FullRect);
            FaceCache[face] = sprite;
            return sprite;
        }
    }

    /// <summary>
    /// 小遊戲對話裡「跟本篇借東西」的接縫。
    /// 由 RMZSurvivorsBridge 在出發前填好，RMZS 這邊只認這幾個委派，
    /// 不會反過來引用 RMZ。沒填的話互動選項會自動收起來。
    /// </summary>
    public static class SVTalkHooks
    {
        /// <summary>現在身上可以送的禮物：(道具 ID, 顯示名稱)。</summary>
        public static System.Func<List<(int id, string label)>> ListGifts;

        /// <summary>送出一份禮物，回傳要顯示給玩家看的一句話。</summary>
        public static System.Func<int, string> GiveGift;

        public static bool HasGifts => ListGifts != null && GiveGift != null;

        public static void Clear()
        {
            ListGifts = null;
            GiveGift = null;
        }
    }
}
