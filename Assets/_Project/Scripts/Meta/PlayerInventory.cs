using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta
{
    /// <summary>육성 재료 인벤토리. PlayerPrefs 저장. (가챠 재화 크리스탈과 별개)</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Player Inventory")]
    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private string saveKey = "inventory";
        [SerializeField, Tooltip("세이브가 없을 때 테스트용 기본 재료 지급.")] private bool grantDefaultsIfEmpty = true;

        [Serializable] private class Entry { public MaterialType type; public int count; }
        [Serializable] private class SaveData { public List<Entry> items = new(); }

        private readonly Dictionary<MaterialType, int> _items = new();
        public event Action OnChanged;

        private void Awake()
        {
            bool had = PlayerPrefs.HasKey(saveKey);
            Load();
            if (grantDefaultsIfEmpty && !had)
            {
                _items[MaterialType.ExpSmall] = 40;
                _items[MaterialType.ExpPotion] = 30;
                _items[MaterialType.ExpLarge] = 15;
                _items[MaterialType.ExpCrystal] = 6;
                _items[MaterialType.SkillBook] = 24;
                Save();
            }
        }

        public int Count(MaterialType t) => _items.TryGetValue(t, out var n) ? n : 0;
        public bool CanAfford(MaterialType t, int n) => Count(t) >= n;

        public bool Spend(MaterialType t, int n)
        {
            if (n <= 0) { OnChanged?.Invoke(); return true; }
            if (Count(t) < n) return false;
            _items[t] = Count(t) - n;
            Save();
            OnChanged?.Invoke();
            return true;
        }

        public void Add(MaterialType t, int n)
        {
            if (n <= 0) return;
            _items[t] = Count(t) + n;
            Save();
            OnChanged?.Invoke();
        }

        private void Save()
        {
            var data = new SaveData();
            foreach (var kv in _items) data.items.Add(new Entry { type = kv.Key, count = kv.Value });
            PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        private void Load()
        {
            _items.Clear();
            if (!PlayerPrefs.HasKey(saveKey)) return;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(saveKey));
                if (data != null && data.items != null)
                    foreach (var e in data.items) _items[e.type] = e.count;
            }
            catch { }
        }

        [ContextMenu("재료 테스트 지급")]
        public void GrantTest()
        {
            _items[MaterialType.ExpSmall] = Count(MaterialType.ExpSmall) + 40;
            _items[MaterialType.ExpPotion] = Count(MaterialType.ExpPotion) + 30;
            _items[MaterialType.ExpLarge] = Count(MaterialType.ExpLarge) + 15;
            _items[MaterialType.ExpCrystal] = Count(MaterialType.ExpCrystal) + 6;
            Add(MaterialType.SkillBook, 12);
            // 속성별 돌파 재료(40/60/80 돌파 각 1회분 정도)
            for (int e = 0; e <= (int)Element.Wind; e++)
            {
                _items[Materials.Ascension((Element)e, 1)] = Count(Materials.Ascension((Element)e, 1)) + 5;
                _items[Materials.Ascension((Element)e, 2)] = Count(Materials.Ascension((Element)e, 2)) + 8;
                _items[Materials.Ascension((Element)e, 3)] = Count(Materials.Ascension((Element)e, 3)) + 12;
            }
            Save();
            OnChanged?.Invoke();
        }
    }
}
