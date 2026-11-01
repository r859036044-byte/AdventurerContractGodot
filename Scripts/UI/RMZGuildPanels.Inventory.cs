using Godot;
using System.IO;
using System.Collections.Generic;
namespace RMZ;

public partial class RMZGuildPanels
{
    const int BagPageSize=28;
    // 保留既有道具分類編號；武器是新增的獨立頁面。
    static readonly int[] BagTabOrder={4,0,1,2,3};
    static readonly string[] BagTabTitles={"武器","防具","捲軸","道具","其他"};
    bool EquipmentBagTab => _tab==0||_tab==4;
    int _bagPage, _inventoryWornUid;
    readonly List<Button> _wornSlots=new();
    readonly List<Button> _bagCells=new(), _bagTabs=new();
    readonly List<TextureRect> _bagIcons=new(), _wornIcons=new();
    readonly List<Label> _bagCounts=new(), _wornNames=new();
    Label _bagPages,_bagGold,_bagStats,_bagEmpty;
    Button _bagPrevious,_bagNext;
    void BuildInventory()
    {
        var screen=new Control {MouseFilter=Control.MouseFilterEnum.Stop};AddChild(screen);screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var dim=new ColorRect {Color=new Color(0,0,0,.58f)};screen.AddChild(dim);dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _smithStage=new Control {Size=new Vector2(1280,720),TextureFilter=CanvasItem.TextureFilterEnum.Nearest};screen.AddChild(_smithStage);
        void Fit(){var size=GetViewport().GetVisibleRect().Size;float scale=Mathf.Min(size.X/1280,size.Y/720);_smithStage.Scale=Vector2.One*scale;_smithStage.Position=(size-new Vector2(1280,720)*scale)/2;}
        Fit();screen.Resized+=Fit;
        var iconPath=ProjectSettings.GlobalizePath("res://rmz/img/system/IconSet.png");if(File.Exists(iconPath))_icons=ImageTexture.CreateFromImage(Image.LoadFromFile(iconPath));
        SmithArt("UI button/bag ui/weapon defend equip background",new Rect2(35,20,390,680));
        SmithArt("UI button/bag ui/equipment bag background",new Rect2(440,20,810,680));
        var close=SmithButton("UI button/Exit button",new Rect2(14,0,72,72));close.TooltipText="關閉背包";close.Pressed+=()=>{if(!_enhancing)QueueFree();};
        SmithLabel("目前裝備",new Rect2(95,37,270,42),28);
        SmithLabel("冒險背包",new Rect2(470,35,290,42),28);
        _bagGold=SmithLabel("",new Rect2(944,39,265,36),23);
        for(int i=0;i<5;i++){
            int slot=i+1;float y=100+i*89;
            var worn=SmithButton("UI button/bag ui/select weapon slot",new Rect2(65,y,330,78));_wornSlots.Add(worn);
            _wornIcons.Add(SmithArt(null,new Rect2(72,y+7,64,64)));_wornIcons[^1].TextureFilter=CanvasItem.TextureFilterEnum.Nearest;   // 圖示放大到 2 倍，像素圖用最近點取樣才不會糊
            _wornNames.Add(SmithLabel("",new Rect2(139,y+4,238,68),19));
            worn.TooltipText="單擊查看，雙擊卸下裝備";
            BindEquipmentClicks(worn,()=>SelectWornEquipment(slot),()=>{var equip=RMZEquipment.WornAt(slot);if(equip!=null)DoubleClickEquipment(equip.Uid,true);});
        }
        _bagStats=SmithLabel("",new Rect2(69,557,320,68),20);
        SmithLabel("單擊查看，雙擊穿上／卸下",new Rect2(66,642,331,28),17);
        for(int i=0;i<5;i++){
            int tab=BagTabOrder[i];var b=SmithButton("UI button/bag ui/scroll or equipment",new Rect2(479+i*146,91,138,48));
            SmithLabel(BagTabTitles[i],new Rect2(479+i*146,94,138,40),22);_bagTabs.Add(b);
            b.Pressed+=()=>{if(_enhancing)return;_tab=tab;if(!EquipmentBagTab)_scrollId=0;_bagPage=0;_notice.Text="點選格子查看物品；禮物可在角色互動時贈送。";RefreshInventory();};
        }
        for(int i=0;i<BagPageSize;i++){
            int cell=i;float x=484+(i%7)*104,y=150+(i/7)*79;
            var b=SmithButton("UI button/bag ui/equipment bag",new Rect2(x,y,96,72));_bagCells.Add(b);
            _bagIcons.Add(SmithArt(null,new Rect2(x+16,y+2,64,64)));_bagIcons[^1].TextureFilter=CanvasItem.TextureFilterEnum.Nearest;   // 45 → 64（原圖 32px 的 2 倍）
            var count=SmithLabel("",new Rect2(x+3,y+49,89,21),16);count.HorizontalAlignment=HorizontalAlignment.Right;_bagCounts.Add(count);
            BindEquipmentClicks(b,()=>Select(_bagPage*BagPageSize+cell),()=>{int index=_bagPage*BagPageSize+cell;if(_tab==4&&index<_equips.Count)DoubleClickEquipment(_equips[index].Uid,false);});
        }
        _inventoryRows=BuildEquipmentRows(new Rect2(481,145,731,350),60,10,2,5);
        _bagEmpty=SmithLabel("這個分類目前沒有物品",new Rect2(585,248,505,47),23);
        _bagPrevious=SmithButton("UI button/bag ui/left cursor",new Rect2(735,467,32,32));
        _bagNext=SmithButton("UI button/bag ui/right cursor",new Rect2(930,467,32,32));
        _bagPages=SmithLabel("",new Rect2(771,466,151,34),20);
        _bagPrevious.Pressed+=()=>{if(!_enhancing){_bagPage--;RefreshInventory();}};
        _bagNext.Pressed+=()=>{if(!_enhancing){_bagPage++;RefreshInventory();}};
        var detailPanel=new PanelContainer {Position=new Vector2(481,508),Size=new Vector2(731,117),MouseFilter=Control.MouseFilterEnum.Ignore};
        detailPanel.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("573d25"),BorderColor=new Color("b88c46"),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,ContentMarginLeft=13,ContentMarginRight=13,ContentMarginTop=8,ContentMarginBottom=8});_smithStage.AddChild(detailPanel);
        var details=new ScrollContainer {HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};detailPanel.AddChild(details);
        _detail=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};_detail.AddThemeFontOverride("font",RMZFonts.Main);_detail.AddThemeFontSizeOverride("font_size",20);_detail.AddThemeColorOverride("font_color",new Color("fff3cc"));details.AddChild(_detail);
        _action=SmithButton("UI button/bag ui/select weapon slot",new Rect2(977,637,230,47));_action.AddThemeFontOverride("font",RMZFonts.Main);_action.AddThemeFontSizeOverride("font_size",21);_action.AddThemeColorOverride("font_color",new Color("fff3cc"));_action.AddThemeColorOverride("font_outline_color",new Color("35200d"));_action.AddThemeConstantOverride("outline_size",5);_action.Pressed+=Act;
        _notice=SmithLabel("點選格子查看物品；禮物可在角色互動時贈送。",new Rect2(482,636,478,48),17);
        RefreshInventory();_bagTabs[System.Array.IndexOf(BagTabOrder,_tab)].GrabFocus();
    }
    void RefreshInventory()
    {
        _equips=RMZEquipment.Sorted().FindAll(e=>!RMZEquipment.IsWorn(e)&&(_tab==4?e.IsWeapon:!e.IsWeapon));_inventoryWornUid=0;_items=RMZInventory.Contents((RMZBagTab)_tab);
        if(_tab==0)_bagPage=0;
        // 先切換兩組互斥的格子，再填入當頁資料。
        for(int i=0;i<BagPageSize;i++){
            _bagCells[i].Visible=_tab!=0;_bagIcons[i].Visible=_tab!=0;_bagCounts[i].Visible=_tab!=0;
            _bagIcons[i].Texture=null;_bagCounts[i].Text="";
        }
        _inventoryRows.Root.Visible=_tab==0;
        if(_tab==0)FillEquipmentRows(_inventoryRows,_equips,uid=>{int index=_equips.FindIndex(e=>e.Uid==uid);if(index>=0)Select(index);},uid=>DoubleClickEquipment(uid,false));
        int count=EquipmentBagTab?_equips.Count:_items.Count,pages=System.Math.Max(1,(count+BagPageSize-1)/BagPageSize);
        _bagPage=System.Math.Clamp(_bagPage,0,pages-1);_selected=-1;
        for(int i=0;i<BagPageSize;i++){
            int index=_bagPage*BagPageSize+i;bool exists=index<count;
            _bagCells[i].Visible=_tab!=0;_bagIcons[i].Visible=_tab!=0;_bagCounts[i].Visible=_tab!=0;
            _bagCells[i].Disabled=!exists;_bagCells[i].Modulate=Colors.White;
            _bagIcons[i].Texture=exists?Icon(EquipmentBagTab?_equips[index].IconIndex:_items[index].IconIndex):null;
            _bagCounts[i].Text=exists?(EquipmentBagTab?"":"×"+_items[index].Count):"";
            _bagCells[i].TooltipText=exists?(EquipmentBagTab?_equips[index].Name:_items[index].Name):"";
        }
        for(int i=0;i<5;i++)_bagTabs[i].Modulate=BagTabOrder[i]==_tab?new Color(1.17f,1.08f,.7f):new Color(.84f,.84f,.84f);
        for(int i=0;i<5;i++){_wornSlots[i].Modulate=Colors.White;var e=RMZEquipment.WornAt(i+1);_wornIcons[i].Texture=e==null?null:Icon(e.IconIndex);_wornNames[i].Text=RMZEquipment.SlotNames[i+1]+"\n"+(e?.Name??"尚未裝備");}
        _bagGold.Text=RMZParty.Gold+" G";
        _bagStats.Text=$"裝備物攻 {RMZEquipment.TotalAtk}　魔攻 {RMZEquipment.TotalMAtk}\n物防 {RMZEquipment.TotalDef}　魔防 {RMZEquipment.TotalMDef}";
        _bagEmpty.Visible=_tab!=0&&count==0;_bagPages.Visible=_tab!=0;_bagPrevious.Visible=_tab!=0;_bagNext.Visible=_tab!=0;_bagPages.Text=$"{_bagPage+1} / {pages}";_bagPrevious.Disabled=_bagPage==0;_bagNext.Disabled=_bagPage>=pages-1;
        _detail.Text=count==0?"這個分類目前沒有物品。":"選擇格子查看說明。";_action.Text="選擇物品";_action.Disabled=true;
        if(count>0)Select(_bagPage*BagPageSize);
    }
    void SelectWornEquipment(int slot)
    {
        if(_enhancing)return;
        var equip=RMZEquipment.WornAt(slot);
        if(equip==null){_notice.Text="這個部位尚未穿戴裝備。";return;}
        // 穿戴裝備獨立選取，不把它重新塞入背包清單。
        _tab=equip.IsWeapon?4:0;RefreshInventory();_selected=-1;_inventoryWornUid=equip.Uid;
        HighlightInventory(-1);_wornSlots[slot-1].Modulate=new Color(1.25f,1.12f,.65f);
        ShowEquipmentDetails(equip);_wornSlots[slot-1].GrabFocus();
    }
    void ShowEquipmentDetails(RMZEquipInstance e)
    {
        _detail.Text=$"{e.Name}　｜　{RMZEquipment.SlotNames[e.Slot]}・{e.RollName}　｜　{(RMZEquipment.IsWorn(e)?"已裝備":"背包")}\n物攻 {e.Atk}　魔攻 {e.MAtk}　物防 {e.Def}　魔防 {e.MDef}\n生命 {e.MaxHp}　｜　剩餘強化次數 {e.SlotsLeft}";
        _action.Text=_scrollId>0?"使用捲軸強化":RMZEquipment.IsWorn(e)?"卸下裝備":"穿上裝備";
        _action.Disabled=false;
    }
    void HighlightInventory(int index)
    {
        HighlightEquipmentRows(_inventoryRows,index>=0&&index<_equips.Count&&EquipmentBagTab?_equips[index].Uid:0);
        for(int i=0;i<_bagCells.Count;i++)_bagCells[i].Modulate=_bagPage*BagPageSize+i==index?new Color(1.25f,1.12f,.65f):Colors.White;
    }
}
