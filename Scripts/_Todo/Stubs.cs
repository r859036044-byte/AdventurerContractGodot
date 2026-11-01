// [尚未移植] 這一批全是空殼，只為了讓已移植的直譯器編得過。
//
// 事件直譯器會去問「某某畫面開著沒」「某某小遊戲跑完沒」，
// 那些 UI 與小遊戲都還在 Unity 那邊沒搬過來。這裡讓它們一律回答
// 「沒開、不忙」，直譯器就會照常往下跑，不會卡在等待狀態。
//
// 移植某個系統時，就把它從這支檔案刪掉，換成真正的實作。
// 對應的 Unity 原始碼路徑都寫在各自的註解裡。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    /// <summary>Unity: UI/RMZDevPanel.cs（484 行）</summary>
    public static class RMZDevPanel { public static bool IsOpen => false; }

    /// <summary>Unity: UI/RMZEnhanceWindow.cs（735 行）</summary>
    public static class RMZEnhanceWindow { public static bool IsOpen => false; }

    /// <summary>Unity: UI/RMZTavernWindow.cs（656 行）</summary>
    public static class RMZTavernWindow { public static bool IsOpen => false; }

    /// <summary>Unity: Slave/RMZSlaveWorkWindow.cs（663 行）</summary>
    public static class RMZSlaveWorkWindow { public static bool IsOpen => false; }

    /// <summary>Unity: Slave/RMZSlaveShopWindow.cs（423 行）</summary>
    public static class RMZSlaveShopWindow { public static bool IsOpen => false; }

    /// <summary>Unity: Heroines/RMZTemptWindow.cs（362 行）</summary>
    public static class RMZTemptWindow { public static bool IsOpen => false; }

    /// <summary>Unity: Heroines/RMZUnionWindow.cs（400 行）</summary>
    public static class RMZUnionWindow { public static bool IsOpen => false; }

    /// <summary>Unity: UI/RMZLootReport.cs（367 行）</summary>

    /// <summary>Unity: Slave/RMZSlaveWork.cs（668 行）</summary>
    public static class RMZSlaveWork { public static void AutoResolve() { } }

    /// <summary>Unity: Adventure/RMZSurvivorsBridge.cs（532 行）—— 認真探索小遊戲的橋接</summary>

    /// <summary>Unity: Plants/PZDefense.cs 那套守城小遊戲的橋接</summary>
    public static class RMZDefenseBridge { public static bool IsBusy => false; }

    /// <summary>Unity: Adventure/RMZAdventureTalk.cs（442 行）</summary>


}
