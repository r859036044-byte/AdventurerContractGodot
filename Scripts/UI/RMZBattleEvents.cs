using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
namespace RMZ;

public sealed class RMZBattleEventOption
{
    public string Label,GoodText,BadText;
    public int ItemId,Chance,Min,Max,Bad;
}
public sealed class RMZBattleEventData
{
    public string Id,Character,Title,Scene,Base,Face;
    public int Heroine,Tier;
    public List<RMZBattleEventOption> Options;
}
public static class RMZBattleEvents
{
    static List<RMZBattleEventData> _catalog;
    public static readonly List<string> History=new();
    public static bool LegacyAlreadyHandled;
    public static void Reset(){History.Clear();LegacyAlreadyHandled=false;}
    public static int TierOf(int love)=>love>=80?3:love>=60?2:love>=30?1:0;
    public static IEnumerator Play(int heroine,bool survivors=false)
    {
        if(heroine<1||heroine>5)yield break;
        // 好感鎖定中不會發生互動（要先送禮突破，或等鎖定後的隨機事件突破）
        if(RMZGifts.IsLocked(heroine))yield break;
        if(!survivors&&LegacyAlreadyHandled){LegacyAlreadyHandled=false;yield break;}
        if(_catalog==null){
            try{_catalog=JsonConvert.DeserializeObject<List<RMZBattleEventData>>(File.ReadAllText(Path.Combine(RMZConfig.RootPath,"battle_events.json")));}
            catch(Exception e){GD.PushError("戰後事件讀取失敗："+e.Message);yield break;}
        }
        var pool=_catalog.FindAll(e=>e.Heroine==heroine&&e.Tier==TierOf(RMZGifts.Love(heroine)));
        if(pool.Count==0)yield break;
        // 每一輪先看完目前階段的事件，再重新抽；上一輪最後一件不連續重複。
        var unseen=pool.FindAll(e=>!History.Contains(e.Id));
        if(unseen.Count==0){string last=History.LastOrDefault();History.RemoveAll(id=>pool.Exists(e=>e.Id==id));unseen=pool.FindAll(e=>e.Id!=last);}
        var selected=unseen[(int)GD.RandRange(0,unseen.Count-1)];
        History.Add(selected.Id);
        if(survivors)LegacyAlreadyHandled=true;
        var panel=new RMZBattleEventPanel();panel.Event=selected;
        RMZBootstrap.Instance.GetTree().Root.AddChild(panel);
        while(GodotObject.IsInstanceValid(panel)&&!panel.Finished)yield return null;
        if(GodotObject.IsInstanceValid(panel))panel.QueueFree();
    }
}

public partial class RMZBattleEventPanel:CanvasLayer
{
    // 戰後互動視窗。2026-09-30 第三版：
    //   ・上框：標題＋情境文字（「鈴音停在岔路旁……」）；選完改顯示結果
    //   ・下框：三張選項卡（字放大置中，需要道具時道具也置中）；選完改顯示角色回應
    //   ・鈴音用魔力外框（RMZMagicFrame），其他角色跟公會對話框同一套配色
    public RMZBattleEventData Event;
    public bool Finished;
    Control _stage;
    Label _story,_heading,_scene,_notice;
    Panel _top;
    CpuParticles2D _topSparks;
    readonly List<Button> _choices=new();
    Button _continue;
    Label _who,_next;
    bool _resolved,_talking;
    readonly List<(string who,string text)> _lines=new();
    int _lineIndex;
    // 選完之後先放對話，對話播完才顯示結果
    bool _good;int _actual,_amount;RMZBattleEventOption _picked;
    ulong _opened;
    Texture2D _icons;
    Color _fill,_border,_nameFill,_speaker;
    bool _magic;   // 鈴音：藍紫外框＋魔力光暈＋亮晶晶

    StyleBoxFlat MagicStyle(Color fill,int radius=12){var sb=RMZMagicFrame.Style(fill,radius);RMZMagicFrame.Pulse(this,sb);return sb;}

    static readonly Color Soft=new(0.80f,0.78f,0.74f), Calm=new(0.62f,0.72f,0.86f), Dim=new(.65f,.65f,.68f), Lack=new(1f,.45f,.4f);
    static StyleBoxFlat Box(Color bg,Color border,int bw=2,int r=12,int m=0)=>new(){BgColor=bg,BorderColor=border,BorderWidthLeft=bw,BorderWidthRight=bw,BorderWidthTop=bw,BorderWidthBottom=bw,CornerRadiusTopLeft=r,CornerRadiusTopRight=r,CornerRadiusBottomLeft=r,CornerRadiusBottomRight=r,ContentMarginLeft=m,ContentMarginRight=m,ContentMarginTop=m,ContentMarginBottom=m};

    const float PanelX=24,PanelY=24,PanelW=812;
    const float BoxX=24,BoxY=470,BoxW=1232,BoxH=226;
    const float CardGap=16,CardW=(BoxW-48-CardGap*2)/3,CardH=142,CardY=BoxY+30;

    public override void _Ready()
    {
        Layer=800;ProcessMode=ProcessModeEnum.Always;_opened=Time.GetTicksMsec();
        (_fill,_border,_nameFill,_speaker)=RMZWindowSkin.DialogueColors(Event.Character,Event.Character);
        _magic=RMZMagicFrame.IsSuzune(Event.Character);
        if(_magic){_border=RMZMagicFrame.Border;_speaker=RMZMagicFrame.Speaker;_nameFill=RMZMagicFrame.NameFill;}
        var iconPath=ProjectSettings.GlobalizePath("res://rmz/img/system/IconSet.png");if(File.Exists(iconPath))_icons=ImageTexture.CreateFromImage(Image.LoadFromFile(iconPath));
        var screen=new Control {MouseFilter=Control.MouseFilterEnum.Stop};AddChild(screen);screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var dim=new ColorRect {Color=new Color(0,0,0,.6f)};screen.AddChild(dim);dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _stage=new Control {Size=new Vector2(1280,720),ClipContents=true,TextureFilter=CanvasItem.TextureFilterEnum.Linear};screen.AddChild(_stage);
        void Fit(){var size=GetViewport().GetVisibleRect().Size;float scale=Mathf.Min(size.X/1280,size.Y/720);_stage.Scale=Vector2.One*scale;_stage.Position=(size-new Vector2(1280,720)*scale)/2;}
        Fit();screen.Resized+=Fit;

        // 立繪：RM 的立繪是整張 1280×720 畫布、人物畫在右半邊，原位置畫就好（不另外位移）
        foreach(var path in new[]{Event.Base,Event.Face}){
            var tex=RMZImageCache.LoadPicture(path); if(tex==null)continue;
            var portrait=new TextureRect {Texture=tex,Position=Vector2.Zero,Size=new Vector2(1280,720),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore};_stage.AddChild(portrait);
        }

        // ---- 上框：標題＋情境 ----
        _top=new Panel {Position=new Vector2(PanelX,PanelY),Size=new Vector2(PanelW,160),MouseFilter=Control.MouseFilterEnum.Ignore};
        _top.AddThemeStyleboxOverride("panel",_magic?MagicStyle(_fill):RMZWindowSkin.PanelStyle(_fill,_border));_stage.AddChild(_top);
        _heading=Label(Event.Title,new Rect2(PanelX+24,PanelY+14,PanelW-48,36),28,_speaker);
        var line=new ColorRect {Color=new Color(_border.R,_border.G,_border.B,0.5f),Position=new Vector2(PanelX+24,PanelY+58),Size=new Vector2(PanelW-48,2),MouseFilter=Control.MouseFilterEnum.Ignore};_stage.AddChild(line);
        _scene=Label(Event.Scene,new Rect2(PanelX+24,PanelY+72,PanelW-48,200),24,RMZWindowSkin.Text);
        int lines=Math.Max(1,_scene.GetLineCount());
        _scene.Size=new Vector2(PanelW-48,lines*36);
        _top.Size=new Vector2(PanelW,Mathf.Max(150,72+lines*36+24));
        if(_magic)_topSparks=RMZMagicFrame.Sparkles(_top,_top.Size,34);

        // ---- 下框：三張選項卡 ----
        var box=new Panel {Position=new Vector2(BoxX,BoxY),Size=new Vector2(BoxW,BoxH),MouseFilter=Control.MouseFilterEnum.Ignore};
        box.AddThemeStyleboxOverride("panel",_magic?MagicStyle(_fill):RMZWindowSkin.PanelStyle(_fill,_border));_stage.AddChild(box);
        if(_magic)RMZMagicFrame.Sparkles(box,box.Size,24);
        var plate=new Panel {Position=new Vector2(BoxX+20,BoxY-22),Size=new Vector2(190,40),MouseFilter=Control.MouseFilterEnum.Ignore};
        plate.AddThemeStyleboxOverride("panel",RMZWindowSkin.PanelStyle(_nameFill,_border));_stage.AddChild(plate);
        _who=Label(Event.Character,new Rect2(BoxX+20,BoxY-22,190,40),22,_speaker);_who.HorizontalAlignment=HorizontalAlignment.Center;_who.VerticalAlignment=VerticalAlignment.Center;
        _story=Label("",new Rect2(BoxX+32,BoxY+36,BoxW-64,130),26,RMZWindowSkin.Text);_story.Visible=false;
        _next=Label("▼ 點擊繼續",new Rect2(BoxX+BoxW-200,BoxY+BoxH-40,176,26),16,Dim);_next.HorizontalAlignment=HorizontalAlignment.Right;_next.Visible=false;

        bool anyItem=false;
        for(int i=0;i<3;i++){
            int index=i;var option=Event.Options[i];
            var button=new Button {Position=new Vector2(BoxX+24+i*(CardW+CardGap),CardY),Size=new Vector2(CardW,CardH),FocusMode=Control.FocusModeEnum.All};
            var cardFill=new Color(_fill.R*0.8f+0.04f,_fill.G*0.8f+0.04f,_fill.B*0.8f+0.04f,0.96f);
            button.AddThemeStyleboxOverride("normal",Box(cardFill,new Color(_border.R,_border.G,_border.B,0.55f),1,10));
            button.AddThemeStyleboxOverride("hover",Box(cardFill.Lightened(0.12f),_speaker,2,10));
            button.AddThemeStyleboxOverride("focus",Box(new Color(0,0,0,0),_speaker,2,10));
            button.AddThemeStyleboxOverride("pressed",Box(cardFill.Lightened(0.2f),_speaker,2,10));
            button.AddThemeStyleboxOverride("disabled",Box(new Color(0.10f,0.10f,0.11f,0.9f),new Color(0.25f,0.25f,0.27f),1,10));
            _stage.AddChild(button);_choices.Add(button);
            bool needItem=option.ItemId>0;anyItem|=needItem;
            bool lacking=needItem&&RMZParty.NumItems(option.ItemId)<1;
            button.Disabled=lacking;
            button.Pressed+=()=>Resolve(index);

            // 左上角小編號
            var badge=new Panel {Position=new Vector2(10,10),Size=new Vector2(30,30),MouseFilter=Control.MouseFilterEnum.Ignore};
            badge.AddThemeStyleboxOverride("panel",Box(lacking?new Color(0.25f,0.25f,0.27f):new Color(_border.R*0.6f,_border.G*0.6f,_border.B*0.6f),_speaker,2,15));button.AddChild(badge);
            var num=ChildLabel(button,(i+1).ToString(),new Rect2(10,10,30,30),17,Colors.White);num.HorizontalAlignment=HorizontalAlignment.Center;num.VerticalAlignment=VerticalAlignment.Center;

            // 選項名稱：放大、置中（不需要道具就佔滿整張卡的高度置中）
            var titleRect=needItem?new Rect2(44,8,CardW-88,74):new Rect2(44,0,CardW-88,CardH);
            var title=ChildLabel(button,option.Label,titleRect,26,lacking?new Color(.55f,.55f,.58f):Colors.White);
            title.ClipText=false;title.AutowrapMode=TextServer.AutowrapMode.WordSmart;
            title.HorizontalAlignment=HorizontalAlignment.Center;title.VerticalAlignment=VerticalAlignment.Center;

            // 需要的道具：圖示＋名稱置中，下面一行持有數也置中
            if(needItem){
                var row=new HBoxContainer {Position=new Vector2(0,86),Size=new Vector2(CardW,30),Alignment=BoxContainer.AlignmentMode.Center,MouseFilter=Control.MouseFilterEnum.Ignore};
                row.AddThemeConstantOverride("separation",6);button.AddChild(row);
                var data=RMZDatabase.Item(option.ItemId);
                if(_icons!=null&&data!=null)row.AddChild(new TextureRect {Texture=new AtlasTexture{Atlas=_icons,Region=new Rect2((data.iconIndex%16)*32,(data.iconIndex/16)*32,32,32)},CustomMinimumSize=new Vector2(28,28),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore});
                row.AddChild(PlainLabel($"{ItemName(option.ItemId)} ×1",20,lacking?Lack:Soft));
                var have=ChildLabel(button,$"持有 {RMZParty.NumItems(option.ItemId)}",new Rect2(0,114,CardW,22),16,lacking?Lack:Dim);
                have.HorizontalAlignment=HorizontalAlignment.Center;
            }
            // 成功率與好感增加量不顯示給玩家（讓選擇靠直覺，不是算數字）
        }
        _notice=Label(anyItem?"道具選項不論成功或失敗都會消耗 1 個道具。":"",new Rect2(BoxX+24,CardY+CardH+6,BoxW-48,24),15,Dim);
        _notice.HorizontalAlignment=HorizontalAlignment.Center;

        _continue=new Button {Text="返回公會",Position=new Vector2(BoxX+BoxW-24-240,BoxY+BoxH-16-50),Size=new Vector2(240,50),Visible=false};
        _continue.AddThemeFontOverride("font",RMZFonts.Main);_continue.AddThemeFontSizeOverride("font_size",22);
        _continue.AddThemeStyleboxOverride("normal",_magic?MagicStyle(new Color(0.20f,0.14f,0.42f,1f),10):Box(new Color(_border.R*0.5f,_border.G*0.5f,_border.B*0.5f,1f),_speaker,2,10));
        _continue.AddThemeStyleboxOverride("hover",Box(new Color(_border.R*0.7f,_border.G*0.7f,_border.B*0.7f,1f),_speaker,2,10));
        _continue.AddThemeStyleboxOverride("focus",Box(new Color(0,0,0,0),Colors.White,2,10));
        _continue.AddThemeStyleboxOverride("pressed",Box(new Color(_border.R*0.4f,_border.G*0.4f,_border.B*0.4f,1f),_speaker,2,10));
        _stage.AddChild(_continue);
        _continue.Pressed+=()=>{if(_resolved&&Time.GetTicksMsec()-_opened>=200)Finished=true;};
        (_choices.Find(b=>!b.Disabled)??_choices[0]).GrabFocus();
    }
    static string ItemName(int id)=>RMZLocalization.Translate(RMZDatabase.Item(id)?.name??"道具");
    Label Label(string text,Rect2 rect,int size,Color color){var l=new Label {Text=text,Position=rect.Position,Size=rect.Size,AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=Control.MouseFilterEnum.Ignore};l.AddThemeFontOverride("font",RMZFonts.Main);l.AddThemeFontSizeOverride("font_size",size);l.AddThemeColorOverride("font_color",color);_stage.AddChild(l);return l;}
    static Label ChildLabel(Control parent,string text,Rect2 rect,int size,Color color){var l=new Label {Text=text,Position=rect.Position,Size=rect.Size,ClipText=true,MouseFilter=Control.MouseFilterEnum.Ignore};l.AddThemeFontOverride("font",RMZFonts.Main);l.AddThemeFontSizeOverride("font_size",size);l.AddThemeColorOverride("font_color",color);parent.AddChild(l);return l;}
    static Label PlainLabel(string text,int size,Color color){var l=new Label {Text=text,VerticalAlignment=VerticalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};l.AddThemeFontOverride("font",RMZFonts.Main);l.AddThemeFontSizeOverride("font_size",size);l.AddThemeColorOverride("font_color",color);return l;}
    static readonly Color HeroColor=new(0.55f,0.72f,1.00f);

    /// <summary>男主的名字（玩家取的）。取不到就用「你」。</summary>
    static string HeroName(){var n=RMZTextWindowLayer.ConvertCodes("\\N[1]");return string.IsNullOrWhiteSpace(n)||n.Contains("[")?"你":n;}

    void Resolve(int index)
    {
        if(_resolved||Time.GetTicksMsec()-_opened<200)return;
        var option=Event.Options[index];
        if(option.ItemId>0&&RMZParty.NumItems(option.ItemId)<1){_choices[index].Disabled=true;_notice.Text="道具數量不足，請選擇其他選項。";return;}
        _resolved=true;_picked=option;
        if(option.ItemId>0)RMZParty.GainItem(option.ItemId,-1);
        _good=GD.RandRange(1,100)<=option.Chance;
        _amount=_good?(int)GD.RandRange(option.Min,option.Max):option.Bad;
        int before=RMZGifts.Love(Event.Heroine);RMZGifts.AddLove(Event.Heroine,_amount);
        int after=RMZGifts.Love(Event.Heroine);_actual=after-before;RMZS.SVTalk.Love=after;
        foreach(var button in _choices)button.Visible=false;
        _notice.Visible=false;

        // 上框：選了什麼＋發生了什麼（第一行旁白）
        string outcome=_good?option.GoodText:option.BadText;
        var parts=outcome.Split('\n');
        _scene.Visible=false;
        _y=PanelY+68;
        Label(option.Label,new Rect2(PanelX+24,_y,PanelW-48,26),19,Soft);_y+=32;
        var narr=Label(parts[0],new Rect2(PanelX+24,_y,PanelW-48,200),22,RMZWindowSkin.Text);
        int nl=Math.Max(1,narr.GetLineCount());narr.Size=new Vector2(PanelW-48,nl*32);_y+=nl*32+10;
        FitTop();

        // 下框：一句一句放對話
        _lines.Clear();
        string hero=HeroName();
        for(int i=1;i<parts.Length;i++){
            string l=parts[i].Trim();if(l.Length==0)continue;
            int c=l.IndexOf('：');
            if(c>0&&c<12){string who=l.Substring(0,c);_lines.Add((who=="主角"?hero:who,l.Substring(c+1)));}
            else _lines.Add(("",l));
        }
        _story.Visible=true;
        _lineIndex=0;_talking=_lines.Count>0;
        if(_talking)ShowLine();else ShowResult();
        _opened=Time.GetTicksMsec();
    }

    float _y;
    void FitTop(){_top.Size=new Vector2(PanelW,_y-PanelY+14);RMZMagicFrame.Fit(_topSparks,_top.Size);}

    void ShowLine()
    {
        var (who,text)=_lines[_lineIndex];
        bool heroine=who==Event.Character||who.Length==0;
        _who.Text=who.Length==0?Event.Character:who;
        _who.AddThemeColorOverride("font_color",heroine?_speaker:HeroColor);
        _story.Text=text;
        _next.Visible=true;_next.MoveToFront();
    }

    void Advance()
    {
        if(!_talking||Time.GetTicksMsec()-_opened<150)return;
        _opened=Time.GetTicksMsec();
        _lineIndex++;
        if(_lineIndex<_lines.Count){ShowLine();RMZAudio.Instance?.PlaySeByName("Cursor1");return;}
        _talking=false;ShowResult();
    }

    /// <summary>對話講完才揭曉：好事件／小插曲、好感有沒有變。</summary>
    void ShowResult()
    {
        _next.Visible=false;
        float x=PanelX+24,w=PanelW-48;
        var titleColor=_good?(_magic?new Color(0.88f,0.82f,1f):_speaker):Calm;
        Label(_good?"★ 好事件！":"小插曲……",new Rect2(x,_y,w,42),30,titleColor);_y+=46;
        // 結果不秀數字，只說好感有沒有變
        Label(_actual>0?"♥ 好感度上升了":_actual<0?"好感度下降了……":"好感度沒有變化",new Rect2(x,_y,w,34),24,_actual>0?new Color(1f,.6f,.72f):_actual<0?new Color(.7f,.75f,.85f):Soft);_y+=38;
        string extra="";
        if(_good&&_actual<_amount)extra="已達目前好感上限。";
        if(Event.Heroine==4)extra+=(extra.Length>0?"　":"")+"雪瑤與雪霜共用好感度。";
        if(extra.Length>0){Label(extra,new Rect2(x,_y,w,24),15,new Color(.62f,.62f,.66f));_y+=28;}
        FitTop();
        if(!_good&&!_magic)_top.AddThemeStyleboxOverride("panel",RMZWindowSkin.PanelStyle(_fill,Calm));

        if(_picked.ItemId>0){_notice.Text="已消耗 "+ItemName(_picked.ItemId)+" ×1。";_notice.HorizontalAlignment=HorizontalAlignment.Left;_notice.Position=new Vector2(BoxX+32,BoxY+BoxH-44);_notice.Size=new Vector2(600,24);_notice.Visible=true;}
        _continue.Visible=true;_continue.MoveToFront();_continue.GrabFocus();_opened=Time.GetTicksMsec();
        RMZAudio.Instance?.PlaySeByName(_good?"Item3":"Cancel1");
        RMZEventRunner.Instance?.RequestRefresh();
    }
    public override void _Input(InputEvent input)
    {
        // 對話中：左鍵點哪裡都算下一句
        if(_talking&&input is InputEventMouseButton mb&&mb.Pressed&&mb.ButtonIndex==MouseButton.Left){GetViewport().SetInputAsHandled();Advance();return;}
        if(input is not InputEventKey key||!key.Pressed||key.Echo)return;
        if(key.Keycode==Key.Escape){GetViewport().SetInputAsHandled();return;}
        if(key.Keycode!=Key.Key1&&key.Keycode!=Key.Key2&&key.Keycode!=Key.Key3&&key.Keycode!=Key.Enter&&key.Keycode!=Key.Space&&key.Keycode!=Key.KpEnter)return;
        GetViewport().SetInputAsHandled();
        if(_talking){if(key.Keycode==Key.Enter||key.Keycode==Key.Space||key.Keycode==Key.KpEnter)Advance();return;}
        if(Time.GetTicksMsec()-_opened<200)return;
        if(_resolved){Finished=true;return;}
        if(key.Keycode==Key.Key1||key.Keycode==Key.Key2||key.Keycode==Key.Key3){Resolve((int)key.Keycode-(int)Key.Key1);return;}
        for(int i=0;i<_choices.Count;i++)if(_choices[i].HasFocus()&&!_choices[i].Disabled){Resolve(i);break;}
    }
}
