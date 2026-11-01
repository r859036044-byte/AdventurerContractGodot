// 黑貓商人的商店（原作是 VisuMZ_4_RandomizeShop 的「遙控隨機商店」）。
// 「隨機寶箱」（商店 202）改成抽卡：選寶箱 → 單抽／十連抽（九折），當場開獎，不用先買再去背包開。
//
// 商品清單照原作事件裡寫的 JS（chance_80/50/20_items、targetCount）抽：
//   ・同一天打開幾次都是同一批（用「遊戲天數＋商店編號」當亂數種子，不用另外存檔）
//   ・開關 84／81 被每日重置關掉後，下一天就會換一批
// 賣出：照原作進商店前的處理，道具 2~105、武器、防具都不能賣，其餘道具半價收購。
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Godot;

namespace RMZ;

public partial class RMZItemShop : CanvasLayer
{
    public static bool IsOpen => GodotObject.IsInstanceValid(_active);
    static RMZItemShop _active;

    /// <summary>商店編號（原作的遙控事件 ID）→ 商品 JS。CreateNewJsShop 時記下來，讀檔後找不到就從地圖資料再抓。</summary>
    static readonly Dictionary<int, string> GoodsScripts = new();

    public static void RegisterGoods(int shopId, string goodsJs) { if (!string.IsNullOrEmpty(goodsJs)) GoodsScripts[shopId] = goodsJs; }

    public static void Open(int mapId, int shopId)
    {
        if (IsOpen || RMZBootstrap.Instance == null) return;
        var goods = BuildGoods(mapId, shopId);
        bool gacha = shopId == 202;
        var node = new RMZItemShop { Layer = 60, _goods = goods, _gacha = gacha, _title = gacha ? "隨機寶箱" : "黑貓商店" };
        _active = node;
        RMZBootstrap.Instance.GetTree().Root.AddChild(node);
    }

    // ---------------- 商品 ----------------

    static List<int> BuildGoods(int mapId, int shopId)
    {
        if (!GoodsScripts.TryGetValue(shopId, out var js)) js = FindGoodsScript(mapId, shopId);
        var list = new List<int>();
        if (string.IsNullOrEmpty(js)) return list;

        int[] Arr(string name)
        {
            var m = Regex.Match(js, name + @"\s*=\s*\[([^\]]*)\]");
            if (!m.Success) return Array.Empty<int>();
            var ids = new List<int>();
            foreach (var part in m.Groups[1].Value.Split(','))
                if (int.TryParse(part.Trim(), out int id) && RMZDatabase.Item(id) != null) ids.Add(id);
            return ids.ToArray();
        }
        var c80 = Arr("chance_80_items"); var c50 = Arr("chance_50_items"); var c20 = Arr("chance_20_items");
        var tm = Regex.Match(js, @"targetCount\s*=\s*(\d+)");
        int target = tm.Success ? int.Parse(tm.Groups[1].Value) : 8;
        bool allowDup = js.Contains("all_possible_ids");

        var rng = new Random(RMZGameState.Variable(61) * 7919 + shopId);
        void Roll(int[] ids, int chance) { foreach (var id in ids) if (rng.Next(100) < chance && (allowDup || !list.Contains(id))) list.Add(id); }
        Roll(c80, 80); Roll(c50, 50); Roll(c20, 20);

        // 多退少補
        for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
        if (list.Count > target) list.RemoveRange(target, list.Count - target);
        var pool = new List<int>(); pool.AddRange(c80); pool.AddRange(c50); pool.AddRange(c20);
        if (!allowDup) pool.RemoveAll(list.Contains);
        while (list.Count < target && pool.Count > 0)
        {
            int k = rng.Next(pool.Count); list.Add(pool[k]);
            if (!allowDup) pool.RemoveAt(k);
        }
        // 重複的合併成一列（數量無限，不用分開放）
        var unique = new List<int>(); foreach (var id in list) if (!unique.Contains(id)) unique.Add(id);
        return unique;
    }

    /// <summary>從地圖的事件裡找「Create New JS Shop」的商品 JS（讀檔後第一次開店用）。</summary>
    static string FindGoodsScript(int mapId, int shopId)
    {
        var map = RMZDatabase.LoadMap(mapId);
        if (map?.events == null) return null;
        foreach (var ev in map.events)
        {
            if (ev?.pages == null) continue;
            foreach (var page in ev.pages)
            {
                if (page?.list == null) continue;
                foreach (var c in page.list)
                {
                    if (c.code != 357 || c.parameters == null || c.parameters.Count < 4) continue;
                    if (c.parameters[1]?.ToString() != "RemoteEventCreateNewJsShop") continue;
                    var args = c.parameters[3] as Newtonsoft.Json.Linq.JObject;
                    if (args == null) continue;
                    if (args["EventID:eval"]?.ToString().Trim() == shopId.ToString())
                    {
                        string raw = args["GoodsJS:func"]?.ToString() ?? "";
                        try { raw = Newtonsoft.Json.JsonConvert.DeserializeObject<string>(raw) ?? raw; } catch { }
                        GoodsScripts[shopId] = raw;
                        return raw;
                    }
                }
            }
        }
        return null;
    }

    // ---------------- 介面 ----------------

    List<int> _goods = new();
    string _title = "商店";
    bool _gacha;
    bool _sellMode;
    int _index, _qty = 1;
    Texture2D _icons;
    Control _stage;
    VBoxContainer _rows;
    Label _gold, _desc, _qtyLabel, _hint;
    Button _buyTab, _sellTab, _actBtn, _tenBtn;

    /// <summary>十連抽 = 單抽價格 × 9（打九折）。</summary>
    const int TenPriceFactor = 9;
    readonly List<Button> _rowButtons = new();
    List<int> _shown = new();
    ulong _opened;

    static readonly Color Gold = new(1f, .88f, .55f), Soft = new(.80f, .78f, .74f), Dim = new(.6f, .6f, .64f);

    static StyleBoxFlat Box(Color bg, Color border, int bw = 2, int r = 10) => new()
    {
        BgColor = bg, BorderColor = border, BorderWidthLeft = bw, BorderWidthRight = bw, BorderWidthTop = bw, BorderWidthBottom = bw,
        CornerRadiusTopLeft = r, CornerRadiusTopRight = r, CornerRadiusBottomLeft = r, CornerRadiusBottomRight = r,
        ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 4, ContentMarginBottom = 4,
    };

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always; _opened = Time.GetTicksMsec();
        var iconPath = ProjectSettings.GlobalizePath("res://rmz/img/system/IconSet.png");
        if (File.Exists(iconPath)) _icons = ImageTexture.CreateFromImage(Image.LoadFromFile(iconPath));

        var screen = new Control { MouseFilter = Control.MouseFilterEnum.Stop }; AddChild(screen); screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // 不再整片壓暗：黑貓的立繪要保持原本的亮度
        _stage = new Control { Size = new Vector2(1280, 720), MouseFilter = Control.MouseFilterEnum.Ignore }; screen.AddChild(_stage);
        void Fit() { var size = GetViewport().GetVisibleRect().Size; float s = Mathf.Min(size.X / 1280, size.Y / 720); _stage.Scale = Vector2.One * s; _stage.Position = (size - new Vector2(1280, 720) * s) / 2; }
        Fit(); screen.Resized += Fit;

        if (_gacha) { BuildGacha(); return; }

        // 右半邊留給黑貓的立繪，商店框放左邊偏中
        var panel = new Panel { Position = new Vector2(420, 60), Size = new Vector2(820, 600), MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", Box(new Color(.07f, .07f, .11f, .85f), new Color(.82f, .7f, .38f, .7f), 2, 14));
        _stage.AddChild(panel);

        Put(Label(_title, 30, Gold), 450, 74, 300, 40);
        _gold = Label("", 22, Gold); _gold.HorizontalAlignment = HorizontalAlignment.Right; Put(_gold, 900, 80, 320, 32);

        _buyTab = Tab("購買", () => SetMode(false)); _buyTab.Position = new Vector2(450, 124);
        _sellTab = Tab("賣出", () => SetMode(true)); _sellTab.Position = new Vector2(580, 124);

        var scroll = new ScrollContainer { Position = new Vector2(450, 176), Size = new Vector2(760, 300), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _stage.AddChild(scroll);
        _rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _rows.AddThemeConstantOverride("separation", 4); scroll.AddChild(_rows);

        var line = new ColorRect { Color = new Color(.82f, .7f, .38f, .4f), Position = new Vector2(450, 484), Size = new Vector2(760, 2) }; _stage.AddChild(line);
        _desc = Label("", 19, Soft); _desc.AutowrapMode = TextServer.AutowrapMode.WordSmart; Put(_desc, 450, 494, 760, 70);

        _qtyLabel = Label("1", 24, Colors.White); _qtyLabel.HorizontalAlignment = HorizontalAlignment.Center; Put(_qtyLabel, 504, 588, 60, 40);
        var minus = SmallBtn("－", () => ChangeQty(-1)); minus.Position = new Vector2(450, 586);
        var plus = SmallBtn("＋", () => ChangeQty(1)); plus.Position = new Vector2(568, 586);
        var plus10 = SmallBtn("+10", () => ChangeQty(10)); plus10.Position = new Vector2(622, 586); plus10.Size = new Vector2(64, 44);
        _actBtn = SmallBtn("購買", Act); _actBtn.Position = new Vector2(720, 586); _actBtn.Size = new Vector2(160, 44);
        _hint = Label("↑↓ 選擇　←→ 數量　Enter 確定　Esc 離開", 15, Dim);
        var close = SmallBtn("離開", Close); close.Position = new Vector2(1060, 586); close.Size = new Vector2(150, 44);
        Put(_hint, 760, 632, 450, 22); _hint.HorizontalAlignment = HorizontalAlignment.Right;

        SetMode(false);
    }

    Label Label(string text, int size, Color color)
    {
        var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        l.AddThemeFontOverride("font", RMZFonts.Main); l.AddThemeFontSizeOverride("font_size", size); l.AddThemeColorOverride("font_color", color);
        return l;
    }
    void Put(Control c, float x, float y, float w, float h) { c.Position = new Vector2(x, y); c.Size = new Vector2(w, h); _stage.AddChild(c); }

    Button Tab(string text, Action onClick)
    {
        var b = new Button { Text = text, Size = new Vector2(120, 42), FocusMode = Control.FocusModeEnum.None };
        b.AddThemeFontOverride("font", RMZFonts.Main); b.AddThemeFontSizeOverride("font_size", 21);
        b.Pressed += onClick; _stage.AddChild(b); return b;
    }
    Button SmallBtn(string text, Action onClick)
    {
        var b = new Button { Text = text, Size = new Vector2(50, 44), FocusMode = Control.FocusModeEnum.None };
        b.AddThemeFontOverride("font", RMZFonts.Main); b.AddThemeFontSizeOverride("font_size", 21);
        b.AddThemeStyleboxOverride("normal", Box(new Color(.16f, .14f, .20f), new Color(.82f, .7f, .38f, .6f)));
        b.AddThemeStyleboxOverride("hover", Box(new Color(.26f, .22f, .30f), Gold));
        b.AddThemeStyleboxOverride("pressed", Box(new Color(.12f, .10f, .14f), Gold));
        b.Pressed += onClick; _stage.AddChild(b); return b;
    }

    static int SellPrice(EquipItemData d) => d.price / 2;
    static bool Sellable(int id)
    {
        var d = RMZDatabase.Item(id);
        return d != null && id > 105 && d.price > 0 && d.itypeId != 2 && RMZParty.NumItems(id) > 0;
    }

    void SetMode(bool sell)
    {
        _sellMode = sell; _index = 0; _qty = 1;
        foreach (var (b, on) in new[] { (_buyTab, !sell), (_sellTab, sell) })
        {
            b.AddThemeStyleboxOverride("normal", Box(on ? new Color(.36f, .28f, .14f) : new Color(.14f, .13f, .17f), on ? Gold : new Color(.5f, .45f, .35f)));
            b.AddThemeColorOverride("font_color", on ? Colors.White : Dim);
        }
        _actBtn.Text = sell ? "賣出" : "購買";
        Rebuild();
    }

    void Rebuild()
    {
        if (_gacha) { RebuildGacha(); return; }
        foreach (var b in _rowButtons) b.QueueFree();
        _rowButtons.Clear();
        _shown = new List<int>();
        if (_sellMode) { foreach (var kv in RMZParty.AllItems) if (Sellable(kv.Key)) _shown.Add(kv.Key); _shown.Sort(); }
        else _shown.AddRange(_goods);

        for (int i = 0; i < _shown.Count; i++)
        {
            int index = i, id = _shown[i];
            var d = RMZDatabase.Item(id);
            var row = new Button { CustomMinimumSize = new Vector2(740, 44), FocusMode = Control.FocusModeEnum.None };
            row.Pressed += () => { if (_index == index) Act(); else { _index = index; _qty = 1; Refresh(); } };
            var h = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; h.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); h.OffsetLeft = 10; h.OffsetRight = -12; row.AddChild(h);
            h.AddThemeConstantOverride("separation", 10);
            if (_icons != null && d != null)
                h.AddChild(new TextureRect { Texture = IconOf(d.iconIndex), CustomMinimumSize = new Vector2(32, 32), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            var name = Label(ItemName(id), 21, Colors.White); name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; h.AddChild(name);
            var own = Label($"持有 {RMZParty.NumItems(id)}", 17, Dim); own.CustomMinimumSize = new Vector2(110, 0); h.AddChild(own);
            int price = d == null ? 0 : _sellMode ? SellPrice(d) : d.price;
            var pl = Label($"{price} G", 21, Gold); pl.CustomMinimumSize = new Vector2(120, 0); pl.HorizontalAlignment = HorizontalAlignment.Right; h.AddChild(pl);
            _rows.AddChild(row); _rowButtons.Add(row);
        }
        if (_shown.Count == 0)
        {
            var empty = new Button { Text = _sellMode ? "沒有可以賣的東西" : "今天沒有商品", Disabled = true, CustomMinimumSize = new Vector2(740, 44), FocusMode = Control.FocusModeEnum.None };
            empty.AddThemeFontOverride("font", RMZFonts.Main); _rows.AddChild(empty); _rowButtons.Add(empty);
        }
        _index = Mathf.Clamp(_index, 0, Math.Max(0, _shown.Count - 1));
        Refresh();
    }

    static string ItemName(int id) => RMZLocalization.Translate(RMZDatabase.Item(id)?.name ?? $"道具{id}");
    Texture2D IconOf(int iconIndex) => _icons == null ? null : new AtlasTexture { Atlas = _icons, Region = new Rect2((iconIndex % 16) * 32, (iconIndex / 16) * 32, 32, 32) };

    int MaxQty()
    {
        if (_shown.Count == 0) return 0;
        var d = RMZDatabase.Item(_shown[_index]); if (d == null) return 0;
        if (_sellMode) return RMZParty.NumItems(d.id);
        int afford = d.price > 0 ? RMZParty.Gold / d.price : 99;
        return Math.Min(99 - RMZParty.NumItems(d.id), afford);
    }

    void ChangeQty(int delta) { int max = Math.Max(1, MaxQty()); _qty = Mathf.Clamp(_qty + delta, 1, max); Refresh(); }

    void Refresh()
    {
        if (_gacha) { RefreshGacha(); return; }
        _gold.Text = $"持有金錢　{RMZParty.Gold} G";
        for (int i = 0; i < _rowButtons.Count && i < _shown.Count; i++)
        {
            bool on = i == _index;
            _rowButtons[i].AddThemeStyleboxOverride("normal", Box(on ? new Color(.30f, .24f, .12f, .95f) : new Color(.11f, .10f, .14f, .9f), on ? Gold : new Color(.35f, .32f, .28f), on ? 2 : 1, 8));
            _rowButtons[i].AddThemeStyleboxOverride("hover", Box(new Color(.24f, .20f, .12f, .95f), Gold, 2, 8));
        }
        if (_shown.Count == 0) { _desc.Text = ""; _qtyLabel.Text = "0"; _actBtn.Disabled = true; return; }
        var d = RMZDatabase.Item(_shown[_index]);
        _qty = Mathf.Clamp(_qty, 1, Math.Max(1, MaxQty()));
        int total = d == null ? 0 : (_sellMode ? SellPrice(d) : d.price) * _qty;
        _desc.Text = RMZLocalization.Translate(d?.description ?? "").Replace("\\n", "\n");
        _qtyLabel.Text = _qty.ToString();
        _actBtn.Text = (_sellMode ? "賣出" : "購買") + $"　{total} G";
        _actBtn.Disabled = MaxQty() < 1;
    }

    void Act()
    {
        if (Time.GetTicksMsec() - _opened < 200 || _shown.Count == 0 || MaxQty() < 1) { RMZAudio.Instance?.PlaySeByName("Buzzer1"); return; }
        var d = RMZDatabase.Item(_shown[_index]); if (d == null) return;
        int q = Mathf.Clamp(_qty, 1, MaxQty());
        if (_sellMode) { RMZParty.GainItem(d.id, -q); RMZParty.GainGold(SellPrice(d) * q); }
        else { RMZParty.GainGold(-d.price * q); RMZParty.GainItem(d.id, q); }
        RMZAudio.Instance?.PlaySeByName("Shop1");
        _qty = 1;
        Rebuild();
        RMZEventRunner.Instance?.RequestRefresh();
    }

    // ================= 抽卡（隨機寶箱） =================
    // 版面：上面是直立的寶箱卡，選中的那張放大置中、發光、輕輕浮動；沒選到的縮小變暗、停住不動。
    // 下面只有「單抽」「十連抽」兩顆大按鈕。
    // 按下去：黑貓換笑臉喊台詞 → 寶箱越晃越大力 → 閃光彈開 → 獎勵卡片一張張翻出來（背後有光芒）。

    const float CarCX = 410, CardW = 210, CardH = 290, CardCY = 165, CardStep = 240;
    Control _carousel;
    readonly List<Control> _gCards = new();   // 外層：管位置與大小
    readonly List<Button> _gFaces = new();    // 卡面：管浮動
    readonly List<Panel> _gGlows = new();     // 選中時外圈的光
    readonly List<int> _lastOffset = new();
    readonly List<Color> _gTheme = new();     // 每種寶箱自己的顏色

    /// <summary>每種寶箱一個顏色（看名字裡的關鍵字），卡片底色、外框、光都用它。</summary>
    static Color ChestTheme(string name)
    {
        if (name.Contains("好感")) return new Color(1f, .45f, .65f);      // 粉紅
        if (name.Contains("特定")) return new Color(.3f, .85f, .8f);      // 青綠
        if (name.Contains("武器")) return new Color(1f, .45f, .3f);       // 橘紅
        if (name.Contains("防具")) return new Color(.35f, .65f, 1f);      // 藍
        if (name.Contains("捲軸")) return new Color(.55f, .8f, 1f);       // 淺藍
        if (name.Contains("稀有-史詩")) return new Color(.8f, .5f, 1f);   // 紫
        if (name.Contains("精良-稀有")) return new Color(.4f, .7f, 1f);   // 藍
        if (name.Contains("粗製")) return new Color(.45f, .9f, .5f);      // 綠
        if (name.Contains("裝備")) return new Color(.75f, .55f, 1f);      // 紫
        if (name.Contains("道具")) return new Color(.5f, .9f, .45f);      // 綠
        return new Color(1f, .85f, .45f);                                   // 金
    }
    Label _onePrice, _tenPrice;
    Tween _carTween;
    float _time;

    void BuildGacha()
    {
        var panel = new Panel { Position = new Vector2(420, 60), Size = new Vector2(820, 600), MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", Box(new Color(.07f, .07f, .11f, .70f), new Color(.82f, .7f, .38f, .7f), 2, 14));
        _stage.AddChild(panel);

        Put(Label(_title, 30, Gold), 450, 74, 300, 40);
        _gold = Label("", 22, Gold); _gold.HorizontalAlignment = HorizontalAlignment.Right; Put(_gold, 780, 78, 330, 36);
        var close = SmallBtn("離開", Close); close.Position = new Vector2(1124, 74); close.Size = new Vector2(96, 40);

        _carousel = new Control { Position = new Vector2(420, 120), Size = new Vector2(820, 330), MouseFilter = Control.MouseFilterEnum.Ignore };
        _stage.AddChild(_carousel);

        _desc = Label("", 18, Soft); _desc.HorizontalAlignment = HorizontalAlignment.Center; _desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Put(_desc, 450, 454, 760, 62);

        (_actBtn, _onePrice) = PullButton("單抽", new Vector2(462, 528), false, () => Pull(1));
        (_tenBtn, _tenPrice) = PullButton("十連抽", new Vector2(838, 528), true, () => Pull(10));

        _hint = Label("←→ 選寶箱　Enter 單抽　Shift+Enter 十連抽　Esc 離開", 14, Dim); _hint.HorizontalAlignment = HorizontalAlignment.Center;
        Put(_hint, 450, 632, 760, 22);

        RebuildGacha();
    }

    (Button, Label) PullButton(string title, Vector2 pos, bool premium, Action onClick)
    {
        var accent = premium ? new Color(1f, .78f, .35f) : new Color(.55f, .75f, 1f);
        var fill = premium ? new Color(.30f, .21f, .08f, .96f) : new Color(.10f, .15f, .28f, .96f);
        var b = new Button { Position = pos, Size = new Vector2(340, 96), FocusMode = Control.FocusModeEnum.None };
        b.AddThemeStyleboxOverride("normal", Box(fill, accent, 2, 16));
        b.AddThemeStyleboxOverride("hover", Box(fill.Lightened(.15f), Colors.White, 3, 16));
        b.AddThemeStyleboxOverride("pressed", Box(fill.Darkened(.2f), accent, 3, 16));
        b.AddThemeStyleboxOverride("disabled", Box(new Color(.12f, .12f, .14f, .9f), new Color(.3f, .3f, .32f), 2, 16));
        b.Pressed += onClick; _stage.AddChild(b);
        var t = Label(title, 32, Colors.White); t.HorizontalAlignment = HorizontalAlignment.Center; t.Position = new Vector2(0, 8); t.Size = new Vector2(340, 46); b.AddChild(t);
        var p = Label("", 20, accent); p.HorizontalAlignment = HorizontalAlignment.Center; p.Position = new Vector2(0, 56); p.Size = new Vector2(340, 30); b.AddChild(p);
        return (b, p);
    }

    void RebuildGacha()
    {
        foreach (var c in _gCards) c.QueueFree();
        _gCards.Clear(); _gFaces.Clear(); _gGlows.Clear(); _lastOffset.Clear(); _gTheme.Clear();
        _shown = new List<int>(_goods);
        _index = Mathf.Clamp(_index, 0, Math.Max(0, _shown.Count - 1));

        for (int i = 0; i < _shown.Count; i++)
        {
            int index = i, id = _shown[i];
            var d = RMZDatabase.Item(id);
            var theme = ChestTheme(d?.name ?? "");
            var holder = new Control { Size = new Vector2(CardW, CardH), PivotOffset = new Vector2(CardW / 2, CardH / 2), MouseFilter = Control.MouseFilterEnum.Ignore };
            var glow = new Panel { Position = new Vector2(-10, -10), Size = new Vector2(CardW + 20, CardH + 20), MouseFilter = Control.MouseFilterEnum.Ignore };
            glow.AddThemeStyleboxOverride("panel", Box(new Color(theme.R, theme.G, theme.B, .18f), new Color(theme.R, theme.G, theme.B, .65f), 3, 22));
            holder.AddChild(glow);

            var face = new Button { Size = new Vector2(CardW, CardH), FocusMode = Control.FocusModeEnum.None };
            face.Pressed += () => { if (_result == null) Select(index); };
            holder.AddChild(face);
            if (d != null && _icons != null)
                face.AddChild(new TextureRect
                {
                    Texture = IconOf(d.iconIndex), Position = new Vector2((CardW - 104) / 2, 30), Size = new Vector2(104, 104),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = Control.MouseFilterEnum.Ignore,
                });
            // 上緣色帶＋寶箱前面貼一個徽章（裡面放這箱最好的獎品圖示），一眼分得出是哪種寶箱
            face.AddChild(new ColorRect { Color = new Color(theme.R, theme.G, theme.B, .85f), Position = new Vector2(16, 8), Size = new Vector2(CardW - 32, 6), MouseFilter = Control.MouseFilterEnum.Ignore });
            int sample = RMZLootBox.SampleItem(id);
            var sd = sample > 0 ? RMZDatabase.Item(sample) : null;
            if (sd != null && _icons != null)
            {
                var badge = new Panel { Position = new Vector2(CardW / 2 + 14, 96), Size = new Vector2(48, 48), MouseFilter = Control.MouseFilterEnum.Ignore };
                badge.AddThemeStyleboxOverride("panel", Box(new Color(theme.R * .45f, theme.G * .45f, theme.B * .45f, 1f), theme, 2, 24));
                badge.AddChild(new TextureRect
                {
                    Texture = IconOf(sd.iconIndex), Position = new Vector2(8, 8), Size = new Vector2(32, 32),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = Control.MouseFilterEnum.Ignore,
                });
                face.AddChild(badge);
            }
            var name = Label(ItemName(id), 21, Colors.White);
            name.HorizontalAlignment = HorizontalAlignment.Center; name.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
            name.Position = new Vector2(10, 148); name.Size = new Vector2(CardW - 20, 70); face.AddChild(name);
            face.AddChild(new ColorRect { Color = new Color(.82f, .7f, .38f, .45f), Position = new Vector2(24, 226), Size = new Vector2(CardW - 48, 1), MouseFilter = Control.MouseFilterEnum.Ignore });
            var price = Label($"單抽 {d?.price ?? 0} G", 20, Gold);
            price.HorizontalAlignment = HorizontalAlignment.Center; price.Position = new Vector2(0, 236); price.Size = new Vector2(CardW, 36); face.AddChild(price);

            _carousel.AddChild(holder);
            _gCards.Add(holder); _gFaces.Add(face); _gGlows.Add(glow); _lastOffset.Add(int.MinValue); _gTheme.Add(theme);
        }
        if (_shown.Count == 0)
        {
            var empty = Label("今天沒有寶箱", 24, Soft); empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.Position = new Vector2(0, 140); empty.Size = new Vector2(820, 40); _carousel.AddChild(empty); _gCards.Add(empty);
        }
        LayoutCards(false);
        RefreshGacha();
    }

    /// <summary>第 i 張卡離選中那張差幾格（三張以上會頭尾相接，選中的永遠在中間）。</summary>
    int Offset(int i)
    {
        int n = _shown.Count, k = i - _index;
        if (n >= 3) { k = ((k % n) + n) % n; if (k > n / 2) k -= n; }
        return k;
    }

    void LayoutCards(bool animate)
    {
        _carTween?.Kill(); _carTween = null;
        if (animate) _carTween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        for (int i = 0; i < _shown.Count && i < _gFaces.Count; i++)
        {
            int k = Offset(i); bool on = k == 0, vis = Math.Abs(k) <= 1;
            var c = _gCards[i];
            var pos = new Vector2(CarCX + k * CardStep - CardW / 2, CardCY - CardH / 2);
            var scale = Vector2.One * (on ? 1.06f : .80f);
            var mod = on ? Colors.White : new Color(.55f, .55f, .6f, vis ? 1f : 0f);
            _gFaces[i].MouseFilter = vis ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
            // 從一邊繞到另一邊的那張不要橫越畫面，直接換位置再淡入
            bool jump = _lastOffset[i] != int.MinValue && Math.Abs(_lastOffset[i] - k) > 1;
            _lastOffset[i] = k;
            if (!animate || jump)
            {
                c.Position = pos; c.Scale = scale;
                if (animate) { c.Modulate = new Color(mod.R, mod.G, mod.B, 0f); _carTween.TweenProperty(c, "modulate", mod, .24f); }
                else c.Modulate = mod;
                continue;
            }
            _carTween.TweenProperty(c, "position", pos, .24f);
            _carTween.TweenProperty(c, "scale", scale, .24f);
            _carTween.TweenProperty(c, "modulate", mod, .24f);
        }
    }

    void Select(int i)
    {
        if (_shown.Count == 0 || i == _index) return;
        _index = (i % _shown.Count + _shown.Count) % _shown.Count;
        RMZAudio.Instance?.PlaySeByName("Cursor3", 70);
        LayoutCards(true);
        RefreshGacha();
    }

    void RefreshGacha()
    {
        _gold.Text = $"持有金錢　{RMZParty.Gold} G";
        for (int i = 0; i < _gFaces.Count; i++)
        {
            bool on = Offset(i) == 0;
            var t = _gTheme[i];
            var fill = on ? new Color(t.R * .32f, t.G * .32f, t.B * .32f, .97f) : new Color(t.R * .16f + .03f, t.G * .16f + .03f, t.B * .16f + .04f, .95f);
            var border = on ? t.Lightened(.25f) : new Color(t.R * .6f, t.G * .6f, t.B * .6f);
            _gFaces[i].AddThemeStyleboxOverride("normal", Box(fill, border, on ? 3 : 2, 16));
            _gFaces[i].AddThemeStyleboxOverride("hover", Box(fill.Lightened(.1f), t.Lightened(.25f), 3, 16));
            _gFaces[i].AddThemeStyleboxOverride("pressed", Box(fill, t.Lightened(.25f), 3, 16));
            _gGlows[i].Visible = on;
        }
        if (_shown.Count == 0)
        {
            _desc.Text = ""; _onePrice.Text = ""; _tenPrice.Text = "";
            _actBtn.Disabled = _tenBtn.Disabled = true; _actBtn.Modulate = _tenBtn.Modulate = new Color(1, 1, 1, .5f);
            return;
        }
        var d = RMZDatabase.Item(_shown[_index]);
        int one = d?.price ?? 0;
        _desc.Text = RMZLocalization.Translate(d?.description ?? "").Replace("\\n", "\n");
        _onePrice.Text = $"{one} G";
        _tenPrice.Text = $"{one * TenPriceFactor} G（10抽只收9抽）";
        _actBtn.Disabled = RMZParty.Gold < one;
        _tenBtn.Disabled = RMZParty.Gold < one * TenPriceFactor;
        _actBtn.Modulate = _actBtn.Disabled ? new Color(1, 1, 1, .5f) : Colors.White;
        _tenBtn.Modulate = _tenBtn.Disabled ? new Color(1, 1, 1, .5f) : Colors.White;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        for (int i = 0; i < _gFaces.Count; i++)
        {
            var face = _gFaces[i];
            if (Offset(i) == 0) face.Position = new Vector2(0, Mathf.Sin(_time * 2.6f) * 3f);   // 選中：輕輕上下浮動
            else face.Position = face.Position.Lerp(Vector2.Zero, .2f);                        // 沒選中：停住
            _gGlows[i].Position = face.Position + new Vector2(-10, -10);
            _gGlows[i].Modulate = new Color(1, 1, 1, .55f + .45f * Mathf.Sin(_time * 3.2f));
        }
        if (_rays != null && IsInstanceValid(_rays)) _rays.Rotation += (float)delta * .35f;
        if (_rainbow && _light != null && IsInstanceValid(_light))
        {
            var h = Color.FromHsv(_time * .18f % 1f, .55f, 1f);
            _light.Modulate = new Color(h.R, h.G, h.B, _light.Modulate.A);
        }
        if (_rainbowBack != null && IsInstanceValid(_rainbowBack)) _rainbowBack.Rotation += (float)delta * .12f;
    }

    // ---------------- 開箱動畫 ----------------

    Control _result;            // 開箱＋開獎的整片畫面
    ColorRect _dim, _flash;
    Node2D _rays;
    TextureRect _rainbowBack;   // 稀有時整片彩色背景
    TextureRect _light, _chest;
    Control _bubble;
    Label _bubbleText;
    readonly List<Control> _cards = new();
    Tween _openTween, _revealTween;
    int _phase;                 // 0 沒有　1 寶箱晃動中　2 卡片翻開中　3 翻完了
    int _lastPull;
    List<RMZLootBox.Drop> _pending = new();
    int _pendingTimes;
    bool _rainbow;              // 這次有史詩以上：背景用彩色
    string _catFace;            // 開箱前黑貓的表情圖，結束後換回來

    const int CatFacePicture = 3;                  // 地圖事件用 3 號圖片放黑貓的表情
    const string CatSmile = "黑特喵立繪/笑2";
    static readonly Vector2 ChestPos = new(830, 300), RewardCenter = new(640, 345);

    static readonly string[] RarityNames = { "普通", "優良", "稀有", "史詩", "傳說" };
    static readonly Color[] RarityColors =
    {
        new(.78f, .78f, .82f), new(.45f, .9f, .5f), new(.4f, .7f, 1f), new(.78f, .5f, 1f), new(1f, .8f, .3f),
    };

    /// <summary>把「第幾層／共幾層」換成 0~4 的稀有度。</summary>
    static int Rarity(RMZLootBox.Drop d) => d.TierCount <= 1 ? 0 : Mathf.Clamp(Mathf.RoundToInt(d.Tier * 4f / (d.TierCount - 1)), 0, 4);
    static int Best(List<RMZLootBox.Drop> drops) { int b = 0; foreach (var d in drops) b = Math.Max(b, Rarity(d)); return b; }

    void Pull(int times)
    {
        if (_phase == 1 || _phase == 2) return;
        if (Time.GetTicksMsec() - _opened < 200 || _shown.Count == 0) return;
        var d = RMZDatabase.Item(_shown[_index]); if (d == null) return;
        int cost = d.price * (times >= 10 ? TenPriceFactor : 1);
        if (RMZParty.Gold < cost) { RMZAudio.Instance?.PlaySeByName("Buzzer1"); return; }

        RMZParty.GainGold(-cost);
        var drops = new List<RMZLootBox.Drop>();
        for (int i = 0; i < times; i++) drops.AddRange(RMZLootBox.Open(d.id));
        _lastPull = times;
        RefreshGacha();
        RMZEventRunner.Instance?.RequestRefresh();
        StartOpening(drops, times, d.iconIndex);
    }

    /// <summary>黑貓換成笑臉（或換回原本的表情）。</summary>
    void SetCatFace(bool smile)
    {
        var pic = RMZScreen.Instance?.Picture(CatFacePicture);
        if (pic == null) return;
        if (smile)
        {
            if (_catFace == null) { if (!(pic.Name ?? "").StartsWith("黑特喵立繪")) return; _catFace = pic.Name; }
            pic.Name = CatSmile;
        }
        else if (_catFace != null) { pic.Name = _catFace; _catFace = null; }
    }

    /// <summary>「開！」：字放大、整句一次跳出來（不打字），框跟著震一下。</summary>
    void Shout()
    {
        if (_bubbleText == null || !IsInstanceValid(_bubbleText)) return;
        _bubbleText.Text = "開！"; _bubbleText.VisibleRatio = 1f;
        _bubbleText.AddThemeFontSizeOverride("font_size", 40);
        _bubbleText.PivotOffset = _bubbleText.Size / 2; _bubbleText.Scale = Vector2.One * 1.6f;
        _bubbleText.CreateTween().TweenProperty(_bubbleText, "scale", Vector2.One, .18f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        RMZAudio.Instance?.PlaySeByName("Blow3", 85);
    }

    void Say(string text)
    {
        if (_bubbleText == null || !IsInstanceValid(_bubbleText)) return;
        _bubbleText.Text = text; _bubbleText.VisibleRatio = 0f;
        _bubbleText.CreateTween().TweenProperty(_bubbleText, "visible_ratio", 1f, Math.Max(.15f, text.Length * .05f));
    }

    void StartOpening(List<RMZLootBox.Drop> drops, int times, int chestIcon)
    {
        CloseResult(false, false);   // 「再抽一次」：先收掉上一輪，黑貓維持笑臉
        _pending = drops; _pendingTimes = times; _phase = 1;
        var tint = RarityColors[Best(drops)];   // 光的顏色先偷偷透露這次最好的稀有度

        _result = new Control { Size = new Vector2(1280, 720), MouseFilter = Control.MouseFilterEnum.Stop };
        _stage.AddChild(_result);
        _result.GuiInput += ev => { if (ev is InputEventMouseButton m && m.Pressed && m.ButtonIndex == MouseButton.Left) SkipStep(); };
        _dim = new ColorRect { Color = new Color(.03f, .02f, .06f, 0f), Size = new Vector2(1280, 720), MouseFilter = Control.MouseFilterEnum.Ignore };
        _result.AddChild(_dim);

        var add = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        _light = new TextureRect
        {
            Texture = RadialTexture(), Size = new Vector2(520, 520), PivotOffset = new Vector2(260, 260), Position = ChestPos - new Vector2(260, 260),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Modulate = new Color(tint.R, tint.G, tint.B, 0f), Material = add, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _result.AddChild(_light);
        _rainbow = Best(drops) >= 3;
        _rays = MakeRays(tint, add, _rainbow); _rays.Position = ChestPos; _rays.Modulate = new Color(1, 1, 1, 0f);
        _result.AddChild(_rays);

        _chest = new TextureRect
        {
            Texture = IconOf(chestIcon), Size = new Vector2(176, 176), PivotOffset = new Vector2(88, 88), Position = ChestPos - new Vector2(88, 88),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = Control.MouseFilterEnum.Ignore,
            Scale = Vector2.One * .3f, Modulate = new Color(1, 1, 1, 0f),
        };
        _result.AddChild(_chest);

        // 黑貓的台詞框（放在左下、她的立繪前面）
        var (fill, border, nameFill, speaker) = RMZWindowSkin.DialogueColors("黑貓", "黑貓");
        _bubble = new Control { Size = new Vector2(1280, 720), MouseFilter = Control.MouseFilterEnum.Ignore };
        _result.AddChild(_bubble);
        var box = new Panel { Position = new Vector2(36, 548), Size = new Vector2(370, 96), MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeStyleboxOverride("panel", RMZWindowSkin.PanelStyle(fill, border)); _bubble.AddChild(box);
        var plate = new Panel { Position = new Vector2(52, 528), Size = new Vector2(110, 36), MouseFilter = Control.MouseFilterEnum.Ignore };
        plate.AddThemeStyleboxOverride("panel", RMZWindowSkin.PanelStyle(nameFill, border)); _bubble.AddChild(plate);
        var who = Label("黑貓", 20, speaker); who.HorizontalAlignment = HorizontalAlignment.Center; who.Position = plate.Position; who.Size = plate.Size; _bubble.AddChild(who);
        _bubbleText = Label("", 26, RMZWindowSkin.Text); _bubbleText.Position = new Vector2(56, 568); _bubbleText.Size = new Vector2(336, 66);
        _bubbleText.AutowrapMode = TextServer.AutowrapMode.Arbitrary; _bubble.AddChild(_bubbleText);

        _flash = new ColorRect { Color = new Color(1, 1, 1, 0f), Size = new Vector2(1280, 720), MouseFilter = Control.MouseFilterEnum.Ignore };
        _result.AddChild(_flash);

        SetCatFace(true);
        RMZAudio.Instance?.PlaySeByName("Decision2");

        // 黑貓先把話講完（一句一句打字出來、停一下讓人看），講完才開始晃寶箱、開箱
        var tw = _openTween = CreateTween();
        tw.TweenProperty(_dim, "color:a", .25f, .25f);   // 開箱時只稍微壓暗，黑貓的笑臉要看得到
        tw.Parallel().TweenProperty(_chest, "scale", Vector2.One, .32f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(_chest, "modulate:a", 1f, .2f);
        tw.Parallel().TweenProperty(_light, "modulate:a", .3f, .4f);
        foreach (var (line, hold) in new[] { ("來看看會是什麼呢～", 1.1f), ("將大局逆轉吧！", .9f) })
        {
            tw.TweenCallback(Callable.From(() => Say(line)));
            tw.TweenInterval(line.Length * .05f + hold);
        }

        // 寶箱越晃越大力、越來越快，背後的光也越來越亮（蓄力）
        float[] amp = { 4, -4, 7, -7, 10, -10, 13, -13, 16, -16 };
        for (int i = 0; i < amp.Length; i++)
        {
            float t = i / (float)(amp.Length - 1);
            float dur = Mathf.Lerp(.09f, .045f, t);
            if (i % 3 == 0) tw.TweenCallback(Callable.From(() => RMZAudio.Instance?.PlaySeByName("Knock", 75)));
            tw.TweenProperty(_chest, "rotation", Mathf.DegToRad(amp[i]), dur);
            tw.Parallel().TweenProperty(_light, "modulate:a", Mathf.Lerp(.35f, .9f, t), dur);
            tw.Parallel().TweenProperty(_light, "scale", Vector2.One * Mathf.Lerp(1f, 1.35f, t), dur);
        }
        tw.TweenProperty(_chest, "rotation", 0f, .05f);
        tw.Parallel().TweenProperty(_chest, "scale", new Vector2(1.18f, .82f), .14f);   // 蓄力壓扁
        // 最後大喊「開！」，整句喊完才彈開
        tw.TweenCallback(Callable.From(Shout));
        tw.TweenInterval(.55f);
        tw.TweenCallback(Callable.From(BurstOpen));
    }

    /// <summary>寶箱彈開：閃光、寶箱放大消失，光芒移到畫面中間當獎勵的背景。</summary>
    void BurstOpen()
    {
        if (_phase != 1 || _result == null) return;
        _phase = 2;
        _openTween?.Kill();
        int best = Best(_pending);
        RMZAudio.Instance?.PlaySeByName("Open3");
        RMZAudio.Instance?.PlaySeByName(best >= 3 ? "Saint4" : "Flash2", 80);
        _flash.Color = new Color(1, 1, 1, .9f);
        _chest.Rotation = 0f;

        var tw = _openTween = CreateTween().SetParallel();
        tw.TweenProperty(_flash, "color:a", 0f, .45f);
        tw.TweenProperty(_chest, "scale", Vector2.One * 1.9f, .3f).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(_chest, "modulate:a", 0f, .3f);
        tw.TweenProperty(_bubble, "modulate:a", 0f, .3f);
        tw.TweenProperty(_light, "position", RewardCenter - _light.Size / 2, .35f);
        tw.TweenProperty(_light, "scale", Vector2.One * 2.1f, .35f);
        tw.TweenProperty(_light, "modulate:a", .55f, .35f);
        tw.TweenProperty(_rays, "position", RewardCenter, .35f);
        tw.TweenProperty(_rays, "modulate:a", .85f, .2f);
        tw.TweenProperty(_dim, "color:a", .78f, .35f);
        tw.Chain().TweenCallback(Callable.From(() => ShowResult(_pending, _pendingTimes)));
    }

    static GradientTexture2D _radial;
    static Texture2D RadialTexture()
    {
        if (_radial != null) return _radial;
        var g = new Gradient();
        g.SetColor(0, new Color(1, 1, 1, 1f)); g.SetColor(1, new Color(1, 1, 1, 0f));
        g.AddPoint(.4f, new Color(1, 1, 1, .4f));
        _radial = new GradientTexture2D { Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(.5f, 0f), Width = 256, Height = 256 };
        return _radial;
    }

    static GradientTexture2D _rainbowTex, _rainbowCard;
    /// <summary>從中心往外一圈圈的彩虹（整片背景用）。</summary>
    static Texture2D RainbowTexture()
    {
        if (_rainbowTex != null) return _rainbowTex;
        var g = new Gradient();
        g.SetColor(0, Color.FromHsv(0f, .7f, 1f)); g.SetColor(1, new Color(0, 0, 0, 0));
        for (int i = 1; i < 6; i++) g.AddPoint(i / 6f * .85f, Color.FromHsv(i / 6f, .7f, 1f));
        _rainbowTex = new GradientTexture2D { Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(.5f, 0f), Width = 256, Height = 256 };
        return _rainbowTex;
    }
    /// <summary>斜向的彩虹（稀有卡片的底色用）。</summary>
    static Texture2D RainbowCardTexture()
    {
        if (_rainbowCard != null) return _rainbowCard;
        var g = new Gradient();
        g.SetColor(0, Color.FromHsv(0f, .6f, .9f)); g.SetColor(1, Color.FromHsv(.83f, .6f, .9f));
        for (int i = 1; i < 5; i++) g.AddPoint(i / 5f, Color.FromHsv(i / 5f * .83f, .6f, .9f));
        _rainbowCard = new GradientTexture2D { Gradient = g, FillFrom = new Vector2(0f, 0f), FillTo = new Vector2(1f, 1f), Width = 128, Height = 128 };
        return _rainbowCard;
    }

    static Node2D MakeRays(Color tint, Material mat, bool rainbow)
    {
        var root = new Node2D();
        const int n = 14; const float r = 640;
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.Tau * i / n, w = .075f;
            var c = rainbow ? Color.FromHsv(i / (float)n, .65f, 1f) : tint;   // 稀有：彩虹光
            root.AddChild(new Polygon2D
            {
                Polygon = new[] { Vector2.Zero, Vector2.FromAngle(a - w) * r, Vector2.FromAngle(a + w) * r },
                VertexColors = new[] { new Color(c.R, c.G, c.B, .55f), new Color(c.R, c.G, c.B, 0f), new Color(c.R, c.G, c.B, 0f) },
                Material = mat,
            });
        }
        return root;
    }

    // ---------------- 開獎結果 ----------------

    void ShowResult(List<RMZLootBox.Drop> drops, int times)
    {
        if (_result == null) return;
        _phase = 2;
        if (_rainbow)
        {
            // 抽到史詩以上：整片背景換成慢慢轉的彩色光
            _rainbowBack = new TextureRect
            {
                Texture = RainbowTexture(), Size = new Vector2(1600, 1600), PivotOffset = new Vector2(800, 800), Position = RewardCenter - new Vector2(800, 800),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                Modulate = new Color(1, 1, 1, 0f), MouseFilter = Control.MouseFilterEnum.Ignore,
                Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            };
            _result.AddChild(_rainbowBack);
            _result.MoveChild(_rainbowBack, 1);   // 壓暗層上面、光芒和卡片下面
            _rainbowBack.CreateTween().TweenProperty(_rainbowBack, "modulate:a", .38f, .5f);
        }
        var title = Label(times >= 10 ? "十連抽結果" : "抽獎結果", 34, Gold);
        title.HorizontalAlignment = HorizontalAlignment.Center; title.Position = new Vector2(0, 26); title.Size = new Vector2(1280, 50);
        _result.AddChild(title);

        // 卡片排版：一排 5 張，最多 4 排（十連抽偶爾會開出雙份）
        int n = Math.Max(1, drops.Count), cols = Math.Min(5, n), rows = (n + 4) / 5;
        float cw = 200, ch = rows <= 2 ? 190 : 122, gap = 14;
        float gridH = rows * ch + (rows - 1) * gap;
        float y0 = 90 + (530 - gridH) / 2;

        _cards.Clear();
        int goldBack = 0, best = 0;
        for (int i = 0; i < drops.Count; i++)
        {
            var dr = drops[i]; int r = Rarity(dr); best = Math.Max(best, r); goldBack += dr.Gold;
            var card = MakeCard(dr, r, cw, ch);
            int col = i % 5, row = i / 5;
            int inRow = Math.Min(5, drops.Count - row * 5);
            float rowX = (1280 - (inRow * cw + (inRow - 1) * gap)) / 2;
            card.Position = new Vector2(rowX + col * (cw + gap), y0 + row * (ch + gap));
            card.PivotOffset = new Vector2(cw / 2, ch / 2);
            card.Modulate = new Color(1, 1, 1, 0); card.Scale = Vector2.One * .6f;
            _result.AddChild(card); _cards.Add(card);
        }
        if (drops.Count == 0)
        {
            var none = Label("寶箱是空的……", 24, Soft); none.HorizontalAlignment = HorizontalAlignment.Center;
            none.Position = new Vector2(0, 320); none.Size = new Vector2(1280, 40); _result.AddChild(none);
        }

        var sum = Label(goldBack > 0 ? $"重複的裝備自動換成了 {goldBack} G" : "", 18, Gold);
        sum.HorizontalAlignment = HorizontalAlignment.Center; sum.Position = new Vector2(0, 600); sum.Size = new Vector2(1280, 28);
        _result.AddChild(sum);

        var again = SmallBtn(times >= 10 ? "再十連抽" : "再抽一次", () => { if (_phase == 3) Pull(_lastPull); });
        again.Reparent(_result); again.Position = new Vector2(440, 640); again.Size = new Vector2(190, 46);
        var ok = SmallBtn("確定", () => { if (_phase == 2) SkipReveal(); else CloseResult(true); });
        ok.Reparent(_result); ok.Position = new Vector2(650, 640); ok.Size = new Vector2(190, 46);

        // 一張一張翻出來
        _revealTween = CreateTween();
        _revealTween.TweenInterval(times >= 10 ? .2f : .25f);
        for (int i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i]; int r = Rarity(drops[i]);
            _revealTween.TweenCallback(Callable.From(() => RMZAudio.Instance?.PlaySeByName(r >= 3 ? "Saint4" : r == 2 ? "Item3" : "Cursor3", r >= 3 ? 90 : 70)));
            _revealTween.TweenProperty(card, "modulate", Colors.White, .12f);
            _revealTween.Parallel().TweenProperty(card, "scale", Vector2.One, .18f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            _revealTween.TweenInterval(times >= 10 ? .08f : .1f);
        }
        _revealTween.TweenCallback(Callable.From(() => FinishReveal(best)));
    }

    Control MakeCard(RMZLootBox.Drop dr, int r, float w, float h)
    {
        var col = RarityColors[r];
        var card = new Panel { Size = new Vector2(w, h), MouseFilter = Control.MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", Box(new Color(.10f, .09f, .14f, .97f), col, r >= 3 ? 3 : 2, 12));

        if (r >= 3)
        {
            // 史詩以上：卡片底色換成彩虹
            card.AddChild(new TextureRect
            {
                Texture = RainbowCardTexture(), Position = new Vector2(4, 4), Size = new Vector2(w - 8, h - 8),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                Modulate = new Color(1, 1, 1, r >= 4 ? .6f : .45f), MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        }
        if (r >= 1)
        {
            // 優良以上：卡片背後發光，史詩以上會一閃一閃
            float a = r >= 3 ? .26f : .14f;
            var glow = new Panel { Position = new Vector2(-7, -7), Size = new Vector2(w + 14, h + 14), MouseFilter = Control.MouseFilterEnum.Ignore, ShowBehindParent = true };
            glow.AddThemeStyleboxOverride("panel", Box(new Color(col.R, col.G, col.B, a), new Color(col.R, col.G, col.B, a * 2.4f), 2, 16));
            card.AddChild(glow);
            if (r >= 3)
            {
                var pulse = glow.CreateTween().SetLoops();
                pulse.TweenProperty(glow, "modulate", new Color(1, 1, 1, .35f), .6f);
                pulse.TweenProperty(glow, "modulate", Colors.White, .6f);
            }
        }

        var d = RMZDatabase.Item(dr.ItemId);
        bool tall = h >= 150;
        float icon = tall ? 56 : 36;
        if (_icons != null && d != null)
            card.AddChild(new TextureRect
            {
                Texture = IconOf(d.iconIndex),
                Position = new Vector2((w - icon) / 2, tall ? 16 : 8), Size = new Vector2(icon, icon),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = Control.MouseFilterEnum.Ignore,
            });

        var name = Label(ItemName(dr.ItemId), tall ? 18 : 15, Colors.White);
        name.HorizontalAlignment = HorizontalAlignment.Center; name.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        name.Position = new Vector2(6, tall ? 80 : 46); name.Size = new Vector2(w - 12, tall ? 52 : 40);
        card.AddChild(name);

        var tag = Label(dr.Gold > 0 ? $"已擁有 → +{dr.Gold} G" : RarityNames[r], tall ? 16 : 13, dr.Gold > 0 ? Gold : col);
        tag.HorizontalAlignment = HorizontalAlignment.Center;
        tag.Position = new Vector2(0, h - (tall ? 34 : 24)); tag.Size = new Vector2(w, tall ? 26 : 20);
        card.AddChild(tag);
        return card;
    }

    /// <summary>點一下／按 Enter：晃動中 → 直接開箱；翻卡中 → 全部翻開。</summary>
    void SkipStep()
    {
        if (_phase == 1) BurstOpen();
        else if (_phase == 2) SkipReveal();
    }

    void SkipReveal()
    {
        if (_phase != 2) return;
        if (_cards.Count == 0 && _revealTween == null) return;   // 還在彈開的那一下，等卡片出來
        _revealTween?.Kill();
        foreach (var c in _cards) { c.Modulate = Colors.White; c.Scale = Vector2.One; }
        FinishReveal(0);
    }

    void FinishReveal(int best)
    {
        if (_phase != 2) return;
        _phase = 3;
        if (_rays != null && IsInstanceValid(_rays)) _rays.CreateTween().TweenProperty(_rays, "modulate:a", .4f, .6f);
        if (best >= 4) RMZAudio.Instance?.PlaySeByName("Starlight");
    }

    void CloseResult(bool sound, bool restoreFace = true)
    {
        if (_result == null) return;
        _openTween?.Kill(); _revealTween?.Kill(); _revealTween = null;
        _phase = 0;
        _result.QueueFree(); _result = null; _cards.Clear();
        _rays = null; _rainbowBack = null; _rainbow = false; _light = null; _chest = null; _bubble = null; _bubbleText = null; _dim = null; _flash = null;
        if (restoreFace) SetCatFace(false);
        if (sound) RMZAudio.Instance?.PlaySeByName("Cancel2");
        RefreshGacha();
    }

    void Close() { if (_phase == 1 || _phase == 2) return; RMZAudio.Instance?.PlaySeByName("Cancel2"); QueueFree(); }
    public override void _ExitTree() { SetCatFace(false); if (_active == this) _active = null; }

    public override void _Input(InputEvent e)
    {
        if (_result != null)
        {
            // 開箱／開獎畫面：Enter／Space／Esc／右鍵 → 跳過動畫，翻完了就關掉
            bool go = e is InputEventMouseButton m && m.Pressed && m.ButtonIndex == MouseButton.Right
                   || e is InputEventKey rk && rk.Pressed && !rk.Echo && rk.Keycode is Key.Enter or Key.KpEnter or Key.Space or Key.Escape;
            if (!go) return;
            GetViewport().SetInputAsHandled();
            if (_phase == 3) CloseResult(true); else SkipStep();
            return;
        }
        if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right) { GetViewport().SetInputAsHandled(); Close(); return; }
        if (e is not InputEventKey k || !k.Pressed) return;
        GetViewport().SetInputAsHandled();
        if (_gacha)
        {
            switch (k.Keycode)
            {
                case Key.Escape: Close(); break;
                case Key.Left: case Key.Up: Select(_index - 1); break;
                case Key.Right: case Key.Down: Select(_index + 1); break;
                case Key.Enter: case Key.KpEnter: case Key.Space: if (!k.Echo) Pull(k.ShiftPressed ? 10 : 1); break;
            }
            return;
        }
        switch (k.Keycode)
        {
            case Key.Escape: Close(); break;
            case Key.Up: if (_shown.Count > 0) { _index = (_index - 1 + _shown.Count) % _shown.Count; _qty = 1; Refresh(); } break;
            case Key.Down: if (_shown.Count > 0) { _index = (_index + 1) % _shown.Count; _qty = 1; Refresh(); } break;
            case Key.Left: ChangeQty(-1); break;
            case Key.Right: ChangeQty(1); break;
            case Key.Tab: SetMode(!_sellMode); break;
            case Key.Enter: case Key.KpEnter: case Key.Space: if (!k.Echo) Act(); break;
        }
    }
}
