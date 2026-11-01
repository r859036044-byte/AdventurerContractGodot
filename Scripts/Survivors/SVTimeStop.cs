// 「砸瓦魯多」——時停大招的整段演出與規則。
//
// 分成五段，每一段做的事都不一樣：
//
//   ① 第一圈擴散到全場 → 敵人、隊友、所有飛行道具原地定格
//   ② 第二圈擴散        → 畫面開始慢慢褪色
//   ③ 第三圈快速擴出去再瞬間收回男主身上 → 畫面徹底變灰
//   ④ 時停本體（2 秒）  → 只有男主能動，攻速三倍，他打出去的劍氣一樣停在半空
//   ⑤ 回復              → 顏色慢慢回來，回完之後全場才解除定格
//
// 計時從第 ④ 段才開始算，前面的演出不吃時停的秒數 —— 不然帥歸帥，
// 兩秒有一大半花在看圈圈擴散。
//
// 去彩度是換材質做的（RMZS/DesaturateSprite），不是後製：
// URP 的 OnRenderImage 不會被呼叫，而且「除了男主以外都變灰」用換材質更精準。
using System.Collections;
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public static class SVTimeStop
    {
        public enum Phase { None, Ring1, Ring2, Ring3, Stopped, Recover }

        public static Phase Current { get; private set; } = Phase.None;

        /// <summary>整段演出進行中（含前搖與回復）。</summary>
        public static bool Active => Current != Phase.None;

        /// <summary>
        /// 世界定格：敵人、隊友、飛行道具都不動，也不生新怪。
        /// 男主「不」受影響 —— 這就是這招的重點。
        /// </summary>
        public static bool WorldFrozen { get; private set; }

        /// <summary>時停本體進行中（男主攻速加成只在這段生效）。</summary>
        public static bool Stopped => Current == Phase.Stopped;

        /// <summary>男主在時停期間的攻速倍率。3 = 快三倍（+200%）。</summary>
        public const float HasteMultiplier = 3f;

        /// <summary>時停本體幾秒。</summary>
        public const float StopSeconds = 2f;

        /// <summary>冷卻幾秒。</summary>
        public const float Cooldown = 60f;

        // ---- 演出的節奏（秒）----
        const float Ring1Time = 0.55f;
        const float Ring2Time = 0.65f;
        const float Ring3Out = 0.28f;
        const float Ring3Back = 0.22f;
        const float RecoverTime = 0.9f;

        /// <summary>圈要擴到多大才算蓋滿全場。</summary>
        static float MaxRadius => SVConfig.RectArena
            ? SVConfig.RectHalfSize.magnitude * 1.15f
            : SVConfig.ArenaRadius * 1.35f;

        // ---- 去彩度 ----

        /// <summary>0 = 原色，1 = 全灰。</summary>
        public static float Desaturation { get; private set; }

        static Material _grey;
        static readonly Dictionary<SpriteRenderer, Material> Swapped =
            new Dictionary<SpriteRenderer, Material>();

        static Material Grey
        {
            get
            {
                if (_grey != null) return _grey;

                var shader = Shader.Find("RMZS/DesaturateSprite");
                if (shader == null)
                {
                    Debug.LogWarning("[倖存者] 找不到 RMZS/DesaturateSprite 著色器，" +
                                     "時停只會定格、不會變灰。");
                    return null;
                }

                _grey = new Material(shader) { name = "SVTimeStopGrey" };
                return _grey;
            }
        }

        // ---------------- 進入點 ----------------

        public static bool Begin(SVGame game)
        {
            if (Active || game == null || game.Player == null) return false;

            var go = new GameObject("SVTimeStopRunner");
            go.AddComponent<SVTimeStopRunner>().Run(game);
            return true;
        }

        /// <summary>一局結束或重開時把狀態收乾淨，免得留著灰畫面。</summary>
        public static void Reset()
        {
            Current = Phase.None;
            WorldFrozen = false;
            Desaturation = 0f;
            RestoreMaterials();
        }

        // ---------------- 材質換與還原 ----------------

        /// <summary>
        /// 把場上除了男主以外的 SpriteRenderer 都換成去彩度材質。
        /// 男主與他底下的子物件跳過，所以只有他保持原色。
        /// </summary>
        static void ApplyMaterials(SVGame game)
        {
            var mat = Grey;
            if (mat == null) return;

            var playerRoot = game.Player != null ? game.Player.transform : null;

            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (sr == null || Swapped.ContainsKey(sr)) continue;
                if (playerRoot != null && sr.transform.IsChildOf(playerRoot)) continue;

                Swapped[sr] = sr.sharedMaterial;
                sr.sharedMaterial = mat;
            }
        }

        static void RestoreMaterials()
        {
            foreach (var pair in Swapped)
                if (pair.Key != null) pair.Key.sharedMaterial = pair.Value;

            Swapped.Clear();
            if (_grey != null) _grey.SetFloat("_Saturation", 1f);
        }

        static void SetDesaturation(float value)
        {
            Desaturation = Mathf.Clamp01(value);
            if (_grey != null) _grey.SetFloat("_Saturation", 1f - Desaturation);
        }

        // ---------------- 演出 ----------------

        /// <summary>整段流程。由 SVTimeStopRunner 跑，跑完自己把自己收掉。</summary>
        internal static IEnumerator Sequence(SVGame game)
        {
            var player = game.Player;

            // ① 第一圈：擴到全場，碰到的當下全世界定格
            Current = Phase.Ring1;
            SVTimeRing.Spawn(game, player, 0f, MaxRadius, Ring1Time,
                             new Color(0.85f, 0.90f, 1f, 0.95f), 0.30f);

            yield return Wait(game, Ring1Time * 0.55f);
            WorldFrozen = true;                     // 圈掃過大半個場地時才真的停
            yield return Wait(game, Ring1Time * 0.45f);

            // ② 第二圈：褪色開始
            Current = Phase.Ring2;
            ApplyMaterials(game);
            SVTimeRing.Spawn(game, player, 0f, MaxRadius, Ring2Time,
                             new Color(0.75f, 0.75f, 0.80f, 0.9f), 0.22f);

            float t = 0f;
            while (t < Ring2Time)
            {
                t += Time.unscaledDeltaTime;
                SetDesaturation(Mathf.SmoothStep(0f, 0.55f, t / Ring2Time));
                yield return null;
            }

            // ③ 第三圈：快速擴出去，再瞬間收回男主身上，畫面徹底變灰
            Current = Phase.Ring3;
            SVTimeRing.Spawn(game, player, 0f, MaxRadius, Ring3Out,
                             new Color(1f, 1f, 1f, 1f), 0.16f);

            t = 0f;
            while (t < Ring3Out)
            {
                t += Time.unscaledDeltaTime;
                SetDesaturation(Mathf.Lerp(0.55f, 0.75f, t / Ring3Out));
                yield return null;
            }

            // 收回來：同一圈反著跑，視覺上像整個世界被吸進他身上
            SVTimeRing.Spawn(game, player, MaxRadius, 0f, Ring3Back,
                             new Color(1f, 0.95f, 0.75f, 1f), 0.20f);

            t = 0f;
            while (t < Ring3Back)
            {
                t += Time.unscaledDeltaTime;
                SetDesaturation(Mathf.Lerp(0.75f, 1f, t / Ring3Back));
                yield return null;
            }

            SetDesaturation(1f);

            // ④ 時停本體。到這裡才開始算那 2 秒
            Current = Phase.Stopped;
            game.SpawnPulse(player.Pos, 3.2f, new Color(1f, 1f, 1f, 0.9f), 0.35f);

            yield return Wait(game, StopSeconds);

            // ⑤ 顏色慢慢回來，回完了世界才解凍
            Current = Phase.Recover;
            t = 0f;
            while (t < RecoverTime)
            {
                t += Time.unscaledDeltaTime;
                SetDesaturation(1f - Mathf.SmoothStep(0f, 1f, t / RecoverTime));
                yield return null;
            }

            SetDesaturation(0f);
            RestoreMaterials();
            WorldFrozen = false;
            Current = Phase.None;
        }

        /// <summary>
        /// 等待。用 unscaled，因為這段演出本身不該被遊戲內的緩速影響；
        /// 但玩家開系統選單或死掉時要停下來。
        /// </summary>
        static IEnumerator Wait(SVGame game, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                if (game == null || game.IsOver) yield break;
                if (!game.Paused) t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    /// <summary>把 SVTimeStop 的協程跑起來的小殼，跑完自己銷毀。</summary>
    public class SVTimeStopRunner : MonoBehaviour
    {
        public void Run(SVGame game) => StartCoroutine(Drive(game));

        IEnumerator Drive(SVGame game)
        {
            yield return SVTimeStop.Sequence(game);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            // 中途被銷毀（換場、重開一局）的話一定要把材質還回去，
            // 不然下一局整個畫面會留在灰色
            if (SVTimeStop.Active) SVTimeStop.Reset();
        }
    }

    /// <summary>時停的擴散圈。半徑從 from 走到 to，走完自己消失。</summary>
    public class SVTimeRing : MonoBehaviour
    {
        SpriteRenderer _renderer;
        Transform _follow;
        float _from, _to, _duration, _thickness, _t;
        Color _color;

        public static SVTimeRing Spawn(SVGame game, SVUnit follow, float from, float to,
                                       float duration, Color color, float thickness)
        {
            var go = new GameObject("TimeRing");
            go.transform.SetParent(game.ShotRoot, false);
            go.transform.position = follow != null ? (Vector3)follow.Pos : Vector3.zero;

            var ring = go.AddComponent<SVTimeRing>();
            ring._follow = follow != null ? follow.transform : null;
            ring._from = from;
            ring._to = to;
            ring._duration = Mathf.Max(0.01f, duration);
            ring._color = color;
            ring._thickness = thickness;

            ring._renderer = go.AddComponent<SpriteRenderer>();
            ring._renderer.sprite = SVShapes.Ring();
            ring._renderer.color = color;
            ring._renderer.sortingOrder = 25000;   // 蓋在所有場上物件之上
            return ring;
        }

        void Update()
        {
            // 用 unscaled：這段演出不該被遊戲內的任何緩速影響
            _t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_t / _duration);

            if (_follow != null) transform.position = _follow.position;

            // 擴出去的時候先快後慢，收回來的時候先慢後快，兩種手感不一樣
            float eased = _to > _from ? 1f - Mathf.Pow(1f - k, 3f) : Mathf.Pow(k, 2.2f);
            float radius = Mathf.Lerp(_from, _to, eased);
            transform.localScale = Vector3.one * Mathf.Max(0.01f, radius * 2f);

            // 圈本身在後段淡掉，不要一直卡在畫面上
            float alpha = _color.a * (1f - Mathf.Pow(k, 2f));
            _renderer.color = new Color(_color.r, _color.g, _color.b, alpha);

            if (k >= 1f) Destroy(gameObject);
        }
    }
}
