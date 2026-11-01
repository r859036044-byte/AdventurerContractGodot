// 除錯開關。用來對照 RM 那邊的演出時間軸。
using Godot;

namespace RMZ
{
    public static class RMZDebug
    {
        /// <summary>印出事件的等待、移動路線、氣泡等節點與經過時間。</summary>
        public static bool LogEvents;

        static float _start;

        public static void Reset() => _start = Now;

        /// <summary>Godot 沒有 Unity 的 Time.time，用開機以來的毫秒數換算。</summary>
        static float Now => Time.GetTicksMsec() / 1000f;

        public static string Stamp => $"{Now - _start,6:0.00}s";

        public static void Log(string message)
        {
            if (LogEvents) GD.Print($"[RMZ {Stamp}] {message}");
        }

        /// <summary>
        /// 關閉時連字串都不要組。
        /// 直接寫 Log($"...") 的話，即使沒開除錯，內插字串每次都還是會配置記憶體。
        /// </summary>
        public static void Log(System.Func<string> message)
        {
            if (LogEvents) GD.Print($"[RMZ {Stamp}] {message()}");
        }
    }
}
