using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Meta.Shop;
using SubcultureRPG.Meta.Cloud;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 친구: 가상 지휘관(<see cref="Commanders"/>, 신청하면 바로 수락) + 실제 플레이어(RealFriend — 신청 → 상대가 '받은 신청'에서 수락해야 친구,
    /// 서버 동기화는 <see cref="FriendLink"/>).
    /// · 친구 최대 30명 · 추천 친구 6명(새로고침 가능) · UID(8자리)로 찾아 추가 — 신청하면 바로 수락됨
    /// · 받은 신청: 하루에 최대 2건씩 들어옴(최대 5건 보관) → 수락 / 거절
    /// · 인사: 친구마다 하루 1번 → 행동력 +2 · 금화 500 (05:00 초기화)
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(PlayerSocial.Instance).
    /// </summary>
    public class PlayerSocial : MonoBehaviour
    {
        public const int MaxFriends = 30, RecommendCount = 6, MaxRequests = 5, GreetStamina = 2, GreetGold = 500;
        private const string SaveKey = "social";

        /// <summary>실제 플레이어 친구(서버 플레이어 id). 이름 · UID는 서버를 못 읽을 때 보여 줄 마지막 값.</summary>
        [Serializable]
        public class RealFriend { public string pid, uid, name; }

        [Serializable]
        private class SaveData
        {
            public List<int> friends = new(), requests = new(), recommend = new(), greeted = new();
            public string day = "";
            public List<RealFriend> real = new();
            public List<string> realGreeted = new();
            public List<RealFriend> outgoing = new();   // 보낸 신청(수락 대기, 최대 FriendLink.Slots)
            public List<string> declined = new();       // 거절한 신청(보낸 사람이 보고 지우도록 공개)
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
                if (SaveStore.HasKey(SaveKey))
                {
                    try { _d = JsonUtility.FromJson<SaveData>(SaveStore.GetString(SaveKey)) ?? new SaveData(); }
                    catch { _d = new SaveData(); }
                }
                _d.friends ??= new(); _d.requests ??= new(); _d.recommend ??= new(); _d.greeted ??= new();
                _d.real ??= new(); _d.realGreeted ??= new(); _d.outgoing ??= new(); _d.declined ??= new();
            }
            string today = PlayerShop.PeriodKey(ShopReset.Daily, DateTime.Now);
            if (_d.day == today) return;
            // 하루가 바뀜: 인사 초기화 · 새 신청 도착 · 추천 새로
            bool first = string.IsNullOrEmpty(_d.day);
            _d.day = today;
            _d.greeted.Clear();
            _d.realGreeted.Clear();
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
            SaveStore.SetString(SaveKey, JsonUtility.ToJson(_d));
            SaveStore.Save();
        }

        private void Changed() { Save(); OnChanged?.Invoke(); }

        /// <summary>저장 없이 화면 · 알림만 다시 그림(서버 동기화 뒤).</summary>
        public void Ping() => OnChanged?.Invoke();

        /// <summary>실제 플레이어 친구 목록 · 신청이 바뀜 → 저장 + 서버에 올림.</summary>
        private void RealChanged() { Changed(); FriendLink.MarkDirty(); }

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
        public bool IsFull => FriendCount >= MaxFriends;

        /// <summary>친구 수(가상 지휘관 + 실제 플레이어).</summary>
        public int FriendCount { get { EnsureLoaded(); return _d.friends.Count + _d.real.Count; } }

        public IReadOnlyList<RealFriend> RealFriends { get { EnsureLoaded(); return _d.real; } }
        public bool IsRealFriend(string pid) { EnsureLoaded(); return _d.real.Exists(f => f.pid == pid); }
        public bool GreetedReal(string pid) { EnsureLoaded(); return _d.realGreeted.Contains(pid); }

        public IReadOnlyList<RealFriend> Outgoing { get { EnsureLoaded(); return _d.outgoing; } }
        public IReadOnlyList<string> Declined { get { EnsureLoaded(); return _d.declined; } }
        public bool IsPending(string pid) { EnsureLoaded(); return _d.outgoing.Exists(f => f.pid == pid); }
        public bool IsDeclined(string pid) { EnsureLoaded(); return _d.declined.Contains(pid); }

        public string NameOf(string pid)
        {
            var f = _d.real.Find(x => x.pid == pid) ?? _d.outgoing.Find(x => x.pid == pid);
            return f != null ? f.name : "지휘관";
        }

        /// <summary>실제 플레이어에게 친구 신청(상대가 수락해야 친구). 실패 사유(성공이면 null).</summary>
        public string SendRequest(string pid, string uid, string name)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(pid)) return "찾을 수 없는 지휘관입니다";
            if (!FriendLink.Online) return "친구 신청은 로그인해야 보낼 수 있습니다";
            if (IsRealFriend(pid)) return "이미 친구입니다";
            if (IsPending(pid)) return "이미 신청했습니다. 상대가 수락하면 친구가 됩니다";
            if (FriendCount + _d.outgoing.Count >= MaxFriends) return $"친구는 {MaxFriends}명까지입니다";
            if (_d.outgoing.Count >= FriendLink.Slots) return $"보낸 신청은 {FriendLink.Slots}명까지입니다. 수락 · 거절되거나 취소하면 다시 보낼 수 있습니다";
            _d.outgoing.Add(new RealFriend { pid = pid, uid = uid, name = name });
            _d.declined.Remove(pid);
            RealChanged();
            FriendLink.Instance.RequestSync();   // 상대가 나에게 먼저 신청했으면 바로 친구
            return null;
        }

        public void CancelRequest(string pid)
        {
            if (_d.outgoing.RemoveAll(f => f.pid == pid) > 0) RealChanged();
        }

        /// <summary>받은 신청 수락 → 친구(보낸 사람은 다음 동기화 때 확정). 실패 사유(성공이면 null).</summary>
        public string AcceptReal(string pid, string uid, string name)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(pid)) return "찾을 수 없는 지휘관입니다";
            if (IsRealFriend(pid)) return "이미 친구입니다";
            if (IsFull) return $"친구는 {MaxFriends}명까지입니다";
            _d.real.Add(new RealFriend { pid = pid, uid = uid, name = name });
            _d.declined.Remove(pid);
            _d.outgoing.RemoveAll(f => f.pid == pid);
            FriendLink.Instance.RemoveIncoming(pid);
            RealChanged();
            return null;
        }

        /// <summary>받은 신청 거절(보낸 사람이 보고 신청을 지움).</summary>
        public void DeclineReal(string pid)
        {
            EnsureLoaded();
            if (!_d.declined.Contains(pid)) _d.declined.Add(pid);
            FriendLink.Instance.RemoveIncoming(pid);
            RealChanged();
        }

        /// <summary>(동기화) 상대가 내 신청을 수락함 → 친구로 확정. 새로 친구가 됐으면 true.</summary>
        public bool ConfirmAccepted(string pid)
        {
            var o = _d.outgoing.Find(f => f.pid == pid);
            if (o == null) return false;
            _d.outgoing.Remove(o);
            if (!IsRealFriend(pid)) _d.real.Add(o);
            RealChanged();
            return true;
        }

        /// <summary>(동기화) 상대가 거절함 → 신청 지움.</summary>
        public void DropOutgoing(string pid)
        {
            if (_d.outgoing.RemoveAll(f => f.pid == pid) > 0) RealChanged();
        }

        /// <summary>(동기화) 상대가 친구를 삭제함 → 나도 지움.</summary>
        public void DropFriend(string pid)
        {
            if (_d.real.RemoveAll(f => f.pid == pid) == 0) return;
            _d.realGreeted.Remove(pid);
            RealChanged();
        }

        /// <summary>(동기화) 더 이상 나에게 신청하지 않는 사람은 거절 목록에서 뺌.</summary>
        public void PruneDeclined(ICollection<string> stillRequesting)
        {
            if (_d.declined.RemoveAll(p => !stillRequesting.Contains(p)) > 0) { Save(); FriendLink.MarkDirty(); }
        }

        /// <summary>서버에서 새로 읽은 이름 · UID 기억.</summary>
        public void UpdateReal(string pid, string uid, string name)
        {
            var f = _d.real.Find(x => x.pid == pid);
            if (f == null || (f.uid == uid && f.name == name)) return;
            f.uid = uid; f.name = name;
            Save();
        }

        public void RemoveReal(string pid)
        {
            if (_d.real.RemoveAll(f => f.pid == pid) == 0) return;
            _d.realGreeted.Remove(pid);
            RealChanged();   // 상대도 다음 동기화 때 지움
        }

        /// <summary>실제 플레이어 친구에게 인사(보상은 가상 친구와 같음). 이미 했으면 false.</summary>
        public bool GreetReal(string pid)
        {
            EnsureLoaded();
            if (!IsRealFriend(pid) || _d.realGreeted.Contains(pid)) return false;
            _d.realGreeted.Add(pid);
            Reward(1);
            Changed();
            return true;
        }

        private void Reward(int n)
        {
            var wallet = FindAnyObjectByType<PlayerWallet>();
            if (wallet != null) { wallet.AddStamina(GreetStamina * n); wallet.AddGold(GreetGold * n); }
        }

        /// <summary>오늘 아직 인사하지 않은 친구 수.</summary>
        public int GreetableCount
        {
            get
            {
                EnsureLoaded();
                int n = 0;
                foreach (int f in _d.friends) if (!_d.greeted.Contains(f)) n++;
                foreach (var f in _d.real) if (!_d.realGreeted.Contains(f.pid)) n++;
                return n;
            }
        }

        /// <summary>홈 타일 알림 수(받은 신청 + 인사할 친구).</summary>
        public int BadgeCount => Requests.Count + IncomingReal + GreetableCount;

        /// <summary>실제 플레이어에게 받은 신청 수(마지막 동기화).</summary>
        public static int IncomingReal => FriendLink.Instance.Incoming.Count;

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
        public int GreetAll()
        {
            EnsureLoaded();
            int real = 0;
            foreach (var f in _d.real)
                if (!_d.realGreeted.Contains(f.pid)) { _d.realGreeted.Add(f.pid); real++; }
            if (real > 0) { Reward(real); Save(); }
            int n = GreetMany(new List<int>(_d.friends));
            if (real > 0 && n == 0) OnChanged?.Invoke();
            return n + real;
        }

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
