// 文字在地化，對應 DKTools_Localization。
//
// 你的事件文字都是寫成 {翻譯鍵} 的形式，例如 {請選擇遊戲難度}，
// 真正的文字放在 locales/<語言>/*.json。
// 目前這份「瘦身版」的 locales 資料夾是空的，所以找不到翻譯時
// 就把大括號去掉直接顯示鍵本身 —— 因為鍵本身就是中文，讀起來剛好正確。
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Godot;

namespace RMZ
{
    public static class RMZLocalization
    {
        static readonly Dictionary<string, string> Table = new Dictionary<string, string>();
        static readonly Regex Key = new Regex(@"\{([^{}\r\n]+)\}", RegexOptions.Compiled);

        public static string Locale { get; private set; } = "CN";
        public static bool Loaded { get; private set; }

        public static void Load(string locale)
        {
            Locale = locale;
            Table.Clear();
            Loaded = true;

            string dir = Path.Combine(RMZConfig.RootPath, "locales", locale);
            if (!Directory.Exists(dir))
            {
                GD.Print($"[RMZ] 沒有 {locale} 的翻譯檔（{dir}），文字會直接顯示原始的翻譯鍵。");
                return;
            }

            int count = 0;
            foreach (var file in Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var token = JToken.Parse(File.ReadAllText(file));
                    count += Flatten(token);
                }
                catch (System.Exception e)
                {
                    GD.PushWarning($"[RMZ] 翻譯檔 {Path.GetFileName(file)} 解析失敗：{e.Message}");
                }
            }

            GD.Print($"[RMZ] 載入 {locale} 翻譯 {count} 筆。");
        }

        /// <summary>DKTools 的檔案可能是巢狀物件，全部攤平成 key -> text。</summary>
        static int Flatten(JToken token)
        {
            int n = 0;
            if (token is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    if (prop.Value.Type == JTokenType.String)
                    {
                        Table[prop.Name] = prop.Value.ToString();
                        n++;
                    }
                    else n += Flatten(prop.Value);
                }
            }
            return n;
        }

        /// <summary>把文字裡的 {鍵} 換成翻譯；查不到就去掉大括號。</summary>
        public static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;

            return Key.Replace(text, m =>
            {
                string key = m.Groups[1].Value;
                return Table.TryGetValue(key, out var value) ? value : key;
            });
        }
    }
}
