using System;
using UnityEngine;
using SubcultureRPG.Gacha;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 재화 지갑. PlayerPrefs 저장.
    /// 크리스탈 = 가챠 재화(첫 클리어 등), 금화 = 육성 공용 재화(레벨업·돌파·스킬·합성/초월),
    /// 뽑기권 = 픽업 뽑기권 / 상시 뽑기권(각 배너 종류에서만 사용, 1장 = 1회).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Player Wallet")]
    public class PlayerWallet : MonoBehaviour
    {
        [SerializeField] private int startingCrystals = 16000;
        [SerializeField] private int startingGold = 100000;
        [SerializeField, Tooltip("새 세이브(또는 뽑기권 도입 전 세이브)에 지급할 픽업 뽑기권.")] private int startingPickupTickets = 10;
        [SerializeField, Tooltip("새 세이브(또는 뽑기권 도입 전 세이브)에 지급할 상시 뽑기권.")] private int startingStandardTickets = 10;
        [SerializeField] private string saveKey = "wallet";

        public int Crystals { get; private set; }
        public int Gold { get; private set; }
        public int PickupTickets { get; private set; }
        public int StandardTickets { get; private set; }
        public event Action OnChanged;

        // ---------------- 뽑기권 ----------------

        public static string TicketName(GachaBannerType t) => t == GachaBannerType.Standard ? "상시 뽑기권" : "픽업 뽑기권";

        public int Tickets(GachaBannerType t) => t == GachaBannerType.Standard ? StandardTickets : PickupTickets;

        public bool CanAffordTickets(GachaBannerType t, int n) => Tickets(t) >= n;

        public bool SpendTickets(GachaBannerType t, int n)
        {
            if (n <= 0) return true;
            if (Tickets(t) < n) return false;
            SetTickets(t, Tickets(t) - n);
            Save();
            OnChanged?.Invoke();
            return true;
        }

        public void AddTickets(GachaBannerType t, int n)
        {
            if (n <= 0) return;
            SetTickets(t, (int)Mathf.Min(int.MaxValue, (long)Tickets(t) + n));
            Save();
            OnChanged?.Invoke();
        }

        private void SetTickets(GachaBannerType t, int v)
        {
            if (t == GachaBannerType.Standard) StandardTickets = v; else PickupTickets = v;
        }

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
        private class SaveData
        {
            public int crystals; public bool init;
            public int gold; public bool goldInit;
            public int pickupTickets, standardTickets; public bool ticketInit;
        }

        private void Save()
        {
            PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(new SaveData
            {
                crystals = Crystals, init = true,
                gold = Gold, goldInit = true,
                pickupTickets = PickupTickets, standardTickets = StandardTickets, ticketInit = true,
            }));
            PlayerPrefs.Save();
        }

        private void Load()
        {
            Crystals = startingCrystals;
            Gold = startingGold;
            PickupTickets = startingPickupTickets;
            StandardTickets = startingStandardTickets;
            if (!PlayerPrefs.HasKey(saveKey)) return;
            try
            {
                var d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(saveKey));
                if (d == null) return;
                if (d.init) Crystals = d.crystals;
                if (d.goldInit) Gold = d.gold;   // 금화 도입 전 세이브 → 시작 금화 지급
                if (d.ticketInit)                // 뽑기권 도입 전 세이브 → 시작 뽑기권 지급
                {
                    PickupTickets = d.pickupTickets;
                    StandardTickets = d.standardTickets;
                }
            }
            catch { }
        }

        [ContextMenu("지갑 초기화")]
        public void ResetWallet()
        {
            Crystals = startingCrystals;
            Gold = startingGold;
            PickupTickets = startingPickupTickets;
            StandardTickets = startingStandardTickets;
            Save();
            OnChanged?.Invoke();
        }

        [ContextMenu("뽑기권 테스트 지급 (픽업·상시 각 +10)")]
        public void GrantTestTickets()
        {
            PickupTickets += 10;
            StandardTickets += 10;
            Save();
            OnChanged?.Invoke();
        }

        [ContextMenu("금화 테스트 지급 (+100,000)")]
        public void GrantTestGold() => AddGold(100000);
    }
}
