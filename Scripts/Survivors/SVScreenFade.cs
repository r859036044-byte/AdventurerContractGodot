// 黑屏過場。兩關制地圖從第一關切到第二關（王關）時用這個蓋一下畫面，
// 場地才有時間在背後重新蓋好，不會讓玩家看到障礙物憑空消失、王憑空冒出來。
//
// 純靜態，疊在最上層（sortingOrder 900，比對話框的 700 還高）。
using System.Collections;
using RMZS.GodotPort;
using RMZS.GodotPort.UI;

namespace RMZS
{
    public static class SVScreenFade
    {
        static Canvas _canvas;
        static Image _img;

        static void Ensure()
        {
            if (_img != null) return;

            var go = new GameObject("SVScreenFade");
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 點陣字型要落在整數像素上才不會整體偏一點點
            _canvas.pixelPerfect = true;
            _canvas.sortingOrder = 900;
            go.AddComponent<CanvasScaler>();

            var imgGo = new GameObject("Black");
            imgGo.transform.SetParent(go.transform, false);
            _img = imgGo.AddComponent<Image>();
            _img.color = new Color(0f, 0f, 0f, 0f);
            _img.raycastTarget = false;

            var rt = (RectTransform)imgGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>畫面漸漸變黑（0 → 1）。過場開始前 yield 這個。</summary>
        public static IEnumerator FadeOut(float seconds)
        {
            Ensure();
            _img.raycastTarget = true;   // 黑屏期間別讓玩家點到底下的東西
            yield return Fade(_img.color.a, 1f, seconds);
        }

        /// <summary>畫面漸漸恢復（1 → 0）。場地重蓋好之後 yield 這個。</summary>
        public static IEnumerator FadeIn(float seconds)
        {
            Ensure();
            yield return Fade(_img.color.a, 0f, seconds);
            _img.raycastTarget = false;
        }

        static IEnumerator Fade(float from, float to, float seconds)
        {
            seconds = Mathf.Max(0.05f, seconds);
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                _img.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            _img.color = new Color(0f, 0f, 0f, to);
        }
    }
}
