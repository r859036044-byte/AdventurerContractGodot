using System;
using Godot;
using V = RMZS.GodotPort.Vector2;
namespace RMZS;

public static class SVCombatVisuals
{
    public static void Sword(V at,float radius,int damage)
    {
        if(SVRuntime.Instance==null)return;
        var node=new SVFallingSword { Position=new Vector2(at.x*48,-at.y*48),Radius=radius*48,
            Impact=()=>SVEnemy.DamageInRadius(at,radius,damage,2f), ZIndex=2550,ZAsRelative=false };
        SVRuntime.Instance.World.AddChild(node);
    }
    public static void Lightning(V at,float radius)
    {
        if(SVRuntime.Instance==null)return;
        SVRuntime.Instance.World.AddChild(new SVLightningStrike { Position=new Vector2(at.x*48,-at.y*48),Radius=radius*48,ZIndex=2550,ZAsRelative=false });
    }
}

/// <summary>落點提示 → 實體劍刃下墜 → 碰地才結算傷害 → 殘光。</summary>
public partial class SVFallingSword : Node2D
{
    public float Radius;public Action Impact;float age;bool hit;
    public override void _Process(double dt)
    {
        if(SVGame.Instance==null||SVGame.Instance.IsOver){QueueFree();return;}
        if(SVGame.Instance.Paused||SVTimeStop.WorldFrozen)return;
        age+=(float)dt;
        if(!hit&&age>=.3f){hit=true;Impact?.Invoke();}
        if(age>.72f){QueueFree();return;}QueueRedraw();
    }
    public override void _Draw()
    {
        var blue=new Color(.45f,.78f,1,.75f);
        if(!hit)
        {
            DrawArc(Vector2.Zero,Radius,0,Mathf.Tau,48,blue,2,true);
            DrawCircle(Vector2.Zero,Radius,new Color(.2f,.55f,1,.1f));
        }
        float fall=Mathf.Clamp(age/.3f,0,1);
        float tipY=-240*(1-fall*fall),fade=hit?Mathf.Clamp(1-(age-.3f)/.42f,0,1):1;
        var tip=new Vector2(0,tipY);
        DrawLine(tip-new Vector2(0,128),tip, new Color(.3f,.7f,1,.28f*fade),14,true);
        DrawColoredPolygon(new[]{tip,tip+new Vector2(-7,-19),tip+new Vector2(-7,-86),tip+new Vector2(7,-86),tip+new Vector2(7,-19)},new Color(.7f,.9f,1,fade));
        DrawLine(tip+new Vector2(0,-12),tip+new Vector2(0,-84),new Color(1,1,1,fade),2,true);
        DrawLine(tip+new Vector2(-19,-86),tip+new Vector2(19,-86),new Color(1,.79f,.32f,fade),5,true);
        DrawLine(tip+new Vector2(0,-87),tip+new Vector2(0,-111),new Color(.27f,.38f,.62f,fade),6,true);
        DrawCircle(tip+new Vector2(0,-112),4,new Color(1,.83f,.45f,fade));
        if(hit)
        {
            float burst=(age-.3f)/.42f;
            DrawArc(Vector2.Zero,Radius*(.3f+.7f*burst),0,Mathf.Tau,48,new Color(.7f,.9f,1,fade),3,true);
            for(int i=0;i<10;i++){var d=Vector2.FromAngle(i*Mathf.Tau/10);DrawLine(d*Radius*burst,d*(Radius*burst+12),new Color(1,.88f,.5f,fade),2,true);}
        }
    }
}

/// <summary>從畫面上方落下的主雷柱、分叉與地面電弧。</summary>
public partial class SVLightningStrike : Node2D
{
    public float Radius;float age;
    public override void _Process(double dt)
    {
        if(SVGame.Instance?.Paused==true||SVTimeStop.WorldFrozen)return;
        age+=(float)dt;if(age>.55f){QueueFree();return;}QueueRedraw();
    }
    public override void _Draw()
    {
        float fade=Mathf.Clamp(1-age/.55f,0,1);
        float flicker=((int)(age*30)%2==0)?1:.65f;
        Vector2[] bolt={new(-38,-460),new(-8,-358),new(-26,-287),new(13,-204),new(-12,-133),new(16,-65),Vector2.Zero};
        DrawPolyline(bolt,new Color(.3f,.5f,1,.25f*fade),20,true);
        DrawPolyline(bolt,new Color(.55f,.8f,1,fade*flicker),7,true);
        DrawPolyline(bolt,new Color(1,1,1,fade*flicker),2,true);
        DrawPolyline(new[]{bolt[2],new Vector2(55,-238),new Vector2(91,-174)},new Color(.65f,.8f,1,fade),3,true);
        DrawPolyline(new[]{bolt[4],new Vector2(-70,-87),new Vector2(-98,-28)},new Color(.65f,.8f,1,fade),3,true);
        DrawCircle(Vector2.Zero,Radius,new Color(.4f,.6f,1,.12f*fade));
        DrawArc(Vector2.Zero,Radius*(.75f+age*.5f),0,Mathf.Tau,48,new Color(.65f,.85f,1,fade),2,true);
        for(int i=0;i<8;i++){var d=Vector2.FromAngle(i*Mathf.Tau/8);DrawPolyline(new[]{d*12,d*(Radius*.5f)+d.Orthogonal()*9,d*Radius},new Color(.75f,.9f,1,fade),2,true);}
    }
}

/// <summary>由毒霧技能本身傳入時間，暫停、消散與作用範圍保持一致。</summary>
public partial class SVPoisonMist : Node2D
{
    public float Radius,Age,Life=6;
    public override void _Draw()
    {
        float fade=Mathf.Clamp((Life-Age)/1.2f,0,1);
        DrawCircle(Vector2.Zero,Radius,new Color(.12f,.28f,.08f,.32f*fade));
        DrawArc(Vector2.Zero,Radius,0,Mathf.Tau,64,new Color(.6f,.95f,.22f,.7f*fade),2,true);
        for(int i=0;i<9;i++)
        {
            float a=i*Mathf.Tau/9+Age*.18f;
            var p=Vector2.FromAngle(a)*(Radius*.5f);
            float r=Radius*(.31f+.035f*Mathf.Sin(Age*2+i));
            DrawCircle(p,r,new Color(.35f,.73f,.18f,.19f*fade));
            DrawCircle(p-new Vector2(3,7),r*.68f,new Color(.55f,.86f,.27f,.1f*fade));
        }
        for(int i=0;i<12;i++)
        {
            float phase=(Age*.4f+i*.137f)%1;
            var p=Vector2.FromAngle(i*2.4f)*Radius*.7f-new Vector2(0,phase*20);
            DrawArc(p,2+phase*4,0,Mathf.Tau,12,new Color(.7f,1,.35f,(1-phase)*.75f*fade),1,true);
        }
        float pulse=(Age%2.5f)/2.5f;
        DrawArc(Vector2.Zero,Radius*(.5f+.5f*pulse),0,Mathf.Tau,48,new Color(.7f,1,.4f,(1-pulse)*.3f*fade),2,true);
    }
}
