using System;
namespace RMZ;
// 所有持久資料放入既有變數／物品存檔，不另建全域靜態進度。
public static partial class RMZDistrict
{
    const int Init=8100, Last=8101, Fatigue=8102, Level=8120, Assign=8130;
    public const int Message=8199;
    static readonly string[] Jobs={"織布工房","礦料加工","藥草栽培","夜間巡守","魔晶提煉","夜間接待"};
    static readonly string[] Goods={"工房細布","精煉鐵料","乾燥藥材","守備徽記","夜輝魔晶","接待收入"};
    static readonly int[] Yield={4,3,4,2,2,0};
    // 第 6 個工作站「夜間接待」只能排夜班，不產物品：每人每班直接入帳金錢與隸章（乘設施等級），疲勞額外增加。
    const int Hosting=5, HostGold=80, HostStamp=3, HostFatigue=10;
    // 角色順序：尤梨卡、露娜、克萊爾、雪瑤、雪霜、鈴音。雙子共用關係值，分別簽約。
    static readonly string[] Names={"尤梨卡","露娜","克萊爾","雪瑤","雪霜","鈴音"};
    static readonly int[] Heroines={1,2,3,4,4,5};
    public static int ContractCount { get { int n=0;for(int i=0;i<6;i++)if(V(8201+i)>0)n++;return n; } }
    public static bool CanEnter=>V(8140)>0&&ContractCount>0;
    public static string EntryReason=>"進入奴隸區需要：已購買住宅，並有至少一位簽約角色。\n"+(V(8140)>0?"住宅：已購買":"住宅：未購買（3000 G）")+"｜已簽約："+ContractCount+" 人\n可在公會引路人處購屋及辦理契約。";
    static void SyncAssignments(){for(int j=0;j<6;j++){int count=0;for(int i=0;i<6;i++)if(V(8201+i)>0&&V((j>=3?8221:8211)+i)==j+1)count++;Set(Assign+j,count);}}
    static string Sign(int who){
        if(who<0||who>=6)return "請選擇簽約對象。";
        if(V(8201+who)>0)return Names[who]+"已經簽約，不需重複辦理。";
        int h=Heroines[who];
        if(RMZGifts.Love(h)<100||RMZGifts.Fall(h)<100)return Names[who]+"尚未符合條件。\n好感 "+RMZGifts.Love(h)+"/100｜墮落 "+RMZGifts.Fall(h)+"/100\n兩項都達到 100 後才能簽訂奴隸契約。";
        Set(8201+who,1);return "已與"+Names[who]+"簽訂奴隸契約。\n可以安排日班與夜班；同一班次只能做一種工作。";
    }
    static string AssignPerson(int job,int who){
        if(!CanEnter)return EntryReason;
        if(who<0||who>=6||V(8201+who)==0)return "這位角色還沒有簽約，不能安排工作。";
        int key=(job>=3?8221:8211)+who;
        if(V(key)==job+1)return Names[who]+"已安排這項工作，不會重複計算人數。";
        Set(key,job+1);SyncAssignments();return Names[who]+"已安排「"+Jobs[job]+"」。\n同班原有工作已替換，下次換班結算產物。";
    }
    static int V(int id)=>RMZGameState.Variable(id);
    static void Set(int id,int value)=>RMZGameState.SetVariable(id,value);
    // 一天兩個結算段；傍晚開始夜班，翌日早晨結算夜班。
    static int Stamp(int day,int phase)=>Math.Max(0,day)*2+(phase>=2?1:0);
    static int Now=>Stamp(V(61),V(28));
    static void Ensure(){if(V(Init)!=0)return;Set(Init,1);Set(Last,Now);for(int i=0;i<6;i++)Set(Level+i,1);}
    public static void Advance(int day,int phase)
    {
        if(V(Init)==0)return;
        SyncAssignments();
        int target=Stamp(day,phase),last=V(Last);
        if(target<=last)return;
        if(!CanEnter){Set(Last,target);return;}
        int[] produced=new int[6];int worked=0;int hostGold=0,hostStamp=0;string guestNotice="";
        for(int s=last;s<target;s++){
            bool night=(s%2)==1;if(!night&&V(8141)>0)RMZParty.GainItem(2106,3);int start=night?3:0,end=night?6:3;
            int staff=0;for(int i=start;i<end;i++)staff+=V(Assign+i);
            if(V(Fatigue)>=75||staff==0){Set(Fatigue,Math.Max(0,V(Fatigue)-35));continue;}
            int hosted=0;
            for(int i=start;i<end;i++){
                if(i==Hosting){hosted=V(Assign+i);continue;}
                int n=V(Assign+i)*Yield[i]*V(Level+i);
                if(n>0){RMZParty.GainItem(2101+i,n);produced[i]+=n;}
            }
            if(hosted>0){int lv=V(Level+Hosting);hostGold+=hosted*HostGold*lv;hostStamp+=hosted*HostStamp*lv;if(night)guestNotice+=CountHostShift();}
            worked++;Set(Fatigue,Math.Min(100,V(Fatigue)+(night?25:20)+(hosted>0?HostFatigue:0)));
        }
        Set(Last,target);
        if(hostGold>0){RMZParty.GainGold(hostGold);Set(738,V(738)+hostStamp);}
        string result="本次換班結算：";
        for(int i=0;i<5;i++)if(produced[i]>0)result+="\n"+Goods[i]+" ×"+produced[i];
        if(hostGold>0)result+="\n夜間接待收入 "+hostGold+" G、隸章 +"+hostStamp;
        result+=guestNotice;
        if(worked==0)result+="\n休息恢復體力，沒有生產。";
        RMZGameState.SetVariableString(8188,result);
    }
    public static void Command(string action,int job)
    {
        Ensure();Advance(V(61),V(28));
        int person=job; string[] parts=(action??"status").Split(':');action=parts[0];
        if(parts.Length>1&&int.TryParse(parts[1],out int parsed))person=parsed;
        job=Math.Clamp(job,0,5);
        SyncAssignments();
        if(!CanEnter&&(action=="upgrade"||action=="deliver"||action.StartsWith("ranch")||action=="buyRanch")){RMZGameState.SetVariableString(Message,EntryReason);return;}
        string msg="";int level=V(Level+job);
        switch(action){
            case "sign":msg=Sign(person);break;
            case "assignPerson":msg=AssignPerson(job,person);break;
            case "entryCheck":Set(8231,CanEnter?1:0);msg=EntryReason;break;
            case "roster":
                msg="簽約名冊（好感／墮落需各 100）";
                for(int i=0;i<6;i++)msg+=(i%2==0?"\n":"　")+Names[i]+(V(8201+i)>0?"：已簽約":"：未簽約");
                break;
            case "buyHouse":
                if(V(8140)>0)msg="這排住宅已經屬於你，可以進屋休息。";
                else if(RMZParty.Gold<3000)msg="住宅售價 3000 G，金錢不足。";
                else{RMZParty.GainGold(-3000);Set(8140,1);msg="已購得住宅！從中央門口進入，有簽約角色後即可進入奴隸區定居。";}
                break;
            case "buyRanch":
                if(V(8141)>0)msg="牧場已經屬於你。";
                else if(RMZParty.Gold<5000)msg="牧場售價 5000 G，金錢不足。";
                else{RMZParty.GainGold(-5000);Set(8141,1);msg="牧場購買完成！每個日班生產 3 份牧場補給。";}
                break;
            case "ranchWork":
                if(V(8142)==Now+1)msg="這個班次已打過工，換班後再來。";
                else{Set(8142,Now+1);RMZParty.GainGold(60);msg="完成清掃與添料，獲得 60 G。每班僅能領一次。";}
                break;
            case "ranchDeliver":
                if(RMZParty.NumItems(2106)<5)msg="需要 5 份牧場補給。";
                else{RMZParty.GainItem(2106,-5);Set(738,V(738)+15);msg="交付牧場補給，獲得 15 隸章。";}
                break;
            case "guest":msg=PlayGuest(person);break;
            case "assign":msg="請選擇已簽約的角色安排工作。";break;
            case "restDay":for(int i=0;i<6;i++)Set(8211+i,0);SyncAssignments();msg="已取消所有角色的日班排程。";break;
            case "restNight":for(int i=0;i<6;i++)Set(8221+i,0);SyncAssignments();msg="已取消所有角色的夜班排程。";break;
            case "upgrade":
                int cost=20*level;
                if(level>=3)msg="這座設施已達 3 級。";
                else if(job==Hosting){
                    int gold=500*level;
                    if(V(738)<cost||RMZParty.Gold<gold)msg="升級需要 "+cost+" 隸章和 "+gold+" G。";
                    else{Set(738,V(738)-cost);RMZParty.GainGold(-gold);Set(Level+job,level+1);msg=Jobs[job]+"升至 "+(level+1)+" 級，每人每班收入 "+HostGold*(level+1)+" G、隸章 "+HostStamp*(level+1)+"。";}
                }
                else if(V(738)<cost||RMZParty.NumItems(2101+job)<6*level)msg="升級需要 "+cost+" 隸章和 "+6*level+" 份"+Goods[job]+"。";
                else{Set(738,V(738)-cost);RMZParty.GainItem(2101+job,-6*level);Set(Level+job,level+1);msg=Jobs[job]+"升至 "+(level+1)+" 級，每人每班產出 "+Yield[job]*(level+1)+" 份。";}
                break;
            case "deliver":
                if(job==Hosting)msg="夜間接待的收入每班直接入帳，沒有物品可交貨。";
                else if(RMZParty.NumItems(2101+job)<5)msg="交貨需要 5 份"+Goods[job]+"。";
                else{RMZParty.GainItem(2101+job,-5);int pay=job>=3?15:10;Set(738,V(738)+pay);msg="交付 5 份"+Goods[job]+"，獲得 "+pay+" 隸章。";}
                break;
            default:
                msg="已簽約 "+ContractCount+" 人｜疲勞 "+V(Fatigue)+"/100｜隸章 "+V(738)+"\n日／夜分班；疲勞達 75 時該班自動休息。";
                msg+="\n日班：織布 "+V(Assign)+"／礦料 "+V(Assign+1)+"／藥草 "+V(Assign+2);
                msg+="\n夜班：巡守 "+V(Assign+3)+"／魔晶 "+V(Assign+4)+"／接待 "+V(Assign+5);
                break;
        }
        RMZGameState.SetVariableString(Message,msg);RMZEventRunner.Instance?.RequestRefresh();
    }
}
