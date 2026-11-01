// 實心物件：門、箱子這類東西不該被半格穿身。
//
// HalfMove 的 EventThrough（イベントすり抜け）本意是「側面接觸 NPC 時可以半格閃身過去」，
// 但它的判斷是「Y 不一樣就不擋路」，套到門上就變成玩家可以站進門裡（上半身疊在門上）。
//
// 這裡的做法：把「物件類」事件標成實心，實心的不套用 EventThrough，
// 需要的話還可以把碰撞框往上加高。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    [System.Serializable]
    public class RMZSolidObjectSettings
    {
        public bool Enabled = true;

        public bool ObjectGraphicsAreSolid = true;

        public List<string> NameKeywords = new List<string> { "門", "箱", "櫃", "桶" };

        public List<string> GraphicKeywords = new List<string> { "Door", "Chest", "Gate" };

        /// <summary>實心物件的碰撞框往「上」加高多少格（0~1）。0 = 只用標準的 1x1。</summary>
        public float ExtraHeightUp = 0f;

        /// <summary>事件的名稱或備註寫 &lt;solid&gt; 強制實心、&lt;nosolid&gt; 強制不實心。</summary>
        public const string ForceSolidTag = "<solid>";
        public const string ForceThroughTag = "<nosolid>";

        public static RMZSolidObjectSettings Current =>
            RMZBootstrap.Instance != null && RMZBootstrap.Instance.SolidObjects != null
                ? RMZBootstrap.Instance.SolidObjects
                : Fallback;

        static readonly RMZSolidObjectSettings Fallback = new RMZSolidObjectSettings();

        /// <summary>判斷一個事件算不算實心物件。</summary>
        public bool IsSolid(string eventName, string note, string graphicName)
        {
            if (!Enabled) return false;

            string tags = (eventName ?? "") + "\n" + (note ?? "");
            if (tags.Contains(ForceThroughTag)) return false;
            if (tags.Contains(ForceSolidTag)) return true;

            if (ObjectGraphicsAreSolid && !string.IsNullOrEmpty(graphicName) && graphicName.StartsWith("!"))
                return true;

            if (!string.IsNullOrEmpty(eventName) && NameKeywords != null)
                foreach (var k in NameKeywords)
                    if (!string.IsNullOrEmpty(k) && eventName.Contains(k)) return true;

            if (!string.IsNullOrEmpty(graphicName) && GraphicKeywords != null)
                foreach (var k in GraphicKeywords)
                    if (!string.IsNullOrEmpty(k) && graphicName.Contains(k)) return true;

            return false;
        }

        /// <summary>
        /// 這個角色擋不擋得住站在 (x, y) 的人。
        /// 實心物件不吃 EventThrough，而且碰撞框可以往上加高。
        /// </summary>
        public static bool Blocks(RMZCharacterBase c, float x, float y)
        {
            const float eps = 0.001f;

            float dx = Mathf.Abs(c.X - x);
            if (dx >= 1f - eps) return false;

            if (!c.SolidObject)
            {
                if (Mathf.Abs(c.Y - y) >= 1f - eps) return false;
                // 一般事件：Y 不同就可以側身閃過（HalfMove 的 EventThrough）
                return !(HalfMoveSettings.EventThrough && !HalfMoveSettings.Same(c.Y, y));
            }

            // 實心物件：往上加高（y 比較小 = 比較上面）
            float up = 1f + Mathf.Max(0f, Current.ExtraHeightUp);
            float dy = c.Y - y;                 // > 0 表示對方在下面、我在上面
            if (dy > 0f) return dy < up - eps;
            return -dy < 1f - eps;
        }
    }
}
