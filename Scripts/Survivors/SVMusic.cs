// 小遊戲的背景音樂。
//
// 為什麼不共用本篇的 RMZAudio：進小遊戲時橋接層會把本篇場景的根物件
// SetActive(false)，那邊的 AudioSource 跟著被關掉，叫也叫不動。
// 這支活在倖存者場景裡，場景一卸掉音樂就跟著沒了，剛好。
//
// 曲子放 Assets/Resources/Audio/BGM/，用 Resources.Load 讀，
// 所以檔名就是曲名（不含副檔名），mp3 / ogg / wav 都可以。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public class SVMusic : MonoBehaviour
    {
        public static SVMusic Instance { get; private set; }

        /// <summary>曲子放在哪個 Resources 資料夾底下。</summary>
        public const string Folder = "Audio/BGM";

        /// <summary>音量倍率。本篇進場前會把玩家的 BGM 設定值填進來。</summary>
        public static float Volume = 0.8f;

        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        AudioSource _source;
        string _playing = "";
        float _target;
        float _fadeSpeed;

        public static SVMusic Ensure()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("SVMusic");
            Instance = go.AddComponent<SVMusic>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _source = gameObject.AddComponent<AudioSource>();
            _source.loop = true;
            _source.playOnAwake = false;
            _source.volume = 0f;
            _source.spatialBlend = 0f;      // 2D，不要跟著鏡頭位置衰減
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ---------------- 讀檔 ----------------

        public static AudioClip Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Cache.TryGetValue(name, out var cached)) return cached;

            var clip = Resources.Load<AudioClip>(Folder + "/" + name);
            if (clip == null)
                Debug.LogWarning($"[倖存者] 找不到音樂「{name}」。" +
                                 $"檔案要放在 Assets/Resources/{Folder}/，" +
                                 "而且名字不含副檔名。");

            Cache[name] = clip;
            return clip;
        }

        // ---------------- 播放 ----------------

        /// <summary>放一首。同一首再叫一次不會重頭播。</summary>
        public static void Play(string name, float fadeIn = 1.2f)
        {
            if (string.IsNullOrEmpty(name)) { Stop(); return; }

            var music = Ensure();
            if (music._playing == name && music._source.isPlaying)
            {
                music._target = Volume;
                return;
            }

            var clip = Load(name);
            if (clip == null) return;

            music._playing = name;
            music._source.clip = clip;
            music._source.volume = 0f;
            music._source.Play();
            music._target = Volume;
            music._fadeSpeed = fadeIn > 0.01f ? Volume / fadeIn : 999f;
        }

        public static void Stop(float fadeOut = 0.6f)
        {
            if (Instance == null) return;
            Instance._playing = "";
            Instance._target = 0f;
            Instance._fadeSpeed = fadeOut > 0.01f ? Instance._source.volume / fadeOut : 999f;
        }

        void Update()
        {
            if (_source == null) return;

            // 用 unscaled：暫停（Time.timeScale 之類）時音樂照樣淡
            float step = _fadeSpeed * Time.unscaledDeltaTime;
            _source.volume = Mathf.MoveTowards(_source.volume, _target, step);

            if (_target <= 0f && _source.volume <= 0.001f && _source.isPlaying) _source.Stop();
        }
    }
}
