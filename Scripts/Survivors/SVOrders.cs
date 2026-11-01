// 隊友的指揮系統：滑鼠拖曳定點、長按叫出「自由移動／定點待命／跟隨玩家」選單。
//
// 只有「僱傭」和「獨自組隊」才有指揮權；加入別人的隊伍時整支不會被建出來，
// 隊友一律 Free，玩家只能顧好自己。
using System.Collections.Generic;
using RMZS.GodotPort;
using RMZS.GodotPort.InputSystem;
using RMZS.GodotPort.UI;

namespace RMZS
{
    public enum SVOrder
    {
        /// <summary>自由移動：自己找敵人打。</summary>
        Free = 0,
        /// <summary>定點待命：守在指定的點上。</summary>
        Hold = 1,
        /// <summary>跟隨玩家。</summary>
        Follow = 2,
    }

    /// <summary>把滑鼠操作翻成隊友的命令。場上只會有一個。</summary>
    public class SVCommandController : MonoBehaviour
    {
        /// <summary>按住多久算長按。</summary>
        public const float LongPressTime = 0.35f;

        /// <summary>按住之後滑鼠移動超過這個距離（世界單位）就算拖曳，不算長按。</summary>
        public const float DragThreshold = 0.45f;

        /// <summary>點在隊友身上的判定半徑，比實際碰撞半徑寬一點才好點。</summary>
        public const float PickRadius = 0.75f;

        public static SVCommandController Instance { get; private set; }

        SVCompanion _pressed;          // 正按著的隊友
        float _pressTime;
        Vector2 _pressWorld;
        bool _dragging;

        SVCompanion _menuTarget;       // 選單開在誰頭上
        SVCompanion _awaitingPoint;    // 選了「定點待命」，等玩家點位置

        Canvas _canvas;
        RectTransform _menuRoot;
        readonly List<Button> _menuButtons = new List<Button>();
        readonly List<Text> _menuLabels = new List<Text>();
        Text _hintText;
        Transform _marker;

        static readonly string[] MenuTexts = { "自由移動", "定點待命", "跟隨玩家" };

        void Awake()
        {
            Instance = this;
            BuildUi();
            BuildMarker();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ---------------- 介面 ----------------

        void BuildUi()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 點陣字型要落在整數像素上才不會整體偏一點點
            _canvas.pixelPerfect = true;
            _canvas.sortingOrder = 600;          // 蓋在 HUD(500) 上面

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();

            _menuRoot = NewPanel("OrderMenu", transform, new Color(0f, 0f, 0f, 0f));
            _menuRoot.sizeDelta = new Vector2(132f, 3f * 34f + 4f * 4f);
            // 座標直接用「Canvas 左下角起算的像素」，所以錨點釘在左下，
            // 樞紐放在下緣中央，選單才會浮在頭頂正上方。
            _menuRoot.anchorMin = _menuRoot.anchorMax = Vector2.zero;
            _menuRoot.pivot = new Vector2(0.5f, 0f);

            var bg = NewPanel("Bg", _menuRoot, new Color(0.06f, 0.07f, 0.11f, 0.92f));
            bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one;
            bg.offsetMin = Vector2.zero; bg.offsetMax = Vector2.zero;

            var frame = NewPanel("Frame", _menuRoot, new Color(0.82f, 0.70f, 0.38f, 1f));
            frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one;
            frame.offsetMin = new Vector2(-2f, -2f); frame.offsetMax = new Vector2(2f, 2f);
            frame.SetSiblingIndex(0);

            for (int i = 0; i < MenuTexts.Length; i++)
            {
                int index = i;

                var item = NewPanel("Item" + i, _menuRoot, new Color(0.16f, 0.18f, 0.24f, 1f));
                item.anchorMin = item.anchorMax = item.pivot = new Vector2(0.5f, 0f);
                item.sizeDelta = new Vector2(124f, 34f);
                // 由下往上排：陣列 0 在最上面，所以反過來擺
                item.anchoredPosition = new Vector2(0f, 4f + (MenuTexts.Length - 1 - i) * 38f);

                var button = item.gameObject.AddComponent<Button>();
                button.targetGraphic = item.GetComponent<Image>();
                var colors = button.colors;
                colors.highlightedColor = new Color(0.32f, 0.36f, 0.48f, 1f);
                colors.pressedColor = new Color(0.5f, 0.42f, 0.2f, 1f);
                button.colors = colors;
                button.onClick.AddListener(() => OnMenuClick(index));
                _menuButtons.Add(button);

                var label = NewText("Label", item, 18, TextAnchor.MiddleCenter);
                var lr = (RectTransform)label.transform;
                lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
                lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
                label.text = MenuTexts[i];
                _menuLabels.Add(label);
            }

            _hintText = NewText("Hint", transform, 20, TextAnchor.LowerCenter);
            var hr = (RectTransform)_hintText.transform;
            hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(0.5f, 0f);
            hr.sizeDelta = new Vector2(700f, 34f);
            hr.anchoredPosition = new Vector2(0f, 96f);
            _hintText.color = new Color(1f, 0.92f, 0.65f);

            _menuRoot.gameObject.SetActive(false);
            _hintText.gameObject.SetActive(false);
        }

        void BuildMarker()
        {
            var go = new GameObject("HoldMarker");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SVShapes.Ring();
            sr.color = new Color(1f, 0.85f, 0.4f, 0.75f);
            sr.sortingOrder = -20000;
            go.transform.localScale = Vector3.one * 1.1f;
            go.SetActive(false);
            _marker = go.transform;
        }

        RectTransform NewPanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        Text NewText(string name, Transform parent, int size, TextAnchor anchor)
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
            return t;
        }

        // ---------------- 每幀 ----------------

        void Update()
        {
            var game = SVGame.Instance;
            if (game == null || !game.PlayerCommands) { CloseMenu(); return; }

            var mouse = Mouse.current;
            var cam = game.Cam;
            if (mouse == null || cam == null) return;

            // 死亡畫面、選強化的時候不收指揮輸入
            if (game.IsOver || game.Choosing) { CloseMenu(); return; }

            var world = (Vector2)cam.ScreenToWorldPoint(mouse.position.ReadValue());

            if (_awaitingPoint != null) { UpdateAwaitingPoint(mouse, world); return; }

            if (mouse.leftButton.wasPressedThisFrame) BeginPress(mouse, world);
            else if (mouse.leftButton.isPressed) ContinuePress(world);
            else if (mouse.leftButton.wasReleasedThisFrame) EndPress(world);

            // 右鍵取消：關選單、解除定點
            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (_menuRoot.gameObject.activeSelf) CloseMenu();
                else
                {
                    var hit = Pick(world);
                    if (hit != null) hit.SetOrder(SVOrder.Follow);
                }
            }

            UpdateMenuPosition();
        }

        void BeginPress(Mouse mouse, Vector2 world)
        {
            // 點在選單上就交給 Button 處理，不要當成點地圖
            if (_menuRoot.gameObject.activeSelf && IsPointerOverMenu(mouse)) return;

            CloseMenu();
            _pressed = Pick(world);
            _pressTime = 0f;
            _pressWorld = world;
            _dragging = false;
        }

        void ContinuePress(Vector2 world)
        {
            if (_pressed == null) return;

            _pressTime += Time.unscaledDeltaTime;

            if (!_dragging && (world - _pressWorld).magnitude > DragThreshold)
            {
                _dragging = true;
                _pressed.BeginDrag();
            }

            if (_dragging) _pressed.DragTo(world);
            else if (_pressTime >= LongPressTime) { OpenMenu(_pressed); _pressed = null; }
        }

        void EndPress(Vector2 world)
        {
            if (_pressed == null) return;

            if (_dragging)
            {
                // 拖到哪就守在哪
                _pressed.EndDrag();
                _pressed.HoldAt(world);
                ShowHint($"{_pressed.DisplayName} 在此待命");
            }

            _pressed = null;
            _dragging = false;
        }

        void UpdateAwaitingPoint(Mouse mouse, Vector2 world)
        {
            _marker.gameObject.SetActive(true);
            _marker.position = world;

            if (mouse.rightButton.wasPressedThisFrame)
            {
                _awaitingPoint = null;
                _marker.gameObject.SetActive(false);
                HideHint();
                return;
            }

            if (!mouse.leftButton.wasPressedThisFrame) return;

            _awaitingPoint.HoldAt(world);
            ShowHint($"{_awaitingPoint.DisplayName} 在此待命");
            _awaitingPoint = null;
            _marker.gameObject.SetActive(false);
        }

        // ---------------- 選單 ----------------

        /// <summary>
        /// 這一下左鍵是不是指揮系統要的（點在隊友身上、選單開著、正在等玩家點待命位置）。
        /// 男主的滑鼠移動會先問這個，是的話就不當成「走到這裡」。
        /// </summary>
        public static bool WantsClick(Vector2 world)
        {
            var self = Instance;
            if (self == null) return false;
            var game = SVGame.Instance;
            if (game == null || !game.PlayerCommands) return false;
            if (self._awaitingPoint != null) return true;
            if (self._menuRoot != null && self._menuRoot.gameObject.activeSelf) return true;
            return Pick(world) != null;
        }

        static SVCompanion Pick(Vector2 world)
        {
            SVCompanion best = null;
            float bestSq = PickRadius * PickRadius;
            foreach (var c in SVCompanion.Active)
            {
                if (c == null) continue;
                float sq = (c.Pos - world).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = c; }
            }
            return best;
        }

        void OpenMenu(SVCompanion target)
        {
            _menuTarget = target;
            _menuRoot.gameObject.SetActive(true);
            UpdateMenuPosition();

            // 目前的命令高亮一下，讓玩家知道現在是什麼狀態
            for (int i = 0; i < _menuLabels.Count; i++)
                _menuLabels[i].color = (int)target.Order == i
                    ? new Color(1f, 0.88f, 0.45f) : Color.white;
        }

        void CloseMenu()
        {
            _menuTarget = null;
            if (_menuRoot != null) _menuRoot.gameObject.SetActive(false);
        }

        void UpdateMenuPosition()
        {
            if (_menuTarget == null || !_menuRoot.gameObject.activeSelf) return;

            var game = SVGame.Instance;
            if (game == null || game.Cam == null) { CloseMenu(); return; }

            // 世界座標 → 螢幕 → Canvas。ScreenSpaceOverlay 的 Canvas 座標就是螢幕像素，
            // 但 CanvasScaler 會縮，所以要除回去。
            var screen = game.Cam.WorldToScreenPoint(_menuTarget.Pos + Vector2.up * 0.9f);
            float scale = _canvas.scaleFactor <= 0f ? 1f : _canvas.scaleFactor;
            _menuRoot.anchoredPosition = new Vector2(screen.x / scale, screen.y / scale);
        }

        bool IsPointerOverMenu(Mouse mouse)
        {
            var screen = mouse.position.ReadValue();
            return RectTransformUtility.RectangleContainsScreenPoint(_menuRoot, screen, null);
        }

        void OnMenuClick(int index)
        {
            var target = _menuTarget;
            CloseMenu();
            if (target == null) return;

            switch ((SVOrder)index)
            {
                case SVOrder.Free:
                    target.SetOrder(SVOrder.Free);
                    ShowHint($"{target.DisplayName} 自由行動");
                    break;

                case SVOrder.Hold:
                    _awaitingPoint = target;
                    ShowHint($"點一個位置讓 {target.DisplayName} 待命（右鍵取消）");
                    break;

                case SVOrder.Follow:
                    target.SetOrder(SVOrder.Follow);
                    ShowHint($"{target.DisplayName} 跟著你");
                    break;
            }
        }

        // ---------------- 提示 ----------------

        float _hintLeft;

        void ShowHint(string text)
        {
            _hintText.text = text;
            _hintText.gameObject.SetActive(true);
            _hintLeft = 2.2f;
        }

        void HideHint()
        {
            _hintLeft = 0f;
            _hintText.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (_hintLeft <= 0f) return;

            // 等玩家點位置的時候提示要一直留著
            if (_awaitingPoint != null) { _hintLeft = 2.2f; return; }

            _hintLeft -= Time.unscaledDeltaTime;
            if (_hintLeft <= 0f) _hintText.gameObject.SetActive(false);
        }
    }
}
