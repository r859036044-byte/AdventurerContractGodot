using System.Collections.Generic;
using RMZS.GodotPort;
namespace RMZS;
public partial class SVGame
{
    float highWave=24, highStorm=16, highSkillClock;
    int highRunStage=1;
    readonly List<(Vector2 pos,float time)> highWarnings=new();
    void ResetHighRank(){highWave=24;highStorm=16;highSkillClock=0;highRunStage=1;highWarnings.Clear();}
    void UpdateHighRank(float dt)
    {
        if(MapRank<3||IsTutorial||Player==null||IsOver||_stageTransitioning)return;
        if(highRunStage!=RunStage){highRunStage=RunStage;highWarnings.Clear();highStorm=12;highWave=18;}
        if(BossPhase||_bossArrival||(RunStage==1&&_stage1BossSpawned))highWarnings.Clear();
        // 停止追蹤後留下 1.8 秒預警；傷害與動畫使用同一中心。
        for(int i=highWarnings.Count-1;i>=0;i--){
            var w=highWarnings[i];w.time-=dt;
            if(w.time>0){highWarnings[i]=w;continue;}
            highWarnings.RemoveAt(i);
            SpawnPulse(w.pos,2.3f,new Color(.8f,.45f,1f,.8f),.35f);
            if((Player.Pos-w.pos).sqrMagnitude<2.3f*2.3f)
                Player.TakeDamage(MapRank==4?32:20,SVDamageKind.Magic);
        }
        // 王戰保留王本身的招式，避免地形攻擊和召援疊在王的預警上。
        if(!BossPhase && !_bossArrival && (RunStage>1||!_stage1BossSpawned)){
            highStorm-=dt;highWave-=dt;
            if(highStorm<=0){
                highStorm=MapRank==4?14:20;
                int count=MapRank==4?(RunStage==2?3:2):1;
                for(int i=0;i<count;i++){
                    var pos=Player.Pos+(i==0?Vector2.zero:SVConfig.RandomOnCircle(4f));
                    highWarnings.Add((pos,1.8f));
                    SpawnPulse(pos,2.3f,new Color(1f,.3f,.2f,.65f),1.8f);
                }
            }
            if(highWave<=0){
                highWave=MapRank==4?26:32;
                for(int i=0;i<(MapRank==4?4:3);i++){
                    if(SVEnemy.Active.Count>=Mathf.Min(MaxEnemies,SVQuality.MaxEnemies))break;
                    var type=PickType(Stage);if(type==null||type.IsBoss)continue;
                    var e=SpawnOne(type,Mathf.Pow(HpGrowth,Stage)*1.65f,Mathf.Pow(DamageGrowth,Stage)*1.1f);
                    SpawnPulse(e.Pos,2f,new Color(1f,.7f,.2f,.75f),1f);
                }
            }
        }
        highSkillClock+=dt;
        if(highSkillClock<8f)return;highSkillClock-=8f;
        int ward=Stacks("a_rune_guard"),nova=Stacks("a_frost_echo"),hunt=Stacks("s_star_hunt"),drain=Stacks("s_soul_tide");
        if(ward>0){Player.AddShield(Mathf.Max(0,Mathf.Min(12*ward,36*ward-Player.Shield)));SpawnPulse(Player.Pos,2f,new Color(.5f,.8f,1f,.6f),.5f);}
        if(nova>0){
            SpawnPulse(Player.Pos,4f,new Color(.55f,.9f,1f,.7f),.6f);
            foreach(var e in SVEnemy.Active.ToArray())if(e!=null&&!e.IsDead&&(e.Pos-Player.Pos).sqrMagnitude<=16f){e.TakeDamage(24*nova,SVDamageKind.Magic);e.Slow(.65f,2f);}
        }
        if(hunt>0){
            var targets=new List<SVEnemy>();foreach(var e in SVEnemy.Active.ToArray())if(e!=null&&!e.IsDead&&(e.Pos-Player.Pos).sqrMagnitude<144f)targets.Add(e);
            targets.Sort((a,b)=>b.Hp.CompareTo(a.Hp));
            for(int i=0;i<Mathf.Min(3,targets.Count);i++){var e=targets[i];SpawnPulse(e.Pos,1.4f,new Color(1f,.85f,.4f,.8f),.4f);e.TakeDamage(65*hunt,SVDamageKind.Magic);}
        }
        if(drain>0){
            int hits=0;SpawnPulse(Player.Pos,5f,new Color(.9f,.3f,.6f,.7f),.6f);
            foreach(var e in SVEnemy.Active.ToArray())if(e!=null&&!e.IsDead&&(e.Pos-Player.Pos).sqrMagnitude<=25f){if(e.TakeDamage(36*drain,SVDamageKind.Magic)>0)hits++;}
            Player.Heal(Mathf.Min(5,hits)*3*drain);
        }
    }
}
