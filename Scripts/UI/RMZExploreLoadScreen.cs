using Godot;
using RMZS;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
namespace RMZ;
public partial class RMZExploreLoadScreen : CanvasLayer
{
    static RMZExploreLoadScreen instance;
    TextureRect player;
    AudioStreamPlayer loadingMusic;
    Label words,name,caption;
    int heroine,frame=-1;
    double elapsed;
    Panel bubble;
    readonly Dictionary<string,TextureRect> actors=new();
    readonly List<(string speaker,string text)> conversation=new();
    public static void Show(int who,string title="準備出發")
    {
        Hide(); if(RMZBootstrap.Instance==null)return;
        instance=new RMZExploreLoadScreen { Layer=3000,heroine=who,ProcessMode=ProcessModeEnum.Always };
        RMZBootstrap.Instance.GetTree().Root.AddChild(instance);
    }
    public new static void Hide(){if(GodotObject.IsInstanceValid(instance)){instance.loadingMusic?.Stop();instance.QueueFree();}instance=null;}
    public static async Task PlayIntro(int who)
    {
        Show(who); var view=instance; if(view==null)return;
        for(int i=0;i<view.conversation.Count;i++)
        {
            view.ShowLine(i); await view.ToSignal(view.GetTree().CreateTimer(who>0?2.6:1.8),SceneTreeTimer.SignalName.Timeout);
            if(!GodotObject.IsInstanceValid(view)||view!=instance)return;
        }
        await view.ToSignal(view.GetTree(),SceneTree.SignalName.ProcessFrame);
    }
    public override void _Ready()
    {
        var screen=new ColorRect { Color=Colors.Black };AddChild(screen);screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var stage=new Control { MouseFilter=Control.MouseFilterEnum.Ignore };screen.AddChild(stage);
        stage.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);stage.OffsetLeft=-640;stage.OffsetTop=-360;stage.OffsetRight=640;stage.OffsetBottom=360;
        player=SpriteView(stage,new Vector2(heroine==4?204:heroine>0?344:544,290));actors["player"]=player;
        if(heroine>0) {
            var members=SVParties.AloneParty((SVHeroine)heroine);
            for(int i=0;i<members.Count;i++)actors[members[i].Name]=SpriteView(stage,new Vector2(heroine==4?544+i*340:744,290));
        }
        bubble=new Panel { Size=new Vector2(440,156) };stage.AddChild(bubble);
        bubble.AddThemeStyleboxOverride("panel",new StyleBoxFlat { BgColor=new Color(.07f,.08f,.12f,.97f),BorderColor=new Color(.82f,.70f,.38f),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,CornerRadiusTopLeft=12,CornerRadiusTopRight=12,CornerRadiusBottomLeft=12,CornerRadiusBottomRight=12 });
        var tail=new Polygon2D { Polygon=new[]{new Vector2(207,155),new Vector2(233,155),new Vector2(220,175)},Color=new Color(.82f,.70f,.38f) };bubble.AddChild(tail);
        name=Text(bubble,new Rect2(20,10,400,30),22);name.Modulate=new Color(1,.88f,.6f);
        words=Text(bubble,new Rect2(20,44,400,102),26);words.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        caption=Text(stage,new Rect2(240,620,800,40),22);
        // 「加載中」固定不動，只有後面的點一格一格長出來：文字靠左，起點對齊「加載中...」置中時的位置
        caption.HorizontalAlignment=HorizontalAlignment.Left;
        float full=RMZFonts.Main.GetStringSize(LoadingText+"...",HorizontalAlignment.Left,-1,22).X;
        caption.Position=new Vector2(640-full/2,620);
        caption.Text=LoadingText+"."; 
        LoadConversation();ShowLine(0);
        loadingMusic=new AudioStreamPlayer { Stream=RMZLoadingMusic.Stream, ProcessMode=ProcessModeEnum.Always };
        AddChild(loadingMusic);UpdateMusicVolume();loadingMusic.Play();
    }
    void LoadConversation()
    {
        try {
            var data=JObject.Parse(File.ReadAllText(Path.Combine(RMZConfig.RootPath,"loading_talk.json")));
            int love=heroine>0?RMZGifts.Love(heroine):0;
            string key=heroine==4&&love<35?"4_low":heroine==5?"5_"+(love>=70?"high":love>=35?"mid":"low"):heroine.ToString();
            var options=(JArray)data[key];var chosen=(JArray)options[GD.RandRange(0,options.Count-1)];
            foreach(JArray line in chosen)if(actors.ContainsKey(line[0].ToString()))conversation.Add((line[0].ToString(),line[1].ToString()));
        }catch(System.Exception e){GD.PushWarning("載入對話讀取失敗："+e.Message);}
        if(conversation.Count==0)conversation.Add(("player","準備好了，出發吧。"));
    }
    void ShowLine(int index)
    {
        var line=conversation[index];var speaker=actors[line.speaker];
        name.Text=line.speaker=="player"?RMZLocalization.Translate(RMZDatabase.Actor(1)?.name??"男主"):line.speaker;
        words.Text=line.text;
        bubble.Position=new Vector2(speaker.Position.X+96-220,speaker.Position.Y-192);
        foreach(var actor in actors)actor.Value.Modulate=actor.Key==line.speaker?Colors.White:new Color(.72f,.72f,.78f);
    }
    const string LoadingText="加載中";
    const string ReadyText="點擊螢幕或按任意鍵開始";
    bool ready;
    TaskCompletionSource<bool> started;

    /// <summary>
    /// 載入完了：文字換成「點擊螢幕或按任意鍵開始」，等玩家按一下才回來。
    /// 畫面沒開著（例如已經被關掉）就直接放行。
    /// </summary>
    public static async Task WaitForStart()
    {
        var view=instance; if(view==null||!GodotObject.IsInstanceValid(view))return;
        view.ready=true; view.started=new TaskCompletionSource<bool>();
        view.caption.HorizontalAlignment=HorizontalAlignment.Center;
        view.caption.Position=new Vector2(240,620);
        view.caption.Text=ReadyText;
        await view.started.Task;
        // 等按鍵／滑鼠放開再開打，不然這一下會被戰鬥畫面當成「點地板走路」
        while(GodotObject.IsInstanceValid(view)&&(Input.IsMouseButtonPressed(MouseButton.Left)||Input.IsMouseButtonPressed(MouseButton.Right)))
            await view.ToSignal(view.GetTree(),SceneTree.SignalName.ProcessFrame);
    }
    public override void _Input(InputEvent e)
    {
        if(!ready||started==null)return;
        bool pressed=(e is InputEventKey k&&k.Pressed&&!k.Echo)||(e is InputEventMouseButton m&&m.Pressed)||
                     (e is InputEventScreenTouch t&&t.Pressed)||(e is InputEventJoypadButton j&&j.Pressed);
        if(!pressed)return;
        GetViewport().SetInputAsHandled();
        started.TrySetResult(true);
    }
    public override void _Process(double delta)
    {
        UpdateMusicVolume();
        elapsed+=delta;
        if(!ready)caption.Text=LoadingText+new string('.',(int)(elapsed/.45)%3+1);
        else caption.Modulate=new Color(1,1,1,.55f+.45f*Mathf.Abs(Mathf.Sin((float)elapsed*2.4f)));   // 提示字慢慢閃
        int f=(int)(elapsed/.16)%4;if(f==frame)return;frame=f;int p=new[]{0,1,2,1}[f];
        // RM 方向 2 是各角色區塊最上排：面向玩家的正面行走動畫。
        player.Texture=SVSpriteSheet.Get("男主1",3,2,p)?.Native;
        foreach(var actor in actors)if(actor.Key!="player") {var type=SVPortraitLayout.Character(actor.Key);if(type!=null)actor.Value.Texture=SVSpriteSheet.Get(type.SheetName,type.SheetIndex,2,p)?.Native;}
    }
    static TextureRect SpriteView(Control parent,Vector2 at){var t=new TextureRect { Position=at,Size=new Vector2(192,192),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,TextureFilter=CanvasItem.TextureFilterEnum.Nearest,MouseFilter=Control.MouseFilterEnum.Ignore };parent.AddChild(t);return t;}
    static Label Text(Control parent,Rect2 rect,int size){var t=new Label { Position=rect.Position,Size=rect.Size,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore };parent.AddChild(t);t.AddThemeFontOverride("font",RMZFonts.Main);t.AddThemeFontSizeOverride("font_size",size);return t;}
    void UpdateMusicVolume(){if(loadingMusic!=null){float gain=RMZOptions.BgmScale*.4f*.45f;loadingMusic.VolumeDb=gain>0?Mathf.LinearToDb(gain):-80f;}}
    public override void _ExitTree(){loadingMusic?.Stop();started?.TrySetResult(false);if(instance==this)instance=null;}
}
