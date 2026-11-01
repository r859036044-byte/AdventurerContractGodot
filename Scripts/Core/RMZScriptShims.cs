// 事件裡的 JavaScript 腳本，挑最常見的樣板用 C# 對應實作。
//
// 全專案有約 23000 行嵌入的 JS。完整支援要另外內嵌 JS 直譯器，
// 但其中很大一部分是重複的單行樣板（光 $gameTemp.clearDestination() 就 654 次），
// 這裡先把這些接起來，剩下真正有邏輯的再逐一處理。
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;

namespace RMZ
{
    public static class RMZScriptShims
    {
        static readonly Regex Balloon = new Regex(
            @"requestBalloon\(\s*(\$gamePlayer|\$gameMap\.event\(\s*(\d+)\s*\)|this\.character\(\s*(-?\d+)\s*\))\s*,\s*(\d+)\s*\)",
            RegexOptions.Compiled);

        static readonly Regex SetVariable = new Regex(
            @"^\s*\$gameVariables\.setValue\(\s*(\d+)\s*,\s*(-?\d+)\s*\)\s*;?\s*$", RegexOptions.Compiled);

        // 有些事件把變數當文字用（選單項目的動態標籤），例如
        // $gameVariables.setValue(621, "{交談}")。MZ 的變數本來就是動態型別，
        // 這裡另外接一條，數字版那條原本會直接吃不進去、被記成「腳本未執行」跳過。
        static readonly Regex SetVariableString = new Regex(
            @"^\s*\$gameVariables\.setValue\(\s*(\d+)\s*,\s*[""']((?:[^""'\\]|\\.)*)[""']\s*\)\s*;?\s*$", RegexOptions.Compiled);

        static readonly Regex SetSwitch = new Regex(
            @"^\s*\$gameSwitches\.setValue\(\s*(\d+)\s*,\s*(true|false)\s*\)\s*;?\s*$", RegexOptions.Compiled);

        static readonly Regex GainGold = new Regex(
            @"^\s*\$gameParty\.gainGold\(\s*(-?\d+)\s*\)\s*;?\s*$", RegexOptions.Compiled);

        static readonly Regex ClearDestination = new Regex(
            @"^\s*\$gameTemp\.clearDestination\(\s*\)\s*;?\s*$", RegexOptions.Compiled);

        static readonly Regex Comment = new Regex(@"^\s*(//.*)?$", RegexOptions.Compiled);

        static readonly Regex PictureAction = new Regex(
            @"pictureAction\(\s*(\d+)\s*,\s*['""](\w+)['""]\s*\)", RegexOptions.Compiled);

        // 標題畫面的「挑戰」用 DataManager.savefileExists(20) 判斷有沒有挑戰存檔
        static readonly Regex SaveFileExists = new Regex(
            @"DataManager\.savefileExists\(\s*(\d+)\s*\)", RegexOptions.Compiled);

        // ---- 事件裡用 prompt() 讓玩家自己取名的那一套 ----
        // 樣板長這樣：
        //   var sRst = "";
        //   while (!sRst || sRst.trim().length === 0) { sRst = prompt("【請輸入角色名字】", ""); }
        //   $gameActors.actor(1).setName(sRst);
        // prompt() 在 Unity 沒有，改成叫出 RMZInputWindow 的名字輸入框。

        static readonly Regex SetNameCall = new Regex(
            @"\$gameActors\.actor\(\s*(\d+)\s*\)\.setName\(", RegexOptions.Compiled);

        static readonly Regex SetNameLiteral = new Regex(
            @"\$gameActors\.actor\(\s*(\d+)\s*\)\.setName\(\s*['""]([^'""]*)['""]\s*\)", RegexOptions.Compiled);

        static readonly Regex PromptCall = new Regex(
            @"prompt\(\s*(['""])((?:\\.|(?!\1)[^\\])*)\1", RegexOptions.Compiled);

        static readonly Regex InputClear = new Regex(
            @"^\s*(Input|TouchInput)\.clear\(\s*\)\s*;?\s*$", RegexOptions.Compiled);

        static readonly Regex LoneBrace = new Regex(@"^\s*[{}]\s*;?\s*$", RegexOptions.Compiled);

        // ---- 整段（多行）才看得出來的樣板 ----
        // 355/655 是一段連續的腳本，很多樣板（for 迴圈之類）逐行看不出來，要合併起來才認得。

        static readonly Regex ClearAllSwitches = new Regex(
            @"\$dataSystem\.switches\.length[\s\S]{0,200}?\$gameSwitches\.setValue\([^)]*,\s*false\s*\)",
            RegexOptions.Compiled);

        static readonly Regex ClearAllVariables = new Regex(
            @"\$dataSystem\.variables\.length[\s\S]{0,200}?\$gameVariables\.setValue\([^)]*,\s*0\s*\)",
            RegexOptions.Compiled);

        static readonly Regex GoldAssign = new Regex(
            @"\$gameParty\._gold\s*=\s*(\d+)\s*;?", RegexOptions.Compiled);

        static readonly Regex ClearBag = new Regex(
            @"\$gameParty\._(items|weapons|armors)\s*=\s*\{\s*\}\s*;?", RegexOptions.Compiled);

        /// <summary>
        /// 條件分歧裡的腳本。看得懂就把結果放進 result 並回傳 true。
        /// 目前支援 Hendrix 的 pictureAction()，也就是圖片按鈕。
        /// </summary>
        public static bool TryEvaluateCondition(string script, int ownerId, out bool result)
        {
            result = false;
            if (string.IsNullOrEmpty(script)) return false;

            var save = SaveFileExists.Match(script);
            if (save.Success)
            {
                result = RMZSaveSystem.Exists(int.Parse(save.Groups[1].Value));
                return true;
            }

            var m = PictureAction.Match(script);
            if (!m.Success) return false;

            result = RMZPictureInput.Action(int.Parse(m.Groups[1].Value), m.Groups[2].Value.ToLowerInvariant(), ownerId);
            return true;
        }

        /// <summary>
        /// 這一行是不是「叫玩家取名」？是的話回傳 true，並給出角色 ID 與提示文字。
        /// 呼叫端要負責開輸入視窗並等它關掉。
        /// </summary>
        public static bool TryNameInput(string script, out int actorId, out string prompt)
        {
            actorId = 0;
            prompt = null;
            if (string.IsNullOrEmpty(script)) return false;

            // setName("固定名字") 不是取名，交給 TryRun 直接設定
            if (SetNameLiteral.IsMatch(script)) return false;

            var m = SetNameCall.Match(script);
            if (!m.Success) return false;

            actorId = int.Parse(m.Groups[1].Value);

            var pm = PromptCall.Match(script);
            if (pm.Success) prompt = Unescape(pm.Groups[2].Value);
            return true;
        }

        /// <summary>
        /// 整段腳本的樣板。目前接的是「新遊戲重置」那一段：
        /// 清空所有開關、變數、金錢與背包。看得懂就做掉並回傳 true。
        /// </summary>
        // CE48 組隊畫面「換選另一位女主」：把原本選的那位的戰力從變數 9 扣掉、她的佣金變數歸零。
        // (function(){var old=$gameVariables.value(59);var costs={1:700,...};var tiers={1:669,...};
        //  if(old>0&&old!==1&&costs[old]!==undefined){...}})();
        static readonly Regex SwitchHeroine = new Regex(
            @"var old=\$gameVariables\.value\((\d+)\);var costs=\{([^}]*)\};var tiers=\{([^}]*)\};if\(old>0&&old!==(\d+)&&costs\[old\]!==undefined\)\{\$gameVariables\.setValue\((\d+),");

        static bool TrySwitchHeroine(string block)
        {
            var m = SwitchHeroine.Match(block);
            if (!m.Success) return false;
            int teamVar = int.Parse(m.Groups[1].Value);
            int self = int.Parse(m.Groups[4].Value);
            int powerVar = int.Parse(m.Groups[5].Value);
            static Dictionary<int, int> Parse(string s)
            {
                var d = new Dictionary<int, int>();
                foreach (var part in s.Split(','))
                {
                    var kv = part.Split(':');
                    if (kv.Length == 2 && int.TryParse(kv[0].Trim(), out int k) && int.TryParse(kv[1].Trim(), out int v)) d[k] = v;
                }
                return d;
            }
            var costs = Parse(m.Groups[2].Value);
            var tiers = Parse(m.Groups[3].Value);
            int old = RMZGameState.Variable(teamVar);
            if (old > 0 && old != self && costs.TryGetValue(old, out int cost))
            {
                RMZGameState.SetVariable(powerVar, RMZGameState.Variable(powerVar) - cost);
                if (tiers.TryGetValue(old, out int tierVar)) RMZGameState.SetVariable(tierVar, 0);
            }
            return true;
        }

        public static bool TryRunBlock(string block)
        {
            if (string.IsNullOrWhiteSpace(block)) return true;

            if (RMZFinanceScripts.TryRun(block)) return true;
            if (TrySwitchHeroine(block)) return true;

            bool handled = false;

            bool allowReset = RMZBootstrap.Instance == null || RMZBootstrap.Instance.AllowScriptResetAll;
            if (!allowReset && (ClearAllSwitches.IsMatch(block) || ClearAllVariables.IsMatch(block)))
            {
                GD.Print("[RMZ] 事件腳本要清空所有開關／變數，但 Allow Script Reset All 是關的，先跳過。");
                return true;
            }

            if (ClearAllSwitches.IsMatch(block))
            {
                RMZGameState.ClearSwitches();
                GD.Print("[RMZ] 事件腳本：清空所有開關");
                handled = true;
            }

            if (ClearAllVariables.IsMatch(block))
            {
                RMZGameState.ClearVariables();
                GD.Print("[RMZ] 事件腳本：清空所有變數");
                handled = true;
            }

            var gold = GoldAssign.Match(block);
            if (gold.Success)
            {
                RMZParty.SetGold(int.Parse(gold.Groups[1].Value));
                handled = true;
            }

            if (ClearBag.IsMatch(block))
            {
                RMZParty.ClearBags();
                handled = true;
            }

            if (handled) RMZEventRunner.Instance?.RequestRefresh();
            return handled;
        }

        /// <summary>看得懂就執行並回傳 true；看不懂回傳 false，由呼叫端記錄。</summary>
        public static bool TryRun(string script, int eventId, int mapId)
        {
            if (string.IsNullOrWhiteSpace(script)) return true;
            if (Comment.IsMatch(script)) return true;                 // 空行或註解
            if (ClearDestination.IsMatch(script))
            {
                RMZPlayer.Instance?.ClearDestination();   // 取消滑鼠指定的目的地
                return true;
            }

            // 取名樣板的周邊雜訊：吃掉，不用記進 Console
            if (InputClear.IsMatch(script)) return true;
            if (LoneBrace.IsMatch(script)) return true;

            if (PromptCall.IsMatch(script)) return true;   // 提示文字在整段判定時就取走了

            var lit = SetNameLiteral.Match(script);
            if (lit.Success)
            {
                var a = RMZDatabase.Actor(int.Parse(lit.Groups[1].Value));
                if (a != null) a.name = lit.Groups[2].Value;
                return true;
            }

            var m = SetVariable.Match(script);
            if (m.Success)
            {
                RMZGameState.SetVariable(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
                RMZEventRunner.Instance?.RequestRefresh();
                return true;
            }

            m = SetVariableString.Match(script);
            if (m.Success)
            {
                RMZGameState.SetVariableString(int.Parse(m.Groups[1].Value), m.Groups[2].Value);
                RMZEventRunner.Instance?.RequestRefresh();
                return true;
            }

            m = SetSwitch.Match(script);
            if (m.Success)
            {
                RMZGameState.SetSwitch(int.Parse(m.Groups[1].Value), m.Groups[2].Value == "true");
                RMZEventRunner.Instance?.RequestRefresh();
                return true;
            }

            m = GainGold.Match(script);
            if (m.Success)
            {
                RMZParty.GainGold(int.Parse(m.Groups[1].Value));
                return true;
            }

            m = Balloon.Match(script);
            if (m.Success)
            {
                var target = ResolveCharacter(m, eventId);
                target?.RequestBalloon(int.Parse(m.Groups[4].Value));
                return true;
            }

            return false;
        }

        /// <summary>把 JS 字串裡的跳脫字元還原（只處理 \n \t \" \' \\）。</summary>
        static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("\\n", "\n").Replace("\\t", "\t")
                    .Replace("\\\"", "\"").Replace("\\'", "'").Replace("\\\\", "\\");
        }

        static RMZCharacterBase ResolveCharacter(Match m, int eventId)
        {
            if (m.Groups[1].Value.StartsWith("$gamePlayer")) return RMZPlayer.Instance;

            if (m.Groups[2].Success)
                return RMZEventRunner.Instance?.EventById(int.Parse(m.Groups[2].Value));

            if (m.Groups[3].Success)
            {
                int id = int.Parse(m.Groups[3].Value);
                if (id < 0) return RMZPlayer.Instance;
                return RMZEventRunner.Instance?.EventById(id > 0 ? id : eventId);
            }

            return null;
        }
    }
}
