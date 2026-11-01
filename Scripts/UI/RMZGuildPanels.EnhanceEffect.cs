using Godot;
using System.Threading.Tasks;
namespace RMZ;

public partial class RMZGuildPanels
{
    // 沿用 RM CE26：鐵砧、兩次預備錘擊，第三擊成功或斷錘。
    async Task PlayEnhanceEffect(bool success, bool reset, string message)
    {
        var overlay = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var dim = new ColorRect { Color = new Color(0,0,0,.84f), MouseFilter = Control.MouseFilterEnum.Stop };
        overlay.AddChild(dim); dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var stage = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        overlay.AddChild(stage);
        void Fit() {
            var viewport = GetViewport().GetVisibleRect().Size;
            float scale = Mathf.Min(viewport.X/1280f,viewport.Y/720f);
            stage.Scale = Vector2.One*scale;
            stage.Position = (viewport-new Vector2(1280,720)*scale)/2f;
        }
        Fit(); GetViewport().SizeChanged += Fit;
        TextureRect Art(string name, Vector2 pos) {
            var texture=RMZImageCache.LoadPicture("UI button/bag ui/"+name);
            var rect=new TextureRect { Texture=texture, Position=pos, Size=texture?.GetSize()??Vector2.Zero, MouseFilter=Control.MouseFilterEnum.Ignore };
            stage.AddChild(rect); return rect;
        }
        var anvil=Art("tie zhen",new Vector2(390,328));
        var hammer=Art("hammer",new Vector2(464,96));
        hammer.PivotOffset=new Vector2(108,244);
        var impact = new Vector2(640,340);
        // 錘面接觸點先繞握柄旋轉，再反推節點位置；不能直接把整張錘子往右移。
        var contact = new Vector2(154,146);
        var strikePosition = impact-hammer.PivotOffset-(contact-hammer.PivotOffset).Rotated(Mathf.DegToRad(38f));
        var title=new Label { Text=reset?"裝備回真":"裝備強化", Position=new Vector2(200,36), Size=new Vector2(880,52), HorizontalAlignment=HorizontalAlignment.Center };
        title.AddThemeFontOverride("font",RMZFonts.Main);title.AddThemeFontSizeOverride("font_size",34);stage.AddChild(title);
        async Task Pause(double seconds) => await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
        async Task Strike() {
            var down=CreateTween().SetParallel();
            down.TweenProperty(hammer,"position",strikePosition,.22).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            down.TweenProperty(hammer,"rotation_degrees",38f,.22);
            await ToSignal(down,Tween.SignalName.Finished);
            RMZAudio.Instance?.PlaySe(new AudioFile { name="Sword2", volume=100, pitch=120, pan=0 });
            // 用打點附近的一顆圓形閃光代替整塊鐵砧大小的矩形，不然會變成很突兀的「方塊」蓋在鐵砧上。
            var flashSize=new Vector2(150,150);
            var flash=new Panel {Position=impact-flashSize/2f,Size=flashSize,MouseFilter=Control.MouseFilterEnum.Ignore};
            var flashStyle=new StyleBoxFlat {BgColor=new Color(1f,.88f,.55f,.6f)};flashStyle.SetCornerRadiusAll(999);
            flash.AddThemeStyleboxOverride("panel",flashStyle);stage.AddChild(flash);
            var fade=CreateTween();fade.TweenProperty(flash,"modulate:a",0f,.2);fade.TweenCallback(Callable.From(()=>flash.QueueFree()));
            for(int i=0;i<10;i++) {
                var angle=i*Mathf.Tau/10f;
                var spark=new Polygon2D { Position=impact, Color=new Color(1f,.85f,.3f), Polygon=new[]{new Vector2(-3,0),new Vector2(0,-9),new Vector2(3,0),new Vector2(0,9)} };
                stage.AddChild(spark);
                var t=CreateTween().SetParallel();t.TweenProperty(spark,"position",spark.Position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*105f,.25);t.TweenProperty(spark,"modulate:a",0f,.25);
                t.Chain().TweenCallback(Callable.From(()=>spark.QueueFree()));
            }
            var up=CreateTween().SetParallel();up.TweenProperty(hammer,"position",new Vector2(464,96),.22);up.TweenProperty(hammer,"rotation_degrees",0f,.22);
            await ToSignal(up,Tween.SignalName.Finished);
        }
        try {
            await Strike(); await Strike(); await Strike();
            if(!success) {
                hammer.Hide();
                var head=Art("hammer broken",new Vector2(464,96));
                Art("hammer broken 2",new Vector2(464,96));
                head.PivotOffset=new Vector2(108,140);
                var fall=CreateTween().SetParallel();fall.TweenProperty(head,"position",new Vector2(544,320),.3);fall.TweenProperty(head,"rotation_degrees",-45f,.3);
                await ToSignal(fall,Tween.SignalName.Finished);
            }
            title.Text=reset?"回真完成":success?"強化成功！":"強化失敗";
            title.Modulate=success?new Color(1f,.86f,.4f):new Color(1f,.55f,.48f);
            var result=new Label {Text=message,Position=new Vector2(160,614),Size=new Vector2(960,70),HorizontalAlignment=HorizontalAlignment.Center,AutowrapMode=TextServer.AutowrapMode.WordSmart};
            result.AddThemeFontOverride("font",RMZFonts.Main);result.AddThemeFontSizeOverride("font_size",24);stage.AddChild(result);
            await Pause(.9);
        }
        finally { GetViewport().SizeChanged -= Fit; overlay.QueueFree(); }
    }
}
