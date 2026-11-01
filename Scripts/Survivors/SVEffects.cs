using System;
using System.Collections.Generic;
using Godot;
using V=RMZS.GodotPort.Vector2;
namespace RMZS;
public static class SVEffects {
public static float MaxScale=4;public static int MaxConcurrent=36;
internal static readonly List<SVImpactVisual> Active=new();
public static RMZS.GodotPort.GameObject Play(string name,V pos,float scale=1,float life=.65f){if(SVRuntime.Instance==null||Active.Count>=MaxConcurrent)return null;var fx=new SVImpactVisual{Kind=name??"arcane",Position=new Vector2(pos.x*48,-pos.y*48),Radius=Math.Clamp(scale*48,18,240),Duration=Math.Clamp(life,.22f,1.2f),ZIndex=2600,ZAsRelative=false};Active.Add(fx);SVRuntime.Instance.World.AddChild(fx);return null;}
public static void Preload(IEnumerable<string> names){foreach(var name in new[]{"Firebomb","Ice Lance","Magic Sparks","Darkness Orb","Pixelart Shield"})SVVisualAssets.Spell(name);}
public static void Clear(){foreach(var fx in Active.ToArray())if(GodotObject.IsInstanceValid(fx))fx.QueueFree();Active.Clear();}
}
public partial class SVImpactVisual:Node2D {
// 技能命中特效。以前是把 16×16 的像素法術圖放大到兩三百像素，馬賽克很重；
// 現在主體改成程式畫的光暈、衝擊波與火星（平滑、不吃貼圖），像素圖只當小點綴、最多放大 3 倍。
// 效能：每個特效幾十次簡單繪圖，數量由 SVEffects.MaxConcurrent（畫質分級）壓住；低畫質火星減半。
public string Kind;public float Radius,Duration;float age;Color tint;string spell;bool slash,lightning,dark,heal,fire,overdrive,star,meteor;
float[] sparkAngle,sparkSpeed;
public override void _Ready(){string k=Kind.ToLowerInvariant();slash=k.Contains("blade")||k.Contains("sword")||k.Contains("whirl")||k.Contains("collector")||k.Contains("warlord");lightning=k.Contains("chain")||k.Contains("thunder");dark=k.Contains("dark")||k.Contains("black")||k.Contains("time");heal=k.Contains("bond")||k.Contains("barrier")||k.Contains("adrenaline");
tint=slash?new Color(.65f,.88f,1):lightning?new Color(.65f,.85f,1):dark?new Color(.65f,.4f,1):heal?new Color(.3f,1,.7f):new Color(1,.65f,.2f);
spell=slash?"Magic Sparks":lightning?"Light Bolt":dark?"Darkness Orb":heal?"Pixelart Shield":"Firebomb";
if(k.Contains("poison")||k.Contains("earth")){tint=new Color(.6f,1,.25f);spell="Plant Missle";}if(k.Contains("ice")||k.Contains("frost")||k.Contains("zero")){tint=new Color(.4f,.9f,1);spell="Ice Lance";}
overdrive=k.Contains("overdrive");star=k.Contains("lightall")||k.Contains("starfall")||k.Contains("judgment");
fire=spell=="Firebomb"&&!overdrive&&!star;meteor=fire&&k.Contains("fireone");
if(overdrive)tint=new Color(1,.86f,.35f);if(star)tint=new Color(.82f,.62f,1);
int n=SVQuality.MinorPulses?16:8;var rng=new System.Random((int)(GetInstanceId()&0x7fffffff));sparkAngle=new float[n];sparkSpeed=new float[n];
for(int i=0;i<n;i++){sparkAngle[i]=(float)(i*Math.PI*2/n+rng.NextDouble()*.35);sparkSpeed[i]=.65f+(float)rng.NextDouble()*.55f;}
}
public override void _Process(double delta){if(SVGame.Instance?.Paused==true)return;age+=(float)delta;if(age>=Duration){QueueFree();return;}QueueRedraw();}
public override void _ExitTree()=>SVEffects.Active.Remove(this);
static Color A(Color c,float a){c.A=Mathf.Clamp(a,0,1);return c;}
// 幾層半透明圓疊成柔和光暈（中心最亮），不用貼圖、不會有馬賽克
void Glow(Vector2 at,float r,Color c,float alpha){for(int i=0;i<4;i++)DrawCircle(at,r*(1-i*.22f),A(c,alpha*.22f));}
// 從天而降的光尾（隕石、星辰），p=0..1
void Streak(Vector2 from,float p,Color c,float width){var head=from.Lerp(Vector2.Zero,p);var dir=(Vector2.Zero-from).Normalized();
for(int i=0;i<5;i++){float a=i/5f;DrawLine(head-dir*Radius*(.15f+a*.55f),head-dir*Radius*(.15f+(a+.2f)*.55f),A(c,(1-a)*.85f),width*(1-a*.7f),true);}
Glow(head,width*2.2f,c,.9f);DrawCircle(head,width*.6f,A(Colors.White,.95f));}
void Sparks(float t,Color c,float reach,float size){for(int i=0;i<sparkAngle.Length;i++){var dir=Vector2.FromAngle(sparkAngle[i]);float d=reach*sparkSpeed[i]*(1-MathF.Pow(1-t,2.2f));DrawCircle(dir*d,size*(1-t)+.8f,A(c,(1-t)*.95f));}}
public override void _Draw(){float t=age/Duration,fade=1-t,ease=1-MathF.Pow(1-t,3);
if(overdrive){DrawOverdrive(t,fade,ease);return;}
if(star){DrawStar(t);return;}
if(fire){DrawFire(t);return;}
float r=Radius*(.3f+.7f*ease);var c=tint;c.A=fade*.8f;
DrawArc(Vector2.Zero,r,slash?-2.9f:0,slash?-.1f:Mathf.Tau,64,c,slash?5:2,true);
if(!slash){var inner=c;inner.A*=.25f;DrawCircle(Vector2.Zero,r*.75f,inner);DrawArc(Vector2.Zero,r*.72f,age*3,age*3+Mathf.Tau,48,c,1,true);}
int count=lightning?7:12;for(int i=0;i<count;i++){float a=i*Mathf.Tau/count+.3f;var dir=Vector2.FromAngle(a);var p=dir*r; if(lightning){var points=new[]{dir*12,dir*(r*.4f)+dir.Orthogonal()*12,dir*(r*.65f)-dir.Orthogonal()*7,p};DrawPolyline(points,c,2,true);}else{var tail=p-dir*(8+18*fade);DrawLine(tail,p,c,2,true);}if(heal)DrawLine(p-new Vector2(0,4),p+new Vector2(0,4),c,2,true);}
DrawPixelSprite(slash?.55f:1.3f,fade);
}
// 像素法術圖：只放大到原尺寸 3 倍為止，保持清楚的像素點綴
void DrawPixelSprite(float mul,float alpha){var sprite=SVVisualAssets.Spell(spell,age);if(sprite==null)return;float native=Math.Max(8,sprite.Native.GetWidth());float size=Math.Min(Radius*mul,native*3);DrawTextureRect(sprite.Native,new Rect2(new Vector2(-size/2,-size/2),new Vector2(size,size)),false,new Color(1,1,1,alpha));}
// 爆炸（火箭、星隕、爆裂等）：光暈＋衝擊波＋火星，隕石先從右上方劃下來
void DrawFire(float t){float fall=meteor?.22f:0;
if(t<fall){Streak(new Vector2(Radius*1.1f,-Radius*2.6f),t/fall,new Color(1,.6f,.2f),Math.Max(6,Radius*.12f));return;}
float u=(t-fall)/(1-fall),fade=1-u,ease=1-MathF.Pow(1-u,3);
Glow(Vector2.Zero,Radius*(.45f+.55f*ease),new Color(1,.42f,.1f),fade*.9f);
Glow(Vector2.Zero,Radius*(.5f-.25f*u),new Color(1,.78f,.32f),fade);
DrawCircle(Vector2.Zero,Radius*.2f*fade,A(new Color(1,.97f,.85f),fade));
DrawArc(Vector2.Zero,Radius*(.35f+.85f*ease),0,Mathf.Tau,48,A(new Color(1,.85f,.55f),fade*.75f),Math.Max(1.5f,7*fade),true);
Sparks(u,new Color(1,.8f,.35f),Radius*1.05f,3.2f);}
// 鈴音・星辰墜落：一道星光直直落下，炸開成紫白色的四芒星
void DrawStar(float t){const float fall=.2f;var c=tint;
if(t<fall){Streak(new Vector2(0,-Radius*2.8f),t/fall,c,Math.Max(6,Radius*.1f));return;}
float u=(t-fall)/(1-fall),fade=1-u,ease=1-MathF.Pow(1-u,3);
Glow(Vector2.Zero,Radius*(.4f+.6f*ease),c,fade*.85f);
DrawCircle(Vector2.Zero,Radius*.16f*fade,A(Colors.White,fade));
float len=Radius*(.5f+.7f*ease)*MathF.Sqrt(fade+.05f),w=Radius*.07f+2;
StarShape(len,w,0,A(new Color(1,.95f,1),fade*.95f));StarShape(len*.55f,w*.8f,Mathf.Pi/4+u*.6f,A(c,fade*.8f));
DrawArc(Vector2.Zero,Radius*(.3f+.8f*ease),0,Mathf.Tau,48,A(c,fade*.7f),Math.Max(1.5f,5*fade),true);
Sparks(u,new Color(.92f,.82f,1),Radius*.95f,2.6f);}
void StarShape(float len,float w,float rot,Color c){for(int i=0;i<2;i++){float a=rot+i*Mathf.Pi/2;var d=Vector2.FromAngle(a);var o=d.Orthogonal()*w;DrawColoredPolygon(new[]{d*len,o,-d*len,-o},c);}}
// 極速連擊：金色的風壓與速度線往外甩，外圈三段弧線快速旋轉（不是爆炸）
void DrawOverdrive(float t,float fade,float ease){var c=tint;
Glow(Vector2.Zero,Radius*.35f*(1-t*.5f),c,fade*.55f);
for(int i=0;i<sparkAngle.Length;i++){var d=Vector2.FromAngle(sparkAngle[i]);float r0=Radius*(.2f+.75f*ease)*sparkSpeed[i];float len=Radius*.28f*fade;DrawLine(d*r0,d*(r0+len),A(c,fade*.9f),3*fade+1,true);}
float spin=age*14;for(int i=0;i<3;i++){float a0=spin+i*Mathf.Tau/3;DrawArc(Vector2.Zero,Radius*(.45f+.2f*ease),a0,a0+1.1f,18,A(new Color(1,.97f,.8f),fade*.9f),Math.Max(1.5f,5*fade),true);}
DrawArc(Vector2.Zero,Radius*(.3f+.75f*ease),0,Mathf.Tau,48,A(c,fade*.6f),Math.Max(1,4*fade),true);}
}
