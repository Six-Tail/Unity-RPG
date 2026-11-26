using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Meta.Shop;
using SubcultureRPG.Meta.Cloud;

namespace SubcultureRPG.Meta
{
    /// <summary>길드원 1명(표시용).</summary>
    public class GuildMember
    {
        public Commander commander;   // 가상 길드원(나 · 실제 길드원이면 null)
        public ProfileSnap snap;      // 실제 길드원 공개 프로필(불러오기 전엔 이름만)
        public string pid = "";       // 실제 길드원 플레이어 id
        public string role;           // 길드장 · 부길드장 · 길드원
        public long contribution;     // 이번 주 기여도
        public long bossDamage;       // 이번 주 길드 보스 피해
        public int warStars;          // 이번 주 길드전 별(합)
        public int[] warDetail;       // 상대 길드원별 별(실제 길드원)
        public bool me;
        public bool Real => !string.IsNullOrEmpty(pid);
    }

    /// <summary>
    /// 길드: 가상 길드와 실제 플레이어 길드를 함께 씁니다(docs/guild.md).
    /// · 가상 길드: 서버 없이 번호로 늘 같은 길드 · 길드원(가상 지휘관). 바로 가입.
    /// · 실제 길드: 로그인한 플레이어가 만든 길드. 각자 자기 Cloud Save 공개 데이터만 써서 맞춤(<see cref="GuildLink"/>).
    ///   자유 가입이면 바로, 승인제면 길드장이 수락해야 가입. 탈퇴 · 추방 · 해산은 상대가 다음 동기화 때 알아챔.
    /// · 출석(하루 1번): 금화 5,000 × (1 + 길드 레벨당 10%) + 크리스탈 10, 기여도 +20
    /// · 기부(하루 3번): 금화 10,000 → 크리스탈 5, 기여도 +50
    /// · 길드 경험치: 가상 길드 = 출석 · 기부 + 가상 길드원이 날마다 쌓음, 실제 길드 = 길드원 누적 기여도의 합(길드장이 계산)
    /// · 길드 보스(<see cref="GuildBoss"/>) · 길드전(<see cref="GuildWar"/>) — 주간, 상대 · 순위에는 가상 길드가 함께
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(PlayerGuild.Instance).
    /// </summary>
    public class PlayerGuild : MonoBehaviour
    {
        public const int MaxMembers = 30, MaxLevel = 10, CreateCost = 50_000, DonateCost = 10_000, DonatePerDay = 3;
        public const int AttendGold = 5000, AttendCrystals = 10, AttendExp = 20, DonateExp = 50, DonateCrystals = 5;
        public const int MinNameLength = 2, MaxNameLength = 8, MaxIntroLength = 30, RecommendCount = 8;
        private const string SaveKey = "guild";

        [Serializable]
        private class SaveData
        {
            public bool joined, founder;
            public int seed, level = 1, exp, members = 1, contribution;
            public string name = "", intro = "";
            public string day = "", week = "";
            public bool attended;
            public int donated;
            public int recommendSalt;

            // 실제 길드
            public bool real;                 // 지금 길드가 실제 길드
            public string gid = "";           // 실제 길드 id(= 길드장 플레이어 id)
            public string reqGid = "", reqName = "";   // 가입 신청 중인 실제 길드
            public long contribTotal;         // 이 길드에서 쌓은 누적 기여도(실제 길드 경험치 = 길드원 누적 합)
            public long joinedAt;             // 가입한 때(UTC ticks)

            // 길드 보스 · 길드전(주간)
            public long bossDamage;           // 이번 주 내 길드 보스 피해
            public int bossUsed; public string bossDay = "";
            public int bossClaims;            // 받은 단계 보상(비트)
            public int[] warStars = new int[GuildWar.Defenders];   // 이번 주 상대 길드원별 내 최고 별
            public int warUsed; public string warDay = "";
            public int warRating;             // 길드전 점수(0 = 아직 없음 → 시작 점수). 실제 길드는 길드장 값을 따름

            // 지난주 정산용(이번 주에 마지막으로 본 길드 기록)
            public string seenWeek = "";
            public long seenBossTotal;
            public int seenOurStars, seenTheirStars, seenRank, seenRankTotal;
            public string seenOpponent = "";
        }

        private SaveData _d = new();
        private bool _loaded;
        public event Action OnChanged;

        private static PlayerGuild _instance;
        public static PlayerGuild Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<PlayerGuild>();
                if (_instance == null) _instance = new GameObject("PlayerGuild").AddComponent<PlayerGuild>();
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
                if (_d.warStars == null || _d.warStars.Length != GuildWar.Defenders) _d.warStars = new int[GuildWar.Defenders];
            }
            var now = DateTime.Now;
            string today = PlayerShop.PeriodKey(ShopReset.Daily, now), week = PlayerShop.PeriodKey(ShopReset.Weekly, now);
            if (_d.day == today && _d.week == week) return;
            if (_d.day != today)
            {
                // 하루가 바뀜: 출석 · 기부 초기화, 가상 길드는 길드원들이 쌓은 경험치(하루치) 반영 · 빈자리에 새 길드원
                if (_d.joined && !_d.real && !string.IsNullOrEmpty(_d.day))
                {
                    AddExp(_d.members * 12);
                    if (_d.members < MaxMembers) _d.members += UnityEngine.Random.Range(0, 2);
                }
                _d.day = today;
                _d.attended = false;
                _d.donated = 0;
            }
            if (_d.week != week)
            {
                // 주가 바뀜: 지난주 길드 보스 순위 · 길드전 결과 정산(우편) → 주간 기록 초기화
                if (_d.joined && !string.IsNullOrEmpty(_d.seenWeek) && _d.seenWeek == _d.week)
                {
                    GuildBoss.SettleWeek(_d.seenBossTotal, _d.bossDamage, _d.seenRank, _d.seenRankTotal);
                    int before = LocalWarRating;
                    int delta = GuildWar.RatingDelta(_d.seenOurStars, _d.seenTheirStars);
                    if (_d.seenOurStars > 0 || _d.seenTheirStars > 0) _d.warRating = Mathf.Max(0, before + delta);
                    GuildWar.SettleWeek(_d.seenOurStars, _d.seenTheirStars, _d.seenOpponent, MyWarTotal(), before, _d.warRating);
                }
                _d.week = week;
                _d.contribution = 0;
                _d.bossDamage = 0; _d.bossClaims = 0;
                _d.warStars = new int[GuildWar.Defenders];
                _d.seenWeek = ""; _d.seenBossTotal = 0; _d.seenOurStars = 0; _d.seenTheirStars = 0; _d.seenRank = 0; _d.seenRankTotal = 0; _d.seenOpponent = "";
                GuildLink.MarkDirty();
            }
            Save();
        }

        private void Save()
        {
            SaveStore.SetString(SaveKey, JsonUtility.ToJson(_d));
            SaveStore.Save();
        }

        internal void Changed() { Save(); OnChanged?.Invoke(); }

        // ---------------- 추천 · 가상 길드 ----------------

        public static GuildInfo InfoOf(int seed) => VirtualGuilds.Info(seed);

        /// <summary>추천 가상 길드(날마다 · 새로고침할 때 바뀜).</summary>
        public List<GuildInfo> Recommended()
        {
            EnsureLoaded();
            int h = Commanders.Hash(_d.day) + _d.recommendSalt * 977;
            var list = new List<GuildInfo>();
            for (int i = 0; i < RecommendCount; i++) list.Add(InfoOf((h + i * 131) % 100_000 + VirtualGuilds.RankPool + 1));
            return list;
        }

        public void RefreshRecommend() { _d.recommendSalt++; Changed(); GuildLink.Instance.RequestSearch(); }

        // ---------------- 상태 ----------------

        public bool Joined { get { EnsureLoaded(); return _d.joined; } }
        public bool IsReal => _d.joined && _d.real;
        public string Gid => _d.real ? _d.gid : "";
        /// <summary>내가 길드장(가상 길드를 만든 사람 · 실제 길드장).</summary>
        public bool IsFounder => _d.founder;
        public bool IsLeader => _d.joined && _d.founder;
        public int Seed => _d.seed;
        public string Name => _d.name;
        public string Intro => _d.intro;
        public int Level => _d.level;
        public int Exp => _d.exp;
        public int Members => _d.members;
        public int Contribution => _d.contribution;
        public long ContributionTotal => _d.contribTotal;
        public bool Attended { get { EnsureLoaded(); return _d.attended; } }
        public int DonationsLeft { get { EnsureLoaded(); return Mathf.Max(0, DonatePerDay - _d.donated); } }
        public static int ExpForNext(int level) => level >= MaxLevel ? int.MaxValue : 1000 * level;
        public int AttendGoldNow => Mathf.RoundToInt(AttendGold * (1f + 0.1f * (_d.level - 1)));
        public string Week { get { EnsureLoaded(); return _d.week; } }

        /// <summary>가입 신청 중인 실제 길드(없으면 "").</summary>
        public string RequestedGid => _d.reqGid;
        public string RequestedName => _d.reqName;
        public long JoinedAt => _d.joinedAt;

        /// <summary>실제 길드: 길드장 공개 데이터 · 길드원 · 받은 가입 신청(마지막 동기화 값).</summary>
        public GuildLink.Info RealInfo { get; internal set; }
        public List<GuildMember> RealMembers { get; internal set; } = new();
        public List<ProfileSnap> Requests { get; internal set; } = new();

        /// <summary>동기화 중 생긴 알림(가입 수락 · 거절 · 추방 · 해산) — 길드 화면이 보여 주고 비움.</summary>
        public readonly List<string> Notices = new();

        /// <summary>홈 타일 알림(출석 안 함 · 받은 가입 신청 · 받을 길드 보스 보상).</summary>
        public int BadgeCount
        {
            get
            {
                if (!Joined) return 0;
                int n = !Attended ? 1 : 0;
                if (IsLeader && IsReal) n += Requests.Count;
                n += GuildBoss.ClaimableCount;
                return n;
            }
        }

        private void AddExp(int amount)
        {
            _d.exp += amount;
            while (_d.level < MaxLevel && _d.exp >= ExpForNext(_d.level)) { _d.exp -= ExpForNext(_d.level); _d.level++; }
            if (_d.level >= MaxLevel) _d.exp = 0;
        }

        /// <summary>누적 경험치 → (레벨, 이번 레벨 안의 경험치).</summary>
        public static (int level, int exp) LevelOf(long totalExp)
        {
            int level = 1;
            long e = Math.Max(0, totalExp);
            while (level < MaxLevel && e >= ExpForNext(level)) { e -= ExpForNext(level); level++; }
            return (level, level >= MaxLevel ? 0 : (int)e);
        }

        // ---------------- 가입 · 창설 · 탈퇴 ----------------

        public static string CleanName(string raw, int max = MaxNameLength)
        {
            string s = (raw ?? "").Trim().Replace("<", "").Replace(">", "").Replace("\n", "").Replace(",", "");
            return s.Length > max ? s.Substring(0, max) : s;
        }

        /// <summary>가입(가상 길드 = 바로, 실제 길드 = 자유 가입이면 바로 · 승인제면 신청). 실패 사유(성공이면 null).</summary>
        public string Join(GuildInfo g)
        {
            EnsureLoaded();
            if (_d.joined) return "이미 길드에 가입해 있습니다";
            if (g == null) return "길드를 찾을 수 없습니다";
            if (PlayerProfile.Instance.Level < g.minLevel) return $"지휘관 Lv.{g.minLevel} 이상만 가입할 수 있습니다";
            if (g.members >= MaxMembers) return "정원이 가득 찼습니다";
            if (g.Real)
            {
                if (!GuildLink.Online) return "실제 플레이어 길드는 로그인해야 가입할 수 있습니다";
                if (g.gid == CloudAccount.PlayerId) return "내가 만든 길드입니다";
                if (!g.free)
                {
                    _d.reqGid = g.gid; _d.reqName = g.name;
                    GuildLink.MarkDirty();
                    Changed();
                    return null;
                }
                EnterReal(g, false);
                return null;
            }
            _d.joined = true; _d.founder = false; _d.real = false; _d.gid = "";
            _d.reqGid = ""; _d.reqName = "";
            _d.seed = g.seed; _d.name = g.name; _d.intro = g.intro;
            _d.level = g.level; _d.exp = 0; _d.members = g.members + 1; _d.contribution = 0;
            _d.joinedAt = DateTime.UtcNow.Ticks;
            Changed();
            return null;
        }

        /// <summary>실제 길드에 들어감(자유 가입 · 신청 수락 확인 · 내가 창설).</summary>
        internal void EnterReal(GuildInfo g, bool leader)
        {
            _d.joined = true; _d.founder = leader; _d.real = true; _d.gid = g.gid; _d.seed = -1;
            _d.reqGid = ""; _d.reqName = "";
            _d.name = g.name; _d.intro = g.intro ?? ""; _d.level = Mathf.Max(1, g.level); _d.exp = 0; _d.members = Mathf.Max(1, g.members + (leader ? 0 : 1));
            _d.contribution = 0; _d.contribTotal = 0;
            _d.joinedAt = DateTime.UtcNow.Ticks;
            RealInfo = null; RealMembers = new(); Requests = new();
            GuildLink.MarkDirty();
            GuildLink.Instance.RequestSync();
            ChatService.Instance.JoinGuild(_d.gid);
            Changed();
        }

        public void CancelRequest()
        {
            if (string.IsNullOrEmpty(_d.reqGid)) return;
            _d.reqGid = ""; _d.reqName = "";
            GuildLink.MarkDirty();
            Changed();
        }

        internal void ClearRequest(string notice)
        {
            if (string.IsNullOrEmpty(_d.reqGid)) return;
            _d.reqGid = ""; _d.reqName = "";
            if (!string.IsNullOrEmpty(notice)) Notices.Add(notice);
            GuildLink.MarkDirty();
            Changed();
        }

        /// <summary>
        /// 창설: 로그인 중이면 실제 길드(다른 플레이어가 찾아 가입), 아니면 가상 길드. 금화 50,000. 실패 사유(성공이면 null).
        /// </summary>
        public string Create(string rawName, bool free = true, int minLevel = 1)
        {
            EnsureLoaded();
            if (_d.joined) return "이미 길드에 가입해 있습니다";
            string name = CleanName(rawName);
            if (name.Length < MinNameLength) return $"길드 이름은 {MinNameLength} ~ {MaxNameLength}자입니다";
            var wallet = FindAnyObjectByType<PlayerWallet>();
            if (wallet == null || !wallet.SpendGold(CreateCost)) return $"금화가 부족합니다 (필요 {CreateCost:N0})";
            if (GuildLink.Online)
            {
                var g = new GuildInfo { seed = -1, gid = CloudAccount.PlayerId, name = name, intro = "새로 닻을 올린 길드입니다", level = 1, members = 1, minLevel = Mathf.Max(1, minLevel), free = free };
                _d.intro = g.intro;
                EnterReal(g, true);
                GuildLink.Instance.CreateInfo(g);
                return null;
            }
            _d.joined = true; _d.founder = true; _d.real = false; _d.gid = "";
            _d.seed = 100_000 + UnityEngine.Random.Range(0, 900_000);
            _d.name = name; _d.intro = "새로 닻을 올린 길드입니다";
            _d.level = 1; _d.exp = 0; _d.members = 6; _d.contribution = 0;   // 창설 소식을 듣고 온 길드원 5명
            _d.joinedAt = DateTime.UtcNow.Ticks;
            Changed();
            return null;
        }

        /// <summary>탈퇴(실제 길드장이면 해산).</summary>
        public void Leave()
        {
            if (!_d.joined) return;
            bool real = _d.real, leader = _d.founder;
            string gid = _d.gid;
            Reset();
            if (real)
            {
                if (leader) GuildLink.Instance.Disband();
                ChatService.Instance.LeaveGuild(gid);
                GuildLink.MarkDirty();
            }
            Changed();
        }

        /// <summary>실제 길드에서 나가게 됨(추방 · 해산 · 정원 초과) — 동기화가 부름.</summary>
        internal void Removed(string notice)
        {
            if (!_d.joined || !_d.real) return;
            string gid = _d.gid;
            Reset();
            if (!string.IsNullOrEmpty(notice)) Notices.Add(notice);
            ChatService.Instance.LeaveGuild(gid);
            GuildLink.MarkDirty();
            Changed();
        }

        private void Reset()
        {
            _d.joined = false; _d.founder = false; _d.real = false; _d.gid = "";
            _d.name = ""; _d.contribution = 0; _d.contribTotal = 0;
            _d.bossClaims = 0;
            RealInfo = null; RealMembers = new(); Requests = new();
        }

        /// <summary>실제 길드 정보 반영(길드장 공개 데이터를 읽은 뒤).</summary>
        internal void ApplyRealInfo(GuildLink.Info info)
        {
            RealInfo = info;
            if (info == null) return;
            if (!_d.founder && info.warRating > 0) _d.warRating = info.warRating;   // 길드원은 길드장의 점수를 따름
            var (lv, ex) = LevelOf(info.exp);
            _d.name = info.name; _d.intro = info.intro ?? ""; _d.level = lv; _d.exp = ex;
            _d.members = Mathf.Max(1, info.members != null ? info.members.Count + 1 : 1);
        }

        /// <summary>길드장: 소개 · 가입 방식 · 가입 레벨 바꾸기.</summary>
        public string EditRecruit(string intro, bool free, int minLevel)
        {
            if (!IsLeader || !IsReal) return "길드장만 바꿀 수 있습니다";
            _d.intro = CleanName(intro, MaxIntroLength);
            GuildLink.Instance.EditInfo(_d.intro, free, Mathf.Clamp(minLevel, 1, 80));
            Changed();
            return null;
        }

        // ---------------- 출석 · 기부 ----------------

        private void Contribute(int amount)
        {
            _d.contribution += amount;
            _d.contribTotal += amount;
            if (_d.real) GuildLink.MarkDirty();
            else AddExp(amount);
        }

        public bool Attend()
        {
            EnsureLoaded();
            if (!_d.joined || _d.attended) return false;
            var wallet = FindAnyObjectByType<PlayerWallet>();
            if (wallet != null) { wallet.AddGold(AttendGoldNow); wallet.Add(AttendCrystals); }
            _d.attended = true;
            Contribute(AttendExp);
            Changed();
            return true;
        }

        /// <summary>기부. 실패 사유(성공이면 null).</summary>
        public string Donate()
        {
            EnsureLoaded();
            if (!_d.joined) return "길드에 가입해 있지 않습니다";
            if (_d.donated >= DonatePerDay) return "오늘 기부 횟수를 모두 썼습니다";
            var wallet = FindAnyObjectByType<PlayerWallet>();
            if (wallet == null || !wallet.SpendGold(DonateCost)) return $"금화가 부족합니다 (필요 {DonateCost:N0})";
            wallet.Add(DonateCrystals);
            _d.donated++;
            Contribute(DonateExp);
            Changed();
            return null;
        }

        // ---------------- 길드 보스 · 길드전 기록 ----------------

        public long MyBossDamage { get { EnsureLoaded(); return _d.bossDamage; } }
        public int BossUsedToday { get { EnsureLoaded(); return _d.bossDay == _d.day ? _d.bossUsed : 0; } }
        public int WarUsedToday { get { EnsureLoaded(); return _d.warDay == _d.day ? _d.warUsed : 0; } }
        public int WarStarsOf(int i) { EnsureLoaded(); return i >= 0 && i < _d.warStars.Length ? _d.warStars[i] : 0; }
        public int[] MyWarStars { get { EnsureLoaded(); return (int[])_d.warStars.Clone(); } }
        public int MyWarTotal() { int n = 0; foreach (int s in _d.warStars) n += s; return n; }

        private int LocalWarRating => _d.warRating > 0 ? _d.warRating : GuildWar.StartRating;

        /// <summary>우리 길드 길드전 점수(실제 길드 = 길드장이 올린 값, 그 밖 = 이 PC 기록).</summary>
        public int WarRating
        {
            get
            {
                EnsureLoaded();
                if (_d.real && !_d.founder && RealInfo != null && RealInfo.warRating > 0) return RealInfo.warRating;
                return LocalWarRating;
            }
        }
        public bool BossClaimed(int stage) => (_d.bossClaims & (1 << stage)) != 0;

        internal void UseBossAttempt() { EnsureLoaded(); if (_d.bossDay != _d.day) { _d.bossDay = _d.day; _d.bossUsed = 0; } _d.bossUsed++; Changed(); }
        internal void RefundBossAttempt() { if (_d.bossDay == _d.day && _d.bossUsed > 0) { _d.bossUsed--; Changed(); } }
        internal void AddBossDamage(long dmg) { EnsureLoaded(); _d.bossDamage += Math.Max(0, dmg); GuildLink.MarkDirty(); Changed(); }
        internal void MarkBossClaimed(int stage) { _d.bossClaims |= 1 << stage; Changed(); }

        internal void UseWarAttempt() { EnsureLoaded(); if (_d.warDay != _d.day) { _d.warDay = _d.day; _d.warUsed = 0; } _d.warUsed++; Changed(); }
        internal void RefundWarAttempt() { if (_d.warDay == _d.day && _d.warUsed > 0) { _d.warUsed--; Changed(); } }
        internal bool SetWarStars(int i, int stars)
        {
            EnsureLoaded();
            if (i < 0 || i >= _d.warStars.Length || stars <= _d.warStars[i]) return false;
            _d.warStars[i] = stars;
            GuildLink.MarkDirty();
            Changed();
            return true;
        }

        /// <summary>이번 주 마지막으로 본 길드 기록(주가 바뀔 때 지난주 정산에 씀).</summary>
        internal void Seen(long bossTotal, int ourStars, int theirStars, string opponent, int rank = 0, int rankTotal = 0)
        {
            EnsureLoaded();
            _d.seenWeek = _d.week;
            _d.seenBossTotal = Math.Max(_d.seenBossTotal, bossTotal);
            _d.seenOurStars = ourStars; _d.seenTheirStars = theirStars; _d.seenOpponent = opponent ?? "";
            if (rank > 0) { _d.seenRank = rank; _d.seenRankTotal = rankTotal; }
            Save();
        }

        /// <summary>지금 길드 기록(길드 보스 합계 · 길드전 별 · 순위)을 기억 — 주가 바뀌면 이 값으로 지난주를 정산.</summary>
        public void RefreshSeen()
        {
            if (!Joined) return;
            var (rank, total) = GuildRanking.MyRank(GuildRanking.Tab.Boss);
            Seen(GuildBossTotal(), GuildWarStars(), GuildWar.TheirStars(), GuildWar.Opponent().name, rank, total);
        }

        // ---------------- 길드원 ----------------

        /// <summary>가상 길드원 i번(가상 길드).</summary>
        private Commander VirtualMember(int i, GameDatabase db) => Commanders.FromSeed((_d.seed * 53 + i * 7907 + 11) % Commanders.MaxSeed, db);

        /// <summary>가상 길드원 한 명의 이번 주 기록(길드 보스 피해 · 길드전 별) — 길드 전체 합과 맞게 나눔.</summary>
        private (long boss, int stars, long contrib) VirtualShare(int i, int count)
        {
            var rng = new System.Random(_d.seed * 97 + i * 131 + Commanders.Hash(_d.week));
            float p = VirtualGuilds.WeekProgress();
            double w = 0.4 + rng.NextDouble();
            long boss = (long)(VirtualGuilds.MemberBossWeek(_d.level) * w * Math.Pow(p, 0.9));
            int dayOfWeek = Mathf.Clamp(Mathf.CeilToInt(p * 7f), 1, 7);
            return (boss, Mathf.Clamp(Mathf.RoundToInt((float)(w * 3.5 * dayOfWeek / 7.0)), 0, GuildWar.MaxStars), rng.Next(10, 120) * dayOfWeek);
        }

        /// <summary>길드원 목록(기여도 높은 순, 나 포함).</summary>
        public List<GuildMember> MemberList(GameDatabase db)
        {
            EnsureLoaded();
            var list = new List<GuildMember>();
            if (!_d.joined) return list;
            if (_d.real)
            {
                foreach (var m in RealMembers) if (!m.me) list.Add(m);
                list.Add(new GuildMember { me = true, pid = CloudAccount.PlayerId, role = _d.founder ? "길드장" : "길드원", contribution = _d.contribution, bossDamage = _d.bossDamage, warStars = MyWarTotal() });
            }
            else
            {
                for (int i = 0; i < _d.members - 1; i++)
                {
                    var (boss, stars, contrib) = VirtualShare(i, _d.members - 1);
                    list.Add(new GuildMember
                    {
                        commander = VirtualMember(i, db),
                        role = !_d.founder && i == 0 ? "길드장" : i <= 2 ? "부길드장" : "길드원",
                        contribution = contrib, bossDamage = boss, warStars = stars,
                    });
                }
                list.Add(new GuildMember { me = true, role = _d.founder ? "길드장" : "길드원", contribution = _d.contribution, bossDamage = _d.bossDamage, warStars = MyWarTotal() });
            }
            list.Sort((a, b) => b.contribution.CompareTo(a.contribution));
            return list;
        }

        /// <summary>이번 주 길드 보스 누적 피해(길드 전체).</summary>
        public long GuildBossTotal()
        {
            EnsureLoaded();
            if (!_d.joined) return 0;
            long sum = _d.bossDamage;
            if (_d.real) { foreach (var m in RealMembers) if (!m.me) sum += m.bossDamage; }
            else for (int i = 0; i < _d.members - 1; i++) sum += VirtualShare(i, _d.members - 1).boss;
            return sum;
        }

        /// <summary>
        /// 이번 주 길드전에서 우리 길드가 얻은 별(상대 길드원별 최고 별의 합, 0 ~ 30).
        /// 실제 길드 = 길드원들의 상대별 최고 별, 가상 길드 = 나 + 가상 길드원들이 날마다 공략한 만큼.
        /// </summary>
        public int GuildWarStars()
        {
            EnsureLoaded();
            if (!_d.joined) return 0;
            var best = (int[])_d.warStars.Clone();
            if (_d.real)
            {
                foreach (var m in RealMembers)
                {
                    if (m.me || m.warDetail == null) continue;
                    for (int i = 0; i < best.Length && i < m.warDetail.Length; i++) best[i] = Mathf.Max(best[i], m.warDetail[i]);
                }
            }
            else
            {
                // 가상 길드원들: 약한 상대(앞 번호)부터 날마다 차례로 공략
                float p = VirtualGuilds.WeekProgress();
                var rng = new System.Random(_d.seed * 389 + Commanders.Hash(_d.week));
                double power = Math.Min(1.0, 0.35 + 0.05 * _d.level + 0.2 * rng.NextDouble());
                for (int i = 0; i < best.Length; i++)
                {
                    double reach = (1.0 - i / (double)best.Length) * power * Math.Min(1.0, p * 1.4);
                    int s = reach > 0.66 ? 3 : reach > 0.42 ? 2 : reach > 0.2 ? 1 : 0;
                    best[i] = Mathf.Max(best[i], s);
                }
            }
            int sum = 0;
            foreach (int s in best) sum += s;
            return sum;
        }

        [ContextMenu("테스트: 하루 넘기기(출석 · 기부 초기화)")]
        private void TestNextDay() { _d.day = "test"; EnsureLoaded(); OnChanged?.Invoke(); }

        [ContextMenu("테스트: 길드 경험치 +500")]
        private void TestExp() { if (_d.real) { _d.contribTotal += 500; GuildLink.MarkDirty(); } else AddExp(500); Changed(); }

        [ContextMenu("테스트: 주 넘기기(지난주 정산)")]
        private void TestNextWeek() { _d.seenWeek = "test"; _d.week = "test"; _d.day = "test"; EnsureLoaded(); OnChanged?.Invoke(); }
    }
}
