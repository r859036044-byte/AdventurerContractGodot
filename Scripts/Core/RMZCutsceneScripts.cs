// 結局／劇情演出裡常見的 JS 小腳本（原作直接寫在事件「腳本」指令裡的那些）。
// 看得懂的就照做，看不懂的回傳 false，交給原本的逐行處理記 Log。
using System.Globalization;
using System.Text.RegularExpressions;
using Godot;

namespace RMZ
{
    public static class RMZCutsceneScripts
    {
        static readonly Regex ForRange = new(@"for\s*\(\s*let\s+i\s*=\s*(\d+)\s*;\s*i\s*<=\s*(\d+)\s*;");
        static readonly Regex LoopOpacity = new(@"\$gameMap\.event\(\s*i\s*\)\.setOpacity\(\s*(\d+)\s*\)");
        static readonly Regex LoopScale = new(@"_scaleX\s*=\s*([\d.]+)");
        static readonly Regex EventPos = new(@"\$gameMap\.event\(\s*(\d+)\s*\)\.setPosition\(\s*(\d+)\s*,\s*(\d+)\s*\)");
        static readonly Regex PlayerPos = new(@"\$gamePlayer\.setPosition\(\s*(\d+)\s*,\s*(\d+)\s*\)");
        static readonly Regex JumpTo = new(@"\$gameMap\.event\(\s*(\d+)\s*\)\s*;\s*const\s+targetX\s*=\s*(\d+)\s*;\s*const\s+targetY\s*=\s*(\d+)\s*;[\s\S]*?\.jump\(");
        static readonly Regex Bgm = new(@"AudioManager\.playBgm\(");
        static readonly Regex BgmName = new(@"name\s*:\s*""([^""]*)""");
        static readonly Regex BgmVolume = new(@"volume\s*:\s*(\d+)");
        static readonly Regex BgmPitch = new(@"pitch\s*:\s*(\d+)");
        static readonly Regex ScaleVar = new(@"let\s+oldValue\s*=\s*\$gameVariables\.value\(\s*(\d+)\s*\)\s*;\s*\$gameVariables\.setValue\(\s*\1\s*,\s*Math\.round\(\s*oldValue\s*\*\s*([\d.]+)\s*\)\s*\)");

        public static bool TryRun(string block)
        {
            if (string.IsNullOrWhiteSpace(block)) return false;
            bool handled = false;
            var runner = RMZEventRunner.Instance;

            // 穿透：演出時讓大家可以互相穿過去，不會被擋住卡在原地
            if (block.Contains("setThrough("))
            {
                bool on = block.Contains("setThrough(true)");
                if (block.Contains("$gamePlayer.setThrough") && RMZPlayer.Instance != null) RMZPlayer.Instance.Through = on;
                if (block.Contains("$gameMap.events()") && runner != null)
                    foreach (var e in runner.AllEvents) if (e != null) e.Through = on;
                handled = true;
            }

            // for (let i = a; i <= b; i++) 對一排事件做同一件事
            var range = ForRange.Match(block);
            if (range.Success && runner != null)
            {
                int a = int.Parse(range.Groups[1].Value), b = int.Parse(range.Groups[2].Value);
                var op = LoopOpacity.Match(block);
                var sc = LoopScale.Match(block);
                for (int i = a; i <= b; i++)
                {
                    var ev = runner.EventById(i);
                    if (ev == null) continue;
                    if (op.Success) ev.Opacity = int.Parse(op.Groups[1].Value) / 255f;
                    if (sc.Success && ev.View != null)
                    {
                        float s = float.Parse(sc.Groups[1].Value, CultureInfo.InvariantCulture);
                        ev.View.Scale = new Vector2(s, s);
                    }
                }
                if (op.Success || sc.Success) handled = true;
            }

            foreach (Match m in EventPos.Matches(block))
            {
                runner?.EventById(int.Parse(m.Groups[1].Value))?.Locate(int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
                handled = true;
            }
            var pp = PlayerPos.Match(block);
            if (pp.Success) { RMZPlayer.Instance?.Locate(int.Parse(pp.Groups[1].Value), int.Parse(pp.Groups[2].Value)); handled = true; }

            var jm = JumpTo.Match(block);
            if (jm.Success)
            {
                var ev = runner?.EventById(int.Parse(jm.Groups[1].Value));
                if (ev != null) ev.Jump(int.Parse(jm.Groups[2].Value) - ev.X, int.Parse(jm.Groups[3].Value) - ev.Y);
                handled = true;
            }

            if (Bgm.IsMatch(block))
            {
                var n = BgmName.Match(block);
                if (n.Success)
                {
                    var v = BgmVolume.Match(block); var pt = BgmPitch.Match(block);
                    RMZAudio.Instance?.PlayBgm(new AudioFile
                    {
                        name = n.Groups[1].Value,
                        volume = v.Success ? int.Parse(v.Groups[1].Value) : 90,
                        pitch = pt.Success ? int.Parse(pt.Groups[1].Value) : 100,
                    });
                    handled = true;
                }
            }

            var sv = ScaleVar.Match(block);
            if (sv.Success)
            {
                int id = int.Parse(sv.Groups[1].Value);
                double k = double.Parse(sv.Groups[2].Value, CultureInfo.InvariantCulture);
                RMZGameState.SetVariable(id, (int)System.Math.Round(RMZGameState.Variable(id) * k));
                handled = true;
            }

            if (handled) RMZEventRunner.Instance?.RequestRefresh();
            return handled;
        }
    }
}
