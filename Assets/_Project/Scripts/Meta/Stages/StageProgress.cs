using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 스테이지 진행도(최고 별, 클리어 횟수) 저장 — PlayerPrefs JSON("stages").
    /// 해금 규칙: 진행 순서상 앞 스테이지를 클리어하면 다음 스테이지가 열림(첫 스테이지는 항상 열림).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Stage Progress")]
    public class StageProgress : MonoBehaviour
    {
        [SerializeField] private string saveKey = "stages";

        [Serializable]
        private class Record { public string id; public int stars; public int clears; }
        [Serializable]
        private class SaveData { public List<Record> records = new(); }

        private readonly Dictionary<string, Record> _records = new();

        public event Action OnChanged;

        private void Awake() => Load();

        public bool IsCleared(StageData s) => s != null && _records.TryGetValue(s.Key, out var r) && r.clears > 0;

        /// <summary>모든 스테이지(전 난이도 · 특수 던전 포함)에서 얻은 별 합계.</summary>
        public int AllStars
        {
            get { int n = 0; foreach (var r in _records.Values) n += r.stars; return n; }
        }
        public int StarsOf(StageData s) => s != null && _records.TryGetValue(s.Key, out var r) ? r.stars : 0;
        public int ClearsOf(StageData s) => s != null && _records.TryGetValue(s.Key, out var r) ? r.clears : 0;

        public bool IsUnlocked(StageData s, StageDatabase db)
        {
            if (s == null) return false;
            if (s.IsDungeon) return s.unlockAfter == null || IsCleared(s.unlockAfter);
            if (!IsDifficultyUnlocked(db, s.difficulty)) return false;
            var prev = db != null ? db.Previous(s) : null;
            return prev == null || IsCleared(prev);
        }

        /// <summary>난이도 개방 여부(쉬움은 항상, 하드/극악은 쉬움 해당 챕터 클리어).</summary>
        public bool IsDifficultyUnlocked(StageDatabase db, StageDifficulty d)
        {
            if (d == StageDifficulty.Easy || db == null) return true;
            return IsChapterCleared(db, db.UnlockChapterOf(d), StageDifficulty.Easy);
        }

        /// <summary>챕터 클리어 = 그 챕터의 마지막 스테이지(보스)를 클리어.</summary>
        public bool IsChapterCleared(StageDatabase db, int chapter, StageDifficulty d = StageDifficulty.Easy)
        {
            if (db == null) return false;
            var list = db.InChapter(chapter, d);
            return list.Count > 0 && IsCleared(list[list.Count - 1]);
        }

        /// <summary>획득한 별 합계(전체 또는 챕터).</summary>
        public int TotalStars(StageDatabase db, int chapter = 0, StageDifficulty d = StageDifficulty.Easy)
        {
            int sum = 0;
            if (db == null) return 0;
            foreach (var s in db.Ordered(d))
                if (chapter == 0 || s.chapter == chapter) sum += StarsOf(s);
            return sum;
        }

        /// <summary>클리어 기록. 첫 클리어면 true.</summary>
        public bool RecordClear(StageData s, int stars)
        {
            if (s == null) return false;
            if (!_records.TryGetValue(s.Key, out var r))
            {
                r = new Record { id = s.Key };
                _records[s.Key] = r;
            }
            bool first = r.clears == 0;
            r.clears++;
            r.stars = Mathf.Max(r.stars, Mathf.Clamp(stars, 0, 3));
            Save();
            OnChanged?.Invoke();
            return first;
        }

        // ---------------- 저장 ----------------

        public void Save()
        {
            var data = new SaveData();
            data.records.AddRange(_records.Values);
            SaveStore.SetString(saveKey, JsonUtility.ToJson(data));
            SaveStore.Save();
        }

        public void Load()
        {
            _records.Clear();
            if (!SaveStore.HasKey(saveKey)) return;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(SaveStore.GetString(saveKey));
                if (data?.records != null)
                    foreach (var r in data.records)
                        if (r != null && !string.IsNullOrEmpty(r.id)) _records[r.id] = r;
            }
            catch { }
        }

        [ContextMenu("스테이지 진행도 초기화")]
        public void ResetProgress()
        {
            _records.Clear();
            Save();
            OnChanged?.Invoke();
        }
    }
}
