using Godot;
using RMZ;
using P=RMZS.GodotPort;
namespace RMZS;

/// <summary>只掛在倖存者男主身上；與戰鬥物件一起釋放，不修改公會行走圖。</summary>
public partial class SVHeldWeapon : Node2D
{
    Texture2D _texture;
    SVWeaponCircle _circle;
    bool _staff;
    float _age=10,_duration=.34f,_aim,_clock,_angle;
    Vector2 _hand,_tip;
    // 手持大小（畫面像素）與握把位置（圖上的比例座標）。
    // 素材頭朝左上、柄朝右下；握把放在柄的最底端，手握的是武器底部。
    // 劍圖實測柄端約 (0.93,0.90)、法杖約 (0.74,0.83)，往內收一點點才不會握在空氣上。
    const float SwordSize=32,StaffSize=46;
    static readonly Vector2 SwordGrip=new(.86f,.84f),StaffGrip=new(.70f,.78f);
    float Size=>_staff?StaffSize:SwordSize;
    bool _mirror;
    Vector2 Grip=>_staff?StaffGrip:SwordGrip;
    public bool Attacking=>_age<_duration;
    public P.Vector2 Muzzle {get{var p=ToGlobal(_tip);return new P.Vector2(p.X/48,-p.Y/48);}}
    public void Configure(RMZEquipInstance equip)
    {
        _texture=RMZWeaponArt.Texture(equip);_staff=equip?.IsStaff==true;
        TextureFilter=TextureFilterEnum.Nearest;ZAsRelative=false;
        _circle=new SVWeaponCircle {ZAsRelative=false,Visible=_staff};AddChild(_circle);
        Visible=_texture!=null;
    }
    public void Strike(P.Vector2 direction,float interval)
    {
        // 極高速連擊時維持一次完整揮動，避免每幀重置而看起來沒有動。
        _aim=Mathf.Atan2(-direction.y,direction.x);
        if(!Attacking||_age>_duration*.65f){_age=0;_duration=Mathf.Clamp(interval*.8f,.13f,_staff?.42f:.34f);}
    }
    public void Sync(SVPlayer player,int sorting,float dt)
    {
        if(_texture==null)return;
        bool frozen=SVGame.Instance==null||SVGame.Instance.Paused||RMZTutorialOverlay.IsOpen;
        Visible=!player.IsDead&&SVGame.Instance?.IsOver!=true;
        if(!frozen){_age+=dt;_clock+=dt;}
        int direction=player.FacingDirection;
        _mirror=direction==2&&!Attacking;   // 正面待機時劍、法杖都左右反轉
        float bob=player.Velocity.magnitude>.05f&&player.WalkPattern!=1?1.5f:0;
        // 男主是大頭比例行走圖，手在畫格 y≈36（腳底樞紐 y≈41），不是頭部中心。
        _hand=new Vector2(direction==4?-3:direction==8?7:direction==2?-7:3,-5+bob);
        // 法杖正面：再往左下一點，杖身才會落在男主手邊而不是蓋在身上
        if(_staff&&direction==2)_hand+=new Vector2(-4,3);
        // 只有正面（朝下）時武器在男主前面；側面、背面都畫在男主後面
        ZIndex=System.Math.Clamp(sorting/8+(direction==2?1:-1),-4095,4095);
        float idle=direction==4?-1.95f:-1.18f;
        float t=Mathf.Clamp(_age/_duration,0,1);
        if(_staff)_angle=idle+(_aim-idle)*Mathf.Sin(t*Mathf.Pi)*.35f;
        else if(t<.72f)_angle=_aim-1.25f+2.5f*Mathf.SmoothStep(0,1,t/.72f);
        else _angle=Mathf.LerpAngle(_aim+1.25f,idle,(t-.72f)/.28f);
        if(!Attacking)_angle=idle;
        // 原始素材頭朝左上；以握柄為軸心旋轉，手的位置不會隨武器旋轉飄走。
        float size=Size;
        var grip=Grip;
        float rotation=_angle+(_staff?2.13f:Mathf.Pi*.75f);
        var head=_staff?new Vector2(.33f,.21f):new Vector2(.12f,.12f);
        _tip=_hand+((head-grip)*size).Rotated(rotation);
        _circle.ZIndex=System.Math.Clamp(sorting/8-2,-4096,4096);
        _circle.Clock=_clock;_circle.Pulse=Attacking?Mathf.Sin(t*Mathf.Pi):0;_circle.QueueRedraw();
        QueueRedraw();
    }
    public override void _Draw()
    {
        if(_texture==null)return;
        float t=Mathf.Clamp(_age/_duration,0,1);
        if(!_staff&&Attacking&&t<.82f){
            float end=_aim-1.25f+2.5f*Mathf.SmoothStep(0,1,Mathf.Min(1,t/.72f));
            float start=Mathf.Max(_aim-1.3f,end-.9f),alpha=Mathf.Sin(t*Mathf.Pi)*.85f;
            // 刀光半徑跟著劍的長度走（原本 43 像素的劍配 31／32／26）
            float r=Size/43f;
            DrawArc(_hand,31*r,start,end,24,new Color(.45f,.78f,1,.24f*alpha),11*r,true);
            DrawArc(_hand,32*r,start,end,24,new Color(.85f,.96f,1,alpha),3,true);
            DrawArc(_hand,26*r,start+.08f,end,20,new Color(1,.84f,.44f,.6f*alpha),1.5f,true);
        }
        float size=Size;
        var grip=Grip;
        // 正面（朝下）待機時劍左右反轉；揮劍時照原本方向，刀光才對得上
        if(_mirror){
            var b=new Transform2D(_angle+(_staff?2.13f:Mathf.Pi*.75f),Vector2.Zero);
            DrawSetTransformMatrix(new Transform2D(new Vector2(-b.X.X,b.X.Y),new Vector2(-b.Y.X,b.Y.Y),_hand));
        }
        else DrawSetTransform(_hand,_angle+(_staff?2.13f:Mathf.Pi*.75f),Vector2.One);
        DrawTextureRect(_texture,new Rect2(-grip*size,Vector2.One*size),false);
        DrawSetTransform(Vector2.Zero);
        DrawCircle(_hand,2.2f,new Color("dba274"));
        if(_staff&&Attacking){
            float pulse=Mathf.Sin(t*Mathf.Pi);
            DrawCircle(_tip,9,new Color(.25f,.65f,1,.18f*pulse));
            DrawCircle(_tip,4,new Color(.6f,.9f,1,.7f*pulse));
            DrawLine(_tip-Vector2.Up*7,_tip+Vector2.Up*7,new Color(.8f,.95f,1,pulse),1.5f,true);
            DrawLine(_tip-Vector2.Right*7,_tip+Vector2.Right*7,new Color(.8f,.95f,1,pulse),1.5f,true);
        }
    }
}

/// <summary>法杖施法的腳下符文：位於角色下層，移動時跟隨、暫停時凍結。</summary>
public partial class SVWeaponCircle : Node2D
{
    public float Clock,Pulse;
    public override void _Draw()
    {
        float alpha=.25f+Pulse*.6f,radius=27+Pulse*3;
        DrawSetTransform(new Vector2(0,1),0,new Vector2(1,.48f));
        DrawCircle(Vector2.Zero,radius,new Color(.15f,.45f,1,.06f+Pulse*.08f));
        var color=new Color(.4f,.8f,1,alpha);
        DrawArc(Vector2.Zero,radius,0,Mathf.Tau,64,color,1.5f,true);
        DrawArc(Vector2.Zero,radius-4,0,Mathf.Tau,64,new Color(.75f,.9f,1,alpha*.8f),1,true);
        for(int i=0;i<6;i++){
            float a=i*Mathf.Tau/6+Clock*.35f;
            var p=Vector2.FromAngle(a)*(radius-8);var q=Vector2.FromAngle(a+Mathf.Tau/3)*(radius-8);
            DrawLine(p,q,new Color(.4f,.7f,1,alpha*.6f),1,true);
            var d=Vector2.FromAngle(a);DrawLine(d*(radius-2),d*(radius+2),color,2,true);
        }
        DrawSetTransform(Vector2.Zero);
    }
}
