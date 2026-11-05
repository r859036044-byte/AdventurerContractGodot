using System;
namespace RMZ;
// 牧場：只收「懷孕中」或「哺乳中」的簽約角色，產出鮮乳（item 2107）。
// 受孕由玩家控制：預設都有避孕魔法套餐（沿用原作設定），在「夜間接待」管理處對個別角色「停用避孕」後，
// 她排夜間接待班時每班有機率懷孕。懷孕中不能接待；生產後進入哺乳期；哺乳期結束回到一般狀態，離開牧場班。
// 全部用遊戲變數存檔：8300+序號＝階段(0一般／1懷孕中／2哺乳中)、8310+＝剩餘天數、8320+＝1表示停用避孕、8330+＝生產次數。
public static partial class RMZDistrict
{
    const int Preg=8300, PregDays=8310, NoPill=8320, Births=8330, PregnantCount=750;
    public const int PregnancyLength=6, LactationLength=10, ConceiveChance=25;   // 天、天、%
    const int MilkPregnant=1, MilkLactating=4;                                   // 每人每班鮮乳（乘牧場搾乳設施等級）

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
            Set(8221+w,0);SyncAssignments();   // 懷孕中退出夜間接待
            note+="\n"+Names[w]+"懷孕了，已退出夜間接待。可以安排到牧場搾乳。";
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
            if(ph==1){Set(Preg+w,2);Set(PregDays+w,LactationLength);Set(Births+w,V(Births+w)+1);note+="\n"+Names[w]+"生下了孩子，進入哺乳期。";}
            else{Set(Preg+w,0);Set(PregDays+w,0);if(V(8211+w)==Ranch+1){Set(8211+w,0);}note+="\n"+Names[w]+"的哺乳期結束，離開牧場搾乳班。";}
        }
        SyncAssignments();SyncPregnantCount();return note;
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

    static string PhaseText(int w)
    {
        int ph=V(Preg+w);
        string s=ph==1?"懷孕中（剩 "+V(PregDays+w)+" 天）":ph==2?"哺乳中（剩 "+V(PregDays+w)+" 天）":"一般";
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
        string s="牧場搾乳（"+(V(8141)>0?"牧場已購買":"尚未購買牧場")+"｜設施 "+Lv(Ranch)+" 級）｜鮮乳 "+RMZParty.NumItems(2107)+" 份";
        for(int w=0;w<6;w++)if(V(8201+w)>0)s+="\n"+Names[w]+"："+PhaseText(w)+(V(8211+w)==Ranch+1?"｜搾乳班":"");
        return s;
    }
}
