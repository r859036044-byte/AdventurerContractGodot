// 介面：血條、經驗條、時間、擊殺數、隊友技能 CD、結束畫面。
// 全部用程式生成的 uGUI，不需要任何美術資產。
using System.Collections.Generic;
using RMZS.GodotPort;
using RMZS.GodotPort.EventSystems;
using RMZS.GodotPort.UI;

namespace RMZS
{
    public class SVHud : MonoBehaviour
    {
        const float RefWidth = 1280f;
        const float RefHeight = 720f;

        Canvas _canvas;
        Font _font;

        Image _hpFill;
        Image _shieldFill;
        Text _hpText;
        Image _xpFill;
        Text _levelText;
        Text _timerText;
        Text _killText;
        Text _powerText;   // 力量藥水生效中：畫面上方大字＋倒數
        RectTransform _pauseButton;
        Image _pauseFrame;
        Text _pauseLabel;

        // 結尾王的血條（打王階段才出現）
        RectTransform _bossBar;
        Image _bossFill;
        Text _bossName;

        // 隊友的藍條（技能冷卻）已經移到左邊角色欄、貼在各自的頭像右邊，
        // 見 SVCharacterPanel。這裡不再有那一排。

        // 男主的主動技能按鈕（畫面最下方，可以點也可以按技能鍵，預設 Q W E）
        RectTransform _playerRow;
        readonly List<Image> _playerFills = new List<Image>();
        readonly List<Text> _playerLabels = new List<Text>();
        readonly List<Button> _playerButtons = new List<Button>();
        int _playerSkillsVersion = -1;

        // 大覺按鈕（Q/E/F/C）
        RectTransform _awakenRow;
        readonly List<Image> _awakenFills = new List<Image>();
        readonly List<Text> _awakenLabels = new List<Text>();
        readonly List<Image> _awakenFrames = new List<Image>();
        int _awakenVersion = -1;

        // 右下角：戰利品與返回公會
        Text _lootText;
        RectTransform _retreatButton;
        Image _retreatFill;
        Text _retreatLabel;

        // 吃到補給品時畫面中央偏上的提示
        Text _pickupBanner;

        RectTransform _overPanel;
        Text _overText;

        Text _choiceTitle;
        RectTransform _choicePanel;
        readonly List<RectTransform> _choiceCards = new List<RectTransform>();
        readonly List<Text> _choiceTitles = new List<Text>();
        readonly List<Text> _choiceDescs = new List<Text>();
        readonly List<Image> _choiceIcons = new List<Image>();
        readonly List<Image> _choiceHeads = new List<Image>();
        readonly List<Text> _choiceHeadNames = new List<Text>();
        readonly List<Image> _choiceFrames = new List<Image>();
        object _shownChoices;

        void Awake()
        {
            _font = LoadFont();
            Build();
        }

        static Font LoadFont() => SVFonts.Pixel;

        // ---------------- 版面 ----------------

        void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 點陣字型要落在整數像素上才不會整體偏一點點
            _canvas.pixelPerfect = true;
            _canvas.sortingOrder = 500;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            gameObject.AddComponent<GraphicRaycaster>();

            BuildHurtVignette();   // 最先建，才會被其他 HUD 元件蓋在上面
            BuildEndBanner();
            BuildTopBars();
            BuildBossBar();
            BuildStatsBox();
            BuildSkillRow();
            BuildChoicePanel();
            BuildOverPanel();
            EnsureEventSystem();
        }

        Image _hurtVignette;

        /// <summary>
        /// 受傷時的血色暈染。整片蓋在畫面上，但中間是透空的，
        /// 所以只有四周泛紅，不會擋到玩家看場中的東西。
        /// </summary>
        void BuildHurtVignette()
        {
            var rt = NewImage("HurtVignette", transform, new Color(0.75f, 0.05f, 0.08f, 0f));
            Stretch(rt, 0f);

            _hurtVignette = rt.GetComponent<Image>();
            _hurtVignette.sprite = SVShapes.Vignette();
            _hurtVignette.type = Image.Type.Simple;
            _hurtVignette.raycastTarget = false;   // 不能擋住底下的按鈕
            _hurtVignette.enabled = false;
        }

        void UpdateHurtVignette(SVGame game)
        {
            // 挨打的瞬間閃一下，殘血時再疊一層固定的底噪
            float alpha = Mathf.Clamp01(game.HurtFlash + game.LowHpTint);

            bool show = alpha > 0.002f;
            if (_hurtVignette.enabled != show) _hurtVignette.enabled = show;
            if (!show) return;

            var c = _hurtVignette.color;
            _hurtVignette.color = new Color(c.r, c.g, c.b, alpha);
        }

        Text _endBanner;

        /// <summary>打完王之後的提示（雜魚在逃、剩幾秒）。平常收起來。</summary>
        void BuildEndBanner()
        {
            _endBanner = NewText("EndBanner", transform, 24, TextAnchor.MiddleCenter);
            Anchor((RectTransform)_endBanner.transform,
                   new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            ((RectTransform)_endBanner.transform).sizeDelta = new Vector2(700f, 34f);
            ((RectTransform)_endBanner.transform).anchoredPosition = new Vector2(0f, -74f);
            _endBanner.color = new Color(1f, 0.86f, 0.5f);
            _endBanner.gameObject.SetActive(false);
        }

        void UpdateEndBanner(SVGame game)
        {
            bool show = game.EndPhase == SVGame.SVEndPhase.Fleeing;
            if (_endBanner.gameObject.activeSelf != show) _endBanner.gameObject.SetActive(show);
            if (!show) return;

            _endBanner.text = SVEnemy.Active.Count > 0
                ? $"魔物四散奔逃！追上去還來得及　　剩下 {SVEnemy.Active.Count} 隻　{Mathf.CeilToInt(game.FleeLeft)} 秒"
                : "都跑光了……";
        }

        void BuildTopBars()
        {
            // 血條左邊那顆愛心。底下墊一層柔邊的暗色光暈，
            // 這樣不管地圖是亮的還暗的，圖示都不會糊進背景裡。
            var heartHalo = NewImage("HeartHalo", transform, new Color(0f, 0f, 0f, 0.55f));
            Anchor(heartHalo, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            heartHalo.sizeDelta = new Vector2(64f, 64f);
            heartHalo.anchoredPosition = new Vector2(12f, -4f);
            heartHalo.GetComponent<Image>().sprite = SVShapes.Soft();

            var heart = NewImage("Heart", transform, Color.white);
            Anchor(heart, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            heart.sizeDelta = new Vector2(38f, 38f);
            heart.anchoredPosition = new Vector2(24f, -14f);
            var heartImg = heart.GetComponent<Image>();
            heartImg.sprite = SVArt.Heart;
            heartImg.preserveAspect = true;
            heartImg.enabled = heartImg.sprite != null;

            // 血條
            var hpBg = NewImage("HpBg", transform, new Color(0f, 0f, 0f, 0.55f));
            Anchor(hpBg, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            hpBg.sizeDelta = new Vector2(302f, 26f);
            hpBg.anchoredPosition = new Vector2(62f, -20f);

            _hpFill = NewImage("HpFill", hpBg, new Color(0.85f, 0.25f, 0.3f)).GetComponent<Image>();
            Stretch((RectTransform)_hpFill.transform, 3f);
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;

            _shieldFill = NewImage("ShieldFill", hpBg, new Color(0.45f, 0.8f, 1f, 0.55f)).GetComponent<Image>();
            Stretch((RectTransform)_shieldFill.transform, 0f);
            _shieldFill.type = Image.Type.Filled;
            _shieldFill.fillMethod = Image.FillMethod.Horizontal;
            _shieldFill.raycastTarget = false;
            _shieldFill.enabled = false;

            _hpText = NewText("HpText", hpBg, 18, TextAnchor.MiddleCenter);
            Stretch((RectTransform)_hpText.transform, 0f);

            // 能量條（外面本篇已經有「經驗值」這個詞了，這裡改叫能量，
            // 免得玩家搞混——集滿能量不是升等，是開一次海克斯三選一）
            var xpLabel = NewText("EnergyLabel", transform, 13, TextAnchor.MiddleLeft);
            Anchor((RectTransform)xpLabel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            ((RectTransform)xpLabel.transform).sizeDelta = new Vector2(56f, 14f);
            ((RectTransform)xpLabel.transform).anchoredPosition = new Vector2(4f, -52f);
            xpLabel.text = "能量值";
            xpLabel.color = new Color(0.85f, 0.9f, 1f, 0.9f);

            var xpBg = NewImage("XpBg", transform, new Color(0f, 0f, 0f, 0.55f));
            Anchor(xpBg, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            xpBg.sizeDelta = new Vector2(302f, 14f);
            xpBg.anchoredPosition = new Vector2(62f, -52f);

            _xpFill = NewImage("XpFill", xpBg, new Color(0.35f, 0.75f, 1f)).GetComponent<Image>();
            Stretch((RectTransform)_xpFill.transform, 2f);
            _xpFill.type = Image.Type.Filled;
            _xpFill.fillMethod = Image.FillMethod.Horizontal;

            _levelText = NewText("Level", transform, 20, TextAnchor.MiddleLeft);
            Anchor((RectTransform)_levelText.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            ((RectTransform)_levelText.transform).sizeDelta = new Vector2(200f, 24f);
            ((RectTransform)_levelText.transform).anchoredPosition = new Vector2(376f, -33f);

            // 時間與擊殺
            _timerText = NewText("Timer", transform, 34, TextAnchor.UpperCenter);
            Anchor((RectTransform)_timerText.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            ((RectTransform)_timerText.transform).sizeDelta = new Vector2(300f, 44f);
            ((RectTransform)_timerText.transform).anchoredPosition = new Vector2(0f, -16f);

            _powerText = NewText("PowerBuff", transform, 26, TextAnchor.UpperCenter);
            Anchor((RectTransform)_powerText.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            ((RectTransform)_powerText.transform).sizeDelta = new Vector2(420f, 36f);
            ((RectTransform)_powerText.transform).anchoredPosition = new Vector2(0f, -100f);
            _powerText.text = "";

            _killText = NewText("Kills", transform, 20, TextAnchor.UpperRight);
            Anchor((RectTransform)_killText.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            ((RectTransform)_killText.transform).sizeDelta = new Vector2(240f, 28f);
            // 右邊留給暫停鈕與能力面板鈕兩顆
            ((RectTransform)_killText.transform).anchoredPosition = new Vector2(-194f, -22f);

            BuildPauseButton();
        }

        /// <summary>右上角的暫停鈕。隨時都能按，包括剛開場跟王出來的時候。</summary>
        void BuildPauseButton()
        {
            _pauseButton = NewImage("Pause", transform, new Color(0.12f, 0.11f, 0.14f, 0.85f));
            Anchor(_pauseButton, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            _pauseButton.sizeDelta = new Vector2(48f, 48f);
            _pauseButton.anchoredPosition = new Vector2(-20f, -16f);
            _pauseButton.GetComponent<Image>().raycastTarget = true;

            _pauseFrame = NewImage("Frame", _pauseButton, new Color(0.85f, 0.8f, 0.6f, 0.85f)).GetComponent<Image>();
            Stretch((RectTransform)_pauseFrame.transform, -2f);
            _pauseFrame.raycastTarget = false;
            _pauseFrame.transform.SetAsFirstSibling();

            _pauseLabel = NewText("Label", _pauseButton, 24, TextAnchor.MiddleCenter);
            Stretch((RectTransform)_pauseLabel.transform, 0f);
            _pauseLabel.text = "II";

            var button = _pauseButton.gameObject.AddComponent<Button>();
            button.targetGraphic = _pauseButton.GetComponent<Image>();
            var pauseColors = button.colors;
            pauseColors.highlightedColor = new Color(1.6f, 1.6f, 1.6f);
            pauseColors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = pauseColors;
            button.onClick.AddListener(() => SVGame.Instance?.TogglePause());

            BuildStatsButton();
        }

        /// <summary>暫停鈕左邊那顆：打開技能一覽（這局拿到的技能與效果）。打開時遊戲暫停。能力值在左下角常駐。</summary>
        void BuildStatsButton()
        {
            var btn = NewImage("Stats", transform, new Color(0.12f, 0.11f, 0.14f, 0.85f));
            Anchor(btn, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            btn.sizeDelta = new Vector2(48f, 48f);
            btn.anchoredPosition = new Vector2(-132f, -16f);
            btn.GetComponent<Image>().raycastTarget = true;

            var frame = NewImage("Frame", btn, new Color(0.85f, 0.8f, 0.6f, 0.85f)).GetComponent<Image>();
            Stretch((RectTransform)frame.transform, -2f);
            frame.raycastTarget = false;
            frame.transform.SetAsFirstSibling();

            var label = NewText("Label", btn, 17, TextAnchor.MiddleCenter);
            Stretch((RectTransform)label.transform, 0f);
            label.text = "技能";

            var button = btn.gameObject.AddComponent<Button>();
            button.targetGraphic = btn.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            button.onClick.AddListener(() => RMZ.SVStatsOverlay.Toggle());

            BuildOptionsButton();
        }

        /// <summary>暫停鈕跟技能鈕中間那顆：打開設置（音量、按鍵…）。開著時遊戲暫停。</summary>
        void BuildOptionsButton()
        {
            var btn = NewImage("Options", transform, new Color(0.12f, 0.11f, 0.14f, 0.85f));
            Anchor(btn, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            btn.sizeDelta = new Vector2(48f, 48f);
            btn.anchoredPosition = new Vector2(-76f, -16f);
            btn.GetComponent<Image>().raycastTarget = true;

            var frame = NewImage("Frame", btn, new Color(0.85f, 0.8f, 0.6f, 0.85f)).GetComponent<Image>();
            Stretch((RectTransform)frame.transform, -2f);
            frame.raycastTarget = false;
            frame.transform.SetAsFirstSibling();

            var label = NewText("Label", btn, 17, TextAnchor.MiddleCenter);
            Stretch((RectTransform)label.transform, 0f);
            label.text = "設置";

            var button = btn.gameObject.AddComponent<Button>();
            button.targetGraphic = btn.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            button.onClick.AddListener(() => { if (!RMZ.RMZOptionsScene.IsOpen) RMZ.RMZOptionsScene.OpenOnTop(); });
        }

        void BuildBossBar()
        {
            _bossBar = NewImage("BossBar", transform, new Color(0f, 0f, 0f, 0.65f));
            Anchor(_bossBar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            _bossBar.sizeDelta = new Vector2(620f, 26f);
            _bossBar.anchoredPosition = new Vector2(0f, -68f);

            var frame = NewImage("Frame", _bossBar, new Color(0.9f, 0.35f, 0.35f, 0.9f)).GetComponent<Image>();
            Stretch((RectTransform)frame.transform, -2f);
            frame.raycastTarget = false;
            frame.transform.SetAsFirstSibling();

            _bossFill = NewImage("Fill", _bossBar, new Color(0.85f, 0.2f, 0.25f)).GetComponent<Image>();
            Stretch((RectTransform)_bossFill.transform, 2f);
            _bossFill.type = Image.Type.Filled;
            _bossFill.fillMethod = Image.FillMethod.Horizontal;
            _bossFill.raycastTarget = false;

            _bossName = NewText("Name", _bossBar, 18, TextAnchor.MiddleCenter);
            Stretch((RectTransform)_bossName.transform, 0f);

            _bossBar.gameObject.SetActive(false);
        }

        void BuildSkillRow()
        {
            // 畫面正下方兩排：上面是大覺，下面是男主自己的技能。
            // 隊友的藍條不在這裡了（移到左邊角色欄的頭像右邊），所以正下方空得出來。
            _awakenRow = NewImage("AwakenRow", transform, new Color(0f, 0f, 0f, 0f));
            Anchor(_awakenRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            _awakenRow.sizeDelta = new Vector2(400f, 62f);
            _awakenRow.anchoredPosition = new Vector2(0f, AwakenRowY);

            _playerRow = NewImage("PlayerSkillRow", transform, new Color(0f, 0f, 0f, 0f));
            Anchor(_playerRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            _playerRow.sizeDelta = new Vector2(PlayerSlotSize * 3f, PlayerSlotSize);
            _playerRow.anchoredPosition = new Vector2(0f, PlayerRowY);

            BuildLootAndRetreat();

            // 上緣對齊、往下長：補給品單行位置不變，羈絆說明可以放好幾行
            _pickupBanner = NewText("PickupBanner", transform, 30, TextAnchor.UpperCenter);
            var pb = (RectTransform)_pickupBanner.transform;
            Anchor(pb, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            pb.sizeDelta = new Vector2(1000f, 190f);
            pb.anchoredPosition = new Vector2(0f, -114f);
            _pickupBanner.gameObject.SetActive(false);
        }

        // ================= 共用的按鈕樣式 =================
        //
        // 半透明的底 + 圓角空心外框 + 一格圖示。選擇卡、技能鈕、大覺鈕都走這一套，
        // 三處長得一樣，之後改配色只要改這幾個常數。

        static readonly Color SlotFill = new Color(0.06f, 0.07f, 0.11f, 0.42f);
        static readonly Color SlotFillOn = new Color(0.79f, 0.66f, 0.36f, 0.28f);
        static readonly Color SlotBorder = new Color(0.05f, 0.05f, 0.06f, 0.95f);
        static readonly Color SlotBorderOn = new Color(1f, 0.85f, 0.45f, 1f);
        const int SlotRadius = 12;

        /// <summary>把一個面板變成「半透明圓角底 + 外框」，回傳那個外框。</summary>
        Image Dress(RectTransform panel, Color fill, Color border, int radius = SlotRadius)
        {
            var img = panel.GetComponent<Image>();
            if (img != null)
            {
                img.color = fill;
                SVShapes.ApplyRounded(img, radius);
            }

            var frame = NewImage("Frame", panel, border).GetComponent<Image>();
            Stretch((RectTransform)frame.transform, 0f);
            frame.raycastTarget = false;
            SVShapes.ApplyRoundedRing(frame, radius, 2);
            return frame;
        }

        /// <summary>圖示格。還沒有圖也會留一個淡淡的框，之後補圖直接就位。</summary>
        Image NewIconSlot(RectTransform parent, float size, Vector2 pos, string icon)
        {
            var box = NewImage("IconBox", parent, new Color(1f, 1f, 1f, 0.06f));
            Anchor(box, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            box.sizeDelta = new Vector2(size, size);
            box.anchoredPosition = pos;
            SVShapes.ApplyRounded(box.GetComponent<Image>(), 8);
            box.GetComponent<Image>().raycastTarget = false;

            var edge = NewImage("Edge", box, new Color(1f, 1f, 1f, 0.16f)).GetComponent<Image>();
            Stretch((RectTransform)edge.transform, 0f);
            edge.raycastTarget = false;
            SVShapes.ApplyRoundedRing(edge, 8, 1);

            var img = NewImage("Icon", box, Color.white).GetComponent<Image>();
            Stretch((RectTransform)img.transform, 4f);
            img.raycastTarget = false;
            img.preserveAspect = true;

            var sprite = SVArt.SkillIcon(icon);
            img.sprite = sprite;
            img.enabled = sprite != null;
            return img;
        }

        void BuildChoicePanel()
        {
            _choicePanel = NewImage("Choices", transform, new Color(0f, 0f, 0f, 0.78f));
            Stretch(_choicePanel, 0f);

            var title = NewText("Title", _choicePanel, 34, TextAnchor.UpperCenter);
            Anchor((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            ((RectTransform)title.transform).sizeDelta = new Vector2(960f, 52f);
            ((RectTransform)title.transform).anchoredPosition = new Vector2(0f, -120f);
            title.text = "你獲得了新的能量";
            _choiceTitle = title;
            title.color = new Color(1f, 0.88f, 0.5f);



            const float cardW = 300f, cardH = 398f, gap = 24f;

            for (int i = 0; i < 3; i++)
            {
                int index = i;

                var card = NewImage("Card" + i, _choicePanel, SlotFill);
                Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                card.sizeDelta = new Vector2(cardW, cardH);
                card.anchoredPosition = new Vector2((i - 1) * (cardW + gap), -10f);
                card.GetComponent<Image>().raycastTarget = true;

                var frame = Dress(card, SlotFill, SlotBorder, 16);
                _choiceFrames.Add(frame);

                // 技能圖示：現在是空框，之後把圖放進 Resources/Survivors/Icon/ 就會出現
                _choiceIcons.Add(NewIconSlot(card, 72f, new Vector2(0f, -14f), null));

                // 這個強化是給誰的：圓形小人頭，貼在卡片下緣的框上面
                var headBox = NewImage("OwnerHead", card, new Color(1f, 1f, 1f, 0f));
                Anchor(headBox, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
                headBox.sizeDelta = new Vector2(52f, 52f);
                headBox.anchoredPosition = new Vector2(0f, 34f);

                var head = headBox.GetComponent<Image>();
                head.preserveAspect = true;
                head.raycastTarget = false;
                _choiceHeads.Add(head);

                var headName = NewText("OwnerName", card, 15, TextAnchor.LowerCenter);
                Anchor((RectTransform)headName.transform,
                       new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
                ((RectTransform)headName.transform).sizeDelta = new Vector2(cardW - 20f, 20f);
                ((RectTransform)headName.transform).anchoredPosition = new Vector2(0f, 12f);
                headName.color = new Color(1f, 1f, 1f, 0.65f);
                _choiceHeadNames.Add(headName);

                var num = NewText("Num", card, 22, TextAnchor.UpperLeft);
                Anchor((RectTransform)num.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                ((RectTransform)num.transform).sizeDelta = new Vector2(40f, 30f);
                ((RectTransform)num.transform).anchoredPosition = new Vector2(14f, -10f);
                num.text = (i + 1).ToString();
                num.color = new Color(1f, 1f, 1f, 0.45f);

                var name = NewText("Name", card, 24, TextAnchor.MiddleCenter);
                Anchor((RectTransform)name.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
                ((RectTransform)name.transform).sizeDelta = new Vector2(-24f, 66f);
                ((RectTransform)name.transform).anchoredPosition = new Vector2(0f, -92f);
                _choiceTitles.Add(name);

                var desc = NewText("Desc", card, 18, TextAnchor.UpperCenter);
                Anchor((RectTransform)desc.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
                ((RectTransform)desc.transform).sizeDelta = new Vector2(-36f, 164f);
                ((RectTransform)desc.transform).anchoredPosition = new Vector2(0f, -162f);
                desc.horizontalOverflow = HorizontalWrapMode.Wrap;
                _choiceDescs.Add(desc);

                var button = card.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.ColorTint;
                button.targetGraphic = card.GetComponent<Image>();
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
                button.colors = colors;
                button.onClick.AddListener(() => SVGame.Instance?.Choose(index));

                _choiceCards.Add(card);
            }

            var hint = NewText("Hint", _choicePanel, 18, TextAnchor.LowerCenter);
            Anchor((RectTransform)hint.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            ((RectTransform)hint.transform).sizeDelta = new Vector2(600f, 30f);
            ((RectTransform)hint.transform).anchoredPosition = new Vector2(0f, 110f);
            hint.text = "點一下，或按 1 / 2 / 3";
            hint.color = new Color(1f, 1f, 1f, 0.55f);

            _choicePanel.gameObject.SetActive(false);
        }

        /// <summary>uGUI 的按鈕要有 EventSystem 才收得到滑鼠。</summary>
        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<RMZS.GodotPort.InputSystem.UI.InputSystemUIInputModule>();
            try { module.AssignDefaultActions(); }
            catch (System.Exception e) { Debug.LogWarning($"[倖存者] EventSystem 預設輸入設定失敗：{e.Message}"); }
        }

        void UpdateChoicePanel(SVGame game)
        {
            if (!game.Choosing)
            {
                if (_choicePanel.gameObject.activeSelf) _choicePanel.gameObject.SetActive(false);
                _shownChoices = null;
                return;
            }

            _choicePanel.gameObject.SetActive(true);
            if (ReferenceEquals(_shownChoices, game.Choices)) return;
            _shownChoices = game.Choices;

            if (_choiceTitle != null)
            {
                if (game.ChoosingBlessing)
                {
                    _choiceTitle.text = $"公會的祝福　選一個（還可選 {game.BlessingsLeft} 個）";
                    _choiceTitle.color = new Color(0.7f, 0.95f, 1f);
                }
                else
                {
                    // 卡片是撿來的，跟升級的能量分開講
                    _choiceTitle.text = game.ChoosingAwakening ? "大　覺　醒　　選一個"
                                      : game.ChoosingCard      ? "你獲得額外的補給"
                                                               : "你獲得了新的能量";
                    _choiceTitle.color = game.ChoosingAwakening ? new Color(1f, 0.6f, 0.88f)
                                       : game.ChoosingCard      ? new Color(0.85f, 0.7f, 1f)
                                                                : new Color(1f, 0.88f, 0.5f);
                }
            }

            var choices = game.Choices;
            for (int i = 0; i < _choiceCards.Count; i++)
            {
                bool has = i < choices.Count;
                _choiceCards[i].gameObject.SetActive(has);
                if (!has) continue;

                var u = choices[i];
                int stacks = game.Stacks(u.Id);

                _choiceTitles[i].text = stacks > 0 ? $"{u.Name}\n<size=16>強化 {stacks} → {stacks + 1} 階</size>" : u.Name;
                _choiceTitles[i].supportRichText = true;
                _choiceTitles[i].color = u.Tint;
                _choiceDescs[i].supportRichText = true;
                _choiceDescs[i].text = SVUpgradePreview.Describe(u, game)
                    + (u.ActiveSkill && stacks == 0 ? "\n<size=17><color=#FFD98A>首次取得・主動技能</color></size>" : "");
                bool blessing = game.ChoosingBlessing;
                bool awakening = u.Category == SVUpgradeCategory.Awaken;
                _choiceCards[i].GetComponent<Image>().color = blessing
                    ? new Color(0.06f, 0.18f, 0.16f, 0.96f)
                    : awakening ? new Color(0.18f, 0.10f, 0.25f, 0.96f) : SlotFill;
                _choiceFrames[i].color = blessing ? new Color(0.51f, 0.85f, 0.74f)
                    : awakening ? new Color(0.95f, 0.80f, 0.44f)
                    : new Color(u.Tint.r, u.Tint.g, u.Tint.b, 0.9f);

                // 圖示：有放圖就顯示，還沒放就留空框
                var icon = SVArt.SkillIcon(u.Icon);
                _choiceIcons[i].sprite = icon;
                _choiceIcons[i].enabled = icon != null;
            }

            UpdateChoiceOwners(game, choices);
        }

        /// <summary>
        /// 每張卡各自標「這個強化是給誰的」。
        /// SVUpgrade 沒填 OwnerSheet 就是男主自己。
        /// </summary>
        void UpdateChoiceOwners(SVGame game, List<SVUpgrade> choices)
        {
            for (int i = 0; i < _choiceHeads.Count; i++)
            {
                if (i >= choices.Count) continue;

                var u = choices[i];
                string sheet = u.OwnerSheet;
                int index = u.OwnerIndex;
                string name;

                if (string.IsNullOrEmpty(sheet))
                {
                    var p = game.Player;
                    sheet = p != null ? p.SheetName : "";
                    index = p != null ? p.SheetIndex : 0;
                    name = "男主";
                }
                else
                {
                    name = sheet;
                    foreach (var c in game.AllCompanions)
                        if (c != null && c.Type != null &&
                            c.Type.SheetName == sheet && c.Type.SheetIndex == index)
                        { name = c.Type.Name; break; }
                }

                var face = string.IsNullOrEmpty(sheet)
                    ? null : SVPortrait.Get(sheet, index, new Color(u.Tint.r, u.Tint.g, u.Tint.b, 1f));

                _choiceHeads[i].sprite = face;
                _choiceHeads[i].enabled = face != null;
                _choiceHeadNames[i].text = name;
            }
        }

        void BuildOverPanel()
        {
            _overPanel = NewImage("Over", transform, new Color(0f, 0f, 0f, 0.72f));
            Stretch(_overPanel, 0f);

            _overText = NewText("OverText", _overPanel, 40, TextAnchor.MiddleCenter);
            _overText.supportRichText = true;
            Stretch((RectTransform)_overText.transform, 0f);
            _overPanel.gameObject.SetActive(false);
        }

        /// <summary>開新的一局時重建下面那兩排。</summary>
        public void Rebuild()
        {
            var game = SVGame.Instance;
            if (game == null) return;

            // 隊友的藍條在 SVCharacterPanel 那邊（貼在各自頭像右邊），它自己會重建
            RebuildPlayerSkills(game);
        }

        void BuildLootAndRetreat()
        {
            _lootText = NewText("Loot", transform, 19, TextAnchor.LowerRight);
            var lr = (RectTransform)_lootText.transform;
            Anchor(lr, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            lr.sizeDelta = new Vector2(320f, 60f);
            lr.anchoredPosition = new Vector2(-24f, 84f);

            _retreatButton = NewImage("Retreat", transform, new Color(0.14f, 0.10f, 0.08f, 0.9f));
            Anchor(_retreatButton, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            _retreatButton.sizeDelta = new Vector2(196f, 54f);
            _retreatButton.anchoredPosition = new Vector2(-24f, 20f);
            _retreatButton.GetComponent<Image>().raycastTarget = true;

            var frame = NewImage("Frame", _retreatButton, new Color(0.85f, 0.6f, 0.35f, 0.9f)).GetComponent<Image>();
            Stretch((RectTransform)frame.transform, -2f);
            frame.raycastTarget = false;
            frame.transform.SetAsFirstSibling();

            // 長按進度：從左往右填滿才會真的撤退
            _retreatFill = NewImage("Fill", _retreatButton, new Color(1f, 0.7f, 0.35f, 0.5f)).GetComponent<Image>();
            Stretch((RectTransform)_retreatFill.transform, 2f);
            _retreatFill.type = Image.Type.Filled;
            _retreatFill.fillMethod = Image.FillMethod.Horizontal;
            _retreatFill.fillAmount = 0f;
            _retreatFill.raycastTarget = false;

            _retreatLabel = NewText("Label", _retreatButton, 17, TextAnchor.MiddleCenter);
            Stretch((RectTransform)_retreatLabel.transform, 0f);
            // C# 的一般字串不能跨行，換行要寫成 \n
            _retreatLabel.text = "返回公會\n<size=13>按住不放 / Backspace</size>";
            _retreatLabel.supportRichText = true;

            var hold = _retreatButton.gameObject.AddComponent<SVHoldButton>();
            hold.OnHold = down => SVGame.Instance?.SetRetreatButton(down);
        }

        /// <summary>大覺按鈕。拿到新的大覺才會重建。</summary>
        void RebuildAwakenings(SVGame game)
        {
            foreach (Transform child in new List<Transform>(_awakenRow)) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            _awakenFills.Clear();
            _awakenLabels.Clear();
            _awakenFrames.Clear();
            _awakenVersion = game.Awakenings.Version;

            var skills = game.Awakenings.Skills;
            const float w = 132f;
            _awakenRow.sizeDelta = new Vector2(w * Mathf.Max(1, skills.Count), 58f);

            string[] keys = { RMZ.RMZKeyBindings.Label("awaken1"), RMZ.RMZKeyBindings.Label("awaken2"), RMZ.RMZKeyBindings.Label("awaken3"), "-" };

            for (int i = 0; i < skills.Count; i++)
            {
                int index = i;
                var skill = skills[i];

                var slot = NewImage("ASlot" + i, _awakenRow, SlotFill);
                Anchor(slot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
                slot.sizeDelta = new Vector2(w - 8f, 52f);
                slot.anchoredPosition = new Vector2(w * i + w * 0.5f, 0f);

                var frame = Dress(slot, SlotFill, new Color(1f, 0.55f, 0.85f, 0.95f), 10);
                _awakenFrames.Add(frame);

                var fill = NewImage("Fill", slot, new Color(1f, 0.6f, 0.9f, 0.32f)).GetComponent<Image>();
                Stretch((RectTransform)fill.transform, 2f);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.raycastTarget = false;
                SVShapes.ApplyRounded(fill, 10);
                _awakenFills.Add(fill);

                NewIconSlot(slot, 30f, new Vector2(-(w - 8f) * 0.5f + 22f, -11f), skill.Icon);

                var label = NewText("Label", slot, 15, TextAnchor.MiddleCenter);
                Stretch((RectTransform)label.transform, 0f);
                ((RectTransform)label.transform).offsetMin = new Vector2(38f, 0f);
                label.text = $"{(index < keys.Length ? keys[index] : "-")}. {skill.Name}";
                _awakenLabels.Add(label);

                var button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = slot.GetComponent<Image>();
                var awakenColors = button.colors;
                awakenColors.highlightedColor = new Color(1.6f, 1.6f, 1.6f);
                awakenColors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
                button.colors = awakenColors;
                button.onClick.AddListener(() => SVGame.Instance?.TriggerAwakening(index));
            }
        }

        /// <summary>技能格的邊長。</summary>
        const float PlayerSlotSize = 62f;

        /// <summary>大覺那一排離畫面下緣多高。男主的技能排在它下面。</summary>
        const float AwakenRowY = 122f;
        const float PlayerRowY = 48f;

        /// <summary>男主的技能按鈕。學到新技能才會重建。</summary>
        void RebuildPlayerSkills(SVGame game)
        {
            foreach (Transform child in new List<Transform>(_playerRow)) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            _playerFills.Clear();
            _playerLabels.Clear();
            _playerButtons.Clear();

            if (game.Player == null) return;
            _playerSkillsVersion = game.Player.SkillsVersion;

            var skills = game.Player.Skills;
            const float step = PlayerSlotSize + 6f;

            // 由左往右排，整排水平置中 —— 跟上面的大覺那排對齊
            _playerRow.sizeDelta = new Vector2(step * Mathf.Max(1, skills.Count), PlayerSlotSize);

            for (int i = 0; i < skills.Count; i++)
            {
                int index = i;
                var skill = skills[i];

                var slot = NewImage("PSlot" + i, _playerRow, SlotFill);
                Anchor(slot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
                slot.sizeDelta = new Vector2(PlayerSlotSize, PlayerSlotSize);
                slot.anchoredPosition = new Vector2(step * i + step * 0.5f, 0f);

                // 框的顏色分物理／魔法，一眼看得出這招吃物攻還是魔攻
                Dress(slot, SlotFill,
                      skill.DamageKind == SVDamageKind.Magic
                          ? new Color(0.45f, 0.68f, 1f, 0.95f)
                          : new Color(0.85f, 0.72f, 0.38f, 0.95f), 10);

                // 冷卻覆蓋：由下往上填回來，滿了就是可以放
                var fill = NewImage("Fill", slot, new Color(1f, 0.82f, 0.4f, 0.32f)).GetComponent<Image>();
                Stretch((RectTransform)fill.transform, 2f);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.raycastTarget = false;
                SVShapes.ApplyRounded(fill, 10);
                _playerFills.Add(fill);

                // 圖示格：佔掉大半個格子，之後補圖直接就位
                NewIconSlot(slot, 38f, new Vector2(0f, -6f), skill.Icon);

                // 左上角的按鍵號碼
                var key = NewText("Key", slot, 14, TextAnchor.UpperLeft);
                Stretch((RectTransform)key.transform, 4f);
                key.text = (index + 1).ToString();
                key.color = new Color(1f, 1f, 1f, 0.75f);

                // 名字與等級放在格子下緣，字小一點
                var label = NewText("Label", slot, 12, TextAnchor.LowerCenter);
                Stretch((RectTransform)label.transform, 2f);
                label.text = $"{skill.Name}\nLv.{skill.Level}";
                _playerLabels.Add(label);

                var button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = slot.GetComponent<Image>();
                var playerColors = button.colors;
                playerColors.highlightedColor = new Color(1.6f, 1.6f, 1.6f);
                playerColors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
                button.colors = playerColors;
                button.onClick.AddListener(() => SVGame.Instance?.Player?.TryCast(index));
                _playerButtons.Add(button);
            }
        }

        // ---------------- 每幀更新 ----------------

        void LateUpdate()
        {
            var game = SVGame.Instance;
            if (game == null || game.Player == null) return;

            var p = game.Player;

            UpdateHurtVignette(game);
            UpdateStatsBox(game);
            UpdateStatTip();
            UpdateEndBanner(game);

            _hpFill.fillAmount = p.MaxHp > 0 ? (float)p.Hp / p.MaxHp : 0f;

            // 護盾疊在血條上，用比較亮的藍，數字也一起顯示
            if (_shieldFill != null)
            {
                bool hasShield = p.Shield > 0;
                if (_shieldFill.enabled != hasShield) _shieldFill.enabled = hasShield;
                if (hasShield)
                    _shieldFill.fillAmount = p.MaxHp > 0
                        ? Mathf.Clamp01((float)p.Shield / p.MaxHp) : 0f;
            }

            _hpText.text = p.Shield > 0
                ? $"{p.Hp} / {p.MaxHp}　+{p.Shield}"
                : $"{p.Hp} / {p.MaxHp}";
            _xpFill.fillAmount = p.XpToNext > 0 ? (float)p.Xp / p.XpToNext : 0f;
            _levelText.text = $"Lv.{p.Level}";

            int total = Mathf.FloorToInt(game.Elapsed);
            if (game.TwoStageMode)
            {
                // 兩關制不是撐時間過關，是打死王才過關，倒數在這裡沒意義——
                // 照樣往上數，第一關快出王的時候另外用文字提示就好。
                _timerText.text = $"{total / 60:00}:{total % 60:00}";
            }
            else if (game.SurviveSeconds > 0f)
            {
                // 有委託的時候倒數，玩家才知道還要撐多久
                int left = Mathf.Max(0, Mathf.CeilToInt(game.SurviveSeconds - game.Elapsed));
                _timerText.text = $"{left / 60:00}:{left % 60:00}";
            }
            else _timerText.text = $"{total / 60:00}:{total % 60:00}";

            // 打王階段改成顯示「還剩多久被迫撤退」的倒數，
            // 剩不到一分鐘轉紅色，讓人知道要收了
            if (game.BossPhase && game.BossTimeLeft >= 0f)
            {
                int left = Mathf.CeilToInt(game.BossTimeLeft);
                _timerText.text = $"討伐 {left / 60:00}:{left % 60:00}";
                _timerText.color = left <= 60
                    ? new Color(1f, 0.45f, 0.4f)
                    : new Color(1f, 0.85f, 0.55f);
            }
            else if (game.BossPhase) _timerText.text = "討伐目標";
            else if (game.TwoStageMode && game.RunStage == 1) _timerText.text += "　尋找王";

            _killText.text = $"擊殺 {game.Kills}　第 {game.Stage + 1} 階";
            if(game.IsTutorial) { _timerText.text = ""; }

            UpdateBossBar(game);

            // 隊友的藍條在 SVCharacterPanel 那邊自己更新（貼在各自頭像右邊）

            // 學到新技能就重排按鈕
            if (p.SkillsVersion != _playerSkillsVersion) RebuildPlayerSkills(game);

            for (int i = 0; i < _playerFills.Count && i < p.Skills.Count; i++)
            {
                var skill = p.Skills[i];
                _playerFills[i].fillAmount = skill.CooldownNormalized;
                _playerLabels[i].color = skill.CooldownLeft <= 0f
                    ? new Color(1f, 0.92f, 0.65f) : new Color(0.7f, 0.7f, 0.72f);
                _playerLabels[i].text = $"{RMZ.RMZKeyBindings.Label("skill" + (i + 1))}. {skill.Name}\nLv.{skill.Level}";
            }

            // ---- 大覺 ----
            if (game.Awakenings.Version != _awakenVersion) RebuildAwakenings(game);

            var awakens = game.Awakenings.Skills;
            for (int i = 0; i < _awakenFills.Count && i < awakens.Count; i++)
            {
                var a = awakens[i];

                if (a.IsActive)
                {
                    // 發動中：整格填滿並閃動，一眼看得出來在爆發
                    _awakenFills[i].fillAmount = 1f;
                    float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f);
                    _awakenFills[i].color = new Color(1f, 0.75f, 0.95f, Mathf.Lerp(0.45f, 0.85f, k));
                    _awakenFrames[i].color = new Color(1f, 0.85f, 1f, 1f);
                    _awakenLabels[i].color = Color.white;
                    _awakenLabels[i].text = $"{a.Name}\n<size=13>{a.ActiveLeft:0.0}s</size>";
                }
                else
                {
                    _awakenFills[i].fillAmount = a.CooldownNormalized;
                    _awakenFills[i].color = new Color(1f, 0.6f, 0.9f, 0.35f);
                    _awakenFrames[i].color = a.Ready
                        ? new Color(1f, 0.55f, 0.85f, 0.95f)
                        : new Color(0.5f, 0.35f, 0.45f, 0.7f);
                    _awakenLabels[i].color = a.Ready ? new Color(1f, 0.9f, 1f) : new Color(0.65f, 0.6f, 0.65f);

                    string key = i < 3 ? RMZ.RMZKeyBindings.Label("awaken" + (i + 1)) : "-";
                    _awakenLabels[i].text = a.Ready
                        ? $"{key}. {a.Name}"
                        : $"{key}. {a.Name}\n<size=13>{a.CooldownLeft:0.0}s</size>";
                }
                _awakenLabels[i].supportRichText = true;
            }

            // ---- 補給品提示 ----
            string banner = game.PickupBanner;
            bool showBanner = !string.IsNullOrEmpty(banner) && !game.Choosing;
            if (_pickupBanner.gameObject.activeSelf != showBanner)
                _pickupBanner.gameObject.SetActive(showBanner);

            if (showBanner)
            {
                // 最後半秒淡出
                float a = Mathf.Clamp01(game.PickupBannerLeft / 0.5f);
                var c = game.PickupBannerColor;
                _pickupBanner.color = new Color(c.r, c.g, c.b, a);
                _pickupBanner.text = banner;
            }

            // ---- 暫停鈕 ----
            // 教學不給暫停：鈕直接藏起來，SVGame.TogglePause 那邊也擋掉了，雙重保險。
            if (_pauseButton.gameObject.activeSelf == game.IsTutorial)
                _pauseButton.gameObject.SetActive(!game.IsTutorial);

            bool paused = game.ManualPaused;
            _pauseLabel.text = paused ? "\u25B6" : "II";
            // 暫停中：繼續鈕亮起來、一閃一閃，提示「點這裡繼續」
            float glow = paused ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f) : 0f;
            _pauseLabel.color = paused ? Color.Lerp(new Color(1f, 0.88f, 0.5f), Color.white, glow) : Color.white;
            _pauseFrame.color = paused
                ? Color.Lerp(new Color(1f, 0.78f, 0.3f, 1f), new Color(1f, 0.97f, 0.75f, 1f), glow)
                : new Color(0.85f, 0.8f, 0.6f, 0.85f);
            _pauseButton.GetComponent<Image>().color = paused
                ? Color.Lerp(new Color(0.45f, 0.33f, 0.12f, 0.95f), new Color(0.7f, 0.52f, 0.2f, 0.95f), glow)
                : new Color(0.12f, 0.11f, 0.14f, 0.85f);
            _pauseButton.localScale = Vector3.one * (paused ? 1.08f + 0.06f * glow : 1f);

            // ---- 戰利品與撤退 ----
            var loot = game.Loot;
            float flash = Mathf.Clamp01(game.LootFlash / 0.6f);
            _lootText.color = Color.Lerp(new Color(0.9f, 0.88f, 0.8f), new Color(1f, 0.9f, 0.4f), flash);
            // 力量藥水：畫面上方紅色大字一閃一閃＋倒數，最後 3 秒閃得更快
            if (game.AttackBuffLeft > 0f && !game.IsTutorial)
            {
                float speed = game.AttackBuffLeft < 3f ? 14f : 6f;
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
                _powerText.text = $"力量爆發！攻擊 +{(game.AttackBuffMultiplier - 1f) * 100f:0}%　{game.AttackBuffLeft:0.0}s";
                _powerText.color = Color.Lerp(new Color(1f, 0.3f, 0.2f), new Color(1f, 0.85f, 0.4f), k);
            }
            else if (_powerText.text != "") _powerText.text = "";

            string buff = game.AttackBuffLeft > 0f
                ? $"　攻擊 +{(game.AttackBuffMultiplier - 1f) * 100f:0}%（{game.AttackBuffLeft:0.0}s）"
                : "";
            _lootText.text = game.IsTutorial ? "" : $"戰利品　{loot.Gold} G　物品 {loot.TotalPieces}{buff}";

            _retreatFill.fillAmount = game.RetreatProgress;
            _retreatButton.gameObject.SetActive(!game.IsOver);

            UpdateChoicePanel(game);

            if (game.IsOver)
            {
                _overPanel.gameObject.SetActive(true);
                _overText.text = BuildResultText(game, p, total);
            }
            // 「暫停」這塊暗屏只該在玩家自己按暫停鈕時出現。game.Paused 是廣義的暫停
            // （露娜教學說話、Boss 登場、選強化…全都算），拿它來擋這個文字疊層的話，
            // 每次露娜講話畫面都會被這片暗屏和「按 Esc 繼續」蓋住——這正是露娜台詞
            // 看起來被暗屏遮住的原因。改成只看 ManualPaused，其餘暫停狀態不會再誤觸這塊。
            else if (game.ManualPaused)
            {
                _overPanel.gameObject.SetActive(true);
                _overText.text = "暫停中\n\n點擊右上角繼續探索";
                // 繼續鈕要蓋在暗屏上面，才看得到它在閃、也點得到
                if (_pauseButton.parent != null && _pauseButton.parent.GetChild(_pauseButton.parent.childCount - 1) != (Transform)_pauseButton) _pauseButton.SetAsLastSibling();
            }
            else
            {
                _overPanel.gameObject.SetActive(false);
            }
        }

        /// <summary>結算畫面：結果與成績（戰利品在下一個畫面呈現）。</summary>
        static string BuildResultText(SVGame game, SVPlayer p, int total)
        {
            string head;
            string note;

            if(game.IsTutorial && game.Cleared) { head = "教學完成"; note = "露娜另贈樺木裝備全套與 1000G，連同戰利品返回公會領取"; }
            else if (game.Cleared && !game.Retreated)
            {
                head = "委託完成";
                note = game.JoinedOtherTeam ? "加入隊伍，分得戰利品的 30%" : "戰利品全部帶回公會";
            }
            else if (game.Retreated)
            {
                head = "撤退";
                note = $"活著走出來，戰利品帶回 {Mathf.RoundToInt(game.RetreatKeepRatio * 100f)}%";
            }
            else
            {
                head = "倒下了";
                note = $"被抬回公會，戰利品只剩 {Mathf.RoundToInt(game.DefeatKeepRatio * 100f)}%";
            }

            string tail = game.LaunchedFromStory
                ? "按 R / Enter / 空白鍵　回公會"
                : "按 R / Enter / 空白鍵　再來一次";

            return $"{head}\n<size=20>{note}</size>\n\n" +
                   $"撐了 {total / 60:00}:{total % 60:00}　擊殺 {game.Kills}　Lv.{p.Level}\n\n{tail}";
        }

        void UpdateBossBar(SVGame game)
        {
            var boss = game.FinalBoss;
            bool show = game.BossPhase && boss != null && !boss.IsDead;

            if (_bossBar.gameObject.activeSelf != show) _bossBar.gameObject.SetActive(show);
            if (!show) return;

            _bossFill.fillAmount = boss.MaxHp > 0 ? (float)boss.Hp / boss.MaxHp : 0f;
            _bossName.text = $"{boss.DisplayName}　{boss.Hp} / {boss.MaxHp}";
        }

        // ---------------- 小工具 ----------------

        RectTransform NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            // 一定要有 sprite。Image 的 sprite 是空的時候，Unity 會畫一整塊實心矩形，
            // type = Filled / fillAmount 會被完全忽略 —— 血條、王的血條、經驗條、
            // 技能冷卻、撤退長按條之所以「永遠是滿的」就是這個原因。
            img.sprite = SVShapes.Square();

            return (RectTransform)go.transform;
        }

        // ---------------- 最左下角：男主能力值（常駐，參考 LOL 的能力面板） ----------------
        // 2 欄 × 6 列，每格「圖示＋數字」。左欄偏攻擊、右欄偏防禦／機動。
        // 左邊角色頭像欄會往上讓出這塊（SVCharacterPanel 的 ColumnBottom）。
        // 每 0.25 秒更新一次就好，數字不需要每幀重排。
        public const float StatsPanelWidth = 176f;
        public const float StatsPanelHeight = 218f;   // 10 列
        public const float StatsPanelMargin = 8f;

        static readonly string[] StatKeys =
        {
            "dmgbonus", "bossdmg",
            "atkbonus", "skilldmg",
            "phys", "magic",
            "aspd", "interval",
            "def", "mdef",
            "crit", "critdmg",
            "lifesteal", "omnisteal",
            "heal", "cdr",
            "speed", "range",
            "shots", "pierce",
        };

        // 滑鼠移到（或手指長按）某一格時，在面板上方顯示這個圖示代表什麼
        static readonly string[] StatTips =
        {
            "通用傷害：所有傷害都會提升", "BOSS傷害：對 BOSS 的傷害",
            "普攻傷害：普通攻擊的傷害", "技能傷害：主動技能的傷害",
            "物理攻擊（含目前的增益）", "魔法攻擊（含目前的增益）",
            "攻擊速度：每秒普攻幾次", "攻擊間隔：兩次普攻隔幾秒",
            "物理防禦", "魔法防禦",
            "暴擊率", "暴擊傷害：暴擊時額外多打幾 %",
            "普攻吸血：普通攻擊的傷害轉為生命", "全能吸血：普攻、技能、毒、流血的傷害轉為生命",
            "回復效果：回血量的倍率", "技能冷卻縮減",
            "移動速度", "攻擊範圍",
            "普攻發數：每次攻擊射出幾發", "穿透：普攻可以貫穿幾個敵人",
        };

        readonly List<Text> _statValues = new List<Text>();
        readonly List<RectTransform> _statCells = new List<RectTransform>();
        RectTransform _statTip;
        Text _statTipText;
        float _statsTimer;

        void BuildStatsBox()
        {
            _statValues.Clear();
            _statCells.Clear();

            var box = NewImage("StatsBox", transform, new Color(0f, 0f, 0f, 0.5f));
            Anchor(box, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            box.sizeDelta = new Vector2(StatsPanelWidth, StatsPanelHeight);
            box.anchoredPosition = new Vector2(StatsPanelMargin, StatsPanelMargin);
            box.GetComponent<Image>().raycastTarget = true;   // 擋住滑鼠，點面板時男主不會走過去
            Dress(box, new Color(0.06f, 0.07f, 0.10f, 0.82f), new Color(0.72f, 0.60f, 0.34f, 0.85f), 8);

            const float padX = 10f, padY = 9f, colW = 80f, rowH = 20f, icon = 16f;
            for (int i = 0; i < StatKeys.Length; i++)
            {
                int col = i % 2, row = i / 2;
                float x = padX + col * colW, y = padY + row * rowH;

                var ic = NewImage("Icon_" + StatKeys[i], box, Color.white);
                Anchor(ic, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                ic.sizeDelta = new Vector2(icon, icon);
                ic.anchoredPosition = new Vector2(x, -y - 2f);
                var img = ic.GetComponent<Image>();
                img.sprite = SVPixelArt.StatIcon(StatKeys[i]);
                img.raycastTarget = false;

                var val = NewText("Value_" + StatKeys[i], box, 15, TextAnchor.MiddleLeft);
                var rt = (RectTransform)val.transform;
                Anchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                rt.sizeDelta = new Vector2(colW - icon - 8f, rowH);
                rt.anchoredPosition = new Vector2(x + icon + 5f, -y);
                _statValues.Add(val);

                // 看不見的感應格（圖示＋數字），用來判斷滑鼠有沒有停在這格
                var cell = NewImage("Cell_" + StatKeys[i], box, new Color(0f, 0f, 0f, 0f));
                Anchor(cell, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                cell.sizeDelta = new Vector2(colW - 2f, rowH);
                cell.anchoredPosition = new Vector2(x - 2f, -y);
                cell.GetComponent<Image>().raycastTarget = false;
                _statCells.Add(cell);
            }

            // 提示框：預設隱藏，顯示在面板正上方
            _statTip = NewImage("StatTip", transform, new Color(0f, 0f, 0f, 0.5f));
            Anchor(_statTip, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            _statTip.sizeDelta = new Vector2(240f, 28f);
            _statTip.GetComponent<Image>().raycastTarget = false;
            Dress(_statTip, new Color(0.06f, 0.07f, 0.10f, 0.92f), new Color(0.72f, 0.60f, 0.34f, 0.9f), 6);
            _statTipText = NewText("StatTipText", _statTip, 15, TextAnchor.MiddleLeft);
            var trt = (RectTransform)_statTipText.transform;
            Anchor(trt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f));
            trt.sizeDelta = new Vector2(-16f, 0f);
            trt.anchoredPosition = new Vector2(8f, 0f);
            _statTip.gameObject.SetActive(false);
        }

        // ---------------- 血條右邊：已取得的公會祝福 ----------------
        // 每個祝福一個小方塊（祝福顏色的框＋名字第一個字），滑鼠移上去或長按顯示名稱與效果。
        const float BlessRowX = 446f, BlessRowY = -19f, BlessSize = 28f, BlessGap = 4f;
        readonly List<RectTransform> _blessIcons = new List<RectTransform>();
        readonly List<string> _blessTips = new List<string>();
        int _blessShown = -1;

        void SyncBlessingRow(SVGame game)
        {
            var chosen = game.Passives.Chosen;
            if (chosen.Count == _blessShown) return;
            _blessShown = chosen.Count;

            foreach (var icon in _blessIcons) if (icon != null) { icon.gameObject.SetActive(false); Destroy(icon.gameObject); }
            _blessIcons.Clear();
            _blessTips.Clear();

            for (int i = 0; i < chosen.Count; i++)
            {
                var p = chosen[i];
                string name = StripRich(p.Name);
                var cell = NewImage("Blessing_" + i, transform, new Color(0f, 0f, 0f, 0f));
                Anchor(cell, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                cell.sizeDelta = new Vector2(BlessSize, BlessSize);
                cell.anchoredPosition = new Vector2(BlessRowX + i * (BlessSize + BlessGap), BlessRowY);
                Dress(cell, new Color(0.06f, 0.07f, 0.10f, 0.85f), new Color(p.Tint.r, p.Tint.g, p.Tint.b, 0.95f), 6);
                cell.GetComponent<Image>().raycastTarget = true;   // 點在圖示上男主不會走過去

                var letter = NewText("Letter", cell, 16, TextAnchor.MiddleCenter);
                Stretch((RectTransform)letter.transform, 0f);
                letter.text = name.Length > 0 ? name.Substring(0, 1) : "祝";
                letter.color = p.Tint;

                _blessIcons.Add(cell);
                _blessTips.Add(string.IsNullOrWhiteSpace(p.Desc) ? name : name + "：" + StripRich(p.Desc));
            }
        }

        static string StripRich(string s) =>
            string.IsNullOrEmpty(s) ? "" : System.Text.RegularExpressions.Regex.Replace(s, "<[^>]*>", "");

        // 提示框共用：能力面板用（顯示在面板上方），祝福圖示用（顯示在圖示下方）
        string _tipKey;

        void UpdateStatTip()
        {
            if (_statTip == null) return;
            var game = SVGame.Instance;
            if (game != null && game.Passives != null) SyncBlessingRow(game);

            string key = null, text = null;
            bool below = false;
            Vector2 at = Vector2.zero;
            var mouse = RMZS.GodotPort.InputSystem.Mouse.current;
            if (mouse != null)
            {
                var pos = mouse.position.ReadValue();
                for (int i = 0; i < _statCells.Count && key == null; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(_statCells[i], pos, null))
                    {
                        key = "stat" + i; text = StatTips[i];
                        at = new Vector2(StatsPanelMargin + (i % 2) * 80f, StatsPanelMargin + StatsPanelHeight + 4f);
                    }
                for (int i = 0; i < _blessIcons.Count && key == null; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(_blessIcons[i], pos, null))
                    {
                        key = "bless" + i + "/" + _blessShown; text = _blessTips[i]; below = true;
                        at = new Vector2(BlessRowX + i * (BlessSize + BlessGap), BlessRowY - BlessSize - 4f);
                    }
            }
            if (key == _tipKey) return;
            _tipKey = key;
            if (key == null) { _statTip.gameObject.SetActive(false); return; }

            _statTipText.text = text;
            _statTip.sizeDelta = new Vector2(24f + text.Length * 15f, 28f);
            var corner = below ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
            Anchor(_statTip, corner, corner, corner);
            _statTip.anchoredPosition = at;
            _statTip.SetAsLastSibling();
            _statTip.gameObject.SetActive(true);
        }

        void UpdateStatsBox(SVGame game)
        {
            if (_statValues.Count != StatKeys.Length) return;
            _statsTimer -= Time.unscaledDeltaTime;
            if (_statsTimer > 0f) return;
            _statsTimer = 0.1f;   // 喝藥水、大覺發動這類變化要馬上看得到

            var p = game.Player;

            // 目前真的打得出來的攻擊：大覺倍率 × 力量藥水 × 祝福的條件加成（跟 SVEnemy.TakeDamage 同一套）
            // 物攻／魔攻顯示「乘上通用傷害」後的值（普攻、技能、BOSS 另外看上面四格）
            float phys = p.PhysicalPower * (1f + SVDamageBonus.General(game, SVDamageKind.Physical));
            float magic = p.MagicPower * (1f + SVDamageBonus.General(game, SVDamageKind.Magic));

            // 目前的普攻間隔：時停、極速連擊都算進去
            float interval = SVTimeStop.Stopped ? p.AttackInterval / SVTimeStop.HasteMultiplier : p.AttackInterval;
            interval /= Mathf.Max(0.01f, game.Awakenings.AttackSpeedMultiplier);
            interval = Mathf.Max(0.02f, interval);

            float steal = SVLifesteal.Attack(game) * 100f;
            float omni = SVLifesteal.Omni(game) * 100f;
            float healMul = game.Passives.HealMul * 100f;
            float cdr = (1f - game.SkillCooldownMul * game.Passives.SkillCooldownMul) * 100f;

            // 四種傷害加成（跟 SVEnemy.TakeDamage 乘的是同一套，見 SVDamageBonus.cs）
            float genBonus = SVDamageBonus.General(game, SVDamageKind.Physical) * 100f;
            float bossBonus = SVDamageBonus.Boss(game) * 100f;
            float atkBonus = SVDamageBonus.Attack(game) * 100f;
            float skillBonus = SVDamageBonus.Skill(game) * 100f;
            string P(float x) => $"{(x >= 0f ? "+" : "")}{x:0}%";

            string[] v =
            {
                P(genBonus),                                 P(bossBonus),
                P(atkBonus),                                 P(skillBonus),
                $"{Mathf.RoundToInt(phys)}",                 $"{Mathf.RoundToInt(magic)}",
                $"{1f / interval:0.0}/秒",                    $"{interval:0.00}秒",
                $"{p.Defense}",                              $"{p.MagicDefense}",
                $"{p.CritChance * 100f:0}%",                 $"+{(p.CritMultiplier - 1f) * 100f:0}%",
                $"{steal:0}%",                               $"{omni:0}%",
                $"{healMul:0}%",                             $"{cdr:0}%",
                $"{p.MoveSpeed:0.0}",                        $"{p.AttackRange:0.#}",
                $"{p.AttackShots}",                          $"{p.AttackPierce}",
            };
            for (int i = 0; i < v.Length; i++) _statValues[i].text = v[i];
        }

        Text NewText(string name, Transform parent, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.color = new Color(0.95f, 0.94f, 0.9f);
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
        }

        static void Stretch(RectTransform rt, float padding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
