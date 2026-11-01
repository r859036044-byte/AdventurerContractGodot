// 開關、變數、獨立開關的存放處，對應 MZ 的 $gameSwitches / $gameVariables / $gameSelfSwitches。
// 目前只做記憶體版本；存檔序列化留到存檔系統那一階段再接。
using System.Collections.Generic;

namespace RMZ
{
    public static class RMZGameState
    {
        static readonly Dictionary<int, bool> Switches = new Dictionary<int, bool>();
        static readonly Dictionary<int, int> Variables = new Dictionary<int, int>();
        static readonly Dictionary<string, bool> SelfSwitches = new Dictionary<string, bool>();

        // MZ 的變數其實是動態型別，事件常常拿它存文字（例如選單項目的標籤）。
        // 我們的 Variables 只存數字，所以另外開一份字串版本：有存過字串的就優先顯示字串，
        // 沒存過的照舊當數字用（Variable(id) 不變，數值腳本完全不受影響）。
        static readonly Dictionary<int, string> StringVariables = new Dictionary<int, string>();

        public static bool Switch(int id) => Switches.TryGetValue(id, out var v) && v;
        public static void SetSwitch(int id, bool v) => Switches[id] = v;

        public static int Variable(int id) => Variables.TryGetValue(id, out var v) ? v : 0;
        public static void SetVariable(int id, int v)
        {
            // 換日（變數 61 遊戲天數往上加）：前一晚沒去過酒館 → 今天冒險有休息加成。
            // 第 0 天進第 1 天是開場，不算。
            // 「去過酒館」的記號等到早上（晝夜設回 1）才清 —— 有些酒館劇情一晚會連加好幾次天數。
            if (id == VarDay)
            {
                int old = Variable(VarDay);
                if (v > old && old >= 1)
                    Variables[VarRestBonus] = Variable(VarVisitedTavern) == 0 ? 1 : 0;
            }
            else if (id == VarDayNight && v == 1) Variables[VarVisitedTavern] = 0;
            if(id==VarDayNight||(id==VarDay&&Variable(VarDayNight)<=1))RMZDistrict.Advance(id==VarDay?v:Variable(VarDay),id==VarDayNight?v:Variable(VarDayNight));
            Variables[id] = v; StringVariables.Remove(id);
        }

        public const int VarDay = 61, VarDayNight = 28, VarVisitedTavern = 677, VarRestBonus = 678;

        /// <summary>進地圖時呼叫：晚上（晝夜 ≥ 2）進酒館就記下來。</summary>
        public static void NoteMapEntered(int mapId)
        {
            if ((mapId == 5 || mapId == 34) && Variable(VarDayNight) >= 2)
                Variables[VarVisitedTavern] = 1;
        }

        /// <summary>事件腳本把變數設成字串時用這個（例如 $gameVariables.setValue(621,"{交談}")）。</summary>
        public static void SetVariableString(int id, string v) => StringVariables[id] = v;

        /// <summary>給 \V[n] 顯示用：有字串就顯示字串，否則顯示數字。</summary>
        public static string VariableDisplay(int id) =>
            StringVariables.TryGetValue(id, out var s) ? s : Variable(id).ToString();

        public static string SelfKey(int mapId, int eventId, string ch) => $"{mapId},{eventId},{ch}";
        public static bool SelfSwitch(int mapId, int eventId, string ch) =>
            SelfSwitches.TryGetValue(SelfKey(mapId, eventId, ch), out var v) && v;
        public static void SetSelfSwitch(int mapId, int eventId, string ch, bool v) =>
            SelfSwitches[SelfKey(mapId, eventId, ch)] = v;

        /// <summary>把所有開關關掉。對應事件裡「新遊戲重置」那段 for 迴圈。</summary>
        public static void ClearSwitches() => Switches.Clear();

        /// <summary>把所有變數歸零。</summary>
        public static void ClearVariables() => Variables.Clear();

        public static void Clear()
        {
            Switches.Clear();
            Variables.Clear();
            SelfSwitches.Clear();
            RMZAchievements.Reset();   // 成就跟著存檔走，新遊戲從零開始
            RMZMessageCore.Reset();
            HalfMoveSettings.Enabled = true;   // 新遊戲：半格移動預設開
        }

        // ---------------- 存讀檔 ----------------
        //
        // JsonUtility 不吃 Dictionary，所以一律攤成「鍵陣列 + 值陣列」。
        // 只存有被動過的項目，沒動過的讀回來本來就是預設值。

        public static void CaptureSwitches(List<int> ids, List<bool> values)
        {
            ids.Clear(); values.Clear();
            foreach (var kv in Switches) { ids.Add(kv.Key); values.Add(kv.Value); }
        }

        public static void CaptureVariables(List<int> ids, List<int> values)
        {
            ids.Clear(); values.Clear();
            foreach (var kv in Variables) { ids.Add(kv.Key); values.Add(kv.Value); }
        }

        public static void CaptureSelfSwitches(List<string> keys, List<bool> values)
        {
            keys.Clear(); values.Clear();
            foreach (var kv in SelfSwitches) { keys.Add(kv.Key); values.Add(kv.Value); }
        }

        public static void RestoreSwitches(List<int> ids, List<bool> values)
        {
            Switches.Clear();
            if (ids == null || values == null) return;
            for (int i = 0; i < ids.Count && i < values.Count; i++) Switches[ids[i]] = values[i];
        }

        public static void RestoreVariables(List<int> ids, List<int> values)
        {
            Variables.Clear();
            if (ids == null || values == null) return;
            for (int i = 0; i < ids.Count && i < values.Count; i++) Variables[ids[i]] = values[i];
        }

        public static void RestoreSelfSwitches(List<string> keys, List<bool> values)
        {
            SelfSwitches.Clear();
            if (keys == null || values == null) return;
            for (int i = 0; i < keys.Count && i < values.Count; i++) SelfSwitches[keys[i]] = values[i];
        }
    }
}
