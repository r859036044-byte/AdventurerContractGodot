// 程式內建的像素圖。不讀任何圖檔 —— 用字元畫格子，一個字元一個像素，
// 點取樣（Point）顯示，放大也是清楚的方格，不會糊。
//
// 目前用在：地上的補給品圖示、男主的普攻劍氣。
// 要改圖就改下面的字串；'.' 是透明，其他字元查各自的調色盤。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public static class SVPixelArt
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>把字元圖轉成 Sprite。pixelsPerUnit = 圖寬，所以整張圖在世界裡是 1 單位寬。</summary>
        public static Sprite FromRows(string key, string[] rows, Dictionary<char, Color> palette)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;

            int h = rows.Length, w = rows[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                string row = rows[h - 1 - y];   // 貼圖原點在左下，字串第一行是最上面
                for (int x = 0; x < w; x++)
                {
                    char c = x < row.Length ? row[x] : '.';
                    px[y * w + x] = palette.TryGetValue(c, out var col) ? col : new Color(0f, 0f, 0f, 0f);
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w, 0, SpriteMeshType.FullRect);
            Cache[key] = sprite;
            return sprite;
        }

        static Color C(string hex, float a = 1f)
        {
            int v = System.Convert.ToInt32(hex, 16);
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, a);
        }

        static readonly Color Outline = C("2a1a14");

        // ---------------- 補給品 ----------------

        public static Sprite PickupIcon(SVPickupKind kind) => kind switch
        {
            SVPickupKind.Heal => Potion("potion_heal", C("3fc86a"), C("1f8a45"), C("b8ffcf")),
            SVPickupKind.AttackUp => Potion("potion_power", C("e8483c"), C("9c2020"), C("ffc0b0")),
            SVPickupKind.Shield => Shield(),
            SVPickupKind.Invincible => Star(),
            SVPickupKind.Magnet => Magnet(),
            SVPickupKind.Card => Card(),
            _ => null,
        };

        static Sprite Potion(string key, Color liquid, Color dark, Color shine) => FromRows(key, new[]
        {
            "................",
            "......kkkk......",
            "......kcck......",
            "......kcck......",
            ".....kkkkkk.....",
            "......kwwk......",
            ".....kwLLwk.....",
            "....kLLLLLLk....",
            "...kLhLLLLLLk...",
            "...kLhLLLLLLk...",
            "...kLLLLLLLLk...",
            "...kLLLLLLLLk...",
            "...kDLLLLLLDk...",
            "....kDDDDDDk....",
            ".....kkkkkk.....",
            "................",
        }, new Dictionary<char, Color>
        {
            ['k'] = Outline, ['c'] = C("b07a45"), ['w'] = C("e8f4ff", 0.85f),
            ['L'] = liquid, ['D'] = dark, ['h'] = shine,
        });

        static Sprite Shield() => FromRows("shield", new[]
        {
            "................",
            "..kkkkkkkkkkkk..",
            "..kBBBBBBBBBBk..",
            "..kBWWBBBBBBBk..",
            "..kBWBBBYBBBBk..",
            "..kBBBBYYYBBBk..",
            "..kBBBBBYBBBBk..",
            "..kBBBBBYBBBBk..",
            "...kBBBBBBBBk...",
            "...kBBBBBBBDk...",
            "....kBBBBBBk....",
            "....kDBBBBDk....",
            ".....kDBBDk.....",
            "......kDDk......",
            ".......kk.......",
            "................",
        }, new Dictionary<char, Color>
        {
            ['k'] = Outline, ['B'] = C("4d9be6"), ['D'] = C("2c5fa8"),
            ['W'] = C("cfe8ff"), ['Y'] = C("ffd35a"),
        });

        static Sprite Star() => FromRows("star", new[]
        {
            "................",
            ".......kk.......",
            "......kYYk......",
            "......kYYk......",
            ".kkkkkYYYYkkkkk.",
            ".kYYYYYWYYYYYYk.",
            "..kYYYYWYYYYYk..",
            "...kYYYYYYYYk...",
            "....kYYYYYYk....",
            "....kYYYYYYk....",
            "...kYYYkkYYYk...",
            "...kYYk..kYYk...",
            "..kYYk....kYYk..",
            "..kkk......kkk..",
            "................",
            "................",
        }, new Dictionary<char, Color>
        {
            ['k'] = C("7a4a10"), ['Y'] = C("ffd84a"), ['W'] = C("fff6c8"),
        });

        static Sprite Magnet() => FromRows("magnet", new[]
        {
            "................",
            "................",
            "...kkkk..kkkk...",
            "...kSSk..kSSk...",
            "...kSSk..kSSk...",
            "...kRRk..kRRk...",
            "...kRRk..kRRk...",
            "...kRRk..kRRk...",
            "...kRRk..kRRk...",
            "...kRRkkkkRRk...",
            "...kRhRRRRRRk...",
            "....kRRRRRRk....",
            ".....kkkkkk.....",
            "................",
            "................",
            "................",
        }, new Dictionary<char, Color>
        {
            ['k'] = Outline, ['S'] = C("d8dde6"), ['R'] = C("d8433a"), ['h'] = C("ff9a8a"),
        });

        static Sprite Card() => FromRows("card", new[]
        {
            "................",
            "...kkkkkkkkkk...",
            "...kPPPPPPPPk...",
            "...kPggggggPk...",
            "...kPgPPPPgPk...",
            "...kPgPVVPgPk...",
            "...kPgPVVPgPk...",
            "...kPgVVVVgPk...",
            "...kPgPVVPgPk...",
            "...kPgPPPPgPk...",
            "...kPggggggPk...",
            "...kPPPPPPPPk...",
            "...kkkkkkkkkk...",
            "................",
            "................",
            "................",
        }, new Dictionary<char, Color>
        {
            ['k'] = Outline, ['P'] = C("f1e3c2"), ['g'] = C("c9a24a"), ['V'] = C("9b5de5"),
        });

        // ---------------- HUD 左下角能力值圖示（8×8，放大 2 倍成 16×16） ----------------
        // HUD 的 Image 不是點取樣，所以直接在貼圖裡放大，畫面上 1:1 顯示才不會糊。

        static string[] Up2(string[] rows)
        {
            var o = new string[rows.Length * 2];
            for (int y = 0; y < rows.Length; y++)
            {
                var sb = new System.Text.StringBuilder();
                foreach (char c in rows[y]) { sb.Append(c); sb.Append(c); }
                o[y * 2] = o[y * 2 + 1] = sb.ToString();
            }
            return o;
        }

        static readonly string[] SwordRows = { "......W.", ".....WW.", "....WW..", "...WW...", "G.WW....", ".GW.....", ".HG.....", "H......." };
        static readonly string[] ShieldRows = { "SSSSSSSS", "SLLSSSSS", "SLSSSSSS", "SSSSSSSD", ".SSSSSD.", ".SSSSSD.", "..SSSD..", "...SD..." };

        public static Sprite StatIcon(string key)
        {
            switch (key)
            {
                case "atk": return FromRows("st_atk", Up2(SwordRows), new Dictionary<char, Color> { ['W'] = C("e6ebf2"), ['G'] = C("e0b040"), ['H'] = C("8a5a30") });
                case "phys": return FromRows("st_phys", Up2(SwordRows), new Dictionary<char, Color> { ['W'] = C("ff8a50"), ['G'] = C("e0b040"), ['H'] = C("8a5a30") });
                case "aspd": return FromRows("st_aspd", Up2(new[] { "....YY..", "...YY...", "..YY....", ".YYYYY..", "....YY..", "...YY...", "..YY....", ".Y......" }), new Dictionary<char, Color> { ['Y'] = C("ffd84a") });
                // 魔攻：藍色法杖（杖頭發光的寶珠＋藍色杖身）
                case "magic": return FromRows("st_magic2", Up2(new[] { "....LBL.", "....BWB.", "....LBL.", "...H....", "..H.....", ".H......", "H.......", "........" }), new Dictionary<char, Color> { ['B'] = C("3d8bff"), ['L'] = C("9cc8ff"), ['W'] = C("e8f4ff"), ['H'] = C("5a7fd0") });
                case "def": return FromRows("st_def", Up2(ShieldRows), new Dictionary<char, Color> { ['S'] = C("c9a36a"), ['L'] = C("f2dfb8"), ['D'] = C("7a5a30") });
                case "mdef": return FromRows("st_mdef", Up2(ShieldRows), new Dictionary<char, Color> { ['S'] = C("4d9be6"), ['L'] = C("cfe8ff"), ['D'] = C("2c5fa8") });
                case "crit": return FromRows("st_crit", Up2(new[] { "...R....", "...R....", ".R.R.R..", "..RRR...", "RRRWRRR.", "..RRR...", ".R.R.R..", "...R...." }), new Dictionary<char, Color> { ['R'] = C("ff5a4a"), ['W'] = C("fff0d0") });
                case "critdmg": return FromRows("st_critdmg", Up2(new[] { "...R....", "...R....", "..RRR...", "..RRR...", ".RRWRR..", ".RRRRR..", "..RRR...", "........" }), new Dictionary<char, Color> { ['R'] = C("d8433a"), ['W'] = C("ffb0a0") });
                case "atkbonus": return FromRows("st_atkbonus", Up2(new[] { "......W.", ".....WG.", "....WG..", "GG.WG...", ".GWG....", "..G..PP.", ".G.G.PP.", "G......." }), new Dictionary<char, Color> { ['W'] = C("e6ebf2"), ['G'] = C("e0b040"), ['P'] = C("7cf07c") });
                case "dmgbonus": return FromRows("st_dmgbonus", Up2(new[] { "...O....", ".O.O.O..", "..OYO...", "OOYWYOO.", "..OYO...", ".O.O.O..", "...O....", "........" }), new Dictionary<char, Color> { ['O'] = C("ff7a2a"), ['Y'] = C("ffd84a"), ['W'] = C("fff6d0") });
                case "bossdmg": return FromRows("st_bossdmg", Up2(new[] { "P..P..P.", "PP.P.PP.", "PPPPPPP.", ".PYPYP..", ".PPPPP..", "..RRR...", ".R.R.R..", "........" }), new Dictionary<char, Color> { ['P'] = C("b060ff"), ['Y'] = C("ffd84a"), ['R'] = C("ff5a4a") });
                case "skilldmg": return FromRows("st_skilldmg", Up2(new[] { "...B....", "...B....", "..BLB...", "BBLWLBB.", "..BLB...", "...B..B.", "...B.BLB", "......B." }), new Dictionary<char, Color> { ['B'] = C("3d8bff"), ['L'] = C("9cc8ff"), ['W'] = C("ffffff") });
                case "lifesteal": return FromRows("st_lifesteal", Up2(new[] { "...R....", "..RR....", ".RRRR...", ".RRRR...", "RRRWRR..", "RRRRRR..", ".RRRR...", "........" }), new Dictionary<char, Color> { ['R'] = C("d8433a"), ['W'] = C("ffb0a0") });
                case "interval": return FromRows("st_interval", Up2(new[] { "YYYYYY..", ".YWWY...", "..YY....", "...Y....", "..YY....", ".Y..Y...", "YYYYYY..", "........" }), new Dictionary<char, Color> { ['Y'] = C("e0b040"), ['W'] = C("fff0c0") });
                case "omnisteal": return FromRows("st_omnisteal", Up2(new[] { "...R..Y.", "..RR.YWY", ".RRRR.Y.", ".RRRR...", "RRPWRR..", "RPPPPR..", ".RPPR...", "........" }), new Dictionary<char, Color> { ['R'] = C("d8433a"), ['P'] = C("9a3ad8"), ['W'] = C("ffb0a0"), ['Y'] = C("ffd84a") });
                case "heal": return FromRows("st_heal", Up2(new[] { "..GGG...", "..GWG...", "GGGWGGG.", "GWWWWWG.", "GGGWGGG.", "..GWG...", "..GGG...", "........" }), new Dictionary<char, Color> { ['G'] = C("3fbf5a"), ['W'] = C("d8ffd8") });
                case "cdr": return FromRows("st_cdr", Up2(new[] { "..BBB...", ".B...B..", "B..W..B.", "B..WW.B.", "B.....B.", ".B...B..", "..BBB...", "........" }), new Dictionary<char, Color> { ['B'] = C("6cb8ff"), ['W'] = C("ffffff") });
                case "speed": return FromRows("st_speed", Up2(new[] { "..BBB...", "..BBB...", "..BBB...", "..BBB...", "..BBBB..", "..BBBBBB", "..BBBBBB", "........" }), new Dictionary<char, Color> { ['B'] = C("5fd08a") });
                case "range": return FromRows("st_range", Up2(new[] { "...T....", "..TTT...", ".T.T.T..", "TTT.TTT.", ".T.T.T..", "..TTT...", "...T....", "........" }), new Dictionary<char, Color> { ['T'] = C("4fd6d0") });
                case "shots": return FromRows("st_shots", Up2(new[] { "........", "A..A..A.", "AA.AA.AA", "AAAAAAAA", "AA.AA.AA", "A..A..A.", "........", "........" }), new Dictionary<char, Color> { ['A'] = C("f1e3c2") });
                case "pierce": return FromRows("st_pierce", Up2(new[] { "...G....", "...G....", "...G..A.", "AAAGAAAA", "...G..A.", "...G....", "...G....", "........" }), new Dictionary<char, Color> { ['A'] = C("f1e3c2"), ['G'] = C("8a7a66") });
            }
            return null;
        }

        // ---------------- 男主普攻：劍氣 ----------------
        // 朝右的一道月牙形刀光。遊戲裡會依飛行方向旋轉。
        public static Sprite Slash(bool staff) => staff
            ? FromRows("slash_staff", SlashRows, new Dictionary<char, Color>
              {
                  ['o'] = C("5a3fb0", 0.9f), ['m'] = C("9b7bff"), ['i'] = C("d9ccff"), ['w'] = C("ffffff"),
              })
            : FromRows("slash_sword", SlashRows, new Dictionary<char, Color>
              {
                  ['o'] = C("2f6fa8", 0.9f), ['m'] = C("7fd0ff"), ['i'] = C("c8f0ff"), ['w'] = C("ffffff"),
              });

        static readonly string[] SlashRows =
        {
            "................",
            "........oo......",
            ".........omo....",
            "..........omo...",
            "..........omio..",
            "...........mio..",
            "...........miwo.",
            "...........miwo.",
            "...........miwo.",
            "...........miwo.",
            "...........mio..",
            "..........omio..",
            "..........omo...",
            ".........omo....",
            "........oo......",
            "................",
        };
    }
}
