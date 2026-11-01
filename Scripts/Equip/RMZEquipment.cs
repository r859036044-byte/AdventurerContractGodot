// 裝備系統：每一件都是獨立的實例，可以重複持有，各自擲出自己的數值。
//
// ---- 為什麼不沿用 RM 那套 ----
// 專案裡的裝備是以「道具」形式放在背包（Items.json 標 <BagEquip>，
// 再用 <BindWeapon: n> / <BindArmor: n> 指向真正的武器防具），
// 而 RM 靠 <IndependentItem> 做「每一件都不一樣」的獨立道具 ——
// 那套在這個專案裡會出問題，所以同一件裝備只拿得到一個。
//
// 這裡改成楓之谷的做法：背包裡放的是**實例**，同一件裝備要幾把有幾把，
// 每一把在取得的當下就把數值擲好、存起來，之後不會再變。
//
// ---- 數值怎麼來的 ----
// RM 這邊每件裝備只填了 params[2]，那其實是「戰力」用的基準值
// （CE49 拿它乘上各部位的係數去算戰力）。戰力那條路原封不動，
// 這裡是在它之上，用同一個基準值再攤出攻擊／血量／防禦。
using System.Collections.Generic;
using Godot;

namespace RMZ
{
    /// <summary>System.json 的 weaponTypes 裡我們真的會用到的兩個。</summary>
    public static class RMZWeaponTypes
    {
        public const int Sword = 2;   // 剑
        public const int Staff = 6;   // 杖
    }

    /// <summary>裝備欄位，對應 System.json 的 equipTypes。</summary>
    public enum RMZEquipSlot
    {
        Weapon = 1,   // 武器
        Body = 2,     // 上衣
        Legs = 3,     // 褲子
        Head = 4,     // 頭部
        Feet = 5,     // 鞋子
    }

    /// <summary>背包裡的一件裝備。每一件都是獨立的，數值在取得時就決定了。</summary>
    [System.Serializable]
    public class RMZEquipInstance
    {
        public int Uid;
        /// <summary>穿在哪位女主身上（RMZHeroineGear）。0 = 男主的背包／身上。</summary>
        public int Owner;
        /// <summary>背包道具的 ID（Items.json）。名稱與圖示都取自它。</summary>
        public int ItemId;
        /// <summary>綁定的武器或防具 ID，決定基準值與欄位。</summary>
        public int BoundId;
        public bool IsWeapon;
        public int Slot;

        // 擲出來的原始值。強化是在這之上加成，回真捲軸會把它重擲。
        public int BaseAtk;    // 物攻
        public int BaseMAtk;   // 魔攻
        public int BaseDef;    // 物防
        public int BaseMDef;   // 魔防
        public int BaseHp;

        /// <summary>強化累積的加成，0.30 = +30%。是「加法累積」不是連乘，不然 50% 捲軸會爆掉。</summary>
        public float Bonus;

        /// <summary>可強化次數。用掉一次就少一次，不管成功失敗。</summary>
        public int Slots = 7;

        /// <summary>已經用掉幾次。</summary>
        public int Used;

        /// <summary>成功幾次，就是名字後面那個 +N。</summary>
        public int Enhanced;

        /// <summary>擲出來的品質，0~100，用來上色與排序。50 是平均。</summary>
        public int Roll;

        public int Atk => Mathf.RoundToInt(BaseAtk * (1f + Bonus));
        public int MAtk => Mathf.RoundToInt(BaseMAtk * (1f + Bonus));
        public int Def => Mathf.RoundToInt(BaseDef * (1f + Bonus));
        public int MDef => Mathf.RoundToInt(BaseMDef * (1f + Bonus));
        public int MaxHp => Mathf.RoundToInt(BaseHp * (1f + Bonus));

        /// <summary>
        /// 武器類型。RM 的 weaponTypes：2 = 劍，6 = 杖。
        /// 劍走物理、杖走魔法 —— 男主的普攻跟技能都看這個。
        /// </summary>
        public int WeaponType => IsWeapon ? (Bound?.wtypeId ?? 0) : 0;
        public bool IsStaff => WeaponType == RMZWeaponTypes.Staff;
        public bool IsSword => IsWeapon && !IsStaff;

        public int SlotsLeft => Mathf.Max(0, Slots - Used);

        public EquipItemData Data => RMZDatabase.Item(ItemId);
        public EquipItemData Bound => IsWeapon ? RMZDatabase.Weapon(BoundId) : RMZDatabase.Armor(BoundId);

        public string BaseName => Data != null ? RMZLocalization.Translate(Data.name) : $"#{ItemId}";

        /// <summary>強化過就在名字後面掛 +N，跟楓之谷一樣。</summary>
        public string Name => Enhanced > 0 ? $"{BaseName} +{Enhanced}" : BaseName;
        public int IconIndex => Data?.iconIndex ?? 0;

        public RMZEquipSlot SlotKind => (RMZEquipSlot)Slot;

        /// <summary>擲得好不好，給 UI 上色用。</summary>
        public Color RollColor =>
            Roll >= 85 ? new Color(1f, 0.55f, 0.85f) :      // 極品
            Roll >= 65 ? new Color(1f, 0.85f, 0.45f) :      // 優秀
            Roll >= 35 ? Colors.White :                       // 普通
                         new Color(0.72f, 0.75f, 0.82f);     // 偏低

        public string RollName =>
            Roll >= 85 ? "極品" : Roll >= 65 ? "優秀" : Roll >= 35 ? "普通" : "粗劣";
    }

    public static class RMZEquipment
    {
        /// <summary>擲值的浮動幅度。±20% 才看得出「這把比較好」。</summary>
        public const float Variance = 0.20f;

        public static readonly List<RMZEquipInstance> Owned = new List<RMZEquipInstance>();

        /// <summary>各欄位穿著哪一件（Uid）。0 = 空。索引就是 RMZEquipSlot。</summary>
        static readonly Dictionary<int, int> Worn = new Dictionary<int, int>();

        static int _nextUid = 1;

        public static readonly string[] SlotNames = { "", "武器", "上衣", "褲子", "頭部", "鞋子" };

        // ---------------- 取得 ----------------

        /// <summary>
        /// 這件道具是不是裝備（備註欄標了 &lt;BagEquip&gt;）。
        /// 是的話它不進一般背包，改成生一件實例。
        /// </summary>
        public static bool IsEquipItem(int itemId)
        {
            var data = RMZDatabase.Item(itemId);
            return data != null && RMZInventory.HasNoteTag(data, "BagEquip");
        }

        /// <summary>拿到一件裝備。回傳新生出來的實例（數量 &gt; 1 就生好幾件）。</summary>
        public static RMZEquipInstance Gain(int itemId, int count = 1)
        {
            RMZEquipInstance last = null;
            for (int i = 0; i < Mathf.Max(1, count); i++) last = Create(itemId);
            return last;
        }

        static RMZEquipInstance Create(int itemId)
        {
            var inst = Build(itemId, -1f);
            if (inst != null) Owned.Add(inst);
            return inst;
        }

        /// <summary>
        /// 生一件不放進男主背包的裝備（女主的初始裝備用）。
        /// roll 0~1 指定體質；0.5 = 剛好平均，不浮動。
        /// </summary>
        public static RMZEquipInstance CreateLoose(int itemId, float roll = 0.5f) => Build(itemId, roll);

        /// <summary>讀檔時把女主身上的裝備編號也算進去，免得新裝備跟它撞號。</summary>
        public static void ReserveUid(int uid) => _nextUid = Mathf.Max(_nextUid, uid + 1);

        static RMZEquipInstance Build(int itemId, float fixedRoll)
        {
            var data = RMZDatabase.Item(itemId);
            if (data == null) return null;

            int weaponId = RMZInventory.NoteInt(data, "BindWeapon");
            int armorId = RMZInventory.NoteInt(data, "BindArmor");

            bool isWeapon = weaponId > 0;
            int boundId = isWeapon ? weaponId : armorId;
            var bound = isWeapon ? RMZDatabase.Weapon(boundId) : RMZDatabase.Armor(boundId);

            // 沒綁到東西就給預設欄位，至少不會整件消失
            int slot = bound != null && bound.etypeId > 0 ? bound.etypeId : (int)RMZEquipSlot.Body;
            int basePower = BaseValue(bound);

            var inst = new RMZEquipInstance
            {
                Uid = _nextUid++,
                ItemId = itemId,
                BoundId = boundId,
                IsWeapon = isWeapon,
                Slot = slot,
            };

            inst.Slots = slot == (int)RMZEquipSlot.Weapon ? 7 : 5;   // 武器可以多打兩次
            RollStats(inst, basePower, fixedRoll);
            return inst;
        }

        /// <summary>RM 只填了 params[2]，那是戰力的基準值。</summary>
        static int BaseValue(EquipItemData data)
        {
            if (data?.Params == null || data.Params.Length <= 2) return 10;
            return Mathf.Max(1, data.Params[2]);
        }

        /// <summary>
        /// 把戰力基準值攤成攻擊／防禦／血量，再各自加上一點浮動。
        /// 武器偏攻擊、上衣褲子偏血防、鞋子最輕 —— 這樣換裝才有取捨。
        ///
        /// 物攻／魔攻分家：劍幾乎全給物攻、法杖幾乎全給魔攻，
        /// 另一邊只留一點點（拿劍的法師還是打得動，只是很難看）。
        /// 防具的物防／魔防也分兩邊：上衣褲子偏物防、頭部鞋子偏魔防。
        /// </summary>
        static void RollStats(RMZEquipInstance inst, int basePower, float fixedRoll = -1f)
        {
            float atkW, defW, hpW;
            switch (inst.SlotKind)
            {
                case RMZEquipSlot.Weapon: atkW = 0.90f; defW = 0.05f; hpW = 0.6f; break;
                case RMZEquipSlot.Body:   atkW = 0.10f; defW = 0.50f; hpW = 2.2f; break;
                case RMZEquipSlot.Legs:   atkW = 0.08f; defW = 0.45f; hpW = 1.8f; break;
                case RMZEquipSlot.Head:   atkW = 0.06f; defW = 0.40f; hpW = 1.5f; break;
                default:                  atkW = 0.12f; defW = 0.30f; hpW = 1.0f; break;
            }

            // 一件裝備擲一次「體質」，三圍一起好或一起差，玩家才看得懂這把是不是好貨
            float roll = fixedRoll >= 0f ? Mathf.Clamp(fixedRoll, 0f, 1f) : GD.Randf();
            inst.Roll = Mathf.Clamp(Mathf.RoundToInt(roll * 100f), 0, 100);

            float mul = 1f + (roll - 0.5f) * 2f * Variance;
            float total = basePower * mul;

            // 攻擊：武器看類型偏一邊，防具兩邊各半
            float physShare, magicShare;
            if (inst.SlotKind == RMZEquipSlot.Weapon)
            {
                physShare = inst.IsStaff ? 0.15f : 1f;
                magicShare = inst.IsStaff ? 1f : 0.15f;
            }
            else { physShare = 0.5f; magicShare = 0.5f; }

            inst.BaseAtk = Mathf.Max(0, Mathf.RoundToInt(total * atkW * physShare));
            inst.BaseMAtk = Mathf.Max(0, Mathf.RoundToInt(total * atkW * magicShare));

            // 防禦：上衣褲子擋刀，頭部鞋子擋法術
            float physDef = inst.SlotKind == RMZEquipSlot.Body || inst.SlotKind == RMZEquipSlot.Legs
                ? 0.70f : 0.35f;
            inst.BaseDef = Mathf.Max(0, Mathf.RoundToInt(total * defW * physDef));
            inst.BaseMDef = Mathf.Max(0, Mathf.RoundToInt(total * defW * (1.05f - physDef)));

            inst.BaseHp = Mathf.Max(0, Mathf.RoundToInt(total * hpW));
        }

        // ---------------- 強化 ----------------

        /// <summary>一張強化捲軸的規格。數值照 Items.json 的道具說明來。</summary>
        public struct RMZScroll
        {
            public int ItemId;
            public bool ForWeapon;
            public float SuccessRate;   // 0~1
            public float Gain;          // 成功時加多少（基準值的比例）
            public bool IsReset;        // 回真捲軸
        }

        /// <summary>
        /// 專案裡的捲軸是 107~113（Items.json 標 &lt;BagItem1&gt;），
        /// 成功率與加成直接照道具說明寫死：
        ///   100% → +10%　50% → +20%　20% → +50%
        /// </summary>
        public static bool ScrollFor(int itemId, out RMZScroll scroll)
        {
            scroll = default;
            switch (itemId)
            {
                case 107: scroll = new RMZScroll { ItemId = itemId, ForWeapon = true,  SuccessRate = 1.00f, Gain = 0.10f }; return true;
                case 108: scroll = new RMZScroll { ItemId = itemId, ForWeapon = true,  SuccessRate = 0.50f, Gain = 0.20f }; return true;
                case 109: scroll = new RMZScroll { ItemId = itemId, ForWeapon = true,  SuccessRate = 0.20f, Gain = 0.50f }; return true;
                case 110: scroll = new RMZScroll { ItemId = itemId, ForWeapon = false, SuccessRate = 1.00f, Gain = 0.10f }; return true;
                case 111: scroll = new RMZScroll { ItemId = itemId, ForWeapon = false, SuccessRate = 0.50f, Gain = 0.20f }; return true;
                case 112: scroll = new RMZScroll { ItemId = itemId, ForWeapon = false, SuccessRate = 0.20f, Gain = 0.50f }; return true;
                case 113: scroll = new RMZScroll { ItemId = itemId, IsReset = true }; return true;
            }
            return false;
        }

        public static bool IsScroll(int itemId) => ScrollFor(itemId, out _);

        /// <summary>這張捲軸能不能用在這件裝備上。不行的話 reason 會說明原因。</summary>
        public static bool CanUse(RMZScroll scroll, RMZEquipInstance inst, out string reason)
        {
            reason = "";
            if (inst == null) { reason = "沒有選到裝備"; return false; }

            if (scroll.IsReset)
            {
                if (inst.Used <= 0 && Mathf.IsZeroApprox(inst.Bonus))
                {
                    reason = "這件還沒強化過，不需要回真";
                    return false;
                }
                return true;
            }

            if (scroll.ForWeapon && inst.SlotKind != RMZEquipSlot.Weapon) { reason = "這是武器專用的捲軸"; return false; }
            if (!scroll.ForWeapon && inst.SlotKind == RMZEquipSlot.Weapon) { reason = "這是防具專用的捲軸"; return false; }
            if (inst.SlotsLeft <= 0) { reason = "可強化次數已經用完了"; return false; }

            return true;
        }

        /// <summary>
        /// 對一件裝備用一張捲軸。回傳有沒有成功（回真一定成功），
        /// message 是要顯示給玩家看的一句話。捲軸會被消耗掉。
        /// </summary>
        public static bool Enhance(RMZScroll scroll, RMZEquipInstance inst, out string message)
        {
            if (!CanUse(scroll, inst, out message)) return false;

            RMZParty.GainItem(scroll.ItemId, -1);

            if (scroll.IsReset)
            {
                // 回真：把強化的加成與次數還回來，順便重擲一次體質
                int basePower = BaseValue(inst.Bound);
                inst.Bonus = 0f;
                inst.Used = 0;
                inst.Enhanced = 0;
                RollStats(inst, basePower);

                message = $"{inst.BaseName} 回到了原本的樣子（重新擲出 {inst.RollName}）";
                return true;
            }

            inst.Used++;

            if (GD.Randf() > scroll.SuccessRate)
            {
                message = $"強化失敗……剩下 {inst.SlotsLeft} 次";
                return false;
            }

            inst.Bonus += scroll.Gain;
            inst.Enhanced++;
            message = $"強化成功！{inst.Name}　攻 {inst.Atk}　防 {inst.Def}　血 {inst.MaxHp}";
            return true;
        }

        // ---------------- 穿脫 ----------------

        public static int WornUid(int slot) => Worn.TryGetValue(slot, out var uid) ? uid : 0;

        public static RMZEquipInstance WornAt(int slot) => Find(WornUid(slot));

        public static bool IsWorn(RMZEquipInstance inst) =>
            inst != null && WornUid(inst.Slot) == inst.Uid;

        public static RMZEquipInstance Find(int uid)
        {
            if (uid <= 0) return null;
            foreach (var e in Owned) if (e.Uid == uid) return e;
            return null;
        }

        /// <summary>穿上。同一個欄位原本穿的那件會被換下來（留在背包裡）。</summary>
        public static void Equip(RMZEquipInstance inst)
        {
            if (inst == null) return;
            Worn[inst.Slot] = inst.Uid;
            SyncToActor();
        }

        public static void Unequip(int slot)
        {
            Worn.Remove(slot);
            SyncToActor();
        }

        /// <summary>點一下就穿／脫，背包用這個。</summary>
        public static void Toggle(RMZEquipInstance inst)
        {
            if (inst == null) return;
            if (IsWorn(inst)) Unequip(inst.Slot); else Equip(inst);
        }

        /// <summary>丟掉一件（穿著的話先脫下來）。</summary>
        public static void Discard(RMZEquipInstance inst)
        {
            if (inst == null) return;
            if (IsWorn(inst)) Worn.Remove(inst.Slot);
            Owned.Remove(inst);
            SyncToActor();
        }

        /// <summary>
        /// 把穿著的裝備同步回 RM 的角色裝備欄。
        /// 事件裡有大量「條件分歧 → 裝備著某武器／防具」在判斷 UI 要畫哪張圖，
        /// 不同步的話那些判斷會全部失準。
        /// </summary>
        static void SyncToActor()
        {
            const int actorId = 1;
            for (int slot = 1; slot <= 5; slot++)
            {
                var inst = WornAt(slot);
                RMZActors.ChangeEquipById(actorId, slot, inst?.BoundId ?? 0);
            }
            RMZEventRunner.Instance?.RequestRefresh();
        }

        // ---------------- 合計 ----------------

        public static int TotalAtk => Sum(e => e.Atk);
        public static int TotalMAtk => Sum(e => e.MAtk);
        public static int TotalDef => Sum(e => e.Def);
        public static int TotalMDef => Sum(e => e.MDef);
        public static int TotalMaxHp => Sum(e => e.MaxHp);

        /// <summary>手上那把是不是法杖。沒穿武器就當成劍（赤手空拳也是用打的）。</summary>
        public static bool WieldingStaff => WornAt((int)RMZEquipSlot.Weapon)?.IsStaff ?? false;

        static int Sum(System.Func<RMZEquipInstance, int> pick)
        {
            int n = 0;
            for (int slot = 1; slot <= 5; slot++)
            {
                var inst = WornAt(slot);
                if (inst != null) n += pick(inst);
            }
            return n;
        }

        /// <summary>某個欄位現在有幾件可以換。</summary>
        public static List<RMZEquipInstance> InSlot(int slot)
        {
            var list = new List<RMZEquipInstance>();
            foreach (var e in Owned) if (e.Slot == slot) list.Add(e);
            return list;
        }

        /// <summary>背包裝備頁的排序：穿著的排最前，再照欄位、再照擲值高低。</summary>
        public static List<RMZEquipInstance> Sorted()
        {
            var list = new List<RMZEquipInstance>(Owned);
            list.Sort((a, b) =>
            {
                bool wa = IsWorn(a), wb = IsWorn(b);
                if (wa != wb) return wa ? -1 : 1;
                if (a.Slot != b.Slot) return a.Slot.CompareTo(b.Slot);
                if (a.Roll != b.Roll) return b.Roll.CompareTo(a.Roll);
                return a.Uid.CompareTo(b.Uid);
            });
            return list;
        }

        public static void Clear()
        {
            Owned.Clear();
            Worn.Clear();
            _nextUid = 1;
            RMZHeroineGear.Clear();
        }

        // ---------------- 存讀檔 ----------------

        public static void Capture(List<RMZEquipInstance> into, List<int> wornSlots, List<int> wornUids)
        {
            into.Clear(); wornSlots.Clear(); wornUids.Clear();
            into.AddRange(Owned);
            foreach (var kv in Worn) { wornSlots.Add(kv.Key); wornUids.Add(kv.Value); }
        }

        public static void Restore(List<RMZEquipInstance> owned, List<int> wornSlots, List<int> wornUids)
        {
            Clear();
            if (owned != null)
            {
                Owned.AddRange(owned);
                foreach (var e in Owned) _nextUid = Mathf.Max(_nextUid, e.Uid + 1);
            }

            if (wornSlots != null && wornUids != null)
                for (int i = 0; i < wornSlots.Count && i < wornUids.Count; i++)
                    Worn[wornSlots[i]] = wornUids[i];

            SyncToActor();
        }
    }
}
