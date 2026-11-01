// 事件指令直譯器，對應 MZ 的 Game_Interpreter。
//
// 分歧與循環的處理方式跟 MZ 一樣：靠指令的 indent 縮排來跳過區塊，
// 所以不需要先把指令樹重建出來，逐條掃過去就好。
//
// 目前不支援的：355/655 的 JavaScript 腳本（全專案約 23000 行）、戰鬥、商店。
// 這些會記在 Console，方便之後逐一補。
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Godot;

namespace RMZ
{
    public class RMZInterpreter
    {
        public int EventId { get; private set; }
        public int MapId { get; private set; }

        List<EventCommand> _list;
        int _index;
        int _indent;
        float _waitCount;
        string _waitMode = "";

        readonly Dictionary<int, object> _branch = new Dictionary<int, object>();
        RMZInterpreter _child;
        readonly int _depth;

        RMZCharacterBase _waitingCharacter;

        public RMZInterpreter(int depth = 0) => _depth = depth;

        public bool IsRunning => _list != null;

        // ---- 診斷用：卡住時要看得出來是誰、卡在哪一條 ----
        public int CurrentIndex => _index;
        public int CommandCount => _list != null ? _list.Count : 0;
        public int CurrentCode => (_list != null && _index >= 0 && _index < _list.Count) ? _list[_index].code : 0;
        public string WaitMode => _waitMode;
        public float WaitCount => _waitCount;

        public void Clear()
        {
            _list = null;
            _index = 0;
            _waitCount = 0f;
            _waitMode = "";
            _child = null;
            _waitingCharacter = null;
        }

        public void Setup(List<EventCommand> list, int eventId, int mapId)
        {
            Clear();
            _list = list;
            EventId = eventId;
            MapId = mapId;
            _branch.Clear();
        }

        public void Update(float frames)
        {
            // 一幀最多跑這麼多條指令。事件寫成無窮迴圈時才不會把編輯器卡死。
            int budget = 20000;

            while (IsRunning)
            {
                if (UpdateChild(frames) || UpdateWait(frames)) break;
                if (!ExecuteCommand()) break;

                if (--budget <= 0)
                {
                    GD.PushWarning($"[RMZ] 事件 {EventId} 這一幀執行超過 20000 條指令，可能有無窮迴圈，先中斷。");
                    break;
                }
            }
        }

        bool UpdateChild(float frames)
        {
            if (_child == null) return false;
            _child.Update(frames);
            if (_child.IsRunning) return true;
            _child = null;
            return false;
        }

        bool UpdateWait(float frames)
        {
            if (_waitCount > 0f)
            {
                // 快轉時「等待」指令跑快一點
                _waitCount -= frames * RMZFastForward.WaitMultiplier;
                return true;
            }

            switch (_waitMode)
            {
                case "tutorial":
                    if(RMZTutorialOverlay.IsOpen) return true;
                    break;
                case "itemshop":
                    if (RMZItemShop.IsOpen) return true;
                    break;
                case "guildpanel":
                    if(RMZGuildPanels.IsOpen) return true;
                    _waitMode = "";
                    var hud = RMZDatabase.CommonEvent(89);
                    if(hud?.list != null) { _child = new RMZInterpreter(_depth + 1); _child.Setup(hud.list, EventId, MapId); return true; }
                    break;
                case "message":
                    if (RMZMessage.Instance.IsBusy) return true;
                    break;
                case "input":
                    if (RMZInputWindow.Instance != null && RMZInputWindow.Instance.IsBusy) return true;
                    break;
                case "balloon":
                    if (_waitingCharacter != null && _waitingCharacter.IsBalloonPlaying) return true;
                    break;
                case "animation":
                    if (RMZAnimationPlayer.Instance != null && RMZAnimationPlayer.Instance.IsPlaying) return true;
                    break;
                case "route":
                    // 跟 MZ 一樣：等到強制移動路線跑完為止
                    if (_waitingCharacter != null && _waitingCharacter.RouteForcing) return true;
                    break;
                case "fade":
                    if (RMZScreen.Instance != null && RMZScreen.Instance.IsFading) return true;
                    break;
                case "transfer":
                    if (RMZBootstrap.Instance != null && RMZBootstrap.Instance.IsTransferring) return true;
                    break;
                case "savescene":
                    if (RMZSaveScene.IsOpen) return true;
                    break;
                case "endtalk":
                    if (RMZAdventureTalkRunner.IsBusy) return true;
                    break;
                case "enhance":
                    if (RMZEnhanceWindow.IsOpen) return true;
                    break;
                case "slavework":
                    if (RMZSlaveWorkWindow.IsOpen) return true;
                    break;
                case "optionsscene":
                    if (RMZOptionsScene.IsOpen) return true;
                    break;
                case "tavern":
                    if (RMZTavernWindow.IsOpen) return true;
                    break;
                case "minigame":
                    // 認真探索：整段本篇被 SetActive(false) 冰住，這裡回來時才會再被問到。
                    if (RMZSurvivorsBridge.IsBusy) return true;
                    break;
                case "report":
                    if (RMZLootReport.IsOpen) return true;
                    break;
                case "tempt":
                    if (RMZTemptWindow.IsOpen) return true;
                    break;
                case "union":
                    if (RMZUnionWindow.IsOpen) return true;
                    break;
                case "slaveshop":
                    if (RMZSlaveShopWindow.IsOpen) return true;
                    break;
                case "defense":
                    if (RMZDefenseBridge.IsBusy) return true;
                    break;
            }

            if (_waitMode == "route" && _waitingCharacter != null)
            {
                var c = _waitingCharacter;
                RMZDebug.Log(() => $"事件{EventId} 移動路線結束 → {c.Name} 停在 ({c.X},{c.Y})");
            }

            _waitMode = "";
            _waitingCharacter = null;
            return false;
        }

        EventCommand Current() => (_list != null && _index < _list.Count) ? _list[_index] : null;

        bool ExecuteCommand()
        {
            var command = Current();
            if (command == null) { Clear(); return false; }

            _indent = command.indent;
            bool advance = Dispatch(command);
            if (!advance) return false;

            _index++;
            return true;
        }

        void SkipBranch()
        {
            while (_index + 1 < _list.Count && _list[_index + 1].indent > _indent) _index++;
        }

        // ================= 指令分派 =================

        bool Dispatch(EventCommand c)
        {
            var p = c.parameters;

            switch (c.code)
            {
                case 0: return true;
                case 108: case 408: return true;   // 註解
                case 657: return true;             // 插件指令的參數說明，只是給編輯器看的
                case 605: return true;             // 物品選擇的後續資料
                case 134: case 135: case 136: return true;  // 禁止存檔／選單／遇敵，選單系統做好再接

                // ---- 訊息 ----
                case 101: return Command101(p);
                case 401: return true;   // 文章內容在 101 就一起讀走了
                case 102: return Command102(p);
                case 402: return Command402(p);
                case 403: return Command403();
                case 404: return true;
                case 103: return Command103(p);
                case 303: return Command303(p);
                case 316: return Command316(p);
                case 319: return Command319(p);
                case 320: return Command320(p);
                case 105: case 405: return true;   // 捲動文章：先略過

                // ---- 流程控制 ----
                case 111: return Command111(p);
                case 411:
                    // 跟 MZ 一樣：只有條件明確是 false 才走「否則」；跳標籤進來（狀態被清掉）時也跳過
                    if (!(_branch.TryGetValue(_indent, out var b) && b is bool ok && !ok)) SkipBranch();
                    return true;
                case 412: return true;
                case 112: return true;
                case 413: return Command413();
                case 113: return Command113();
                case 115: Clear(); return false;

                // 352 = 開啟存檔畫面。RM 是切到 Scene_Save，這裡是蓋一層 Canvas
                case 352:
                    RMZSaveScene.Open(RMZSaveScene.Mode.Save);
                    _waitMode = "savescene";
                    return true;
                case 117: return Command117(p);
                case 118: return true;
                case 119: return Command119(p);

                // ---- 開關 / 變數 ----
                case 121: return Command121(p);
                case 122: return Command122(p);
                case 123: return Command123(p);

                // ---- 隊伍 ----
                case 125: RMZParty.GainGold(Operand(p, 0, 1, 2)); return true;
                case 126: RMZParty.GainItem(Num(p, 0), Operand(p, 1, 2, 3)); return true;
                case 127: RMZParty.GainWeapon(Num(p, 0), Operand(p, 1, 2, 3)); return true;
                case 128: RMZParty.GainArmor(Num(p, 0), Operand(p, 1, 2, 3)); return true;

                // ---- 地圖與角色 ----
                case 201: return Command201(p);
                case 203: return Command203(p);
                case 322: return Command322(p);
                case 204: return true;               // 地圖捲動：鏡頭系統接手後再補
                case 205: return Command205(p);
                case 505: return true;               // 移動路線的內容已在 205 一起讀走
                case 211: if (RMZPlayer.Instance != null) RMZPlayer.Instance.Transparent = Num(p, 0) == 0; return true;
                case 212: return Command212(p);
                case 213: return Command213(p);
                case 214: return Command214();

                // ---- 畫面 ----
                case 221: RMZScreen.Instance?.StartFadeOut(24f); _waitMode = "fade"; return true;
                case 222: RMZScreen.Instance?.StartFadeIn(24f); _waitMode = "fade"; return true;
                case 223: return Command223(p);
                case 224: return Command224(p);
                case 225: return Command225(p);
                case 230:
                    _waitCount = Num(p, 0);
                    RMZDebug.Log(() => $"事件{EventId} 等待 {_waitCount} 幀");
                    return true;
                case 236: return true;               // 天氣

                // ---- 圖片 ----
                case 231: return Command231(p);
                case 232: return Command232(p);
                case 233: RMZScreen.Instance?.Picture(Num(p, 0))?.Rotate(Num(p, 1)); return true;
                case 234: return Command234(p);
                case 235: RMZScreen.Instance?.ErasePicture(Num(p, 0)); return true;

                // ---- 音效 ----
                case 241: RMZAudio.Instance?.PlayBgm(Audio(p, 0)); return true;
                case 242: RMZAudio.Instance?.FadeOutBgm(Num(p, 0)); return true;
                case 243: RMZAudio.Instance?.SaveBgm(); return true;
                case 244: RMZAudio.Instance?.ReplayBgm(); return true;
                case 245: RMZAudio.Instance?.PlayBgs(Audio(p, 0)); return true;
                case 249: RMZAudio.Instance?.PlayMe(Audio(p, 0)); return true;
                case 250: RMZAudio.Instance?.PlaySe(Audio(p, 0)); return true;
                case 251: RMZAudio.Instance?.StopSe(); return true;

                // ---- 尚未實作 ----
                case 355: return Command355(p);
                case 655: return Command355(p);   // 理論上不會單獨出現，355 會把後面的 655 一起讀走
                case 357: return Command357(p);

                default:
                    if (!_reported.Contains(c.code))
                    {
                        _reported.Add(c.code);
                        GD.Print($"[RMZ] 事件指令 {c.code} 尚未實作（之後補）。");
                    }
                    return true;
            }
        }

        static readonly HashSet<int> _reported = new HashSet<int>();

        // ================= 各指令 =================

        bool Command101(List<object> p)
        {
            if (RMZMessage.Instance.IsBusy) return false;   // 上一段還沒關掉，這一幀先等

            var msg = RMZMessage.Instance;
            msg.Clear();
            msg.FaceName = Str(p, 0);
            msg.FaceIndex = Num(p, 1);
            msg.Background = Num(p, 2);
            msg.PositionType = Num(p, 3);
            // 第 5 個參數是說話者名字，舊版的事件可能沒有這一欄
            msg.SpeakerName = p != null && p.Count > 4 ? Str(p, 4) : "";

            // 跟 MZ 一樣，把後面連續的 401 一次讀完，再看看是不是接著選項
            while (NextCode() == 401)
            {
                _index++;
                msg.Add(Str(Current().parameters, 0));
            }

            if (NextCode() == 102)
            {
                _index++;
                SetupChoices(Current().parameters);
            }
            else if (NextCode() == 103)
            {
                _index++;
                Command103(Current().parameters);
            }

            _waitMode = "message";
            return true;
        }

        bool Command102(List<object> p)
        {
            SetupChoices(p);
            _waitMode = "message";
            return true;
        }

        void SetupChoices(List<object> p)
        {
            var msg = RMZMessage.Instance;
            msg.Choices.Clear();

            if (p != null && p.Count > 0 && p[0] is JArray arr)
                foreach (var t in arr) msg.Choices.Add(t.ToString());

            msg.ChoiceCancel = Num(p, 1);
            msg.ChoiceDefault = p != null && p.Count > 2 ? Num(p, 2) : 0;
            msg.ChoiceResult = -1;

            int indent = _indent;
            msg.OnChoice = n => _branch[indent] = n;
        }

        bool Command402(List<object> p)
        {
            // 選了別的選項就整段跳過
            if (!(_branch.TryGetValue(_indent, out var v) && v is int n && n == Num(p, 0))) SkipBranch();
            return true;
        }

        bool Command403()
        {
            // 取消分支：只有真的按了取消（結果為負）才執行
            if (_branch.TryGetValue(_indent, out var v) && v is int n && n >= 0) SkipBranch();
            return true;
        }

        int NextCode() => (_index + 1 < _list.Count) ? _list[_index + 1].code : 0;

        /// <summary>數值輸入處理。params: [變數ID, 位數]</summary>
        bool Command103(List<object> p)
        {
            var window = RMZInputWindow.Instance;
            if (window == null)
            {
                RMZGameState.SetVariable(Num(p, 0), 0);
                return true;
            }

            window.OpenNumber(Num(p, 0), Num(p, 1));
            _waitMode = "input";
            return true;
        }

        /// <summary>名稱輸入處理。params: [角色ID, 最大字數]</summary>
        bool Command303(List<object> p)
        {
            var window = RMZInputWindow.Instance;
            if (window == null) return true;

            window.OpenName(Num(p, 0), Num(p, 1));
            _waitMode = "input";
            return true;
        }

        /// <summary>顯示動畫。params: [對象角色, 動畫ID, 等待完成]</summary>
        bool Command212(List<object> p)
        {
            var player = RMZAnimationPlayer.Instance;
            if (player == null) return true;

            var target = CharacterFor(Num(p, 0));
            player.Play(Num(p, 1), target);

            if (p != null && p.Count > 2 && Num(p, 2) == 1) _waitMode = "animation";
            return true;
        }

        /// <summary>變更等級。params: [對象類型, 角色ID或變數, 增減, 運算元類型, 值, 顯示升級訊息]</summary>
        bool Command316(List<object> p)
        {
            int actorId = Num(p, 0) == 0 ? Num(p, 1) : RMZGameState.Variable(Num(p, 1));
            int value = Num(p, 3) == 0 ? Num(p, 4) : RMZGameState.Variable(Num(p, 4));
            if (Num(p, 2) == 1) value = -value;   // 1 = 減少

            foreach (int id in TargetActors(actorId))
                RMZActors.ChangeLevel(id, RMZActors.Level(id) + value);

            return true;
        }

        /// <summary>變更裝備。params: [角色ID, 裝備類型(1起算), 道具ID]</summary>
        bool Command319(List<object> p)
        {
            RMZActors.ChangeEquipById(Num(p, 0), Num(p, 1), Num(p, 2));
            return true;
        }

        /// <summary>角色ID 為 0 時代表全隊。</summary>
        static IEnumerable<int> TargetActors(int actorId)
        {
            if (actorId > 0) { yield return actorId; yield break; }
            foreach (int id in RMZActors.PartyMembers) yield return id;
        }

        /// <summary>變更角色名稱。params: [角色ID, 名稱]</summary>
        bool Command320(List<object> p)
        {
            var actor = RMZDatabase.Actor(Num(p, 0));
            if (actor != null) actor.name = Str(p, 1);
            return true;
        }

        static readonly HashSet<int> ReportedConditions = new HashSet<int>();

        void LogUnsupportedCondition(int type)
        {
            if (!ReportedConditions.Add(type)) return;
            GD.PushWarning($"[RMZ] 條件分歧類型 {type} 還沒支援，一律當成立。" +
                             "一連串互斥的分歧會全部進去，畫面可能出現疊圖。");
        }

        bool Command111(List<object> p)
        {
            bool result = EvaluateCondition(p);
            _branch[_indent] = result;
            if (!result) SkipBranch();
            return true;
        }

        bool EvaluateCondition(List<object> p)
        {
            switch (Num(p, 0))
            {
                case 0: return RMZGameState.Switch(Num(p, 1)) == (Num(p, 2) == 0);

                case 1:
                {
                    int value1 = RMZGameState.Variable(Num(p, 1));
                    int value2 = Num(p, 2) == 0 ? Num(p, 3) : RMZGameState.Variable(Num(p, 3));
                    switch (Num(p, 4))
                    {
                        case 0: return value1 == value2;
                        case 1: return value1 >= value2;
                        case 2: return value1 <= value2;
                        case 3: return value1 > value2;
                        case 4: return value1 < value2;
                        case 5: return value1 != value2;
                    }
                    return false;
                }

                case 2:
                    return RMZGameState.SelfSwitch(MapId, EventId, Str(p, 1)) == (Num(p, 2) == 0);

                case 7:
                {
                    int gold = RMZParty.Gold, value = Num(p, 1);
                    switch (Num(p, 2))
                    {
                        case 0: return gold >= value;
                        case 1: return gold <= value;
                        case 2: return gold < value;
                    }
                    return false;
                }

                case 4:
                {
                    // 角色條件。params = [4, 角色ID, 子類型, 值]
                    // 子類型：0=在隊伍中 1=名字 2=職業 3=技能 4=武器 5=防具 6=狀態
                    int actorId = Num(p, 1);
                    int sub = Num(p, 2);
                    switch (sub)
                    {
                        case 0: return RMZActors.InParty(actorId);
                        case 1: return RMZDatabase.Actor(actorId)?.name == Str(p, 3);
                        case 2: return RMZActors.Actor(actorId)?.ClassId == Num(p, 3);
                        case 3: return RMZActors.HasSkill(actorId, Num(p, 3));
                        case 4: return RMZActors.HasWeapon(actorId, Num(p, 3));
                        case 5: return RMZActors.HasArmor(actorId, Num(p, 3));
                        case 6: return RMZActors.HasState(actorId, Num(p, 3));
                    }
                    return false;
                }

                case 8: return RMZParty.HasItem(Num(p, 1));
                case 9: return RMZParty.HasWeapon(Num(p, 1));
                case 10: return RMZParty.HasArmor(Num(p, 1));

                case 12:
                {
                    // 條件分歧裡的 JS。看得懂的（例如 Hendrix 的圖片按鈕）就真的判斷，
                    // 其餘一律視為不成立，並且只在第一次記錄。
                    string script = Str(p, 1);
                    if (RMZScriptShims.TryEvaluateCondition(script, EventId, out bool ok)) return ok;

                    LogScriptSkipped(script);
                    return false;
                }

                default:
                    // 還沒支援的條件一律當成立，避免整段劇情卡死。
                    // 但這會讓「一長串互斥分歧」全部成立（例如裝備欄四階圖片會疊在一起），
                    // 所以要記錄下來，看到就補實作。
                    LogUnsupportedCondition(Num(p, 0));
                    return true;
            }
        }

        bool Command413()
        {
            // 「以上反覆」：往回找到同一層的循環開頭
            do { _index--; } while (_index >= 0 && _list[_index].indent != _indent);
            return true;
        }

        bool Command113()
        {
            int depth = 0;
            while (_index < _list.Count - 1)
            {
                _index++;
                int code = _list[_index].code;
                if (code == 112) depth++;
                if (code == 413)
                {
                    if (depth > 0) depth--;
                    else break;
                }
            }
            return true;
        }

        bool Command117(List<object> p)
        {
            int id = Num(p, 0);
            if (id == 42 || id == 45) { RMZGuildPanels.Open(false, id == 42 ? 0 : 2); _waitMode = "guildpanel"; return true; }
            var ce = RMZDatabase.CommonEvent(id);
            if (ce?.list == null || ce.list.Count <= 1) return true;

            if (_depth >= 10)
            {
                GD.PushWarning($"[RMZ] 公共事件 {id} 呼叫層數過深，已中止。");
                return true;
            }

            _child = new RMZInterpreter(_depth + 1);
            _child.Setup(ce.list, EventId, MapId);
            return true;
        }

        bool Command119(List<object> p)
        {
            string label = Str(p, 0);
            for (int i = 0; i < _list.Count; i++)
            {
                if (_list[i].code == 118 && Str(_list[i].parameters, 0) == label)
                {
                    // 跟 MZ 的 jumpTo 一樣：跨過的縮排層，分支結果都清掉。
                    // 不清的話，從「否則」裡跳回「如果」裡面的標籤，走到「否則」時又會再跑一次（黑貓商店就這樣無限迴圈卡死）
                    int from = Mathf.Min(i, _index), to = Mathf.Max(i, _index), indent = _indent;
                    for (int k = from; k <= to && k < _list.Count; k++)
                    {
                        int ni = _list[k].indent;
                        if (ni != indent) { _branch.Remove(indent); indent = ni; }
                    }
                    _index = i;
                    return true;
                }
            }
            return true;
        }

        /// <summary>被監看的開關被改動時，把「誰改的」印出來。找「為什麼不能移動」很好用。</summary>
        void ReportWatchedSwitch(int id, bool value, string how)
        {
            int watch = RMZBootstrap.Instance != null ? RMZBootstrap.Instance.WatchSwitchId : 0;
            if (watch <= 0 || id != watch) return;

            GD.Print($"[RMZ] 開關 {id} → {(value ? "ON" : "OFF")}　來源：地圖 {MapId} 事件 {EventId} 第 {_index} 條（{how}）");
        }

        bool Command121(List<object> p)
        {
            int from = Num(p, 0), to = Num(p, 1);
            bool on = Num(p, 2) == 0;
            for (int id = from; id <= to; id++)
            {
                RMZGameState.SetSwitch(id, on);
                ReportWatchedSwitch(id, on, "開關操作指令");
            }
            RMZEventRunner.Instance?.RequestRefresh();
            return true;
        }

        bool Command122(List<object> p)
        {
            int from = Num(p, 0), to = Num(p, 1);
            int operationType = Num(p, 2);
            int operandType = Num(p, 3);

            int value = 0;
            switch (operandType)
            {
                case 0: value = Num(p, 4); break;
                case 1: value = RMZGameState.Variable(Num(p, 4)); break;
                case 2:
                {
                    int lo = Num(p, 4), hi = Num(p, 5);
                    value = GD.RandRange(Mathf.Min(lo, hi), Mathf.Max(lo, hi));
                    break;
                }
                case 3: value = GameData(p); break;
                case 4:
                    LogScriptSkipped($"變數操作腳本：{Str(p, 4)}");
                    return true;
            }

            for (int id = from; id <= to; id++)
            {
                int old = RMZGameState.Variable(id);
                int result = operationType switch
                {
                    0 => value,
                    1 => old + value,
                    2 => old - value,
                    3 => old * value,
                    4 => value == 0 ? old : old / value,
                    5 => value == 0 ? old : old % value,
                    _ => value
                };
                RMZGameState.SetVariable(id, result);
            }

            RMZEventRunner.Instance?.RequestRefresh();
            return true;
        }

        int GameData(List<object> p)
        {
            // 「遊戲資料」的來源很多（角色、敵人、地圖…），目前只做常用的幾種
            switch (Num(p, 4))
            {
                case 0: return RMZParty.NumItems(Num(p, 5));
                case 1: return RMZParty.NumWeapons(Num(p, 5));
                case 2: return RMZParty.NumArmors(Num(p, 5));
                case 3:
                {
                    // 角色：目前只存了等級。狀態欄的「等級 LV v[69]」靠這個（沒實作時永遠是 LV 0）
                    var actor = RMZActors.Actor(Num(p, 5));
                    if (actor == null) return 0;
                    // 狀態欄重畫時順便把男主的經驗（716/717）也更新，公會左上角的「經驗」才會對
                    if (Num(p, 5) == 1) RMZActors.SyncExpVars();
                    // MZ 的 5 = 經驗值
                    if (Num(p, 6) == 5) return RMZActors.Exp(actor.Id);
                    return Num(p, 6) == 0 ? actor.Level : 0;
                }
                case 7:
                    // 其他：狀態欄的「金錢 v[8]」靠這個（沒實作時永遠是 0G）
                    return Num(p, 5) switch
                    {
                        0 => RMZBootstrap.Instance?.CurrentMapId ?? 0,
                        1 => RMZActors.PartyMembers.Count,
                        2 => RMZParty.Gold,
                        4 => Mathf.FloorToInt(RMZSaveSystem.Playtime),
                        _ => 0,
                    };
                case 6:
                {
                    var player = RMZPlayer.Instance;
                    if (player == null) return 0;
                    return Num(p, 6) switch
                    {
                        0 => player.TileX,
                        1 => player.TileY,
                        2 => player.Direction,
                        _ => 0
                    };   // TileX/TileY 已經是取整後的格子座標
                }
                default: return 0;
            }
        }

        bool Command123(List<object> p)
        {
            RMZGameState.SetSelfSwitch(MapId, EventId, Str(p, 0), Num(p, 1) == 0);
            RMZEventRunner.Instance?.RequestRefresh();
            return true;
        }

        bool Command201(List<object> p)
        {
            int designation = Num(p, 0);
            int mapId = designation == 0 ? Num(p, 1) : RMZGameState.Variable(Num(p, 1));
            int x = designation == 0 ? Num(p, 2) : RMZGameState.Variable(Num(p, 2));
            int y = designation == 0 ? Num(p, 3) : RMZGameState.Variable(Num(p, 3));
            int direction = Num(p, 4);

            if(mapId==39&&MapId>=40&&MapId<=43&&!RMZDistrict.CanEnter){mapId=2;x=19;y=26;}
            if(mapId>=39&&mapId<=43&&!RMZDistrict.CanEnter){
                RMZScreen.Instance?.StartFadeIn(1f);
                var message=RMZMessage.Instance;message.Clear();message.SpeakerName="奴隸區管理處";
                foreach(var line in RMZDistrict.EntryReason.Split('\n'))message.Add(line);
                _waitMode="message";return true;
            }
            RMZBootstrap.Instance?.ReserveTransfer(mapId, x, y, direction, Num(p, 5));
            _waitMode = "transfer";
            return true;
        }

        bool Command203(List<object> p)
        {
            var target = CharacterFor(Num(p, 0));
            if (target == null) return true;

            int designation = Num(p, 1);
            float x, y;
            if (designation == 0) { x = Num(p, 2); y = Num(p, 3); }
            else if (designation == 1) { x = RMZGameState.Variable(Num(p, 2)); y = RMZGameState.Variable(Num(p, 3)); }
            else
            {
                var other = CharacterFor(Num(p, 2));
                if (other == null) return true;
                x = other.X; y = other.Y;
            }

            target.Locate(x, y);
            int d = Num(p, 4);
            if (d > 0) target.SetDirection(d);
            return true;
        }

        bool Command205(List<object> p)
        {
            var target = CharacterFor(Num(p, 0));
            if (target == null) return true;
            // MZ 會先刷新事件頁再派送強制路線，否則下一幀換頁會清掉剛開始的移動。
            if(target is RMZEventCharacter ev) ev.RefreshPage();

            var route = ParseMoveRoute(p, 1);
            if (route == null) return true;

            target.ForceMoveRoute(route);

            RMZDebug.Log(() => $"事件{EventId} 移動路線 → {target.Name}（{route.list.Count - 1} 步，" +
                               $"wait={route.wait} skippable={route.skippable}）位置 ({target.X},{target.Y})");

            if (route.wait)
            {
                _waitMode = "route";
                _waitingCharacter = target;
            }
            return true;
        }

        static MoveRoute ParseMoveRoute(List<object> p, int index)
        {
            if (p == null || index >= p.Count) return null;
            return (p[index] as JObject)?.ToObject<MoveRoute>();
        }

        bool Command213(List<object> p)
        {
            var target = CharacterFor(Num(p, 0));
            if (target == null) return true;

            target.RequestBalloon(Num(p, 1));

            if (Num(p, 2) == 1)
            {
                _waitMode = "balloon";
                _waitingCharacter = target;
            }
            return true;
        }

        bool Command214()
        {
            RMZEventRunner.Instance?.EraseEvent(EventId);
            return true;
        }

        bool Command223(List<object> p)
        {
            var tone = ToneOf(p, 0);
            float duration = Num(p, 1);
            RMZScreen.Instance?.StartTint(tone, duration);
            if (Num(p, 2) == 1) _waitCount = duration;
            return true;
        }

        bool Command224(List<object> p)
        {
            var c = ColorOf(p, 0);
            float duration = Num(p, 1);
            RMZScreen.Instance?.StartFlash(c, duration);
            if (Num(p, 2) == 1) _waitCount = duration;
            return true;
        }

        bool Command225(List<object> p)
        {
            float duration = Num(p, 2);
            RMZScreen.Instance?.StartShake(Num(p, 0), Num(p, 1), duration);
            if (Num(p, 3) == 1) _waitCount = duration;
            return true;
        }

        bool Command231(List<object> p)
        {
            int id = Num(p, 0);
            string name = Str(p, 1);
            int origin = Num(p, 2);
            bool byVariable = Num(p, 3) == 1;

            float x = byVariable ? RMZGameState.Variable(Num(p, 4)) : Num(p, 4);
            float y = byVariable ? RMZGameState.Variable(Num(p, 5)) : Num(p, 5);

            RMZScreen.Instance?.ShowPicture(id, name, origin, x, y, Num(p, 6), Num(p, 7), Num(p, 8), Num(p, 9));
            return true;
        }

        bool Command232(List<object> p)
        {
            int id = Num(p, 0);
            var pic = RMZScreen.Instance?.Picture(id);
            if (pic == null) return true;

            int origin = Num(p, 2);
            bool byVariable = Num(p, 3) == 1;
            float x = byVariable ? RMZGameState.Variable(Num(p, 4)) : Num(p, 4);
            float y = byVariable ? RMZGameState.Variable(Num(p, 5)) : Num(p, 5);
            float duration = Num(p, 10);
            int easing = p != null && p.Count > 12 ? Num(p, 12) : 0;

            pic.Move(origin, x, y, Num(p, 6), Num(p, 7), Num(p, 8), Num(p, 9), duration, easing);
            if (Num(p, 11) == 1) _waitCount = duration;
            return true;
        }

        bool Command234(List<object> p)
        {
            int id = Num(p, 0);
            var pic = RMZScreen.Instance?.Picture(id);
            float duration = Num(p, 2);
            pic?.Tint(ToneOf(p, 1), duration);
            if (Num(p, 3) == 1) _waitCount = duration;
            return true;
        }

        static readonly System.Text.StringBuilder ScriptBuilder = new System.Text.StringBuilder();

        /// <summary>更改角色圖像。params: [角色ID, 行走圖, 行走圖編號, 臉圖, 臉圖編號, 戰鬥圖]（結局換婚紗那種）</summary>
        bool Command322(List<object> p)
        {
            var actor = RMZDatabase.Actor(Num(p, 0));
            if (actor == null) return true;
            actor.characterName = Str(p, 1);
            actor.characterIndex = Num(p, 2);
            actor.faceName = Str(p, 3);
            actor.faceIndex = Num(p, 4);
            // 隊伍第一位就是玩家的外觀
            var party = RMZActors.PartyMembers;
            int leader = party != null && party.Count > 0 ? party[0] : 1;
            if (Num(p, 0) == leader && RMZPlayer.Instance != null)
            {
                RMZPlayer.Instance.CharacterName = actor.characterName;
                RMZPlayer.Instance.CharacterIndex = actor.characterIndex;
            }
            return true;
        }

        bool Command355(List<object> p)
        {
            // 跟 MZ 的 Game_Interpreter.command355 一樣：355 後面連續的 655 屬於同一段腳本，
            // 一次全部讀完再判斷。for 迴圈那種樣板逐行是看不出來的。
            ScriptBuilder.Clear();
            ScriptBuilder.Append(Str(p, 0));

            while (NextCode() == 655)
            {
                _index++;
                ScriptBuilder.Append('\n').Append(Str(Current().parameters, 0));
            }

            string block = ScriptBuilder.ToString();

            // 事件裡用 prompt() 讓玩家自己取名的那一套，改叫 Unity 的輸入視窗
            if (RMZScriptShims.TryNameInput(block, out int actorId, out string prompt))
            {
                var window = RMZInputWindow.Instance;
                if (window != null)
                {
                    window.OpenName(actorId, 0, prompt, "");
                    _waitMode = "input";
                }
                return true;
            }

            // 整段才看得出來的樣板（新遊戲重置那種）
            if (RMZScriptShims.TryRunBlock(block))
            {
                ReportWatchedSwitch(RMZBootstrap.Instance != null ? RMZBootstrap.Instance.WatchSwitchId : 0,
                                    false, "腳本清空所有開關");
                return true;
            }

            // 結局／劇情演出常見的小腳本：穿透、換位置、跳躍、縮放、隱藏、放音樂
            if (RMZCutsceneScripts.TryRun(block)) return true;

            // 都不是的話退回逐行處理，只把真的看不懂的記進 Console
            foreach (var line in block.Split('\n'))
                if (!RMZScriptShims.TryRun(line, EventId, MapId)) LogScriptSkipped(line);

            return true;
        }

        bool Command357(List<object> p)
        {
            string plugin = Str(p, 0);
            string command = Str(p, 1);
            RMZPluginCommands.Execute(plugin, command, p, this);
            return true;
        }

        // ================= 工具 =================

        public void SetWait(float frames) => _waitCount = frames;

        /// <summary>給插件指令用：讓這條事件停在原地等某件事做完。</summary>
        public void SetWaitMode(string mode) => _waitMode = mode;

        RMZCharacterBase CharacterFor(int id)
        {
            if (id < 0) return RMZPlayer.Instance;
            if (id == 0) return RMZEventRunner.Instance?.EventById(EventId);
            return RMZEventRunner.Instance?.EventById(id);
        }

        static readonly HashSet<string> _loggedScripts = new HashSet<string>();

        static void LogScriptSkipped(string script)
        {
            if (string.IsNullOrEmpty(script)) return;
            if (!_loggedScripts.Add(script)) return;   // 先用完整字串去重，避免每幀 Split 配置記憶體

            int nl = script.IndexOf('\n');
            string head = nl >= 0 ? script.Substring(0, nl) : script;
            if (head.Length > 60) head = head.Substring(0, 60);
            GD.Print($"[RMZ] JS 腳本未執行：{head}");
        }

        internal static int Num(List<object> p, int i) => RMZCharacterBase.Num(p, i);
        internal static string Str(List<object> p, int i) => RMZCharacterBase.Str(p, i);
        internal static AudioFile Audio(List<object> p, int i) => RMZCharacterBase.ToAudioFile(p, i);

        /// <summary>物品/金錢增減的數量：operationType 0=增加 1=減少，operandType 0=常數 1=變數。</summary>
        static int Operand(List<object> p, int opTypeIndex, int operandTypeIndex, int valueIndex)
        {
            int amount = Num(p, operandTypeIndex) == 0 ? Num(p, valueIndex) : RMZGameState.Variable(Num(p, valueIndex));
            return Num(p, opTypeIndex) == 0 ? amount : -amount;
        }

        static Vector4 ToneOf(List<object> p, int i)
        {
            if (p != null && i < p.Count && p[i] is JArray a && a.Count >= 4)
                return new Vector4((float)a[0], (float)a[1], (float)a[2], (float)a[3]);
            return new Vector4(0f, 0f, 0f, 0f);
        }

        static Color ColorOf(List<object> p, int i)
        {
            if (p != null && i < p.Count && p[i] is JArray a && a.Count >= 4)
                return new Color((float)a[0] / 255f, (float)a[1] / 255f, (float)a[2] / 255f, (float)a[3] / 255f);
            return new Color(0f, 0f, 0f, 0f);
        }
    }
}
