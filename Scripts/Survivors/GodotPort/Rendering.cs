using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Godot;
namespace RMZS.GodotPort {
public enum TextureFormat {RGBA32}public enum FilterMode {Point,Bilinear}public enum TextureWrapMode {Clamp,Repeat}public enum SpriteMeshType {FullRect}
public class Texture2D:Object {
 internal Godot.Image Img;internal ImageTexture Native;public FilterMode filterMode;public TextureWrapMode wrapMode;public int width=>Img.GetWidth();public int height=>Img.GetHeight();
 public Texture2D(int w,int h,TextureFormat f=TextureFormat.RGBA32,bool mip=false){Img=Godot.Image.CreateEmpty(w,h,false,Godot.Image.Format.Rgba8);}
 public bool LoadImage(byte[] bytes,bool unreadable=false){var err=Img.LoadPngFromBuffer(bytes);if(err!=Error.Ok)err=Img.LoadJpgFromBuffer(bytes);if(err!=Error.Ok)return false;Native=ImageTexture.CreateFromImage(Img);return true;}
 public void SetPixels(Color[] pixels){for(int y=0;y<height;y++)for(int x=0;x<width;x++)Img.SetPixel(x,height-1-y,pixels[y*width+x]);}
 public void SetPixels32(Color32[] pixels)=>SetPixels(pixels.Select(p=>(Color)p).ToArray());public Color GetPixel(int x,int y)=>Img.GetPixel(x,height-1-y);
 public void Apply(bool mip=false,bool unreadable=false){Native=ImageTexture.CreateFromImage(Img);}
}
public class Sprite:Object {public Texture2D texture;public Rect rect;public Vector2 pivot;public float pixelsPerUnit;internal AtlasTexture Native;public Vector4 border;
 public Bounds bounds=>new(){size=new Vector3(rect.width/pixelsPerUnit,rect.height/pixelsPerUnit,0)};
 public static Sprite Create(Texture2D tex,Rect r,Vector2 p,float ppu=100,uint extrude=0,SpriteMeshType mesh=SpriteMeshType.FullRect,Vector4 border=default){if(tex.Native==null)tex.Apply();return new Sprite{texture=tex,rect=r,pivot=p,pixelsPerUnit=ppu,border=border,Native=new AtlasTexture{Atlas=tex.Native,Region=new Rect2(r.x,tex.height-r.y-r.height,r.width,r.height)}};}
}
public class Shader:Object {internal Godot.Shader Native;public static Shader Find(string name)=>new(){Native=new Godot.Shader{Code="shader_type canvas_item; uniform float saturation=1.0; void fragment(){vec4 c=texture(TEXTURE,UV)*COLOR;float g=dot(c.rgb,vec3(0.299,0.587,0.114));COLOR=vec4(mix(vec3(g),c.rgb,saturation),c.a);}"}};}
public class Material:Object {internal ShaderMaterial Native;public Texture2D mainTexture;public Material(Shader s){Native=new ShaderMaterial{Shader=s.Native};}public void SetFloat(string key,float v)=>Native.SetShaderParameter(key=="_Saturation"?"saturation":key,v);}
public enum SpriteDrawMode {Simple,Sliced,Tiled}
public class SpriteRenderer:Component {
 public Vector2 visualOffset,visualScale=Vector2.one;public SpriteDrawMode drawMode;public Vector2 size=Vector2.one;public Sprite sprite;public Color color=Color.white;public int sortingOrder;public string sortingLayerName;public bool flipX,flipY;public Material sharedMaterial;public Material material {get=>sharedMaterial;set=>sharedMaterial=value;}internal Sprite2D Visual;
 internal void Attach(){Visual=new Sprite2D{ZAsRelative=false};gameObject.Node.AddChild(Visual);}
 internal void Sync(){Visual.Visible=enabled&&sprite!=null;if(sprite==null)return;Visual.Texture=sprite.Native;Visual.Centered=false;Visual.Offset=new(-sprite.rect.width*sprite.pivot.x,-sprite.rect.height*(1-sprite.pivot.y));Visual.Scale=Godot.Vector2.One*(48/sprite.pixelsPerUnit);if(drawMode!=SpriteDrawMode.Simple){Visual.Scale=new(size.x*48/sprite.rect.width,size.y*48/sprite.rect.height);}Visual.Position=visualOffset;Visual.Scale*= (Godot.Vector2)visualScale;Visual.RegionEnabled=drawMode==SpriteDrawMode.Tiled;if(drawMode==SpriteDrawMode.Tiled){Visual.Texture=sprite.texture.Native;Visual.TextureRepeat=CanvasItem.TextureRepeatEnum.Enabled;var pixels=new Godot.Vector2(size.x*sprite.pixelsPerUnit,size.y*sprite.pixelsPerUnit);Visual.RegionRect=new Rect2(Godot.Vector2.Zero,pixels);Visual.Scale=Godot.Vector2.One*(48/sprite.pixelsPerUnit);Visual.Offset=-pixels*.5f;}Visual.Modulate=color;Visual.ZIndex=Math.Clamp(sortingOrder/8,-4096,4096);Visual.FlipH=flipX;Visual.FlipV=flipY;Visual.Material=sharedMaterial?.Native;Visual.TextureFilter=sprite.texture.filterMode==FilterMode.Point?CanvasItem.TextureFilterEnum.Nearest:CanvasItem.TextureFilterEnum.Linear;}
}
public class Font:Object {internal Godot.Font Native=RMZ.RMZFonts.Main;public Material material;}
public enum TextAnchor {UpperLeft,UpperCenter,UpperRight,MiddleLeft,MiddleCenter,MiddleRight,LowerLeft,LowerCenter,LowerRight}public enum TextAlignment {Left,Center,Right}public enum FontStyle {Normal,Bold,Italic,BoldAndItalic}public enum HorizontalWrapMode {Wrap,Overflow}public enum VerticalWrapMode {Truncate,Overflow}
public class MeshRenderer:Component {public Material sharedMaterial;public int sortingOrder;}
public class TextMesh:Component {public string text;public Font font;public int fontSize=48;public float characterSize=.06f;public TextAnchor anchor;public TextAlignment alignment;public Color color;Label label;
 internal void Attach(){label=new Label{MouseFilter=Control.MouseFilterEnum.Ignore,ZIndex=3900,ZAsRelative=false};gameObject.Node.AddChild(label);}
 internal void Sync(){label.Text=text;label.AddThemeFontOverride("font",font?.Native??RMZ.RMZFonts.Main);label.AddThemeFontSizeOverride("font_size",fontSize);label.Modulate=color;label.Scale=Godot.Vector2.One*(characterSize*48/10);label.Position=new(-label.Size.X*label.Scale.X/2,-label.Size.Y*label.Scale.Y);}}
public class AudioClip:Object {internal AudioStream Native;}
public class AudioSource:Component {public AudioClip clip;public bool loop,playOnAwake;public float volume=1,spatialBlend;AudioStreamPlayer player;public bool isPlaying=>player?.Playing??false;internal void Attach(){player=new AudioStreamPlayer();gameObject.Node.AddChild(player);player.Finished+=()=>{if(loop&&!dead)player.Play();};}public void Play(){player.Stream=clip?.Native;player.Play();}public void Stop()=>player.Stop();internal void Sync(){player.VolumeDb=volume<=0?-80:Mathf.Max(-80,20*MathF.Log10(volume));}}
public static class Resources {
 static readonly Dictionary<string,Object> cache=new();public static T Load<T>(string key) where T:Object {
 string ck=typeof(T).Name+key;if(cache.TryGetValue(ck,out var hit))return hit as T;string dir=ProjectSettings.GlobalizePath("res://PortAssets/"),p=Path.Combine(dir,key);Object result=null;
 if(typeof(T)==typeof(Sprite)){var file=new[]{".png",".jpg",".jpeg"}.Select(ext=>p+ext).FirstOrDefault(File.Exists);if(file!=null){var t=new Texture2D(2,2);if(t.LoadImage(File.ReadAllBytes(file))){float ppu=100;try{var meta=File.ReadAllText(file+".meta");var m=System.Text.RegularExpressions.Regex.Match(meta,@"spritePixelsToUnits: ([0-9.]+)");if(m.Success)ppu=float.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);}catch(IOException){}result=Sprite.Create(t,new Rect(0,0,t.width,t.height),new Vector2(.5f,.5f),ppu);}}}
 else if(typeof(T)==typeof(Font))result=new Font();
 else if(typeof(T)==typeof(AudioClip)){foreach(var ext in new[]{".ogg",".mp3",".wav"}){string f=p+ext;if(!File.Exists(f))continue;AudioStream stream=ext==".ogg"?AudioStreamOggVorbis.LoadFromFile(f):ext==".mp3"?AudioStreamMP3.LoadFromFile(f):AudioStreamWav.LoadFromFile(f);result=new AudioClip{Native=stream};break;}}
 cache[ck]=result;return result as T;
 }
 public static T[] LoadAll<T>(string key) where T:Object {var one=Load<T>(key);if(one!=null)return new[]{one};var dir=Path.Combine(ProjectSettings.GlobalizePath("res://PortAssets/"),key);if(!Directory.Exists(dir))return Array.Empty<T>();return Directory.GetFiles(dir).Where(p=>!p.EndsWith(".meta")).Select(p=>Load<T>(key+"/"+Path.GetFileNameWithoutExtension(p))).Where(p=>p!=null).ToArray();}
 public static T GetBuiltinResource<T>(string key) where T:Object=>Load<T>(key);
}
}
namespace RMZS {public static class SVFonts {public static GodotPort.Font Pixel {get;}=new();}
}
