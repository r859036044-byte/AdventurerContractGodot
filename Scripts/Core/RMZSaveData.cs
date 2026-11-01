// 存檔的資料格式。
//
// 用 Newtonsoft.Json（項目其餘資料都靠它），公開欄位直接序列化，存檔是純文字 json，
// 出問題時可以直接開起來看。欄位刻意對齊 RM 的概念（開關／變數／獨立開關／隊伍／角色／地圖座標）。
//
// [移植待辦] 奴隸區（契約／貨幣／裝備／工作外派）那些欄位還沒搬 —— 對應系統本身都還是
// Stubs.cs 裡的空殼，沒有狀態可以存，等那些系統移植過來再把欄位加回來。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public class RMZSaveData
    {
        /// <summary>存檔格式版本。之後改欄位時用來做相容處理。</summary>
        public int Version = 1;
        public List<string> BattleEventHistory = new List<string>();

        public string SavedAt = "";
        public float Playtime;
        /// <summary>半格移動總開關被劇情關掉了（HalfMove 的 HALF_MOVE_DISABLE；RM 也存在存檔裡）。</summary>
        public bool HalfMoveOff;
        /// <summary>存檔當下地圖 UI（選單鈕，圖片 90）有沒有開著。舊存檔沒有這欄（null）＝當成開著。</summary>
        public bool? HudVisible;
        public string Locale = "CN";

        // ---- 位置 ----
        public int MapId;
        public float PlayerX;
        public float PlayerY;
        public int PlayerDirection = 2;

        // ---- 開關與變數 ----
        public List<int> SwitchIds = new List<int>();
        public List<bool> SwitchValues = new List<bool>();

        public List<int> VariableIds = new List<int>();
        public List<int> VariableValues = new List<int>();

        public List<string> SelfSwitchKeys = new List<string>();
        public List<bool> SelfSwitchValues = new List<bool>();

        // ---- 隊伍 ----
        public int Gold;
        public List<int> ItemIds = new List<int>();
        public List<int> ItemCounts = new List<int>();
        public List<int> WeaponIds = new List<int>();
        public List<int> WeaponCounts = new List<int>();
        public List<int> ArmorIds = new List<int>();
        public List<int> ArmorCounts = new List<int>();

        public List<int> PartyMembers = new List<int>();

        // ---- 裝備（每一件都是獨立實例）----
        public List<RMZEquipInstance> Equipment = new List<RMZEquipInstance>();
        public List<int> WornSlots = new List<int>();
        public List<int> WornUids = new List<int>();
        /// <summary>女主身上的裝備（Owner = 哪位女主、Slot = 欄位）。舊存檔沒有這欄就用初始裝備。</summary>
        public List<RMZEquipInstance> HeroineGear = new List<RMZEquipInstance>();

        /// <summary>已解鎖的成就 id（照解鎖順序）。跟 RM 的 HC_Achievements 一樣存在每個存檔裡。</summary>
        public List<string> Achievements = new List<string>();

        // ---- 音樂 ----
        // 存的是「現在在放什麼」。讀檔時要在場所移動「之後」才蓋回去，
        // 不然地圖自己的音樂設定會把它蓋掉。
        public string BgmName = "";
        public int BgmVolume = 90;
        public int BgmPitch = 100;
        public int BgmPan;

        // ---- 角色 ----
        public List<RMZSaveActor> Actors = new List<RMZSaveActor>();

        // ---- 存檔清單上要顯示的東西 ----
        public string HeaderName = "";
        public int HeaderLevel = 1;
        public int HeaderDay;
        public string HeaderCharName = "";   // 存檔當下主角的行走圖（存檔畫面的小人）
        public int HeaderCharIndex;

        public string PlaytimeText
        {
            get
            {
                int t = Mathf.Max(0, Mathf.FloorToInt(Playtime));
                return $"{t / 3600:00}:{t % 3600 / 60:00}:{t % 60:00}";
            }
        }
    }

    public class RMZSaveActor
    {
        public int Id;
        public string Name = "";
        public int Level = 1;
        public int Exp = -1;   // 舊存檔沒有這欄 → -1，讀進來時補成該等級的起點
        public int ClassId;
        public List<int> Equips = new List<int>();
        public List<int> Skills = new List<int>();
    }

    /// <summary>存檔清單那一頁要顯示的摘要，不用把整份存檔讀進來。</summary>
    public class RMZSaveHeader
    {
        public int Slot;
        public bool Exists;
        public string SavedAt = "";
        public string PlaytimeText = "";
        public string Name = "";
        public int Level = 1;
        public int Day;
        public string CharName = "";
        public int CharIndex;
    }
}
