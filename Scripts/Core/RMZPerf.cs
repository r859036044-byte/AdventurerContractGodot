// 極簡的分段計時器。用來回答「到底是哪一段在吃時間」。
using System.Collections.Generic;
using System.Diagnostics;

namespace RMZ
{
    public static class RMZPerf
    {
        public static bool Enabled;

        class Section
        {
            public long ticks;
            public long lastTicks;
            public int calls;
        }

        static readonly Dictionary<string, Section> Sections = new Dictionary<string, Section>();
        static readonly Stopwatch Watch = Stopwatch.StartNew();

        static string _current;
        static long _startTicks;

        public static void Begin(string name)
        {
            if (!Enabled) return;
            _current = name;
            _startTicks = Watch.ElapsedTicks;
        }

        public static void End()
        {
            if (!Enabled || _current == null) return;

            if (!Sections.TryGetValue(_current, out var s))
            {
                s = new Section();
                Sections[_current] = s;
            }
            s.ticks += Watch.ElapsedTicks - _startTicks;
            s.calls++;
            _current = null;
        }

        /// <summary>每幀結束時呼叫：把這一幀的累計搬到「上一幀」，然後歸零。</summary>
        public static void EndFrame()
        {
            foreach (var s in Sections.Values)
            {
                s.lastTicks = s.ticks;
                s.ticks = 0;
                s.calls = 0;
            }
        }

        public static IEnumerable<KeyValuePair<string, double>> Report()
        {
            foreach (var kv in Sections)
                yield return new KeyValuePair<string, double>(
                    kv.Key, kv.Value.lastTicks * 1000.0 / Stopwatch.Frequency);
        }
    }
}
