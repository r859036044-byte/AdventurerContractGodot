using Godot;
using System.Text.RegularExpressions;
namespace RMZ;
public partial class RMZEventCharacter
{
    void AddMapSign(string note)
    {
        if ((note??"").Contains("<RestroomExit>")) {
            // 腳底錨點扣除空白角色的位移，讓凹槽正好覆蓋最下方出口格。
            var opening=new Polygon2D { Position=new Vector2(0,RMZConfig.CharacterShiftY),
                Polygon=new[]{new Vector2(-24,-48),new Vector2(24,-48),new Vector2(24,0),new Vector2(-24,0)},
                Color=new Color("090808"),ZIndex=1 };
            AddChild(opening);
            return; // 不在出口上方掛文字，避免遮住站在出口前的角色。
        }
        var match=Regex.Match(note??"",@"<MapSign:([^>]+)>");if(!match.Success)return;
        var panel=new PanelContainer {Name="MapSign",Position=new Vector2(-30,-78),Size=new Vector2(60,25),ZIndex=2900,MouseFilter=Control.MouseFilterEnum.Ignore};
        panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new Color("49301e"),BorderColor=new Color("b9914e"),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,ContentMarginLeft=5,ContentMarginRight=5});
        var label=new Label {Text=match.Groups[1].Value,HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore};label.AddThemeFontOverride("font",RMZFonts.Main);label.AddThemeFontSizeOverride("font_size",17);label.AddThemeColorOverride("font_color",new Color("fff0bd"));panel.AddChild(label);AddChild(panel);
    }
}
