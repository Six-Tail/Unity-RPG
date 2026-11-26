using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;
using SubcultureRPG.Meta.Cloud;
using SubcultureRPG.Meta.Shop;
using PlayerSaveOptions = Unity.Services.CloudSave.Models.Data.Player.SaveOptions;
using PlayerQueryOptions = Unity.Services.CloudSave.Models.Data.Player.QueryOptions;
using PublicWrite = Unity.Services.CloudSave.Models.Data.Player.PublicWriteAccessClassOptions;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 월드 레이드(서버 공유 보스): 서버의 모든 플레이어가 같은 보스(Lv.100 고정)의 체력을 한 주 동안 함께 깎음.
    ///
    /// · 공유 체력: 각 플레이어가 이번 주에 준 피해를 Cloud Save '공개' 플레이어 데이터(wr_week · wr_dmg · wr_name)에 올리고,
    ///   모든 플레이어의 값을 검색(Query)해 더함 → 깎인 체력. (대시보드 Cloud Save에 wr_week · wr_pweek 인덱스가 있어야 함 — docs/world-raid.md)
    /// · 공유 보상: 깎인 체력 20% · 40% · 60% · 80% · 100%마다 1번씩(총 5번), 뒤로 갈수록 좋음. 참여하지 않았어도 모든 플레이어가 받음
    /// · 순위: 이번 주 준 피해 순. 주가 끝나면(월요일 05:00) 지난주 순위로 보상을 우편함에:
    ///   1 · 2 · 3위 = 칭호 · 테두리 + 개별 보상, 4~10위 · 11~30위 · 31~50위 · 51~100위 = 차등 보상
    /// · 도전: 하루 2회(05:00 충전), 로그인해야 함. 전투 틀(5분 · 격노)은 주간 보스와 같고 극악 배율
    /// · 지난주 기록이 사라지지 않도록, 새 주에 처음 올릴 때 이전 값을 wr_pweek · wr_pdmg · wr_pname으로 옮겨 둠
    /// · 가상 지휘관 60명도 함께 싸움(<see cref="VirtualEntries"/>): 주마다 같은 명단 · 같은 기록(모든 PC에서 똑같이 계산),
    ///   하루 2번씩 도전한 것처럼 기록이 계단식으로 늘어 주가 끝날 때 최대 체력의 92%를 깎음 → 실제 플레이어가 적거나 약해도
    ///   공유 보상 4단계까지는 받고, 마지막 100%는 실제 플레이어가 보태야 함. 순위표 · 공유 체력 · 지난주 순위 정산에 모두 포함
    /// </summary>
    public class ServerRaid : MonoBehaviour
    {
        public const string BossName = "종언의 거신";
        public const int BossLevel = 100;
        public const int AttemptsPerDay = 2;
        /// <summary>
        /// 보스 공유 체력(튜닝 지점). 능력치 10배 뒤 최종 육성 1명이 한 판 약 1.8억 → 그런 플레이어 4명이 하루 2번씩 일주일(56판) ≈ 100억.
        /// 최종까지 키운 플레이어가 여러 명이어도 하루 이틀에 끝나지 않게(주간 보스보다 훨씬 크게) 잡음.
        /// </summary>
        public const long MaxHp = 10_000_000_000;
        public const int RankLimit = 100;

        /// <summary>
        /// 기록 검증(조작 방지): 한 판 피해 상한(최종 육성 한 판 약 1.8억의 3배 남짓) · 한 주 상한(= 한 판 상한 × 하루 도전 × 7일).
        /// 상한을 넘는 다른 플레이어 기록은 순위표 · 공유 체력에서 빼고, 내 한 판 기록은 상한으로 자름.
        /// </summary>
        public const long MaxRunDamage = 600_000_000;
        public static long MaxWeekDamage => MaxRunDamage * AttemptsPerDay * 7;
        public static bool Plausible(long weekDamage) => weekDamage > 0 && weekDamage <= MaxWeekDamage;

        /// <summary>함께 싸우는 가상 지휘관 수와, 주가 끝날 때까지 그들이 깎는 몫(최대 체력 대비) — 튜닝 지점.</summary>
        public const int VirtualCount = 1000;
        public const int AroundSpan = 3;
        public const float VirtualShare = 0.92f;
        private const string SaveKey = "wraid";

        // 클라우드 공개 데이터 키(대시보드 인덱스와 같아야 함)
        public const string KWeek = "wr_week", KDmg = "wr_dmg", KName = "wr_name", KPWeek = "wr_pweek", KPDmg = "wr_pdmg", KPName = "wr_pname";

        // ---------------- 보상(튜닝 지점) ----------------

        public class Reward
        {
            public string label;
            public Func<RewardBundle> bundle;
        }

        /// <summary>공유 체력 보상(20% 단위, 총 5번).</summary>
        public static readonly Reward[] HpRewards =
        {
            new() { label = "금화 50,000 · 크리스탈 100", bundle = () => new RewardBundle().Gold(50000).Crystals(100) },
            new() { label = "대형 경험치 물약 ×10 · 상급 스킬 교본 ×3", bundle = () => new RewardBundle().Mat(MaterialType.ExpLarge, 10).Mat(MaterialType.SkillBookPurple, 3) },
            new() { label = "크리스탈 200 · 장비 정수 100", bundle = () => new RewardBundle().Crystals(200).Mat(MaterialType.EquipEssence, 100) },
            new() { label = "전설의 비전서 ×1 · 재련석 ×1", bundle = () => new RewardBundle().Mat(MaterialType.SkillLegend, 1).Mat(MaterialType.RefineStone, 1) },
            new() { label = "픽업 뽑기권 ×2 · 크리스탈 300 · 재련석 ×1", bundle = () => new RewardBundle().Pickup(2).Crystals(300).Mat(MaterialType.RefineStone, 1) },
        };

        public class RankTier
        {
            public int from, to;            // 순위 범위(포함)
            public string title, frame;     // 칭호 · 테두리 id(1 · 2 · 3위만)
            public float pct;               // 0보다 크면 퍼센트 구간(100위 밖 · 상위 pct 이내)
            public string label;
            public Func<RewardBundle> bundle;
        }

        /// <summary>지난주 순위 보상(우편함).</summary>
        public static readonly RankTier[] RankTiers =
        {
            new() { from = 1, to = 1, title = "tw_1", frame = "fw_1", label = "칭호 '종언을 끊어낸 자' · 테두리 · 크리스탈 1,000 · 픽업 뽑기권 ×3 · 재련석 ×2",
                bundle = () => new RewardBundle().Crystals(1000).Pickup(3).Mat(MaterialType.RefineStone, 2) },
            new() { from = 2, to = 2, title = "tw_2", frame = "fw_2", label = "칭호 '거신을 꺾은 자' · 테두리 · 크리스탈 800 · 픽업 뽑기권 ×2 · 재련석 ×2",
                bundle = () => new RewardBundle().Crystals(800).Pickup(2).Mat(MaterialType.RefineStone, 2) },
            new() { from = 3, to = 3, title = "tw_3", frame = "fw_3", label = "칭호 '거신에 맞선 자' · 테두리 · 크리스탈 600 · 픽업 뽑기권 ×2 · 재련석 ×1",
                bundle = () => new RewardBundle().Crystals(600).Pickup(2).Mat(MaterialType.RefineStone, 1) },
            new() { from = 4, to = 10, label = "크리스탈 400 · 픽업 뽑기권 ×1 · 재련석 ×1",
                bundle = () => new RewardBundle().Crystals(400).Pickup(1).Mat(MaterialType.RefineStone, 1) },
            new() { from = 11, to = 30, label = "크리스탈 300 · 픽업 뽑기권 ×1",
                bundle = () => new RewardBundle().Crystals(300).Pickup(1) },
            new() { from = 31, to = 50, label = "크리스탈 200 · 장비 정수 100",
                bundle = () => new RewardBundle().Crystals(200).Mat(MaterialType.EquipEssence, 100) },
            new() { from = 51, to = 100, label = "크리스탈 100 · 금화 50,000",
                bundle = () => new RewardBundle().Crystals(100).Gold(50000) },
            // 100위 밖: 퍼센트 구간
            new() { from = 101, to = int.MaxValue, pct = 0.10f, label = "크리스탈 80 · 금화 30,000",
                bundle = () => new RewardBundle().Crystals(80).Gold(30000) },
            new() { from = 101, to = int.MaxValue, pct = 0.30f, label = "크리스탈 50 · 금화 20,000",
                bundle = () => new RewardBundle().Crystals(50).Gold(20000) },
            new() { from = 101, to = int.MaxValue, pct = 1f, label = "금화 10,000 (참여)",
                bundle = () => new RewardBundle().Gold(10000) },
        };

        /// <summary>순위 보상 구간(total = 그 주 참여 인원, 퍼센트 구간에 씀).</summary>
        public static RankTier TierOf(int rank, int total = 0)
        {
            foreach (var t in RankTiers)
            {
                if (rank < t.from || rank > t.to) continue;
                if (t.pct <= 0f) return t;
                if (rank <= Mathf.CeilToInt(Mathf.Max(total, rank) * t.pct)) return t;
            }
            return null;
        }

        public static string RangeLabel(RankTier t) =>
            t.pct > 0f ? (t.pct >= 1f ? "그 밖 참여자" : $"상위 {t.pct * 100f:0}%") : t.from == t.to ? $"{t.from}위" : $"{t.from} ~ {t.to}위";

        // ---------------- 세이브 ----------------

        [Serializable]
        private class SaveData
        {
            public string week = "", day = "";
            public int used;
            public long damage;         // 이번 주 내가 준 피해
            public long best;
            public int claimed;         // 공유 체력 보상 비트(0~4)
            public string pubWeek = ""; // 클라우드에 마지막으로 올린 주 · 피해(새 주에 이전 값으로 옮기기 위해)
            public long pubDmg;
            public string rankWeek = "";   // 순위 보상을 처리한 주
            public int lastRank;        // 지난주 내 순위(0 = 순위 밖 · 미참여)
            public int rankBits;        // 받은 칭호 · 테두리(비트: 1위 · 2위 · 3위)
        }

        private SaveData _d = new();
        private bool _loaded;
        public event Action OnChanged;

        private static ServerRaid _instance;
        public static ServerRaid Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<ServerRaid>();
                if (_instance == null) _instance = new GameObject("ServerRaid").AddComponent<ServerRaid>();
                _instance.EnsureLoaded();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
            EnsureLoaded();
        }

        public static string WeekKey(DateTime t) => PlayerShop.PeriodKey(ShopReset.Weekly, t);
        public static string CurrentWeek => WeekKey(DateTime.Now);
        public static string LastWeek => WeekKey(DateTime.Now.AddDays(-7));

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
            }
            var now = DateTime.Now;
            string week = WeekKey(now), day = PlayerShop.PeriodKey(ShopReset.Daily, now);
            bool dirty = false;
            if (_d.week != week) { _d.week = week; _d.damage = 0; _d.best = 0; _d.claimed = 0; dirty = true; }
            if (_d.day != day) { _d.day = day; _d.used = 0; dirty = true; }
            if (dirty) Save();
        }

        private void Save()
        {
            SaveStore.SetString(SaveKey, JsonUtility.ToJson(_d));
            SaveStore.Save();
        }

        private void Changed() { Save(); OnChanged?.Invoke(); }

        public int AttemptsLeft { get { EnsureLoaded(); return WorldRaid.FreeAttempts ? AttemptsPerDay : Mathf.Max(0, AttemptsPerDay - _d.used); } }
        public long MyDamage { get { EnsureLoaded(); return _d.damage; } }
        public long MyBest { get { EnsureLoaded(); return _d.best; } }
        public int LastRank => _d.lastRank;
        public string RankWeek => _d.rankWeek;
        /// <summary>받은 칭호 · 테두리(비트 1 · 2 · 4 = 1 · 2 · 3위) — ProfileRewards가 해금 판정에 씀.</summary>
        public int RankBits { get { EnsureLoaded(); return _d.rankBits; } }

        public static bool Online => CloudAccount.SignedIn;

        /// <summary>로그인 중이면 5분마다 조용히 다시 불러옴(홈 알림 · 지난주 순위 보상이 화면을 열지 않아도 처리되게).</summary>
        private void Update()
        {
            if (!Online || Fetching || !CloudAccount.Synced) return;
            if ((DateTime.Now - FetchedAt).TotalSeconds > 300 && Time.unscaledTime > _nextAuto)
            {
                _nextAuto = Time.unscaledTime + 60f;   // 실패해도 1분은 쉬었다가
                _ = RefreshAsync();
            }
        }
        private float _nextAuto;

        // ---------------- 공유 체력 · 순위(서버) ----------------

        public class Entry
        {
            public string id, name;
            public long damage;
            public bool me;
            public int seed = -1;   // 가상 지휘관 번호(실제 플레이어는 -1)
            public int rank;        // 이번 주 순위(순위표를 만들 때 매김)
            public bool Virtual => seed >= 0;
        }

        // ---------------- 가상 지휘관 ----------------

        /// <summary>문자열 → 늘 같은 수(PC · 실행마다 같아야 해서 string.GetHashCode 대신).</summary>
        private static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char ch in s ?? "") h = (h ^ ch) * 16777619;
                return (int)(h & 0x7fffffff);
            }
        }

        /// <summary>이번 주가 지난 비율(월요일 05:00 = 0 → 다음 월요일 05:00 = 1).</summary>
        public static float WeekProgress => Mathf.Clamp01(1f - (float)(WorldRaid.UntilWeekReset.TotalDays / 7.0));

        /// <summary>
        /// 그 주에 함께 싸우는 가상 지휘관들의 기록(progress = 주가 지난 비율, 1이면 한 주 전부).
        /// 주 키로 명단 · 몫을 정하므로 모든 PC에서 같음. 위로 갈수록 몫이 크고(1위 ≈ 몫의 17%) 레벨도 높음.
        /// 하루 2번 도전하는 것처럼 14단계로 늘고, 몇 명은 주 중간부터 참여.
        /// </summary>
        public static List<Entry> VirtualEntries(string week, float progress)
        {
            var list = new List<Entry>();
            var rng = new System.Random(StableHash("wr_virtual_" + week));
            var weight = new double[VirtualCount];
            var start = new double[VirtualCount];
            var phase = new double[VirtualCount];
            var seeds = new int[VirtualCount];
            var used = new HashSet<int>();
            double sum = 0;
            for (int i = 0; i < VirtualCount; i++)
            {
                weight[i] = Math.Pow(i + 2, -1.25) * (0.85 + 0.3 * rng.NextDouble());
                start[i] = rng.NextDouble() < 0.75 ? 0 : rng.NextDouble() * 0.35;
                phase[i] = rng.NextDouble();
                // 상위 60명은 순위가 높을수록 높은 레벨의 지휘관(정보창의 육성 상태가 기록과 어울리게), 나머지는 아무나
                int minLevel = i < 60 ? Mathf.Clamp(80 - i, 20, 78) : 0, seed = 0;
                for (int tries = 0; tries < (i < 60 ? 300 : 8); tries++)
                {
                    seed = rng.Next(0, Commanders.MaxSeed);
                    if (!used.Contains(seed) && (minLevel == 0 || Commanders.FromSeed(seed, null).level >= minLevel)) break;
                }
                used.Add(seed);
                seeds[i] = seed;
                sum += weight[i];
            }
            progress = Mathf.Clamp01(progress);
            for (int i = 0; i < VirtualCount; i++)
            {
                double p = progress >= 1f ? 1.0 : Math.Clamp((progress - start[i]) / (1.0 - start[i]), 0.0, 1.0);
                p = Math.Floor(p * 14.0 + phase[i] * (p > 0 ? 1 : 0)) / 14.0;   // 하루 2번(주 14번) 계단식
                p = Math.Clamp(p, 0.0, 1.0);
                long dmg = (long)(MaxHp * (double)VirtualShare * weight[i] / sum * p);
                if (dmg <= 0) continue;
                var c = Commanders.FromSeed(seeds[i], null);
                list.Add(new Entry { id = "v" + seeds[i], name = c.name, damage = dmg, seed = seeds[i] });
            }
            return list;
        }

        /// <summary>실제 플레이어 기록 + 이번 주 가상 지휘관 기록 → 순위표 · 공유 체력.</summary>
        private void Combine(List<Entry> real)
        {
            var list = new List<Entry>();
            if (real != null) foreach (var e in real) if (!e.Virtual) list.Add(e);
            list.AddRange(VirtualEntries(CurrentWeek, WeekProgress));
            list.Sort((a, b) => b.damage != a.damage ? b.damage.CompareTo(a.damage) : b.me.CompareTo(a.me));
            for (int i = 0; i < list.Count; i++) list[i].rank = i + 1;
            Ranking = list;
            long sum = 0;
            foreach (var e in list) sum += e.damage;
            var mine = list.Find(e => e.me);
            sum += Math.Max(0, _d.damage - (mine != null ? mine.damage : 0));   // 아직 서버에 안 올라간 내 피해도 반영
            ServerDamage = sum;
        }

        /// <summary>이번 주 참여자(피해 순). 마지막으로 불러온 값.</summary>
        public List<Entry> Ranking { get; private set; } = new();
        /// <summary>서버 전체가 이번 주에 준 피해(마지막으로 불러온 값 — 내 최신 피해는 바로 반영).</summary>
        public long ServerDamage { get; private set; }
        public DateTime FetchedAt { get; private set; } = DateTime.MinValue;
        public bool Fetching { get; private set; }
        public string LastError { get; private set; }

        public long HpLeft => Math.Max(0, MaxHp - ServerDamage);
        public float HpRatio => Mathf.Clamp01(HpLeft / (float)MaxHp);
        public static long Threshold(int i) => MaxHp / HpRewards.Length * (i + 1);

        public bool IsClaimed(int i) { EnsureLoaded(); return (_d.claimed & (1 << i)) != 0; }
        public bool CanClaim(int i) => i >= 0 && i < HpRewards.Length && !IsClaimed(i) && ServerDamage >= Threshold(i);
        public int ClaimableCount { get { int n = 0; for (int i = 0; i < HpRewards.Length; i++) if (CanClaim(i)) n++; return n; } }

        /// <summary>공유 체력 보상 받기. 받은 보상(못 받으면 null).</summary>
        public RewardBundle Claim(int i)
        {
            if (!CanClaim(i)) return null;
            var r = HpRewards[i].bundle();
            r.Grant();
            _d.claimed |= 1 << i;
            Changed();
            return r;
        }

        /// <summary>
        /// 친구 순위표: 나 + 실제 플레이어 친구(이번 주 기록이 있으면) + 가상 친구(지휘관 레벨에 맞춘 기록 — 하루 2번씩 도전하는 것처럼 늘어남).
        /// rank는 전체 순위표 기준.
        /// </summary>
        public List<Entry> FriendBoard()
        {
            var list = new List<Entry>();
            var mine = Ranking.Find(e => e.me);
            list.Add(mine ?? new Entry { id = CloudAccount.PlayerId, name = PlayerProfile.Instance.DisplayName, damage = MyDamage, me = true });
            var social = PlayerSocial.Instance;
            foreach (var f in social.RealFriends)
            {
                var e = Ranking.Find(x => x.id == f.pid);
                if (e != null) list.Add(e);
            }
            float steps = Mathf.Floor(WeekProgress * 14f);
            foreach (int seed in social.Friends)
            {
                var c = Commanders.FromSeed(seed, null);
                float strength = Mathf.Pow(c.level / 80f, 2f);
                long dmg = (long)(MaxRunDamage * 0.2f * strength * steps * (0.8f + (seed % 40) / 100f));
                list.Add(new Entry { id = "f" + seed, name = c.name, damage = dmg, seed = seed });
            }
            foreach (var e in list)
            {
                if (e.rank > 0 && Ranking.Contains(e)) continue;
                int above = 0;
                foreach (var r in Ranking) if (r.damage > e.damage) above++;
                e.rank = e.damage > 0 ? above + 1 : 0;
            }
            list.Sort((a, b) => b.damage != a.damage ? b.damage.CompareTo(a.damage) : b.me.CompareTo(a.me));
            return list;
        }

        /// <summary>내 순위(이번 주, 0 = 순위표에 없음).</summary>
        public int MyRank
        {
            get
            {
                for (int i = 0; i < Ranking.Count; i++) if (Ranking[i].me) return i + 1;
                return 0;
            }
        }

        /// <summary>서버에서 이번 주 참여자 · 공유 체력을 다시 불러옴(+ 지난주 순위 보상 처리). 로그인 안 했으면 아무것도 안 함.</summary>
        public async Task RefreshAsync()
        {
            if (Fetching || !Online) return;
            Fetching = true; LastError = null;
            OnChanged?.Invoke();
            try
            {
                await PublishAsync();   // 새 주 이월 · 내 최신 값
                var list = await QueryWeekAsync(CurrentWeek, false);
                Combine(list);
                FetchedAt = DateTime.Now;
                await SettleLastWeekAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ServerRaid] 서버 기록을 불러오지 못했습니다: {e.Message}");
                Combine(Ranking);   // 서버를 못 읽어도 가상 지휘관 기록 · 공유 체력은 지금 시각으로
                LastError = e.Message != null && e.Message.ToLowerInvariant().Contains("index")
                    ? "서버 검색 설정(인덱스)이 없습니다 — docs/world-raid.md 참고"
                    : "서버 기록을 불러오지 못했습니다.";
            }
            finally
            {
                Fetching = false;
                OnChanged?.Invoke();
            }
        }

        /// <summary>내 이번 주 피해를 공개 데이터로 올림. 새 주에 처음 올리면 이전 주 값을 wr_p* 로 옮김.</summary>
        private async Task PublishAsync()
        {
            if (!Online) return;
            string week = CurrentWeek;
            var data = new Dictionary<string, object>
            {
                { KWeek, week }, { KDmg, _d.damage }, { KName, PlayerProfile.Instance.DisplayName },
            };
            if (!string.IsNullOrEmpty(_d.pubWeek) && _d.pubWeek != week)
            {
                data[KPWeek] = _d.pubWeek; data[KPDmg] = _d.pubDmg; data[KPName] = PlayerProfile.Instance.DisplayName;
            }
            await CloudSaveService.Instance.Data.Player.SaveAsync(data, new PlayerSaveOptions(new PublicWrite()));
            if (_d.pubWeek != week || _d.pubDmg != _d.damage)
            {
                _d.pubWeek = week; _d.pubDmg = _d.damage;
                Save();
            }
        }

        /// <summary>그 주의 참여자 목록(피해 순). prev = 이월된 값(wr_pweek)에서 찾기.</summary>
        private static async Task<List<Entry>> QueryWeekAsync(string week, bool prev)
        {
            string kWeek = prev ? KPWeek : KWeek, kDmg = prev ? KPDmg : KDmg, kName = prev ? KPName : KName;
            var result = new List<Entry>();
            string me = CloudAccount.PlayerId;
            const int page = 100;
            for (int offset = 0; offset < 1000; offset += page)
            {
                var q = new Query(new List<FieldFilter> { new FieldFilter(kWeek, week, FieldFilter.OpOptions.EQ, true) },
                    new HashSet<string> { kDmg, kName }, offset, page);
                var rows = await CloudSaveService.Instance.Data.Player.QueryAsync(q, new PlayerQueryOptions());
                if (rows == null) break;
                foreach (var row in rows)
                {
                    var e = new Entry { id = row.Id, name = "지휘관", me = row.Id == me };
                    if (row.Data != null)
                        foreach (var it in row.Data)
                        {
                            if (it?.Value == null) continue;
                            if (it.Key == kDmg) { try { e.damage = it.Value.GetAs<long>(); } catch { } }
                            else if (it.Key == kName) { try { e.name = it.Value.GetAs<string>(); } catch { } }
                        }
                    if (Plausible(e.damage)) result.Add(e);   // 비정상 기록은 제외
                    else if (e.damage > 0) Debug.LogWarning($"[ServerRaid] 비정상 기록 제외: {e.name} {e.damage:N0}");
                }
                if (rows.Count < page) break;
            }
            result.Sort((a, b) => b.damage.CompareTo(a.damage));
            return result;
        }

        /// <summary>지난주 순위 보상: 아직 처리하지 않았으면 지난주 참여자를 모아 내 순위를 구하고 우편으로 보냄(칭호 · 테두리 포함).</summary>
        private async Task SettleLastWeekAsync()
        {
            string last = LastWeek;
            if (_d.rankWeek == last) return;
            var a = await QueryWeekAsync(last, false);   // 이번 주에 아직 안 올린 사람(값이 지난주 그대로)
            var b = await QueryWeekAsync(last, true);    // 이번 주에 올리면서 지난주 값을 옮겨 둔 사람
            var merged = new Dictionary<string, Entry>();
            foreach (var e in a) merged[e.id] = e;
            foreach (var e in b) if (!merged.TryGetValue(e.id, out var x) || x.damage < e.damage) merged[e.id] = e;
            var list = new List<Entry>(merged.Values);
            list.AddRange(VirtualEntries(last, 1f));   // 지난주 가상 지휘관의 최종 기록
            list.Sort((x, y) => y.damage.CompareTo(x.damage));

            int rank = 0;
            for (int i = 0; i < list.Count; i++) if (list[i].me) { rank = i + 1; break; }
            _d.rankWeek = last;
            _d.lastRank = rank;
            var tier = rank > 0 ? TierOf(rank, list.Count) : null;
            if (tier != null)
            {
                if (rank <= 3) _d.rankBits |= 1 << (rank - 1);
                PlayerMail.Instance.Send($"월드 레이드 순위 보상 — {rank}위",
                    $"지난주 월드 레이드 '{BossName}'에서 {rank}위(참여 {list.Count}명)를 기록했습니다!" +
                    (tier.title != null ? $"\n칭호 '{ProfileRewards.Title(tier.title).name}'와 테두리가 열렸습니다(프로필에서 장착)." : ""),
                    tier.bundle());
            }
            Changed();
        }

        // ---------------- 전투 ----------------

        private BattleUnitData _boss;
        private List<CharacterData> _party;

        /// <summary>이번 주 약점(주간 보스와 다른 순서로 돌아감).</summary>
        public static Element[] Weaknesses
        {
            get
            {
                Element[][] rot =
                {
                    new[] { Element.Ice, Element.Wind }, new[] { Element.Physical, Element.Lightning }, new[] { Element.Fire, Element.Wind },
                    new[] { Element.Lightning, Element.Ice }, new[] { Element.Fire, Element.Physical },
                };
                var start = DateTime.ParseExact(CurrentWeek.Substring(1), "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
                int week = (int)((start - new DateTime(2024, 1, 1)).TotalDays / 7);
                return rot[((week % rot.Length) + rot.Length) % rot.Length];
            }
        }

        /// <summary>보스: 주간 보스 극악 배율 Lv.100 바탕, 이름 · 기술 · 약점만 다름.</summary>
        public static BattleUnitData CreateBoss()
        {
            var u = WorldRaid.CreateBoss(BossLevel, WorldRaid.DifficultyCount - 1);
            u.displayName = BossName;
            u.weaknesses = new List<Element>(Weaknesses);
            string[] names = { "거신의 주먹", "대지 분쇄", "종언의 낙인" };
            for (int i = 0; i < u.skills.Count && i < names.Length; i++) if (u.skills[i] != null) u.skills[i].skillName = names[i];
            return u;
        }

        public string Fight(IList<CharacterData> party)
        {
            if (!Online) return "월드 레이드는 로그인해야 도전할 수 있습니다";
            if (AttemptsLeft <= 0) return "오늘 도전 횟수를 모두 썼습니다 (매일 05:00 충전)";
            CleanupBoss();
            _boss = CreateBoss();
            _party = new List<CharacterData>(party);
            bool ok = WorldRaid.Instance.FightWorld(_party, _boss, "월드 레이드로", OnFightEnd,
                () => Fight(_party));
            return ok ? null : "전투를 시작하지 못했습니다";
        }

        private void OnFightEnd(long dmg)
        {
            // 끝까지 싸운 전투만 도전 횟수를 씀(중간에 나가면 기록 · 횟수 모두 없음)
            if (!WorldRaid.FreeAttempts) _d.used++;
            dmg = Math.Min(Math.Max(0, dmg), MaxRunDamage);
            _d.damage += dmg;
            bool newBest = dmg > _d.best;
            if (newBest) _d.best = dmg;
            ServerDamage = Math.Max(ServerDamage + dmg, _d.damage);
            Changed();

            var raid = WorldRaid.Instance;
            var hud = FindAnyObjectByType<SubcultureRPG.Battle.UI.BattleHUD>();
            hud?.SetResultDetail($"<size=110%>월드 레이드 — {BossName}  <size=80%>Lv.{BossLevel}</size></size>\n" +
                                 $"<color=#FFD36B>준 피해</color>  <b>{dmg:N0}</b>" + (newBest ? "  <color=#7CFF9A>최고 기록!</color>" : "") + "\n" +
                                 $"<color=#9AA3B5>이번 주 내 피해</color>  {_d.damage:N0}   <color=#9AA3B5>서버 전체</color>  {ServerDamage:N0} / {MaxHp:N0}\n" +
                                 $"<size=80%><color=#9AA3B5>오늘 남은 도전 {AttemptsLeft} / {AttemptsPerDay}  ·  버틴 시간 {raid.SurvivedSeconds}초 / {WorldRaid.TimeLimit:0}초  ·  격노 {raid.EnrageStage}단계</color></size>");
            CleanupBoss();
            _ = RefreshAsync();   // 서버에 올리고 순위 · 공유 체력 갱신
        }

        private void CleanupBoss()
        {
            if (_boss == null) return;
            if (_boss.skills != null) foreach (var sk in _boss.skills) if (sk != null) Destroy(sk, 1f);
            Destroy(_boss, 1f);
            _boss = null;
        }

        [ContextMenu("테스트: 도전 횟수 초기화")]
        private void TestResetAttempts() { _d.used = 0; Changed(); }

        [ContextMenu("테스트: 지난주 순위 보상 다시 처리")]
        private void TestResettle() { _d.rankWeek = ""; Changed(); _ = RefreshAsync(); }
    }
}
