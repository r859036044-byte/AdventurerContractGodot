using Godot;
using RMZS;
namespace RMZ;
public static class RMZTalkPortrait
{
    static CanvasLayer layer;
    public static void Show(string body,string face)
    {
        Hide(); if(RMZBootstrap.Instance == null) return;
        var pair = (body: SVTalk.Face(body), face: SVTalk.Face(face));
        layer = new CanvasLayer { Layer=8 }; RMZBootstrap.Instance.GetTree().Root.AddChild(layer);
        var screen = new Control { MouseFilter=Control.MouseFilterEnum.Ignore }; layer.AddChild(screen);
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var slot = new Control { MouseFilter=Control.MouseFilterEnum.Ignore }; screen.AddChild(slot);
        slot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach(var sprite in new[]{pair.body,pair.face})
        {
            if(sprite==null)continue;
            var view=new TextureRect { Texture=sprite.Native, ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter=Control.MouseFilterEnum.Ignore };
            slot.AddChild(view); view.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }
    }
    public static void Hide() { if(GodotObject.IsInstanceValid(layer))layer.QueueFree(); layer=null; }
}
