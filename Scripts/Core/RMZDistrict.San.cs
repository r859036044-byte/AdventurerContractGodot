using System;
namespace RMZ;
// SAN 與主人：奴隸有 SAN（沿用原作變數 754～758，雙子共用 757；751＝最低SAN、752＝拒絕工作人數）。
// 工作會消耗 SAN；低於 SanMin 就拒絕工作（排班自動取消、不能再排）。SAN 沒有自然恢復，
// 只能由男主人在住宅「召喚奴隸」：同寢（+25）或配種（+60，她一定受孕、孩子是主人的）。每人每天一次。
// 5 位角色（雙子算一位）都曾被主人配種 → 後宮奴隸結局（遊戲照常繼續）。
// 變數：8370+序號＝最近陪伴的日期+1、8410+角色群(1～5)＝已被主人配種、8420＝已解鎖後宮結局、8430+群＝SAN 已初始化。
public static partial class RMZDistrict
{
    const int SanBase=753, ServedDay=8370, BredFlag=8410, HaremEnding=8420, SanInit=8430;
    public const int SanMax=100, SanMin=30;
    const int LaborCost=1, HostSanCost=4, BreedReqSanCost=10, BirthSanCost=10, ServeGain=25, MasterGain=60;

    static readonly string[] HaremPages={
        "五位少女並排跪在主人面前，低下了頭。\n尤梨卡、露娜、克萊爾、雪瑤與雪霜、鈴音——\n每一個人，都曾為他孕育過孩子。",
        "她們不再提起公會，也不再提起過去。\n契約書上的名字，早已沒有人想起要撕掉。",
        "宅邸的燈每晚都亮到很晚。\n有人在哄孩子，有人在擦拭他的靴子，\n有人在窗邊，等他回來。",
        "這座後宮沒有圍牆，也沒有鎖。\n因為她們每一個人，都已經不想離開了。\n——後宮奴隸結局（故事仍在繼續）"};

    // SAN 屬於角色群：雙子共用。第一次讀取時初始化成 100（舊存檔也一樣）。
    static int San(int w){int h=Heroines[w];if(V(SanInit+h)==0){Set(SanInit+h,1);Set(SanBase+h,SanMax);}return V(SanBase+h);}
    static void AddSan(int w,int d){int h=Heroines[w];San(w);Set(SanBase+h,Math.Clamp(V(SanBase+h)+d,0,SanMax));}

    static void SyncSan()
    {
        int min=SanMax,bad=0;bool any=false;
        for(int h=1;h<=5;h++){
            bool has=false;for(int w=0;w<6;w++)if(Heroines[w]==h&&V(8201+w)>0)has=true;
            if(!has)continue;
            int w0=Array.IndexOf(Heroines,h);int s=San(w0);any=true;min=Math.Min(min,s);if(s<SanMin)bad++;
        }
        Set(751,any?min:SanMax);Set(752,bad);
    }

    // 每個班次開始前：SAN 太低的簽約角色拒絕工作，排班取消。
    static string EnforceSan()
    {
        string note="";
        for(int w=0;w<6;w++){
            if(V(8201+w)==0||San(w)>=SanMin)continue;
            if(V(8211+w)==0&&V(8221+w)==0)continue;
            Set(8211+w,0);Set(8221+w,0);
            note+="\n"+Names[w]+"的 SAN 太低（"+San(w)+"），拒絕工作了。需要主人陪伴才能恢復。";
        }
        if(note!="")SyncAssignments();
        SyncSan();return note;
    }

    // 工作過的班次消耗 SAN：同一角色群（雙子）取較大的一個，不重複扣。
    static void SpendSan(int[] jobs,bool night)
    {
        for(int h=1;h<=5;h++){
            int c=0,who=-1;
            for(int w=0;w<6;w++){
                if(Heroines[w]!=h||V(8201+w)==0)continue;
                int job=V((night?8221:8211)+w)-1;if(job<0||Array.IndexOf(jobs,job)<0)continue;
                who=w;c=Math.Max(c,job==Hosting?HostSanCost:LaborCost);
            }
            if(c>0)AddSan(who,-c);
        }
    }

    static bool AllBred(){for(int h=1;h<=5;h++)if(V(BredFlag+h)==0)return false;return true;}
    static int BredCount(){int n=0;for(int h=1;h<=5;h++)if(V(BredFlag+h)>0)n++;return n;}
    static readonly string[] GroupNames={"","尤梨卡","露娜","克萊爾","雪瑤＆雪霜","鈴音"};

    // 主人召喚奴隸。breed=false 為同寢，true 為配種（她一定受孕）。
    static string ServeMaster(int w,bool breed)
    {
        Set(GuestHas,0);
        if(w<0||w>=6||V(8201+w)==0)return "這位角色還沒有簽約。";
        if(V(ServedDay+w)==V(61)+1)return Names[w]+"今天已經陪過主人了，明天再來。";
        if(breed&&V(Preg+w)!=0)return Names[w]+"目前懷孕或在哺乳期，不能配種。\n（可以用催產用品縮短孕期。）";
        Set(ServedDay+w,V(61)+1);
        int gain=breed?MasterGain:ServeGain;AddSan(w,gain);
        string msg=Names[w]+(breed?"被主人親自配種。":"陪伴了主人一夜。")+"\nSAN +"+gain+"（現在 "+San(w)+"/"+SanMax+"）。";
        if(breed){
            Set(Preg+w,1);Set(PregDays+w,PregnancyLength);Set(Father+w,2);Set(Req+w,0);Set(Breeds+w,V(Breeds+w)+1);SyncPregnantCount();
            bool first=V(BredFlag+Heroines[w])==0;Set(BredFlag+Heroines[w],1);
            msg+="\n她懷孕了，孩子是主人的。（已被主人配種的角色 "+BredCount()+"/5）";
            if(first&&AllBred()&&V(HaremEnding)==0){Set(HaremEnding,1);LoadPages(HaremPages);msg+="\n\n【後宮奴隸結局】已解鎖！";}
        }
        SyncSan();return msg;
    }

    static string HaremReplay()
    {
        Set(GuestHas,0);
        if(V(HaremEnding)==0)return "尚未達成：需要 5 位角色都被主人配種過（目前 "+BredCount()+"/5）。";
        LoadPages(HaremPages);return "回想：後宮奴隸結局。";
    }

    static string SanList()
    {
        Set(GuestHas,0);
        string s="主人的奴隸（SAN 低於 "+SanMin+" 會拒絕工作）";
        for(int w=0;w<6;w++){
            if(V(8201+w)==0)continue;
            int sn=San(w);
            s+="\n"+Names[w]+"：SAN "+sn+"/"+SanMax+(sn<SanMin?"（拒絕工作）":"")+(V(ServedDay+w)==V(61)+1?"｜今天已陪伴":"")+(V(BredFlag+Heroines[w])>0?"｜已配種":"");
        }
        s+="\n已被主人配種的角色 "+BredCount()+"/5"+(V(HaremEnding)>0?"｜後宮奴隸結局：已解鎖":"");
        if(V(HaremEnding)==0&&BredCount()<5){string miss="";for(int h=1;h<=5;h++)if(V(BredFlag+h)==0)miss+=(miss==""?"":"、")+GroupNames[h];s+="\n尚未配種："+miss;}
        return s;
    }
}
