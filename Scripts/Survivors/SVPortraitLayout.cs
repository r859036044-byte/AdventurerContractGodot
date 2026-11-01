using System.Collections.Generic;
using RMZS.GodotPort;
namespace RMZS;
public static class SVPortraitLayout
{
    static readonly Dictionary<string,(Sprite body,Sprite face)> Cache = new();
    public static (Sprite body,Sprite face) Crop(string bodyPath,string facePath)
    {
        string key = bodyPath + "|" + facePath;
        if(Cache.TryGetValue(key,out var hit)) return hit;
        var body = SVTalk.Face(bodyPath); var face = SVTalk.Face(facePath);
        var reference = body ?? face; if(reference == null) return (null,null);
        var used = reference.texture.Img.GetUsedRect();
        if(used.Size.X <= 0 || used.Size.Y <= 0) return (body,face);
        // 表情沿用身體的裁切框，不能各自裁切，否則五官會移位。
        Sprite Cut(Sprite s) => s == null ? null : Sprite.Create(s.texture,
            new Rect(used.Position.X,s.texture.height-used.End.Y,used.Size.X,used.Size.Y),new Vector2(.5f,0),100);
        return Cache[key] = (Cut(body),Cut(face));
    }
    public static SVCompanionType Character(string who)
    {
        for(int i=1;i<=5;i++) foreach(var c in SVParties.PartyOf((SVHeroine)i)) if(c.Name==who)return c;
        return null;
    }
}
