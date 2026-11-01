using System;
using System.Collections.Generic;
using RMZS.GodotPort;
namespace RMZS;
// 外部素材來源及 CC0 聲明見專案根目錄「素材來源與版權.txt」。
public static class SVVisualAssets {
static readonly Dictionary<string,Sprite> icons=new();
public static Sprite Icon(string key){if(string.IsNullOrEmpty(key))return null;if(icons.TryGetValue(key,out var old))return old;var atlas=Resources.Load<Sprite>("External/Skills/64x64_FantasySkillsIcons_NoBackground");if(atlas==null)return null;
int row=9,col=4;string k=key.ToLowerInvariant();
if(k.Contains("whirlwind")||k.Contains("blade")||k.Contains("sw_might")||k=="power_4") {row=0;col=1;}
if(k.Contains("shockwave")||k.Contains("warlord")||k=="power_3") {row=19;col=1;}
if(k.Contains("barrier")||k.Contains("shield")||k.Contains("ward")||k.Contains("armor")||k.Contains("bulwark")||k.Contains("frost_")){row=0;col=7;}
if(k.Contains("chain")||k.Contains("thunder")){row=4;col=7;}
if(k.Contains("star")||k.Contains("meteor")||k.Contains("rocket")||k.Contains("burn")||k.Contains("detonate")){row=3;col=7;}
if(k.Contains("poison")||k.Contains("dot")){row=5;col=1;}
if(k.Contains("dark")||k.Contains("blackhole")){row=5;col=4;}
if(k.Contains("blood")||k.Contains("bleed")||k.Contains("vampire")||k.Contains("berserk")){row=20;col=7;}
if(k.Contains("collector")){row=20;col=4;}
if(k.Contains("timestop")||k.EndsWith("_cd")||k.Contains("duration")||k.Contains("doublecast")){row=8;col=7;}
if(k.Contains("heal")||k.Contains("luna")||k.Contains("adrenaline")){row=17;col=7;}
if(k.Contains("bond")||k.Contains("team")||k.Contains("blessing")){row=8;col=4;}
if(k=="hp"||k.Contains("vitality")||k=="card_hp"||k=="power_1"){row=1;col=4;}
if(k=="speed"||k=="power_6"||k.Contains("overdrive")){row=10;col=7;}
if(k=="power_2"||k.Contains("yurika")){row=0;col=4;}
if(k=="power_5"){row=7;col=7;}
if(k.Contains("magnet")){row=6;col=7;}
if(k.Contains("claire")){row=4;col=4;}
if(k.Contains("twin")||k.Contains("yao")){row=3;col=1;}
if(k.Contains("suzune")||k=="mg_focus"){row=11;col=4;}
string passive=k.Replace("blessing_", "");
var cell=passive switch {
"kill_hp"=>(1,4),"kill_atk"=>(0,1),"kill_shield"=>(0,7),"kill_all"=>(9,1),"double"=>(0,4),"crit"=>(17,1),"crit_heavy"=>(19,1),"frenzy"=>(17,4),"heavy"=>(1,4),"spread"=>(1,1),"pierce"=>(0,1),"eagle"=>(7,7),"lone"=>(12,1),"thorns"=>(16,7),"iron"=>(0,7),"regen"=>(17,7),"phoenix"=>(13,7),"swift"=>(10,7),"scholar"=>(6,4),"greed"=>(6,7),"hunter"=>(6,1),"frost"=>(4,4),"boom"=>(3,7),"head_start"=>(8,1),"focus"=>(2,7),"command"=>(9,1),"rally"=>(8,4),"guardian"=>(11,7),"glass_cannon"=>(21,7),"stalwart"=>(11,7),"close_quarters"=>(17,1),"ascetic"=>(2,7),"double_cast"=>(7,4),_ =>(-1,-1)};
if(cell.Item1>=0){row=cell.Item1;col=cell.Item2;}
if(k.StartsWith("awaken_"))col=col/3*3+2;
var t=atlas.texture;var icon=Sprite.Create(t,new Rect(col*64,t.height-(row+1)*64,64,64),new Vector2(.5f,.5f),64);icons[key]=icon;return icon;}
static readonly Dictionary<string,int> monsters=new(){["史萊姆"]=32,["粉史萊姆"]=32,["哥布林"]=33,["野獸"]=18,["遺跡守衛"]=1,["岩甲巨獸"]=41,["洞窟史萊姆"]=52,["獸人"]=43,["森林狼"]=18,["毒蛛"]=53,["石魔"]=36,["獸王 加爾姆"]=48,["惡魔"]=54,["亡靈"]=22,["拉米亞"]=26,["魔導兵"]=8,["影魔"]=25,["拉米亞女王"]=26,["深淵獸"]=44,["高階惡魔"]=54,["亡靈騎士"]=33,["魔將"]=31,["虛空影"]=47,["深淵魔王"]=50};
// 用可見圖案的最長邊衡量尺寸，避免素材解析度與透明留白影響體型。
// 鈴音基礎法陣半徑 4.5（直徑 9）；王保留少量餘裕供呼吸動畫縮放。
public static float MonsterVisualSpan(SVEnemyType type) => type.IsBoss
    ? Math.Max(9.5f, type.Scale * 4.3f)
    : Math.Clamp(1.05f + (type.Scale - 1f) * .12f, 1.05f, 1.3f);
static readonly Dictionary<(string key,float span),Sprite> sizedMonsters=new();
public static Sprite Monster(SVEnemyType type,bool back)
{
    if(type==null||!monsters.TryGetValue(type.Name,out var id))return null;
    bool alt=type.Name is "粉史萊姆" or "拉米亞女王" or "高階惡魔" or "亡靈騎士";
    string palette=alt?"Alternative":"Normal";
    string key=$"External/Monsters/50+ Monsters Pack 2D/Monsters/{palette} Colors/Monster #{id} {(back?"Back":"Front")} {palette} Color Palette";
    float localSpan=MonsterVisualSpan(type)/Math.Max(.01f,type.Scale);
    if(sizedMonsters.TryGetValue((key,localSpan),out var cached))return cached;
    var source=Resources.Load<Sprite>(key);
    if(source==null)return null;
    var tex=source.texture;
    var visible=tex.Img.GetUsedRect();
    if(visible.Size.X<=0||visible.Size.Y<=0)return source;
    float pixelsPerUnit=Math.Max(visible.Size.X,visible.Size.Y)/localSpan;
    var sprite=Sprite.Create(tex,new Rect(visible.Position.X,tex.height-visible.End.Y,visible.Size.X,visible.Size.Y),
        new Vector2(.5f,.08f),pixelsPerUnit);
    sizedMonsters[(key,localSpan)]=sprite;
    return sprite;
}

static readonly Dictionary<string,Sprite[]> spells=new();
public static Sprite Spell(string name,float time=0){if(!spells.TryGetValue(name,out var frames)){var atlas=Resources.Load<Sprite>("External/Spells/Pixelart Spells/PNG Files/"+name);if(atlas==null)return null;var t=atlas.texture;int size=t.height;frames=new Sprite[t.width/size];for(int i=0;i<frames.Length;i++)frames[i]=Sprite.Create(t,new Rect(i*size,0,size,size),new Vector2(.5f,.5f),size);spells[name]=frames;}return frames[(int)(time*12)%frames.Length];}
}