using System;
using UnityEngine;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 재화 지갑. PlayerPrefs 저장.
    /// 크리스탈 = 가챠 재화(첫 클리어 등), 금화 = 육성 공용 재화(레벨업·돌파·스킬·합성/초월).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Player Wallet")]
    public class PlayerWallet : MonoBehaviour
    {
        [SerializeField] private int startingCrystals = 16000;
        [SerializeField] private int startingGold = 100000;
        [SerializeField] private string saveKey = "wallet";

        public int Crystals { get; private set; }
        public int Gold { get; private set; }
        public event Action OnChanged;

        private void Awake() => Load();

        public bool CanAfford(int cost) => Crystals >= cost;

        public bool Spend(int cost)
        {
            if (cost <= 0) { OnChanged?.Invoke(); return true; }
            if (Crystals < cost) return false;
            Crystals -= cost;
            Save();
            OnChanged?.Invoke();
            return true;
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            Crystals += amount;
            Save();
            OnChanged?.Invoke();
        }

        // ---------------- 금화 ----------------

        public bool CanAffordGold(int cost) => Gold >= cost;

        public bool SpendGold(int cost)
        {
            if (cost <= 0) return true;
            if (Gold < cost) return false;
            Gold -= cost;
            Save();
            OnChanged?.Invoke();
            return true;
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            Gold = (int)Mathf.Min(int.MaxValue, (long)Gold + amount);
            Save();
            OnChanged?.Invoke();
        }

        [Serializable]
        private class SaveData { public int crystals; public bool init; public int gold; public bool goldInit; }

        private void Save()
        {
            PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(new SaveData { crystals = Crystals, init = true, gold = Gold, goldInit = true }));
            PlayerPrefs.Save();
        }

        private void Load()
        {
            Crystals = startingCrystals;
            Gold = startingGold;
            if (!PlayerPrefs.HasKey(saveKey)) return;
            try
            {
                var d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(saveKey));
                if (d == null) return;
                if (d.init) Crystals = d.crystals;
                if (d.goldInit) Gold = d.gold;   // 금화 도입 전 세이브 → 시작 금화 지급
            }
            catch { }
        }

        [ContextMenu("지갑 초기화")]
        public void ResetWallet()
        {
            Crystals = startingCrystals;
            Gold = startingGold;
            Save();
            OnChanged?.Invoke();
        }

        [ContextMenu("금화 테스트 지급 (+100,000)")]
        public void GrantTestGold() => AddGold(100000);
    }
}
