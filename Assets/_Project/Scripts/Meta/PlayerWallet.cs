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

        [Header("행동력")]
        [SerializeField, Tooltip("자연 회복 최대치.")] private int maxStamina = 240;
        [SerializeField, Tooltip("1 회복에 걸리는 시간(초).")] private int staminaRegenSeconds = 360;
        [SerializeField, Tooltip("새 세이브의 시작 행동력.")] private int startingStamina = 240;

        // 값은 처음 읽힐 때 불러옴(다른 화면이 Awake에서 먼저 읽어도 세이브가 기본값으로 덮이지 않도록)
        public int Crystals { get { EnsureLoaded(); return _crystals; } private set => _crystals = value; }
        public int Gold { get { EnsureLoaded(); return _gold; } private set => _gold = value; }
        public int PickupTickets { get { EnsureLoaded(); return _pickup; } private set => _pickup = value; }
        public int StandardTickets { get { EnsureLoaded(); return _standard; } private set => _standard = value; }
        private int _crystals, _gold, _pickup, _standard;
        private bool _loaded;

        /// <summary>아직 세이브를 불러오지 않았으면 지금 불러옴(한 번만).</summary>
        private void EnsureLoaded()
        {
            if (_loaded) return;
            Load();
        }
        public event Action OnChanged;

        // ---------------- 행동력 ----------------
        // 실시간 회복: 최대치 미만일 때 staminaRegenSeconds마다 1. 최대치 이상(충전 등)이면 회복 멈춤.

        public int Stamina { get { TickStamina(); return _stamina; } }
        public int MaxStamina => maxStamina;
        public const int StaminaHardCap = 999;
        private int _stamina;
        private long _staminaAnchor;   // 마지막 회복 기준 시각(UTC ticks)
        private float _staminaPoll;

        private static long Now => DateTime.UtcNow.Ticks;
        private long RegenTicks => TimeSpan.TicksPerSecond * Math.Max(1, staminaRegenSeconds);

        /// <summary>다음 1 회복까지 남은 초(최대치 이상이면 0).</summary>
        public int SecondsToNextStamina
        {
            get
            {
                TickStamina();
                if (_stamina >= maxStamina) return 0;
                long left = RegenTicks - (Now - _staminaAnchor);
                return (int)Math.Max(0, Math.Ceiling(left / (double)TimeSpan.TicksPerSecond));
            }
        }

        /// <summary>최대치까지 남은 초.</summary>
        public int SecondsToFullStamina
        {
            get
            {
                int s = SecondsToNextStamina;
                int missing = maxStamina - _stamina;
                return missing <= 0 ? 0 : s + (missing - 1) * Math.Max(1, staminaRegenSeconds);
            }
        }

        /// <summary>경과 시간만큼 회복 반영. 값이 바뀌면 저장 + OnChanged.</summary>
        private bool TickStamina()
        {
            EnsureLoaded();
            long now = Now;
            if (_stamina >= maxStamina) { _staminaAnchor = now; return false; }
            long elapsed = now - _staminaAnchor;
            if (elapsed < 0) { _staminaAnchor = now; return false; }   // 시계가 뒤로 간 경우
            long gain = elapsed / RegenTicks;
            if (gain <= 0) return false;
            _stamina = (int)Math.Min(maxStamina, _stamina + gain);
            _staminaAnchor = _stamina >= maxStamina ? now : _staminaAnchor + gain * RegenTicks;
            Save();
            OnChanged?.Invoke();
            return true;
        }

        private void Update()
        {
            // 1초마다 회복 확인(화면의 행동력 표시 갱신용)
            _staminaPoll -= Time.unscaledDeltaTime;
            if (_staminaPoll > 0f) return;
            _staminaPoll = 1f;
            TickStamina();
        }

        public bool CanAffordStamina(int n) => Stamina >= n;

        public bool SpendStamina(int n)
        {
            if (n <= 0) return true;
            TickStamina();
            if (_stamina < n) return false;
            if (_stamina >= maxStamina) _staminaAnchor = Now;   // 최대치에서 쓰면 지금부터 회복 시작
            _stamina -= n;
            Save();
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>행동력 추가(충전·반환). 최대치를 넘을 수 있음(최대 999).</summary>
        public void AddStamina(int n)
        {
            if (n <= 0) return;
            TickStamina();
            _stamina = Math.Min(StaminaHardCap, _stamina + n);
            if (_stamina >= maxStamina) _staminaAnchor = Now;
            Save();
            OnChanged?.Invoke();
        }

        /// <summary>'mm:ss' 또는 'h시간 mm분'.</summary>
        public static string FormatTime(int seconds)
        {
            seconds = Math.Max(0, seconds);
            if (seconds >= 3600) return $"{seconds / 3600}시간 {seconds % 3600 / 60:00}분";
            return $"{seconds / 60}:{seconds % 60:00}";
        }

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

        private void Awake() => EnsureLoaded();

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
            public int stamina; public long staminaAnchor; public bool staminaInit;
        }

        private void Save()
        {
            if (!_loaded) return;   // 불러오기 전 값(기본값)으로 세이브를 덮어쓰지 않음
            PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(new SaveData
            {
                crystals = Crystals, init = true,
                gold = Gold, goldInit = true,
                pickupTickets = PickupTickets, standardTickets = StandardTickets, ticketInit = true,
                stamina = _stamina, staminaAnchor = _staminaAnchor, staminaInit = true,
            }));
            PlayerPrefs.Save();
        }

        private void Load()
        {
            _loaded = true;
            Crystals = startingCrystals;
            Gold = startingGold;
            PickupTickets = startingPickupTickets;
            StandardTickets = startingStandardTickets;
            _stamina = startingStamina;
            _staminaAnchor = Now;
            if (!PlayerPrefs.HasKey(saveKey)) { PlayerPrefs.SetInt("wallet.restore.v1", 1); return; }   // 새 세이브는 복구 대상 아님
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
                if (d.staminaInit)               // 행동력 도입 전 세이브 → 시작 행동력
                {
                    _stamina = d.stamina;
                    _staminaAnchor = d.staminaAnchor > 0 ? d.staminaAnchor : Now;
                }
            }
            catch { }
            RestoreOnce();
        }

        /// <summary>
        /// 1회 복구: 스테이지 화면이 지갑을 불러오기 전에 행동력을 읽어 세이브가 0으로 덮였던 문제(수정됨)로
        /// 잃은 값을 되돌림. 그 전 마지막으로 확인된 값까지 올려 주고(이미 더 많으면 그대로), 다시는 실행되지 않음.
        /// </summary>
        private void RestoreOnce()
        {
            const string key = "wallet.restore.v1";
            if (PlayerPrefs.GetInt(key, 0) == 1) return;
            PlayerPrefs.SetInt(key, 1);
            _crystals = Math.Max(_crystals, 24860);
            _gold = Math.Max(_gold, 14009);
            _pickup = Math.Max(_pickup, 10);
            _standard = Math.Max(_standard, 10);
            if (_stamina < maxStamina) { _stamina = maxStamina; _staminaAnchor = Now; }
            Save();
            Debug.Log("[PlayerWallet] 지갑 1회 복구: 크리스탈 24,860 · 금화 14,009 · 뽑기권 10/10 · 행동력 가득(이미 더 많은 값은 유지)");
        }

        [ContextMenu("지갑 초기화")]
        public void ResetWallet()
        {
            Crystals = startingCrystals;
            Gold = startingGold;
            PickupTickets = startingPickupTickets;
            StandardTickets = startingStandardTickets;
            _stamina = startingStamina;
            _staminaAnchor = Now;
            Save();
            OnChanged?.Invoke();
        }

        [ContextMenu("행동력 테스트 충전 (+120)")]
        public void GrantTestStamina() => AddStamina(120);

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
