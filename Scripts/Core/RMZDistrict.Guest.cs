using System;
namespace RMZ;
// 神秘客人：角色排「夜間接待」累計夠多班後，會有跟她背景相關的客人登門，
// 到工房的「神秘客人」處接見即可看到專屬劇情（第一次有獎勵，之後可重看）。
// 劇情一律是 4 個對話框，內容點到為止、不描寫細節；由事件依序顯示變數 8195～8198。
public static partial class RMZDistrict
{
    const int GuestState=8250, GuestShifts=8260, GuestHas=8194, GuestPage1=8195, GuestNeed=2;
    // 狀態：0 尚未登門／1 客人在等／2 已接見過

    sealed record Scene(string Guest,string[] Pages,int Gold,int Stamp,int Item,int ItemCount);

    // 以 Names 的順序（尤梨卡、露娜、克萊爾、雪瑤、雪霜、鈴音）；null＝尚未設計客人。
    static readonly Scene[] Scenes={
        new("錢商人",new[]{
            "夜深了，一位戴著兜帽的男人走進接待室。\n他把一只沉甸甸的錢袋放在桌上，沒有報上名字。\n「聽說你是六年前從鄉下搬來的。」",
            "他的手指在帳簿上輕輕敲著。\n「令尊當年欠下的那筆錢，我替你還清了。」\n尤梨卡的呼吸停了一瞬——那件事，她從沒對任何人說過。",
            "「我是商人，不做虧本的買賣。」\n他笑著把錢袋推到她面前。\n「今晚，就當是第一期的利息。」",
            "燭火搖了搖，熄滅了。\n…………\n天亮前，桌上多了一袋金幣，和一張寫著「下次再來」的字條。"},1500,0,0,0),
        new("老公的隊友",new[]{
            "接待室的門被推開，來的是個熟悉的身影。\n——她丈夫隊伍裡的劍士。\n他滿身塵土，像是剛從地下城回來。",
            "「……露娜姊？」他愣在門口。\n露娜垂下視線，雙手在身前握緊。\n「今晚的事，請你……不要告訴他。」",
            "他沉默了很久，最後把門關上。\n「他還在迷宮深處，沒那麼快回來。」\n「我不會說的。但今晚，我也不打算走。」",
            "窗外的月亮被雲遮住了。\n…………\n清晨，他留下一袋金幣，和一句幾乎聽不見的「對不起」。"},0,20,0,0),
        null,
        new("鐵匠",new[]{
            "一位滿臉煤灰的鐵匠走進接待室，身上還帶著爐火的味道。\n他把一把嶄新的短劍放在桌上。\n「聽說這裡有個會魔法的獸族姑娘。」",
            "雪瑤的耳朵動了動。\n「那把短劍的尺寸……是我妹妹慣用的。」\n鐵匠咧嘴一笑：「你們姐妹倆的裝備，都是我打的。」",
            "「我只是想看看，用我的鐵打出來的人，現在過得好不好。」\n他攤開粗糙的大手，掌心全是燒傷的舊疤。\n「今晚，請你陪我喝一杯。」",
            "酒過三巡，燈也暗了。\n…………\n他留下了一批精煉鐵料，說下次要替她打一支新的法杖。"},0,0,2102,10),
        new("鐵匠",new[]{
            "又是那位鐵匠。這次他帶來一對嶄新的護腕。\n「你姐姐的法杖還在打，先給妹妹的拳頭備著。」\n雪霜把護腕套上手臂，意外地合手。",
            "「獸族的骨架跟人類不一樣，我改了三次版型。」\n他有些得意，又有些不好意思。\n「你們是我打過最難伺候、也最有意思的客人。」",
            "雪霜盯著他看了好一會兒，尾巴慢慢晃了起來。\n「……那今晚，我就當是替姐姐謝你。」\n鐵匠摸摸後腦勺，笑得像個孩子。",
            "爐火般的溫度在房裡慢慢散去。\n…………\n他留下了鐵料和一小袋金幣，說下個月再帶新作品來。"},300,0,2102,10),
        null};

    // 夜班接待結算時，替每位在接待班的角色累計班數；剛好達標時回傳通知文字。
    static string CountHostShift()
    {
        string note="";
        for(int w=0;w<6;w++){
            if(V(8221+w)!=Hosting+1)continue;
            int n=V(GuestShifts+w)+1;Set(GuestShifts+w,n);
            if(Scenes[w]!=null&&V(GuestState+w)==0&&n>=GuestNeed){Set(GuestState+w,1);note+="\n神秘客人（"+Scenes[w].Guest+"）登門找"+Names[w]+"，到「神秘客人」處接見。";}
        }
        return note;
    }

    // 接見客人：把劇情放進變數 8195～8198（4 個對話框），並把 8194 設成 1 讓事件顯示；失敗時只給 8199 一段說明。
    static string PlayGuest(int who)
    {
        Set(GuestHas,0);
        if(who<0||who>=6||V(8201+who)==0)return "這位角色還沒有簽約。";
        var sc=Scenes[who];
        if(sc==null)return Names[who]+"還沒有神秘客人。";
        int state=V(GuestState+who);
        if(state==0){
            int left=Math.Max(0,GuestNeed-V(GuestShifts+who));
            return "目前沒有客人指名"+Names[who]+"。\n需要讓她排夜間接待班（還差 "+left+" 班）。";
        }
        for(int i=0;i<4;i++)RMZGameState.SetVariableString(GuestPage1+i,sc.Pages[i]);
        Set(GuestHas,1);
        string note=Names[who]+"｜神秘客人："+sc.Guest;
        if(state==1){
            Set(GuestState+who,2);
            string reward="";
            if(sc.Gold>0){RMZParty.GainGold(sc.Gold);reward+="　"+sc.Gold+" G";}
            if(sc.Stamp>0){Set(738,V(738)+sc.Stamp);reward+="　隸章 +"+sc.Stamp;}
            if(sc.ItemCount>0){RMZParty.GainItem(sc.Item,sc.ItemCount);reward+="　"+Goods[sc.Item-2101]+" ×"+sc.ItemCount;}
            note+="\n（首次接見獎勵："+reward.TrimStart('　')+"）";
        }
        return note;
    }
}
