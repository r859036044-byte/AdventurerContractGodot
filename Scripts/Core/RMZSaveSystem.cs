// 存讀檔。
//
// 檔案放 user://rmz-save/fileN.json（Godot 的每人專屬可寫目錄，等同 Unity 的
// Application.persistentDataPath），一格一個檔。用 Godot.FileAccess／DirAccess
// 讀寫，不用另外把 user:// 轉成真實路徑。
//
// 槽位：
//   0        普通模式的自動存檔（手動無法覆蓋）
//   20       挑戰模式唯一的一格（自動存檔寫這格，也可以手動覆蓋）；標題畫面用 savefileExists(20) 判斷
//   其餘     普通模式的手動存檔。畫面上編號 1..ManualCount，對應檔案時跳過 20，
//            所以舊的 file1..file12 原封不動還是第 1..12 格。
using System;
using System.Collections.Generic;
using Godot;
using Newtonsoft.Json;

namespace RMZ
{
    public static class RMZSaveSystem
    {
        public const int AutoSlot = 0;
        public const int ChallengeSlot = 20;
        /// <summary>普通模式手動存檔的格數（每頁 8 格，共 3 頁）。</summary>
        public const int ManualCount = 24;

        /// <summary>畫面上的第 N 格手動存檔 → 檔案編號（跳過挑戰用的 20）。</summary>
        public static int ManualSlot(int number) => number < ChallengeSlot ? number : number + 1;

        /// <summary>挑戰模式（開關 83）。</summary>
        public static bool IsChallenge => RMZGameState.Switch(83);

        public const string Folder = "user://rmz-save";

        static string PathFor(int slot) => $"{Folder}/file{slot}.json";

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        /// <summary>這一輪玩了多久。讀檔時會接續存檔裡的時間。</summary>
        public static float Playtime { get; private set; }

        public static void TickPlaytime(float dt) => Playtime += dt;
        public static void ResetPlaytime() { Playtime = 0f; RMZBattleEvents.Reset(); }

        // ---------------- 查詢 ----------------

        public static bool Exists(int slot) => Godot.FileAccess.FileExists(PathFor(slot));

        /// <summary>存檔檔案最後修改時間（秒）。沒有這格就回傳 0。比 SavedAt（只到分鐘）精準。</summary>
        public static ulong ModifiedTime(int slot) =>
            Exists(slot) ? Godot.FileAccess.GetModifiedTime(PathFor(slot)) : 0UL;

        public static bool AnyExists()
        {
            // 普通模式的存檔（自動＋手動）；挑戰存檔是分開的，不算在這裡
            if (Exists(AutoSlot)) return true;
            for (int i = 1; i <= ManualCount; i++) if (Exists(ManualSlot(i))) return true;
            return false;
        }

        /// <summary>讀清單用的摘要。不存在的格子也會回傳一筆（Exists = false）。</summary>
        public static RMZSaveHeader Header(int slot)
        {
            var header = new RMZSaveHeader { Slot = slot, Exists = false };
            if (!Exists(slot)) return header;

            var data = Load(slot);
            if (data == null) return header;

            header.Exists = true;
            header.SavedAt = data.SavedAt;
            header.PlaytimeText = data.PlaytimeText;
            header.Name = data.HeaderName;
            header.Level = data.HeaderLevel;
            header.Day = data.HeaderDay;
            header.CharName = data.HeaderCharName ?? "";
            header.CharIndex = data.HeaderCharIndex;
            return header;
        }

        /// <summary>手動存檔某一頁（page 從 0 開始）的摘要。</summary>
        public static List<RMZSaveHeader> ManualHeaders(int page, int pageSize)
        {
            var list = new List<RMZSaveHeader>();
            for (int i = 0; i < pageSize; i++)
            {
                int number = page * pageSize + i + 1;
                if (number > ManualCount) break;
                list.Add(Header(ManualSlot(number)));
            }
            return list;
        }

        /// <summary>
        /// 自動存檔。普通模式寫第 0 格；挑戰模式寫它唯一的第 20 格。
        /// 對應 VisuMZ_1_SaveCore 的 AutosaveForce。
        /// </summary>
        public static bool Autosave() => Save(IsChallenge ? ChallengeSlot : AutoSlot);

        // ---------------- 存 ----------------

        public static bool Save(int slot)
        {
            try
            {
                EnsureFolder();
                var data = Capture();
                string json = JsonConvert.SerializeObject(data, Formatting.Indented, Settings);

                using var f = Godot.FileAccess.Open(PathFor(slot), Godot.FileAccess.ModeFlags.Write);
                if (f == null)
                {
                    GD.PushError($"[RMZ] 存檔失敗（第 {slot} 格）：{Godot.FileAccess.GetOpenError()}");
                    return false;
                }
                f.StoreString(json);
                GD.Print($"[RMZ] 已存檔到第 {slot} 格：{PathFor(slot)}");
                return true;
            }
            catch (Exception e)
            {
                GD.PushError($"[RMZ] 存檔失敗（第 {slot} 格）：{e.Message}");
                return false;
            }
        }

        public static RMZSaveData Load(int slot)
        {
            try
            {
                if (!Exists(slot)) return null;
                using var f = Godot.FileAccess.Open(PathFor(slot), Godot.FileAccess.ModeFlags.Read);
                if (f == null) return null;
                string json = f.GetAsText();
                return JsonConvert.DeserializeObject<RMZSaveData>(json, Settings);
            }
            catch (Exception e)
            {
                GD.PushError($"[RMZ] 讀檔失敗（第 {slot} 格）：{e.Message}");
                return null;
            }
        }

        public static bool Delete(int slot)
        {
            try
            {
                if (!Exists(slot)) return true;
                var dir = DirAccess.Open(Folder);
                dir?.Remove($"file{slot}.json");
                return true;
            }
            catch (Exception e)
            {
                GD.PushError($"[RMZ] 刪除存檔失敗（第 {slot} 格）：{e.Message}");
                return false;
            }
        }

        static void EnsureFolder()
        {
            if (!DirAccess.DirExistsAbsolute(Folder))
                DirAccess.MakeDirRecursiveAbsolute(Folder);
        }

        // ---------------- 快照與還原 ----------------

        public static RMZSaveData Capture()
        {
            var data = new RMZSaveData
            {
                SavedAt = DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
                Playtime = Playtime,
                HalfMoveOff = !HalfMoveSettings.Enabled,
                HudVisible = RMZScreen.Instance?.Picture(90) != null,
                Locale = RMZLocalization.Locale,
                Gold = RMZParty.Gold,
            };

            var boot = RMZBootstrap.Instance;
            var player = RMZPlayer.Instance;
            if (boot != null) data.MapId = boot.CurrentMapId;
            if (player != null)
            {
                data.PlayerX = player.X;
                data.PlayerY = player.Y;
                data.PlayerDirection = player.Direction;
            }

            var bgm = RMZAudio.Instance?.CurrentBgm;
            if (bgm != null)
            {
                data.BgmName = bgm.name;
                data.BgmVolume = bgm.volume;
                data.BgmPitch = bgm.pitch;
                data.BgmPan = bgm.pan;
            }

            RMZGameState.CaptureSwitches(data.SwitchIds, data.SwitchValues);
            RMZGameState.CaptureVariables(data.VariableIds, data.VariableValues);
            RMZGameState.CaptureSelfSwitches(data.SelfSwitchKeys, data.SelfSwitchValues);

            RMZParty.CaptureItems(data.ItemIds, data.ItemCounts);
            RMZParty.CaptureWeapons(data.WeaponIds, data.WeaponCounts);
            RMZParty.CaptureArmors(data.ArmorIds, data.ArmorCounts);

            RMZEquipment.Capture(data.Equipment, data.WornSlots, data.WornUids);
            RMZHeroineGear.Capture(data.HeroineGear);
            RMZAchievements.Capture(data.Achievements);
            data.BattleEventHistory.AddRange(RMZBattleEvents.History);

            data.PartyMembers.AddRange(RMZActors.PartyMembers);
            foreach (int id in RMZActors.PartyMembers)
            {
                var actor = RMZActors.Actor(id);
                if (actor == null) continue;

                var saved = new RMZSaveActor
                {
                    Id = actor.Id,
                    Level = actor.Level,
                    Exp = RMZActors.Exp(actor.Id),
                    ClassId = actor.ClassId,
                    Name = RMZDatabase.Actor(actor.Id)?.name ?? "",
                };
                if (actor.Equips != null) saved.Equips.AddRange(actor.Equips);
                saved.Skills.AddRange(actor.Skills);
                data.Actors.Add(saved);
            }

            // 清單上顯示的摘要
            int leader = data.PartyMembers.Count > 0 ? data.PartyMembers[0] : 1;
            data.HeaderName = RMZDatabase.Actor(leader)?.name ?? "";
            data.HeaderLevel = RMZActors.Actor(leader)?.Level ?? 1;
            data.HeaderDay = RMZGameState.Variable(61);   // 61 = 遊戲天數
            if (player != null && !string.IsNullOrEmpty(player.CharacterName))
            {
                data.HeaderCharName = player.CharacterName;
                data.HeaderCharIndex = player.CharacterIndex;
            }
            else
            {
                var db = RMZDatabase.Actor(leader);
                data.HeaderCharName = db?.characterName ?? "";
                data.HeaderCharIndex = db?.characterIndex ?? 0;
            }
            return data;
        }

        /// <summary>把存檔套回去。會排一次場所移動，呼叫端不用自己處理地圖切換。</summary>
        public static void Apply(RMZSaveData data)
        {
            if (data == null) return;

            // 先把「上一個畫面」整個收掉再套資料，不然地圖換好了也看不到
            // —— 標題的圖還蓋在最上層，或事件的殘留狀態還卡著。
            ResetPresentation();

            RMZGameState.RestoreSwitches(data.SwitchIds, data.SwitchValues);
            RMZGameState.RestoreVariables(data.VariableIds, data.VariableValues);
            RMZGameState.RestoreSelfSwitches(data.SelfSwitchKeys, data.SelfSwitchValues);

            RMZParty.SetGold(data.Gold);
            RMZParty.RestoreItems(data.ItemIds, data.ItemCounts);
            RMZParty.RestoreWeapons(data.WeaponIds, data.WeaponCounts);
            RMZParty.RestoreArmors(data.ArmorIds, data.ArmorCounts);

            RMZActors.RestoreParty(data.PartyMembers);
            foreach (var saved in data.Actors)
            {
                var actor = RMZActors.Actor(saved.Id);
                if (actor == null) continue;

                actor.Level = saved.Level;
                actor.ClassId = saved.ClassId;
                actor.Exp = saved.Exp;

                if (saved.Equips.Count > 0)
                {
                    actor.Equips = new int[Mathf.Max(5, saved.Equips.Count)];
                    for (int i = 0; i < saved.Equips.Count; i++) actor.Equips[i] = saved.Equips[i];
                }

                actor.Skills.Clear();
                foreach (int id in saved.Skills) actor.Skills.Add(id);

                // 名字是玩家自己取的，存在資料庫物件上（跟 MZ 的 setName 一樣）
                var db = RMZDatabase.Actor(saved.Id);
                if (db != null && !string.IsNullOrEmpty(saved.Name)) db.name = saved.Name;
            }

            RMZActors.SyncExpVars();   // 公會左上角的等級／經驗

            // 裝備放在角色裝備欄還原「之後」—— 它會把穿著的那幾件同步回角色身上，
            // 順序反過來的話會被上面那圈蓋掉
            RMZEquipment.Restore(data.Equipment, data.WornSlots, data.WornUids);
            RMZHeroineGear.Restore(data.HeroineGear);
            RMZAchievements.Restore(data.Achievements);
            RMZBattleEvents.Reset();
            if(data.BattleEventHistory!=null)RMZBattleEvents.History.AddRange(data.BattleEventHistory);

            Playtime = data.Playtime;
            HalfMoveSettings.Enabled = !data.HalfMoveOff;

            // 地圖 UI 是圖片，存檔沒有存圖片，讀回來會整個不見：換好地圖後重跑公共事件 89「玩家數值打開」
            // 自動存檔常在起床劇情開頭（UI 還沒開）寫下，所以不看存檔當下 UI 開沒開，一律重開；標題畫面除外
            if (data.MapId != 3)
            {
                var runner = RMZEventRunner.Instance;
                if (runner != null) runner.ReservedCommonEvent = 89;
            }
            if (!string.IsNullOrEmpty(data.Locale)) RMZLocalization.Load(data.Locale);

            var boot = RMZBootstrap.Instance;
            if (boot == null) return;

            if (data.MapId <= 0)
            {
                GD.PushError("[RMZ] 這個存檔沒有記到地圖編號，沒辦法讀。");
                return;
            }

            GD.Print($"[RMZ] 讀檔：地圖 {data.MapId}　座標 ({data.PlayerX}, {data.PlayerY})　" +
                     $"第 {data.HeaderDay} 天　金錢 {data.Gold}");

            // 座標是半格制的浮點數，四捨五入成整數格交給 ReserveTransfer，
            // 精確位置另外交給 PendingPreciseLanding，換完地圖後套回去。
            boot.ReserveTransfer(data.MapId,
                                 Mathf.RoundToInt(data.PlayerX),
                                 Mathf.RoundToInt(data.PlayerY),
                                 data.PlayerDirection);
            boot.PendingPreciseLanding = new Vector2(data.PlayerX, data.PlayerY);

            if (!string.IsNullOrEmpty(data.BgmName))
                boot.PendingBgm = new AudioFile
                {
                    name = data.BgmName,
                    volume = data.BgmVolume,
                    pitch = data.BgmPitch,
                    pan = data.BgmPan,
                };
        }

        /// <summary>讀檔前的清場：把畫面、對話框、事件直譯器、主角的殘留狀態全部歸零。</summary>
        static void ResetPresentation()
        {
            RMZScreen.Instance?.ClearForLoad();
            RMZTextWindowLayer.Instance?.EraseAll();
            RMZPictureInput.ClearInteractive();
            RMZMessage.Instance?.Clear();
            RMZEventRunner.Instance?.ResetForLoad();
            RMZPlayer.Instance?.ClearEventResidue();
        }

        public static bool LoadInto(int slot)
        {
            var data = Load(slot);
            if (data == null) return false;
            Apply(data);
            return true;
        }
    }
}
