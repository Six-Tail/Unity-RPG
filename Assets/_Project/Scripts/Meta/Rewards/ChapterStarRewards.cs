using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 챕터 별 보상: 한 챕터에서 모은 별이 12 · 15 · 18개가 되면 단계별 보상을 한 번씩 받습니다(난이도마다 따로).
    /// 세이브: PlayerPrefs JSON("chapter_stars") — 받은 단계 목록("난이도_챕터_단계").
    /// </summary>
    public static class ChapterStarRewards
    {
        public static readonly int[] Thresholds = { 12, 15, 18 };
        private const string SaveKey = "chapter_stars";

        [Serializable] private class SaveData { public List<string> claimed = new(); }

        private static HashSet<string> _claimed;
        public static event Action OnChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _claimed = null; OnChanged = null; }

        private static HashSet<string> Claimed
        {
            get
            {
                if (_claimed != null) return _claimed;
                _claimed = new HashSet<string>();
                if (PlayerPrefs.HasKey(SaveKey))
                {
                    try
                    {
                        var d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
                        if (d?.claimed != null) foreach (var k in d.claimed) _claimed.Add(k);
                    }
                    catch { }
                }
                return _claimed;
            }
        }

        private static void Save()
        {
            var d = new SaveData();
            d.claimed.AddRange(Claimed);
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(d));
            PlayerPrefs.Save();
        }

        private static string Key(StageDifficulty d, int chapter, int tier) => $"{(int)d}_{chapter}_{tier}";

        /// <summary>
        /// 단계 보상(tier = 0 ~ 2). 튜닝 지점.
        /// 12개: 크리스탈 30 + 금화 · 15개: 크리스탈 50 + 경험치 재료 · 18개: 크리스탈 100 + 상시 뽑기권 1장. 하드는 크리스탈 1.5배, 극악은 2배.
        /// </summary>
        public static RewardBundle Reward(StageDifficulty d, int chapter, int tier)
        {
            float mul = d == StageDifficulty.Easy ? 1f : (int)d == 1 ? 1.5f : 2f;
            int Cr(int n) => Mathf.RoundToInt(n * mul);
            var exp = chapter <= 5 ? MaterialType.ExpPotion : MaterialType.ExpLarge;
            return tier switch
            {
                0 => new RewardBundle().Crystals(Cr(30)).Gold(5000 + 2000 * chapter),
                1 => new RewardBundle().Crystals(Cr(50)).Mat(exp, 5),
                _ => new RewardBundle().Crystals(Cr(100)).Standard(1),
            };
        }

        public static int Stars(StageDatabase db, StageProgress progress, StageDifficulty d, int chapter) =>
            db != null && progress != null ? progress.TotalStars(db, chapter, d) : 0;

        public static bool IsClaimed(StageDifficulty d, int chapter, int tier) => Claimed.Contains(Key(d, chapter, tier));

        public static bool CanClaim(StageDatabase db, StageProgress progress, StageDifficulty d, int chapter, int tier) =>
            tier >= 0 && tier < Thresholds.Length && !IsClaimed(d, chapter, tier) && Stars(db, progress, d, chapter) >= Thresholds[tier];

        /// <summary>받기(받은 보상, 못 받으면 null).</summary>
        public static RewardBundle Claim(StageDatabase db, StageProgress progress, StageDifficulty d, int chapter, int tier)
        {
            if (!CanClaim(db, progress, d, chapter, tier)) return null;
            Claimed.Add(Key(d, chapter, tier));
            Save();
            var r = Reward(d, chapter, tier);
            r.Grant();
            OnChanged?.Invoke();
            return r;
        }

        /// <summary>그 챕터에서 지금 받을 수 있는 단계 수.</summary>
        public static int ClaimableIn(StageDatabase db, StageProgress progress, StageDifficulty d, int chapter)
        {
            int stars = Stars(db, progress, d, chapter), n = 0;
            for (int t = 0; t < Thresholds.Length; t++)
                if (stars >= Thresholds[t] && !IsClaimed(d, chapter, t)) n++;
            return n;
        }

        /// <summary>모든 난이도 · 챕터에서 지금 받을 수 있는 단계 수(홈 알림용).</summary>
        public static int ClaimableTotal(StageDatabase db, StageProgress progress)
        {
            if (db == null || progress == null) return 0;
            int n = 0;
            foreach (var d in Difficulties.All)
                foreach (int ch in db.Chapters(d)) n += ClaimableIn(db, progress, d, ch);
            return n;
        }
    }

    /// <summary>
    /// 기능 개방 조건: 결투장 · 주간 보스는 3장(쉬움)을 클리어해야 열립니다.
    /// 던전 개방(1장 = 경험치 · 금화 · 스킬 교본, 2장 = 장비, 3장 = 돌파)은 스테이지 데이터(unlockAfter)가 정합니다.
    /// </summary>
    public static class FeatureGate
    {
        public const int ArenaChapter = 3, RaidChapter = 3, EventChapter = 2;

        /// <summary>쉬움 난이도에서 1장부터 이어서 클리어한 마지막 장(없으면 0).</summary>
        public static int ClearedChapters()
        {
            var progress = UnityEngine.Object.FindAnyObjectByType<StageProgress>();
            var screen = UnityEngine.Object.FindAnyObjectByType<SubcultureRPG.Meta.UI.StageScreen>();
            var db = screen != null ? screen.Database : null;
            if (progress == null || db == null) return 99;   // 스테이지가 없는 씬(데모)에서는 잠그지 않음
            int cleared = 0;
            foreach (int ch in db.Chapters(StageDifficulty.Easy))
            {
                if (!progress.IsChapterCleared(db, ch)) break;
                cleared = ch;
            }
            return cleared;
        }

        public static bool ArenaOpen => ClearedChapters() >= ArenaChapter;
        public static bool RaidOpen => ClearedChapters() >= RaidChapter;
        public static bool EventOpen => ClearedChapters() >= EventChapter;
        public static string LockText(int chapter) => $"{chapter}장 클리어 시 개방";
    }
}
