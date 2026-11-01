// 訊息狀態，對應 MZ 的 Game_Message。UI 由 RMZMessageWindow 負責畫。
using System.Collections.Generic;

namespace RMZ
{
    public class RMZMessage
    {
        public static RMZMessage Instance { get; } = new RMZMessage();

        public readonly List<string> Texts = new List<string>();
        public readonly List<string> Choices = new List<string>();

        public string FaceName = "";
        public int FaceIndex;

        /// <summary>說話者名字（MZ 1.1 之後「顯示文章」的第 5 個參數）。空字串就不顯示名字框。</summary>
        public string SpeakerName = "";
        public int Background;      // 0=視窗 1=暗色 2=透明
        public int PositionType = 2; // 0=上 1=中 2=下

        public int ChoiceDefault = 0;
        public int ChoiceCancel = -1;
        public int ChoiceResult = -1;

        /// <summary>選項選好之後要回填的位置；由直譯器設定。</summary>
        public System.Action<int> OnChoice;

        public bool IsBusy => Texts.Count > 0 || Choices.Count > 0;
        public bool HasChoices => Choices.Count > 0;

        /// <summary>
        /// 每重設一次就 +1。
        /// 事件常常「關掉一段訊息、同一幀馬上開下一段」，中間 IsBusy 不會變成 false，
        /// UI 只看 IsBusy 的話就不會重新套用新內容（選項會整個消失）。
        /// </summary>
        public int Version { get; private set; }

        public void Clear()
        {
            Version++;
            Texts.Clear();
            Choices.Clear();
            FaceName = "";
            FaceIndex = 0;
            SpeakerName = "";
            Background = 0;
            PositionType = 2;
            ChoiceResult = -1;
            OnChoice = null;
        }

        public void Add(string text) => Texts.Add(text);
        public string AllText() => string.Join("\n", Texts);
    }
}
