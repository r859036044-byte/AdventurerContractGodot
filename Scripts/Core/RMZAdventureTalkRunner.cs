using Godot;
using System.Collections;
using System.Collections.Generic;
namespace RMZ;
public partial class RMZAdventureTalkRunner:Node {
public static bool IsBusy{get;private set;}
readonly Stack<IEnumerator> stack=new();
public static void Begin(){if(IsBusy)return;IsBusy=true;var n=new RMZAdventureTalkRunner();n.stack.Push(RMZAdventureTalk.PlayEndFromVars());RMZBootstrap.Instance.GetTree().Root.AddChild(n);}
public override void _Process(double delta){try{while(stack.Count>0){var e=stack.Peek();if(!e.MoveNext()){stack.Pop();continue;}if(e.Current is IEnumerator child){stack.Push(child);continue;}return;}QueueFree();}catch(System.Exception e){GD.PushError(e.ToString());QueueFree();}}
public override void _ExitTree(){RMZTalkPortrait.Hide();IsBusy=false;}
}