// 暴擊的共用設定與演出。
//
// 演出刻意做在敵人身上而不是傷害數字上：這個遊戲畫面很亂，
// 一堆小數字飄來飄去看不出重點，所以暴擊只做兩件事 ——
// 頭上跳一個「暴擊」，以及被打的那隻自己震一下。
// 兩個都是短促、會自己消失的東西，不會留在畫面上累積。
using RMZS.GodotPort;

namespace RMZS
{
    public static class SVCrit
    {
        /// <summary>暴擊倍率的預設值。170% —— 比原本的兩倍溫和，配合除法減傷才不會一發秒殺。</summary>
        public const float DefaultMultiplier = 1.7f;

        /// <summary>「暴擊」兩個字往上飄多久。</summary>
        public const float PopupLife = 0.55f;

        /// <summary>被暴擊的那隻震動多久、晃幾度、瞬間放大多少。</summary>
        public const float ShakeTime = 0.2f;
        public const float ShakeAngle = 14f;
        public const float ShakePunch = 0.16f;

        static Font _font;

        static Font TextFont
        {
            get
            {
                if (_font == null) _font = SVFonts.Pixel;
                return _font;
            }
        }

        /// <summary>在世界座標跳一個「暴擊」，往上飄並淡出。</summary>
        public static void Popup(Vector2 at, Color color)
        {
            if (!SVQuality.CritPopups) return;   // 極低畫質不跳字
            var go = new GameObject("CritPopup");
            go.transform.position = at + new Vector2(0f, 0.6f);

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = "暴擊";
            mesh.font = TextFont;
            mesh.fontSize = 48;
            mesh.characterSize = 0.06f;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                // TextMesh 預設用字型自己的材質，這裡只要確保它畫在所有人上面
                if (TextFont != null) mr.sharedMaterial = TextFont.material;
                mr.sortingOrder = 30000;
            }

            go.AddComponent<SVCritPopup>();
        }
    }

    /// <summary>「暴擊」兩個字：往上飄、後段淡出，時間到自己收掉。</summary>
    public class SVCritPopup : MonoBehaviour
    {
        TextMesh _mesh;
        float _left = SVCrit.PopupLife;
        Color _base;

        void Awake()
        {
            _mesh = GetComponent<TextMesh>();
            if (_mesh != null) _base = _mesh.color;
        }

        void Update()
        {
            // 用 unscaled：暫停或時停的時候也不要卡在畫面上
            float dt = Time.unscaledDeltaTime;
            _left -= dt;

            if (_left <= 0f) { Destroy(gameObject); return; }

            transform.position += new Vector3(0f, 1.6f * dt, 0f);

            if (_mesh != null)
            {
                float k = Mathf.Clamp01(_left / SVCrit.PopupLife);
                _mesh.color = new Color(_base.r, _base.g, _base.b, k);
            }
        }
    }

    /// <summary>
    /// 被暴擊時的震動。
    ///
    /// 刻意不動座標 —— 敵人每一幀都會把自己的位置寫回 transform，
    /// 在這裡改位移不但會被蓋掉，還會讓下一幀的移動從被推歪的座標接著算，
    /// 整隻慢慢漂走。改成抖旋轉＋一下縮放，這兩個沒有別人在寫，安全又看得出來。
    /// </summary>
    public class SVCritShake : MonoBehaviour
    {
        float _left;
        Transform _visual;
        Quaternion _rotation;
        Vector3 _scale;
        bool _captured;

        /// <summary>對某個物件套一次震動。已經在震的就重新計時，不會疊加。</summary>
        public static void Play(GameObject target)
        {
            if (target == null) return;

            var shake = target.GetComponent<SVCritShake>();
            if (shake == null) shake = target.AddComponent<SVCritShake>();
            shake.Begin();
        }

        void Begin()
        {
            if (!_captured)
            {
                var sr = GetComponentInChildren<SpriteRenderer>();
                _visual = sr != null ? sr.transform : transform;
                _rotation = _visual.localRotation;
                _scale = _visual.localScale;
                _captured = true;
            }
            _left = SVCrit.ShakeTime;
        }

        void LateUpdate()
        {
            if (!_captured || _visual == null) { Destroy(this); return; }

            _left -= Time.deltaTime;
            if (_left <= 0f)
            {
                Restore();
                Destroy(this);
                return;
            }

            float k = _left / SVCrit.ShakeTime;
            float angle = Mathf.Sin(_left * 90f) * SVCrit.ShakeAngle * k;
            _visual.localRotation = _rotation * Quaternion.Euler(0f, 0f, angle);
            _visual.localScale = _scale * (1f + SVCrit.ShakePunch * k);
        }

        void Restore()
        {
            if (!_captured || _visual == null) return;
            _visual.localRotation = _rotation;
            _visual.localScale = _scale;
        }

        void OnDestroy() { Restore(); }
    }
}
