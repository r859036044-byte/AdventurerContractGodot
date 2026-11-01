// 音樂與音效。RM 的音檔是 ogg，從 rmz/audio 直讀。
//
// Godot 版比 Unity 版簡單很多：Unity 要用 UnityWebRequest + Coroutine 非同步串流，
// Godot 的 AudioStreamOggVorbis.LoadFromFile 直接同步載入就好，不用協程。
//
// 音量：Unity 是 0~1 的線性值，Godot 用分貝，所以要換算（LinearToDb）。
// 聲道平衡（AudioFile.pan）Godot 的 AudioStreamPlayer 沒有直接對應，目前忽略 ——
// 這個專案的事件幾乎都是 pan = 0。
using System.Collections.Generic;
using System.IO;
using Godot;

namespace RMZ
{
    public partial class RMZAudio : Node
    {
        public static RMZAudio Instance { get; private set; }

        const int SePoolSize = 8;
        // RM 背景音樂：設置 100% 對應原本的 40%，其他音訊維持原倍率。
        const float NativeBgmGain = 0.4f;
        static float NativeBgmScale => RMZOptions.BgmScale * NativeBgmGain;

        AudioStreamPlayer _bgm, _bgs, _me;
        readonly List<AudioStreamPlayer> _sePool = new List<AudioStreamPlayer>();
        readonly Dictionary<string, AudioStream> _cache = new Dictionary<string, AudioStream>();

        AudioFile _currentBgm;
        // 每條聲道「音檔本身」的音量（0~1）。實際輸出 = 這個 × 設置裡的音量。
        float _bgmBase = 1f, _bgsBase = 1f, _meBase = 1f;
        AudioFile _savedBgm;
        float _bgmFadeSpeed;   // 每秒衰減的音量；0 = 不淡出
        float _bgmVolume = 1f; // 淡出用的目前線性音量

        public override void _EnterTree() => Instance = this;

        public override void _Ready()
        {
            _bgm = NewPlayer("BGM");
            _bgs = NewPlayer("BGS");
            _me = NewPlayer("ME");

            for (int i = 0; i < SePoolSize; i++) _sePool.Add(NewPlayer($"SE{i}"));

            // BGM／BGS 要循環：Godot 的循環是設在 AudioStream 上，載入時再處理
            _bgm.Finished += () => { if (_bgm.Stream != null) _bgm.Play(); };
            _bgs.Finished += () => { if (_bgs.Stream != null) _bgs.Play(); };
        }

        AudioStreamPlayer NewPlayer(string name)
        {
            var player = new AudioStreamPlayer { Name = name };
            AddChild(player);
            return player;
        }

        public override void _Process(double delta)
        {
            if (_bgmFadeSpeed > 0f && _bgm.Playing)
            {
                _bgmVolume -= _bgmFadeSpeed * (float)delta;
                if (_bgmVolume <= 0f)
                {
                    _bgm.Stop();
                    _bgmFadeSpeed = 0f;
                    _currentBgm = null;
                    _bgmVolume = 0f;
                }
                else _bgm.VolumeDb = Mathf.LinearToDb(_bgmVolume);
            }
        }

        // ---------------- 對外 API ----------------

        public void PlayBgm(AudioFile file)
        {
            if (file == null || string.IsNullOrEmpty(file.name)) { StopBgm(); return; }

            // 同一首就不重播，避免換地圖時音樂從頭開始
            if (_currentBgm != null && _currentBgm.name == file.name)
            {
                _bgmBase = file.volume / 100f;
                _bgmVolume = _bgmBase * NativeBgmScale;
                SetVolume(_bgm, _bgmVolume);
                _bgm.PitchScale = Mathf.Max(0.01f, file.pitch / 100f);
                _bgmFadeSpeed = 0f;
                _bgm.StreamPaused = false;
                if (!_bgm.Playing && _bgm.Stream != null) _bgm.Play();
                return;
            }

            _currentBgm = file;
            _bgmFadeSpeed = 0f;
            PlayOn(_bgm, "bgm", file, loop: true);
        }

        public void StopBgm()
        {
            _bgm.Stop();
            _currentBgm = null;
            _bgmFadeSpeed = 0f;
        }

        public void FadeOutBgm(float seconds)
        {
            if (!_bgm.Playing) return;
            _bgmFadeSpeed = seconds > 0f ? _bgmVolume / seconds : 999f;
        }

        /// <summary>存檔要記現在放的是哪一首。</summary>
        public AudioFile CurrentBgm => _currentBgm;

        /// <summary>現在該放的曲子如果沒在放，把它接回去。</summary>
        public void EnsureBgmPlaying()
        {
            if (_currentBgm == null || string.IsNullOrEmpty(_currentBgm.name)) return;
            if (_bgm != null && _bgm.Playing) return;

            var file = _currentBgm;
            _currentBgm = null;      // 清掉才不會被 PlayBgm 的「同一首就跳過」擋下來
            PlayBgm(file);
        }

        public void SaveBgm() => _savedBgm = _currentBgm;
        public void ReplayBgm() => PlayBgm(_savedBgm);

        public void PlayBgs(AudioFile file)
        {
            if (file == null || string.IsNullOrEmpty(file.name)) { _bgs.Stop(); return; }
            PlayOn(_bgs, "bgs", file, loop: true);
        }

        public void PlayMe(AudioFile file)
        {
            if (file == null || string.IsNullOrEmpty(file.name)) return;
            PlayOn(_me, "me", file, loop: false);
        }

        public void PlaySe(AudioFile file)
        {
            if (file == null || string.IsNullOrEmpty(file.name)) return;

            var stream = Load("se", file.name);
            if (stream == null) return;

            var player = FreeSePlayer();
            player.Stream = stream;
            SetVolume(player, file.volume / 100f * RMZOptions.SeScale);
            player.PitchScale = Mathf.Max(0.01f, file.pitch / 100f);
            player.Play();
        }

        /// <summary>只給檔名就播，音量音高走預設值。UI 用得到。</summary>
        public void PlaySeByName(string name, int volume = 90)
        {
            if (string.IsNullOrEmpty(name)) return;
            PlaySe(new AudioFile { name = name, volume = volume });
        }

        /// <summary>先把音檔載好放著，免得第一次播放時卡一下。</summary>
        public void Preload(string folder, string name) => Load(folder, name);

        /// <summary>設置改了音量之後叫這個，正在播的 BGM／BGS／ME 立刻跟著變。</summary>
        public void RefreshVolumes()
        {
            // 淡出中不要動，不然會把淡出的曲線洗掉
            if (_bgmFadeSpeed <= 0f && _bgm != null) SetVolume(_bgm, _bgmBase * NativeBgmScale);
            if (_bgs != null) SetVolume(_bgs, _bgsBase * RMZOptions.BgsScale);
            if (_me != null) SetVolume(_me, _meBase * RMZOptions.MeScale);
        }

        public void StopSe()
        {
            foreach (var p in _sePool) p.Stop();
        }

        // ---------------- 內部 ----------------

        void PlayOn(AudioStreamPlayer player, string folder, AudioFile file, bool loop)
        {
            var stream = Load(folder, file.name, loop);
            if (stream == null) return;

            float baseVolume = file.volume / 100f;
            if (player == _bgm) { _bgmBase = baseVolume; _bgmVolume = baseVolume * NativeBgmScale; }
            else if (player == _bgs) _bgsBase = baseVolume;
            else if (player == _me) _meBase = baseVolume;

            player.Stream = stream;
            SetVolume(player, baseVolume * ScaleFor(player));
            player.PitchScale = Mathf.Max(0.01f, file.pitch / 100f);
            player.StreamPaused = false;
            player.Play();
        }

        float ScaleFor(AudioStreamPlayer player)
        {
            if (player == _bgm) return NativeBgmScale;
            if (player == _bgs) return RMZOptions.BgsScale;
            if (player == _me) return RMZOptions.MeScale;
            return RMZOptions.SeScale;
        }

        /// <summary>線性音量（0~1）換成 Godot 的分貝。0 直接靜音。</summary>
        static void SetVolume(AudioStreamPlayer player, float linear)
        {
            linear = Mathf.Clamp(linear, 0f, 1f);
            player.VolumeDb = linear <= 0.0001f ? -80f : Mathf.LinearToDb(linear);
        }

        AudioStreamPlayer FreeSePlayer()
        {
            foreach (var p in _sePool) if (!p.Playing) return p;
            return _sePool[0];   // 全都在響就搶最舊的那個
        }

        AudioStream Load(string folder, string name, bool loop = false)
        {
            if (string.IsNullOrEmpty(name)) return null;

            string key = folder + "/" + name;
            if (_cache.TryGetValue(key, out var cached)) return cached;

            string path = Path.Combine(RMZConfig.AudioPath, folder, name + ".ogg");
            if (!File.Exists(path))
            {
                GD.PushWarning($"[RMZ] 找不到音檔：{path}");
                _cache[key] = null;
                return null;
            }

            var stream = AudioStreamOggVorbis.LoadFromFile(path);
            if (stream != null) stream.Loop = loop;

            _cache[key] = stream;
            return stream;
        }
    }
}
