using System;
using UnityEngine;

namespace SubcultureRPG.Meta
{
    /// <summary>간단한 재화(크리스탈) 지갑. PlayerPrefs 저장.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Player Wallet")]
    public class PlayerWallet : MonoBehaviour
    {
        [SerializeField] private int startingCrystals = 16000;
        [SerializeField] private string saveKey = "wallet";

        public int Crystals { get; private set; }
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

        [Serializable]
        private class SaveData { public int crystals; public bool init; }

        private void Save() =>
            PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(new SaveData { crystals = Crystals, init = true }));

        private void Load()
        {
            if (PlayerPrefs.HasKey(saveKey))
            {
                try
                {
                    var d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(saveKey));
                    Crystals = d != null && d.init ? d.crystals : startingCrystals;
                }
                catch { Crystals = startingCrystals; }
            }
            else Crystals = startingCrystals;
        }

        [ContextMenu("지갑 초기화")]
        public void ResetWallet()
        {
            Crystals = startingCrystals;
            Save();
            OnChanged?.Invoke();
        }
    }
}
