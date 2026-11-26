using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;
using SubcultureRPG.Battle.UI;
using SubcultureRPG.Meta.Shop;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 주간 보스(월드 레이드, PVE): 체력이 사실상 끝없는 보스 '별을 삼키는 자'에게 준 피해를 한 주 동안 쌓아 보상을 받음.
    /// · 하루 도전 3회(05:00 초기화) · 제한 시간 180초(전투 시간 기준 — 배속과 무관, 직접 조작 입력 대기 중에는 멈춤)
    /// · 30초마다 보스 격노: 공격력 +25%씩(겹침) + 그 즉시 '사건의 지평선'(아군 전체, 최대 체력 비례 피해가 단계마다 커짐)
    ///   → 시간이 다 되거나 아군이 전멸하면 그때까지 준 피해를 집계. 보스 공격은 최대 체력 비례 피해가 섞여 육성도와 상관없이 위협이 됨
    ///   (밸런스 확인: 에디터 메뉴 '주간 보스 밸런스 시뮬레이션' — docs/modes.md)
    /// · 약점 2속성이 매주 바뀜(월요일 05:00)
    /// · 난이도 4단계(쉬움 · 보통 · 어려움 · 극악 = 보스 Lv.30 · 50 · 80 · 100). 난이도마다 보스 능력 · 누적 점수 · 보상 단계가 따로
    ///   (도전 횟수는 하루 3회를 함께 씀)
    /// · 누적 피해 단계별 보상(주간, 수동 수령) — 주가 바뀌면 누적 · 수령 기록 초기화
    /// · 서버 공유 보스 '월드 레이드'(ServerRaid)도 이 전투 틀(시간 · 격노)을 함께 씀(FightWorld)
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(WorldRaid.Instance).
    /// </summary>
    public class WorldRaid : MonoBehaviour
    {
        public const string BossName = "별을 삼키는 자";
        public const int AttemptsPerDay = 3;
        /// <summary>제한 시간(초) · 격노 간격(초) · 격노 1단계당 공격력 증가.</summary>
        public const float TimeLimit = 300f, EnrageEvery = 30f, EnrageAtk = 0.25f;
        /// <summary>보스 기술의 최대 체력 비례 피해: 포식(단일) · 별빛 붕괴(전체) · 사건의 지평선(전체, 격노 1단계마다 + HorizonPerStage).</summary>
        public const float BiteHp = 0.06f, CollapseHp = 0.04f, HorizonHp = 0.06f, HorizonPerStage = 0.045f;

        /// <summary>이번 주 보너스 캐릭터 수 · 파티에서 인정하는 최대 인원 · 1명당 점수 보너스.</summary>
        public const int FeaturedCount = 4, FeaturedMax = 2;
        public const float FeaturedBonus = 0.30f;
        private const string SaveKey = "raid";

        /// <summary>주차별 약점(돌아가며).</summary>
        private static readonly Element[][] WeakRotation =
        {
            new[] { Element.Fire, Element.Ice }, new[] { Element.Lightning, Element.Wind }, new[] { Element.Physical, Element.Fire },
            new[] { Element.Ice, Element.Lightning }, new[] { Element.Wind, Element.Physical },
        };

        /// <summary>
        /// 누적 점수 보상 단계(한 주). 시뮬레이션 기준(2026-11-26) 한 번 점수: 장비 없는 Lv.40 ≈ 4만 · Lv.70 ≈ 7만 · Lv.100 ≈ 14만,
        /// 풀 육성(6★+6 · 장비) ≈ 1,100만(보너스 캐릭터 2명이면 ≈ 1,700만). 하루 3번 → 풀 육성은 4~5일째에 마지막 단계.
        /// </summary>
        public class Milestone
        {
            public long damage;
            public string label;
            public Action<PlayerWallet, PlayerInventory> grant;
        }

        /// <summary>난이도별 보스 · 보상(튜닝 지점).</summary>
        public class Difficulty
        {
            public string name, hex;
            public int level;            // 보스 레벨(고정)
            public float atkMul = 1f, defMul = 1f;
            public float hpMul = 1f;     // 최대 체력 비례 피해 배율(포식 · 붕괴 · 지평선)
            public float enrageAtk = EnrageAtk;
            public int toughness = 420;
            public List<Milestone> milestones;
        }

        private static Milestone M(long dmg, string label, Action<PlayerWallet, PlayerInventory> g) => new() { damage = dmg, label = label, grant = g };

        /// <summary>
        /// 쉬움 · 보통 · 어려움 · 극악. 점수 기준(시뮬레이션 2026-11-26 바탕, 한 번 점수):
        /// 쉬움 = 장비 없는 Lv.30~40 파티 ≈ 4~8만 · 보통 = Lv.50~60 · 장비 조금 ≈ 30~60만 ·
        /// 어려움 = Lv.80 · 장비 갖춤 ≈ 200~400만 · 극악 = 풀 육성(6★+6 · 장비) ≈ 900만(보너스 캐릭터 2명이면 ≈ 1,400만).
        /// 각 난이도 마지막 단계 = 그 난이도에 맞는 파티가 하루 3번 · 일주일(21번)을 거의 다 쳐야 닿는 정도.
        /// </summary>
        public static readonly Difficulty[] Difficulties =
        {
            new()
            {
                name = "쉬움", hex = "#7CFF9A", level = 30, atkMul = 0.8f, hpMul = 0.4f, enrageAtk = 0.12f, toughness = 300,
                milestones = new()
                {
                    M(500_000, "금화 10,000", (w, i) => w?.AddGold(10000)),
                    M(1_500_000, "경험치 물약 ×10", (w, i) => i?.Add(MaterialType.ExpPotion, 10)),
                    M(3_000_000, "크리스탈 50", (w, i) => w?.Add(50)),
                    M(6_000_000, "중급 스킬 교본 ×5", (w, i) => i?.Add(MaterialType.SkillBookBlue, 5)),
                    M(10_000_000, "크리스탈 100 · 행동력 물약 ×1", (w, i) => { w?.Add(100); i?.Add(MaterialType.StaminaLarge, 1); }),
                },
            },
            new()
            {
                name = "보통", hex = "#6BC8FF", level = 50, atkMul = 0.9f, hpMul = 0.7f, enrageAtk = 0.18f, toughness = 360,
                milestones = new()
                {
                    M(3_000_000, "금화 20,000", (w, i) => w?.AddGold(20000)),
                    M(10_000_000, "대형 경험치 물약 ×5", (w, i) => i?.Add(MaterialType.ExpLarge, 5)),
                    M(25_000_000, "크리스탈 100", (w, i) => w?.Add(100)),
                    M(50_000_000, "상급 스킬 교본 ×3", (w, i) => i?.Add(MaterialType.SkillBookPurple, 3)),
                    M(80_000_000, "크리스탈 150 · 행동력 물약 ×2", (w, i) => { w?.Add(150); i?.Add(MaterialType.StaminaLarge, 2); }),
                },
            },
            new()
            {
                name = "어려움", hex = "#FFB347", level = 80, atkMul = 1f, hpMul = 1f, enrageAtk = 0.25f, toughness = 420,
                milestones = new()
                {
                    M(15_000_000, "금화 40,000", (w, i) => w?.AddGold(40000)),
                    M(50_000_000, "대형 경험치 물약 ×10", (w, i) => i?.Add(MaterialType.ExpLarge, 10)),
                    M(120_000_000, "크리스탈 150", (w, i) => w?.Add(150)),
                    M(250_000_000, "상급 스킬 교본 ×5 · 장비 정수 60", (w, i) => { i?.Add(MaterialType.SkillBookPurple, 5); i?.Add(MaterialType.EquipEssence, 60); }),
                    M(450_000_000, "전설의 비전서 ×1 · 크리스탈 200", (w, i) => { i?.Add(MaterialType.SkillLegend, 1); w?.Add(200); }),
                },
            },
            new()
            {
                // 2026-11-26 실측(최대 육성 · 보너스 2명): 회복 없이 92초 전멸 · 회복 1명이어도 1분~1분 30초에 딜러 전멸(점수 2,058만)
                // → 공격 · 비례 피해를 어려움과 같게, 격노만 조금 더 세게(방어 · 내구도는 높게 유지 — 점수가 덜 나오는 쪽으로 어렵게)
                name = "극악", hex = "#FF5C7A", level = 100, atkMul = 1f, defMul = 1.15f, hpMul = 1f, enrageAtk = 0.27f, toughness = 520,
                milestones = new()
                {
                    // 실측(2026-11-26, 최대 육성 · 회복 1명 · 아무도 안 쓰러짐): 한 판 준 피해 1,816만 → 능력치 10배 뒤 약 1.8억, 보너스 2명이면 점수 약 2.9억.
                    // 이미 최종까지 키운 플레이어는 여러 판 치지 않아도 되게: 보너스 2명이면 5판(이틀) · 없으면 8판쯤에 마지막 단계.
                    // 다시 맞추기: 에디터 '주간 보스 밸런스 시뮬레이션'에 diff=3 (docs/modes.md)
                    M(200_000_000, "크리스탈 100 · 금화 60,000", (w, i) => { w?.Add(100); w?.AddGold(60000); }),
                    M(500_000_000, "전설의 비전서 ×1 · 장비 정수 120", (w, i) => { i?.Add(MaterialType.SkillLegend, 1); i?.Add(MaterialType.EquipEssence, 120); }),
                    M(800_000_000, "크리스탈 200 · 행동력 물약 ×2", (w, i) => { w?.Add(200); i?.Add(MaterialType.StaminaLarge, 2); }),
                    M(1_100_000_000, "픽업 뽑기권 ×1 · 크리스탈 300 · 재련석 ×1", (w, i) => { w?.AddTickets(SubcultureRPG.Gacha.GachaBannerType.Pickup, 1); w?.Add(300); i?.Add(MaterialType.RefineStone, 1); }),
                    M(1_500_000_000, "크리스탈 300 · 재련석 ×2", (w, i) => { w?.Add(300); i?.Add(MaterialType.RefineStone, 2); }),
                },
            },
        };

        public const int DifficultyCount = 4;
        public static Difficulty Diff(int d) => Difficulties[Mathf.Clamp(d, 0, DifficultyCount - 1)];

        [Serializable]
        private class SaveData
        {
            public string week = "", day = "";
            public long total, best;    // (옛 세이브 — 난이도 도입 전. 쓰지 않음)
            public int used, claimed;   // claimed(옛) 대신 claims[난이도]
            public long[] totals = new long[DifficultyCount], bests = new long[DifficultyCount];
            public int[] claims = new int[DifficultyCount];   // 보상 단계 비트
            public int diff = 2;        // 마지막으로 고른 난이도(기본 어려움)
        }

        private SaveData _d = new();
        private bool _loaded;
        public event Action OnChanged;

        private static WorldRaid _instance;
        public static WorldRaid Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<WorldRaid>();
                if (_instance == null) _instance = new GameObject("WorldRaid").AddComponent<WorldRaid>();
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
            }
            // 주 · 하루가 바뀌었으면 초기화
            var now = DateTime.Now;
            string week = PlayerShop.PeriodKey(ShopReset.Weekly, now), day = PlayerShop.PeriodKey(ShopReset.Daily, now);
            bool dirty = false;
            if (_d.totals == null || _d.totals.Length != DifficultyCount) { _d.totals = new long[DifficultyCount]; dirty = true; }
            if (_d.bests == null || _d.bests.Length != DifficultyCount) { _d.bests = new long[DifficultyCount]; dirty = true; }
            if (_d.claims == null || _d.claims.Length != DifficultyCount) { _d.claims = new int[DifficultyCount]; dirty = true; }
            if (_d.week != week)
            {
                _d.week = week; _d.total = 0; _d.best = 0; _d.claimed = 0;
                for (int i = 0; i < DifficultyCount; i++) { _d.totals[i] = 0; _d.bests[i] = 0; _d.claims[i] = 0; }
                dirty = true;
            }
            if (_d.day != day) { _d.day = day; _d.used = 0; dirty = true; }
            if (dirty) Save();
        }

        private void Save()
        {
            SaveStore.SetString(SaveKey, JsonUtility.ToJson(_d));
            SaveStore.Save();
        }

        private void Changed() { Save(); OnChanged?.Invoke(); }

        /// <summary>고른 난이도(화면 탭). 저장됨.</summary>
        public int Selected
        {
            get { EnsureLoaded(); return Mathf.Clamp(_d.diff, 0, DifficultyCount - 1); }
            set { EnsureLoaded(); int v = Mathf.Clamp(value, 0, DifficultyCount - 1); if (v == _d.diff) return; _d.diff = v; Changed(); }
        }

        public long TotalDamage(int d) { EnsureLoaded(); return _d.totals[Mathf.Clamp(d, 0, DifficultyCount - 1)]; }
        public long BestDamage(int d) { EnsureLoaded(); return _d.bests[Mathf.Clamp(d, 0, DifficultyCount - 1)]; }
        public int AttemptsLeft { get { EnsureLoaded(); return FreeAttempts ? AttemptsPerDay : Mathf.Max(0, AttemptsPerDay - _d.used); } }

        /// <summary>최대 육성 테스트 중(TestMaxData 백업이 있음)에는 도전 횟수를 쓰지 않음.</summary>
        public static bool FreeAttempts => SubcultureRPG.UI.DevTools.Enabled && TestMaxData.HasBackup;
        public bool IsClaimed(int d, int i) { EnsureLoaded(); return (_d.claims[Mathf.Clamp(d, 0, DifficultyCount - 1)] & (1 << i)) != 0; }
        public bool CanClaim(int d, int i) => i >= 0 && i < Diff(d).milestones.Count && !IsClaimed(d, i) && TotalDamage(d) >= Diff(d).milestones[i].damage;
        public int ClaimableIn(int d) { int n = 0; for (int i = 0; i < Diff(d).milestones.Count; i++) if (CanClaim(d, i)) n++; return n; }
        /// <summary>모든 난이도의 받을 보상 수(홈 알림).</summary>
        public int ClaimableCount { get { int n = 0; for (int d = 0; d < DifficultyCount; d++) n += ClaimableIn(d); return n; } }

        /// <summary>이번 주 약점.</summary>
        public static Element[] Weaknesses
        {
            get
            {
                var start = DateTime.ParseExact(PlayerShop.PeriodKey(ShopReset.Weekly, DateTime.Now).Substring(1), "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
                int week = (int)((start - new DateTime(2024, 1, 1)).TotalDays / 7);
                return WeakRotation[((week % WeakRotation.Length) + WeakRotation.Length) % WeakRotation.Length];
            }
        }

        /// <summary>
        /// 이번 주 보너스 캐릭터(주마다 바뀜, 모두가 같은 목록). 태생 6★만 몰리지 않도록 6★ 2명 + 그 외 2명을 고름.
        /// 파티에 넣으면 1명당 점수(기록 피해) +30%, 최대 2명(+60%).
        /// </summary>
        public static List<CharacterData> Featured
        {
            get
            {
                var list = new List<CharacterData>();
                var roster = FindAnyObjectByType<PlayerRoster>();
                var db = roster != null ? roster.Database : null;
                if (db == null) return list;
                var six = new List<CharacterData>();
                var rest = new List<CharacterData>();
                foreach (var c in db.Characters)
                    if (c != null) (c.rarity == Rarity.R6 ? six : rest).Add(c);
                var start = DateTime.ParseExact(PlayerShop.PeriodKey(ShopReset.Weekly, DateTime.Now).Substring(1), "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
                var rng = new System.Random((int)((start - new DateTime(2024, 1, 1)).TotalDays / 7) * 7919 + 17);
                void Pick(List<CharacterData> from, int n)
                {
                    for (int i = 0; i < n && from.Count > 0; i++)
                    {
                        int k = rng.Next(from.Count);
                        list.Add(from[k]);
                        from.RemoveAt(k);
                    }
                }
                Pick(six, FeaturedCount / 2);
                Pick(rest, FeaturedCount - list.Count);
                if (list.Count < FeaturedCount) Pick(six, FeaturedCount - list.Count);
                return list;
            }
        }

        /// <summary>파티의 보너스 캐릭터 수(최대 2명).</summary>
        public static int FeaturedIn(IList<CharacterData> party)
        {
            if (party == null) return 0;
            var f = Featured;
            int n = 0;
            foreach (var c in party) if (c != null && f.Contains(c)) n++;
            return Mathf.Min(FeaturedMax, n);
        }

        /// <summary>파티의 점수 보너스(0 ~ 0.6).</summary>
        public static float PartyBonus(IList<CharacterData> party) => FeaturedIn(party) * FeaturedBonus;

        public static TimeSpan UntilWeekReset => PlayerShop.UntilReset(ShopReset.Weekly, DateTime.Now) ?? TimeSpan.Zero;

        public bool Claim(int d, int i)
        {
            if (!CanClaim(d, i)) return false;
            Diff(d).milestones[i].grant(FindAnyObjectByType<PlayerWallet>(), FindAnyObjectByType<PlayerInventory>());
            _d.claims[d] |= 1 << i;
            Changed();
            return true;
        }

        // ---------------- 보스 ----------------

        /// <summary>난이도의 보스 레벨(고정: 30 · 50 · 80 · 100).</summary>
        public static int BossLevel(int d) => Diff(d).level;

        /// <summary>(옛) 보스 레벨: 내 주력 4명 평균 — 밸런스 시뮬레이션 참고용.</summary>
        public static int BossLevel()
        {
            var roster = FindAnyObjectByType<PlayerRoster>();
            if (roster == null) return 10;
            var top = CombatPower.All(roster, FindAnyObjectByType<PlayerEquipment>());
            int n = 0, sum = 0;
            foreach (var kv in top) { if (n >= 4) break; sum += roster.LevelOf(kv.Key); n++; }
            return Mathf.Clamp(n > 0 ? sum / n : 10, 10, Leveling.AwakenedMaxLevel);   // 개화(Lv.100)까지 따라감
        }

        private static BattleSkill Skill(string name, SkillCategory cat, TargetType t, float power, int energy, float hpDmg, StatusType eff = StatusType.None, float mag = 0f)
        {
            var s = ScriptableObject.CreateInstance<BattleSkill>();
            s.skillName = name; s.category = cat; s.target = t; s.power = power; s.hits = 1; s.maxHpDamage = hpDmg;
            s.energyGain = energy; s.element = Element.Physical; s.breakDamage = 0;
            s.applyEffect = eff; s.effectMagnitude = mag; s.effectDuration = 2; s.effectChance = 1f;
            return s;
        }

        /// <summary>보스 유닛 데이터(밸런스 시뮬레이션에서도 씀 — 에디터 메뉴 '주간 보스 밸런스 시뮬레이션'). difficulty 생략 = 어려움 배율(기준).</summary>
        public static BattleUnitData CreateBoss(int level, int difficulty = 2)
        {
            var df = Diff(difficulty);
            var u = ScriptableObject.CreateInstance<BattleUnitData>();
            u.displayName = BossName;
            u.level = level;
            u.element = Element.Physical;
            u.weaknesses = new List<Element>(Weaknesses);
            u.maxToughness = df.toughness;
            u.baseStats = new BattleStats
            {
                maxHP = 70_000_000, hpGrowth = 0,   // ×Scale(30) = 21억 — int 한도(약 21.47억) 바로 아래. 더 키우면 넘쳐서 체력 1이 됨
                atk = Mathf.RoundToInt(120 * df.atkMul), atkGrowth = Mathf.RoundToInt(12 * df.atkMul), def = Mathf.RoundToInt(60 * df.defMul), defGrowth = Mathf.RoundToInt(9 * df.defMul),
                spd = 92, critRate = 0.05f, critDmg = 0.5f,
            };
            u.skills = new List<BattleSkill>
            {
                Skill("포식", SkillCategory.Basic, TargetType.SingleEnemy, 1.1f, 25, BiteHp * df.hpMul),
                Skill("별빛 붕괴", SkillCategory.Skill, TargetType.AllEnemies, 0.7f, 30, CollapseHp * df.hpMul, StatusType.DefDown, 0.15f),
                Skill("사건의 지평선", SkillCategory.Ultimate, TargetType.AllEnemies, 1.6f, 0, HorizonHp * df.hpMul, StatusType.SpdDown, 0.2f),
            };
            return u;
        }

        /// <summary>
        /// 격노 단계 적용(게임 · 시뮬레이션 공용): 공격력 +EnrageAtk×단계, 사건의 지평선 비례 피해 증가,
        /// 그리고 단계가 오른 순간 보스가 곧바로 사건의 지평선을 씀(추가 행동 — 아군 속도 · 브레이크 · 기절과 상관없이).
        /// </summary>
        public static void ApplyEnrage(BattleUnit boss, int stage, int baseAtk, int difficulty = 2)
        {
            if (boss == null || !boss.IsAlive) return;
            var df = Diff(difficulty);
            boss.atk = Mathf.RoundToInt(baseAtk * (1f + df.enrageAtk * stage));
            foreach (var s in boss.skills)
                if (s != null && s.IsUltimate) s.maxHpDamage = (HorizonHp + HorizonPerStage * stage) * df.hpMul;
            if (stage <= 0) return;
            boss.energy = boss.maxEnergy;
            boss.extraQueued = true;
            boss.actionValue = 0f;
        }

        // ---------------- 전투 ----------------

        private BattleUnitData _boss;
        private List<CharacterData> _party;
        private bool _paid, _pending, _active;
        private int _fightDiff = 2;     // 이번 전투 난이도
        private bool _world, _worldPending;   // 서버 공유 보스(월드 레이드) 전투
        private Action<long> _worldEnd;
        private float _timeLeft;
        private int _enrage, _baseAtk;
        private int _warned;   // 예고를 띄운 격노 단계(같은 단계에 두 번 띄우지 않게)
        private TMPro.TextMeshProUGUI _timerText, _totalText;
        private GameObject _timerRoot;
        private BattleController _controller;
        private BattleHUD _hud;

        public bool Fight(IList<CharacterData> party)
        {
            if (party == null || party.Count == 0) return false;
            var launcher = FindAnyObjectByType<BattleLauncher>();
            if (launcher == null) return false;
            Refund();
            if (AttemptsLeft <= 0) { _hud?.ShowResultToast("오늘 도전 횟수를 모두 썼습니다 (매일 05:00 충전)"); return false; }
            if (!FreeAttempts)
            {
                _d.used++;
                _paid = true;
                Changed();
            }

            Hook();
            CleanupBoss();
            _fightDiff = Selected;
            _boss = CreateBoss(BossLevel(_fightDiff), _fightDiff);
            _party = new List<CharacterData>(party);
            _pending = true; _worldPending = false;
            if (_hud != null)
            {
                _hud.SetBackground("Backgrounds/battle_raid", "Backgrounds/raid", "Backgrounds/battle");
                _hud.SetRetryHandler(() => Fight(_party) ? null : "오늘 도전 횟수를 모두 썼습니다");
                _hud.SetExitLabel("주간 보스로");
                _hud.SetRoundLimit(0);
            }
            launcher.Launch(_party, new List<BattleUnitData> { _boss });
            return true;
        }

        /// <summary>
        /// 월드 레이드(서버 공유 보스) 전투: 같은 시간 제한 · 격노(극악 배율)로 싸우고, 끝나면 준 피해를 onEnd로 넘김.
        /// 도전 횟수 차감 · 보스 데이터 정리는 ServerRaid가 함.
        /// </summary>
        public bool FightWorld(IList<CharacterData> party, BattleUnitData boss, string exitLabel, Action<long> onEnd, Func<string> retry)
        {
            if (party == null || party.Count == 0 || boss == null) return false;
            var launcher = FindAnyObjectByType<BattleLauncher>();
            if (launcher == null) return false;
            Refund();
            Hook();
            CleanupBoss();
            _fightDiff = DifficultyCount - 1;
            _party = new List<CharacterData>(party);
            _pending = false; _worldPending = true;
            _worldEnd = onEnd;
            if (_hud != null)
            {
                _hud.SetBackground("Backgrounds/battle_worldraid", "Backgrounds/worldraid", "Backgrounds/battle");
                _hud.SetRetryHandler(retry);
                _hud.SetExitLabel(exitLabel);
                _hud.SetRoundLimit(0);
            }
            launcher.Launch(_party, new List<BattleUnitData> { boss });
            return true;
        }

        public void Leave()
        {
            Refund();
            _active = false; _pending = false; _world = false; _worldPending = false; _worldEnd = null;
            ShowTimer(false);
            if (_hud != null) _hud.SetRetryHandler(null);
        }

        private void Refund()
        {
            if (!_paid) return;
            _paid = false;
            _d.used = Mathf.Max(0, _d.used - 1);
            Changed();
        }

        private void Hook()
        {
            if (_controller != null) return;
            _controller = FindAnyObjectByType<BattleController>();
            _hud = FindAnyObjectByType<BattleHUD>();
            if (_controller == null) return;
            _controller.OnBattleStart += eng =>
            {
                _world = _worldPending; _worldPending = false;
                _active = _pending || _world; _pending = false;
                _timeLeft = TimeLimit; _enrage = 0; _warned = 0;
                _baseAtk = eng != null && eng.enemies.Count > 0 ? eng.enemies[0].atk : 0;
                ShowTimer(_active);
            };
            _controller.OnBattleEnd += HandleEnd;
        }

        private void Update()
        {
            if (!_active || _controller == null) return;
            if (!_controller.IsRunning) return;
            if (!_controller.WaitingForInput) _timeLeft -= Time.deltaTime * _controller.Speed;   // 직접 조작 입력 대기 중에는 멈춤

            // 격노 예고: 다음 격노 3초 전(전투 시간)에 붉은 화면 가장자리 + WARNING 띠
            {
                float elapsed = TimeLimit - Mathf.Max(0f, _timeLeft);
                int next = Mathf.FloorToInt(elapsed / EnrageEvery) + 1;
                float until = next * EnrageEvery - elapsed;
                if (until <= 3f && next > _warned && next * EnrageEvery < TimeLimit && _hud != null)
                {
                    _warned = next;
                    string ult = "필살기";
                    var en = _controller.Engine;
                    if (en != null && en.enemies.Count > 0 && en.enemies[0].skills != null)
                        foreach (var sk in en.enemies[0].skills) if (sk != null && sk.IsUltimate) ult = sk.skillName;
                    float pct = (HorizonHp + HorizonPerStage * next) * Diff(_fightDiff).hpMul * 100f;
                    _hud.BossWarning("WARNING", $"<color=#FFB0A0>격노 {next}단계</color> · '{ult}' 임박 — 아군 전체에 최대 체력 {pct:0}% 피해", 2.8f / Mathf.Max(1f, _controller.Speed));
                }
            }

            // 격노: 30초마다 공격력 +25%
            int stage = Mathf.FloorToInt((TimeLimit - Mathf.Max(0f, _timeLeft)) / EnrageEvery);
            var eng = _controller.Engine;
            if (stage != _enrage && eng.enemies.Count > 0)
            {
                _enrage = stage;
                ApplyEnrage(eng.enemies[0], _enrage, _baseAtk, _fightDiff);
            }
            RefreshTimer();

            if (_timeLeft <= 0f)
            {
                // 시간 종료 → 전투 종료(패배 아님)
                _controller.RequestEnd(Team.Enemy);
                _hud?.SetEndTexts("TIME UP", "전투 종료", new Color(1f, 0.82f, 0.4f));
            }
        }

        private void RefreshTimer()
        {
            if (_timerText == null) return;
            int sec = Mathf.CeilToInt(Mathf.Max(0f, _timeLeft));
            string col = sec <= 30 ? "#FF7A7A" : "#FFFFFF";
            long dmg = _controller.Engine.enemies.Count > 0 ? _controller.Engine.enemies[0].record.taken : 0;
            // 격노 줄은 둘로 나눔(한 줄이면 칸이 좁아 잘림)
            _timerText.text = $"<size=60%><color=#9AA3B5>남은 시간</color></size>\n<color={col}><b>{sec / 60}:{sec % 60:00}</b></color>" +
                              (_enrage > 0
                                  ? $"\n<size=50%><color=#FF9A6B>격노 {_enrage}단계 · 공격력 +{Diff(_fightDiff).enrageAtk * _enrage * 100f:0}%</color>" +
                                    $"\n<color=#FFB89A>사건의 지평선 · 최대 체력 {(HorizonHp + HorizonPerStage * _enrage) * Diff(_fightDiff).hpMul * 100f:0}%</color></size>"
                                  : "\n<size=50%><color=#9AA3B5>30초마다 격노</color></size>");
            if (_totalText != null)
                _totalText.text = $"<size=50%><color=#E6D2FF>보스에게 가한 총 피해</color></size>\n<b>{dmg:N0}</b>";
        }

        /// <summary>전투 화면 위에 남은 시간 · 격노 · 이번 피해 표시(주간 보스 전투 중에만).</summary>
        private void ShowTimer(bool on)
        {
            if (on && _timerRoot == null)
            {
                var cgo = SubcultureRPG.UI.UIFactory.NewRect("RaidTimerCanvas", transform);
                var canvas = cgo.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 91;   // 전투 HUD(90) 바로 위
                var scaler = cgo.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
                _timerRoot = cgo.gameObject;
                var font = _hud != null ? _hud.Font : null;
                // 왼쪽 위(나가기 버튼 아래): 남은 시간 · 격노
                var bg = SubcultureRPG.UI.UIFactory.AddImage("TimeBg", cgo, new Color(0.04f, 0.04f, 0.08f, 0.78f), SubcultureRPG.UI.ProceduralSprites.Rounded);
                bg.type = UnityEngine.UI.Image.Type.Sliced; bg.preserveAspect = false;
                SubcultureRPG.UI.UIFactory.Anchor(bg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
                // 왼쪽 위: 남은 시간 · 격노(두 줄). 뒷줄 아군 이름표를 가리지 않게 높이는 낮게, 가로만 넓게
                bg.rectTransform.sizeDelta = new Vector2(440, 168); bg.rectTransform.anchoredPosition = new Vector2(30, -90);
                _timerText = SubcultureRPG.UI.UIFactory.AddText("Time", bg.transform, "", 44, SubcultureRPG.UI.GameFonts.Number(font), Color.white,
                    TMPro.TextAlignmentOptions.TopLeft, TMPro.FontStyles.Normal);
                _timerText.richText = true;
                _timerText.lineSpacing = -8f;
                _timerText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                SubcultureRPG.UI.UIFactory.Stretch(_timerText.rectTransform, 22, 8, 12, 10);

                // 위 가운데(행동 순서 줄 아래 — 하늘이라 비어 있음): 이번 전투에서 보스에게 준 피해 합계.
                // 오른쪽에 두면 숫자가 길어질 때 보스 · 보스 이름표를 가려서 옮김. 길어지면 글자가 줄어듦
                var tb = SubcultureRPG.UI.UIFactory.AddImage("TotalBg", cgo, new Color(0.08f, 0.04f, 0.14f, 0.62f), SubcultureRPG.UI.ProceduralSprites.Rounded);
                tb.type = UnityEngine.UI.Image.Type.Sliced; tb.preserveAspect = false;
                SubcultureRPG.UI.UIFactory.Anchor(tb.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
                tb.rectTransform.sizeDelta = new Vector2(480, 92); tb.rectTransform.anchoredPosition = new Vector2(0, -96);
                _totalText = SubcultureRPG.UI.UIFactory.AddText("Total", tb.transform, "", 40, SubcultureRPG.UI.GameFonts.Number(font), new Color(0.88f, 0.75f, 1f),
                    TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Normal);
                _totalText.richText = true;
                _totalText.lineSpacing = -6f;
                _totalText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                _totalText.enableAutoSizing = true; _totalText.fontSizeMin = 22; _totalText.fontSizeMax = 40;
                SubcultureRPG.UI.TextOutline.Apply(_totalText, 0.12f, new Color32(20, 8, 30, 255));
                SubcultureRPG.UI.UIFactory.Stretch(_totalText.rectTransform, 16, 4, 16, 4);
            }
            if (_timerRoot != null) _timerRoot.SetActive(on);
        }

        private void HandleEnd(Team winner)
        {
            if (!_active) return;
            _active = false;
            _paid = false;
            ShowTimer(false);
            long dmg = 0;
            var eng = _controller != null ? _controller.Engine : null;
            if (eng != null && eng.enemies.Count > 0) dmg = eng.enemies[0].record.taken;
            if (_world)
            {
                // 월드 레이드: 준 피해를 그대로 넘김(보너스 캐릭터 없음). 결과 문구는 ServerRaid가 씀
                _world = false;
                var cb = _worldEnd; _worldEnd = null;
                cb?.Invoke(dmg);
                return;
            }
            long raw = dmg;
            float bonus = PartyBonus(_party);
            dmg = (long)Math.Round(raw * (1.0 + bonus));   // 보너스 캐릭터 → 점수(기록 피해) 증가
            int fd = _fightDiff;
            var df = Diff(fd);
            long before = _d.totals[fd];
            // 위 난이도 점수는 아래 난이도 누적에도 똑같이 더함(강한 플레이어가 쉬운 난이도에 도전 횟수를 쓰지 않아도 되게)
            int lowerOpened = 0;
            for (int d = 0; d < fd; d++)
            {
                long b0 = _d.totals[d];
                _d.totals[d] += dmg;
                foreach (var m in Diff(d).milestones) if (b0 < m.damage && _d.totals[d] >= m.damage) lowerOpened++;
            }
            _d.totals[fd] += dmg;
            bool newBest = dmg > _d.bests[fd];
            if (newBest) _d.bests[fd] = dmg;
            Changed();

            int opened = 0;
            for (int i = 0; i < df.milestones.Count; i++)
                if (before < df.milestones[i].damage && _d.totals[fd] >= df.milestones[i].damage) opened++;
            string detail = $"<size=110%>주간 보스 — {BossName}  <color={df.hex}>{df.name}</color> <size=80%>Lv.{df.level}</size></size>\n" +
                            $"<color=#FFD36B>이번 점수</color>  <b>{dmg:N0}</b>" + (newBest ? "  <color=#7CFF9A>최고 기록!</color>" : "") + "\n" +
                            (bonus > 0f ? $"<size=85%><color=#9AA3B5>준 피해 {raw:N0}</color>  <color=#7CFF9A>보너스 캐릭터 {FeaturedIn(_party)}명 +{bonus * 100f:0}%</color></size>\n" : "") +
                            $"<color=#9AA3B5>이번 주 누적({df.name})</color>  {_d.totals[fd]:N0}" +
                            (opened > 0 ? $"\n<color=#7CFF9A>누적 보상 {opened}단계 달성 — 주간 보스 화면에서 받으세요</color>" : "") +
                            (lowerOpened > 0 ? $"\n<color=#7CFF9A>아래 난이도 누적 보상 {lowerOpened}단계도 달성</color>" : "") +
                            $"\n<size=80%><color=#9AA3B5>오늘 남은 도전 {AttemptsLeft} / {AttemptsPerDay}  ·  버틴 시간 {SurvivedSeconds}초 / {TimeLimit:0}초  ·  격노 {_enrage}단계</color></size>";
            _hud?.SetResultDetail(detail);
            CleanupBoss();
        }

        /// <summary>방금(또는 지금) 전투에서 버틴 시간(초) · 격노 단계 — 월드 레이드 결과 문구용.</summary>
        public int SurvivedSeconds => Mathf.CeilToInt(TimeLimit - Mathf.Max(0f, _timeLeft));
        public int EnrageStage => _enrage;

        private void CleanupBoss()
        {
            if (_boss == null) return;
            // 전투가 끝난 뒤 정리(전투 중에는 엔진이 참조)
            if (_boss.skills != null) foreach (var sk in _boss.skills) if (sk != null) Destroy(sk, 1f);
            Destroy(_boss, 1f);
            _boss = null;
        }

        [ContextMenu("테스트: 도전 횟수 초기화")]
        private void ResetAttempts() { _d.used = 0; Changed(); }
    }
}
