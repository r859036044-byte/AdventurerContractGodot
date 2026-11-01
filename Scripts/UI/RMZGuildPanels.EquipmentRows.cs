using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
namespace RMZ;

public partial class RMZGuildPanels
{
    sealed class EquipmentRows
    {
        public Control Root;
        public readonly List<ScrollContainer> Scrolls=new();
        public readonly List<HBoxContainer> Lines=new();
        public readonly Dictionary<int,Button> Buttons=new();
        public string Signature="";
        public int CellSize,MinimumCells,FirstSlot,LastSlot;
    }
    EquipmentRows _inventoryRows,_smithRows;
    EquipmentRows BuildEquipmentRows(Rect2 area,int cellSize,int minimumCells,int firstSlot=1,int lastSlot=5)
    {
        var rows=new EquipmentRows {Root=new Control {Position=area.Position,Size=area.Size},CellSize=cellSize,MinimumCells=minimumCells,FirstSlot=firstSlot,LastSlot=lastSlot};
        _smithStage.AddChild(rows.Root);
        float height=area.Size.Y/(lastSlot-firstSlot+1);
        for(int slot=firstSlot;slot<=lastSlot;slot++){
            var label=SmithLabel(RMZEquipment.SlotNames[slot],new Rect2(0,(slot-firstSlot)*height,60,cellSize),19);
            label.Reparent(rows.Root,false);
            var scroll=new ScrollContainer {Position=new Vector2(65,(slot-firstSlot)*height),Size=new Vector2(area.Size.X-65,height-4),HorizontalScrollMode=ScrollContainer.ScrollMode.ShowAlways,VerticalScrollMode=ScrollContainer.ScrollMode.Disabled,FollowFocus=true};
            rows.Root.AddChild(scroll);rows.Scrolls.Add(scroll);
            var bar=scroll.GetHScrollBar();
            foreach(string state in new[]{"scroll","grabber","grabber_highlight","grabber_pressed"})bar.AddThemeStyleboxOverride(state,new StyleBoxFlat {BgColor=new Color(state=="scroll"?"#594027":state=="grabber"?"#BC914E":"#E4BD70"),BorderColor=new Color("#79552F"),BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,ContentMarginTop=7,ContentMarginBottom=7});
            var line=new HBoxContainer();line.AddThemeConstantOverride("separation",6);scroll.AddChild(line);rows.Lines.Add(line);
        }
        return rows;
    }
    void FillEquipmentRows(EquipmentRows rows,List<RMZEquipInstance> items,Action<int> select,Action<int> doubleClick=null)
    {
        string signature=string.Join("|",items.Select(e=>$"{e.Uid}:{e.Name}:{e.Roll}:{e.Used}:{e.Atk}:{e.MAtk}:{e.Def}:{e.MDef}"));
        if(rows.Signature==signature && rows.Lines[0].GetChildCount()>0){foreach(var b in rows.Buttons.Values)b.Disabled=_enhancing;return;}
        rows.Buttons.Clear();
        // 防具頁從上衣（2）開始；鐵匠鋪則包含武器（1）。
        for(int slot=rows.FirstSlot;slot<=rows.LastSlot;slot++){
            var line=rows.Lines[slot-rows.FirstSlot];int previous=rows.Scrolls[slot-rows.FirstSlot].ScrollHorizontal;
            foreach(var child in line.GetChildren()){line.RemoveChild(child);child.QueueFree();}
            var choices=items.FindAll(e=>e.Slot==slot);
            for(int i=0;i<Math.Max(rows.MinimumCells,choices.Count);i++){
                var equip=i<choices.Count?choices[i]:null;
                var button=SmithButton("UI button/bag ui/equipment bag",new Rect2(0,0,rows.CellSize,rows.CellSize));
                button.Reparent(line,false);button.CustomMinimumSize=new Vector2(rows.CellSize,rows.CellSize);button.SizeFlagsVertical=Control.SizeFlags.ShrinkBegin;
                button.Disabled=equip==null||_enhancing;
                if(equip==null){button.TooltipText=RMZEquipment.SlotNames[slot]+"專屬空格";continue;}
                int uid=equip.Uid;rows.Buttons[uid]=button;
                button.TooltipText=$"{equip.Name}・{equip.RollName}\n物攻 {equip.Atk}　魔攻 {equip.MAtk}\n物防 {equip.Def}　魔防 {equip.MDef}\n剩餘強化 {equip.SlotsLeft} 次"+(doubleClick!=null?"\n單擊查看，雙擊穿戴":"\n單擊選為強化目標");
                var icon=new TextureRect {Texture=Icon(equip.IconIndex),Position=new Vector2(2,2),Size=new Vector2(rows.CellSize-4,rows.CellSize-4),TextureFilter=CanvasItem.TextureFilterEnum.Nearest,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore};button.AddChild(icon);
                if(equip.Enhanced>0){var count=SmithLabel("+"+equip.Enhanced,new Rect2(2,rows.CellSize-20,rows.CellSize-4,18),15);count.Reparent(button,false);count.HorizontalAlignment=HorizontalAlignment.Right;}
                BindEquipmentClicks(button,()=>select(uid),doubleClick==null?null:()=>doubleClick(uid));
            }
            rows.Scrolls[slot-rows.FirstSlot].SetDeferred("scroll_horizontal",previous);
        }
        rows.Signature=signature;
    }
    void BindEquipmentClicks(Button button,Action select,Action doubleClick)
    {
        bool suppressRelease=false;
        button.Pressed+=()=>{if(suppressRelease){suppressRelease=false;return;}if(!_enhancing&&!RMZTutorialOverlay.IsOpen)select();};
        if(doubleClick==null)return;
        button.GuiInput+=input=>{
            if(input is InputEventMouseButton next && next.ButtonIndex==MouseButton.Left && next.Pressed && !next.DoubleClick)suppressRelease=false;
            if(input is InputEventMouseButton mouse && mouse.ButtonIndex==MouseButton.Left && mouse.Pressed && mouse.DoubleClick){
                if(_enhancing||RMZTutorialOverlay.IsOpen)return;
                suppressRelease=true;button.AcceptEvent();
                // 等輸入派送完才刷新格子，避免第二次點擊落到換裝後的新物品。
                Callable.From(()=>{if(IsInsideTree()&&!_enhancing)doubleClick();}).CallDeferred();
            }
        };
    }
    void HighlightEquipmentRows(EquipmentRows rows,int uid)
    {
        if(rows==null)return;
        foreach(var pair in rows.Buttons)pair.Value.Modulate=pair.Key==uid?new Color(1.25f,1.12f,.65f):Colors.White;
    }
    void DoubleClickEquipment(int uid,bool worn)
    {
        var equip=RMZEquipment.Find(uid);
        if(equip==null||RMZEquipment.IsWorn(equip)!=worn)return;
        _scrollId=0;
        if(worn)RMZEquipment.Unequip(equip.Slot);else RMZEquipment.Equip(equip);
        _notice.Text=worn?"已卸下 "+equip.Name:"已裝備 "+equip.Name;
        RMZEventRunner.Instance?.RequestRefresh();RefreshInventory();
    }
}
