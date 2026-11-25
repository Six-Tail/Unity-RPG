using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Battle;
using SubcultureRPG.Characters;
using SubcultureRPG.Meta.Shop;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 호감도: 캐릭터마다 Lv.1 ~ 10. 단계마다 체력 · 공격 · 방어 +1%(전투 · 전투력에 반영), 단계 보상, 개인 스토리(Lv.3 · 6 · 9).
    /// 올리는 법: 전투 승리(출전 캐릭터 +10) · 홈 대표 캐릭터 터치(+10, 하루 3번) · 선물(가방의 선물 재료).
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(PlayerBond.Instance). 문서: docs/bond.md
    /// </summary>
    public class PlayerBond : MonoBehaviour
    {
        public const int MaxLevel = 10;
        public const int VictoryExp = 10, TouchExp = 10, TouchPerDay = 3;
        public const float StatPerLevel = 0.01f;            // Lv.2 부터 단계마다 체력 · 공격 · 방어 +1% (Lv.10 = +9%)
        public const int StoryRewardCrystals = 20;          // 개인 스토리를 처음 볼 때
        public static readonly int[] StoryLevels = { 3, 6, 9 };   // 개인 스토리 1 · 2 · 3화가 열리는 단계

        // Need[lv] = Lv.lv → lv+1 에 필요한 호감도 (합계 3,200)
        private static readonly int[] Need = { 0, 100, 150, 200, 250, 300, 400, 500, 600, 700 };

        private const string SaveKey = "bond";

        [Serializable]
        private class Entry
        {
            public string id;
            public int exp;          // 누적 호감도
            public int claimed = 1;  // 단계 보상을 받은 마지막 단계(1 = 아직 없음)
        }

        [Serializable]
        private class SaveData
        {
            public List<Entry> list = new();
            public string touchDay = "";
            public int touches;
        }

        private SaveData _d = new();
        private bool _loaded;
        public event Action OnChanged;

        private static PlayerBond _instance;
        public static PlayerBond Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<PlayerBond>();
                if (_instance == null) _instance = new GameObject("PlayerBond").AddComponent<PlayerBond>();
                _instance.EnsureLoaded();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
            EnsureLoaded();
        }

        private static string Today => PlayerShop.PeriodKey(ShopReset.Daily, DateTime.Now);

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            if (!SaveStore.HasKey(SaveKey)) return;
            try { _d = JsonUtility.FromJson<SaveData>(SaveStore.GetString(SaveKey)) ?? new SaveData(); }
            catch { _d = new SaveData(); }
            _d.list ??= new List<Entry>();
        }

        private void Save()
        {
            SaveStore.SetString(SaveKey, JsonUtility.ToJson(_d));
            SaveStore.Save();
        }

        private void Changed() { Save(); OnChanged?.Invoke(); }

        private static string KeyOf(CharacterData c) => c == null ? null : !string.IsNullOrEmpty(c.id) ? c.id : c.name;

        private Entry Find(CharacterData c, bool create)
        {
            string key = KeyOf(c);
            if (key == null) return null;
            var e = _d.list.Find(x => x.id == key);
            if (e == null && create) { e = new Entry { id = key }; _d.list.Add(e); }
            return e;
        }

        // ---------------- 단계 계산 ----------------

        /// <summary>Lv.lv 이 시작되는 누적 호감도.</summary>
        public static int ExpAtLevel(int lv)
        {
            int sum = 0;
            for (int i = 1; i < Mathf.Clamp(lv, 1, MaxLevel); i++) sum += Need[i];
            return sum;
        }

        public static int MaxExp => ExpAtLevel(MaxLevel);

        public static int LevelFromExp(int exp)
        {
            int lv = 1;
            while (lv < MaxLevel && exp >= ExpAtLevel(lv + 1)) lv++;
            return lv;
        }

        public int Exp(CharacterData c) => Find(c, false)?.exp ?? 0;
        public int Level(CharacterData c) => LevelFromExp(Exp(c));

        /// <summary>이번 단계 안에서의 진행(현재 · 필요). 최대 단계면 (0, 0).</summary>
        public (int cur, int need) Progress(CharacterData c)
        {
            int lv = Level(c);
            if (lv >= MaxLevel) return (0, 0);
            return (Exp(c) - ExpAtLevel(lv), Need[lv]);
        }

        /// <summary>호감도 단계 능력치 보너스(체력 · 공격 · 방어 %, 0.05 = 5%).</summary>
        public float StatBonusPct(CharacterData c) => (Level(c) - 1) * StatPerLevel;

        /// <summary>전투 · 전투력 계산에 쓰는 보너스 = 장비 + 호감도.</summary>
        public static StatBonus BonusFor(CharacterData c, PlayerEquipment equipment)
        {
            var b = new StatBonus();
            if (equipment != null) b.Add(equipment.BonusFor(c));
            float p = Instance.StatBonusPct(c);
            b.hpPct += p; b.atkPct += p; b.defPct += p;
            return b;
        }

        // ---------------- 올리기 ----------------

        /// <summary>호감도 추가(최대치에서 멈춤). 실제로 오른 양을 돌려줍니다.</summary>
        public int Add(CharacterData c, int amount, bool notify = true)
        {
            if (c == null || amount <= 0) return 0;
            var e = Find(c, true);
            int before = e.exp;
            e.exp = Mathf.Min(MaxExp, e.exp + amount);
            if (notify) Changed();
            return e.exp - before;
        }

        /// <summary>전투 승리: 출전한 캐릭터마다 +10.</summary>
        public void OnVictory(IEnumerable<CharacterData> party)
        {
            if (party == null) return;
            bool any = false;
            foreach (var c in party) if (c != null && Add(c, VictoryExp, false) > 0) any = true;
            if (any) Changed();
        }

        /// <summary>오늘 남은 터치 횟수(홈 대표 캐릭터, 모든 캐릭터 공용).</summary>
        public int TouchesLeft => _d.touchDay == Today ? Mathf.Max(0, TouchPerDay - _d.touches) : TouchPerDay;

        /// <summary>홈에서 대표 캐릭터를 눌렀을 때. 오른 호감도(오늘 횟수를 다 썼거나 최대면 0).</summary>
        public int Touch(CharacterData c)
        {
            if (c == null || TouchesLeft <= 0 || Level(c) >= MaxLevel) return 0;
            if (_d.touchDay != Today) { _d.touchDay = Today; _d.touches = 0; }
            _d.touches++;
            int got = Add(c, TouchExp, false);
            Changed();
            return got;
        }

        /// <summary>선물 count개를 쓸 때 실제로 쓰게 될 개수(최대 단계를 넘는 만큼은 쓰지 않음).</summary>
        public int GiftUsable(CharacterData c, MaterialType gift, int count, PlayerInventory inv)
        {
            int per = Materials.GiftExp(gift);
            if (per <= 0 || inv == null) return 0;
            int room = MaxExp - Exp(c);
            if (room <= 0) return 0;
            int needed = Mathf.CeilToInt(room / (float)per);
            return Mathf.Min(count, inv.Count(gift), needed);
        }

        /// <summary>선물하기. 오른 호감도(못 줬으면 0).</summary>
        public int Gift(CharacterData c, MaterialType gift, int count, PlayerInventory inv)
        {
            int n = GiftUsable(c, gift, count, inv);
            if (n <= 0 || !inv.Spend(gift, n)) return 0;
            return Add(c, Materials.GiftExp(gift) * n);
        }

        // ---------------- 단계 보상 ----------------

        /// <summary>Lv.lv 에 도달하면 받는 보상(Lv.2 ~ 10).</summary>
        public static RewardBundle LevelReward(int lv)
        {
            var r = new RewardBundle();
            return lv switch
            {
                2 => r.Gold(10000),
                3 => r.Crystals(30),
                4 => r.Mat(MaterialType.ExpPotion, 5),
                5 => r.Crystals(50),
                6 => r.Mat(MaterialType.SkillBookPurple, 2),
                7 => r.Gold(30000),
                8 => r.Crystals(80),
                9 => r.Mat(MaterialType.SkillLegend, 1),
                10 => r.Crystals(100).Standard(1),
                _ => r,
            };
        }

        /// <summary>단계 보상을 받은 마지막 단계(1 = 아직 없음).</summary>
        public int Claimed(CharacterData c) => Mathf.Max(1, Find(c, false)?.claimed ?? 1);

        public int ClaimableCount(CharacterData c) => Mathf.Max(0, Level(c) - Claimed(c));

        /// <summary>받을 수 있는 단계 보상을 모두 받음(합친 보상, 없으면 null).</summary>
        public RewardBundle ClaimAll(CharacterData c)
        {
            int lv = Level(c), from = Claimed(c);
            if (lv <= from) return null;
            var total = new RewardBundle();
            for (int l = from + 1; l <= lv; l++)
            {
                var r = LevelReward(l);
                total.crystals += r.crystals; total.gold += r.gold; total.pickup += r.pickup; total.standard += r.standard;
                if (r.mats != null) foreach (var m in r.mats) total.Mat(m.Type, m.count);
            }
            Find(c, true).claimed = lv;
            total.Grant();
            Changed();
            return total;
        }

        // ---------------- 개인 스토리 ----------------

        public static StoryScript Story(CharacterData c, int episode) => StoryLibrary.Bond(KeyOf(c), episode);

        public bool StoryUnlocked(CharacterData c, int episode) =>
            episode >= 1 && episode <= StoryLevels.Length && Level(c) >= StoryLevels[episode - 1];

        /// <summary>열렸지만 아직 안 본 개인 스토리 수.</summary>
        public int NewStoryCount(CharacterData c)
        {
            int n = 0;
            for (int ep = 1; ep <= StoryLevels.Length; ep++)
            {
                var s = Story(c, ep);
                if (s != null && StoryUnlocked(c, ep) && !StoryLibrary.Seen(s)) n++;
            }
            return n;
        }

        /// <summary>개인 스토리를 다 봤을 때. 처음이면 보상(크리스탈)을 주고 돌려줌, 아니면 null.</summary>
        public RewardBundle FinishStory(CharacterData c, int episode)
        {
            var s = Story(c, episode);
            if (s == null || StoryLibrary.Seen(s)) return null;
            StoryLibrary.MarkSeen(s);
            var r = new RewardBundle().Crystals(StoryRewardCrystals);
            r.Grant();
            Changed();
            return r;
        }

        // ---------------- 알림 ----------------

        /// <summary>이 캐릭터에 받을 단계 보상이나 새 개인 스토리가 있는가.</summary>
        public bool HasNotice(CharacterData c) => c != null && (ClaimableCount(c) > 0 || NewStoryCount(c) > 0);

        /// <summary>보유 캐릭터 중 알림이 있는 수.</summary>
        public int NoticeCount(PlayerRoster roster)
        {
            if (roster == null || roster.Database == null) return 0;
            int n = 0;
            foreach (var c in roster.Database.characters)
                if (c != null && roster.IsOwned(c) && HasNotice(c)) n++;
            return n;
        }

        // ---------------- 테스트 ----------------

        [ContextMenu("테스트: 보유 캐릭터 호감도 +500")]
        private void TestAdd()
        {
            var roster = FindAnyObjectByType<PlayerRoster>();
            if (roster == null || roster.Database == null) return;
            foreach (var c in roster.Database.characters) if (c != null && roster.IsOwned(c)) Add(c, 500, false);
            Changed();
        }

        [ContextMenu("테스트: 호감도 초기화")]
        private void TestReset()
        {
            _d = new SaveData();
            Changed();
        }
    }
}
