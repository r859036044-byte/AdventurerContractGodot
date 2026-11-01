using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Godot;
namespace RMZS.GodotPort {
public class Object {
 internal bool dead; public string name="";
 static bool Missing(Object a)=>ReferenceEquals(a,null)||a.dead||(a is Component c&&!ReferenceEquals(c.gameObject,null)&&c.gameObject.dead);
 public static bool operator ==(Object a,Object b)=>Missing(a)&&Missing(b)||ReferenceEquals(a,b);public static bool operator !=(Object a,Object b)=>!(a==b);public static implicit operator bool(Object a)=>!Missing(a);public override bool Equals(object o)=>ReferenceEquals(this,o);public override int GetHashCode()=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
 public static void Destroy(Object o){if(Missing(o))return;if(o is GameObject g){foreach(var ch in g.transform.children.ToArray())Destroy(ch.gameObject);foreach(var c in g.components.ToArray())Destroy(c);g.dead=true;g.transform.parent?.children.Remove(g.transform);g.Node.QueueFree();if(g.UI!=null&&GodotObject.IsInstanceValid(g.UI))g.UI.QueueFree();}else if(o is Component c){if(c is MonoBehaviour m){m.Invoke("OnDisable");m.Invoke("OnDestroy");m.StopAllCoroutines();}c.dead=true;c.gameObject.components.Remove(c);if(c is SpriteRenderer sr)sr.Visual.QueueFree();}else o.dead=true;}
 public static void Destroy(Object o,float seconds){SVRuntime.Instance.Delay(seconds,()=>Destroy(o));}
 public static T FindAnyObjectByType<T>() where T:Component=>SVRuntime.Instance?.Objects.Where(g=>g.activeInHierarchy).SelectMany(g=>g.components).OfType<T>().FirstOrDefault(c=>!c.dead);
 public static T[] FindObjectsByType<T>(FindObjectsInactive inactive,FindObjectsSortMode sort) where T:Component=>SVRuntime.Instance?.Objects.Where(g=>!g.dead&&(inactive==FindObjectsInactive.Include||g.activeInHierarchy)).SelectMany(g=>g.components).OfType<T>().Where(c=>!c.dead).ToArray()??Array.Empty<T>();
 public static void DontDestroyOnLoad(Object o){} // 所有移植物件均由單次探索的 SVRuntime 擁有。
}
public enum FindObjectsInactive {Exclude,Include} public enum FindObjectsSortMode {None}
public class Component:Object {
 public GameObject gameObject;public Transform transform=>gameObject.transform;public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>();public T AddComponent<T>() where T:Component,new()=>gameObject.AddComponent<T>();
 public T GetComponentInChildren<T>() where T:Component=>GetComponent<T>()??transform.children.Select(c=>c.gameObject.GetComponentInChildren<T>()).FirstOrDefault(c=>c!=null);
 public T[] GetComponentsInChildren<T>(bool includeInactive=false) where T:Component=>gameObject.GetComponentsInChildren<T>(includeInactive);
 bool _enabled=true;public bool enabled {get=>_enabled;set{if(_enabled==value)return;_enabled=value;if(this is MonoBehaviour m)m.Invoke(value?"OnEnable":"OnDisable");}} public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy;
}
public class MonoBehaviour:Component {
 readonly Dictionary<string,Action> methods=new(); internal bool started;
 internal void Invoke(string n){if(!methods.TryGetValue(n,out var a)){var t=GetType();MethodInfo mi=null;while(t!=null&&mi==null){mi=t.GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly,null,Type.EmptyTypes,null);t=t.BaseType;}a=mi==null?null:(Action)mi.CreateDelegate(typeof(Action),this);methods[n]=a;}a?.Invoke();}
 readonly Dictionary<string,Coroutine> namedRoutines=new();
 public Coroutine StartCoroutine(string name){var method=GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);var c=StartCoroutine((IEnumerator)method.Invoke(this,null));namedRoutines[name]=c;return c;}
 public void StopCoroutine(string name){if(namedRoutines.TryGetValue(name,out var c))StopCoroutine(c);}
 public Coroutine StartCoroutine(IEnumerator e){var c=new Coroutine(this,e);SVRuntime.Instance.Routines.Add(c);c.Step(0);return c;}public void StopCoroutine(Coroutine c){if(c!=null)c.Done=true;}public void StopAllCoroutines(){foreach(var c in SVRuntime.Instance.Routines)if(c.Owner==this)c.Done=true;}
}
public class Coroutine {
 internal MonoBehaviour Owner;internal bool Done;readonly Stack<IEnumerator> stack=new();float wait;Coroutine waiting;
 internal Coroutine(MonoBehaviour o,IEnumerator e){Owner=o;stack.Push(e);}
 internal void Step(float dt){if(Done||Owner==null){Done=true;return;}if(!Owner.gameObject.activeInHierarchy)return;if(waiting!=null&&!waiting.Done)return;waiting=null;if(wait>0){wait-=dt;if(wait>0)return;}
 for(int guard=0;guard<1000&&stack.Count>0;guard++){var e=stack.Peek();if(!e.MoveNext()){(e as IDisposable)?.Dispose();stack.Pop();continue;}if(e.Current is IEnumerator child){stack.Push(child);continue;}if(e.Current is WaitForSeconds w)wait=w.Seconds;else if(e.Current is Coroutine c)waiting=c;return;}Done=stack.Count==0;}
}
public class WaitForSeconds {internal float Seconds;public WaitForSeconds(float s){Seconds=s;}}
public class WaitForSecondsRealtime:WaitForSeconds {public WaitForSecondsRealtime(float s):base(s){}}
public class GameObject:Object {
 internal Node2D Node;internal Control UI;internal readonly List<Component> components=new();public Transform transform;public string tag="";bool active=true;
 public GameObject(string n="GameObject",params Type[] types){name=n;Node=new Node2D{Name=n};transform=new RectTransform{gameObject=this};SVRuntime.Instance.World.AddChild(Node);SVRuntime.Instance.Objects.Add(this);foreach(var t in types)if(t!=typeof(Transform)&&t!=typeof(RectTransform))AddComponent(t);}
 public bool activeSelf=>active;public bool activeInHierarchy=>!dead&&active&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
 public void SetActive(bool v){if(active==v)return;active=v;Node.Visible=v;if(UI!=null)UI.Visible=v;foreach(var c in components.OfType<MonoBehaviour>().ToArray())c.Invoke(v?"OnEnable":"OnDisable");}
 public T AddComponent<T>() where T:Component,new()=>(T)AddComponent(typeof(T));
 public Component AddComponent(Type t){if(t==typeof(Transform)||t==typeof(RectTransform))return transform;var c=(Component)Activator.CreateInstance(t);c.gameObject=this;components.Add(c);if(c is SpriteRenderer sr)sr.Attach();if(c is Canvas ca)ca.Attach();if(c is UI.Graphic gr)gr.Attach();if(c is AudioSource au)au.Attach();if(c is TextMesh tm)tm.Attach();if(c is MonoBehaviour m){m.Invoke("Awake");if(c!=null&&activeInHierarchy)m.Invoke("OnEnable");}return c;}
 public T GetComponent<T>() where T:Component=>transform is T t?t:components.OfType<T>().FirstOrDefault(c=>!c.dead);
 public T GetComponentInChildren<T>() where T:Component=>GetComponent<T>()??transform.children.Select(t=>t.gameObject.GetComponentInChildren<T>()).FirstOrDefault(t=>t!=null);
 public T[] GetComponentsInChildren<T>(bool inactive=false) where T:Component=>components.OfType<T>().Concat(transform.children.Where(t=>inactive||t.gameObject.activeInHierarchy).SelectMany(t=>t.gameObject.GetComponentsInChildren<T>(inactive))).ToArray();
 internal Control EnsureUI(){if(UI!=null)return UI;UI=new Control{Name=name,MouseFilter=Control.MouseFilterEnum.Ignore};var p=transform.parent;if(p!=null)p.gameObject.EnsureUI().AddChild(UI);else SVRuntime.Instance.UILayer.AddChild(UI);return UI;}
}
public class Transform:Component,IEnumerable<Transform> {
 internal readonly List<Transform> children=new();public Transform parent {get;private set;}public int childCount=>children.Count;public Transform GetChild(int n)=>children[n];public IEnumerator<Transform> GetEnumerator()=>children.GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
 public Vector3 position {get{var p=gameObject.Node.GlobalPosition;return new(p.X/48,-p.Y/48);}set=>gameObject.Node.GlobalPosition=new(value.x*48,-value.y*48);}
 public Vector3 localPosition {get{var p=gameObject.Node.Position;return new(p.X/48,-p.Y/48);}set=>gameObject.Node.Position=new(value.x*48,-value.y*48);}
 public Vector3 localScale {get{var s=gameObject.Node.Scale;return new(s.X,s.Y,1);}set=>gameObject.Node.Scale=new(value.x,value.y);}
 public Quaternion rotation {get=>Quaternion.Euler(0,0,-gameObject.Node.GlobalRotationDegrees);set=>gameObject.Node.GlobalRotationDegrees=-value.z;}
 public Quaternion localRotation {get=>Quaternion.Euler(0,0,-gameObject.Node.RotationDegrees);set=>gameObject.Node.RotationDegrees=-value.z;}
 public void Rotate(float x,float y,float z){gameObject.Node.RotationDegrees-=z;}
 public void SetParent(Transform p,bool worldPositionStays=true){var pos=position;parent?.children.Remove(this);parent=p;p?.children.Add(this);gameObject.Node.Reparent(p?.gameObject.Node??SVRuntime.Instance.World,worldPositionStays);if(!worldPositionStays)localPosition=Vector3.zero;if(gameObject.UI!=null)gameObject.UI.Reparent((Node)p?.gameObject.EnsureUI()??SVRuntime.Instance.UILayer,false);}
 public bool IsChildOf(Transform t)=>this==t||(parent!=null&&parent.IsChildOf(t));
 public void SetSiblingIndex(int i){if(parent!=null){parent.children.Remove(this);parent.children.Insert(Math.Clamp(i,0,parent.children.Count),this);}if(gameObject.UI!=null)gameObject.UI.GetParent().MoveChild(gameObject.UI,Math.Clamp(i,0,gameObject.UI.GetParent().GetChildCount()-1));}
 public Vector3 lossyScale { get { var s=gameObject.Node.GlobalScale; return new(s.X,s.Y,1); } }
 public void SetAsFirstSibling()=>SetSiblingIndex(0);
 public void SetAsLastSibling()=>SetSiblingIndex(parent?.children.Count-1??0);
}
public class RectTransform:Transform {
 public Vector2 anchorMin=new(.5f,.5f),anchorMax=new(.5f,.5f),pivot=new(.5f,.5f);public Vector2 sizeDelta=new(100,100),anchoredPosition;
 public Vector2 offsetMin {get=>anchoredPosition-new Vector2(sizeDelta.x*pivot.x,sizeDelta.y*pivot.y);set{var old=offsetMax;sizeDelta=old-value;anchoredPosition=value+new Vector2(sizeDelta.x*pivot.x,sizeDelta.y*pivot.y);}}
 public Vector2 offsetMax {get=>anchoredPosition+new Vector2(sizeDelta.x*(1-pivot.x),sizeDelta.y*(1-pivot.y));set{var old=offsetMin;sizeDelta=value-old;anchoredPosition=old+new Vector2(sizeDelta.x*pivot.x,sizeDelta.y*pivot.y);}}
 public Rect rect=>new(0,0,gameObject.UI?.Size.X??sizeDelta.x,gameObject.UI?.Size.Y??sizeDelta.y);
 internal void Layout(){var ui=gameObject.UI;if(ui==null)return;var canvas=gameObject.GetComponent<Canvas>();if(canvas!=null){canvas.Layout();return;}Vector2 ps=parent?.gameObject.UI?.Size??(Godot.Vector2)Screen.Size;var sz=new Vector2(ps.x*(anchorMax.x-anchorMin.x)+sizeDelta.x,ps.y*(anchorMax.y-anchorMin.y)+sizeDelta.y);var ap=new Vector2(ps.x*(anchorMin.x+(anchorMax.x-anchorMin.x)*pivot.x),ps.y*(anchorMin.y+(anchorMax.y-anchorMin.y)*pivot.y));var lower=ap+anchoredPosition-new Vector2(sz.x*pivot.x,sz.y*pivot.y);ui.Size=new(Math.Max(0,sz.x),Math.Max(0,sz.y));ui.Position=new(lower.x,ps.y-lower.y-sz.y);ui.Visible=gameObject.activeSelf;var group=gameObject.GetComponent<CanvasGroup>();ui.Modulate=new Godot.Color(1,1,1,group?.alpha??1);}
}
public enum RenderMode {ScreenSpaceOverlay} public class Canvas:Component {public RenderMode renderMode;public bool pixelPerfect;public int sortingOrder;public float scaleFactor=1;internal CanvasLayer layer;internal void Attach(){layer=new CanvasLayer();SVRuntime.Instance.AddChild(layer);var ui=gameObject.EnsureUI();ui.Reparent(layer,false);}internal void Layout(){var ui=gameObject.UI;var scaler=GetComponent<UI.CanvasScaler>();var rr=scaler?.referenceResolution??new Vector2(1280,720);scaleFactor=Math.Min(Screen.width/rr.x,Screen.height/rr.y);if(scaleFactor<=0)scaleFactor=1;ui.Position=Godot.Vector2.Zero;ui.Size=new(Screen.width/scaleFactor,Screen.height/scaleFactor);ui.Scale=Godot.Vector2.One*scaleFactor;layer.Layer=100+sortingOrder;ui.Visible=gameObject.activeSelf;}}
public class CanvasRenderer:Component {}
public static class Time {public static float deltaTime,unscaledDeltaTime,time,unscaledTime;}
public static class Screen {public static Vector2 Size=>SVRuntime.Instance?.GetViewportRect().Size??new Godot.Vector2(1280,720);public static int width=>(int)Size.x;public static int height=>(int)Size.y;}
public enum CameraClearFlags {SolidColor}
public class Camera:Component {public static Camera main=>FindAnyObjectByType<Camera>();public bool orthographic;public float orthographicSize=8.5f;public CameraClearFlags clearFlags;public Color backgroundColor;internal Camera2D Native;
 internal void Sync(){if(Native==null){Native=new Camera2D();gameObject.Node.AddChild(Native);Native.MakeCurrent();}Native.Zoom=Godot.Vector2.One*(Screen.height/(orthographicSize*96));SVRuntime.Instance.GetViewport().CanvasItemDefaultTextureFilter=Viewport.DefaultCanvasItemTextureFilter.Nearest;}
 public Vector3 ScreenToWorldPoint(Vector3 p){var v=SVRuntime.Instance.GetViewport().GetCanvasTransform().AffineInverse()*new Godot.Vector2(p.x,Screen.height-p.y);return new(v.X/48,-v.Y/48);}
 public Vector3 WorldToScreenPoint(Vector3 p){var v=SVRuntime.Instance.GetViewport().GetCanvasTransform()*new Godot.Vector2(p.x*48,-p.y*48);return new(v.X,Screen.height-v.Y);}
}
public static class RectTransformUtility {public static bool RectangleContainsScreenPoint(RectTransform r,Vector2 screen,Camera camera)=>r?.gameObject.UI!=null&&r.gameObject.UI.GetGlobalRect().HasPoint(new(screen.x,Screen.height-screen.y));}
}
