using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SubcultureRPG.Meta.Shop
{
    /// <summary>
    /// 상점 구매 처리 + 구매 제한 기록(PlayerPrefs). 값은 지갑(금화·크리스탈) 또는 인벤토리(재료)에서 차감.
    /// 일간은 매일 05:00, 주간은 매주 월요일 05:00(로컬 시간)에 초기화됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Player Shop")]
    public class PlayerShop : MonoBehaviour
    {
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private string saveKey = "shop";

        /// <summary>한 번에 살 수 있는 최대 횟수(무제한 상품의 수량 선택 상한).</summary>
        public const int MaxPerPurchase = 99;

        [Serializable] private class Entry { public string id; public int count; public string period; }
        [Serializable] private class SaveData { public List<Entry> items = new(); }

        private readonly Dictionary<string, Entry> _bought = new();
        public event Action OnChanged;

        private void Awake()
        {
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            if (inventory == null) inventory = FindAnyObjectByType<PlayerInventory>();
            Load();
        }

        /// <summary>코드로 붙였을 때 참조 연결.</summary>
        public void Bind(PlayerWallet w, PlayerInventory inv)
        {
            if (w != null) wallet = w;
            if (inv != null) inventory = inv;
        }

        // ---------------- 조회 ----------------

        /// <summary>이번 주기에 이미 산 횟수.</summary>
        public int Bought(ShopItem item)
        {
            if (item == null || item.limit <= 0) return 0;
            return _bought.TryGetValue(item.id, out var e) && e.period == PeriodKey(item.reset, DateTime.Now) ? e.count : 0;
        }

        /// <summary>남은 구매 횟수(무제한이면 int.MaxValue).</summary>
        public int Remaining(ShopItem item)
        {
            if (item == null) return 0;
            if (item.limit <= 0) return int.MaxValue;
            return Mathf.Max(0, item.limit - Bought(item));
        }

        /// <summary>보유 재화로 살 수 있는 횟수.</summary>
        public int Affordable(ShopItem item)
        {
            if (item == null || item.price <= 0) return MaxPerPurchase;
            long have = item.currency switch
            {
                ShopCurrency.Gold => wallet != null ? wallet.Gold : 0,
                ShopCurrency.Crystal => wallet != null ? wallet.Crystals : 0,
                _ => inventory != null ? inventory.Count(item.costMaterial) : 0,
            };
            return (int)Math.Min(int.MaxValue, have / item.price);
        }

        /// <summary>지금 한 번에 살 수 있는 최대 횟수(제한 · 재화 · 99 중 최소).</summary>
        public int MaxBuyable(ShopItem item) =>
            Mathf.Min(MaxPerPurchase, Mathf.Min(Remaining(item), Affordable(item)));

        /// <summary>구매 불가 사유(가능하면 null).</summary>
        public string BlockReason(ShopItem item, int qty)
        {
            if (item == null || qty <= 0) return "수량을 선택하세요";
            if (Remaining(item) < qty) return Remaining(item) == 0 ? "구매 제한에 도달했습니다" : "남은 구매 횟수를 넘었습니다";
            if (Affordable(item) < qty)
                return item.currency switch
                {
                    ShopCurrency.Gold => "금화가 부족합니다",
                    ShopCurrency.Crystal => "크리스탈이 부족합니다",
                    _ => $"{Materials.Name(item.costMaterial)}이(가) 부족합니다",
                };
            if (wallet == null && item.currency != ShopCurrency.Material) return "지갑을 찾을 수 없습니다";
            if (inventory == null && ((!item.IsGold && !item.IsTicket) || item.currency == ShopCurrency.Material)) return "가방을 찾을 수 없습니다";
            if (wallet == null && (item.IsGold || item.IsTicket)) return "지갑을 찾을 수 없습니다";
            return null;
        }

        // ---------------- 구매 ----------------

        /// <summary>qty회 구매. 성공하면 값 차감 → 보상 지급 → 제한 기록.</summary>
        public bool Buy(ShopItem item, int qty)
        {
            if (BlockReason(item, qty) != null) return false;
            int cost = item.price * qty;
            bool paid = item.currency switch
            {
                ShopCurrency.Gold => wallet.SpendGold(cost),
                ShopCurrency.Crystal => wallet.Spend(cost),
                _ => inventory.Spend(item.costMaterial, cost),
            };
            if (!paid) return false;

            int got = item.amount * qty;
            if (item.IsTicket) wallet.AddTickets(item.ticketType, got);
            else if (item.IsGold) wallet.AddGold(item.rewardGold * got);
            else inventory.Add(item.reward, got);

            if (item.limit > 0)
            {
                string period = PeriodKey(item.reset, DateTime.Now);
                int prev = Bought(item);
                _bought[item.id] = new Entry { id = item.id, count = prev + qty, period = period };
                Save();
            }
            OnChanged?.Invoke();
            MissionEvents.Report(MissionGoal.ShopBuy);
            return true;
        }

        // ---------------- 초기화 주기 ----------------

        /// <summary>초기화 시각 기준 '게임 날짜'(05:00 이전은 전날).</summary>
        private static DateTime GameDay(DateTime now) => now.AddHours(-ShopCatalog.ResetHour).Date;

        private static DateTime WeekStart(DateTime now)
        {
            var d = GameDay(now);
            int back = ((int)d.DayOfWeek + 6) % 7;   // 월요일 = 0
            return d.AddDays(-back);
        }

        /// <summary>같은 주기면 같은 문자열.</summary>
        public static string PeriodKey(ShopReset reset, DateTime now) => reset switch
        {
            ShopReset.Daily => "d" + GameDay(now).ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            ShopReset.Weekly => "w" + WeekStart(now).ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            _ => "all",
        };

        /// <summary>다음 초기화까지 남은 시간(무제한·초기화 없음이면 null).</summary>
        public static TimeSpan? UntilReset(ShopReset reset, DateTime now)
        {
            DateTime next;
            switch (reset)
            {
                case ShopReset.Daily: next = GameDay(now).AddDays(1).AddHours(ShopCatalog.ResetHour); break;
                case ShopReset.Weekly: next = WeekStart(now).AddDays(7).AddHours(ShopCatalog.ResetHour); break;
                default: return null;
            }
            return next - now;
        }

        /// <summary>'3시간 12분' · '2일 4시간' 형식.</summary>
        public static string FormatSpan(TimeSpan t)
        {
            if (t.TotalDays >= 1) return $"{(int)t.TotalDays}일 {t.Hours}시간";
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}시간 {t.Minutes}분";
            return $"{Mathf.Max(1, t.Minutes)}분";
        }

        // ---------------- 저장 ----------------

        private void Save()
        {
            var data = new SaveData();
            foreach (var kv in _bought) data.items.Add(kv.Value);
            SaveStore.SetString(saveKey, JsonUtility.ToJson(data));
            SaveStore.Save();
        }

        private void Load()
        {
            _bought.Clear();
            if (!SaveStore.HasKey(saveKey)) return;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(SaveStore.GetString(saveKey));
                if (data?.items == null) return;
                foreach (var e in data.items)
                    if (e != null && !string.IsNullOrEmpty(e.id) && ShopCatalog.Find(e.id) != null) _bought[e.id] = e;   // 삭제된 상품은 버림
            }
            catch { }
        }

        [ContextMenu("상점 구매 기록 초기화")]
        public void ResetPurchases()
        {
            _bought.Clear();
            Save();
            OnChanged?.Invoke();
        }
    }
}
