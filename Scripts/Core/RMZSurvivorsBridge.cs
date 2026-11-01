using System;
using System.Collections.Generic;
using RMZS;
using RMZS.GodotPort;
using Random=RMZS.GodotPort.Random;
namespace RMZ {
    public static class RMZAdventureVars
    {
        // RM 那邊 42/43/44 是「組隊／僱傭／獨自隊伍」，59 是選到的隊伍，
        // 117 是冒險地點，9 是玩家戰力。這些都是原本就有的。
        public const int Join = 42;
        public const int Hire = 43;
        public const int Solo = 44;
        public const int ChosenTeam = 59;
        public const int Place = 117;
        public const int PlayerPower = 9;        // 含隊友加成的合計戰力
        public const int PlayerOwnPower = 68;    // 玩家備份戰力：CE1 在加隊友之前先備份的
        public const int Heroine1 = 63;
        public const int Heroine2 = 64;
        public const int Heroine3 = 65;

        // 這三個是移植版新增的，用 System.json 裡沒被用到的號碼。
        public const int Result = 718;      // 0=失敗 1=成功
        public const int Kills = 719;
        public const int Survived = 720;    // 秒
        public const int LootGold = 721;    // 這趟帶回的金幣
        public const int LootPieces = 722;  // 這趟帶回的物品件數
        public const int Retreated = 723;   // 1 = 玩家自己撤退的
    }


public static class RMZSurvivorsBridge {
public static bool IsBusy {get;private set;}
        static void HookLootNames()
        {
            SVLootNames.ItemName = id => RMZLocalization.Translate(RMZDatabase.Item(id)?.name ?? $"道具{id}");
            SVLootNames.WeaponName = id => RMZLocalization.Translate(RMZDatabase.Weapon(id)?.name ?? $"武器{id}");
            SVLootNames.ArmorName = id => RMZLocalization.Translate(RMZDatabase.Armor(id)?.name ?? $"防具{id}");
        }

        /// <summary>
        /// 把「有什麼禮物可以送、送出去會怎樣」接給小遊戲。
        /// 小遊戲那邊只認這兩個委派，不會反過來引用本篇。
        /// </summary>
        /// <summary>
        /// 讓小遊戲的對話框跟本篇用同一組配色（依說話者換底色、半透明）。
        /// 小遊戲那邊只認 SVTalkStyle 這個委派，不會反過來引用本篇。
        /// </summary>
        static void HookTalkGifts()
        {
            int heroine = RMZGameState.Variable(RMZAdventureVars.Solo) == 1
                ? 0
                : Mathf.Clamp(RMZGameState.Variable(RMZAdventureVars.ChosenTeam), 0, 5);

            if (heroine <= 0) { SVTalkHooks.Clear(); return; }

            SVTalkHooks.ListGifts = () =>
            {
                var list = new List<(int id, string label)>();
                foreach (var g in RMZGifts.OwnedFor(heroine))
                    list.Add((g.ItemId, $"{g.Name}（{RMZParty.NumItems(g.ItemId)}）"));
                return list;
            };

            SVTalkHooks.GiveGift = itemId =>
            {
                foreach (var g in RMZGifts.OwnedFor(heroine))
                {
                    if (g.ItemId != itemId) continue;

                    RMZParty.GainItem(itemId, -1);

                    int amount = g.Roll();
                    bool ok = g.Guaranteed || Random.value < RMZGifts.SuccessChance;
                    if (!ok) amount = Mathf.Max(1, amount / 3);

                    if (g.IsCorruption)
                    {
                        RMZGifts.AddFall(heroine, amount);
                        return $"你把{g.Name}遞了過去。（墮落 +{amount}）";
                    }

                    RMZGifts.AddLove(heroine, amount);
                    SVTalk.Love = RMZGifts.Love(heroine);
                    return $"你把{g.Name}遞了過去。（好感 +{amount}）";
                }

                return "……那個好像不在身上了。";
            };
        }


        const int SwitchLearnedBond = 22;

        /// <summary>RM 開關 23「學會砸瓦魯多」。</summary>
        const int SwitchLearnedTimeStop = 23;

        static SVBriefing BuildBriefing()
        {
            var briefing = new SVBriefing
            {
                Mode = ReadMode(),
                MapRank = Mathf.Clamp(RMZGameState.Variable(RMZAdventureVars.Place), 1, 4),
                PlayerPower = ReadPlayerPower(),

                // 穿著的裝備直接帶進小遊戲：血量、物攻魔攻、物防魔防。
                // 拿劍還是拿法杖決定普攻算物理還是魔法，也一起帶過去。
                EquipAtk = RMZEquipment.TotalAtk,
                EquipMAtk = RMZEquipment.TotalMAtk,
                EquipDef = RMZEquipment.TotalDef,
                EquipMDef = RMZEquipment.TotalMDef,
                EquipHp = RMZEquipment.TotalMaxHp,
                UsingStaff = RMZEquipment.WieldingStaff,
            };

            if (briefing.Mode == SVPartyMode.Solo)
            {
                // 獨自組隊可以勾好幾位（CE48）：每位女主各有一個「組隊」旗標。
                // 變數 59 在獨自組隊時只會被設成 1，代表「有進隊伍畫面」，不是選了誰 ——
                // 以前讀 59 的結果是獨自組隊永遠只帶尤梨卡。
                briefing.Team = ReadSoloTeam();
                briefing.Companions = new List<SVCompanionType>();
                foreach (var who in briefing.Team)
                {
                    var members = SVParties.AloneParty(who);
                    if (members != null) briefing.Companions.AddRange(members);
                }
            }
            else briefing.Companions = BuildCompanions(briefing.Mode);

            // 打完王的對話要知道這趟跟誰出門
            briefing.Heroine = RMZGameState.Variable(RMZAdventureVars.Solo) == 1
                ? 0
                : Mathf.Clamp(RMZGameState.Variable(RMZAdventureVars.ChosenTeam), 0, 5);

            // 技能書：跟鈴音的父親學過的大覺才會進三選一（開關 22／23）
            briefing.LearnedBond = RMZGameState.Switch(SwitchLearnedBond);
            briefing.LearnedTimeStop = RMZGameState.Switch(SwitchLearnedTimeStop);

            // C 級 90 秒，往上每級多 30 秒
            briefing.SurviveSeconds = 60f + briefing.MapRank * 30f;
            briefing.Tutorial = RMZGameState.Variable(2) == 1 && briefing.Heroine == 2;
            if(briefing.Tutorial) { briefing.MapRank = 1; briefing.Companions = SVParties.PartyOf((SVHeroine)briefing.Heroine); briefing.SurviveSeconds = 0; }
            // 前一晚沒去酒館的休息加成（教學不算）
            briefing.Rested = !briefing.Tutorial && RMZGameState.Variable(RMZGameState.VarRestBonus) == 1;
            // 女主身上的裝備：以初始裝備為基準換算成威力／減傷／血量倍率
            RMZHeroineGear.ApplyTo(briefing.Companions);
            return briefing;
        }

        /// <summary>
        /// 男主自己的戰力。
        ///
        /// 變數 9「玩家戰力」在隊伍 UI 裡會被加上所選女主的戰力，
        /// 拿它換血量的話「選誰」就會影響男主有多肉，那不合理。
        /// CE1 在加之前先備份到變數 68「玩家備份戰力」，所以優先用那個。
        /// </summary>
        static int ReadPlayerPower()
        {
            int own = RMZGameState.Variable(RMZAdventureVars.PlayerOwnPower);
            if (own > 0) return own;
            return Mathf.Max(1, RMZGameState.Variable(RMZAdventureVars.PlayerPower));
        }

        static SVPartyMode ReadMode()
        {
            if (RMZGameState.Variable(RMZAdventureVars.Hire) == 1) return SVPartyMode.Hire;
            if (RMZGameState.Variable(RMZAdventureVars.Solo) == 1) return SVPartyMode.Solo;

            // 42「組隊」= 加入別人的隊伍。沒有任何旗標時也當成這個，
            // 因為「不能指揮」是比較保守的那一邊。
            return SVPartyMode.JoinOther;
        }

        /// <summary>
        /// 出場名單。變數 59「選擇的隊伍」決定是誰：
        /// 1 尤梨卡 2 露娜 3 克萊爾 4 雪瑤雪霜 5 鈴音。
        ///
        /// 加入別人的隊伍時跟著出門的是「她自己的班底」（尤梨卡帶凱、露娜是老公塔克那一小隊、
        /// 公主克萊爾有四個護衛、鈴音一個人）；僱傭或自己組隊則只有她本人跟你走。
        /// </summary>
        // RM 變數 98 露娜組隊 99 狼姐妹組隊 100 尤梨卡組隊 101 克萊爾組隊 102 鈴音組隊（1 = 有選）
        static readonly (SVHeroine who, int flag)[] SoloFlags =
        {
            (SVHeroine.Yurika, 100), (SVHeroine.Luna, 98), (SVHeroine.Claire, 101),
            (SVHeroine.WolfSisters, 99), (SVHeroine.Suzune, 102),
        };

        static List<SVHeroine> ReadSoloTeam()
        {
            var team = new List<SVHeroine>();
            foreach (var (who, flag) in SoloFlags)
                if (RMZGameState.Variable(flag) == 1) team.Add(who);
            return team;
        }

        static List<SVCompanionType> BuildCompanions(SVPartyMode mode)
        {
            int id = RMZGameState.Variable(RMZAdventureVars.ChosenTeam);
            if (!SVParties.IsValid(id))
            {
                Debug.LogWarning($"[RMZ] 變數 {RMZAdventureVars.ChosenTeam}「選擇的隊伍」是 {id}，" +
                                 "認不出是誰，這趟先讓五位女主全上。");
                return SVGame.DefaultCompanions();
            }

            var who = (SVHeroine)id;

            if (mode == SVPartyMode.JoinOther) return SVParties.PartyOf(who);

            // 雪瑤雪霜是一組兩個人，所以這裡拿的是清單而不是單一隊友
            var alone = SVParties.AloneParty(who);
            return alone != null && alone.Count > 0 ? alone : SVGame.DefaultCompanions();
        }


        static void WriteResult(SVResult result, int heroine, bool tutorial)
        {
            bool cleared = result != null && result.Cleared;
            bool retreated = result != null && result.Retreated;

            RMZGameState.SetVariable(RMZAdventureVars.Result, cleared ? 1 : 0);
            RMZGameState.SetVariable(RMZAdventureVars.Retreated, retreated ? 1 : 0);
            RMZGameState.SetVariable(RMZAdventureVars.Kills, result?.Kills ?? 0);
            RMZGameState.SetVariable(RMZAdventureVars.Survived,
                                     Mathf.FloorToInt(result?.Survived ?? 0f));

            // 清單要在入袋之前先做好 —— GrantLoot 之後袋子還在，
            // 但先做完就不用擔心之後有人把它清掉
            // 新手報酬沿用原作露娜帶新人時的樺木全套與 1000G。
            if(tutorial && heroine == 2 && result != null && result.Cleared) {
                // 露娜教學只給固定報酬：初心者 5 件套＋1000G。
                // 戰利品袋整個換新的，場上撿到的任何東西都不算（怪物本來也不會掉）。
                result.Loot = new SVLootBag();
                result.Loot.Gold += 1000;
                foreach(int id in new[] {2,6,10,14,18,181}) result.Loot.Add(SVLootKind.Item,id,1);
            }
            // 僱傭：金幣只拿一部分（其他女主 50%，尤梨卡比較親民 75%）。道具照拿
            if (!tutorial && result?.Loot != null && RMZGameState.Variable(RMZAdventureVars.Hire) == 1)
                result.Loot.Gold = Mathf.RoundToInt(result.Loot.Gold * (heroine == 1 ? 0.75f : 0.5f));
            var lines = BuildLootLines(result?.Loot);

            if (heroine > 0 && result != null && (result.Kills > 0 || result.Survived >= 30f))
            {
                int gain = Random.Range(1, 4);
                int before = RMZGifts.Love(heroine);
                RMZGifts.AddLove(heroine, gain);
                int actual = RMZGifts.Love(heroine) - before;
                if(actual > 0) lines.Add(new RMZLootLine { IconIndex = 413, Name = SVTalk.NameOf(heroine) + "・共同作戰好感度", Count = actual, IsAffection = true });
            }
            // 經驗值：照原作各地區結算腳本的範圍（成功 / 失敗或撤退）
            {
                int rank = Mathf.Clamp(RMZGameState.Variable(RMZAdventureVars.Place), 1, 4);
                int[] winMin = { 50, 150, 250, 450 }, winMax = { 200, 400, 700, 1000 };
                int[] loseMin = { 10, 25, 50, 50 }, loseMax = { 50, 75, 100, 100 };
                int exp = tutorial ? (cleared ? 100 : 10)
                        : cleared ? Random.Range(winMin[rank - 1], winMax[rank - 1] + 1)
                        : Random.Range(loseMin[rank - 1], loseMax[rank - 1] + 1);
                int gained = RMZActors.GainExp(1, exp);
                lines.Insert(0, new RMZLootLine { IconIndex = 87, Name = "經驗值", Count = exp, IsAffection = true });
                if (gained > 0)
                    lines.Insert(1, new RMZLootLine { IconIndex = 87, Name = $"升級！現在 Lv {RMZActors.Level(1)}", Count = gained, IsAffection = true });
            }
            int gold = GrantLoot(result?.Loot);
            RMZGameState.SetVariable(RMZAdventureVars.LootGold, gold);
            RMZGameState.SetVariable(RMZAdventureVars.LootPieces, result?.Loot?.TotalPieces ?? 0);

            // 結算視窗先備好但不開 —— 讓事件決定什麼時候show（演完成功／失敗的演出之後），
            // 由插件指令 RMZ_UnityBridge / Report 觸發。
            _pendingOutcome = cleared ? 1 : (retreated ? 2 : 0);
            _pendingGold = gold;
            _pendingKills = result?.Kills ?? 0;
            _pendingSeconds = Mathf.FloorToInt(result?.Survived ?? 0f);
            _pendingLines = lines;

            string how = cleared ? "完成" : (retreated ? "撤退" : "失敗");
            RMZDebug.Log(() => $"認真探索結束：{how}　擊殺 {result?.Kills ?? 0}　" +
                               $"撐了 {(result?.Survived ?? 0f):0} 秒　帶回 {gold}G");
        }

        static int _pendingOutcome, _pendingGold, _pendingKills, _pendingSeconds;
        static List<RMZLootLine> _pendingLines;

        /// <summary>
        /// 開啟結算視窗。事件用 RMZ_UnityBridge / Report 呼叫，
        /// 這樣「先演成功／失敗，再清點」的順序是在 RM 那邊排的。
        /// </summary>
        public static void ShowReport()
        {
            RMZLootReport.Open(_pendingOutcome, _pendingGold, _pendingKills,
                               _pendingSeconds, _pendingLines);
        }

        /// <summary>把戰利品攤成結算畫面要顯示的一行一行。</summary>
        static List<RMZLootLine> BuildLootLines(SVLootBag loot)
        {
            var lines = new List<RMZLootLine>();
            if (loot == null) return lines;

            void Add(int id, int count, EquipItemData data)
            {
                if (data == null || count <= 0) return;
                lines.Add(new RMZLootLine
                {
                    IconIndex = data.iconIndex,
                    Name = RMZLocalization.Translate(data.name),
                    Count = count,
                });
            }

            foreach (var pair in loot.Items) Add(pair.Key, pair.Value, RMZDatabase.Item(pair.Key));
            foreach (var pair in loot.Weapons) Add(pair.Key, pair.Value, RMZDatabase.Weapon(pair.Key));
            foreach (var pair in loot.Armors) Add(pair.Key, pair.Value, RMZDatabase.Armor(pair.Key));

            lines.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return lines;
        }

        /// <summary>把戰利品真的加進隊伍。回傳帶回的金幣。</summary>
        static int GrantLoot(SVLootBag loot)
        {
            if (loot == null) return 0;

            if (loot.Gold > 0) RMZParty.GainGold(loot.Gold);

            foreach (var pair in loot.Items) RMZParty.GainItem(pair.Key, pair.Value);
            foreach (var pair in loot.Weapons) RMZParty.GainWeapon(pair.Key, pair.Value);
            foreach (var pair in loot.Armors) RMZParty.GainArmor(pair.Key, pair.Value);

            // 金錢的 UI 讀的是變數 8，不是 $gameParty._gold，所以要同步一份
            RMZGameState.SetVariable(8, RMZParty.Gold);
            RMZEventRunner.Instance?.RequestRefresh();

            return loot.Gold;
        }


public static async void Begin(){
 if(IsBusy || RMZBootstrap.Instance==null)return;
 IsBusy=true; RMZBattleEvents.LegacyAlreadyHandled=false; HookLootNames();HookTalkGifts();
 SVTalk.PlayerName=RMZLocalization.Translate(RMZDatabase.Actor(1)?.name??"男主");
 var briefing=BuildBriefing();SVTalk.Love=briefing.Heroine>0?RMZGifts.Love(briefing.Heroine):0;
 SVMusic.Volume=RMZOptions.BgmScale;
 var story=RMZBootstrap.Instance;var mode=story.ProcessMode;var visible=story.Visible;
 var modes=new List<(Godot.Node node,Godot.Node.ProcessModeEnum mode)>();
 var layers=new List<(Godot.CanvasLayer layer,bool visible)>();
 var sounds=new List<(Godot.AudioStreamPlayer player,bool paused)>();
 // 必須先完整記錄，再停用節點；停用父節點會讓 AudioStreamPlayer 自動暫停。
 // 若停用後才讀 StreamPaused，返回時就會把這個暫停狀態永久寫回去。
 void Capture(Godot.Node n){foreach(var child in n.GetChildren()){modes.Add((child,child.ProcessMode));if(child is Godot.CanvasLayer layer)layers.Add((layer,layer.Visible));if(child is Godot.AudioStreamPlayer player)sounds.Add((player,player.StreamPaused));Capture(child);}}
 Capture(story);
 foreach(var x in sounds)x.player.StreamPaused=true;
 foreach(var x in layers)x.layer.Visible=false;
 foreach(var x in modes)x.node.ProcessMode=Godot.Node.ProcessModeEnum.Disabled;
 story.ProcessMode=Godot.Node.ProcessModeEnum.Disabled;story.Visible=false;
 SVResult finished=null;var runtime=new SVRuntime();
 bool restored=false;
 void Restore(){
 if(restored)return;restored=true;
 if(Godot.GodotObject.IsInstanceValid(story)){story.ProcessMode=mode;story.Visible=visible;foreach(var x in modes)if(Godot.GodotObject.IsInstanceValid(x.node))x.node.ProcessMode=x.mode;foreach(var x in layers)if(Godot.GodotObject.IsInstanceValid(x.layer))x.layer.Visible=x.visible;foreach(var x in sounds)if(Godot.GodotObject.IsInstanceValid(x.player))x.player.StreamPaused=x.paused;story.Camera?.MakeCurrent();}
 // 小遊戲期間本篇整棵樹被 Disabled，BGM 的 AudioStreamPlayer 有可能不是單純暫停而是整個停掉了；
 // 只還原 StreamPaused 救不回來，所以這裡再補一次「該放的曲子沒在放就接回去」。
 RMZAudio.Instance?.EnsureBgmPlaying();
 IsBusy=false;SVTalkHooks.Clear();RMZExploreLoadScreen.Hide();
 }
 briefing.OnFinished=r=>{if(finished!=null)return;finished=r;runtime.Close();};
 runtime.OnClosed=()=>{try{if(finished!=null)WriteResult(finished, briefing.Heroine, briefing.Tutorial);}finally{Restore();}};
 try {
  await RMZExploreLoadScreen.PlayIntro(briefing.Heroine);
  if(!Godot.GodotObject.IsInstanceValid(story)){Restore();runtime.Free();return;}
  SVSession.Pending=briefing;story.GetTree().Root.AddChild(runtime);
  await story.ToSignal(story.GetTree(),Godot.SceneTree.SignalName.ProcessFrame);
  await story.ToSignal(story.GetTree(),Godot.SceneTree.SignalName.ProcessFrame);
  // 戰場載好了：先暫停住，等玩家「點擊螢幕或按任意鍵」才開打
  var runMode=runtime.ProcessMode;
  runtime.ProcessMode=Godot.Node.ProcessModeEnum.Disabled;
  await RMZExploreLoadScreen.WaitForStart();
  if(Godot.GodotObject.IsInstanceValid(runtime))runtime.ProcessMode=runMode;
  RMZExploreLoadScreen.Hide();
 } catch(Exception error) {
  Godot.GD.PushError("探索載入失敗："+error);
  SVSession.Pending=null;
  if(Godot.GodotObject.IsInstanceValid(runtime)){if(runtime.IsInsideTree())runtime.QueueFree();else runtime.Free();}
  Restore();
 }
}
}
}
