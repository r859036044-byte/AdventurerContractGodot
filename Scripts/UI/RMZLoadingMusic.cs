using Godot;
using System;
namespace RMZ;

// 原創四小節鐘琴／柔和撥弦循環；不依賴外部音樂或素材授權。
public static class RMZLoadingMusic
{
    static AudioStreamWav stream;
    public static AudioStreamWav Stream => stream ??= Compose();
    static AudioStreamWav Compose()
    {
        const int rate=24000;
        const double beat=.5;
        int count=rate*8;
        var samples=new double[count];
        int[][] chords={new[]{60,64,67,72},new[]{57,60,64,69},new[]{53,57,60,65},new[]{55,59,62,67}};
        void Note(int midi,double start,double length,double gain)
        {
            double frequency=440*Math.Pow(2,(midi-69)/12.0);
            int from=(int)(start*rate),n=(int)(length*rate);
            for(int i=0;i<n;i++)
            {
                double t=i/(double)rate;
                double envelope=Math.Min(1,t/.012)*Math.Exp(-t*3.4)*Math.Min(1,(length-t)/.08);
                double phase=2*Math.PI*frequency*t;
                double tone=Math.Sin(phase)+.18*Math.Sin(phase*2)+.055*Math.Sin(phase*3);
                // 尾音跨越循環邊界，首尾連接不切斷音符。
                samples[(from+i)%count]+=tone*envelope*gain;
            }
        }
        for(int bar=0;bar<4;bar++)
        {
            var chord=chords[bar];
            Note(chord[0]-12,bar*2,1.9,.15);
            for(int step=0;step<4;step++)Note(chord[new[]{0,2,1,3}[step]]+12,bar*2+step*beat,1.25,.19);
        }
        var bytes=new byte[count*2];
        for(int i=0;i<count;i++)
        {
            short value=(short)(Math.Clamp(samples[i],-.95,.95)*32767);
            bytes[i*2]=(byte)(value&255);bytes[i*2+1]=(byte)((value>>8)&255);
        }
        return new AudioStreamWav { Format=AudioStreamWav.FormatEnum.Format16Bits,MixRate=rate,Stereo=false,Data=bytes,LoopMode=AudioStreamWav.LoopModeEnum.Forward,LoopBegin=0,LoopEnd=count };
    }
}
