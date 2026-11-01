using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using RMZS;
namespace RMZ;

// 公會 ITEM／EQUIP／STATUS 共用的原生視窗；資料沿用原本背包與人物設定。
public partial class RMZGuildPanels : CanvasLayer
{
    static RMZGuildPanels _instance;
    public static bool IsOpen => GodotObject.IsInstanceValid(_instance);
    public static void Open(bool status = false, int tab = 2)
    {
        if(IsOpen) return;
        _instance = new RMZGuildPanels { _status = status, _tab = tab, Layer = 90 };
        RMZBootstrap.Instance.GetTree().Root.AddChild(_instance);
    }
    public static void OpenSmith()
    {
        if(IsOpen) return;
        // 舊 RM 強化頁以變數 16 輪詢，原生視窗接手後必須停止。
        RMZGameState.SetVariable(16,0);
        RMZGameState.SetVariable(282,0);
        RMZScreen.Instance?.EraseAllPictures();
        RMZTextWindowLayer.Instance?.EraseAll();
        RMZEventRunner.Instance?.SetEventsPaused(false);
        _instance = new RMZGuildPanels { _smith = true, _tab = 1, Layer = 90 };
        RMZBootstrap.Instance.GetTree().Root.AddChild(_instance);
    }
    bool _smith;
    bool _enhancing;
    bool _status;
    int _tab, _selected = -1, _scrollId;
    ItemList _list;
    Label _detail, _notice;
    Button _action;
    TextureRect _body, _face;
    List<RMZEquipInstance> _equips = new();
    List<RMZBagEntry> _items = new();
    JArray _profiles;
    Texture2D _icons;
    static StyleBoxFlat Box() => new() { BgColor = new Color("202131"), BorderColor = new Color("cbaa58"), BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2, CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12, ContentMarginLeft = 20, ContentMarginRight = 20, ContentMarginTop = 16, ContentMarginBottom = 16 };
    public override void _Ready()
    {
        if(_smith){BuildSmith();return;}
        if(!_status){BuildInventory();return;}
        var screen = new Control { Theme = new Theme { DefaultFont = RMZFonts.Main, DefaultFontSize = 24 } }; AddChild(screen); screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var dim = new ColorRect { Color = new Color(0,0,0,.7f) }; screen.AddChild(dim); dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", Box()); screen.AddChild(panel); panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); panel.OffsetLeft=48;panel.OffsetRight=-48;panel.OffsetTop=36;panel.OffsetBottom=-36;
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation",16); panel.AddChild(column);
        var header = new HBoxContainer(); column.AddChild(header);
        header.AddChild(new Label { Text = _smith ? "鐵匠鋪・裝備強化" : _status ? "人物狀態" : "背包", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var close = new Button { Text = "關閉 ×" }; close.Pressed += () => { if(!_enhancing) QueueFree(); }; header.AddChild(close);
        if(!_status) { var tabs = new HBoxContainer(); column.AddChild(tabs); for(int i=0;i<(_smith?2:4);i++){ int t=i; var button=new Button {Text=RMZInventory.TabNames[i],SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};button.Pressed+=()=>{_scrollId=0;_tab=t;Refresh();};tabs.AddChild(button);} }
        var row = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; row.AddThemeConstantOverride("separation",24); column.AddChild(row);
        _list = new ItemList { CustomMinimumSize = new Vector2(340,200), SizeFlagsVertical = Control.SizeFlags.ExpandFill, FixedIconSize = new Vector2I(32,32) }; row.AddChild(_list); _list.ItemSelected += n => Select((int)n);
        if(_status){ var art=new Control {CustomMinimumSize=new Vector2(220,0),MouseFilter=Control.MouseFilterEnum.Ignore};row.AddChild(art);_body=Portrait(art);_face=Portrait(art); }
        var right=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};row.AddChild(right);
        var scroll=new ScrollContainer {SizeFlagsVertical=Control.SizeFlags.ExpandFill,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};right.AddChild(scroll);
        _detail=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(_detail);
        _action=new Button {Text="選擇物品",Disabled=true,Visible=!_status};right.AddChild(_action);_action.Pressed+=Act;
        _notice=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart,Text=_smith?"先選擇捲軸，再選擇裝備並按下強化。100% 捲軸一定成功。":"禮物於角色互動時贈送；捲軸可選擇裝備進行強化。"};column.AddChild(_notice);
        if(_status) _profiles=JArray.Parse(File.ReadAllText(ProjectSettings.GlobalizePath("res://rmz/character_profiles.json")));
        var iconPath=ProjectSettings.GlobalizePath("res://rmz/img/system/IconSet.png");if(File.Exists(iconPath))_icons=ImageTexture.CreateFromImage(Image.LoadFromFile(iconPath));
        Refresh();_list.GrabFocus();
    }
    TextureRect Portrait(Control parent){var p=new TextureRect {ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore};parent.AddChild(p);p.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);return p;}
    Texture2D Icon(int i)=>_icons==null?null:new AtlasTexture{Atlas=_icons,Region=new Rect2((i%16)*32,(i/16)*32,32,32)};
    void Refresh()
    {
        if(!_status&&!_smith){RefreshInventory();return;}
        _list.Clear();_selected=-1;_detail.Text="這個分類目前沒有物品。";_action.Disabled=true;
        if(_status){for(int i=1;i<=5;i++)_list.AddItem(SVTalk.NameOf(i));}
        else if(_tab==0){_equips=RMZEquipment.Sorted();foreach(var e in _equips)_list.AddItem((RMZEquipment.IsWorn(e)?"◆ ":"")+e.Name,Icon(e.IconIndex));}
        else{_items=RMZInventory.Contents((RMZBagTab)_tab);foreach(var e in _items)_list.AddItem(e.Name+" ×"+e.Count,Icon(e.IconIndex));}
        if(_list.ItemCount>0){_list.Select(0);Select(0);}
    }
    void Select(int n)
    {
        _selected=n;_inventoryWornUid=0;_action.Disabled=true;
        foreach(var slot in _wornSlots)slot.Modulate=Colors.White;
        if(!_status && (n<0 || n>=(EquipmentBagTab?_equips.Count:_items.Count)))return;
        if(!_status&&!_smith) HighlightInventory(n);
        if(_status){int id=n+1;_detail.Text=string.Join("\n\n",_profiles[n].ToObject<string[]>())+$"\n\n好感度：{RMZGifts.Love(id)}　墮落度：{RMZGifts.Fall(id)}";var lines=SVTalk.Lines(id,"cleared");if(lines.Count>0){var portrait=SVPortraitLayout.Crop(lines[0].Base,lines[0].Face);_body.Texture=portrait.body?.Native;_face.Texture=portrait.face?.Native;}return;}
        if(EquipmentBagTab){ShowEquipmentDetails(_equips[n]);}
        else {var e=_items[n];_detail.Text=RMZInventory.Describe(e)+$"\n\n持有：{e.Count}";_action.Text="選擇強化對象";_action.Disabled=!RMZEquipment.IsScroll(e.Id);}
    }
    async void Act()
    {
        if(_enhancing || (_selected<0 && _inventoryWornUid==0))return;
        if(!EquipmentBagTab){_scrollId=_items[_selected].Id;_tab=RMZEquipment.ScrollFor(_scrollId,out var targetScroll)&&targetScroll.ForWeapon?4:0;_notice.Text="從背包或左側裝備欄選擇目標，再按「使用捲軸強化」。切換分類可取消。";Refresh();return;}
        var equip=_inventoryWornUid>0?RMZEquipment.Find(_inventoryWornUid):(_selected>=0 && _selected<_equips.Count?_equips[_selected]:null);
        if(equip==null){Refresh();return;}
        if(_scrollId>0 && RMZEquipment.ScrollFor(_scrollId,out var scroll)){if(RMZParty.NumItems(_scrollId)<=0){_notice.Text="捲軸已用完。";_scrollId=0;Refresh();return;}if(!RMZEquipment.CanUse(scroll,equip,out var invalid)){_notice.Text=invalid;return;}
            _enhancing=true;
            bool enhanced=RMZEquipment.Enhance(scroll,equip,out var message);
            try { await PlayEnhanceEffect(enhanced,scroll.IsReset,message); }
            finally { _enhancing=false; }
            _notice.Text=message;_scrollId=0;
            if(_smith && enhanced && RMZGameState.Variable(4)==1) {
                RMZGameState.SetVariable(4,2);
                _notice.Text += "\n教學完成！關閉視窗後繼續劇情。";
            }}
        else {RMZEquipment.Toggle(equip);_notice.Text=RMZEquipment.IsWorn(equip)?"已裝備 "+equip.Name:"已卸下 "+equip.Name;}
        RMZEventRunner.Instance?.RequestRefresh();Refresh();
    }
    public override void _UnhandledInput(InputEvent input){if(input.IsActionPressed("ui_cancel")){if(_enhancing || RMZTutorialOverlay.IsOpen){GetViewport().SetInputAsHandled();return;}GetViewport().SetInputAsHandled();QueueFree();}}
    public override void _ExitTree(){if(_instance==this)_instance=null;if(_smith){RMZGameState.SetVariable(16,0);RMZGameState.SetVariable(282,1);RMZEventRunner.Instance?.SetEventsPaused(false);RMZEventRunner.Instance?.RequestRefresh();}}
}
