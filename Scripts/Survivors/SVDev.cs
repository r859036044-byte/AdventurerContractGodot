// 除錯面板改過的玩家數值。放在 RMZS 這邊，是因為依賴方向只能 RMZ -> RMZS：
// 面板（RMZ）寫進來，倖存者這邊（RMZS）讀出去，不會反向引用。
using RMZS.GodotPort;

namespace RMZS
{
    public static class SVDev
    {
        /// <summary>DEV 面板開著。開著的時候小遊戲整個暫停。</summary>
        public static bool PanelOpen;

        /// <summary>男主血量上限。0 = 不覆寫，照 SVConfig 走。</summary>
        public static int PlayerMaxHp;

        /// <summary>男主普攻傷害。0 = 不覆寫，照原本的成長走。</summary>
        public static int PlayerAttack;

        /// <summary>現在生效的值，面板開起來時要顯示這個。</summary>
        public static int CurrentMaxHp =>
            PlayerMaxHp > 0 ? PlayerMaxHp : SVConfig.PlayerMaxHp;

        public static int CurrentAttack
        {
            get
            {
                var game = SVGame.Instance;
                if (game != null && game.Player != null) return game.Player.AttackDamage;
                return PlayerAttack > 0 ? PlayerAttack : 4;
            }
        }

        /// <summary>套到一個剛生出來（或正在場上）的男主身上。</summary>
        public static void Apply(SVPlayer p)
        {
            if (p == null) return;

            if (PlayerMaxHp > 0)
            {
                int before = p.MaxHp;
                p.MaxHp = PlayerMaxHp;
                // 場上調的話血量跟著加，不要讓玩家因為改上限反而變成滿血
                p.Hp = before > 0 ? Mathf.Clamp(p.Hp + (p.MaxHp - before), 1, p.MaxHp) : p.MaxHp;
            }

            if (PlayerAttack > 0) p.AttackDamage = PlayerAttack;
        }

        /// <summary>面板按下套用：記住新值，場上有人就立刻生效。</summary>
        public static void Set(int maxHp, int attack)
        {
            PlayerMaxHp = Mathf.Max(0, maxHp);
            PlayerAttack = Mathf.Max(0, attack);

            var game = SVGame.Instance;
            if (game != null) Apply(game.Player);
        }
    }
}
