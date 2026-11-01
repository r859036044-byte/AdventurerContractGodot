using Godot;
namespace RMZ;

/// <summary>背包圖示與戰鬥手持武器共用的系列素材。</summary>
public static class RMZWeaponArt
{
    static readonly string[] Series={"wood","sand","iron","metal","star"};
    public static string Picture(RMZEquipInstance equip)
    {
        if(equip==null||!equip.IsWeapon)return null;
        int tier=equip.IsStaff?(equip.BoundId-27)/4:equip.BoundId>=22?4:equip.BoundId>=17?3:equip.BoundId>=12?2:equip.BoundId>=7?1:0;
        tier=System.Math.Clamp(tier,0,4);
        return "UI button/equipment ui/icons/"+Series[tier]+(equip.IsStaff?" staff":" 2");
    }
    public static Texture2D Texture(RMZEquipInstance equip)
    {
        string path=Picture(equip);return path==null?null:RMZImageCache.LoadPicture(path);
    }
}
