using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
namespace RMZS {
public partial class SVRuntime:Node2D {
 public static SVRuntime Instance {get;private set;}public Node2D World;public CanvasLayer UILayer;internal readonly List<GodotPort.GameObject> Objects=new();internal readonly List<GodotPort.Coroutine> Routines=new();readonly List<(float left,Action action)> delayed=new();public Action OnClosed;bool closing;
 public override void _EnterTree(){Instance=this;ProcessMode=ProcessModeEnum.Always;World=new Node2D{Name="SurvivorsWorld"};AddChild(World);UILayer=new CanvasLayer{Layer=100};AddChild(UILayer);}
 public override void _Ready(){new GodotPort.GameObject("SurvivorsGame").AddComponent<SVGame>();}
 internal void Delay(float s,Action a)=>delayed.Add((s,a));
 public override void _Input(InputEvent e)=>GodotPort.InputSystem.PortInput.Receive(e);
 public override void _Process(double delta){if(closing)return;try{float dt=(float)Math.Min(delta,.1);GodotPort.Time.deltaTime=GodotPort.Time.unscaledDeltaTime=dt;GodotPort.Time.time+=dt;GodotPort.Time.unscaledTime+=dt;
 var behaviours=Objects.Where(g=>g.activeInHierarchy).SelectMany(g=>g.components).OfType<GodotPort.MonoBehaviour>().Where(c=>c.isActiveAndEnabled).ToArray();
 foreach(var b in behaviours){if(b==null||!b.isActiveAndEnabled)continue;if(!b.started){b.started=true;b.Invoke("Start");}if(b!=null&&b.isActiveAndEnabled){SVHit.Owner=b;b.Invoke("Update");}}SVHit.Owner=null;
 foreach(var r in Routines.ToArray()){SVHit.Owner=r.Owner;r.Step(dt);}SVHit.Owner=null;Routines.RemoveAll(r=>r.Done);
 foreach(var b in behaviours)if(b!=null&&b.isActiveAndEnabled)b.Invoke("LateUpdate");
 for(int i=delayed.Count-1;i>=0;i--){var d=delayed[i];d.left-=dt;if(d.left<=0){delayed.RemoveAt(i);d.action();}else delayed[i]=d;}
 foreach(var g in Objects.ToArray()){if(g.dead)continue;(g.transform as GodotPort.RectTransform)?.Layout();foreach(var c in g.components.ToArray()){if(c is GodotPort.SpriteRenderer sr)sr.Sync();if(c is GodotPort.UI.Graphic gr)gr.Sync();if(c is GodotPort.Camera ca)ca.Sync();if(c is GodotPort.AudioSource au)au.Sync();if(c is GodotPort.TextMesh tm)tm.Sync();}}
 Objects.RemoveAll(g=>g.dead);GodotPort.InputSystem.PortInput.EndFrame();
 }catch(Exception e){GD.PushError("倖存者執行失敗："+e);Close();}}
 public void Close(){if(closing)return;closing=true;CallDeferred(nameof(FinishClose));}
 void FinishClose(){QueueFree();}
 public override void _ExitTree(){SVEffects.Clear();SVTimeStop.Reset();foreach(var g in Objects.ToArray())GodotPort.Object.Destroy(g);Objects.Clear();Routines.Clear();SVProjectile.ResetSession();GodotPort.InputSystem.PortInput.Reset();SVSession.Pending=null;SVGame.ExternalPause=false;Instance=null;OnClosed?.Invoke();}
}
}
