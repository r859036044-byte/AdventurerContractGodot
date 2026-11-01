// 設置（對應 MZ 的 ConfigManager）。
//
// 項目與 MZ 的 Scene_Options 一樣：始終跑步、記住指令、觸屏界面，
// 四條音量各自 0~100、每格 20。另外多加了移植版自己的「快速劇情速度」。
//
// MZ 存在 config.rmmzsave、Unity 版存 PlayerPrefs，
// Godot 這邊用 user://options.cfg —— 一樣是跟存檔分開、換存檔也不會被蓋掉的獨立設定。
using System;
using Godot;

namespace RMZ
{
    public static class RMZOptions
    {
        const string FilePath = "user://options.cfg";
        const string Section = "config";

        // ---------------- MZ 的項目 ----------------

        public static bool AlwaysDash;
        public static bool CommandRemember;
        public static bool TouchUI = true;

        public static int BgmVolume = 100;
        public static int BgsVolume = 100;
        public static int MeVolume = 100;
        public static int SeVolume = 100;

        // ---------------- 移植版自己加的 ----------------

        /// <summary>快速劇情的速度檔位 1~5。</summary>
        public static int FastForwardSpeed = 3;

        static readonly float[] WaitSpeeds = { 2f, 4f, 6f, 9f, 12f };
        static readonly float[] TimeScales = { 1.5f, 2f, 3f, 4f, 5f };
        static readonly string[] SpeedNames = { "很慢", "慢", "普通", "快", "很快" };

        public static string[] FastForwardSpeedNames => SpeedNames;

        /// <summary>冒險（倖存者）戰鬥的畫質：0 高、1 中、2 低、3 極低（RMZS.SVQuality）。</summary>
        public static int SurvivorQuality = 0;
        public static readonly string[] SurvivorQualityNames = { "高", "中", "低", "極低" };

        public static string FastForwardSpeedName =>
            SpeedNames[Mathf.Clamp(FastForwardSpeed, 1, 5) - 1];

        /// <summary>設定改變時通知（音量條、UI 之類的即時反應）。</summary>
        public static event Action Changed;

        static bool _loaded;

        // ---------------- 讀寫 ----------------

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            var cfg = new ConfigFile();
            if (cfg.Load(FilePath) == Error.Ok)
            {
                AlwaysDash      = (bool)cfg.GetValue(Section, "alwaysDash", false);
                CommandRemember = (bool)cfg.GetValue(Section, "commandRemember", false);
                TouchUI         = (bool)cfg.GetValue(Section, "touchUI", true);

                BgmVolume = ClampVolume((int)cfg.GetValue(Section, "bgmVolume", 100));
                BgsVolume = ClampVolume((int)cfg.GetValue(Section, "bgsVolume", 100));
                MeVolume  = ClampVolume((int)cfg.GetValue(Section, "meVolume", 100));
                SeVolume  = ClampVolume((int)cfg.GetValue(Section, "seVolume", 100));

                FastForwardSpeed = Mathf.Clamp((int)cfg.GetValue(Section, "ffSpeed", 3), 1, 5);
                SurvivorQuality = Mathf.Clamp((int)cfg.GetValue(Section, "svQuality", 0), 0, 3);
            }

            Apply();
        }

        public static void Save()
        {
            var cfg = new ConfigFile();
            cfg.SetValue(Section, "alwaysDash", AlwaysDash);
            cfg.SetValue(Section, "commandRemember", CommandRemember);
            cfg.SetValue(Section, "touchUI", TouchUI);
            cfg.SetValue(Section, "bgmVolume", BgmVolume);
            cfg.SetValue(Section, "bgsVolume", BgsVolume);
            cfg.SetValue(Section, "meVolume", MeVolume);
            cfg.SetValue(Section, "seVolume", SeVolume);
            cfg.SetValue(Section, "ffSpeed", FastForwardSpeed);
            cfg.SetValue(Section, "svQuality", SurvivorQuality);
            cfg.Save(FilePath);
        }

        /// <summary>把設定推到實際會用到它的地方，然後存檔。</summary>
        public static void Apply()
        {
            var ff = RMZFastForward.Settings;
            if (ff != null)
            {
                int i = Mathf.Clamp(FastForwardSpeed, 1, 5) - 1;
                ff.WaitSpeed = WaitSpeeds[i];
                ff.TimeScale = TimeScales[i];
            }

            var player = RMZPlayer.Instance;
            if (player != null) player.MouseMoveEnabled = TouchUI;

            RMZAudio.Instance?.RefreshVolumes();

            Changed?.Invoke();
            if (_loaded) Save();
        }

        /// <summary>把所有項目打回 MZ 的預設值。</summary>
        public static void ResetToDefault()
        {
            AlwaysDash = false;
            CommandRemember = false;
            TouchUI = true;
            BgmVolume = BgsVolume = MeVolume = SeVolume = 100;
            FastForwardSpeed = 3;
            SurvivorQuality = 0;
            Apply();
        }

        // ---------------- 音量換算 ----------------

        /// <summary>某條聲道的實際倍率 0~1。</summary>
        public static float BgmScale => BgmVolume / 100f;
        public static float BgsScale => BgsVolume / 100f;
        public static float MeScale  => MeVolume / 100f;
        public static float SeScale  => SeVolume / 100f;

        /// <summary>MZ 的音量是一格 20，方向鍵一次動一格。</summary>
        public const int VolumeStep = 20;

        public static int ClampVolume(int v) => Mathf.Clamp(v, 0, 100);
    }
}
