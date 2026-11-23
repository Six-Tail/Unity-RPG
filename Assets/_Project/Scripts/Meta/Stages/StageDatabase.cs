using System.Collections.Generic;
using UnityEngine;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 스테이지 목록 + 챕터 이름. 메뉴: Create > SubcultureRPG > Stage Database.
    /// 진행 순서 = (챕터, 번호) 오름차순. 앞 스테이지를 클리어해야 다음이 열립니다.
    /// </summary>
    [CreateAssetMenu(fileName = "StageDatabase", menuName = "SubcultureRPG/Stage Database")]
    public class StageDatabase : ScriptableObject
    {
        [Tooltip("챕터 이름(인덱스 0 = 챕터 1).")]
        public List<string> chapterNames = new();
        public List<StageData> stages = new();

        [Header("난이도 개방 (쉬움 기준 챕터 클리어)")]
        [Min(1)] public int hardUnlockChapter = 7;
        [Min(1)] public int hellUnlockChapter = 15;

        /// <summary>이 난이도를 여는 쉬움 챕터(쉬움은 0 = 처음부터).</summary>
        public int UnlockChapterOf(StageDifficulty d) =>
            d == StageDifficulty.Hard ? hardUnlockChapter : d == StageDifficulty.Hell ? hellUnlockChapter : 0;
        [Tooltip("돌파 던전(속성 × 등급). 챕터 진행 순서와 무관.")]
        public List<StageData> dungeons = new();

        /// <summary>돌파 던전 목록(속성 → 등급 순).</summary>
        public List<StageData> Dungeons()
        {
            var list = new List<StageData>();
            if (dungeons != null) foreach (var s in dungeons) if (s != null) list.Add(s);
            list.Sort((a, b) => a.kind != b.kind ? a.kind.CompareTo(b.kind)
                : a.IsAscensionDungeon && a.dungeonElement != b.dungeonElement ? a.dungeonElement.CompareTo(b.dungeonElement)
                : a.chapter != b.chapter ? a.chapter.CompareTo(b.chapter)   // 장비 던전: 세트 묶음 순
                : a.dungeonTier.CompareTo(b.dungeonTier));
            return list;
        }

        /// <summary>특정 종류의 특수 던전.</summary>
        public List<StageData> Dungeons(StageKind kind) => Dungeons().FindAll(d => d.kind == kind);

        /// <summary>이 스테이지를 클리어하면 열리는 던전들.</summary>
        public List<StageData> DungeonsUnlockedBy(StageData stage) =>
            Dungeons().FindAll(d => d.unlockAfter == stage);

        /// <summary>진행 순서대로 정렬된 메인 스테이지(난이도별).</summary>
        public List<StageData> Ordered(StageDifficulty difficulty = StageDifficulty.Easy)
        {
            var list = new List<StageData>();
            if (stages != null) foreach (var s in stages) if (s != null && s.difficulty == difficulty) list.Add(s);
            list.Sort((a, b) => a.chapter != b.chapter ? a.chapter.CompareTo(b.chapter) : a.number.CompareTo(b.number));
            return list;
        }

        /// <summary>존재하는 챕터 번호(오름차순).</summary>
        public List<int> Chapters(StageDifficulty difficulty = StageDifficulty.Easy)
        {
            var set = new SortedSet<int>();
            foreach (var s in Ordered(difficulty)) set.Add(s.chapter);
            return new List<int>(set);
        }

        public List<StageData> InChapter(int chapter, StageDifficulty difficulty = StageDifficulty.Easy) =>
            Ordered(difficulty).FindAll(s => s.chapter == chapter);

        /// <summary>진행 순서상 바로 앞 스테이지(첫 스테이지면 null).</summary>
        public StageData Previous(StageData stage)
        {
            if (stage == null) return null;
            var list = Ordered(stage.difficulty);
            int i = list.IndexOf(stage);
            return i > 0 ? list[i - 1] : null;
        }

        /// <summary>진행 순서상 다음 스테이지(마지막이면 null).</summary>
        public StageData Next(StageData stage)
        {
            if (stage == null) return null;
            var list = Ordered(stage.difficulty);
            int i = list.IndexOf(stage);
            return i >= 0 && i + 1 < list.Count ? list[i + 1] : null;
        }

        public string ChapterName(int chapter) =>
            chapterNames != null && chapter >= 1 && chapter <= chapterNames.Count && !string.IsNullOrEmpty(chapterNames[chapter - 1])
                ? chapterNames[chapter - 1]
                : $"챕터 {chapter}";
    }
}
