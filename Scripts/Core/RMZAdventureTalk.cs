// 認真探索的開場／結尾對話與送禮互動。
//
// 台詞放在 StreamingAssets/rmz/adventure_talk.json，改台詞不用重編譯。
// 讀不到檔案時整套功能自動關閉（只是沒有對話，探索本身照跑）。
//
// 好感／墮落走 RM 原本的變數：好感 82~86、墮落 87~91，
// 對應 RM 變數 59「選擇的隊伍」1~5。送禮的道具對應表就是 Items.json
// 141「-----好感墮落道具-----」與 158「-----特定禮物-----」那兩段。
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using RMZS.GodotPort;

namespace RMZ
{
    public static class RMZAdventureTalk
    {
        public const string FileName = "adventure_talk.json";

        static JObject _root;
        static bool _tried;

        public static bool Loaded => Root() != null;

        static JObject Root()
        {
            if (_tried) return _root;
            _tried = true;

            string path = Path.Combine(RMZConfig.RootPath, FileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[RMZ] 找不到 {path}，認真探索的對話功能先關閉。");
                return null;
            }

            try { _root = JObject.Parse(File.ReadAllText(path)); }
            catch (System.Exception e) { Debug.LogError($"[RMZ] {FileName} 解析失敗：{e.Message}"); }

            return _root;
        }

        /// <summary>改完 JSON 想立刻看效果的話呼叫這個（DEV 面板之類）。</summary>
        public static void Reload() { _tried = false; _root = null; }

        // ---------------- 台詞 ----------------

        /// <summary>一句台詞：文字，外加（可有可無的）立繪。</summary>
        public struct Line
        {
            public string Text;
            public string Face;
            public string Base;

            public bool Empty => string.IsNullOrEmpty(Text);
        }

        /// <summary>
        /// 好感分三段。話少的角色靠這個「隨著好感變得多話」——
        /// JSON 那邊寫成 { "low": [...], "mid": [...], "high": [...] } 就會分段，
        /// 直接寫成陣列的話三段共用，舊格式照樣能跑。
        /// </summary>
        public const int MidLove = 35;
        public const int HighLove = 70;

        static string TierOf(int heroine)
        {
            int love = (heroine >= 1 && heroine <= 5) ? RMZGifts.Love(heroine) : 0;
            return love >= HighLove ? "high" : love >= MidLove ? "mid" : "low";
        }

        static Line Pick(int heroine, string key)
        {
            var root = Root();
            if (root == null) return default;

            var block = root[heroine.ToString()] as JObject;
            var node = block?[key];
            if (node == null) return default;

            // 分好感段的寫法：先找對應的那一段，沒有就往下退
            if (node is JObject tiers)
            {
                string tier = TierOf(heroine);
                node = tiers[tier]
                    ?? (tier == "high" ? tiers["mid"] : null)
                    ?? tiers["low"]
                    ?? tiers["mid"]
                    ?? tiers["high"];
            }

            if (node is not JArray all || all.Count == 0) return default;

            // 提到班底（凱、塔克）的台詞，只有他們真的跟來時才用
            var list = FilterAbsentCrew(all);
            var line = ToLine(list[Random.Range(0, list.Count)]);

            // 沒指定底圖就用這位角色的預設底圖 —— 立繪是「底圖 + 表情」兩張疊的
            if (string.IsNullOrEmpty(line.Base)) line.Base = block?["portrait"]?.ToString();

            // 還是沒有的話，照表情所在的資料夾去查那個資料夾的預設底圖。
            // 雪瑤雪霜這種一段裡兩個人輪流說話的，就靠這個自動配對
            if (string.IsNullOrEmpty(line.Base)) line.Base = DefaultBaseFor(line.Face);
            return line;
        }

        /// <summary>
        /// 尤梨卡的搭檔凱、露娜的老公塔克只有「組隊（加入她的隊伍）」才會跟來；
        /// 僱傭或獨自組隊時只有她本人，提到他們的台詞就不該出現。
        /// 全部都被濾掉的話退回原本的清單（總比沒台詞好）。
        /// </summary>
        static readonly string[] CrewNames = { "凱", "塔克" };

        public static bool CrewPresent =>
            RMZGameState.Variable(RMZAdventureVars.Hire) != 1 && RMZGameState.Variable(RMZAdventureVars.Solo) != 1;

        static JArray FilterAbsentCrew(JArray list)
        {
            if (CrewPresent) return list;
            var kept = new JArray();
            foreach (var t in list)
            {
                string text = t is JObject o ? o["text"]?.ToString() : t?.ToString();
                bool mentions = false;
                if (text != null) foreach (var n in CrewNames) if (text.Contains(n)) { mentions = true; break; }
                if (!mentions) kept.Add(t);
            }
            return kept.Count > 0 ? kept : list;
        }

        /// <summary>
        /// 表情所在資料夾的預設底圖。對照表寫在 JSON 最上層的 "_底圖預設"。
        /// </summary>
        static string DefaultBaseFor(string face)
        {
            if (string.IsNullOrEmpty(face)) return null;

            int slash = face.LastIndexOf('/');
            if (slash <= 0) return null;

            string folder = face.Substring(0, slash);
            var map = Root()?["_底圖預設"] as JObject;
            string name = map?[folder]?.ToString();
            return string.IsNullOrEmpty(name) ? null : folder + "/" + name;
        }

        /// <summary>一格可以是純字串，也可以是 { "text": ..., "face": ... }。</summary>
        static Line ToLine(JToken token)
        {
            if (token == null) return default;
            if (token is JObject o)
                return new Line
                {
                    Text = o["text"]?.ToString(),
                    Face = o["face"]?.ToString(),
                    Base = o["base"]?.ToString(),
                };
            return new Line { Text = token.ToString() };
        }

        static string PickGift(int heroine, string key, string name)
        {
            var root = Root();
            if (root == null) return null;

            // 先看這位角色自己有沒有客製化的禮物反應（例如鈴音拒收禮物時的專屬台詞），
            // 沒有的話才退回通用的那組。key 前面加 gift_ 是為了跟 start/win/chat 那些分開放。
            var block = root[heroine.ToString()] as JObject;
            if (block?["gift_" + key] is JArray own && own.Count > 0)
                return own[Random.Range(0, own.Count)]?.ToString().Replace("{name}", name);

            if (root["gift"] is not JObject gift) return null;
            if (gift[key] is not JArray list || list.Count == 0) return null;

            return list[Random.Range(0, list.Count)]?.ToString().Replace("{name}", name);
        }

        /// <summary>聊聊的隨機小事件用：角色自己的台詞優先，沒有就退回通用的那組（純文字，不帶立繪）。</summary>
        static Line PickChatEvent(int heroine, string key)
        {
            var line = Pick(heroine, key);
            if (!line.Empty) return line;

            var root = Root();
            if (root?[key] is JArray list && list.Count > 0)
                return new Line { Text = list[Random.Range(0, list.Count)]?.ToString() };

            return default;
        }

        static Line WithDelta(Line line, int amount, bool positive)
        {
            if (line.Empty) return line;
            line.Text += $"\n（好感 {(positive ? "+" : "-")}{amount}）";
            return line;
        }

        public static string HeroineName(int heroine)
        {
            var root = Root();
            var block = root?[heroine.ToString()] as JObject;
            return block?["name"]?.ToString() ?? "";
        }

        // ---------------- 對外的三段流程 ----------------

        /// <summary>
        /// 出發前的開場白，純資料版：只回傳「誰講的、講什麼」，不碰任何 UI。
        /// 給 RMZExploreLoadScreen 的氣泡對話用——那邊是自動播放、不能選送禮或聊聊，
        /// 所以不走下面 PlayStart() 那一整套對話框流程。
        /// 讀不到台詞（或這趟沒有同行者）就回傳 (null, null)，呼叫端要自己擋。
        /// </summary>
        public static (string speaker, string text) StartLine(int heroine)
        {
            if (!Loaded) return (null, null);

            // 同行的如果不是一個人（目前是雙子），資料裡會多一組 members，
            // 每個成員有自己的名字跟台詞。這種情況隨機挑一位講話，
            // 所以每輪換一句就有可能換人開口。
            var member = PickMember(heroine);
            if (member != null)
            {
                var list = member["start"] as JArray;
                if (list != null && list.Count > 0)
                {
                    var picked = ToLine(list[Random.Range(0, list.Count)]);
                    if (!picked.Empty)
                        return (member["name"]?.ToString() ?? HeroineName(heroine), picked.Text);
                }
            }

            var line = Pick(heroine, "start");
            if (line.Empty) return (null, null);

            return (HeroineName(heroine), line.Text);
        }

        /// <summary>這位同行者底下有沒有分身（雙子那種）；有的話隨機回傳一位。</summary>
        static JObject PickMember(int heroine)
        {
            var block = Root()?[heroine.ToString()] as JObject;
            if (block?["members"] is not JArray members || members.Count == 0) return null;
            return members[Random.Range(0, members.Count)] as JObject;
        }

        /// <summary>
        /// 舊版：一句開場白，然後給玩家「送禮／聊聊／出發」，走本篇的對話框。
        /// 現在出發前改用讀取畫面的氣泡對話（見 StartLine），這支沒人呼叫了，
        /// 留著是保險——氣泡那邊真的出不了台詞的時候還能退回這一套。
        /// </summary>
        public static IEnumerator PlayStart()
        {
            int who = CurrentHeroine();
            if (!Loaded) yield break;

            yield return Say(who, Pick(who, "start"));

            // 獨自出發沒有互動對象
            if (who <= 0) yield break;

            while (true)
            {
                int choice = -1;
                yield return Ask(new List<string> { "送禮", "聊聊", "出發" }, r => choice = r);

                if (choice == 0) yield return GiveGift(who, false);
                else if (choice == 1) yield return DoChat(who);
                else { RMZTalkPortrait.Hide(); yield break; }
            }
        }

        /// <summary>回城後：依成敗講一句，然後再給一次互動機會。</summary>
        public static IEnumerator PlayEnd(bool cleared)
        {
            int who = CurrentHeroine();
            if(cleared && who>0){
                RMZTalkPortrait.Hide();
                yield return RMZBattleEvents.Play(who);
                yield break;
            }
            if(Loaded)yield return Say(who,Pick(who,cleared?"win":"lose"));
            RMZTalkPortrait.Hide();
        }

        /// <summary>插件指令用的版本：成敗直接讀變數 718。</summary>
        public static IEnumerator PlayEndFromVars() =>
            PlayEnd(RMZGameState.Variable(RMZAdventureVars.Result) == 1);

        // ---------------- 送禮 ----------------

        /// <summary>
        /// afterFailure：這趟沒過關。心情不好，送禮有機率被拒絕，
        /// 而且被拒絕時好感還會倒扣一點。
        /// </summary>
        static IEnumerator GiveGift(int heroine, bool afterFailure)
        {
            string name = HeroineName(heroine);
            var owned = RMZGifts.OwnedFor(heroine);

            if (owned.Count == 0)
            {
                yield return Say(heroine, PickGift(heroine, "none", name));
                yield break;
            }

            // 選一樣送。最後一個選項是取消
            var labels = new List<string>();
            foreach (var g in owned)
                labels.Add($"{g.Name}（{RMZParty.NumItems(g.ItemId)}）");
            labels.Add("算了");

            int picked = -1;
            yield return Ask(labels, r => picked = r);
            if (picked < 0 || picked >= owned.Count) yield break;

            var gift = owned[picked];
            RMZParty.GainItem(gift.ItemId, -1);

            // 失敗回來的時候，心情不好，禮物可能被婉拒——但送的終究是這位角色會喜歡的東西，
            // 頂多這次不收、好感沒增加，不會反過來扣好感。
            if (afterFailure && !gift.Guaranteed && Random.value < RMZGifts.RejectChanceAfterFailure)
            {
                yield return Say(heroine, PickGift(heroine, "bad", name));
                yield break;
            }

            bool ok = gift.Guaranteed || Random.value < RMZGifts.SuccessChance;

            // 每次送禮都重擲，同一樣東西這次加 12、下次加 18 都有可能
            int amount = gift.Roll();
            if (!ok) amount = Mathf.Max(1, amount / 3);

            if (gift.IsCorruption)
            {
                RMZGifts.AddFall(heroine, amount);
                yield return Say(heroine, PickGift(heroine, ok ? "fall_ok" : "fall_fail", name) +
                                          $"\n（墮落 +{amount}）");
            }
            else
            {
                RMZGifts.AddLove(heroine, amount);
                yield return Say(heroine, PickGift(heroine, ok ? "love_ok" : "love_fail", name) +
                                          $"\n（好感 +{amount}）");
            }
        }

        // ---------------- 聊聊 ----------------

        /// <summary>聊聊的好感／壞感機率。多數時候只是閒聊一句，不會加減東西。</summary>
        public const float ChatGoodChance = 0.18f;
        public const float ChatBadChance = 0.12f;
        public const int ChatGoodMin = 2, ChatGoodMax = 5;
        public const int ChatBadMin = 1, ChatBadMax = 3;

        static IEnumerator DoChat(int heroine)
        {
            yield return Say(heroine, Pick(heroine, "chat"));
            if (heroine <= 0) yield break;

            float roll = Random.value;
            if (roll < ChatGoodChance)
            {
                int amount = Random.Range(ChatGoodMin, ChatGoodMax + 1);
                RMZGifts.AddLove(heroine, amount);
                var line = PickChatEvent(heroine, "chat_good");
                if (!line.Empty) yield return Say(heroine, WithDelta(line, amount, true));
            }
            else if (roll < ChatGoodChance + ChatBadChance)
            {
                int amount = Random.Range(ChatBadMin, ChatBadMax + 1);
                RMZGifts.AddLove(heroine, -amount);
                var line = PickChatEvent(heroine, "chat_bad");
                if (!line.Empty) yield return Say(heroine, WithDelta(line, amount, false));
            }
        }

        // ---------------- 對話框的低階操作 ----------------

        /// <summary>
        /// 直接推一段話進 Game_Message 再等它被關掉。
        /// 這裡沒有走直譯器，因為探索的開頭與結尾是在橋接層的協程裡，
        /// 那時事件已經停在「等待小遊戲」那一格上了。
        /// </summary>
        static IEnumerator Say(int heroine, Line line)
        {
            if (line.Empty) yield break;

            if (!string.IsNullOrEmpty(line.Face) || !string.IsNullOrEmpty(line.Base))
                RMZTalkPortrait.Show(line.Base, line.Face);

            var msg = RMZMessage.Instance;
            msg.Clear();
            msg.SpeakerName = HeroineName(heroine);
            foreach (var part in line.Text.Split('\n')) msg.Add(part);

            yield return null;
            while (msg.IsBusy) yield return null;
        }

        /// <summary>送禮那邊給的是現成的字串，沒有立繪。</summary>
        static IEnumerator Say(int heroine, string text) =>
            Say(heroine, new Line { Text = text });

        static IEnumerator Ask(List<string> options, System.Action<int> onPicked)
        {
            var msg = RMZMessage.Instance;
            msg.Clear();
            msg.Choices.AddRange(options);
            msg.ChoiceCancel = options.Count - 1;

            int result = -1;
            msg.OnChoice = r => result = r;

            yield return null;
            while (msg.IsBusy) yield return null;

            onPicked?.Invoke(result >= 0 ? result : options.Count - 1);
        }

        /// <summary>RM 變數 59「選擇的隊伍」。0 = 獨自。</summary>
        static int CurrentHeroine()
        {
            if (RMZGameState.Variable(RMZAdventureVars.Solo) == 1) return 0;
            return Mathf.Clamp(RMZGameState.Variable(RMZAdventureVars.ChosenTeam), 0, 5);
        }
    }


}