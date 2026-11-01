// 長按型按鈕。uGUI 的 Button 只有「放開才算一次點擊」，
// 撤退鈕需要知道「正在按住」，所以自己收指標事件。
using System;
using RMZS.GodotPort;
using RMZS.GodotPort.EventSystems;

namespace RMZS
{
    public class SVHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action<bool> OnHold;

        public void OnPointerDown(PointerEventData e) => OnHold?.Invoke(true);
        public void OnPointerUp(PointerEventData e) => OnHold?.Invoke(false);

        // 按住後把游標拖出按鈕就當放開，不然會一直卡在按住狀態
        public void OnPointerExit(PointerEventData e) => OnHold?.Invoke(false);

        void OnDisable() => OnHold?.Invoke(false);
    }
}
