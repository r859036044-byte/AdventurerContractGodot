// 顯示動畫（事件指令 212）。對應 Unity 版的 RMZAnimationPlayer。
//
// 這個專案的動畫全部是 MZ 的 Effekseer 特效（.efkefc），跟 RM 用的是同一批檔案：
//   特效檔：res://RMZEffects/<effectName>.efkefc（從 RM 專案 effects/ 複製過來，含 Texture／Model）
//   播放：Effekseer for Godot 4（addons/effekseer）的 EffekseerEmitter2D
//   音效與畫面閃爍：Animations.json 的 soundTimings / flashTimings，這裡照幀數排程
//
// 外掛沒載入或找不到特效檔時，至少音效與閃爍照樣出來，演出節奏對得上。
//
// ---- 大小換算 ----
// MZ 用一個 4096px 的視口加上微透視的投影矩陣畫特效（rmmz_sprites.js 的 Sprite_Animation），
// 換算下來 1 個 Effekseer 單位 ≈ 2048 ÷ (10 × 4096 ÷ 720 + 1) ≈ 35.4 像素。
// 覺得特效整體太大或太小就調 PixelsPerUnit。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public partial class RMZAnimationPlayer : Node2D
    {
        public static RMZAnimationPlayer Instance { get; private set; }

        public const string EffectFolder = "res://RMZEffects/";
        public float PixelsPerUnit = 35.4f;
        public float DefaultDurationFrames = 60f;
        /// <summary>特效上下顛倒的話把這個打開。</summary>
        public bool FlipV = false;

        class Playing
        {
            public AnimationData Data;
            public RMZCharacterBase Target;
            public float Frame, Duration;
            public int NextSound, NextFlash;
            public Node2D Emitter;
            public Sprite2D Seq;          // 預先算好的序列圖（RMZAnimFrames）
            public SeqData SeqInfo;
            public float SeqFrame;
        }

        /// <summary>RMZAnimFrames/&lt;特效名&gt;.json：序列圖的切法。</summary>
        public class SeqData
        {
            public int frames, step = 1, cols = 1, fw, fh, ox, oy;
            public float scale = 1f;
            public List<int> empty;
            public Texture2D Sheet;
        }
        public const string SeqFolder = "res://RMZAnimFrames/";
        static readonly Dictionary<string, SeqData> SeqCache = new Dictionary<string, SeqData>();

        readonly List<Playing> _playing = new List<Playing>();
        static readonly Dictionary<string, Resource> Cache = new Dictionary<string, Resource>();
        static bool _warnedMissingPlugin;

        public bool IsPlaying => _playing.Count > 0;

        public override void _EnterTree() => Instance = this;
        public override void _ExitTree() { if (Instance == this) Instance = null; }

        public override void _Ready()
        {
            ZIndex = 4000;          // 蓋在角色上面
            ZAsRelative = false;
        }

        public void Play(int animationId, RMZCharacterBase target)
        {
            var data = RMZDatabase.Animation(animationId);
            if (data == null) { GD.PushWarning($"[RMZ] 找不到動畫 {animationId}。"); return; }

            var p = new Playing { Data = data, Target = target, Duration = DurationOf(data) };
            // 優先用預先算好的序列圖（跟 RM 畫面上看到的一樣）；沒有才試 Effekseer 外掛
            p.SeqInfo = LoadSeq(data.effectName);
            if (p.SeqInfo != null) p.Seq = SpawnSeq(p);
            else p.Emitter = SpawnEmitter(p);
            _playing.Add(p);

            RMZDebug.Log(() => $"播放動畫 {data.id}「{data.name}」（{data.effectName}）" +
                               (p.Emitter == null ? "　※沒有特效，只放音效與閃爍" : ""));
        }

        Node2D SpawnEmitter(Playing p)
        {
            var effect = LoadEffect(p.Data.effectName);
            if (effect == null) return null;

            if (!ClassDB.ClassExists("EffekseerEmitter2D"))
            {
                if (!_warnedMissingPlugin)
                {
                    _warnedMissingPlugin = true;
                    GD.PushWarning("[RMZ] Effekseer for Godot 外掛沒有載入（addons/effekseer），RM 的動畫特效不會顯示。");
                }
                return null;
            }

            var emitter = ClassDB.Instantiate("EffekseerEmitter2D").As<Node2D>();
            if (emitter == null) return null;

            emitter.Set("effect", effect);
            emitter.TextureFilter = CanvasItem.TextureFilterEnum.Linear;   // 專案預設是像素風的 Nearest，特效要平滑
            emitter.Set("speed", Mathf.Max(0.01f, p.Data.speed / 100f));
            var rot = p.Data.rotation;
            if (rot != null) emitter.Set("orientation", new Vector3(rot.x, rot.y, rot.z));
            if (FlipV) emitter.Set("flip_v", true);

            float scale = PixelsPerUnit * Mathf.Max(0.0001f, p.Data.scale / 100f);
            emitter.Scale = new Vector2(scale, scale);
            emitter.Position = AnchorPosition(p);
            AddChild(emitter);
            emitter.Call("play");
            return emitter;
        }

        static SeqData LoadSeq(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (SeqCache.TryGetValue(name, out var c)) return c;
            SeqData d = null;
            string json = SeqFolder + name + ".json", png = SeqFolder + name + ".png";
            if (FileAccess.FileExists(json) && ResourceLoader.Exists(png))
            {
                var dict = Json.ParseString(FileAccess.GetFileAsString(json)).AsGodotDictionary();
                d = new SeqData
                {
                    frames = dict["frames"].AsInt32(), step = dict["step"].AsInt32(), cols = dict["cols"].AsInt32(),
                    fw = dict["fw"].AsInt32(), fh = dict["fh"].AsInt32(), ox = dict["ox"].AsInt32(), oy = dict["oy"].AsInt32(),
                    scale = (float)dict["scale"].AsDouble(), empty = new List<int>(),
                    Sheet = ResourceLoader.Load<Texture2D>(png),
                };
                foreach (var e in dict["empty"].AsGodotArray()) d.empty.Add(e.AsInt32());
                if (d.Sheet == null) d = null;
            }
            SeqCache[name] = d;
            return d;
        }

        Sprite2D SpawnSeq(Playing p)
        {
            var d = p.SeqInfo;
            float s = 1f / Mathf.Max(0.01f, d.scale);
            var sp = new Sprite2D
            {
                Texture = d.Sheet, Centered = false, RegionEnabled = true,
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                Scale = new Vector2(s, s), Visible = false,
            };
            AddChild(sp);
            UpdateSeq(p);
            return sp;
        }

        /// <summary>換到目前的格子；回傳序列是否還沒播完。</summary>
        bool UpdateSeq(Playing p)
        {
            var d = p.SeqInfo;
            int idx = Mathf.FloorToInt(p.SeqFrame / Mathf.Max(1, d.step));
            if (idx >= d.frames) { if (p.Seq != null) p.Seq.Visible = false; return false; }
            if (p.Seq == null) return true;
            p.Seq.Visible = !d.empty.Contains(idx);
            p.Seq.RegionRect = new Rect2((idx % d.cols) * d.fw, (idx / d.cols) * d.fh, d.fw, d.fh);
            p.Seq.Position = AnchorPosition(p) + new Vector2(d.ox, d.oy);
            return true;
        }

        static Resource LoadEffect(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Cache.TryGetValue(name, out var cached)) return cached;

            string path = EffectFolder + name + ".efkefc";
            Resource res = ResourceLoader.Exists(path) ? ResourceLoader.Load(path) : null;

            // 編輯器沒有幫 .efkefc 產生匯入檔（RMZEffects 裡一個 .import 都沒有）時，
            // 直接在執行時用 EffekseerEffect.import() 讀原始檔 —— 跟外掛的匯入器做的是同一件事。
            if (res == null && FileAccess.FileExists(path) && ClassDB.ClassExists("EffekseerEffect"))
            {
                var effect = ClassDB.Instantiate("EffekseerEffect").As<Resource>();
                if (effect != null)
                {
                    effect.Call("import", path, true);
                    effect.Set("scale", 1.0f);
                    res = effect;
                }
            }
            if (res == null) GD.PushWarning($"[RMZ] 找不到特效 {path}（Godot 要先匯入過）。");
            Cache[name] = res;
            return res;
        }

        /// <summary>
        /// MZ：單體動畫對準目標行走圖的中心（腳底往上半個圖高），
        /// 畫面動畫（displayType 2）對準畫面中央。offsetX/Y 是螢幕像素。
        /// </summary>
        Vector2 AnchorPosition(Playing p)
        {
            Vector2 pos;
            if (p.Data.displayType == 2 || p.Target == null || !GodotObject.IsInstanceValid(p.Target))
            {
                var cam = GetViewport()?.GetCamera2D();
                pos = cam != null ? cam.GetScreenCenterPosition() : Vector2.Zero;
            }
            else pos = p.Target.GlobalPosition + new Vector2(0f, -RMZConfig.TileHeight * 0.5f);

            return pos + new Vector2(p.Data.offsetX, p.Data.offsetY);
        }

        float DurationOf(AnimationData d)
        {
            float last = 0f;
            if (d.soundTimings != null) foreach (var t in d.soundTimings) last = Mathf.Max(last, t.frame);
            if (d.flashTimings != null) foreach (var t in d.flashTimings) last = Mathf.Max(last, t.frame + t.duration);
            return Mathf.Max(DefaultDurationFrames, last + 10f);
        }

        public override void _Process(double delta)
        {
            if (_playing.Count == 0) return;
            float frames = (float)delta * 60f;

            for (int i = _playing.Count - 1; i >= 0; i--)
            {
                var p = _playing[i];
                p.Frame += frames * Mathf.Max(1f, p.Data.speed) / 100f;

                if (p.Data.soundTimings != null)
                    while (p.NextSound < p.Data.soundTimings.Count && p.Data.soundTimings[p.NextSound].frame <= p.Frame)
                    {
                        var t = p.Data.soundTimings[p.NextSound++];
                        if (t.se != null && !string.IsNullOrEmpty(t.se.name)) RMZAudio.Instance?.PlaySe(t.se);
                    }

                if (p.Data.flashTimings != null)
                    while (p.NextFlash < p.Data.flashTimings.Count && p.Data.flashTimings[p.NextFlash].frame <= p.Frame)
                    {
                        var t = p.Data.flashTimings[p.NextFlash++];
                        // MZ 的動畫閃爍是「目標角色」閃一下（Sprite_Animation 對目標 setBlendColor），
                        // 不是整個畫面閃白。原本做成閃整個畫面，開場鈴音召喚時連續白屏很刺眼。
                        if (t.color != null && t.color.Length >= 4)
                            FlashTarget(p, new Color(t.color[0] / 255f, t.color[1] / 255f,
                                t.color[2] / 255f, t.color[3] / 255f), Mathf.Max(1, t.duration));
                    }

                bool seqRunning = false;
                if (p.Seq != null && GodotObject.IsInstanceValid(p.Seq))
                {
                    p.SeqFrame += frames * Mathf.Max(1f, p.Data.speed) / 100f;
                    seqRunning = UpdateSeq(p);
                }

                bool emitterAlive = p.Emitter != null && GodotObject.IsInstanceValid(p.Emitter);

                // 單體動畫跟著目標走
                if (emitterAlive && p.Data.displayType != 2 && p.Target != null && GodotObject.IsInstanceValid(p.Target))
                    p.Emitter.Position = AnchorPosition(p);

                // 特效自己還在播就等它播完（有些特效比音效排程長）
                bool effectRunning = seqRunning || (emitterAlive && p.Emitter.Call("is_playing").AsBool());
                if (p.Frame >= p.Duration && !effectRunning)
                {
                    FreeNodes(p);
                    _playing.RemoveAt(i);
                }
                else if (p.Frame >= p.Duration + 600f)   // 保險：最多多等 10 秒
                {
                    FreeNodes(p);
                    _playing.RemoveAt(i);
                }
            }
        }

        static Shader _flashShader;

        /// <summary>
        /// 讓目標角色閃白再退回原色（對應 MZ 的 setBlendColor：整張圖往閃爍色混過去）。
        /// 畫面動畫（沒有目標）就不閃，也不閃整個畫面。
        /// </summary>
        void FlashTarget(Playing p, Color color, int durationFrames)
        {
            var target = p.Target;
            if (target == null || !GodotObject.IsInstanceValid(target) || p.Data.displayType == 2) return;
            var view = target.View;
            if (view == null || !GodotObject.IsInstanceValid(view)) return;

            _flashShader ??= new Shader
            {
                Code = "shader_type canvas_item;\nuniform vec4 blend = vec4(1.0, 1.0, 1.0, 0.0);\n" +
                       "void fragment() { vec4 c = texture(TEXTURE, UV) * COLOR; c.rgb = mix(c.rgb, blend.rgb, blend.a); COLOR = c; }"
            };
            if (view.Material is not ShaderMaterial mat || mat.Shader != _flashShader)
            {
                mat = new ShaderMaterial { Shader = _flashShader };
                view.Material = mat;
            }
            var from = new Color(color.R, color.G, color.B, color.A);
            var to = new Color(color.R, color.G, color.B, 0f);
            if (view.HasMeta("rmz_flash_tween"))
            {
                var old = view.GetMeta("rmz_flash_tween").As<Tween>();
                if (old != null && old.IsValid()) old.Kill();
            }
            mat.SetShaderParameter("blend", from);
            var tween = view.CreateTween();
            tween.TweenMethod(Callable.From<Color>(c => mat.SetShaderParameter("blend", c)), from, to, durationFrames / 60f);
            view.SetMeta("rmz_flash_tween", tween);
        }

        static void FreeNodes(Playing p)
        {
            if (p.Emitter != null && GodotObject.IsInstanceValid(p.Emitter)) p.Emitter.QueueFree();
            if (p.Seq != null && GodotObject.IsInstanceValid(p.Seq)) p.Seq.QueueFree();
        }

        /// <summary>換地圖時把還在播的全部清掉。</summary>
        public void Clear()
        {
            foreach (var p in _playing) FreeNodes(p);
            _playing.Clear();
        }
    }
}
