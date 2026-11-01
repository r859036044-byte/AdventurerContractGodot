using Godot;
using System.Collections.Generic;
namespace RMZS;
public partial class SVRetireLine : CanvasLayer
{
    public const float HoldSeconds=3.4f,FadeSeconds=.8f,PortraitHeight=300f;
    static SVRetireLine instance;
    readonly Queue<(string who,SVTalkLine line,GodotPort.Sprite small)> pending=new();
    Control content;
    float age;
    public static void Show(string who,string sheet=null,int sheetIndex=0)
    {
        if(SVRuntime.Instance==null)return;
        if(!GodotObject.IsInstanceValid(instance)) { instance=new SVRetireLine { Layer=790 }; SVRuntime.Instance.AddChild(instance); }
        var type=SVPortraitLayout.Character(who);
        var small=SVSpriteSheet.Get(sheet ?? type?.SheetName,sheet==null ? type?.SheetIndex ?? 0 : sheetIndex,2,1);
        var line=SVTalk.RetireLine(who);
        if(string.IsNullOrEmpty(line.Text))line.Text="我先退下了，剩下的交給你們！";
        instance.pending.Enqueue((who,line,small));
        if(instance.content==null)instance.Next();
    }
    public static void Clear() { if(GodotObject.IsInstanceValid(instance))instance.QueueFree(); instance=null; }
    void Next()
    {
        if(pending.Count==0)return;
        var item=pending.Dequeue(); age=0;
        content=new Control { MouseFilter=Control.MouseFilterEnum.Ignore }; AddChild(content);
        content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var pair=SVPortraitLayout.Crop(item.line.Base,item.line.Face);
        var anchor=new Control { MouseFilter=Control.MouseFilterEnum.Ignore }; content.AddChild(anchor);
        anchor.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft); anchor.OffsetLeft=24; anchor.OffsetRight=824; anchor.OffsetTop=-380; anchor.OffsetBottom=-80;
        foreach(var s in new[]{pair.body,pair.face})if(s!=null)Image(anchor,s.Native,new Rect2(0,0,225,300));
        if(item.small!=null)Image(anchor,item.small.Native,new Rect2(pair.body==null?40:244,170,96,96));
        var panel=new Panel { Position=new Vector2(350,155),Size=new Vector2(430,132),MouseFilter=Control.MouseFilterEnum.Ignore };anchor.AddChild(panel);
        panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat { BgColor=new Color(.07f,.08f,.12f,.93f),BorderColor=new Color(.82f,.70f,.38f),BorderWidthBottom=2,BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,CornerRadiusTopLeft=8,CornerRadiusTopRight=8,CornerRadiusBottomLeft=8,CornerRadiusBottomRight=8 });
        var name=new Label { Text=item.who+"　撤離戰場",Position=new Vector2(16,10),MouseFilter=Control.MouseFilterEnum.Ignore }; panel.AddChild(name);name.AddThemeFontOverride("font",RMZ.RMZFonts.Main);name.AddThemeFontSizeOverride("font_size",20);name.Modulate=new Color(1,.88f,.6f);
        var text=new Label { Text=item.line.Text,Position=new Vector2(16,44),Size=new Vector2(398,78),AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=Control.MouseFilterEnum.Ignore };panel.AddChild(text);text.AddThemeFontOverride("font",RMZ.RMZFonts.Main);text.AddThemeFontSizeOverride("font_size",21);
    }
    static void Image(Control parent,Texture2D texture,Rect2 rect) { var view=new TextureRect { ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,Texture=texture,MouseFilter=Control.MouseFilterEnum.Ignore,TextureFilter=CanvasItem.TextureFilterEnum.Nearest };parent.AddChild(view);view.Position=rect.Position;view.Size=rect.Size; }
    public override void _Process(double delta) { if(content==null)return;age+=(float)delta;content.Modulate=new Color(1,1,1,Mathf.Min(Mathf.Clamp(age/.18f,0,1),Mathf.Clamp((HoldSeconds-age)/FadeSeconds,0,1)));if(age>=HoldSeconds){content.QueueFree();content=null;Next();} }
    public override void _ExitTree(){if(instance==this)instance=null;}
}
