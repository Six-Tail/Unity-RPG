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
            if (grantAllOnStart && database != null)
            {
                foreach (var c in database.Characters)
                    if (!IsOwned(c)) Add(c, false);
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
