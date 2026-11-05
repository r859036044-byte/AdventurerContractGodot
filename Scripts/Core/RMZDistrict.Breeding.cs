using System;
namespace RMZ;
// 牧場：只收「懷孕中」或「哺乳中」的簽約角色，產出鮮乳（item 2107）。
// 受孕由玩家控制：預設都有避孕魔法套餐（沿用原作設定），在「夜間接待」管理處對個別角色「停用避孕」後，
// 她排夜間接待班時每班有機率懷孕。懷孕中不能接待；生產後進入哺乳期；哺乳期結束回到一般狀態，離開牧場班。
// 催產：催產香膏（道具，孕期 -2 天）、催產藥（藥，孕期 -4 天），用乾燥藥材＋隸章在牧場管理屋製作；
// 懷孕中繼續排夜間接待，每接待一班孕期額外 -1 天。孕期降到 0 就立刻生產。
// 全部用遊戲變數存檔：8300+序號＝階段(0一般／1懷孕中／2哺乳中)、8310+＝剩餘天數、8320+＝1表示停用避孕、8330+＝生產次數。
public static partial class RMZDistrict
{
    const int Preg=8300, PregDays=8310, NoPill=8320, Births=8330, Father=8340, PregnantCount=750;   // Father：懷孕中 0＝一般客人／不明，1＝她的神秘客人
    public const int PregnancyLength=6, LactationLength=10, ConceiveChance=25;   // 天、天、%
    public const int Balm=2108, Pill=2109;                                      // 催產香膏、催產藥（道具編號）
    const int BalmDrop=2, PillDrop=4, HostAccel=1;                              // 使用／接待一班縮短的孕期天數
    const int BalmHerb=1, BalmStamp=5, PillHerb=3, PillStamp=10;                // 製作成本：乾燥藥材(2103)＋隸章
    const int MilkPregnant=1, MilkLactating=4;
    const int LikeChance=40, LikeMult=2, GuestConceiveChance=40;                // 愛孕客人出現機率(%)與打賞倍率、神秘客人來訪受孕機率(%)                                   // 每人每班鮮乳（乘牧場搾乳設施等級）

    // 擲骰（0～n-1）。獨立出來方便測試固定結果。
    public static Func<int,int> Roll=n=>(int)(Godot.GD.Randi()%(uint)n);

    static void SyncPregnantCount(){int n=0;for(int i=0;i<6;i++)if(V(Preg+i)==1)n++;Set(PregnantCount,n);}

    // 夜班接待結算：停用避孕、沒懷孕、也不在哺乳期的接待角色，依機率受孕。回傳通知文字。
    static string TryConceive()
    {
        string note="";
        for(int w=0;w<6;w++){
            if(V(8221+w)!=Hosting+1||V(NoPill+w)==0||V(Preg+w)!=0)continue;
            if(Roll(100)>=ConceiveChance)continue;
            Set(Preg+w,1);Set(PregDays+w,PregnancyLength);
            Set(Father+w,0);
            note+="\n"+Names[w]+"懷孕了。可以安排到牧場搾乳；繼續接待會加快產期。";
        }
        SyncPregnantCount();return note;
    }

    // 一天結束：推進孕期與哺乳期。
    static string TickPregnancy()
    {
        string note="";
        for(int w=0;w<6;w++){
            int ph=V(Preg+w);if(ph==0||V(8201+w)==0)continue;   // 已離開（買斷）的不再推進
            int left=V(PregDays+w)-1;
            if(left>0){Set(PregDays+w,left);continue;}
            if(ph==1)note+=Deliver(w);
            else{Set(Preg+w,0);Set(PregDays+w,0);if(V(8211+w)==Ranch+1){Set(8211+w,0);}note+="\n"+Names[w]+"的哺乳期結束，離開牧場搾乳班。";}
        }
        SyncAssignments();SyncPregnantCount();return note;
    }

    static string Deliver(int w){Set(Father+w,0);Set(Preg+w,2);Set(PregDays+w,LactationLength);Set(Births+w,V(Births+w)+1);return "\n"+Names[w]+"生下了孩子，進入哺乳期。";}

    // 縮短孕期 n 天；降到 0 立即生產。回傳通知文字（沒生產就是空字串）。
    static string DropDays(int w,int n)
    {
        int left=V(PregDays+w)-n;
        if(left>0){Set(PregDays+w,left);return "";}
        string note=Deliver(w);SyncAssignments();SyncPregnantCount();return note;
    }

    // 夜班接待結算：懷孕中的接待角色每班孕期額外縮短。
    static string HostAccelerate()
    {
        string note="";
        for(int w=0;w<6;w++)if(V(8221+w)==Hosting+1&&V(Preg+w)==1&&V(8201+w)>0)note+=DropDays(w,HostAccel);
        return note;
    }

    static string Craft(int item)
    {
        if(!CanEnter)return EntryReason;
        bool pill=item==Pill;int herb=pill?PillHerb:BalmHerb,stamp=pill?PillStamp:BalmStamp;string name=pill?"催產藥":"催產香膏";
        if(RMZParty.NumItems(2103)<herb||V(738)<stamp)return "製作"+name+"需要 "+Goods[2]+" ×"+herb+" 和 "+stamp+" 隸章。";
        RMZParty.GainItem(2103,-herb);Set(738,V(738)-stamp);RMZParty.GainItem(item,1);
        return "製作完成："+name+" ×1（孕期 -"+(pill?PillDrop:BalmDrop)+" 天）。\n持有 "+RMZParty.NumItems(item)+" 份。";
    }

    static string UseInducer(int who,int item)
    {
        bool pill=item==Pill;string name=pill?"催產藥":"催產香膏";int drop=pill?PillDrop:BalmDrop;
        if(who<0||who>=6||V(8201+who)==0)return "這位角色還沒有簽約。";
        if(RMZParty.NumItems(item)<1)return "沒有"+name+"。可以在牧場管理屋的「催產用品」製作。";
        if(V(Preg+who)!=1)return Names[who]+"目前沒有懷孕，不需要使用"+name+"。";
        RMZParty.GainItem(item,-1);
        int before=V(PregDays+who);string born=DropDays(who,drop);
        return Names[who]+"使用了"+name+"。"+(born==""?"\n孕期 "+before+" 天 → "+V(PregDays+who)+" 天。":born.TrimStart('\n'));
    }

    // 牧場搾乳班本班產量。需要已購買牧場。
    static int RanchMilk()
    {
        if(V(8141)==0)return 0;
        int n=0;
        for(int w=0;w<6;w++){
            if(V(8201+w)==0||V(8211+w)!=Ranch+1)continue;
            int ph=V(Preg+w);n+=ph==1?MilkPregnant:ph==2?MilkLactating:0;
        }
        return n*Lv(Ranch);
    }

    // 神秘客人的特殊接見：懷孕中且孩子不是他的 → 不能接見
    static bool GuestBlocked(int w)=>V(Preg+w)==1&&V(Father+w)==0;

    // 夜間接待本班收入。客人喜好不同：有人特別喜歡孕期的角色，出現時該角色收入 ×LikeMult。
    static (int gold,int stamp,string note) HostIncome()
    {
        int gold=0,stamp=0,fans=0;int lv=Lv(Hosting);
        for(int w=0;w<6;w++){
            if(V(8201+w)==0||V(8221+w)!=Hosting+1)continue;
            int g=HostGold*lv;
            if(V(Preg+w)==1&&Roll(100)<LikeChance){g*=LikeMult;fans++;}
            gold+=g;stamp+=HostStamp*lv;
        }
        return (gold,stamp,fans>0?"\n有客人特別喜歡孕期的角色，額外打賞（"+fans+" 位）。":"");
    }

    // 神秘客人來訪時，停用避孕的角色有機會受孕，孩子算是他的。
    static string GuestConceive(int w)
    {
        if(V(NoPill+w)==0||V(Preg+w)!=0||Roll(100)>=GuestConceiveChance)return "";
        Set(Preg+w,1);Set(PregDays+w,PregnancyLength);Set(Father+w,1);SyncPregnantCount();
        return "\n"+Names[w]+"懷孕了，孩子是這位客人的。他之後可以繼續接見。";
    }

    static string PhaseText(int w)
    {
        int ph=V(Preg+w);
        string s=ph==1?"懷孕中（剩 "+V(PregDays+w)+" 天）":ph==2?"哺乳中（剩 "+V(PregDays+w)+" 天）":"一般";
        if(ph==1)s+=V(Father+w)==1?"｜孩子是神秘客人的":"｜（不接見特殊客人）";
        return s+(V(NoPill+w)>0?"｜已停用避孕":"｜避孕中");
    }

    static string ToggleContraception(int who)
    {
        if(who<0||who>=6||V(8201+who)==0)return "這位角色還沒有簽約。";
        Set(NoPill+who,V(NoPill+who)>0?0:1);
        return Names[who]+(V(NoPill+who)>0?"已停用避孕套餐：排夜間接待時，每班有 "+ConceiveChance+"% 機率懷孕。":"已恢復避孕套餐，不會懷孕。")+"\n目前狀態："+PhaseText(who);
    }

    static string BreedStatus()
    {
        string s="催產香膏 "+RMZParty.NumItems(Balm)+"｜催產藥 "+RMZParty.NumItems(Pill)+"\n牧場搾乳（"+(V(8141)>0?"牧場已購買":"尚未購買牧場")+"｜設施 "+Lv(Ranch)+" 級）｜鮮乳 "+RMZParty.NumItems(2107)+" 份";
        for(int w=0;w<6;w++)if(V(8201+w)>0)s+="\n"+Names[w]+"："+PhaseText(w)+(V(8211+w)==Ranch+1?"｜搾乳班":"");
        return s;
    }
}
