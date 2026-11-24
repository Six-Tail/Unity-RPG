using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Meta.Shop;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 친구: 서버가 없어 상대는 가상 지휘관(<see cref="Commanders"/>)입니다.
    /// · 친구 최대 30명 · 추천 친구 6명(새로고침 가능) · UID(8자리)로 찾아 추가 — 신청하면 바로 수락됨
    /// · 받은 신청: 하루에 최대 2건씩 들어옴(최대 5건 보관) → 수락 / 거절
    /// · 인사: 친구마다 하루 1번 → 행동력 +2 · 금화 500 (05:00 초기화)
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(PlayerSocial.Instance).
    /// </summary>
    public class PlayerSocial : MonoBehaviour
    {
        public const int MaxFriends = 30, RecommendCount = 6, MaxRequests = 5, GreetStamina = 2, GreetGold = 500;
        private const string SaveKey = "social";

        [Serializable]
        private class SaveData
        {
            public List<int> friends = new(), requests = new(), recommend = new(), greeted = new();
            public string day = "";
        }

        private SaveData _d = new();
        private bool _loaded;
        public event Action OnChanged;

        private static PlayerSocial _instance;
        public static PlayerSocial Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<PlayerSocial>();
                if (_instance == null) _instance = new GameObject("PlayerSocial").AddComponent<PlayerSocial>();
                _instance.EnsureLoaded();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
            EnsureLoaded();
        }

        private void EnsureLoaded()
        {
            if (!_loaded)
            {
                _loaded = true;
                if (PlayerPrefs.HasKey(SaveKey))
                {
                    try { _d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)) ?? new SaveData(); }
                    catch { _d = new SaveData(); }
                }
                _d.friends ??= new(); _d.requests ??= new(); _d.recommend ??= new(); _d.greeted ??= new();
            }
            string today = PlayerShop.PeriodKey(ShopReset.Daily, DateTime.Now);
            if (_d.day == today) return;
            // 하루가 바뀜: 인사 초기화 · 새 신청 도착 · 추천 새로
            bool first = string.IsNullOrEmpty(_d.day);
            _d.day = today;
            _d.greeted.Clear();
            int incoming = first ? 2 : UnityEngine.Random.Range(1, 3);
            for (int i = 0; i < incoming && _d.requests.Count < MaxRequests; i++)
            {
                int s = NewSeed();
                if (s >= 0) _d.requests.Add(s);
            }
            FillRecommend();
            Save();
        }

        private void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_d));
            PlayerPrefs.Save();
        }

        private void Changed() { Save(); OnChanged?.Invoke(); }

        private bool Known(int seed) => _d.friends.Contains(seed) || _d.requests.Contains(seed) || _d.recommend.Contains(seed);

        private int NewSeed()
        {
            for (int tries = 0; tries < 20; tries++)
            {
                int s = UnityEngine.Random.Range(0, Commanders.MaxSeed);
                if (!Known(s)) return s;
            }
            return -1;
        }

        private void FillRecommend()
        {
            _d.recommend.Clear();
            for (int i = 0; i < RecommendCount; i++)
            {
                int s = NewSeed();
                if (s >= 0) _d.recommend.Add(s);
            }
        }

        // ---------------- 조회 ----------------

        public IReadOnlyList<int> Friends { get { EnsureLoaded(); return _d.friends; } }
        public IReadOnlyList<int> Requests { get { EnsureLoaded(); return _d.requests; } }
        public IReadOnlyList<int> Recommended { get { EnsureLoaded(); return _d.recommend; } }
        public bool IsFriend(int seed) => _d.friends.Contains(seed);
        public bool Greeted(int seed) { EnsureLoaded(); return _d.greeted.Contains(seed); }
        public bool IsFull => _d.friends.Count >= MaxFriends;

        /// <summary>오늘 아직 인사하지 않은 친구 수.</summary>
        public int GreetableCount
        {
            get
            {
                EnsureLoaded();
                int n = 0;
                foreach (int f in _d.friends) if (!_d.greeted.Contains(f)) n++;
                return n;
            }
        }

        /// <summary>홈 타일 알림 수(받은 신청 + 인사할 친구).</summary>
        public int BadgeCount => Requests.Count + GreetableCount;

        // ---------------- 동작 ----------------

        /// <summary>친구 추가(추천 · UID 검색 · 받은 신청 수락). 실패 사유를 돌려줌(성공이면 null).</summary>
        public string Add(int seed)
        {
            EnsureLoaded();
            if (seed < 0) return "찾을 수 없는 지휘관입니다";
            if (_d.friends.Contains(seed)) return "이미 친구입니다";
            if (IsFull) return $"친구는 {MaxFriends}명까지입니다";
            _d.friends.Add(seed);
            _d.requests.Remove(seed);
            _d.recommend.Remove(seed);
            Changed();
            return null;
        }

        public void Decline(int seed) { if (_d.requests.Remove(seed)) Changed(); }

        public void Remove(int seed)
        {
            if (!_d.friends.Remove(seed)) return;
            _d.greeted.Remove(seed);
            Changed();
        }

        public void RefreshRecommend() { FillRecommend(); Changed(); }

        /// <summary>인사 1명. 이미 했으면 false.</summary>
        public bool Greet(int seed) => GreetMany(new[] { seed }) > 0;

        /// <summary>아직 인사하지 않은 친구 모두에게 인사. 반환 = 인사한 수.</summary>
        public int GreetAll() => GreetMany(new List<int>(_d.friends));

        private int GreetMany(IList<int> seeds)
        {
            EnsureLoaded();
            int n = 0;
            foreach (int s in seeds)
            {
                if (!_d.friends.Contains(s) || _d.greeted.Contains(s)) continue;
                _d.greeted.Add(s);
                n++;
            }
            if (n == 0) return 0;
            var wallet = FindAnyObjectByType<PlayerWallet>();
            if (wallet != null) { wallet.AddStamina(GreetStamina * n); wallet.AddGold(GreetGold * n); }
            Changed();
            return n;
        }

        [ContextMenu("테스트: 하루 넘기기(인사 초기화 · 새 신청)")]
        private void TestNextDay() { _d.day = "test"; EnsureLoaded(); OnChanged?.Invoke(); }
    }
}
