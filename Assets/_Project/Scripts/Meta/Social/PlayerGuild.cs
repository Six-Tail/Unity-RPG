using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Meta.Shop;

namespace SubcultureRPG.Meta
{
    /// <summary>길드 1곳의 소개(가상 — 번호로 늘 같은 길드).</summary>
    public class GuildInfo
    {
        public int seed;
        public string name, intro;
        public int level, members, minLevel;
    }

    /// <summary>길드원 1명(표시용).</summary>
    public class GuildMember
    {
        public Commander commander;   // 나 자신이면 null
        public string role;           // 길드장 · 부길드장 · 길드원
        public int contribution;      // 이번 주 기여도
        public bool me;
    }

    /// <summary>
    /// 길드: 서버가 없어 길드와 길드원은 가상입니다.
    /// · 추천 길드에 가입(지휘관 레벨 조건) 또는 창설(금화 50,000, 이름 2 ~ 8자) · 탈퇴
    /// · 출석(하루 1번): 금화 5,000 × (1 + 길드 레벨당 10%) + 크리스탈 10, 길드 경험치 +20, 기여도 +20
    /// · 기부(하루 3번): 금화 10,000 → 크리스탈 5, 길드 경험치 +50, 기여도 +50
    /// · 길드원들도 날마다 경험치를 쌓아 길드 레벨이 오름(최대 Lv.10, 다음 레벨까지 1,000 × 레벨)
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(PlayerGuild.Instance).
    /// </summary>
    public class PlayerGuild : MonoBehaviour
    {
        public const int MaxMembers = 30, MaxLevel = 10, CreateCost = 50_000, DonateCost = 10_000, DonatePerDay = 3;
        public const int AttendGold = 5000, AttendCrystals = 10, AttendExp = 20, DonateExp = 50, DonateCrystals = 5;
        public const int MinNameLength = 2, MaxNameLength = 8, RecommendCount = 8;
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
                if (PlayerPrefs.HasKey(SaveKey))
                {
                    try { _d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)) ?? new SaveData(); }
                    catch { _d = new SaveData(); }
                }
            }
            var now = DateTime.Now;
            string today = PlayerShop.PeriodKey(ShopReset.Daily, now), week = PlayerShop.PeriodKey(ShopReset.Weekly, now);
            if (_d.day == today && _d.week == week) return;
            if (_d.day != today)
            {
                // 하루가 바뀜: 출석 · 기부 초기화, 길드원들이 쌓은 경험치(하루치) 반영, 빈자리에 새 길드원
                if (_d.joined && !string.IsNullOrEmpty(_d.day))
                {
                    AddExp(_d.members * 12);
                    if (_d.members < MaxMembers) _d.members += UnityEngine.Random.Range(0, 2);
                }
                _d.day = today;
                _d.attended = false;
                _d.donated = 0;
            }
            if (_d.week != week) { _d.week = week; _d.contribution = 0; }
            Save();
        }

        private void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_d));
            PlayerPrefs.Save();
        }

        private void Changed() { Save(); OnChanged?.Invoke(); }

        // ---------------- 가상 길드 ----------------

        private static readonly string[] GA = { "별빛", "새벽", "푸른 닻", "은빛", "황금", "고요한", "붉은 달", "서리", "폭풍", "천둥" };
        private static readonly string[] GB = { "기사단", "원정대", "항해단", "연합", "수호대", "여단", "길드", "상회" };
        private static readonly string[] Intros =
        {
            "매일 출석만 해도 환영합니다!", "주간 보스 같이 달려요", "초보 지휘관 대환영", "결투장 상위권 목표",
            "느긋하게 즐기는 길드", "기부 열심히 하실 분", "스토리 좋아하는 사람들", "닻을 올리고 별을 향해",
        };

        public static GuildInfo InfoOf(int seed)
        {
            var rng = new System.Random(seed * 6151 + 7);
            var g = new GuildInfo { seed = seed };
            g.name = GA[rng.Next(GA.Length)] + " " + GB[rng.Next(GB.Length)];
            g.level = 1 + rng.Next(8);
            g.members = Mathf.Min(MaxMembers - 1, 8 + g.level * 2 + rng.Next(8));
            g.minLevel = new[] { 1, 1, 5, 10, 20 }[rng.Next(5)];
            g.intro = Intros[rng.Next(Intros.Length)];
            return g;
        }

        /// <summary>추천 길드 목록(날마다 · 새로고침할 때 바뀜).</summary>
        public List<GuildInfo> Recommended()
        {
            EnsureLoaded();
            int h = Commanders.Hash(_d.day) + _d.recommendSalt * 977;
            var list = new List<GuildInfo>();
            for (int i = 0; i < RecommendCount; i++) list.Add(InfoOf((h + i * 131) % 100_000));
            return list;
        }

        public void RefreshRecommend() { _d.recommendSalt++; Changed(); }

        // ---------------- 상태 ----------------

        public bool Joined { get { EnsureLoaded(); return _d.joined; } }
        public bool IsFounder => _d.founder;
        public string Name => _d.name;
        public string Intro => _d.intro;
        public int Level => _d.level;
        public int Exp => _d.exp;
        public int Members => _d.members;
        public int Contribution => _d.contribution;
        public bool Attended { get { EnsureLoaded(); return _d.attended; } }
        public int DonationsLeft { get { EnsureLoaded(); return Mathf.Max(0, DonatePerDay - _d.donated); } }
        public static int ExpForNext(int level) => level >= MaxLevel ? int.MaxValue : 1000 * level;
        public int AttendGoldNow => Mathf.RoundToInt(AttendGold * (1f + 0.1f * (_d.level - 1)));

        /// <summary>홈 타일 알림(출석 안 함 = 1).</summary>
        public int BadgeCount => Joined && !Attended ? 1 : 0;

        private void AddExp(int amount)
        {
            _d.exp += amount;
            while (_d.level < MaxLevel && _d.exp >= ExpForNext(_d.level)) { _d.exp -= ExpForNext(_d.level); _d.level++; }
            if (_d.level >= MaxLevel) _d.exp = 0;
        }

        // ---------------- 가입 · 창설 · 탈퇴 ----------------

        /// <summary>가입. 실패 사유(성공이면 null).</summary>
        public string Join(GuildInfo g)
        {
            EnsureLoaded();
            if (_d.joined) return "이미 길드에 가입해 있습니다";
            if (g == null) return "길드를 찾을 수 없습니다";
            if (PlayerProfile.Instance.Level < g.minLevel) return $"지휘관 Lv.{g.minLevel} 이상만 가입할 수 있습니다";
            if (g.members >= MaxMembers) return "정원이 가득 찼습니다";
            _d.joined = true; _d.founder = false;
            _d.seed = g.seed; _d.name = g.name; _d.intro = g.intro;
            _d.level = g.level; _d.exp = 0; _d.members = g.members + 1; _d.contribution = 0;
            Changed();
            return null;
        }

        public static string CleanName(string raw)
        {
            string s = (raw ?? "").Trim().Replace("<", "").Replace(">", "").Replace("\n", "");
            return s.Length > MaxNameLength ? s.Substring(0, MaxNameLength) : s;
        }

        /// <summary>창설. 실패 사유(성공이면 null).</summary>
        public string Create(string rawName)
        {
            EnsureLoaded();
            if (_d.joined) return "이미 길드에 가입해 있습니다";
            string name = CleanName(rawName);
            if (name.Length < MinNameLength) return $"길드 이름은 {MinNameLength} ~ {MaxNameLength}자입니다";
            var wallet = FindAnyObjectByType<PlayerWallet>();
            if (wallet == null || !wallet.SpendGold(CreateCost)) return $"금화가 부족합니다 (필요 {CreateCost:N0})";
            _d.joined = true; _d.founder = true;
            _d.seed = 100_000 + UnityEngine.Random.Range(0, 900_000);
            _d.name = name; _d.intro = "새로 닻을 올린 길드입니다";
            _d.level = 1; _d.exp = 0; _d.members = 6; _d.contribution = 0;   // 창설 소식을 듣고 온 길드원 5명
            Changed();
            return null;
        }

        public void Leave()
        {
            if (!_d.joined) return;
            _d.joined = false; _d.founder = false; _d.name = ""; _d.contribution = 0;
            Changed();
        }

        // ---------------- 출석 · 기부 ----------------

        public bool Attend()
        {
            EnsureLoaded();
            if (!_d.joined || _d.attended) return false;
            var wallet = FindAnyObjectByType<PlayerWallet>();
            if (wallet != null) { wallet.AddGold(AttendGoldNow); wallet.Add(AttendCrystals); }
            _d.attended = true;
            _d.contribution += AttendExp;
            AddExp(AttendExp);
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
            _d.contribution += DonateExp;
            AddExp(DonateExp);
            Changed();
            return null;
        }

        // ---------------- 길드원 ----------------

        /// <summary>길드원 목록(기여도 높은 순, 나 포함).</summary>
        public List<GuildMember> MemberList(GameDatabase db)
        {
            EnsureLoaded();
            var list = new List<GuildMember>();
            if (!_d.joined) return list;
            int wk = Commanders.Hash(_d.week);
            int dayOfWeek = Mathf.Clamp(7 - (int)(PlayerShop.UntilReset(ShopReset.Weekly, DateTime.Now) ?? TimeSpan.Zero).TotalDays, 1, 7);
            for (int i = 0; i < _d.members - 1; i++)
            {
                int seed = (_d.seed * 53 + i * 7907 + 11) % Commanders.MaxSeed;
                var rng = new System.Random(seed + wk);
                list.Add(new GuildMember
                {
                    commander = Commanders.FromSeed(seed, db),
                    role = !_d.founder && i == 0 ? "길드장" : i <= 2 ? "부길드장" : "길드원",
                    contribution = rng.Next(10, 120) * dayOfWeek,
                });
            }
            list.Add(new GuildMember { me = true, role = _d.founder ? "길드장" : "길드원", contribution = _d.contribution });
            list.Sort((a, b) => b.contribution.CompareTo(a.contribution));
            return list;
        }

        [ContextMenu("테스트: 하루 넘기기(출석 · 기부 초기화)")]
        private void TestNextDay() { _d.day = "test"; EnsureLoaded(); OnChanged?.Invoke(); }

        [ContextMenu("테스트: 길드 경험치 +500")]
        private void TestExp() { AddExp(500); Changed(); }
    }
}
