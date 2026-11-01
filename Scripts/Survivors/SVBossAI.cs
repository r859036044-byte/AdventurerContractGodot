// 兩關制（A／S 級）結尾王專用的招式組。
//
// 只有這一隻才掛：第一關那隻王只是「打死才能過關」的門檻，
// 玩家會認真打的是這一隻，所以招式留給牠。
//
// 三招輪流放：AOE 範圍衝擊 → 突進 → 召喚小怪，各自獨立冷卻，
// 用一個共用的「這回合換誰」指標決定順序，不會兩招疊在一起放。
using System.Collections.Generic;
using RMZS.GodotPort;

namespace RMZS
{
    public class SVBossAI : MonoBehaviour
    {
        SVEnemy _boss;

        // ---- 節奏 ----
        [Tooltip("放完一招之後，隔多久才能再放下一招")]
        public float SkillInterval = 6.5f;
        float _timer;
        int _turn;   // 0=AOE 1=突進 2=召喚，輪著來

        bool _busy;   // 招式演出中（前搖／突進中），這段時間不再判斷下一招

        // ---- AOE 範圍衝擊 ----
        const float AoeTelegraphTime = 0.85f;
        const float AoeRadius = 3.2f;

        // ---- 突進 ----
        const float ChargeWindup = 0.45f;
        const float ChargeDuration = 0.6f;
        const float ChargeSpeedMul = 3.2f;
        const float ChargeDamageMul = 1.6f;

        // ---- 召喚 ----
        const int SummonCount = 3;

        public void Setup(SVEnemy boss)
        {
            _boss = boss;
            _timer = SkillInterval * 0.5f;   // 王剛登場先讓玩家喘口氣，不要一上來就開技能
            _turn = 0;
        }

        void Update()
        {
            if (_boss == null || _boss.IsDead) { enabled = false; return; }
            var game = SVGame.Instance;
            if (game == null || game.Paused || game.EnemiesFrozen) return;
            if (_busy) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            _timer = SkillInterval;
            switch (_turn)
            {
                case 0: StartCoroutine(CastAoe()); break;
                case 1: StartCoroutine(CastCharge()); break;
                default: CastSummon(); break;
            }
            _turn = (_turn + 1) % 3;
        }

        // ---------------- ① AOE 範圍衝擊：打在玩家腳下，給時間閃 ----------------

        System.Collections.IEnumerator CastAoe()
        {
            _busy = true;
            var game = SVGame.Instance;
            Vector2 center = game.Player.Pos;

            // 前搖：紅圈慢慢浮現，玩家看得到範圍、來得及走開
            game.SpawnPulse(center, AoeRadius, new Color(1f, 0.25f, 0.2f, 0.35f), AoeTelegraphTime);
            yield return new WaitForSeconds(AoeTelegraphTime);

            if (_boss == null || _boss.IsDead) { _busy = false; yield break; }

            game.SpawnPulse(center, AoeRadius, new Color(1f, 0.5f, 0.2f, 0.75f), 0.3f);
            int dmg = Mathf.Max(6, Mathf.RoundToInt(_boss.ContactDamage * 1.4f));
            DamageAround(center, AoeRadius, dmg);

            _busy = false;
        }

        // ---------------- ② 突進：短暫加速衝向玩家，撞到傷害加成 ----------------

        System.Collections.IEnumerator CastCharge()
        {
            _busy = true;
            var game = SVGame.Instance;

            game.SpawnPulse(_boss.Pos, 1.6f, new Color(1f, 0.75f, 0.2f, 0.6f), ChargeWindup);
            yield return new WaitForSeconds(ChargeWindup);

            if (_boss == null || _boss.IsDead) { _busy = false; yield break; }

            int baseContact = _boss.ContactDamage;
            _boss.SpeedMultiplier = ChargeSpeedMul;
            _boss.ContactDamage = Mathf.RoundToInt(baseContact * ChargeDamageMul);

            yield return new WaitForSeconds(ChargeDuration);

            if (_boss != null && !_boss.IsDead)
            {
                _boss.SpeedMultiplier = 1f;
                _boss.ContactDamage = baseContact;
            }

            _busy = false;
        }

        // ---------------- ③ 召喚：拉幾隻雜魚出來 ----------------

        void CastSummon()
        {
            var game = SVGame.Instance;
            var pool = new List<SVEnemyType>();
            foreach (var e in game.Enemies) if (e != null && !e.IsBoss) pool.Add(e);
            if (pool.Count == 0) return;

            game.SpawnPulse(_boss.Pos, 4f, new Color(0.6f, 0.3f, 0.9f, 0.6f), 0.5f);

            for (int i = 0; i < SummonCount; i++)
            {
                var type = pool[Random.Range(0, pool.Count)];
                var go = new GameObject(type.Name);
                go.transform.SetParent(_boss.transform.parent, false);

                var e = go.AddComponent<SVEnemy>();
                e.Setup(type, 1f, 1f);
                var offset = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3f);
                e.Pos = SVConfig.ClampToArena(_boss.Pos + offset);
            }
        }

        // ---------------- 共用 ----------------

        void DamageAround(Vector2 center, float radius, int damage)
        {
            var game = SVGame.Instance;
            var player = game.Player;
            if (player != null && !player.IsDead && player.Overlaps(center, radius))
                player.TakeDamage(damage, SVDamageKind.Physical);

            for (int i = 0; i < SVCompanion.Active.Count; i++)
            {
                var c = SVCompanion.Active[i];
                if (c != null && !c.Retired && c.Overlaps(center, radius))
                    c.TakeDamageFrom(damage, _boss);
            }
        }

        void OnDestroy()
        {
            // 死掉或被清場的時候別留著加成——保險起見還原一次
            if (_boss != null) _boss.SpeedMultiplier = 1f;
        }
    }
}
