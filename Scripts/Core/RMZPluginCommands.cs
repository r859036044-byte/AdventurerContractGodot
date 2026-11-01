// 插件指令的接手處。
//
// 原本的遊戲用了 Hendrix Pictures UI Creator、GALV 鏡頭、VisuStella 等插件，
// 這些都是 JS，不可能直接搬過來。做法是把「實際有用到的指令」一條一條重新實作，
// 其餘先記在 Console，之後照使用頻率補。
//
// [移植狀態] 這一版做了 Hendrix 的 UI 指令與 GALV 的鏡頭指令 —— 標題選單靠這些。
// RMZ_UnityBridge 那一組（探索小遊戲、酒館、墮落、奴隸契約、塔防…）還沒搬，
// 因為它們背後整套系統都還在 Unity 那邊；遇到時會在 Console 記一行。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public static class RMZPluginCommands
    {
        static readonly HashSet<string> Reported = new HashSet<string>();

        public static void Execute(string plugin, string command, List<object> p, RMZInterpreter interpreter)
        {
            switch (plugin)
            {
                case "VisuMZ_4_RandomizeShop":
                    // 黑貓商人的「遙控隨機商店」：建立時記下商品 JS，打開時用 RMZItemShop
                    if (command == "RemoteEventCreateNewJsShop") { RMZItemShop.RegisterGoods(ArgInt(p, "EventID:eval"), DecodeJs(ArgStr(p, "GoodsJS:func"))); return; }
                    if (command == "RemoteEventReopenLastShop") { RMZItemShop.Open(ArgInt(p, "MapID:eval"), ArgInt(p, "EventID:eval")); interpreter?.SetWaitMode("itemshop"); return; }
                    break;
                case "VisuMZ_3_OneTimePurchase":
                    return;   // 移植版的商店本來就是無限購買，這個開關不用管
                case "RMZ_UnityBridge":
                    if(command=="District"){RMZDistrict.Command(ArgStr(p,"action"),ArgInt(p,"job"));return;}
                    if(command=="RouteStep") { var target=ArgInt(p,"eventId")<0?(RMZCharacterBase)RMZPlayer.Instance:RMZEventRunner.Instance?.EventById(ArgInt(p,"eventId"));if(target!=null)target.HalfMoveDisabled=ArgStr(p,"full")=="true";return; }
                    if(command=="Guide"){RMZStoryGuide.Update();return;}
                    if(command=="FocusTutorial"){
                        var rect=new Rect2(ArgFloat(p,"x",0),ArgFloat(p,"y",0),ArgFloat(p,"w",120),ArgFloat(p,"h",80));
                        var pic=RMZScreen.Instance?.Picture(ArgInt(p,"picture"));
                        if(pic!=null){var tex=RMZImageCache.LoadPicture(pic.Name);if(tex!=null){var size=tex.GetSize()*new Vector2(pic.ScaleX/100,pic.ScaleY/100);rect=new Rect2(new Vector2(pic.X,pic.Y)-(pic.Origin==1?size/2:Vector2.Zero),size);}}
                        _ = RMZTutorialOverlay.Show(ArgStr(p,"speaker"),ArgStr(p,"text"),rect);interpreter?.SetWaitMode("tutorial");return;
                    }
                    if(command=="Enhance"){RMZGuildPanels.OpenSmith();interpreter?.SetWaitMode("guildpanel");return;}
                    if(command=="Explore"){RMZSurvivorsBridge.Begin();interpreter?.SetWaitMode("minigame");return;}
                    if(command=="EndTalk"){RMZAdventureTalkRunner.Begin();interpreter?.SetWaitMode("endtalk");return;}
                    if(command=="Report"){RMZSurvivorsBridge.ShowReport();interpreter?.SetWaitMode("report");return;}
                    // 執行中換視差背景（奴隸區晝夜切換）
                    if(command=="Parallax"){RMZParallax.Instance?.Show(ArgStr(p,"name"));return;}
                    break;
                case "RealKeyboardInput":
                    if (command == "setDisplayVariable") { RMZInputWindow.DisplayVariable = ArgInt(p, "variableId"); RMZInputWindow.DisplayLabel = ArgStr(p, "labelText"); }
                    else if (command == "clearDisplayVariable") { RMZInputWindow.DisplayVariable = 0; RMZInputWindow.DisplayLabel = ""; }
                    return;

                case "GALV_CamControlMZ":
                    HandleGalvCam(command, p);
                    return;

                case "HC_Achievements":
                    // 遊戲內選單的成就鈕；其他指令（手動解鎖、領獎）這個專案沒用到
                    if (command == "OpenAchievementBrowser") RMZAchievementBrowser.Open();
                    else if (command == "UnlockAchievement") RMZAchievements.Unlock(ArgStr(p, "id"));
                    return;

                case "HalfMove":
                    // 半格移動總開關。RM 的劇情（起床、交房租、各種演出）會先關掉半格，
                    // 讓移動路線一步走一整格，演完再打開。沒接的話那些路線只走一半，站位就跑掉。
                    if (command == "HALF_MOVE_ENABLE") HalfMoveSettings.Enabled = true;
                    else if (command == "HALF_MOVE_DISABLE") HalfMoveSettings.Enabled = false;
                    return;

                case "VisuMZ_1_MessageCore":
                    // 信件／人物介紹用的對話框大小與位置；選項框的指令（ChoiceWindow…）不影響版面，先不理
                    if (command == "MessageWindowProperties")
                        RMZMessageCore.SetProperties(ArgInt(p, "Rows:num"), ArgInt(p, "Width:num"), ArgStr(p, "WordWrap:str"));
                    else if (command == "MessageWindowXyOffsets")
                        RMZMessageCore.SetOffsets(ArgInt(p, "OffsetX:eval"), ArgInt(p, "OffsetY:eval"));
                    return;

                case "VisuMZ_1_SaveCore":
                    // 只做自動存檔。AutosaveEnable 不理它：挑戰模式現在也要自動存檔
                    // （原本挑戰模式一開始會把自動存檔關掉）。
                    if (command == "AutosaveForce" || command == "AutosaveRequest") RMZSaveSystem.Autosave();
                    return;

                case "SaveSlotLock":
                    if (command == "enterChallenge")
                    {
                        // 標題畫面「挑戰 → 讀檔」：讀挑戰模式那一格。
                        // 延到這一幀結束才讀：讀檔會把事件直譯器整個重設，不能在直譯器執行指令的途中做。
                        int slot = ArgInt(p, "slotId");
                        if (slot <= 0) slot = RMZSaveSystem.ChallengeSlot;
                        if (RMZSaveSystem.Exists(slot))
                            Callable.From(() =>
                            {
                                if (!RMZSaveSystem.LoadInto(slot)) GD.PushError($"[RMZ] 挑戰存檔（第 {slot} 格）讀不起來。");
                            }).CallDeferred();
                    }
                    return;

                case "Hendrix_Pictures_UI_Creator":
                    HandleHendrix(command, p, interpreter);
                    return;
            }

            Report($"{plugin} / {command}");
        }

        static void HandleHendrix(string command, List<object> p, RMZInterpreter interpreter)
        {
            switch (command)
            {
                case "deleteAllPictures":
                    RMZScreen.Instance?.EraseAllPictures();
                    return;

                case "ShowText":
                {
                    var layer = RMZTextWindowLayer.Instance;
                    if (layer == null) return;

                    layer.Show(
                        ArgStr(p, "id"),
                        ArgStr(p, "text"),
                        ArgStr(p, "sizePosition"),
                        ArgStr(p, "attachedToPicture"),
                        ArgStr(p, "update").ToLowerInvariant() != "false",
                        ArgStr(p, "textSettings"));
                    return;
                }

                case "EraseText":
                    RMZTextWindowLayer.Instance?.Erase(ArgStr(p, "id"));
                    return;

                case "EraseAllText":
                    RMZTextWindowLayer.Instance?.EraseAll();
                    return;

                case "callScene":
                {
                    // 專案裡實際用到的只有 Scene_Load / Scene_Save / Scene_Options（酒館等其餘畫面還沒搬）
                    string scene = ArgStr(p, "sceneName");

                    if (scene.IndexOf("Load", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        RMZSaveScene.Open(RMZSaveScene.Mode.Load);
                        interpreter?.SetWaitMode("savescene");
                        return;
                    }

                    if (scene.IndexOf("Save", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        RMZSaveScene.Open(RMZSaveScene.Mode.Save);
                        interpreter?.SetWaitMode("savescene");
                        return;
                    }

                    if (scene.IndexOf("Option", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        scene.IndexOf("Config", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        RMZOptionsScene.Open();
                        interpreter?.SetWaitMode("optionsscene");
                        return;
                    }

                    // 右上角選單的「離開」鈕 → 系統選單（回到標題畫面／退出遊戲）
                    if (scene.IndexOf("GameEnd", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        RMZSystemMenu.Instance?.ShowFromExitButton();
                        return;
                    }

                    Report($"Hendrix_Pictures_UI_Creator / callScene {scene}（該畫面尚未移植）");
                    return;
                }

                case "pauseEvents":
                    RMZEventRunner.Instance?.SetEventsPaused(true);
                    return;

                case "resumeEvents":
                    RMZEventRunner.Instance?.SetEventsPaused(false);
                    return;
            }

            Report($"Hendrix_Pictures_UI_Creator / {command}");
        }

        /// <summary>GALV 的鏡頭指令。專案用到的是聚焦與縮放。</summary>
        static void HandleGalvCam(string command, List<object> p)
        {
            var cam = RMZBootstrap.Instance?.Camera;
            if (cam == null) return;

            switch (command)
            {
                case "focusCharacter":
                {
                    // 0 或空 = 主角；其餘是事件 ID
                    int id = string.IsNullOrEmpty(ArgStr(p, "charId")) ? ArgInt(p, "character") : ArgInt(p, "charId");
                    Node2D target = id > 0
                        ? RMZEventRunner.Instance?.EventById(id)
                        : RMZPlayer.Instance;

                    cam.Target = target ?? RMZPlayer.Instance;
                    // 跟 RM（GALV 捲動會被地圖邊界卡住）和 Unity 版一樣：聚焦角色時鏡頭照樣不超出地圖，
                    // 不然角色走到地圖邊緣時會看到地圖外的黑邊（標題動畫雪霜追男主那段）。
                    cam.ClampToMap = true;
                    cam.FollowSpeed = ArgFloat(p, "speed", 0f);
                    return;
                }

                case "focusPlayer":
                    cam.Target = RMZPlayer.Instance;
                    cam.ClampToMap = true;
                    cam.FollowSpeed = ArgFloat(p, "speed", 0f);
                    return;

                case "zoom":
                    cam.SetZoom(ArgFloat(p, "zScale", ArgFloat(p, "zoomLevel", 1f)), ArgFloat(p, "zDuration", ArgFloat(p, "duration", 0f)));
                    return;
            }

            Report($"GALV_CamControlMZ / {command}");
        }

        /// <summary>MZ 的新式插件指令參數是一個 JSON 物件，值全都是字串。</summary>
        static float ArgFloat(List<object> p, string key, float fallback)
        {
            var s = ArgStr(p, key);
            return float.TryParse(s, out var v) ? v : fallback;
        }

        static int ArgInt(List<object> p, string key)
        {
            var s = ArgStr(p, key);
            return int.TryParse(s, out var v) ? v : 0;
        }

        /// <summary>插件參數裡的 JS 是「再包一層 JSON 字串」存的，解開來。</summary>
        static string DecodeJs(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            try { return Newtonsoft.Json.JsonConvert.DeserializeObject<string>(raw) ?? raw; } catch { return raw; }
        }

        static string ArgStr(List<object> p, string key)
        {
            if (p == null || p.Count < 4) return "";
            var obj = p[3] as Newtonsoft.Json.Linq.JObject;
            return obj?[key]?.ToString() ?? "";
        }

        static void Report(string what)
        {
            if (Reported.Add(what)) GD.Print($"[RMZ] 插件指令尚未實作：{what}");
        }
    }
}
