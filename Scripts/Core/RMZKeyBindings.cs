// 冒險（倖存者）戰鬥的按鍵設定。玩家可以在「選項 → 按鍵設定」自己改。
//
// 預設：技能 Q W E、大覺 R T Y、移動用方向鍵（WASD 不移動，W 給技能用）。
// 存在 user://keybinds.cfg，跟存檔無關，換存檔也會保留。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public static class RMZKeyBindings
    {
        public class Action
        {
            public string Id, Name;
            public Key Default;
            public Key Current;
        }

        public static readonly List<Action> Actions = new List<Action>
        {
            new Action { Id = "skill1", Name = "技能 1", Default = Key.Q },
            new Action { Id = "skill2", Name = "技能 2", Default = Key.W },
            new Action { Id = "skill3", Name = "技能 3", Default = Key.E },
            new Action { Id = "awaken1", Name = "大覺 1", Default = Key.R },
            new Action { Id = "awaken2", Name = "大覺 2", Default = Key.T },
            new Action { Id = "awaken3", Name = "大覺 3", Default = Key.Y },
            new Action { Id = "up", Name = "往上移動", Default = Key.Up },
            new Action { Id = "down", Name = "往下移動", Default = Key.Down },
            new Action { Id = "left", Name = "往左移動", Default = Key.Left },
            new Action { Id = "right", Name = "往右移動", Default = Key.Right },
        };

        const string FilePath = "user://keybinds.cfg";
        static bool _loaded;

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            foreach (var a in Actions) a.Current = a.Default;
            var cfg = new ConfigFile();
            if (cfg.Load(FilePath) != Error.Ok) return;
            foreach (var a in Actions)
            {
                long v = (long)cfg.GetValue("keys", a.Id, (long)a.Default);
                if (v != 0) a.Current = (Key)v;
            }
        }

        public static void Save()
        {
            EnsureLoaded();
            var cfg = new ConfigFile();
            foreach (var a in Actions) cfg.SetValue("keys", a.Id, (long)a.Current);
            cfg.Save(FilePath);
        }

        public static void ResetToDefault()
        {
            EnsureLoaded();
            foreach (var a in Actions) a.Current = a.Default;
            Save();
        }

        public static Action Get(string id)
        {
            EnsureLoaded();
            return Actions.Find(a => a.Id == id);
        }

        public static Key KeyOf(string id) => Get(id)?.Current ?? Key.None;

        /// <summary>
        /// 設定新按鍵。如果那個鍵已經被別的動作用了，兩個互換，不會有一個鍵綁兩個動作。
        /// </summary>
        public static void Set(string id, Key key)
        {
            EnsureLoaded();
            var target = Get(id);
            if (target == null || key == Key.None) return;
            var other = Actions.Find(a => a != target && a.Current == key);
            if (other != null) other.Current = target.Current;
            target.Current = key;
            Save();
        }

        /// <summary>按鍵顯示用的名字（Q、W、↑…）。</summary>
        public static string Label(string id) => KeyName(KeyOf(id));

        public static string KeyName(Key key) => key switch
        {
            Key.Up => "↑", Key.Down => "↓", Key.Left => "←", Key.Right => "→",
            Key.Space => "空白", Key.Shift => "Shift", Key.Ctrl => "Ctrl", Key.Alt => "Alt",
            Key.Tab => "Tab", Key.Enter => "Enter", Key.None => "-",
            _ => OS.GetKeycodeString(key),
        };

        /// <summary>這一幀剛按下（倖存者那邊用自己的輸入緩衝，跟暫停、選單同一套）。</summary>
        public static bool JustPressed(string id)
        {
            var k = KeyOf(id);
            return k != Key.None && RMZS.GodotPort.InputSystem.PortInput.Down.Contains(k);
        }

        /// <summary>現在按著。</summary>
        public static bool Held(string id)
        {
            var k = KeyOf(id);
            return k != Key.None && Input.IsPhysicalKeyPressed(k);
        }
    }
}
