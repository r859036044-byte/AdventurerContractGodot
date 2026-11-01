// 保留戰鬥資料的一格單位及 Y 向上座標，僅在繪圖邊界轉成 Godot 像素。
using System;
namespace RMZS.GodotPort {
public struct Vector2 {
 public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 zero=>new(0,0); public static Vector2 one=>new(1,1); public static Vector2 up=>new(0,1); public static Vector2 right=>new(1,0);
 public float sqrMagnitude=>x*x+y*y; public float magnitude=>MathF.Sqrt(sqrMagnitude); public Vector2 normalized=>magnitude>1e-8f?this/magnitude:zero;
 public void Normalize(){this=normalized;}
 public static Vector2 operator +(Vector2 a,Vector2 b)=>new(a.x+b.x,a.y+b.y); public static Vector2 operator -(Vector2 a,Vector2 b)=>new(a.x-b.x,a.y-b.y); public static Vector2 operator -(Vector2 a)=>new(-a.x,-a.y);
 public static Vector2 operator *(Vector2 a,float b)=>new(a.x*b,a.y*b); public static Vector2 operator *(float b,Vector2 a)=>a*b; public static Vector2 operator /(Vector2 a,float b)=>new(a.x/b,a.y/b);
 public static bool operator ==(Vector2 a,Vector2 b)=>a.x==b.x&&a.y==b.y; public static bool operator !=(Vector2 a,Vector2 b)=>!(a==b); public override bool Equals(object o)=>o is Vector2 v&&this==v; public override int GetHashCode()=>HashCode.Combine(x,y);
 public static float Distance(Vector2 a,Vector2 b)=>(a-b).magnitude; public static float Dot(Vector2 a,Vector2 b)=>a.x*b.x+a.y*b.y;
 public static Vector2 Lerp(Vector2 a,Vector2 b,float t)=>a+(b-a)*Mathf.Clamp01(t);
 public static Vector2 ClampMagnitude(Vector2 a,float n)=>a.magnitude>n?a.normalized*n:a;
 public static Vector2 MoveTowards(Vector2 a,Vector2 b,float d)=>a+ClampMagnitude(b-a,d);
 public static implicit operator Vector3(Vector2 a)=>new(a.x,a.y,0); public static implicit operator Godot.Vector2(Vector2 a)=>new(a.x,a.y); public static implicit operator Vector2(Godot.Vector2 a)=>new(a.X,a.Y);
}
public struct Vector3 {
 public float x,y,z; public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;} public static Vector3 zero=>new(0,0,0); public static Vector3 one=>new(1,1,1);
 public float magnitude=>MathF.Sqrt(x*x+y*y+z*z); public float sqrMagnitude=>x*x+y*y+z*z; public Vector3 normalized=>magnitude>1e-8f?this/magnitude:zero;
 public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z); public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z); public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b); public static Vector3 operator /(Vector3 a,float b)=>new(a.x/b,a.y/b,a.z/b);
 public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*Mathf.Clamp01(t); public static implicit operator Vector2(Vector3 a)=>new(a.x,a.y);
}
public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}}
public struct Vector4 {public float x,y,z,w;public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}}
public struct Quaternion {public float z;public static Vector2 operator *(Quaternion q,Vector2 v){float a=q.z*Mathf.Deg2Rad;return new(v.x*MathF.Cos(a)-v.y*MathF.Sin(a),v.x*MathF.Sin(a)+v.y*MathF.Cos(a));}public static Quaternion identity=>new();public static Quaternion Euler(float x,float y,float z)=>new(){z=z};public static Quaternion operator *(Quaternion a,Quaternion b)=>Euler(0,0,a.z+b.z);}
public struct Rect {public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}public float xMax=>x+width;public float yMax=>y+height;public Vector2 size=>new(width,height);public bool Contains(Vector2 p)=>p.x>=x&&p.x<=xMax&&p.y>=y&&p.y<=yMax;}
public struct Bounds {public Vector3 size;}
public struct Color {
 public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
 public static Color white=>new(1,1,1);public static Color black=>new(0,0,0);public static Color clear=>new(0,0,0,0); public static Color red=>new(1,0,0); public static Color yellow=>new(1,1,0);
 public static Color Lerp(Color a,Color b,float t){t=Mathf.Clamp01(t);return new(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);}
 public static Color HSVToRGB(float h,float s,float v)=>Godot.Color.FromHsv(h,s,v);
 public static implicit operator Godot.Color(Color c)=>new(c.r,c.g,c.b,c.a);public static implicit operator Color(Godot.Color c)=>new(c.R,c.G,c.B,c.A);
 public static bool operator ==(Color a,Color b)=>a.r==b.r&&a.g==b.g&&a.b==b.b&&a.a==b.a;public static bool operator !=(Color a,Color b)=>!(a==b);public override bool Equals(object o)=>o is Color c&&this==c;public override int GetHashCode()=>HashCode.Combine(r,g,b,a);
}
public struct Color32 {public byte r,g,b,a;public Color32(byte r,byte g,byte b,byte a){this.r=r;this.g=g;this.b=b;this.a=a;}public static implicit operator Color(Color32 c)=>new(c.r/255f,c.g/255f,c.b/255f,c.a/255f);}
public static class ColorUtility {public static string ToHtmlStringRGB(Color c)=>((Godot.Color)c).ToHtml(false);}
public static class Mathf {
 public const float PI=MathF.PI,Deg2Rad=PI/180,Rad2Deg=180/PI;
 public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static int Min(int a,int b)=>Math.Min(a,b);
 public static float Clamp(float a,float lo,float hi)=>Math.Clamp(a,lo,hi);public static int Clamp(int a,int lo,int hi)=>Math.Clamp(a,lo,hi);public static float Clamp01(float a)=>Clamp(a,0,1);
 public static float Abs(float a)=>MathF.Abs(a);public static int Abs(int a)=>Math.Abs(a);public static float Sin(float a)=>MathF.Sin(a);public static float Cos(float a)=>MathF.Cos(a);public static float Sqrt(float a)=>MathF.Sqrt(a);public static float Exp(float a)=>MathF.Exp(a);public static float Pow(float a,float b)=>MathF.Pow(a,b);public static float Atan2(float a,float b)=>MathF.Atan2(a,b);
 public static int RoundToInt(float a)=>(int)MathF.Round(a);public static int FloorToInt(float a)=>(int)MathF.Floor(a);public static int CeilToInt(float a)=>(int)MathF.Ceiling(a);
 public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);public static float MoveTowards(float a,float b,float d)=>a+Clamp(b-a,-d,d);
 public static float PingPong(float t,float l){t-=MathF.Floor(t/(l*2))*l*2;return l-MathF.Abs(t-l);}public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return a+(b-a)*t*t*(3-2*t);}public static bool Approximately(float a,float b)=>MathF.Abs(a-b)<1e-5f;
}
public static class Random {public static float value=>(float)System.Random.Shared.NextDouble();public static int Range(int a,int b)=>System.Random.Shared.Next(a,b);public static float Range(float a,float b)=>a+(b-a)*value;public static Vector2 insideUnitCircle {get {float a=value*MathF.PI*2;return new Vector2(MathF.Cos(a),MathF.Sin(a))*MathF.Sqrt(value);}}}
[AttributeUsage(AttributeTargets.All)] public class HeaderAttribute:Attribute {public HeaderAttribute(string s){}}
[AttributeUsage(AttributeTargets.All)] public class TooltipAttribute:Attribute {public TooltipAttribute(string s){}}
[AttributeUsage(AttributeTargets.All)] public class RangeAttribute:Attribute {public RangeAttribute(float a,float b){}}
[AttributeUsage(AttributeTargets.All)] public class SerializeField:Attribute {}
public static class Debug {public static void Log(object s)=>Godot.GD.Print(s);public static void LogWarning(object s)=>Godot.GD.PushWarning(s.ToString());public static void LogError(object s)=>Godot.GD.PushError(s.ToString());}
public static class Application {public static bool isPlaying=>true;public static string streamingAssetsPath=>Godot.ProjectSettings.GlobalizePath("res://");}
}
