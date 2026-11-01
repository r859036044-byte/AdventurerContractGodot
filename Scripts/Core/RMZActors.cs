// 角色的執行期狀態，對應 MZ 的 $gameActors / Game_Actor。
//
// 目前只做「事件會用到」的部分：裝備、等級、隊伍成員。
// 戰鬥數值等做戰鬥系統時再補。
//
// 為什麼需要這個：事件裡大量使用「條件分歧 → 角色 → 裝備著某武器／防具」
// 來決定 UI 要畫哪一張圖（例如鐵匠鋪的裝備欄，木/藍/紅/金四階）。
// 沒有裝備資料的話那些條件無從判斷。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    public class RMZActorState
    {
        public int Id;
        public int Level = 1;

        /// <summary>累積經驗值（跟 MZ 一樣是總量）。-1 = 還沒算過，第一次用到時補成目前等級的起點。</summary>
        public int Exp = -1;

        /// <summary>依 MZ 的裝備欄位排列：索引 0 = 武器欄，1 之後是防具欄。0 表示空。</summary>
        public int[] Equips = new int[5];

        public readonly HashSet<int> States = new HashSet<int>();
        public readonly HashSet<int> Skills = new HashSet<int>();
        public int ClassId;
    }

    /// <summary>一件初始裝備。EquipTypeId 是 MZ 的裝備類型，1 起算（1 = 武器欄）。</summary>
    [System.Serializable]
    public class RMZStartingEquip
    {
        public int ActorId = 1;

        public int EquipTypeId = 1;

        public int ItemId;

        public string Describe()
        {
            var item = EquipTypeId == 1 ? RMZDatabase.Weapon(ItemId) : RMZDatabase.Armor(ItemId);
            string name = item != null ? RMZLocalization.Translate(item.name) : $"id {ItemId}";
            return $"角色{ActorId} 欄位{EquipTypeId} ← {name}";
        }
    }

    public static class RMZActors
    {
        static readonly Dictionary<int, RMZActorState> Cache = new Dictionary<int, RMZActorState>();
        static readonly List<int> Party = new List<int>();
        static bool _partyReady;

        public static RMZActorState Actor(int id)
        {
            if (id <= 0) return null;
            if (Cache.TryGetValue(id, out var a)) return a;

            var data = RMZDatabase.Actor(id);
            a = new RMZActorState { Id = id };

            if (data != null)
            {
                a.Level = Mathf.Max(1, data.initialLevel);
                a.ClassId = data.classId;

                // 資料庫的初始裝備
                if (data.equips != null)
                {
                    a.Equips = new int[Mathf.Max(5, data.equips.Length)];
                    for (int i = 0; i < data.equips.Length; i++) a.Equips[i] = data.equips[i];
                }
            }

            Cache[id] = a;
            return a;
        }

        // ---------------- 隊伍 ----------------

        public static List<int> PartyMembers
        {
            get
            {
                if (!_partyReady)
                {
                    _partyReady = true;
                    Party.Clear();
                    var m = RMZDatabase.System?.partyMembers;
                    if (m != null) Party.AddRange(m);
                }
                return Party;
            }
        }

        public static bool InParty(int actorId) => PartyMembers.Contains(actorId);

        /// <summary>目前已經建出來的所有角色狀態。存檔用。</summary>
        public static IEnumerable<RMZActorState> All => Cache.Values;

        /// <summary>讀檔用：整批換掉隊伍名單。</summary>
        public static void RestoreParty(List<int> members)
        {
            _partyReady = true;
            Party.Clear();
            if (members != null) Party.AddRange(members);
        }

        public static void AddMember(int actorId)
        {
            if (actorId > 0 && !PartyMembers.Contains(actorId)) PartyMembers.Add(actorId);
        }

        public static void RemoveMember(int actorId) => PartyMembers.Remove(actorId);

        // ---------------- 裝備 ----------------

        /// <summary>
        /// 對應 MZ 的 Game_Actor.changeEquipById(etypeId, itemId)。
        /// etypeId 是 1 起算的裝備類型：1 = 武器，其餘是防具欄位。
        /// </summary>
        public static void ChangeEquipById(int actorId, int etypeId, int itemId)
        {
            var a = Actor(actorId);
            if (a == null) return;

            int slot = etypeId - 1;
            if (slot < 0) return;

            if (slot >= a.Equips.Length)
            {
                var bigger = new int[slot + 1];
                a.Equips.CopyTo(bigger, 0);
                a.Equips = bigger;
            }

            a.Equips[slot] = itemId;
            RMZEventRunner.Instance?.RequestRefresh();
        }

        /// <summary>身上有沒有裝備這件武器（對應條件分歧的「角色 → 武器」）。</summary>
        public static bool HasWeapon(int actorId, int weaponId)
        {
            var a = Actor(actorId);
            if (a == null || weaponId <= 0) return false;

            // 武器只會在武器欄（索引 0；雙持時還有索引 1）
            for (int slot = 0; slot < a.Equips.Length; slot++)
            {
                if (!IsWeaponSlot(slot)) continue;
                if (a.Equips[slot] == weaponId) return true;
            }
            return false;
        }

        /// <summary>身上有沒有裝備這件防具。</summary>
        public static bool HasArmor(int actorId, int armorId)
        {
            var a = Actor(actorId);
            if (a == null || armorId <= 0) return false;

            for (int slot = 0; slot < a.Equips.Length; slot++)
            {
                if (IsWeaponSlot(slot)) continue;
                if (a.Equips[slot] == armorId) return true;
            }
            return false;
        }

        /// <summary>MZ 的 equipSlots 預設是 [1,2,3,4,5]，所以只有第 0 格是武器。</summary>
        static bool IsWeaponSlot(int slot) => slot == 0;

        public static int EquipAt(int actorId, int slot)
        {
            var a = Actor(actorId);
            if (a == null || slot < 0 || slot >= a.Equips.Length) return 0;
            return a.Equips[slot];
        }

        // ---------------- 其他 ----------------

        public static void ChangeLevel(int actorId, int level)
        {
            var a = Actor(actorId);
            if (a == null) return;

            var data = RMZDatabase.Actor(actorId);
            int max = data != null ? Mathf.Max(1, data.maxLevel) : 99;
            a.Level = Mathf.Clamp(level, 1, max);
            a.Exp = ExpForLevel(actorId, a.Level);   // 跟 MZ 一樣：直接改等級時經驗從該級起點算
            if (actorId == 1) SyncExpVars();
            RMZEventRunner.Instance?.RequestRefresh();
        }

        // ---------------- 經驗值 ----------------
        // 原作是在冒險結算的腳本裡 gainExp；移植版的冒險改成小遊戲，所以結算時由 RMZSurvivorsBridge 發經驗。

        /// <summary>公會介面顯示用：716 = 這一級已累積的經驗，717 = 升到下一級要的經驗。69 = 等級。</summary>
        public const int ExpNowVar = 716, ExpNeedVar = 717, LevelVar = 69;

        /// <summary>MZ 的經驗曲線（Game_Actor.expForLevel）。</summary>
        public static int ExpForLevel(int actorId, int level)
        {
            if (level <= 1) return 0;
            var p = RMZDatabase.Class(Actor(actorId)?.ClassId ?? 1)?.expParams;
            double basis = p != null && p.Length > 0 ? p[0] : 30, extra = p != null && p.Length > 1 ? p[1] : 20;
            double accA = p != null && p.Length > 2 ? p[2] : 30, accB = p != null && p.Length > 3 ? p[3] : 30;
            return (int)System.Math.Round(basis * System.Math.Pow(level - 1, 0.9 + accA / 250) * level * (level + 1)
                   / (6 + System.Math.Pow(level, 2) / 50 / accB) + (level - 1) * extra);
        }

        public static int Exp(int actorId)
        {
            var a = Actor(actorId);
            if (a == null) return 0;
            if (a.Exp < 0) a.Exp = ExpForLevel(actorId, a.Level);
            return a.Exp;
        }

        /// <summary>加經驗，夠了就升級。回傳升了幾級。</summary>
        public static int GainExp(int actorId, int amount)
        {
            var a = Actor(actorId);
            if (a == null || amount <= 0) return 0;
            var data = RMZDatabase.Actor(actorId);
            int max = data != null ? Mathf.Max(1, data.maxLevel) : 99;
            a.Exp = Exp(actorId) + amount;
            int before = a.Level;
            while (a.Level < max && a.Exp >= ExpForLevel(actorId, a.Level + 1)) a.Level++;
            if (actorId == 1) SyncExpVars();
            RMZEventRunner.Instance?.RequestRefresh();
            return a.Level - before;
        }

        /// <summary>把男主的等級／經驗寫進變數，公會左上角的文字才會跟著變。</summary>
        public static void SyncExpVars()
        {
            var a = Actor(1);
            if (a == null) return;
            int from = ExpForLevel(1, a.Level), to = ExpForLevel(1, a.Level + 1);
            RMZGameState.SetVariable(LevelVar, a.Level);
            RMZGameState.SetVariable(ExpNowVar, Mathf.Max(0, Exp(1) - from));
            RMZGameState.SetVariable(ExpNeedVar, Mathf.Max(1, to - from));
        }

        public static int Level(int actorId) => Actor(actorId)?.Level ?? 1;

        public static bool HasState(int actorId, int stateId) =>
            Actor(actorId)?.States.Contains(stateId) ?? false;

        public static bool HasSkill(int actorId, int skillId) =>
            Actor(actorId)?.Skills.Contains(skillId) ?? false;

        /// <summary>
        /// 套用「新遊戲初始裝備」。
        /// 遊戲本身是靠事件（Map002 EV023／EV026／EV036 等）無條件發這套樺木粗製裝備的，
        /// 但直接指定地圖測試時那些事件不會跑，裝備欄就會缺格。這裡先補上，
        /// 之後事件真的跑到時會用同樣的值覆蓋一次，不衝突。
        /// </summary>
        public static void ApplyStartingEquips(List<RMZStartingEquip> list)
        {
            if (list == null) return;

            foreach (var e in list)
            {
                if (e == null || e.ActorId <= 0 || e.EquipTypeId <= 0 || e.ItemId <= 0) continue;
                ChangeEquipById(e.ActorId, e.EquipTypeId, e.ItemId);
            }
        }

        /// <summary>遊戲事件發的那一套：樺木長劍／胸甲／護腿／頭盔／護靴，全是「粗製」階。</summary>
        public static List<RMZStartingEquip> DefaultStartingEquips() => new List<RMZStartingEquip>
        {
            new RMZStartingEquip { ActorId = 1, EquipTypeId = 1, ItemId = 2  },   // 武器：樺木長劍（粗製）
            new RMZStartingEquip { ActorId = 1, EquipTypeId = 2, ItemId = 2  },   // 樺木胸甲（粗製）
            new RMZStartingEquip { ActorId = 1, EquipTypeId = 3, ItemId = 6  },   // 樺木護腿（粗製）
            new RMZStartingEquip { ActorId = 1, EquipTypeId = 4, ItemId = 10 },   // 樺木頭盔（粗製）
            new RMZStartingEquip { ActorId = 1, EquipTypeId = 5, ItemId = 14 },   // 樺木護靴（粗製）
        };

        public static void Clear()
        {
            Cache.Clear();
            Party.Clear();
            _partyReady = false;
        }
    }
}
