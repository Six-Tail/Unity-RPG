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
        [Tooltip("돌파 던전(속성 × 등급). 챕터 진행 순서와 무관.")]
        public List<StageData> dungeons = new();

        /// <summary>돌파 던전 목록(속성 → 등급 순).</summary>
        public List<StageData> Dungeons()
        {
            var list = new List<StageData>();
            if (dungeons != null) foreach (var s in dungeons) if (s != null) list.Add(s);
            list.Sort((a, b) => a.dungeonElement != b.dungeonElement
                ? a.dungeonElement.CompareTo(b.dungeonElement) : a.dungeonTier.CompareTo(b.dungeonTier));
            return list;
        }

        /// <summary>이 스테이지를 클리어하면 열리는 던전들.</summary>
        public List<StageData> DungeonsUnlockedBy(StageData stage) =>
            Dungeons().FindAll(d => d.unlockAfter == stage);

        /// <summary>진행 순서대로 정렬된 스테이지.</summary>
        public List<StageData> Ordered()
        {
            var list = new List<StageData>();
            if (stages != null) foreach (var s in stages) if (s != null) list.Add(s);
            list.Sort((a, b) => a.chapter != b.chapter ? a.chapter.CompareTo(b.chapter) : a.number.CompareTo(b.number));
            return list;
        }

        /// <summary>존재하는 챕터 번호(오름차순).</summary>
        public List<int> Chapters()
        {
            var set = new SortedSet<int>();
            foreach (var s in Ordered()) set.Add(s.chapter);
            return new List<int>(set);
        }

        public List<StageData> InChapter(int chapter) => Ordered().FindAll(s => s.chapter == chapter);

        /// <summary>진행 순서상 바로 앞 스테이지(첫 스테이지면 null).</summary>
        public StageData Previous(StageData stage)
        {
            var list = Ordered();
            int i = list.IndexOf(stage);
            return i > 0 ? list[i - 1] : null;
        }

        /// <summary>진행 순서상 다음 스테이지(마지막이면 null).</summary>
        public StageData Next(StageData stage)
        {
            var list = Ordered();
            int i = list.IndexOf(stage);
            return i >= 0 && i + 1 < list.Count ? list[i + 1] : null;
        }

        public string ChapterName(int chapter) =>
            chapterNames != null && chapter >= 1 && chapter <= chapterNames.Count && !string.IsNullOrEmpty(chapterNames[chapter - 1])
                ? chapterNames[chapter - 1]
                : $"챕터 {chapter}";
    }
}
