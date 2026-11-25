using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubcultureRPG.Meta.Missions
{
    /// <summary>업적이 세는 값.</summary>
    public enum AchievementStat
    {
        // 누적(임무 집계와 같은 신호로 셈 — PlayerMissions.Counted)
        BattleWin, DungeonClear, GachaPull, Growth, ShopBuy, DailyMissions,
        // 현재 상태(볼 때마다 계산)
        Owned, Stars, Chapters, CommanderLevel, BondMax, Level80,
    }

    /// <summary>업적 1개 — 단계(targets)마다 보상, 마지막 단계에 칭호(있으면).</summary>
    public sealed class Achievement
    {
        public string id, name, format;   // format: "전투 {0}회 승리"
        public AchievementStat stat;
        public int[] targets;
        public string titleId;            // 마지막 단계를 받으면 쓸 수 있는 칭호(ProfileRewards.Titles), 없으면 null

        public int Tiers => targets.Length;
        public string Goal(int tier) => string.Format(format, targets[Mathf.Clamp(tier, 0, Tiers - 1)].ToString("N0"));
    }

    /// <summary>
    /// 업적: 계정 누적 기록(초기화 없음). 12종 × 3 ~ 6단계. 단계마다 크리스탈, 마지막 단계는 상시 뽑기권 + 칭호.
    /// 임무 화면의 '업적' 탭에서 받습니다. 씬에 없어도 처음 쓸 때 스스로 만들어짐(PlayerAchievements.Instance).
    /// 문서: docs/achievements.md
    /// </summary>
    public class PlayerAchievements : MonoBehaviour
    {
        private const string SaveKey = "achievements";

        public static readonly List<Achievement> All = new()
        {
            A("a_battle", "전투의 달인", AchievementStat.BattleWin, "전투 {0}회 승리", "t_ach_battle", 10, 50, 200, 500, 1000),
            A("a_dungeon", "던전 탐험가", AchievementStat.DungeonClear, "특수 던전 {0}회 클리어", "t_ach_dungeon", 10, 50, 150, 400),
            A("a_gacha", "소환의 인도자", AchievementStat.GachaPull, "뽑기 {0}회", "t_ach_gacha", 10, 50, 100, 300, 600),
            A("a_growth", "육성 전문가", AchievementStat.Growth, "캐릭터 육성 {0}회 (레벨업 · 돌파 · 스킬)", "t_ach_growth", 10, 50, 200, 500),
            A("a_shop", "단골 손님", AchievementStat.ShopBuy, "상점 구매 {0}회", "t_ach_shop", 5, 30, 100),
            A("a_daily", "성실한 지휘관", AchievementStat.DailyMissions, "일일 임무 {0}개 완료", "t_ach_daily", 10, 50, 150, 365),
            A("a_owned", "수집의 길", AchievementStat.Owned, "캐릭터 {0}명 보유", null, 5, 10, 15, 20, 25),
            A("a_stars", "별 사냥꾼", AchievementStat.Stars, "별 {0}개 획득 (모든 난이도)", "t_ach_stars", 30, 90, 180, 360, 540, 810),
            A("a_story", "이야기의 끝", AchievementStat.Chapters, "메인 스토리 {0}장 클리어 (쉬움)", null, 3, 6, 9, 12, 15),
            A("a_level", "지휘관의 길", AchievementStat.CommanderLevel, "지휘관 Lv.{0}", "t_ach_level", 10, 20, 30, 50, 70, 100),
            A("a_bond", "깊은 인연", AchievementStat.BondMax, "호감도 Lv.10 캐릭터 {0}명", "t_ach_bond", 1, 3, 5, 10),
            A("a_lv80", "정점의 부대", AchievementStat.Level80, "Lv.80 캐릭터 {0}명", "t_ach_lv80", 1, 3, 6, 12),
        };

        private static Achievement A(string id, string name, AchievementStat stat, string format, string title, params int[] targets) =>
            new() { id = id, name = name, stat = stat, format = format, titleId = title, targets = targets };

        public static Achievement Find(string id) => All.Find(a => a.id == id);

        /// <summary>단계별 보상(0부터). 크리스탈 20 · 40 · 60 · 80 · 120 · 160, 마지막 단계는 상시 뽑기권 1장 추가.</summary>
        public static RewardBundle RewardOf(Achievement a, int tier)
        {
            int[] crystals = { 20, 40, 60, 80, 120, 160 };
            var r = new RewardBundle().Crystals(crystals[Mathf.Clamp(tier, 0, crystals.Length - 1)]);
            if (tier == a.Tiers - 1) r.Standard(1);
            return r;
        }

        // ---------------- 저장 ----------------

        [Serializable] private class Counter { public int stat; public long value; }
        [Serializable] private class Claim { public string id; public int tiers; }
        [Serializable] private class SaveData { public List<Counter> counters = new(); public List<Claim> claims = new(); }

        private SaveData _d = new();
        private bool _loaded;
        public event Action OnChanged;

        private static PlayerAchievements _instance;
        private static bool _booted;

        public static PlayerAchievements Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<PlayerAchievements>();
                if (_instance == null) _instance = new GameObject("PlayerAchievements").AddComponent<PlayerAchievements>();
                _instance.EnsureLoaded();
                return _instance;
            }
        }

        /// <summary>게임 시작 시 임무 집계 신호를 받기 시작(업적 화면을 열기 전의 기록도 셈).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_booted) return;
            _booted = true;
            PlayerMissions.Counted += (goal, amount) => Instance.OnCounted(goal, amount);
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
            EnsureLoaded();
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            if (!SaveStore.HasKey(SaveKey)) return;
            try { _d = JsonUtility.FromJson<SaveData>(SaveStore.GetString(SaveKey)) ?? new SaveData(); }
            catch { _d = new SaveData(); }
            _d.counters ??= new List<Counter>();
            _d.claims ??= new List<Claim>();
        }

        private void Save()
        {
            SaveStore.SetString(SaveKey, JsonUtility.ToJson(_d));
            SaveStore.Save();
        }

        private void Changed() { Save(); OnChanged?.Invoke(); }

        // ---------------- 집계 ----------------

        private void OnCounted(MissionGoal goal, int amount)
        {
            AchievementStat? stat = goal switch
            {
                MissionGoal.BattleWin => AchievementStat.BattleWin,
                MissionGoal.DungeonClear => AchievementStat.DungeonClear,
                MissionGoal.GachaPull => AchievementStat.GachaPull,
                MissionGoal.Growth => AchievementStat.Growth,
                MissionGoal.ShopBuy => AchievementStat.ShopBuy,
                MissionGoal.DailyClaim => AchievementStat.DailyMissions,
                _ => null,
            };
            if (stat == null || amount <= 0) return;
            var c = _d.counters.Find(x => x.stat == (int)stat.Value);
            if (c == null) { c = new Counter { stat = (int)stat.Value }; _d.counters.Add(c); }
            c.value += amount;
            Changed();
        }

        /// <summary>지금 값(누적 기록 또는 현재 상태).</summary>
        public long Value(AchievementStat stat)
        {
            switch (stat)
            {
                case AchievementStat.Owned:
                {
                    var roster = FindAnyObjectByType<PlayerRoster>();
                    return roster != null ? roster.Owned.Count : 0;
                }
                case AchievementStat.Stars:
                {
                    var progress = FindAnyObjectByType<StageProgress>();
                    return progress != null ? progress.AllStars : 0;
                }
                case AchievementStat.Chapters:
                    return ProfileContext.Capture().chaptersCleared;
                case AchievementStat.CommanderLevel:
                    return PlayerProfile.Instance.Level;
                case AchievementStat.BondMax:
                case AchievementStat.Level80:
                {
                    var roster = FindAnyObjectByType<PlayerRoster>();
                    if (roster == null || roster.Database == null) return 0;
                    int n = 0;
                    foreach (var ch in roster.Database.characters)
                    {
                        if (ch == null || !roster.IsOwned(ch)) continue;
                        if (stat == AchievementStat.BondMax ? PlayerBond.Instance.Level(ch) >= PlayerBond.MaxLevel : roster.LevelOf(ch) >= 80) n++;
                    }
                    return n;
                }
                default:
                    return _d.counters.Find(x => x.stat == (int)stat)?.value ?? 0;
            }
        }

        // ---------------- 단계 · 수령 ----------------

        /// <summary>달성한 단계 수(0 ~ Tiers).</summary>
        public int Reached(Achievement a)
        {
            long v = Value(a.stat);
            int n = 0;
            while (n < a.Tiers && v >= a.targets[n]) n++;
            return n;
        }

        /// <summary>보상을 받은 단계 수.</summary>
        public int Claimed(Achievement a) => _d.claims.Find(x => x.id == a.id)?.tiers ?? 0;

        public bool CanClaim(Achievement a) => Claimed(a) < Reached(a);
        public bool IsComplete(Achievement a) => Claimed(a) >= a.Tiers;

        /// <summary>마지막 단계 보상까지 받은 업적인가(칭호 해금 판정).</summary>
        public bool IsComplete(string id) { var a = Find(id); return a != null && IsComplete(a); }

        public int ClaimableCount()
        {
            int n = 0;
            foreach (var a in All) if (CanClaim(a)) n++;
            return n;
        }

        public int ClaimedTiers() { int n = 0; foreach (var a in All) n += Claimed(a); return n; }
        public static int TotalTiers() { int n = 0; foreach (var a in All) n += a.Tiers; return n; }

        /// <summary>이 업적에서 받을 수 있는 단계를 모두 받음(합친 보상, 없으면 null).</summary>
        public RewardBundle ClaimOne(Achievement a)
        {
            var total = Collect(a);
            if (total == null) return null;
            total.Grant();
            Changed();
            return total;
        }

        /// <summary>모든 업적에서 받을 수 있는 단계를 모두 받음.</summary>
        public RewardBundle ClaimAll()
        {
            RewardBundle total = null;
            foreach (var a in All)
            {
                var r = Collect(a);
                if (r == null) continue;
                total ??= new RewardBundle();
                Merge(total, r);
            }
            if (total == null) return null;
            total.Grant();
            Changed();
            return total;
        }

        private RewardBundle Collect(Achievement a)
        {
            int from = Claimed(a), to = Reached(a);
            if (to <= from) return null;
            var total = new RewardBundle();
            for (int t = from; t < to; t++) Merge(total, RewardOf(a, t));
            var c = _d.claims.Find(x => x.id == a.id);
            if (c == null) { c = new Claim { id = a.id }; _d.claims.Add(c); }
            c.tiers = to;
            return total;
        }

        private static void Merge(RewardBundle into, RewardBundle r)
        {
            into.crystals += r.crystals; into.gold += r.gold; into.pickup += r.pickup; into.standard += r.standard;
            if (r.mats != null) foreach (var m in r.mats) into.Mat(m.Type, m.count);
        }

        // ---------------- 테스트 ----------------

        [ContextMenu("테스트: 누적 기록 +100")]
        private void TestAdd()
        {
            foreach (AchievementStat s in Enum.GetValues(typeof(AchievementStat)))
            {
                if (s >= AchievementStat.Owned) continue;
                var c = _d.counters.Find(x => x.stat == (int)s);
                if (c == null) { c = new Counter { stat = (int)s }; _d.counters.Add(c); }
                c.value += 100;
            }
            Changed();
        }

        [ContextMenu("테스트: 업적 초기화")]
        private void TestReset() { _d = new SaveData(); Changed(); }
    }
}
