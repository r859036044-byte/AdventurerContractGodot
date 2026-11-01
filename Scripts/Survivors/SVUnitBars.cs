// 頭上的血條（紅）與魔力條（藍）。
//
// 用 SpriteRenderer 直接畫在世界座標上，不走 uGUI ——
// 同畫面十幾個單位各兩條，走 Canvas 每幀都要重建網格，划不來。
using RMZS.GodotPort;

namespace RMZS
{
    public class SVUnitBars : MonoBehaviour
    {
        const float Width = 0.9f;
        const float Height = 0.09f;
        const float Gap = 0.03f;

        /// <summary>條掛在頭頂多高。</summary>
        public float Height0 = 0.95f;

        public bool ShowMana = true;

        SVUnit _unit;
        Transform _root;
        SpriteRenderer _hpFill;
        SpriteRenderer _mpFill;

        /// <summary>0~1，由外面每幀填。</summary>
        public float Mana;

        public static SVUnitBars Attach(SVUnit unit, bool showMana)
        {
            var bars = unit.gameObject.AddComponent<SVUnitBars>();
            bars._unit = unit;
            bars.ShowMana = showMana;
            bars.Build();
            return bars;
        }

        void Build()
        {
            var rootGo = new GameObject("Bars");
            rootGo.transform.SetParent(transform, false);
            rootGo.transform.localPosition = new Vector3(0f, Height0, 0f);
            _root = rootGo.transform;

            _hpFill = MakeBar("Hp", 0f, new Color(0.88f, 0.25f, 0.28f));
            if (ShowMana) _mpFill = MakeBar("Mp", -(Height + Gap), new Color(0.35f, 0.7f, 1f));
        }

        SpriteRenderer MakeBar(string name, float y, Color color)
        {
            // 底
            var bgGo = new GameObject(name + "Bg");
            bgGo.transform.SetParent(_root, false);
            bgGo.transform.localPosition = new Vector3(0f, y, 0f);
            var bg = bgGo.AddComponent<SpriteRenderer>();
            bg.sprite = SVShapes.Square();
            bg.color = new Color(0f, 0f, 0f, 0.65f);
            bg.sortingOrder = 26000;
            bgGo.transform.localScale = new Vector3(Width + 0.04f, Height + 0.04f, 1f);

            // 填色。樞紐在中間，所以要靠 localPosition 一起往左推才會「從左邊長」
            var fillGo = new GameObject(name + "Fill");
            fillGo.transform.SetParent(_root, false);
            var fill = fillGo.AddComponent<SpriteRenderer>();
            fill.sprite = SVShapes.Square();
            fill.color = color;
            fill.sortingOrder = 26001;
            fillGo.transform.localPosition = new Vector3(0f, y, 0f);
            fillGo.transform.localScale = new Vector3(Width, Height, 1f);
            return fill;
        }

        void SetFill(SpriteRenderer bar, float y, float ratio)
        {
            if (bar == null) return;

            ratio = Mathf.Clamp01(ratio);
            float w = Width * ratio;
            bar.transform.localScale = new Vector3(w, Height, 1f);
            bar.transform.localPosition = new Vector3(-(Width - w) * 0.5f, y, 0f);
        }

        void LateUpdate()
        {
            if (_unit == null || _root == null) return;

            // 極低畫質：雜魚不畫血條，王照樣顯示
            bool hide = !SVQuality.MobHpBars && _unit is SVEnemy e && (e.Type == null || !e.Type.IsBoss);
            if (_root.gameObject.activeSelf == hide) _root.gameObject.SetActive(!hide);
            if (hide) return;

            SetFill(_hpFill, 0f, _unit.MaxHp > 0 ? (float)_unit.Hp / _unit.MaxHp : 0f);
            if (_mpFill != null) SetFill(_mpFill, -(Height + Gap), Mana);

            // 血條不要跟著單位的縮放（大型敵人）一起放大，看起來會很怪
            var s = transform.lossyScale;
            _root.localScale = new Vector3(
                s.x != 0f ? 1f / s.x : 1f, s.y != 0f ? 1f / s.y : 1f, 1f);
        }
    }
}
