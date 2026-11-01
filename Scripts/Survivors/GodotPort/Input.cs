using System.Collections.Generic;
using Godot;
namespace RMZS.GodotPort.InputSystem {
public static class PortInput {internal static HashSet<Key> Down=new(),Up=new();internal static HashSet<MouseButton> MouseDown=new(),MouseUp=new();internal static Vector2 Wheel;
 public static void Receive(InputEvent e){if(e is InputEventKey k&&!k.Echo){var key=k.PhysicalKeycode!=Key.None?k.PhysicalKeycode:k.Keycode;(k.Pressed?Down:Up).Add(key);}if(e is InputEventMouseButton m){(m.Pressed?MouseDown:MouseUp).Add(m.ButtonIndex);if(m.Pressed&&m.ButtonIndex==MouseButton.WheelUp)Wheel.y+=120;if(m.Pressed&&m.ButtonIndex==MouseButton.WheelDown)Wheel.y-=120;}}
 public static void EndFrame(){Down.Clear();Up.Clear();MouseDown.Clear();MouseUp.Clear();Wheel=Vector2.zero;}public static void Reset()=>EndFrame();}
public class ButtonControl {Key key;MouseButton mouse;public ButtonControl(Key k){key=k;}public ButtonControl(MouseButton m){mouse=m;}public bool isPressed=>mouse!=0?Godot.Input.IsMouseButtonPressed(mouse):Godot.Input.IsPhysicalKeyPressed(key);public bool wasPressedThisFrame=>mouse!=0?PortInput.MouseDown.Contains(mouse):PortInput.Down.Contains(key);public bool wasReleasedThisFrame=>mouse!=0?PortInput.MouseUp.Contains(mouse):PortInput.Up.Contains(key);}
public class VectorControl {bool wheel;public VectorControl(bool w){wheel=w;}public Vector2 ReadValue(){if(wheel)return PortInput.Wheel;var p=SVRuntime.Instance.GetViewport().GetMousePosition();return new(p.X,Screen.height-p.Y);}}
public class Keyboard {public static Keyboard current {get;}=new();public ButtonControl rKey => new(Key.R);
public ButtonControl enterKey => new(Key.Enter);
public ButtonControl numpadEnterKey => new(Key.KpEnter);
public ButtonControl spaceKey => new(Key.Space);
public ButtonControl digit1Key => new(Key.Key1);
public ButtonControl numpad1Key => new(Key.Kp1);
public ButtonControl digit2Key => new(Key.Key2);
public ButtonControl numpad2Key => new(Key.Kp2);
public ButtonControl digit3Key => new(Key.Key3);
public ButtonControl numpad3Key => new(Key.Kp3);
public ButtonControl qKey => new(Key.Q);
public ButtonControl eKey => new(Key.E);
public ButtonControl fKey => new(Key.F);
public ButtonControl cKey => new(Key.C);
public ButtonControl backspaceKey => new(Key.Backspace);
public ButtonControl digit4Key => new(Key.Key4);
public ButtonControl numpad4Key => new(Key.Kp4);
public ButtonControl aKey => new(Key.A);
public ButtonControl leftArrowKey => new(Key.Left);
public ButtonControl dKey => new(Key.D);
public ButtonControl rightArrowKey => new(Key.Right);
public ButtonControl wKey => new(Key.W);
public ButtonControl upArrowKey => new(Key.Up);
public ButtonControl sKey => new(Key.S);
public ButtonControl downArrowKey => new(Key.Down);
public ButtonControl zKey => new(Key.Z);}
public class Mouse {public static Mouse current {get;}=new();public ButtonControl leftButton=>new(MouseButton.Left);public ButtonControl rightButton=>new(MouseButton.Right);public ButtonControl middleButton=>new(MouseButton.Middle);public VectorControl position=>new(false);public VectorControl scroll=>new(true);}
}
namespace RMZS.GodotPort.InputSystem.UI {public class InputSystemUIInputModule:Component {public void AssignDefaultActions(){}}}
