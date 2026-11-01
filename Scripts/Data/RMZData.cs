// RMZ 資料模型：對應 RPG Maker MZ 的 data/*.json
// 這些欄位名稱必須跟 MZ 的 JSON 完全一致，不要改名。
using System;
using System.Collections.Generic;

namespace RMZ
{
    [Serializable]
    public class AudioFile
    {
        public string name = "";
        public int pan = 0;
        public int pitch = 100;
        public int volume = 90;
    }

    [Serializable]
    public class MapInfo
    {
        public int id;
        public string name = "";
        public int parentId;
        public int order;
        public bool expanded;

        // RM 存的是編輯器的捲動位置，會是小數（例如 1281.6），不能宣告成 int
        public float scrollX;
        public float scrollY;
    }

    [Serializable]
    public class EventCommand
    {
        public int code;
        public int indent;
        public List<object> parameters = new List<object>();
    }

    [Serializable]
    public class MoveCommand
    {
        public int code;
        public List<object> parameters = new List<object>();
    }

    [Serializable]
    public class MoveRoute
    {
        public List<MoveCommand> list = new List<MoveCommand>();
        public bool repeat = true;
        public bool skippable = false;
        public bool wait = false;
    }

    [Serializable]
    public class EventImage
    {
        public int tileId;
        public string characterName = "";
        public int direction = 2;
        public int pattern = 1;
        public int characterIndex;
    }

    [Serializable]
    public class EventConditions
    {
        public int actorId = 1;
        public bool actorValid;
        public int itemId = 1;
        public bool itemValid;
        public string selfSwitchCh = "A";
        public bool selfSwitchValid;
        public int switch1Id = 1;
        public bool switch1Valid;
        public int switch2Id = 1;
        public bool switch2Valid;
        public int variableId = 1;
        public bool variableValid;
        public int variableValue;
    }

    [Serializable]
    public class EventPage
    {
        public EventConditions conditions = new EventConditions();
        public bool directionFix;
        public EventImage image = new EventImage();
        public List<EventCommand> list = new List<EventCommand>();
        public int moveFrequency = 3;
        public MoveRoute moveRoute = new MoveRoute();
        public int moveSpeed = 3;
        public int moveType;      // 0=固定 1=隨機 2=接近 3=自訂
        public int priorityType;  // 0=角色下方 1=與角色相同 2=角色上方
        public bool stepAnime;
        public bool through;
        public int trigger;       // 0=確定鍵 1=接觸 2=事件接觸 3=自動執行 4=並行處理
        public bool walkAnime = true;
    }

    [Serializable]
    public class MapEvent
    {
        public int id;
        public string name = "";
        public string note = "";
        public List<EventPage> pages = new List<EventPage>();
        public int x;
        public int y;
    }

    [Serializable]
    public class CommonEventData
    {
        public int id;
        public string name = "";
        public int trigger;      // 0=無 1=自動執行 2=並行處理
        public int switchId;
        public List<EventCommand> list = new List<EventCommand>();
    }

    [Serializable]
    public class MapData
    {
        public int width;
        public int height;
        public int tilesetId;
        public int[] data;
        public List<MapEvent> events = new List<MapEvent>();

        public string displayName = "";
        public string parallaxName = "";
        public bool parallaxLoopX;
        public bool parallaxLoopY;
        public bool parallaxShow = true;
        public int parallaxSx;
        public int parallaxSy;
        public int scrollType;
        public bool disableDashing;
        public bool autoplayBgm;
        public bool autoplayBgs;
        public AudioFile bgm = new AudioFile();
        public AudioFile bgs = new AudioFile();
        public string note = "";
    }

    [Serializable]
    public class TilesetData
    {
        public int id;
        public string name = "";
        public int mode;
        public string[] tilesetNames;   // 0=A1 1=A2 2=A3 3=A4 4=A5 5=B 6=C 7=D 8=E
        public int[] flags;             // 每個 tileId 的通行/屬性旗標
        public string note = "";
    }

    [Serializable]
    /// <summary>職業：這裡只用到經驗曲線（expParams：基本值、額外值、增加度A、增加度B）。</summary>
    public class ClassData
    {
        public int id;
        public string name = "";
        public int[] expParams;
    }

    public class ActorData
    {
        public int id;
        public string name = "";
        public string nickname = "";
        public string profile = "";
        public string characterName = "";
        public int characterIndex;
        public string faceName = "";
        public int faceIndex;
        public int classId;
        public int initialLevel = 1;
        public int maxLevel = 99;
        public string note = "";

        /// <summary>資料庫裡的初始裝備。索引 0 = 武器，1 之後是防具（對應 MZ 的 equipSlots）。</summary>
        public int[] equips;
    }

    public class AnimationRotation
    {
        public float x, y, z;
    }

    public class AnimationTiming
    {
        public int frame;
        public AudioFile se;
        public int duration;
        public int[] color;
    }

    /// <summary>
    /// MZ 的動畫。這個專案 121 個動畫全部是 Effekseer（.efkefc），
    /// 畫面特效要另外接 Effekseer 執行期；音效與畫面閃爍在 timings 裡，可以先做。
    /// </summary>
    public class AnimationData
    {
        public int id;
        public string name = "";
        public string effectName = "";
        public int displayType;      // 0=單體 1=全體 2=畫面
        public int offsetX, offsetY;
        public int scale = 100;
        public int speed = 100;
        public AnimationRotation rotation;
        public List<AnimationTiming> soundTimings;
        public List<AnimationTiming> flashTimings;
    }

    /// <summary>武器與防具共用的欄位。目前只取事件判定與戰力計算會用到的部分。</summary>
    public class EquipItemData
    {
        public int id;
        public string name = "";
        public int iconIndex;
        public int etypeId;          // 裝備類型（1 = 武器，2 之後是各種防具欄位）
        public int wtypeId;
        public int atypeId;
        public int itypeId;          // 道具類型（1 = 一般道具，2 = 重要道具）
        public int price;
        public string description = "";

        // params 是 C# 保留字，所以改名並用 JsonProperty 對回 JSON 的欄位名
        [Newtonsoft.Json.JsonProperty("params")]
        public int[] Params;

        public string note = "";
    }

    [Serializable]
    public class SystemData
    {
        public string gameTitle = "";
        public string currencyUnit = "";
        public string[] switches;
        public string[] variables;
        public int[] partyMembers;
        public int startMapId;
        public int startX;
        public int startY;

        /// <summary>資料庫「用語」分頁。設置畫面的項目名稱從這裡取。</summary>
        public TermsData terms;
    }

    /// <summary>System.json 的 terms。params 是 C# 關鍵字，所以改名對應。</summary>
    public class TermsData
    {
        public string[] basic;
        public string[] commands;

        [Newtonsoft.Json.JsonProperty("params")]
        public string[] Params;

        /// <summary>alwaysDash / bgmVolume 之類的字串，鍵名跟 MZ 一致。</summary>
        public Dictionary<string, string> messages;
    }
}
