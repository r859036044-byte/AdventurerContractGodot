using System;
using System.Collections.Generic;
using Godot;
namespace RMZ;

// 依 Unity RMZOptionsScene：640px 單欄、46px 列高、深藍底與淡金邊。
public partial class RMZOptionsScene : CanvasLayer
{
    public static RMZOptionsScene Instance { get; private set; }
    public static bool IsOpen=>Instance!=null&&Instance._open;
    public static void Open()=>Instance?.Show();
    /// <summary>從冒險小遊戲裡打開：小遊戲的介面在 Layer 100 以上，要蓋在它上面。關掉時退回原本的 45。</summary>
    public static void OpenOnTop(){if(Instance==null)return;Instance.Layer=190;Instance.Show();}
    readonly List<Panel> rows=new();
    readonly List<Action> refreshers=new();
    readonly List<Action<int>> changes=new();
    readonly List<Action> confirms=new();
    Control root,panel;OptionButton speed;bool _open,sync;int index;
    static readonly Color Gold=new(1,.9f,.6f);
    public override void _EnterTree()=>Instance=this;
    public override void _ExitTree(){if(Instance==this)Instance=null;}
    public override void _Ready()
    {
        Layer=45;ProcessPriority=390;ProcessMode=ProcessModeEnum.Always;   // 冒險小遊戲裡也要能操作
        root=new Control { Theme=new Theme { DefaultFont=RMZFonts.Main,DefaultFontSize=21 } };AddChild(root);root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shade=new ColorRect{Color=new Color(0,0,0,.72f)};root.AddChild(shade);shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var p=new Panel();panel=p;root.AddChild(p);p.Size=new Vector2(640,668);
        p.AddThemeStyleboxOverride("panel",Skin(new Color(.07f,.08f,.12f,.98f),new Color(.82f,.7f,.38f,.4f),10));
        p.AddThemeFontOverride("font",RMZFonts.Main);p.AddThemeFontSizeOverride("font_size",21);
        Put(Label("選項",26,Gold,HorizontalAlignment.Center),p,12,12,616,34);
        Toggle(Term("alwaysDash","始終跑步"),()=>RMZOptions.AlwaysDash,v=>RMZOptions.AlwaysDash=v);
        Toggle(Term("commandRemember","記住指令"),()=>RMZOptions.CommandRemember,v=>RMZOptions.CommandRemember=v);
        Toggle(Term("touchUI","觸屏界面"),()=>RMZOptions.TouchUI,v=>RMZOptions.TouchUI=v);
        Volume(Term("bgmVolume","背景音樂音量"),()=>RMZOptions.BgmVolume,v=>RMZOptions.BgmVolume=v);
        Volume(Term("bgsVolume","背景聲音量"),()=>RMZOptions.BgsVolume,v=>RMZOptions.BgsVolume=v);
        Volume(Term("meVolume","樂曲音效音量"),()=>RMZOptions.MeVolume,v=>RMZOptions.MeVolume=v);
        Volume(Term("seVolume","聲效音量"),()=>RMZOptions.SeVolume,v=>RMZOptions.SeVolume=v);
        var row=Row("快速劇情速度");speed=new OptionButton{FocusMode=Control.FocusModeEnum.None};
        foreach(var name in RMZOptions.FastForwardSpeedNames)speed.AddItem(name);
        ButtonSkin(speed);Put(speed,row,380,0,208,40);
        speed.GetPopup().AddThemeFontOverride("font",RMZFonts.Main);
        speed.GetPopup().AddThemeFontSizeOverride("font_size",21);
        speed.GetPopup().AddThemeStyleboxOverride("panel",Skin(new Color(.07f,.08f,.12f),new Color(.82f,.7f,.38f),6));
        speed.ItemSelected+=v=>{if(sync)return;RMZOptions.FastForwardSpeed=(int)v+1;Commit();};
        refreshers.Add(()=>speed.Select(RMZOptions.FastForwardSpeed-1));
        changes.Add(d=>{RMZOptions.FastForwardSpeed=Mathf.Clamp(RMZOptions.FastForwardSpeed+d,1,5);Commit();});
        confirms.Add(()=>speed.ShowPopup());
        // 冒險（倖存者）戰鬥的畫質分級：後期卡的話調低，極低以順暢為優先
        var qrow=Row("冒險畫質");var quality=new OptionButton{FocusMode=Control.FocusModeEnum.None};
        foreach(var name in RMZOptions.SurvivorQualityNames)quality.AddItem(name);
        ButtonSkin(quality);Put(quality,qrow,380,0,208,40);
        quality.GetPopup().AddThemeFontOverride("font",RMZFonts.Main);
        quality.GetPopup().AddThemeFontSizeOverride("font_size",21);
        quality.GetPopup().AddThemeStyleboxOverride("panel",Skin(new Color(.07f,.08f,.12f),new Color(.82f,.7f,.38f),6));
        quality.ItemSelected+=v=>{if(sync)return;RMZOptions.SurvivorQuality=(int)v;Commit();};
        refreshers.Add(()=>quality.Select(RMZOptions.SurvivorQuality));
        changes.Add(d=>{RMZOptions.SurvivorQuality=Mathf.Clamp(RMZOptions.SurvivorQuality+d,0,3);Commit();});
        confirms.Add(()=>quality.ShowPopup());
        Command("按鍵設定",()=>RMZKeyBindPanel.Open(this));
        Command("回復預設值",()=>{RMZOptions.ResetToDefault();Refresh();});Command("離開",Close);
        Put(Label("↑↓ 選擇　←→ 調整　音量可拖曳　Esc / 右鍵 離開",15,new Color(.78f,.79f,.84f),HorizontalAlignment.Center),panel,12,630,616,24);
        root.Visible=false;
    }
    static string Term(string key,string fallback)=>RMZDatabase.System?.terms?.messages!=null&&RMZDatabase.System.terms.messages.TryGetValue(key,out var s)?s:fallback;
    static StyleBoxFlat Skin(Color fill,Color edge,int radius=2)=>new(){BgColor=fill,BorderColor=edge,BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,CornerRadiusTopLeft=radius,CornerRadiusTopRight=radius,CornerRadiusBottomLeft=radius,CornerRadiusBottomRight=radius};
    static Label Label(string text,int size,Color color,HorizontalAlignment align=HorizontalAlignment.Left)
    {var l=new Label{Text=text,HorizontalAlignment=align,VerticalAlignment=VerticalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};l.AddThemeFontOverride("font",RMZFonts.Main);l.AddThemeFontSizeOverride("font_size",size);l.AddThemeColorOverride("font_color",color);return l;}
    static void Put(Control c,Control p,float x,float y,float w,float h){p.AddChild(c);c.Position=new Vector2(x,y);c.Size=new Vector2(w,h);}
    static void ButtonSkin(Button b)
    {b.AddThemeStyleboxOverride("normal",new StyleBoxEmpty());b.AddThemeStyleboxOverride("hover",Skin(new Color(1,.85f,.45f,.12f),new Color(1,.85f,.45f,.4f)));b.AddThemeStyleboxOverride("pressed",Skin(new Color(1,.85f,.45f,.2f),Gold));b.AddThemeColorOverride("font_color",Gold);}
    Panel Row(string label)
    {int n=rows.Count;var r=new Panel();Put(r,panel,14,52+n*46,612,40);Put(Label(label,21,Colors.White),r,20,0,355,40);r.MouseEntered+=()=>{index=n;Refresh();};rows.Add(r);return r;}
    void Toggle(string label,Func<bool> get,Action<bool> set)
    {var r=Row(label);var b=new Button{FocusMode=Control.FocusModeEnum.None,Alignment=HorizontalAlignment.Right};ButtonSkin(b);Put(b,r,380,0,208,40);Action toggle=()=>{set(!get());Commit();};b.Pressed+=toggle;refreshers.Add(()=>b.Text=get()?"ON":"OFF");changes.Add(_=>toggle());confirms.Add(toggle);}
    void Volume(string title,Func<int> get,Action<int> set)
    {var r=Row(title);int n=rows.Count-1;var number=Label("",21,Gold,HorizontalAlignment.Right);Put(number,r,518,0,70,40);
        var slider=new HSlider{MinValue=0,MaxValue=100,Step=1,FocusMode=Control.FocusModeEnum.None};
        slider.AddThemeStyleboxOverride("slider",new StyleBoxFlat{BgColor=new Color(.06f,.07f,.11f),ContentMarginTop=6,ContentMarginBottom=6});
        slider.AddThemeStyleboxOverride("grabber_area",new StyleBoxFlat{BgColor=new Color(.86f,.74f,.42f,.75f),ContentMarginTop=4,ContentMarginBottom=4});
        Put(slider,r,245,4,255,32);slider.MouseEntered+=()=>{index=n;Refresh();};
        slider.ValueChanged+=v=>{if(sync)return;set((int)v);Commit();};
        refreshers.Add(()=>{slider.SetValueNoSignal(get());number.Text=get().ToString();number.Modulate=get()==0?new Color(.55f,.57f,.62f):Colors.White;});
        changes.Add(d=>{set(RMZOptions.ClampVolume(get()+d*20));Commit();});confirms.Add(()=>{set(get()>=100?0:RMZOptions.ClampVolume(get()+20));Commit();});}
    void Command(string text,Action action)
    {var r=Row("");var b=new Button{Text=text,Alignment=HorizontalAlignment.Left,FocusMode=Control.FocusModeEnum.None};ButtonSkin(b);Put(b,r,20,0,568,40);b.Pressed+=action;changes.Add(_=>{});confirms.Add(action);refreshers.Add(()=>{});}
    void Commit(){RMZOptions.Apply();Refresh();}
    void Refresh(){sync=true;for(int i=0;i<rows.Count;i++){bool on=i==index;rows[i].AddThemeStyleboxOverride("panel",Skin(on?new Color(.20f,.22f,.30f):new Color(.13f,.15f,.20f),on?new Color(1,.85f,.45f,.4f):Colors.Transparent));refreshers[i]();}sync=false;}
    public new void Show(){_open=true;index=0;root.Visible=true;Refresh();Layout();}
    /// <summary>最後一次關掉的那一幀（冒險小遊戲用來避免同一顆 Esc 又把遊戲暫停）。</summary>
    public static ulong ClosedFrame;
    public void Close(){if(!_open)return;_open=false;Layer=45;ClosedFrame=Engine.GetProcessFrames();speed.GetPopup().Hide();root.Visible=false;RMZOptions.Save();RMZAudio.Instance?.PlaySeByName("Cancel2");}
    void Layout(){var view=GetViewport().GetVisibleRect().Size;float s=Mathf.Max(.1f,Mathf.Min(1,Mathf.Min((view.X-24)/640,(view.Y-24)/668)));panel.Scale=Vector2.One*s;panel.Position=(view-new Vector2(640,668)*s)/2;}
    public override void _Process(double delta)
    {if(!_open)return;Layout();if(speed.GetPopup().Visible)return;if(RMZKeyBindPanel.IsOpen)return;/* 按鍵設定開著時由它處理按鍵 */if(Input.IsActionJustPressed("ui_cancel")){Close();return;}if(Input.IsActionJustPressed("ui_down")){index=Math.Min(rows.Count-1,index+1);Refresh();}if(Input.IsActionJustPressed("ui_up")){index=Math.Max(0,index-1);Refresh();}if(Input.IsActionJustPressed("ui_left"))changes[index](-1);if(Input.IsActionJustPressed("ui_right"))changes[index](1);if(Input.IsActionJustPressed("ui_accept"))confirms[index]();}
    public override void _Input(InputEvent e){if(RMZKeyBindPanel.IsOpen)return;if(_open&&e is InputEventMouseButton m&&m.Pressed&&m.ButtonIndex==MouseButton.Right){Close();GetViewport().SetInputAsHandled();}}
}
