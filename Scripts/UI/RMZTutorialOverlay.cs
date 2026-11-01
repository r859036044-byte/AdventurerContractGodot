using Godot;
using System;
using System.Threading.Tasks;
namespace RMZ;

// 教學是模態提示：讓實際介面留在下方，聚焦目標並攔住同一次點擊。
public partial class RMZTutorialOverlay : CanvasLayer
{
    static RMZTutorialOverlay _active;
    public static bool IsOpen => GodotObject.IsInstanceValid(_active);
    readonly TaskCompletionSource<bool> _done = new();
    Func<Rect2> _target;
    string _speaker, _text;
    float _age;
    FocusSurface _surface;
    Control _stage;
    public static Task Show(string speaker, string text, Rect2 referenceRect) => Show(speaker,text,()=>referenceRect);
    public static Task Show(string speaker, string text, Func<Rect2> target)
    {
        if(IsOpen) return _active._done.Task;
        var node=new RMZTutorialOverlay { Layer=190,_speaker=speaker,_text=text,_target=target };
        _active=node;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(node);
        return node._done.Task;
    }
    public override void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always;
        var blocker=new Control {MouseFilter=Control.MouseFilterEnum.Stop};AddChild(blocker);blocker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _stage=new Control {Size=new Vector2(1280,720),MouseFilter=Control.MouseFilterEnum.Ignore};blocker.AddChild(_stage);
        _surface=new FocusSurface();_stage.AddChild(_surface);
        var rect=_target();
        var panel=new PanelContainer {Position=new Vector2(300,rect.GetCenter().Y>370?72:455),Size=new Vector2(680,195),MouseFilter=Control.MouseFilterEnum.Ignore};_stage.AddChild(panel);
        var box=new StyleBoxFlat {BgColor=new Color("171925"),BorderColor=new Color("dabb62"),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,CornerRadiusTopLeft=12,CornerRadiusTopRight=12,CornerRadiusBottomLeft=12,CornerRadiusBottomRight=12,ContentMarginLeft=24,ContentMarginRight=24,ContentMarginTop=16,ContentMarginBottom=16};panel.AddThemeStyleboxOverride("panel",box);
        var v=new VBoxContainer {MouseFilter=Control.MouseFilterEnum.Ignore};panel.AddChild(v);
        var name=new Label {Text=_speaker,Modulate=new Color("efcf76")};v.AddChild(name);
        var text=new Label {Text=_text,AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new Vector2(630,80)};v.AddChild(text);
        var hint=new Label {Text="點擊任意處繼續",HorizontalAlignment=HorizontalAlignment.Right,Modulate=new Color("d7c99f")};v.AddChild(hint);
        foreach(var label in new[]{name,text,hint}) {label.AddThemeFontOverride("font",RMZFonts.Main);label.AddThemeFontSizeOverride("font_size",label==hint?18:24);label.MouseFilter=Control.MouseFilterEnum.Ignore;}
        UpdateLayout();
    }
    void UpdateLayout()
    {
        var size=GetViewport().GetVisibleRect().Size;float scale=Mathf.Min(size.X/1280f,size.Y/720f);
        _stage.Scale=Vector2.One*scale;_stage.Position=(size-new Vector2(1280,720)*scale)/2f;
        _surface.Target=_target();_surface.Age=_age;_surface.QueueRedraw();
    }
    public override void _Process(double delta){_age+=(float)delta;UpdateLayout();}
    public override void _Input(InputEvent e)
    {
        if(e is InputEventMouseButton mouse) {
            GetViewport().SetInputAsHandled();
            if(!mouse.Pressed && _age>.2f) Finish();
        } else if(e is InputEventKey key) {
            GetViewport().SetInputAsHandled();
            if(!key.Pressed && (key.Keycode==Key.Enter || key.Keycode==Key.Space) && _age>.2f) Finish();
        }
    }
    void Finish(){if(_active==this)_active=null;QueueFree();_done.TrySetResult(true);}
    public override void _ExitTree(){if(_active==this)_active=null;_done.TrySetResult(true);}
    public partial class FocusSurface : Control
    {
        public Rect2 Target;public float Age;
        public FocusSurface(){MouseFilter=MouseFilterEnum.Ignore;}
        public override void _Draw()
        {
            var r=Target.Grow(10).Intersection(new Rect2(0,0,1280,720));var shade=new Color(0,0,0,.7f);
            DrawRect(new Rect2(0,0,1280,Mathf.Max(0,r.Position.Y)),shade);
            DrawRect(new Rect2(0,r.End.Y,1280,Mathf.Max(0,720-r.End.Y)),shade);
            DrawRect(new Rect2(0,r.Position.Y,Mathf.Max(0,r.Position.X),r.Size.Y),shade);
            DrawRect(new Rect2(r.End.X,r.Position.Y,Mathf.Max(0,1280-r.End.X),r.Size.Y),shade);
            var glow=new Color(1,.83f,.32f,.65f+.25f*Mathf.Sin(Age*4));
            DrawRect(r,glow,false,3,true);
            var point=new Vector2(r.GetCenter().X,r.Position.Y);
            for(int i=3;i>0;i--)DrawCircle(point,8+i*5,new Color(1,.8f,.3f,.08f));
            DrawCircle(point,5,glow);
        }
    }
}
