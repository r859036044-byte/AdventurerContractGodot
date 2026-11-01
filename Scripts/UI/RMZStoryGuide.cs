using Godot;
namespace RMZ;
public static class RMZStoryGuide
{
    public static void Update()
    {
        var map=RMZGameMap.Instance;var screen=RMZScreen.Instance;var player=RMZPlayer.Instance;
        if(map==null||screen==null||player==null)return;
        int target=0,opening=RMZGameState.Variable(5),smith=RMZGameState.Variable(4);
        if(map.MapId==4){
            if(RMZGameState.Variable(61)==0 && opening>=1)target=17;
            else if(RMZGameState.Variable(1)==5)target=27;
            else target=22;
        } else if(map.MapId==2||map.MapId==35){
            if(RMZGameState.Variable(2)==4&&smith<3)target=26;
            else if(smith==3)target=22;
            else if(RMZGameState.Variable(1)==5)target=38;
            else target=22;
        }
        var npc=RMZEventRunner.Instance?.EventById(target);
        if(target==0||npc==null){screen.ErasePicture(231);return;}
        var diff=npc.GlobalPosition-player.GlobalPosition;
        if(diff.Length()<40){screen.ErasePicture(231);return;}
        var view=player.GetViewport().GetVisibleRect().Size;
        var at=player.GetGlobalTransformWithCanvas().Origin-new Vector2(0,24);
        at+=diff.Normalized()*60;
        var pic=screen.ShowPicture(231,"arrow",1,at.X*1280/view.X,at.Y*720/view.Y,100,100,255,0);
        pic.Angle=Mathf.RadToDeg(diff.Angle())+90;
    }
}
