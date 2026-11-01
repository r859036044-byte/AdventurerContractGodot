using System.Collections;
using RMZS.GodotPort;
namespace RMZS;
public partial class SVGame
{
    public bool IsTutorial => _briefing?.Tutorial == true;
    public bool TutorialXpEnabled { get; private set; }
    public string TutorialHint { get; private set; } = "";
    bool _tutorialTalking;
    Vector2 _tutorialDrop;
    Godot.Rect2 TutorialWorldRect(Vector2 position,float size)
    {
        var at=Cam.WorldToScreenPoint(position);
        float x=at.x*1280f/Screen.width,y=(Screen.height-at.y)*720f/Screen.height;
        return new Godot.Rect2(Mathf.Clamp(x-size/2,20,1260-size),Mathf.Clamp(y-size/2,20,700-size),size,size);
    }
    IEnumerator Teach(string text,int focus)
    {
        if(IsOver)yield break;
        _tutorialTalking=true;
        try {
            Godot.Rect2 Target()=>focus switch {
                0=>TutorialWorldRect(_tutorialDrop,150),
                1=>new Godot.Rect2(42,52,315,30),
                2=>TutorialWorldRect(FinalBoss?.Pos??Player.Pos,290),
                _=>new Godot.Rect2(1050,636,216,74)
            };
            var task=RMZ.RMZTutorialOverlay.Show("露娜",text,Target);
            while(!task.IsCompleted)yield return null;
        } finally {_tutorialTalking=false;}
    }
    void PracticeWave(int count)
    {
        var type=Enemies.Find(e=>!e.IsBoss);if(type==null)return;
        for(int i=0;i<count;i++){
            var enemy=SpawnOne(type,.12f,.05f);enemy.MaxHp=enemy.Hp=12;enemy.MoveSpeed=1.2f;
            enemy.Pos=Player.Pos+SVConfig.RandomOnCircle(5f+i*.4f);
        }
    }
    IEnumerator LunaTutorial()
    {
        yield return null;
        TutorialXpEnabled=true;PracticeWave(6);
        float elapsed=0,nextWave=4;bool explainedKill=false,explainedLevel=false;
        while(!IsOver&&(elapsed<12||!explainedLevel)){
            if(!Paused){
                if(!explainedKill&&Kills>0){
                    explainedKill=true;
                    yield return Teach("打得很好呢。怪物倒下會留下能量光球，也有機會掉金幣、裝備和捲軸。靠近就能撿起來，我幫你看著周圍。",0);
                    if(IsOver)yield break;
                }
                if(explainedKill&&!explainedLevel&&Player.Level>=2){
                    explainedLevel=true;
                    yield return Teach($"能量值累積滿就會升級。接下來挑一個喜歡的技能吧，選好後按 {RMZ.RMZKeyBindings.Label("skill1")} 或點下方技能圖示使用。慢慢選，大家會等你的。",1);
                    if(IsOver)yield break;
                    OfferChoices();
                    while(!IsOver&&Choosing)yield return null;
                    if(IsOver)yield break;
                    // 選完技能後再提醒一次怎麼放：點圖示或按快捷鍵都行，免得選完就忘了剛才教的。
                    yield return Teach("技能選好了。想放技能的時候，點一下畫面下方對應的圖示，或按快捷鍵都可以，亮起來時就代表可以再用一次。",1);
                    if(IsOver)yield break;
                }
                if(!Paused){elapsed+=Time.deltaTime;
                    if(elapsed>=nextWave){PracticeWave(4);nextWave+=4;}
                    if(explainedKill&&elapsed>=5&&Player.Level<2){SVXpGem.PullAll();Player.GainXp(Player.XpToNext-Player.Xp);}
                }
            }yield return null;
        }
        if(IsOver)yield break;
        var bossType=NextBoss();
        if(bossType!=null){
            BossPhase=true;BossTimeLeft=-1;FinalBoss=SpawnOne(bossType,.05f,.05f);
            FinalBoss.MaxHp=FinalBoss.Hp=180;FinalBoss.MoveSpeed=1.1f;FinalBoss.Pos=Player.Pos+new Vector2(6,0);
            yield return Teach("BOSS 出現了，先站在我身邊。牠比普通怪物強，打倒後也會掉更多戰利品。這次讓大家陪你一起打吧。",2);
            yield return Teach("情況不對就打開暫停選單撤退。一個人冒險能保留八成戰利品；加入別人的隊伍，完成分三成、撤退分兩成。背包原有的東西不會扣，別勉強自己，好嗎？",3);
            float fight=0;
            while(!IsOver&&FinalBoss!=null&&!FinalBoss.IsDead&&fight<8){if(!Paused)fight+=Time.deltaTime;yield return null;}
            if(IsOver)yield break;
            if(FinalBoss!=null&&!FinalBoss.IsDead){SpawnPulse(FinalBoss.Pos,4,new Color(.7f,1,.8f),.6f);FinalBoss.TakeDamage(FinalBoss.MaxHp+100,SVDamageKind.True);}
        }
        foreach(var enemy in SVEnemy.Active.ToArray())if(enemy!=null)Destroy(enemy.gameObject);
        if(!IsOver)CompleteTutorial();
    }
    void CompleteTutorial()
    {
        if(IsOver)return;SVTalkWindow.CloseNow();_tutorialTalking=false;
        _pendingLevelUps=_pendingCards=0;ChoosingBlessing=ChoosingCard=ChoosingAwakening=false;Clear();
    }
}
