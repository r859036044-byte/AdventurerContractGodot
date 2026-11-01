// 小遊戲用到的外部圖片。放在 Assets/Resources/Survivors/ 底下，靠檔名載入。
//
// Resources.Load<Sprite> 只認 Sprite Mode = Single 的圖；
// 設成 Multiple 的話要走 LoadAll 取第一張，所以兩種都試一次，
// 匯入設定不小心改到也不會整個特效消失。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public static class SVArt
    {
        const string EffectFolder = "Survivors/Effect";
        const string UIFolder = "Survivors/UI";
        const string IconFolder = "Survivors/Icon";

        // 每張圖只找一次，找不到也記起來，不要每次攻擊都重試
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>男主普攻的劍氣（藍）。</summary>
        public static Sprite PlayerSlash => Slash("player_attack");

        /// <summary>拿法杖時的彈道。目前是劍氣的複製品，之後直接換圖就好。</summary>
        public static Sprite PlayerStaff => SVVisualAssets.Spell("Arcane Bolt", Time.time);

        /// <summary>尤梨卡的追蹤劍氣（粉紅）。</summary>
        public static Sprite YurikaSlash => Slash("Yurika_attack");

        /// <summary>凱的重斬劍氣（黃）。</summary>
        public static Sprite KaiSlash => Slash("Kai_attack");

        /// <summary>鈴音星隕留在地上的燃燒地面。圖已裁成圓盤外接方框，直徑 = 圖寬。</summary>
        public static Sprite BurnGround => Slash("Fire_Ground");

        /// <summary>血條左邊那顆愛心。</summary>
        public static Sprite Heart => UI("hp_heart");

        /// <summary>掉在地上的金幣。</summary>
        public static Sprite Coin => UI("coin");

        /// <summary>地上的補給包（血包）。</summary>
        public static Sprite HealPack => UI("heal_pack");

        /// <summary>某種補給品的專屬貼圖。沒有就回 null，呼叫端會畫回原本的色球。</summary>
        public static Sprite PickupIcon(RMZS.SVPickupKind kind) => kind switch
        {
            RMZS.SVPickupKind.Heal => HealPack,
            _ => null,
        };

        /// <summary>
        /// 技能圖示。放 Assets/Resources/Survivors/Icon/，檔名不含副檔名。
        /// 還沒放圖就回 null，介面會留一個空的圖示框，不會報錯。
        /// </summary>
        public static Sprite SkillIcon(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            string key = "icon/" + fileName;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var sprite = SVSimpleIcons.Create(fileName);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>UI 資料夾裡的一張圖。</summary>
        public static Sprite UI(string fileName)
        {
            string key = "ui/" + fileName;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var sprite = LoadSprite(UIFolder + "/" + fileName);
            if (sprite == null)
                Debug.LogWarning($"[倖存者] 找不到 Resources/{UIFolder}/{fileName}。");

            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 施法落點的魔法陣。跟劍氣同一個資料夾，這裡只是換個名字讓呼叫端讀起來清楚。
        /// 找不到就回 null，呼叫端會退回原本的圓圈。
        /// </summary>
        public static Sprite MagicCircle(string fileName) => Slash(fileName);

        /// <summary>Effect 資料夾裡的一張劍氣圖。找不到就回 null，呼叫端會退回原本的光球。</summary>
        public static Sprite Slash(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            if (Cache.TryGetValue(fileName, out var cached)) return cached;

            var sprite = LoadSprite(EffectFolder + "/" + fileName);
            if (sprite == null)
                Debug.LogWarning(
                    $"[倖存者] 讀不到 Resources/{EffectFolder}/{fileName}，先用程式畫的圖形代替。\n" +
                    "檔案在的話，多半是匯入設定的 Sprite Mode 設成了 Multiple —— " +
                    "Resources.Load<Sprite> 只吃 Single。在 Inspector 把它改成 Single 再 Apply。");

            Cache[fileName] = sprite;
            return sprite;
        }

        static Sprite LoadSprite(string path)
        {
            var single = Resources.Load<Sprite>(path);
            if (single != null) return single;

            var all = Resources.LoadAll<Sprite>(path);
            return all != null && all.Length > 0 ? all[0] : null;
        }
    }
}
