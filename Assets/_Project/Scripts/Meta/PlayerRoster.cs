using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Gacha;

namespace SubcultureRPG.Meta
{
    /// <summary>보유 캐릭터 1종(중복 수, 레벨).</summary>
    [Serializable]
    public class OwnedCharacter
    {
        public string id;
        public int count = 1;
        public int level = 1;
        public int exp = 0;
        public int ascension = 0;   // 돌파 단계(레벨 상한)
        public int skillLevel = 1;  // (구) 단일 스킬 레벨 — 지금은 skills의 최고값으로 유지
        public List<int> skills = new();   // 스킬별 레벨 [기본 공격, 스킬, 궁극기]
        public int star = 0;        // 현재 성급(합성으로 상승). 0 = 태생 성급
        public int transcend = 0;   // 초월 단계(6성에서 +1~+6)

        /// <summary>합성/초월에 쓸 수 있는 중복 수(본체 1장 제외).</summary>
        public int Duplicates => Mathf.Max(0, count - 1);
    }

    /// <summary>
    /// 보유 캐릭터(로스터). 가챠 결과를 받아 보유 목록에 추가하고 PlayerPrefs(JSON)에 저장합니다.
    /// id는 CharacterData.id(없으면 asset 이름).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Player Roster")]
    public class PlayerRoster : MonoBehaviour
    {
        [SerializeField, Tooltip("비우면 씬에서 자동 탐색. 뽑기 결과를 자동 반영.")]
        private GachaController gacha;
        [SerializeField, Tooltip("테스트용: 시작 시 DB의 모든 캐릭터를 보유 처리.")]
        private bool grantAllOnStart = false;
        [SerializeField, Tooltip("grantAllOnStart 및 조회용 데이터베이스.")]
        private GameDatabase database;
        [SerializeField, Tooltip("보유 캐릭터가 하나도 없을 때(새 세이브) 지급할 스타터 캐릭터.")]
        private List<CharacterData> starterCharacters = new();
        [SerializeField] private string saveKey = "roster";

        private readonly List<OwnedCharacter> _owned = new();
        public IReadOnlyList<OwnedCharacter> Owned => _owned;

        /// <summary>로스터가 바뀔 때.</summary>
        public event Action OnRosterChanged;

        public static string KeyOf(CharacterData c) =>
            c == null ? null : (!string.IsNullOrEmpty(c.id) ? c.id : c.name);

        private void Awake()
        {
            if (gacha == null) gacha = FindAnyObjectByType<GachaController>();
            Load();
        }

        private void OnEnable() { if (gacha != null) gacha.OnPulled += HandlePulled; }
        private void OnDisable() { if (gacha != null) gacha.OnPulled -= HandlePulled; }

        private void Start()
        {
            bool changed = false;

            // 새 세이브면 스타터 지급
            if (_owned.Count == 0 && starterCharacters != null)
                foreach (var c in starterCharacters)
                    if (c != null && !IsOwned(c)) { Add(c, false); changed = true; }

            // 테스트용 전원 지급
            if (grantAllOnStart && database != null)
                foreach (var c in database.Characters)
                    if (!IsOwned(c)) { Add(c, false); changed = true; }

            if (changed)
            {
                Save();
                OnRosterChanged?.Invoke();
            }
        }

        private void HandlePulled(List<GachaResult> results)
        {
            foreach (var r in results) Add(r.character, false);
            Save();
            OnRosterChanged?.Invoke();
        }

        public void Add(CharacterData c, bool notify = true)
        {
            var key = KeyOf(c);
            if (string.IsNullOrEmpty(key)) return;

            var existing = _owned.Find(o => o.id == key);
            if (existing != null) existing.count++;
            else _owned.Add(new OwnedCharacter { id = key, count = 1, level = 1 });

            if (notify) { Save(); OnRosterChanged?.Invoke(); }
        }

        public bool IsOwned(CharacterData c)
        {
            var k = KeyOf(c);
            return k != null && _owned.Exists(o => o.id == k);
        }

        public OwnedCharacter Get(string id) => _owned.Find(o => o.id == id);

        public OwnedCharacter GetOwned(CharacterData c)
        {
            var k = KeyOf(c);
            return k != null ? _owned.Find(o => o.id == k) : null;
        }

        public int LevelOf(CharacterData c)
        {
            var o = GetOwned(c);
            return o != null ? o.level : 1;
        }

        public int AscensionOf(CharacterData c)
        {
            var o = GetOwned(c);
            return o != null ? o.ascension : 0;
        }

        public int SkillLevelOf(CharacterData c)
        {
            var o = GetOwned(c);
            return o != null ? o.skillLevel : 1;
        }

        /// <summary>경험치 추가 + 레벨업(돌파 단계의 레벨 상한까지).</summary>
        public void AddExp(CharacterData c, int amount)
        {
            var o = GetOwned(c);
            if (o == null || amount <= 0) return;

            int cap = Leveling.LevelCap(o.ascension);
            if (o.level >= cap) return; // 상한 도달 → 돌파 필요

            o.exp += amount;
            while (o.level < cap && o.exp >= Leveling.ExpForNext(o.level))
            {
                o.exp -= Leveling.ExpForNext(o.level);
                o.level++;
            }
            if (o.level >= cap) o.exp = 0;

            Save();
            OnRosterChanged?.Invoke();
        }

        /// <summary>돌파 가능 여부(레벨 상한 도달 + 최대 돌파 전).</summary>
        public bool CanAscend(CharacterData c)
        {
            var o = GetOwned(c);
            return o != null && o.ascension < Leveling.MaxAscension && o.level >= Leveling.LevelCap(o.ascension);
        }

        public bool Ascend(CharacterData c)
        {
            var o = GetOwned(c);
            if (o == null || !CanAscend(c)) return false;
            o.ascension++;
            Save();
            OnRosterChanged?.Invoke();
            return true;
        }

        /// <summary>스킬별 레벨 [기본 공격, 스킬, 궁극기] (옛 세이브는 단일 스킬 레벨로 채움).</summary>
        public int[] SkillLevels(CharacterData c)
        {
            var o = GetOwned(c);
            if (o == null) return new[] { 1, 1, 1 };
            EnsureSkills(o);
            return o.skills.ToArray();
        }

        public int SkillLevelOf(CharacterData c, SkillSlot slot) => SkillLevels(c)[(int)slot];

        public bool CanUpgradeSkill(CharacterData c, SkillSlot slot) =>
            GetOwned(c) != null && SkillLevelOf(c, slot) < Leveling.MaxSkillLevel;

        /// <summary>스킬 1개 레벨업(비용 차감은 호출자).</summary>
        public bool UpgradeSkill(CharacterData c, SkillSlot slot)
        {
            var o = GetOwned(c);
            if (o == null || !CanUpgradeSkill(c, slot)) return false;
            EnsureSkills(o);
            o.skills[(int)slot]++;
            o.skillLevel = Mathf.Max(o.skills[0], Mathf.Max(o.skills[1], o.skills[2]));
            Save();
            OnRosterChanged?.Invoke();
            return true;
        }

        private static void EnsureSkills(OwnedCharacter o)
        {
            if (o.skills == null) o.skills = new List<int>();
            while (o.skills.Count < 3) o.skills.Add(Mathf.Clamp(o.skillLevel, 1, Leveling.MaxSkillLevel));
        }

        public bool CanUpgradeSkill(CharacterData c)
        {
            var o = GetOwned(c);
            return o != null && o.skillLevel < Leveling.MaxSkillLevel;
        }

        public bool UpgradeSkill(CharacterData c)
        {
            var o = GetOwned(c);
            if (o == null || !CanUpgradeSkill(c)) return false;
            o.skillLevel++;
            Save();
            OnRosterChanged?.Invoke();
            return true;
        }

        // ---------------- 합성 / 초월 ----------------

        /// <summary>현재 성급(합성 반영).</summary>
        public int StarOf(CharacterData c)
        {
            if (c == null) return Transcendence.MinStar;
            var o = GetOwned(c);
            return Transcendence.CurrentStar(c.rarity, o != null ? o.star : 0);
        }

        /// <summary>초월 단계(0~6).</summary>
        public int TranscendOf(CharacterData c)
        {
            var o = GetOwned(c);
            return o != null ? Mathf.Clamp(o.transcend, 0, Transcendence.MaxTranscend) : 0;
        }

        public int DuplicatesOf(CharacterData c)
        {
            var o = GetOwned(c);
            return o != null ? o.Duplicates : 0;
        }

        /// <summary>다음 합성/초월 1단계 비용(중복 수). 단계가 오를수록 비싸짐. 풀 초월이면 0.</summary>
        public int MergeCost(CharacterData c) =>
            c != null ? Transcendence.NextCost(c.rarity, StarOf(c), TranscendOf(c)) : 0;

        /// <summary>합성 가능: 6성 미만 + 중복 충분.</summary>
        public bool CanPromote(CharacterData c) =>
            GetOwned(c) != null && StarOf(c) < Transcendence.MaxStar && DuplicatesOf(c) >= MergeCost(c);

        /// <summary>합성: 중복을 소모해 성급 +1.</summary>
        public bool Promote(CharacterData c)
        {
            if (!CanPromote(c)) return false;
            var o = GetOwned(c);
            o.count -= MergeCost(c);
            o.star = StarOf(c) + 1;
            Save();
            OnRosterChanged?.Invoke();
            return true;
        }

        /// <summary>초월 가능: 6성 + 초월 6 미만 + 중복 충분.</summary>
        public bool CanTranscend(CharacterData c) =>
            GetOwned(c) != null && StarOf(c) >= Transcendence.MaxStar
            && TranscendOf(c) < Transcendence.MaxTranscend && DuplicatesOf(c) >= MergeCost(c);

        /// <summary>초월: 중복을 소모해 초월 +1.</summary>
        public bool Transcend(CharacterData c)
        {
            if (!CanTranscend(c)) return false;
            var o = GetOwned(c);
            o.count -= MergeCost(c);   // 초월 단계 올리기 전 비용
            o.star = Transcendence.MaxStar;
            o.transcend++;
            Save();
            OnRosterChanged?.Invoke();
            return true;
        }

        /// <summary>테스트용: 해당 캐릭터 중복 추가(c가 null이면 보유 전원).</summary>
        public void GrantTestDuplicates(CharacterData c, int amount)
        {
            if (amount <= 0) return;
            if (c != null) { var o = GetOwned(c); if (o == null) return; o.count += amount; }
            else foreach (var o in _owned) o.count += amount;
            Save();
            OnRosterChanged?.Invoke();
        }

        [ContextMenu("테스트: 보유 캐릭터 전원 중복 +6")]
        private void DebugAddDuplicates() => GrantTestDuplicates(null, 6);

        // ---------------- 저장 ----------------

        [Serializable]
        private class SaveData { public List<OwnedCharacter> owned = new(); }

        public void Save()
        {
            PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(new SaveData { owned = _owned }));
            PlayerPrefs.Save();
        }

        public void Load()
        {
            _owned.Clear();
            if (!PlayerPrefs.HasKey(saveKey)) return;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(saveKey));
                if (data != null && data.owned != null) _owned.AddRange(data.owned);
            }
            catch { }
        }

        [ContextMenu("로스터 초기화")]
        public void ClearRoster()
        {
            _owned.Clear();
            Save();
            OnRosterChanged?.Invoke();
        }
    }
}
