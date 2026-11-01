using System;
using RMZS.GodotPort;

namespace RMZS
{
    // 原創像素符號：所有技能介面共用。32px 原圖，保留清楚的劍／盾／法杖輪廓。
    public static class SVSimpleIcons
    {
        public static Sprite Create(string id)
        {
            string key = id.ToLowerInvariant();
            bool blessing = key.StartsWith("blessing_");
            bool awaken = key.StartsWith("awaken_");
            string k = key.Replace("blessing_", "").Replace("awaken_", "").Replace("pskill_", "");
            var tex = new Texture2D(32, 32) { filterMode = FilterMode.Point };
            Godot.Color C(string hex) => new Godot.Color(hex);
            void Box(int x, int y, int w, int h, string color)
            {
                for (int yy=y; yy<y+h; yy++) for (int xx=x; xx<x+w; xx++)
                    if (xx>=0 && xx<32 && yy>=0 && yy<32) tex.Img.SetPixel(xx, yy, C(color));
            }
            void Line(int x, int y, int x2, int y2, string color, int width=1)
            {
                int dx=Math.Abs(x2-x), dy=-Math.Abs(y2-y), sx=x<x2?1:-1, sy=y<y2?1:-1, error=dx+dy;
                while(true) { Box(x,y,width,width,color); if(x==x2&&y==y2)break; int e=2*error; if(e>=dy){error+=dy;x+=sx;}if(e<=dx){error+=dx;y+=sy;} }
            }
            void Ring(int inset, string color)
            { Box(inset,inset,32-inset*2,1,color);Box(inset,31-inset,32-inset*2,1,color);Box(inset,inset,1,32-inset*2,color);Box(31-inset,inset,1,32-inset*2,color); }
            string edge=awaken?"#F3CD70":blessing?"#83D8BE":"#AA875B";
            Box(1,1,30,30,"#161620");
            Box(3,3,26,26,awaken?"#38234F":blessing?"#173C38":"#302C29");
            Ring(1,edge);
            if(awaken) { Ring(3,"#AA7B45"); foreach(int x in new[]{1,27})foreach(int y in new[]{1,27})Box(x,y,4,4,edge); }
            else if(blessing) { Box(13,1,6,3,"#E6C776");Box(13,28,6,3,"#E6C776");Box(1,13,3,6,edge);Box(28,13,3,6,edge); }
            string glyph = "sword";
            bool Has(params string[] parts) { foreach(var p in parts)if(k.Contains(p))return true;return false; }
            if(Has("shield","armor","ward","bulwark","barrier","guardian","iron","thorns","frost_cd"))glyph="shield";
            else if(Has("heal","hp","vitality","regen","vampire","blood_thirst","adrenaline","bond","phoenix","luna"))glyph="heart";
            else if(Has("speed","swift","power_6","dodge"))glyph="boot";
            else if(Has("timestop","_cd","focus","rally","ascetic"))glyph="clock";
            else if(Has("greed","hunter","magnet","scholar","head_start","blessing"))glyph="star";
            else if(Has("judgment","absolutezero","frostnova","mg_","suzune","claire","chain","starfall","staff","poison","darkmatter","thunderstorm","blackhole","double_cast","burn","flame","frost","boom","detonate","card_skill","card_dot"))glyph="staff";
            string gold="#D5A75E", light="#F4E5BC", steel="#D7E6EB", shade="#7D9FAC";
            if(glyph=="sword")
            { Line(10,23,22,11,"#131C24",5);Line(12,20,23,9,shade,3);Line(12,20,23,9,steel);Box(23,7,2,3,steel);Line(8,18,15,25,gold,2);Line(8,25,11,22,gold,2);Box(7,25,3,3,light); }
            else if(glyph=="shield")
            { Box(8,8,16,12,gold);Box(10,20,12,3,gold);Box(12,23,8,2,gold);Box(14,25,4,2,gold);Box(10,10,12,9,"#548EA3");Box(12,19,8,3,"#548EA3");Box(14,22,4,2,"#548EA3");Box(15,10,2,14,steel);Box(10,14,12,2,steel); }
            else if(glyph=="staff")
            { Line(9,25,20,14,"#77502F",3);Line(9,25,20,14,gold);Box(18,7,7,9,gold);Box(16,9,11,5,gold);Box(19,8,5,7,"#65BFD6");Box(17,10,9,3,"#65BFD6");Box(20,8,2,4,"#E2FFFF");Box(25,19,2,2,light); }
            else if(glyph=="heart")
            { Box(7,10,8,8,"#EC6B78");Box(17,10,8,8,"#EC6B78");Box(9,8,4,2,"#EC6B78");Box(19,8,4,2,"#EC6B78");Box(9,16,14,4,"#EC6B78");Box(11,20,10,2,"#D34762");Box(13,22,6,2,"#D34762");Box(15,24,2,2,"#D34762");Box(9,11,4,2,"#FFD9D8"); }
            else if(glyph=="boot")
            { Box(13,7,9,13,gold);Box(12,18,8,6,gold);Box(8,21,12,4,gold);Box(7,25,15,2,light);Box(14,8,7,3,light);Box(8,12,3,2,shade);Box(6,16,4,2,shade); }
            else if(glyph=="clock")
            { Box(9,8,14,17,gold);Box(7,10,18,13,gold);Box(10,10,12,13,"#315664");Box(9,12,14,9,"#315664");Box(15,11,2,7,light);Box(15,16,6,2,light);Box(14,5,4,2,gold); }
            else
            { Box(14,7,4,18,gold);Box(7,14,18,4,gold);Box(10,11,12,10,gold);Box(12,9,8,14,gold);Box(14,11,2,7,light); }
            tex.Apply();
            return Sprite.Create(tex,new Rect(0,0,32,32),new Vector2(.5f,.5f),32);
        }
    }
}
