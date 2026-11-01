using Godot;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace RMZ;

/// <summary>使用原始 IconSet，以實際圖示寬度排版 Hendrix 的行內圖示。</summary>
public partial class RMZIconLabel : Label
{
    static Texture2D _icons;
    static readonly Regex Token = new("\uE000(\\d+)\uE001");
    string _content;
    public float IconSize = 32;
    public float ColumnSplit = -1;
    public bool CenterColumns;
    public void SetIconText(string value)
    {
        _content = value;
        Text = Token.IsMatch(value) || ColumnSplit >= 0 ? "" : value;
        QueueRedraw();
    }
    public override void _Draw()
    {
        if (string.IsNullOrEmpty(_content) || (!Token.IsMatch(_content) && ColumnSplit < 0)) return;
        if (_icons == null)
        {
            string file = Path.Combine(RMZConfig.RootPath,"img","system","IconSet.png");
            if (File.Exists(file)) _icons = ImageTexture.CreateFromImage(Image.LoadFromFile(file));
        }
        var font = GetThemeFont("font");
        int size = GetThemeFontSize("font_size");
        float iconSize = IconSize;
        float height = Math.Max(iconSize, font.GetHeight(size));
        var lines = _content.Split('\n');
        float y = VerticalAlignment == Godot.VerticalAlignment.Center ? (Size.Y-height*lines.Length)/2 :
            VerticalAlignment == Godot.VerticalAlignment.Bottom ? Size.Y-height*lines.Length : 0;
        foreach (var rawLine in lines)
        {
          var parts = ColumnSplit >= 0 ? rawLine.Split('\t', 2) : new[]{rawLine};
          for(int part=0;part<parts.Length;part++) {
            var line=parts[part];
            size=GetThemeFontSize("font_size");
            float areaX=part==0?0:ColumnSplit;
            float areaWidth=ColumnSplit<0?Size.X:part==0?ColumnSplit:Size.X-ColumnSplit;
            if(ColumnSplit>=0 && !Token.IsMatch(line))
                while(size>12 && font.GetStringSize(line,fontSize:size).X>areaWidth-4)size--;
            float width=0;int previous=0;
            foreach(Match token in Token.Matches(line))
            { width += font.GetStringSize(line.Substring(previous,token.Index-previous),fontSize:size).X+iconSize+4;previous=token.Index+token.Length; }
            width += font.GetStringSize(line.Substring(previous),fontSize:size).X;
            float x=HorizontalAlignment==Godot.HorizontalAlignment.Center?(Size.X-width)/2:
                HorizontalAlignment==Godot.HorizontalAlignment.Right?Size.X-width:0;
            if(ColumnSplit>=0)x=areaX+(CenterColumns?(areaWidth-width)/2:part==0?0:areaWidth-width);
            float baseline=y+(height-font.GetHeight(size))/2+font.GetAscent(size);
            void Write(string text)
            {
                var at=new Vector2(x,baseline);
                DrawStringOutline(font,at,text,fontSize:size,size:GetThemeConstant("outline_size"),modulate:GetThemeColor("font_outline_color"));
                DrawString(font,at,text,fontSize:size,modulate:GetThemeColor("font_color"));
                x+=font.GetStringSize(text,fontSize:size).X;
            }
            previous=0;
            foreach(Match token in Token.Matches(line))
            {
                Write(line.Substring(previous,token.Index-previous));
                int id=int.Parse(token.Groups[1].Value);
                if(_icons!=null)
                {
                    int columns=_icons.GetWidth()/32;
                    if(columns>0 && id<columns*(_icons.GetHeight()/32))
                        DrawTextureRectRegion(_icons,new Rect2(x,y+(height-iconSize)/2,iconSize,iconSize),new Rect2(id%columns*32,id/columns*32,32,32));
                }
                x+=iconSize+4;previous=token.Index+token.Length;
            }
            Write(line.Substring(previous));
          }
          y+=height;
        }
    }
}
