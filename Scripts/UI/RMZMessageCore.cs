// VisuMZ_1_MessageCore 的「對話框屬性」外掛指令。
//
// 信件、日記、人物介紹這些畫面會先把對話框改成很多行、窄一點、往旁邊挪，
// 讓字剛好落在羊皮紙／介紹底圖上面，演完再改回 4 行、寬 1280、位移 0：
//   MessageWindowProperties  Rows / Width / WordWrap
//   MessageWindowXyOffsets   OffsetX / OffsetY
// 預設值照 RM 專案的 MessageCore 參數（MessageRows 4、MessageWidth 1280、位移 0）。
namespace RMZ
{
    public static class RMZMessageCore
    {
        public const int DefaultRows = 4;
        public const int DefaultWidth = 1280;

        public static int Rows = DefaultRows;
        public static int Width = DefaultWidth;
        public static int OffsetX, OffsetY;
        /// <summary>外掛指令的 WordWrap = true：整個對話框都自動換行（等同每段都加 &lt;WordWrap&gt;）。</summary>
        public static bool WordWrap;

        /// <summary>有沒有改過行數／寬度／位置（改過就照 RM 的算法擺對話框）。</summary>
        public static bool IsCustom => Rows != DefaultRows || Width != DefaultWidth || OffsetX != 0 || OffsetY != 0;

        /// <summary>
        /// 行數比預設多的，就是「字寫在圖片上」的那種畫面（信件、人物介紹、日記）：
        /// 不畫對話框底、不畫名字框，字直接疊在圖片上。
        /// </summary>
        public static bool IsPage => Rows > DefaultRows;

        public static void SetProperties(int rows, int width, string wordWrap)
        {
            if (rows > 0) Rows = rows;
            if (width > 0) Width = width;
            if (wordWrap == "true") WordWrap = true;
            else if (wordWrap == "false") WordWrap = false;
        }

        public static void SetOffsets(int x, int y) { OffsetX = x; OffsetY = y; }

        public static void Reset()
        {
            Rows = DefaultRows; Width = DefaultWidth; OffsetX = OffsetY = 0; WordWrap = false;
        }
    }
}
