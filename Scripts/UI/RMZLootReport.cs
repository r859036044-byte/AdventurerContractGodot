using Godot;
using System.Collections.Generic;
namespace RMZ;
public struct RMZLootLine { public int IconIndex, Count; public string Name; public bool IsAffection; }
public partial class RMZLootReport : CanvasLayer
{
    public static bool IsOpen { get; private set; }
    bool closing;
    public static void Open(int outcome, int gold, int kills, int seconds, List<RMZLootLine> lines)
    {
        if (IsOpen || RMZBootstrap.Instance == null) return;
        IsOpen = true;
        var report = new RMZLootReport { Layer = 90 };
        RMZBootstrap.Instance.GetTree().Root.AddChild(report);
        var screen = new Control { Theme = new Theme { DefaultFont = RMZFonts.Main, DefaultFontSize = 23 } }; report.AddChild(screen);
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.AddThemeFontOverride("font", RMZFonts.Main);
        screen.AddThemeFontSizeOverride("font_size", 23);
        var shade = new ColorRect { Color = new Color(.015f, .025f, .05f, .78f) };
        screen.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var center = new CenterContainer(); screen.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(760, 510) }; center.AddChild(panel);
        panel.AddThemeStyleboxOverride("panel", Style(new Color(.07f,.08f,.12f,.98f)));
        var margin = new MarginContainer(); panel.AddChild(margin);
        foreach (var side in new[]{"left","right","top","bottom"}) margin.AddThemeConstantOverride("margin_"+side, 28);
        var content = new VBoxContainer(); margin.AddChild(content); content.AddThemeConstantOverride("separation", 20);
        var title = new Label { Text = outcome == 1 ? "委託完成" : outcome == 2 ? "安全撤退" : "探索結束", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size",34); content.AddChild(title);
        content.AddChild(new Label { Text = $"擊敗 {kills} 隻怪物　／　探索 {seconds/60}:{seconds%60:00}　／　帶回 {gold} G", HorizontalAlignment = HorizontalAlignment.Center });
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0,250), SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        content.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(list);
        list.AddThemeConstantOverride("separation", 10);
        if (lines != null) foreach (var line in lines) { var label = new RMZIconLabel { CustomMinimumSize = new Vector2(0,36) }; label.SetIconText($"\uE000{line.IconIndex}\uE001 {line.Name}　{(line.IsAffection ? "+" : "×")} {line.Count}"); list.AddChild(label); }
        if (lines == null || lines.Count == 0) list.AddChild(new Label { Text = "這次沒有帶回物品。", HorizontalAlignment = HorizontalAlignment.Center });
        var close = new Button { Text = "完成清點，返回公會", CustomMinimumSize = new Vector2(0,52) }; content.AddChild(close);
        close.AddThemeStyleboxOverride("normal", Style(new Color(.13f,.15f,.20f)));
        close.AddThemeStyleboxOverride("hover", Style(new Color(.20f,.22f,.30f)));
        close.AddThemeStyleboxOverride("focus", Style(new Color(.20f,.22f,.30f)));
        close.Pressed += () => { if (report.closing) return; report.closing = true; close.Disabled = true; report.QueueFree(); };
        close.GrabFocus();
    }
    static StyleBoxFlat Style(Color fill) => new StyleBoxFlat { BgColor = fill, BorderColor = new Color(.82f,.70f,.38f), BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10 };
    public override void _ExitTree() { IsOpen = false; }
}
