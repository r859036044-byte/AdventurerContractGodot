// 畫面左邊的角色欄：男主與每位隊友各一顆圓形頭像，
// 滑鼠移上去（或點一下釘住）就在右邊展開那個角色目前的 BUFF。
//
// 自己一個 Canvas，不塞進 SVHud —— 那支已經夠長了，而且這裡的重建時機不一樣。
using System.Collections.Generic;
using RMZS.GodotPort;
using RMZS.GodotPort.EventSystems;
using RMZS.GodotPort.UI;

namespace RMZS
{
    public class SVCharacterPanel : MonoBehaviour
    {
        public const float AvatarSize = 56f;

        /// <summary>
        /// 頭像之間的空隙。名字掛在頭像下方（藍條再下面），
        /// 所以這個值至少要容得下「藍條 7 + 名字 16 + 呼吸空間」，
        /// 不然名字會壓到下一顆頭像。
        /// </summary>
        const float Gap = 30f;

        public const float LeftMargin = 14f;

        // 頭像欄可用的垂直範圍（以 720 高、由下往上算）。
        // 下緣讓出左下角的能力值面板，上緣避開左上角的血條／能量條。
        const float ColumnBottom = SVHud.StatsPanelMargin + SVHud.StatsPanelHeight + 10f;   // 156
        const float ColumnTop = 640f;

        // 頭像底下那一疊東西的位置，改版面時看這裡就好
        const float ManaOffset = 3f;    // 藍條離頭像下緣
        const float ManaHeight = 5f;
        const float LabelOffset = 12f;  // 名字離頭像下緣
        const float LabelHeight = 17f;

        // 隊友的技能條：貼在頭像右邊。原本這排在畫面正下方，
        // 但那裡要留給男主自己的技能與大覺，而且「誰的技能」貼著誰的頭比較好認。
        const float SkillBarGap = 8f;      // 離頭像右緣
        const float SkillBarWidth = 84f;   // 原本 148，太寬會伸進場地擋到人
        const float SkillBarHeight = 18f;  // 原本 34，改成細條，看得到冷卻進度就好

        class Entry
        {
            public RectTransform root;
            public Image portrait;
            public Image ring;        // 血量：沿著頭像外圈填
            public Image mana;        // 頭像下緣那條細的（只有男主有）
            public Text label;
            public Image skillFill;   // 頭像右邊那條技能冷卻（只有隊友有）
            public Text skillLabel;
            public SVCompanion companion;   // null = 男主
        }

        Canvas _canvas;
        RectTransform _column;
        readonly List<Entry> _entries = new List<Entry>();

        RectTransform _detail;
        Text _detailTitle;
        readonly List<Text> _detailLines = new List<Text>();

        int _hovered = -1;
        int _pinned = -1;
        int _builtFor = -1;

        void Awake()
        {
            BuildCanvas();
            BuildDetail();
        }

        // ---------------- 版面 ----------------

        void BuildCanvas()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 點陣字型要落在整數像素上才不會整體偏一點點
            _canvas.pixelPerfect = true;
            _canvas.sortingOrder = 520;      // HUD(500) 之上、指揮選單(600) 之下

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();

            _column = NewRect("Column", transform);
            _column.anchorMin = _column.anchorMax = _column.pivot = new Vector2(0f, 0.5f);
            // 整欄放在「左下角能力值面板上緣」到「頂端血條下緣」之間的正中間
            _column.anchoredPosition = new Vector2(LeftMargin, (ColumnBottom + ColumnTop) * 0.5f - 360f);
        }

        void BuildDetail()
        {
            _detail = NewRect("Detail", transform);
            _detail.anchorMin = _detail.anchorMax = new Vector2(0f, 0.5f);
            _detail.pivot = new Vector2(0f, 0.5f);
            _detail.sizeDelta = new Vector2(268f, 300f);

            var bg = NewImage("Bg", _detail, new Color(0.06f, 0.07f, 0.11f, 0.94f));
            Stretch(bg, 0f);

            var frame = NewImage("Frame", _detail, new Color(0.82f, 0.70f, 0.38f, 0.95f));
            Stretch(frame, -2f);
            frame.SetSiblingIndex(0);

            _detailTitle = NewText("Title", _detail, 20, TextAnchor.UpperLeft);
            var tr = (RectTransform)_detailTitle.transform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-24f, 26f);
            tr.anchoredPosition = new Vector2(0f, -10f);
            _detailTitle.color = new Color(1f, 0.9f, 0.6f);

            for (int i = 0; i < 14; i++)
            {
                var line = NewText("Line" + i, _detail, 16, TextAnchor.UpperLeft);
                var lr = (RectTransform)line.transform;
                lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f);
                lr.pivot = new Vector2(0.5f, 1f);
                lr.sizeDelta = new Vector2(-24f, 20f);
                lr.anchoredPosition = new Vector2(0f, -40f - i * 19f);
                line.horizontalOverflow = HorizontalWrapMode.Overflow;
                _detailLines.Add(line);
            }

            _detail.gameObject.SetActive(false);
        }

        void Rebuild(SVGame game)
        {
            foreach (Transform child in new List<Transform>(_column)) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            _entries.Clear();

            var roster = new List<SVCompanion> { null };   // null 代表男主，排第一個
            foreach (var c in game.AllCompanions) if (c != null) roster.Add(c);

            float step = AvatarSize + Gap;
            _column.sizeDelta = new Vector2(AvatarSize, step * roster.Count);

            // 人多的時候整欄等比縮小，寧可小一點也不要擠在一起或超出畫面。
            // 縮的是整個 Column，所以頭像、藍條、名字的相對位置完全不會跑掉。
            const float MaxColumnHeight = ColumnTop - ColumnBottom;
            float needed = step * roster.Count;
            _column.localScale = Vector3.one *
                (needed > MaxColumnHeight ? MaxColumnHeight / needed : 1f);

            for (int i = 0; i < roster.Count; i++)
            {
                int index = i;
                var companion = roster[i];

                string name = companion != null ? companion.Type.Name : "男主";
                string sheet = companion != null ? companion.Type.SheetName : "男主1";
                int sheetIndex = companion != null ? companion.Type.SheetIndex : 3;
                var ringColor = SVPortrait.RingColorFor(name);

                var slot = NewRect("Avatar" + i, _column);
                slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0f, 0.5f);
                slot.sizeDelta = new Vector2(AvatarSize, AvatarSize);
                // 由上往下排，整欄垂直置中
                slot.anchoredPosition = new Vector2(
                    0f, (roster.Count - 1) * step * 0.5f - i * step);

                // 血量：頭像後面一圈徑向填色，滿血一整圈
                var ring = NewImage("Hp", slot, new Color(ringColor.r, ringColor.g, ringColor.b, 0.9f));
                Stretch(ring, -4f);
                var ringImg = ring.GetComponent<Image>();
                ringImg.sprite = SVShapes.Circle();
                ringImg.type = Image.Type.Filled;
                ringImg.fillMethod = Image.FillMethod.Radial360;
                ringImg.fillOrigin = 2;      // 從上方開始
                ringImg.raycastTarget = false;

                var portrait = NewImage("Portrait", slot, Color.white);
                Stretch(portrait, 0f);
                var portraitImg = portrait.GetComponent<Image>();
                portraitImg.sprite = SVPortrait.Get(sheet, sheetIndex, ringColor);
                portraitImg.preserveAspect = true;
                portraitImg.raycastTarget = true;      // 滑鼠要壓得到

                // 男主：頭像底下一條細的，進度是「最快可以放的技能」。
                // 隊友沒有這條 —— 他們的冷卻改用右邊那條比較好讀的。
                Image manaImg = null;
                if (companion == null)
                {
                    var manaBg = NewImage("MpBg", slot, new Color(0f, 0f, 0f, 0.6f));
                    manaBg.anchorMin = new Vector2(0f, 0f); manaBg.anchorMax = new Vector2(1f, 0f);
                    manaBg.pivot = new Vector2(0.5f, 0f);
                    manaBg.sizeDelta = new Vector2(-8f, ManaHeight);
                    manaBg.anchoredPosition = new Vector2(0f, -ManaOffset);
                    manaBg.GetComponent<Image>().raycastTarget = false;

                    var mana = NewImage("Mp", manaBg, new Color(0.35f, 0.7f, 1f));
                    Stretch(mana, 0f);
                    manaImg = mana.GetComponent<Image>();
                    manaImg.type = Image.Type.Filled;
                    manaImg.fillMethod = Image.FillMethod.Horizontal;
                    manaImg.raycastTarget = false;
                }

                // 隊友：頭像右邊一條技能冷卻，滿了就會自動放技能
                Image skillFillImg = null;
                Text skillLabelText = null;
                if (companion != null)
                {
                    // 淡一點、細一點——這條只是給你瞄一眼冷卻到哪，不該跟場上的人搶視線
                    var bar = NewImage("Skill", slot, new Color(0f, 0f, 0f, 0.3f));
                    bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0f, 0.5f);
                    bar.sizeDelta = new Vector2(SkillBarWidth, SkillBarHeight);
                    bar.anchoredPosition = new Vector2(AvatarSize + SkillBarGap, 0f);
                    bar.GetComponent<Image>().raycastTarget = false;

                    var barFrame = NewImage("Frame", bar,
                                            new Color(ringColor.r, ringColor.g, ringColor.b, 0.5f));
                    Stretch(barFrame, -2f);
                    barFrame.SetSiblingIndex(0);
                    barFrame.GetComponent<Image>().raycastTarget = false;

                    var barFill = NewImage("Fill", bar, new Color(0.35f, 0.7f, 1f, 0.32f));
                    Stretch(barFill, -2f);
                    skillFillImg = barFill.GetComponent<Image>();
                    skillFillImg.type = Image.Type.Filled;
                    skillFillImg.fillMethod = Image.FillMethod.Horizontal;
                    skillFillImg.raycastTarget = false;

                    skillLabelText = NewText("Label", bar, 10, TextAnchor.MiddleCenter);
                    Stretch((RectTransform)skillLabelText.transform, 0f);
                    skillLabelText.supportRichText = true;
                    skillLabelText.text = companion.Type.SkillName;
                }

                var label = NewText("Name", slot, 13, TextAnchor.UpperCenter);
                var lr = (RectTransform)label.transform;
                lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 0f);
                lr.pivot = new Vector2(0.5f, 1f);
                lr.sizeDelta = new Vector2(48f, LabelHeight);
                lr.anchoredPosition = new Vector2(0f, -LabelOffset);
                label.text = name;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;

                // 名字直接壓在地圖上，沒有黑邊會被淺色地形吃掉
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                outline.effectDistance = new Vector2(1.4f, -1.4f);

                var hover = portrait.gameObject.AddComponent<SVHoverTarget>();
                hover.OnEnter = () => _hovered = index;
                hover.OnExit = () => { if (_hovered == index) _hovered = -1; };
                hover.OnClick = () => _pinned = _pinned == index ? -1 : index;

                _entries.Add(new Entry
                {
                    root = slot, portrait = portraitImg, ring = ringImg,
                    mana = manaImg, label = label, companion = companion,
                    skillFill = skillFillImg, skillLabel = skillLabelText,
                });
            }

            _builtFor = roster.Count;
        }

        // ---------------- 每幀 ----------------

        void LateUpdate()
        {
            var game = SVGame.Instance;
            if (game == null || game.Player == null) return;

            if (_builtFor != game.AllCompanions.Count + 1) Rebuild(game);

            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                var c = e.companion;

                if (c == null)
                {
                    var p = game.Player;
                    e.ring.fillAmount = p.MaxHp > 0 ? (float)p.Hp / p.MaxHp : 0f;
                    if (e.mana != null) e.mana.fillAmount = NextSkillReady(p);
                    e.portrait.color = Color.white;
                    continue;
                }

                if (c.Retired)
                {
                    e.ring.fillAmount = 0f;
                    e.portrait.color = new Color(0.35f, 0.35f, 0.38f);   // 退場的變灰
                    e.label.color = new Color(0.5f, 0.5f, 0.55f);

                    if (e.skillFill != null) e.skillFill.fillAmount = 0f;
                    if (e.skillLabel != null)
                    {
                        e.skillLabel.color = new Color(0.55f, 0.55f, 0.58f);
                        e.skillLabel.text = "退場";
                    }
                    continue;
                }

                e.ring.fillAmount = c.MaxHp > 0 ? (float)c.Hp / c.MaxHp : 0f;
                e.portrait.color = Color.white;
                e.label.color = Color.white;

                if (e.skillFill != null)
                    e.skillFill.fillAmount =
                        c.Type.Skill == SVSkillKind.Orbit ? 1f : c.CooldownNormalized;

                if (e.skillLabel != null)
                {
                    e.skillLabel.color = Color.white;

                    // 會輪替元素的（鈴音）順便顯示「下一發是什麼」
                    e.skillLabel.text =
                        c.Type.Skill == SVSkillKind.Arcane &&
                        c.Type.Elements != null && c.Type.Elements.Count > 1
                            ? $"{c.Type.SkillName}\n<size=12>▸ {c.CurrentElementName}</size>"
                            : c.Type.SkillName;
                }
            }

            UpdateDetail(game);
        }

        /// <summary>男主沒有單一的藍條，用「最快可以放的技能」當進度。</summary>
        static float NextSkillReady(SVPlayer player)
        {
            float best = 0f;
            foreach (var s in player.Skills) best = Mathf.Max(best, s.CooldownNormalized);
            return best;
        }

        void UpdateDetail(SVGame game)
        {
            // 釘住的優先；沒釘就看游標壓在誰身上
            int show = _pinned >= 0 && _pinned < _entries.Count ? _pinned : _hovered;
            if (show < 0 || show >= _entries.Count)
            {
                if (_detail.gameObject.activeSelf) _detail.gameObject.SetActive(false);
                return;
            }

            var entry = _entries[show];
            var lines = entry.companion == null
                ? SVBuffs.For(game.Player, game)
                : SVBuffs.For(entry.companion, game);

            _detail.gameObject.SetActive(true);
            _detailTitle.text = entry.label.text + (_pinned == show ? "　(已釘住)" : "");

            // 跟著那顆頭像的高度走，看起來才像從它展開的
            float scale = _column.localScale.y;
            float y = entry.root.anchoredPosition.y * scale;
            _detail.anchoredPosition = new Vector2(
                LeftMargin + (AvatarSize + SkillBarGap + SkillBarWidth) * scale + 12f, y);

            int count = Mathf.Min(lines.Count, _detailLines.Count);
            _detail.sizeDelta = new Vector2(268f, 48f + count * 19f);

            for (int i = 0; i < _detailLines.Count; i++)
            {
                bool has = i < count;
                if (_detailLines[i].gameObject.activeSelf != has)
                    _detailLines[i].gameObject.SetActive(has);
                if (!has) continue;

                _detailLines[i].text = $"{lines[i].Label}　{lines[i].Value}";
                _detailLines[i].color = lines[i].Tint;
            }
        }

        // ---------------- 小工具 ----------------

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.sprite = SVShapes.Square();   // sprite 是空的話 fillAmount 不會生效

            return (RectTransform)go.transform;
        }

        static Text NewText(string name, Transform parent, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = SVFonts.Pixel;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform rt, float padding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-padding, -padding);
            rt.offsetMax = new Vector2(padding, padding);
        }
    }

    /// <summary>滑鼠進出與點擊的小接頭，避免為了三個事件多寫一支 Button。</summary>
    public class SVHoverTarget : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public System.Action OnEnter;
        public System.Action OnExit;
        public System.Action OnClick;

        public void OnPointerEnter(PointerEventData e) => OnEnter?.Invoke();
        public void OnPointerExit(PointerEventData e) => OnExit?.Invoke();
        public void OnPointerClick(PointerEventData e) => OnClick?.Invoke();
    }
}
