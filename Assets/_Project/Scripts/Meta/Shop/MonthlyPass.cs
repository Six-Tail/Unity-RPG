using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubcultureRPG.Meta.Shop
{
    /// <summary>월정액 1종.</summary>
    public sealed class PassDef
    {
        public string id, name, desc, icon;
        public int crystals;             // (옛 임시 크리스탈 가격 — 이제 인앱 결제)
        public string realPrice;         // 인앱 결제 기본 가격(스토어 가격을 못 불러왔을 때 표시)
        public Func<RewardBundle> now;   // 살 때 바로
        public Func<RewardBundle> daily; // 매일 05:00 우편
        public Color color;
    }

    /// <summary>
    /// 월정액: 사면 바로 보상 + 30일 동안 매일 05:00(게임 날짜가 바뀔 때) 우편으로 보상. 다시 사면 30일 연장(남은 기간 최대 180일).
    /// 인앱 결제 상품(IapCatalog · IapStore — 상품 ID = PassDef.id). 게임을 켜지 않은 날의 보상은 쌓이지 않음.
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(MonthlyPass.Instance).
    /// </summary>
    public class MonthlyPass : MonoBehaviour
    {
        public const int Days = 30, MaxDays = 180;
        private const string SaveKey = "monthly_pass";

        public static readonly PassDef[] Passes =
        {
            new()
            {
                id = "pass_crystal", name = "별빛 크리스탈 월정액", icon = "Items/PassCrystal", crystals = 900, realPrice = "₩5,900",
                desc = "가장 효율 좋은 크리스탈 공급", color = new Color(0.35f, 0.75f, 1f),
                now = () => new RewardBundle().Crystals(300),
                daily = () => new RewardBundle().Crystals(100),
            },
            new()
            {
                id = "pass_stamina", name = "행동력 보급 월정액", icon = "Items/PassStamina", crystals = 500, realPrice = "₩4,900",
                desc = "스테이지 · 던전을 더 많이", color = new Color(0.4f, 0.9f, 0.5f),
                now = () => new RewardBundle().Mat(MaterialType.StaminaLarge, 2),
                daily = () => new RewardBundle().Mat(MaterialType.StaminaLarge, 1).Mat(MaterialType.StaminaSmall, 1),
            },
            new()
            {
                id = "pass_gold", name = "금화 보급 월정액", icon = "Items/PassGold", crystals = 500, realPrice = "₩4,900",
                desc = "육성에 쓰는 금화 · 경험치", color = new Color(1f, 0.82f, 0.35f),
                now = () => new RewardBundle().Gold(200000),
                daily = () => new RewardBundle().Gold(60000).Mat(MaterialType.ExpLarge, 3),
            },
            new()
            {
                id = "pass_premium", name = "지휘관 특권 월정액", icon = "Items/PassPremium", crystals = 1500, realPrice = "₩11,000",
                desc = "모든 것을 조금씩 + 픽업 뽑기권", color = new Color(0.95f, 0.6f, 1f),
                now = () => new RewardBundle().Pickup(3),
                daily = () => new RewardBundle().Crystals(60).Mat(MaterialType.StaminaLarge, 1).Gold(30000).Mat(MaterialType.SweepTicket, 5),
            },
        };

        public static PassDef Find(string id) => Array.Find(Passes, p => p.id == id);

        [Serializable] private class Entry { public string id; public string end = ""; public string last = ""; }   // end = 마지막 날 다음 게임 날짜(yyyyMMdd), last = 마지막으로 보낸 날
        [Serializable] private class SaveData { public List<Entry> list = new(); }

        private SaveData _d = new();
        private bool _loaded;
        private string _checkedDay = "";
        public event Action OnChanged;

        private static MonthlyPass _instance;
        public static MonthlyPass Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<MonthlyPass>();
                if (_instance == null) _instance = new GameObject("MonthlyPass").AddComponent<MonthlyPass>();
                _instance.EnsureLoaded();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { _ = Instance; }

        private void Awake() { if (_instance == null) _instance = this; EnsureLoaded(); }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            if (SaveStore.HasKey(SaveKey))
            {
                try { _d = JsonUtility.FromJson<SaveData>(SaveStore.GetString(SaveKey)) ?? new SaveData(); }
                catch { _d = new SaveData(); }
            }
            _d.list ??= new();
        }

        private void Save() { SaveStore.SetString(SaveKey, JsonUtility.ToJson(_d)); SaveStore.Save(); }
        private void Changed() { Save(); OnChanged?.Invoke(); }

        /// <summary>게임 날짜(05:00 이전은 전날).</summary>
        private static DateTime Today => DateTime.Now.AddHours(-ShopCatalog.ResetHour).Date;
        private static string Key(DateTime d) => d.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        private static DateTime Parse(string s) =>
            DateTime.TryParseExact(s ?? "", "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d) ? d : DateTime.MinValue;

        private Entry Get(PassDef p) => _d.list.Find(e => e.id == p.id);

        /// <summary>오늘을 포함한 남은 날(0 = 없음).</summary>
        public int DaysLeft(PassDef p)
        {
            EnsureLoaded();
            var e = Get(p);
            if (e == null) return 0;
            int left = (int)(Parse(e.end) - Today).TotalDays;
            return Mathf.Max(0, left);
        }

        public bool Active(PassDef p) => DaysLeft(p) > 0;
        public bool ReceivedToday(PassDef p) { var e = Get(p); return e != null && e.last == Key(Today); }

        /// <summary>결제 전에 확인하는 구매 불가 사유(가능하면 null) — 남은 기간 최대 180일.</summary>
        public string PaidBlockReason(PassDef p)
        {
            if (p == null) return "상품을 찾을 수 없습니다";
            if (DaysLeft(p) + Days > MaxDays) return $"남은 기간은 최대 {MaxDays}일까지입니다";
            return null;
        }

        /// <summary>결제가 끝난 월정액 시작(또는 30일 연장): 바로 보상 → 오늘 보상도 바로 우편. 값은 스토어에서 받음.</summary>
        public bool Activate(PassDef p)
        {
            EnsureLoaded();
            if (p == null) return false;
            var e = Get(p);
            if (e == null) { e = new Entry { id = p.id }; _d.list.Add(e); }
            var start = Active(p) ? Parse(e.end) : Today;
            e.end = Key(start.AddDays(Days));
            p.now().Grant();
            Changed();
            Deliver(force: true);
            return true;
        }

        /// <summary>오늘 아직 안 보낸 월정액 보상을 우편으로.</summary>
        private void Deliver(bool force = false)
        {
            string today = Key(Today);
            if (!force && _checkedDay == today) return;
            _checkedDay = today;
            bool any = false;
            foreach (var p in Passes)
            {
                var e = Get(p);
                if (e == null || e.last == today) continue;
                int left = DaysLeft(p);
                if (left <= 0) continue;
                e.last = today;
                PlayerMail.Instance.Send($"{p.name} — 오늘의 보상",
                    $"월정액 보상입니다. 남은 기간 {left}일(오늘 포함).", p.daily());
                any = true;
            }
            if (any) Changed();
        }

        private float _tick;
        private void Update()
        {
            if (Time.unscaledTime < _tick) return;
            _tick = Time.unscaledTime + 20f;
            Deliver();
        }

        [ContextMenu("테스트: 하루 넘긴 것처럼 오늘 보상 다시 보내기")]
        private void TestResend() { foreach (var e in _d.list) e.last = ""; _checkedDay = ""; Deliver(); }
    }
}
