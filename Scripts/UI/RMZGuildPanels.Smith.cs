using Godot;
using System;
using System.Collections.Generic;
using System.IO;
namespace RMZ;

public partial class RMZGuildPanels
{
    Control _smithStage;
    readonly List<Button> _smithScrolls=new();
    readonly List<Label> _smithCounts=new();
    readonly List<TextureRect> _smithScrollArts=new();
    bool _taughtSmithScrolls,_taughtSmithAction;
    TextureRect _smithEquipIcon,_smithScrollIcon;
    Label _smithStats,_smithGold,_smithEquipName,_smithScrollName;
    int _smithUid;
    bool _smithFromWorn;
    readonly List<Button> _smithSources=new();
    static readonly string[] ScrollPictures={"scroll weapon 100%","scroll weapon 50%","scroll weapon 20%","scroll armor 100%","scroll armor 50%","scroll armor 20%","reset scroll"};
    void BuildSmith()
    {
        var screen=new Control {MouseFilter=Control.MouseFilterEnum.Stop};AddChild(screen);screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var dim=new ColorRect {Color=new Color(0,0,0,.58f)};screen.AddChild(dim);dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _smithStage=new Control {Size=new Vector2(1280,720),TextureFilter=CanvasItem.TextureFilterEnum.Nearest};screen.AddChild(_smithStage);
        void Fit(){var size=GetViewport().GetVisibleRect().Size;float s=Mathf.Min(size.X/1280,size.Y/720);_smithStage.Scale=Vector2.One*s;_smithStage.Position=(size-new Vector2(1280,720)*s)/2;}
        Fit();screen.Resized+=Fit;
        var iconPath=ProjectSettings.GlobalizePath("res://rmz/img/system/IconSet.png");if(File.Exists(iconPath))_icons=ImageTexture.CreateFromImage(Image.LoadFromFile(iconPath));
        SmithArt("UI button/bag ui/weapon defend equip background",new Rect2(54,25,466,675));
        SmithArt("UI button/bag ui/equipment bag background",new Rect2(534,15,734,690));
        SmithArt("UI button/bag ui/enhance paper",new Rect2(553,92,698,309));
        var close=SmithButton("UI button/Exit button",new Rect2(18,0,76,76));close.TooltipText="返回鐵匠鋪";close.Pressed+=()=>{if(!_enhancing&&!RMZTutorialOverlay.IsOpen)QueueFree();};
        SmithLabel("裝備強化",new Rect2(610,26,330,46),30);
        _smithGold=SmithLabel("",new Rect2(1000,32,225,42),24);
        for(int i=0;i<2;i++){
            bool wornSource=i==1;
            var source=SmithButton("UI button/bag ui/scroll or equipment",new Rect2(94+i*202,57,188,46));
            SmithLabel(wornSource?"已裝備":"背包",new Rect2(94+i*202,60,188,40),23);
            _smithSources.Add(source);
            source.Pressed+=()=>{
                if(_enhancing||RMZTutorialOverlay.IsOpen||_smithFromWorn==wornSource)return;
                _smithFromWorn=wornSource;_smithUid=0;_scrollId=0;RefreshSmith();
                _notice.Text=wornSource?"選擇身上穿戴的裝備進行強化。":"選擇背包內的裝備進行強化。";
            };
        }
        _smithRows=BuildEquipmentRows(new Rect2(94,120,390,520),72,4);
        SmithLabel("強化裝備",new Rect2(635,110,220,35),22);
        SmithLabel("使用捲軸",new Rect2(930,110,220,35),22);
        SmithArt("UI button/bag ui/equipment bag",new Rect2(690,153,94,86));
        SmithArt("UI button/bag ui/equipment bag",new Rect2(986,153,94,86));
        _smithEquipIcon=SmithArt(null,new Rect2(707,166,60,60));_smithScrollIcon=SmithArt(null,new Rect2(1003,166,60,60));
        _smithEquipName=SmithLabel("",new Rect2(604,243,267,32),21);
        _smithScrollName=SmithLabel("",new Rect2(906,243,282,32),21);
        _smithStats=SmithLabel("",new Rect2(595,280,610,106),21);
        for(int i=0;i<6;i++){
            int index=i;float x=572+i*114;
            var button=SmithButton("UI button/bag ui/equipment bag",new Rect2(x,413,94,86));_smithScrolls.Add(button);
            _smithScrollArts.Add(SmithArt("UI button/scroll ui/"+ScrollPictures[i],new Rect2(x+14,423,64,64)));
            _smithCounts.Add(SmithLabel("",new Rect2(x-2,497,98,36),21));
            button.Pressed+=()=>{if(_enhancing||RMZTutorialOverlay.IsOpen)return;_scrollId=107+index;RefreshSmith();TeachSmithAction();};
        }
        var reset=SmithButton("UI button/bag ui/equipment bag",new Rect2(584,545,78,72));_smithScrolls.Add(reset);
        _smithScrollArts.Add(SmithArt("UI button/scroll ui/reset scroll",new Rect2(594,552,56,56)));
        _smithCounts.Add(SmithLabel("",new Rect2(670,548,150,60),20));
        reset.Pressed+=()=>{if(_enhancing||RMZTutorialOverlay.IsOpen)return;_scrollId=113;RefreshSmith();TeachSmithAction();};
        _action=SmithButton("UI button/bag ui/upgrade button",new Rect2(926,547,230,79));_action.Pressed+=EnhanceSmith;
        _notice=SmithLabel("選擇左側裝備，再選擇捲軸。",new Rect2(565,645,670,48),20);
        _smithUid=0;
        _smithFromWorn=RMZEquipment.Owned.Exists(e=>RMZEquipment.IsWorn(e));
        RefreshSmith();
        if(RMZGameState.Variable(4)==1) CallDeferred(nameof(TeachSmith));
    }
    TextureRect SmithArt(string picture,Rect2 rect)
    {
        var image=new TextureRect {Texture=picture==null?null:RMZImageCache.LoadPicture(picture),Position=rect.Position,Size=rect.Size,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=Control.MouseFilterEnum.Ignore};_smithStage.AddChild(image);return image;
    }
    Label SmithLabel(string text,Rect2 rect,int font)
    {
        var label=new Label {Text=text,Position=rect.Position,Size=rect.Size,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=Control.MouseFilterEnum.Ignore};
        label.AddThemeFontOverride("font",RMZFonts.Main);label.AddThemeFontSizeOverride("font_size",font);label.AddThemeColorOverride("font_color",new Color("fff3cc"));label.AddThemeColorOverride("font_outline_color",new Color("35200d"));label.AddThemeConstantOverride("outline_size",5);_smithStage.AddChild(label);return label;
    }
    Button SmithButton(string picture,Rect2 rect)
    {
        var b=new Button {Position=rect.Position,Size=rect.Size,FocusMode=Control.FocusModeEnum.All};_smithStage.AddChild(b);
        var texture=RMZImageCache.LoadPicture(picture);
        foreach(var state in new[]{"normal","hover","pressed","disabled"})b.AddThemeStyleboxOverride(state,new StyleBoxTexture {Texture=texture,ModulateColor=state=="hover"?new Color(1.18f,1.12f,.9f):state=="disabled"?new Color(.6f,.6f,.6f):Colors.White});
        b.AddThemeStyleboxOverride("focus",new StyleBoxFlat {BgColor=Colors.Transparent,BorderColor=new Color("ffe085"),BorderWidthLeft=3,BorderWidthRight=3,BorderWidthTop=3,BorderWidthBottom=3});return b;
    }
    void RefreshSmith()
    {
        var equip=RMZEquipment.Find(_smithUid);
        if(equip!=null && RMZEquipment.IsWorn(equip)!=_smithFromWorn){equip=null;_smithUid=0;_scrollId=0;}
        for(int i=0;i<2;i++){_smithSources[i].Disabled=_enhancing;_smithSources[i].Modulate=(i==1)==_smithFromWorn?new Color(1.2f,1.1f,.7f):new Color(.8f,.8f,.8f);}
        FillEquipmentRows(_smithRows,RMZEquipment.Sorted().FindAll(e=>RMZEquipment.IsWorn(e)==_smithFromWorn),ChooseSmithEquipment);
        HighlightEquipmentRows(_smithRows,_smithUid);
        _smithGold.Text=RMZParty.Gold+" G";
        _smithEquipIcon.Texture=equip==null?null:Icon(equip.IconIndex);_smithEquipName.Text=equip==null?"尚未選擇":(_smithFromWorn?"[已裝備] ":"[背包] ")+equip.Name;
        bool validScroll=RMZEquipment.ScrollFor(_scrollId,out var scroll);
        _smithScrollIcon.Texture=validScroll?RMZImageCache.LoadPicture("UI button/scroll ui/"+ScrollPictures[_scrollId-107]):null;
        _smithScrollName.Text=validScroll?RMZLocalization.Translate(RMZDatabase.Item(_scrollId)?.name??""):equip==null?"先選擇裝備":"請選擇下方捲軸";
        _smithStats.Text=equip==null?"左側依部位分列，點選格子中的裝備。\n同類裝備較多時，可拖動該列下方的拖動條。":$"物攻 {equip.Atk}　魔攻 {equip.MAtk}　生命 {equip.MaxHp}\n物防 {equip.Def}　魔防 {equip.MDef}　剩餘次數 {equip.SlotsLeft}\n"+(validScroll?(scroll.IsReset?"回真：重置強化次數與加成":$"成功率 {Mathf.RoundToInt(scroll.SuccessRate*100)}%　成功加成 {Mathf.RoundToInt(scroll.Gain*100)}%"):"選擇捲軸查看成功率");
        for(int i=0;i<7;i++){
            bool visible=equip!=null&&(i==6||(i<3)==equip.IsWeapon);
            _smithScrolls[i].Visible=visible;_smithScrollArts[i].Visible=visible;_smithCounts[i].Visible=visible;
            if(i<6){float x=660+(i%3)*156;_smithScrolls[i].Position=new Vector2(x,413);_smithScrollArts[i].Position=new Vector2(x+14,423);_smithCounts[i].Position=new Vector2(x-2,497);}
            int n=RMZParty.NumItems(107+i);_smithCounts[i].Text=i==6?"回真捲軸 ×"+n:(i%3==0?"100%":i%3==1?"50%":"20%")+" ×"+n;_smithScrolls[i].Modulate=_scrollId==107+i?new Color(1.2f,1.1f,.65f):Colors.White;_smithScrolls[i].Disabled=n<=0;}
        _action.Disabled=_enhancing||equip==null||!validScroll||RMZParty.NumItems(_scrollId)<=0||!RMZEquipment.CanUse(scroll,equip,out _);
    }
    void ChooseSmithEquipment(int uid)
    {
        if(_enhancing||RMZTutorialOverlay.IsOpen)return;
        var equip=RMZEquipment.Find(uid);
        if(equip==null||RMZEquipment.IsWorn(equip)!=_smithFromWorn)return;
        _smithUid=uid;
        if(RMZEquipment.ScrollFor(_scrollId,out var scroll)&&!scroll.IsReset&&scroll.ForWeapon!=equip?.IsWeapon)_scrollId=0;
        RefreshSmith();
        _notice.Text="只顯示這件裝備適用的捲軸；回真捲軸可重置強化。";
        if(RMZGameState.Variable(4)==1&&!_taughtSmithScrolls){_taughtSmithScrolls=true;CallDeferred(nameof(TeachSmithScrolls));}
    }
    async void TeachSmith()
    {
        await RMZTutorialOverlay.Show("鐵匠","先切換「背包」或「已裝備」，再點選格子中的裝備。每列是一種部位，下方拖動條可查看其他裝備。",new Rect2(94,57,390,159));
    }
    async void TeachSmithScrolls()
    {
        await RMZTutorialOverlay.Show("鐵匠","選好裝備後，這裡才會出現適用的捲軸。數字是成功率，第一次先試試 100% 的吧。",new Rect2(653,408,426,126));
    }
    void TeachSmithAction()
    {
        if(RMZGameState.Variable(4)==1&&!_taughtSmithAction){_taughtSmithAction=true;CallDeferred(nameof(ShowSmithAction));}
    }
    async void ShowSmithAction()
    {
        await RMZTutorialOverlay.Show("鐵匠","裝備和捲軸都選好了，按這裡強化。成功或失敗都會消耗捲軸與一次強化機會。",new Rect2(926,547,230,79));
    }
    async void EnhanceSmith()
    {
        if(_enhancing||RMZTutorialOverlay.IsOpen)return;
        var equip=RMZEquipment.Find(_smithUid);
        if(equip==null||!RMZEquipment.ScrollFor(_scrollId,out var scroll)||RMZParty.NumItems(_scrollId)<=0)return;
        if(!RMZEquipment.CanUse(scroll,equip,out var reason)){_notice.Text=reason;return;}
        _enhancing=true;RefreshSmith();
        bool success=RMZEquipment.Enhance(scroll,equip,out var message);
        try{await PlayEnhanceEffect(success,scroll.IsReset,message);}
        finally{_enhancing=false;}
        _notice.Text=message;
        if(success&&RMZGameState.Variable(4)==1){
            // 教學模式：第一次強化成功後不用玩家自己按關閉，直接收起介面繼續推劇情。
            RMZGameState.SetVariable(4,2);
            _notice.Text="強化完成！";
            RefreshSmith();
            RMZEventRunner.Instance?.RequestRefresh();
            await ToSignal(GetTree().CreateTimer(0.9),SceneTreeTimer.SignalName.Timeout);
            QueueFree();
            return;
        }
        RMZEventRunner.Instance?.RequestRefresh();RefreshSmith();
    }
}
