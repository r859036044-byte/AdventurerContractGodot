// 小遊戲裡的對話框（含立繪）。
//
// 本篇那套 RMZMessageWindow 用不了 —— 進小遊戲時本篇整個被 SetActive(false)，
// 它的 Canvas 跟著關掉。所以這裡自己做一個輕量版：立繪、名字、一段話、選項。
//
// 用法都是協程：
//   yield return SVTalkWindow.Play(lines);         整段講完
//   yield return SVTalkWindow.Ask(options, r => …) 出選項
using System.Collections;
using System.Collections.Generic;
using RMZS.GodotPort;
using RMZS.GodotPort.EventSystems;
using RMZS.GodotPort.InputSystem;
using RMZS.GodotPort.UI;

namespace RMZS
{
    public class SVTalkWindow : MonoBehaviour
    {
        public static SVTalkWindow Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._open;

        const float RefWidth = 1280f;
        const float RefHeight = 720f;
        const float BoxHeight = 190f;

        /// <summary>立繪畫布的高度。立繪本來就是照整個畫面排的，所以填滿畫面高。</summary>
        // 完整立繪畫布不另設高度上限，與公會 Show Picture 的 100% 比例一致。

        Canvas _canvas;
        RectTransform _root;
        RectTransform _box;
        Image _faceBase;   // 身體
        Image _face;       // 表情，疊在身體上
        Text _name;
        RectTransform _namePlate;
        Image _boxFrame, _boxFill, _plateFrame, _plateFill;
        SVTalkColors _colors = SVTalkStyle.Default;
        Text _text;
        Text _hint;

        RectTransform _choicePanel;
        readonly List<RectTransform> _choiceRows = new List<RectTransform>();
        readonly List<Text> _choiceLabels = new List<Text>();
        readonly List<Image> _choiceFrames = new List<Image>();

        bool _open;
        int _choiceIndex;
        int _choiceCount;
        int _picked = -1;

        // ---------------- 對外 ----------------

        static SVTalkWindow Ensure()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("SVTalkWindow");
            Instance = go.AddComponent<SVTalkWindow>();
            return Instance;
        }

        /// <summary>把一整段講完。玩家按一下（或點滑鼠）翻下一句。</summary>
        public static IEnumerator Play(string speaker, List<SVTalkLine> lines)
        {
            if (lines == null || lines.Count == 0) yield break;

            var w = Ensure();
            w.Show();

            foreach (var line in lines)
            {
                w.SetLine(speaker, line);

                // 放開上一次的按鍵，避免一次連翻兩句
                yield return null;
                while (!Advance()) yield return null;
                yield return null;
            }

            w.Hide();
        }

        /// <summary>只講一句，講完不關窗（給互動用）。</summary>
        public static IEnumerator Say(string speaker, SVTalkLine line)
        {
            var w = Ensure();
            w.Show();
            w.SetLine(speaker, line);

            yield return null;
            while (!Advance()) yield return null;
            yield return null;
        }

        /// <summary>出一組選項。結果從 onPicked 回來。</summary>
        public static IEnumerator Ask(List<string> options, System.Action<int> onPicked)
        {
            var w = Ensure();
            w.Show();
            w.ShowChoices(options);

            w._picked = -1;
            while (w._picked < 0) yield return null;

            int result = w._picked;
            w.HideChoices();
            onPicked?.Invoke(result);
        }

        public static void CloseNow()
        {
            if (Instance != null) Instance.Hide();
        }

        /// <summary>翻頁鍵。滑鼠左鍵、空白、Enter、Z 都可以。</summary>
        static bool Advance()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame ||
                               kb.enterKey.wasPressedThisFrame ||
                               kb.numpadEnterKey.wasPressedThisFrame ||
                               kb.zKey.wasPressedThisFrame)) return true;

            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        // ---------------- 生命週期 ----------------

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Build();
            _root.gameObject.SetActive(false);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Show()
        {
            _open = true;
            _root.gameObject.SetActive(true);
            HideChoices();
        }

        void Hide()
        {
            _open = false;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        void SetLine(string speaker, SVTalkLine line)
        {
            _text.text = line.Text ?? "";

            bool named = !string.IsNullOrEmpty(speaker);
            _namePlate.gameObject.SetActive(named);
            _name.text = named ? speaker : "";

            ApplyColors(SVTalkStyle.For(speaker));

            // 公會事件以完整 1280×720 畫布、原比例顯示立繪；保留原圖中的人物位置。
            var baseSprite = SVTalk.Face(line.Base);
            var faceSprite = SVTalk.Face(line.Face);

            // 尺寸以底圖為準，表情跟著同一個矩形，位置才會對得上
            var reference = baseSprite != null ? baseSprite : faceSprite;
            if (reference != null)
            {
                var size = new Vector2(reference.rect.width, reference.rect.height);
                ((RectTransform)_faceBase.transform).sizeDelta = size;
                ((RectTransform)_face.transform).sizeDelta = size;
            }

            _faceBase.sprite = baseSprite;
            _faceBase.enabled = baseSprite != null;
            _face.sprite = faceSprite;
            _face.enabled = faceSprite != null;
        }

        /// <summary>換上這位說話者的配色。名字牌不透明，才不會跟底下的立繪糊在一起。</summary>
        void ApplyColors(SVTalkColors c)
        {
            var (fill, border, nameFill, speaker) = RMZ.RMZWindowSkin.DialogueColors(_name.text, _name.text);
            // 鈴音：跟戰後互動視窗同一個魔力外框（藍紫框＋光暈）
            bool magic = RMZ.RMZMagicFrame.IsSuzune(_name.text);
            if (magic) { border = RMZ.RMZMagicFrame.Border; nameFill = RMZ.RMZMagicFrame.NameFill; speaker = RMZ.RMZMagicFrame.Speaker; }
            _box.GetComponent<Image>().WindowStyle = magic ? RMZ.RMZMagicFrame.Style(fill, RMZ.RMZWindowSkin.DialogRadius) : RMZ.RMZWindowSkin.PanelStyle(fill, border);
            _namePlate.GetComponent<Image>().WindowStyle = RMZ.RMZWindowSkin.PanelStyle(nameFill, border);
            // 直接共用公會視窗樣式，不再疊整片框色與底色。
            _boxFrame.enabled = _boxFill.enabled = _plateFrame.enabled = _plateFill.enabled = false;
            _name.color = speaker;
            _text.color = RMZ.RMZDialogueStyle.Current.TintText ? speaker : RMZ.RMZWindowSkin.Text;
            _choicePanel.GetComponent<Image>().WindowStyle = RMZ.RMZDialogueStyle.Current.ApplyToChoiceWindow
                ? RMZ.RMZWindowSkin.PanelStyle(fill, border) : RMZ.RMZWindowSkin.PanelStyle();
        }

        /// <summary>同一個底色，但不透明 —— 名字牌用。</summary>
        static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        /// <summary>立繪的一層。整張畫布置中，底圖與表情共用同一個矩形。</summary>
        Image NewPortraitLayer(string name)
        {
            var go = NewRect(name, _root);
            go.anchorMin = go.anchorMax = go.pivot = new Vector2(0f, 1f);
            go.anchoredPosition = Vector2.zero;

            var img = go.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.enabled = false;
            return img;
        }

        // ---------------- 選項 ----------------

        void ShowChoices(List<string> options)
        {
            _choiceCount = Mathf.Min(options.Count, _choiceRows.Count);
            _choiceIndex = 0;

            for (int i = 0; i < _choiceRows.Count; i++)
            {
                bool has = i < _choiceCount;
                _choiceRows[i].gameObject.SetActive(has);
                if (has) _choiceLabels[i].text = options[i];
            }

            _choicePanel.sizeDelta = new Vector2(260f, _choiceCount * 44f + 12f);
            _choicePanel.gameObject.SetActive(true);
            RefreshChoices();
        }

        void HideChoices()
        {
            _choiceCount = 0;
            if (_choicePanel != null) _choicePanel.gameObject.SetActive(false);
        }

        void RefreshChoices()
        {
            for (int i = 0; i < _choiceCount; i++)
            {
                bool on = i == _choiceIndex;
                _choiceRows[i].GetComponent<Image>().WindowStyle = RMZ.RMZWindowSkin.RowStyle(on);
                _choiceFrames[i].enabled = false;
                _choiceFrames[i].color = on ? new Color(1f, 0.85f, 0.45f, 0.9f) : new Color(1f, 1f, 1f, 0f);
                _choiceRows[i].GetComponent<Image>().color = on
                    ? new Color(0.20f, 0.22f, 0.30f, 1f)
                    : new Color(0.11f, 0.12f, 0.17f, 0.96f);
                _choiceLabels[i].color = on ? new Color(1f, 0.93f, 0.72f) : Color.white;
            }
        }

        void Update()
        {
            if (!_open || _choiceCount <= 0) return;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) MoveChoice(1);
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) MoveChoice(-1);
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame ||
                    kb.spaceKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame)
                {
                    _picked = _choiceIndex;
                    return;
                }
            }

            var mouse = Mouse.current;
            if (mouse == null) return;

            var pos = mouse.position.ReadValue();
            for (int i = 0; i < _choiceCount; i++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(_choiceRows[i], pos, null)) continue;

                if (_choiceIndex != i) { _choiceIndex = i; RefreshChoices(); }
                if (mouse.leftButton.wasPressedThisFrame) _picked = i;
                break;
            }
        }

        void MoveChoice(int delta)
        {
            _choiceIndex = Mathf.Clamp(_choiceIndex + delta, 0, _choiceCount - 1);
            RefreshChoices();
        }

        // ---------------- 版面 ----------------

        void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 點陣字型要落在整數像素上才不會整體偏一點點
            _canvas.pixelPerfect = true;
            _canvas.sortingOrder = 700;   // HUD(500)、角色欄(520)、指揮(600) 之上

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = NewRect("Root", transform);
            Stretch(_root);

            // 立繪：底圖 + 表情兩層疊起來。
            // 兩張都是整個畫布大小、角色畫在同一個位置，所以用同一個矩形貼上去就會對齊；
            // 只放表情的話畫面上就只有一雙眼睛浮在那裡。
            _faceBase = NewPortraitLayer("FaceBase");
            _face = NewPortraitLayer("Face");

            // 對話框。
            //
            // 疊法跟本篇一樣：最外面一圈暗線 → 金色框 → 底色，一層比一層小 3px，
            // 所以看起來就是「一條細框包著半透明底」。
            // （原本是把整片金色貼在底色上面當框，結果整個框都變成土黃色，
            //   跟本篇的對話框長得完全不一樣 —— 那是這裡看起來很怪的原因。）
            _box = NewImage("Box", _root, _colors.FrameDark);
            _box.anchorMin = new Vector2(0f, 0f);
            _box.anchorMax = new Vector2(1f, 0f);
            _box.pivot = new Vector2(0.5f, 0f);
            _box.sizeDelta = new Vector2(-48f, BoxHeight);
            _box.anchoredPosition = new Vector2(0f, 24f);

            _boxFrame = NewImage("Frame", _box, _colors.Frame).GetComponent<Image>();
            Stretch((RectTransform)_boxFrame.transform, -1f);

            _boxFill = NewImage("Fill", (RectTransform)_boxFrame.transform, _colors.Fill).GetComponent<Image>();
            Stretch((RectTransform)_boxFill.transform, -2f);

            // 名字牌
            _namePlate = NewImage("NamePlate", _box, _colors.FrameDark);
            _namePlate.anchorMin = new Vector2(0f, 1f);
            _namePlate.anchorMax = new Vector2(0f, 1f);
            _namePlate.pivot = new Vector2(0f, 0f);
            _namePlate.sizeDelta = new Vector2(180f, 36f);
            _namePlate.anchoredPosition = new Vector2(28f, 4f);

            _plateFrame = NewImage("Frame", _namePlate, _colors.Frame).GetComponent<Image>();
            Stretch((RectTransform)_plateFrame.transform, -1f);

            _plateFill = NewImage("Fill", (RectTransform)_plateFrame.transform, Opaque(_colors.Fill)).GetComponent<Image>();
            Stretch((RectTransform)_plateFill.transform, -2f);

            _name = NewText("Name", _namePlate, RMZ.RMZFonts.DefaultSize, TextAnchor.MiddleCenter);
            Stretch((RectTransform)_name.transform);
            _name.color = new Color(1f, 0.9f, 0.6f);

            _text = NewText("Text", _box, RMZ.RMZFonts.DefaultSize, TextAnchor.UpperLeft);
            var tr = (RectTransform)_text.transform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(32f, 34f);
            tr.offsetMax = new Vector2(-32f, -22f);
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.lineSpacing = 1.25f;

            _hint = NewText("Hint", _box, 15, TextAnchor.LowerRight);
            var hr = (RectTransform)_hint.transform;
            hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one;
            hr.offsetMin = new Vector2(0f, 8f);
            hr.offsetMax = new Vector2(-24f, 0f);
            _hint.text = "點一下 / 空白鍵　繼續";
            _hint.color = new Color(0.6f, 0.62f, 0.7f);

            BuildChoices();
        }

        void BuildChoices()
        {
            _choicePanel = NewImage("Choices", _root, new Color(0.07f, 0.08f, 0.12f, 0.96f));
            _choicePanel.anchorMin = new Vector2(1f, 0f);
            _choicePanel.anchorMax = new Vector2(1f, 0f);
            _choicePanel.pivot = new Vector2(1f, 0f);
            _choicePanel.sizeDelta = new Vector2(260f, 144f);
            _choicePanel.anchoredPosition = new Vector2(-470f, BoxHeight + 36f);

            var frame = NewImage("Frame", _choicePanel, new Color(0.82f, 0.70f, 0.38f, 120f / 255f));
            Stretch(frame, -2f);
            frame.SetSiblingIndex(0);
            frame.GetComponent<Image>().enabled = false;
            _choicePanel.GetComponent<Image>().WindowStyle = RMZ.RMZWindowSkin.PanelStyle();

            for (int i = 0; i < 8; i++)
            {
                var row = NewImage("Choice" + i, _choicePanel, new Color(0.11f, 0.12f, 0.17f, 0.96f));
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.sizeDelta = new Vector2(-12f, 40f);
                row.anchoredPosition = new Vector2(0f, -6f - i * 44f);
                row.GetComponent<Image>().raycastTarget = true;

                var rowFrame = NewImage("Frame", row, new Color(1f, 1f, 1f, 0f)).GetComponent<Image>();
                Stretch((RectTransform)rowFrame.transform, -2f);
                rowFrame.raycastTarget = false;

                var label = NewText("Label", row, 19, TextAnchor.MiddleCenter);
                Stretch((RectTransform)label.transform);

                _choiceRows.Add(row);
                _choiceFrames.Add(rowFrame);
                _choiceLabels.Add(label);
            }

            _choicePanel.gameObject.SetActive(false);
        }

        // ---------------- 小工具 ----------------

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<RMZS.GodotPort.InputSystem.UI.InputSystemUIInputModule>();
        }

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
            img.sprite = SVShapes.Square();
            img.raycastTarget = false;
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
            t.horizontalOverflow = HorizontalWrapMode.Overflow;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);
            return t;
        }

        static void Stretch(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-padding, -padding);
            rt.offsetMax = new Vector2(padding, padding);
        }
    }
}
