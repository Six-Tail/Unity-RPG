using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Meta.Shop;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 파견(방치형 보상): 캐릭터를 정해진 시간 동안 보내 두면 재료를 가져옵니다. 접속하지 않아도 시간이 흐릅니다.
    /// · 의뢰 게시판: 하루(05:00)마다 새 의뢰 6개. 크리스탈 30으로 다시 뽑기 가능
    /// · 동시에 파견 4팀. 한 캐릭터는 한 의뢰에만(파견 중에도 전투 · 육성은 그대로 가능)
    /// · 의뢰마다 인원 · 최소 레벨 · 추천 조건(속성 · 역할). 추천 조건을 모두 맞추면 보상 +50%
    /// · 다 끝나면 '받기'. 끝나기 전 '취소'하면 보상 없이 돌아옴
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(PlayerDispatch.Instance).
    /// </summary>
    public class PlayerDispatch : MonoBehaviour
    {
        public const int BoardSize = 6, MaxActive = 4, RerollCost = 30;
        public const float BonusRate = 0.5f;
        private const string SaveKey = "dispatch";

        public enum Kind { Gold, Exp, Skill, Ascension, Equip, Crystal }

        [Serializable]
        public class Mission
        {
            public string id;
            public int kind, grade, hours, members, minLevel;
            public int element = -1, role = -1;   // 추천 조건(-1 = 없음)
            public int place;
            public Kind K => (Kind)kind;
            public TimeSpan Duration => TimeSpan.FromHours(hours);
        }

        [Serializable]
        public class Active
        {
            public Mission mission;
            public List<string> team = new();
            public long start, end;
            public bool bonus;
            public DateTime End => new DateTime(end);
            public bool Done => DateTime.Now.Ticks >= end;
            public TimeSpan Left => Done ? TimeSpan.Zero : End - DateTime.Now;
            public float Progress => end <= start ? 1f : Mathf.Clamp01((DateTime.Now.Ticks - start) / (float)(end - start));
        }

        [Serializable]
        private class SaveData
        {
            public string day = "";
            public int seq, rerolls;
            public List<Mission> board = new();
            public List<Active> active = new();
            public int finished;   // 누적 완료 수
        }

        private SaveData _d = new();
        private bool _loaded;
        public event Action OnChanged;

        private static PlayerDispatch _instance;
        public static PlayerDispatch Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<PlayerDispatch>();
                if (_instance == null) _instance = new GameObject("PlayerDispatch").AddComponent<PlayerDispatch>();
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
                _d.board ??= new(); _d.active ??= new();
            }
            string today = PlayerShop.PeriodKey(ShopReset.Daily, DateTime.Now);
            if (_d.day != today)
            {
                _d.day = today;
                _d.rerolls = 0;
                FillBoard();
                Save();
            }
        }

        private void Save()
        {
            SaveStore.SetString(SaveKey, JsonUtility.ToJson(_d));
            SaveStore.Save();
        }

        private void Changed() { Save(); OnChanged?.Invoke(); }

        // ---------------- 의뢰 만들기 ----------------

        private static readonly string[] Places =
        {
            "고요한 숲길 순찰", "낡은 광산 조사", "항구 화물 호위", "별빛 유적 탐사", "서리 언덕 정찰", "화산 지대 채집",
            "폐허 도서관 정리", "번개 평원 측량", "마을 축제 경비", "심연 입구 감시", "바람 계곡 구조", "왕립 학회 의뢰",
        };

        public static string PlaceName(Mission m) => Places[Mathf.Abs(m.place) % Places.Length];
        public static string GradeName(int g) => g switch { 0 => "C", 1 => "B", 2 => "A", _ => "S" };
        public static Color GradeColor(int g) => g switch
        {
            0 => new Color(0.6f, 0.85f, 0.6f), 1 => new Color(0.5f, 0.75f, 1f), 2 => new Color(0.8f, 0.6f, 1f), _ => new Color(1f, 0.8f, 0.35f),
        };

        public static string KindName(Kind k) => k switch
        {
            Kind.Gold => "금화", Kind.Exp => "경험치 재료", Kind.Skill => "스킬 교본", Kind.Ascension => "돌파 재료",
            Kind.Equip => "장비 강화석", _ => "크리스탈",
        };

        /// <summary>등급별 파견 시간(시간) — 등급이 높을수록 오래 걸림. C · B · A · S.</summary>
        private static readonly int[][] HoursByGrade = { new[] { 1, 2 }, new[] { 4, 6 }, new[] { 8, 10 }, new[] { 12, 16 } };

        /// <summary>등급별 보상 배율(시간당) — C 1.0 · B 1.6 · A 2.4 · S 3.2.</summary>
        public static readonly float[] GradeMul = { 1f, 1.6f, 2.4f, 3.2f };

        /// <summary>
        /// 모든 의뢰에 붙는 크리스탈 = 등급 기본값 + 시간당 × 시간. 하루에 고등급 몇 번만 돌려도 충분하도록 기본값을 크게.
        /// C 10 + 2/시간 · B 25 + 3/시간 · A 45 + 4/시간 · S 80 + 5/시간 (예: A 10시간 85개 · S 16시간 160개)
        /// </summary>
        public static readonly int[] CrystalBase = { 10, 25, 45, 80 };
        public static readonly float[] CrystalPerHour = { 2f, 3f, 4f, 5f };

        private Mission NewMission(System.Random rng)
        {
            int grade = rng.Next(100) switch { < 25 => 0, < 55 => 1, < 85 => 2, _ => 3 };   // C 25% · B 30% · A 30% · S 15%
            var m = new Mission
            {
                id = "d" + (++_d.seq),
                grade = grade,
                hours = HoursByGrade[grade][rng.Next(HoursByGrade[grade].Length)],
                place = rng.Next(Places.Length),
                minLevel = new[] { 1, 10, 25, 40 }[grade],
            };
            int k = rng.Next(100);
            // 크리스탈은 모든 의뢰에 붙으므로, 주 보상 종류는 재료 쪽에서만 고름
            m.kind = (int)(k < 25 ? Kind.Gold : k < 50 ? Kind.Exp : k < 68 ? Kind.Skill : k < 84 ? Kind.Ascension : Kind.Equip);
            m.members = Mathf.Clamp(1 + grade / 2 + (m.hours >= 8 ? 1 : 0), 1, 3);
            // 추천 조건: 속성은 거의 항상, 역할은 등급이 높을수록 자주
            m.element = rng.Next(100) < 85 ? rng.Next(Traits.AllElements.Length) : -1;
            if (m.K == Kind.Ascension && m.element < 0) m.element = rng.Next(Traits.AllElements.Length);
            m.role = rng.Next(100) < 25 + grade * 20 ? rng.Next(4) : -1;
            return m;
        }

        private void FillBoard()
        {
            var rng = new System.Random(Environment.TickCount ^ (_d.seq * 7919));
            _d.board.Clear();
            for (int i = 0; i < BoardSize; i++) _d.board.Add(NewMission(rng));
        }

        // ---------------- 보상 ----------------

        /// <summary>의뢰 보상(bonus = 추천 조건 달성 +50%). 튜닝 지점.</summary>
        public static RewardBundle RewardOf(Mission m, bool bonus)
        {
            int g = Mathf.Clamp(m.grade, 0, 3);
            float b = bonus ? 1f + BonusRate : 1f;
            float h = m.hours * GradeMul[g] * b;   // 시간이 길고 등급이 높을수록 많이
            int N(float v) => Mathf.Max(1, Mathf.RoundToInt(v));
            // 모든 의뢰: 크리스탈(등급별 시간당, 추천 조건 보너스 적용)
            var r = new RewardBundle().Crystals(N((CrystalBase[g] + m.hours * CrystalPerHour[g]) * b));
            switch (m.K)
            {
                case Kind.Gold: r.Gold(N(3000 * h)); break;
                case Kind.Exp:
                    if (m.grade >= 2) r.Mat(MaterialType.ExpLarge, N(h * 0.6f));
                    else r.Mat(MaterialType.ExpPotion, N(h * 1.2f));
                    break;
                case Kind.Skill:
                    r.Mat(m.grade >= 3 ? MaterialType.SkillBookPurple : m.grade >= 1 ? MaterialType.SkillBookBlue : MaterialType.SkillBook,
                          N(h * (m.grade >= 3 ? 0.25f : m.grade >= 1 ? 0.5f : 1f)));
                    break;
                case Kind.Ascension:
                {
                    var e = m.element >= 0 ? Traits.AllElements[m.element] : Element.Physical;
                    r.Mat(Materials.Ascension(e, 1), N(h * 1.2f));
                    if (m.grade >= 2) r.Mat(Materials.Ascension(e, 2), N(h * 0.25f));
                    break;
                }
                case Kind.Equip: r.Mat(MaterialType.EquipOre, N(h * 4f)); break;
                default: r.Gold(N(3000 * h)); break;   // 옛 '크리스탈 의뢰'(저장된 것) — 크리스탈은 위에서 이미 줌
            }
            return r;
        }

        // ---------------- 조회 ----------------

        /// <summary>새 의뢰가 오기까지(05:00).</summary>
        public static TimeSpan UntilNewDay => PlayerShop.UntilReset(ShopReset.Daily, DateTime.Now) ?? TimeSpan.Zero;

        public IReadOnlyList<Mission> Board { get { EnsureLoaded(); return _d.board; } }
        public IReadOnlyList<Active> Actives { get { EnsureLoaded(); return _d.active; } }
        public int Finished => _d.finished;
        public int RerollsToday => _d.rerolls;

        public int DoneCount
        {
            get
            {
                EnsureLoaded();
                int n = 0;
                foreach (var a in _d.active) if (a.Done) n++;
                return n;
            }
        }

        /// <summary>파견 중인 캐릭터인지.</summary>
        public bool IsBusy(CharacterData c)
        {
            string key = PlayerRoster.KeyOf(c);
            foreach (var a in Actives) if (a.team.Contains(key)) return true;
            return false;
        }

        public static bool MeetsLevel(Mission m, CharacterData c, PlayerRoster roster) =>
            roster != null && roster.LevelOf(c) >= m.minLevel;

        /// <summary>추천 조건을 모두 맞췄는지(속성: 1명 이상 · 역할: 1명 이상).</summary>
        public static bool BonusMet(Mission m, IList<CharacterData> team)
        {
            if (team == null || team.Count == 0) return false;
            if (m.element < 0 && m.role < 0) return true;
            bool el = m.element < 0, ro = m.role < 0;
            foreach (var c in team)
            {
                if (c == null) continue;
                if (m.element >= 0 && (int)c.element == (int)Traits.AllElements[m.element]) el = true;
                if (m.role >= 0 && (int)c.role == m.role) ro = true;
            }
            return el && ro;
        }

        // ---------------- 동작 ----------------

        /// <summary>파견 보내기. 실패 사유(성공이면 null).</summary>
        public string Send(Mission m, IList<CharacterData> team)
        {
            EnsureLoaded();
            var roster = FindAnyObjectByType<PlayerRoster>();
            if (m == null || !_d.board.Contains(m)) return "의뢰를 찾을 수 없습니다";
            if (_d.active.Count >= MaxActive) return $"동시에 {MaxActive}팀까지 보낼 수 있습니다";
            if (team == null || team.Count != m.members) return $"{m.members}명을 골라 주세요";
            foreach (var c in team)
            {
                if (c == null) return "캐릭터를 골라 주세요";
                if (IsBusy(c)) return $"{c.displayName}은(는) 이미 파견 중입니다";
                if (!MeetsLevel(m, c, roster)) return $"Lv.{m.minLevel} 이상만 보낼 수 있습니다";
            }
            var a = new Active { mission = m, start = DateTime.Now.Ticks, bonus = BonusMet(m, team) };
            a.end = a.start + m.Duration.Ticks;
            foreach (var c in team) a.team.Add(PlayerRoster.KeyOf(c));
            _d.active.Add(a);
            _d.board.Remove(m);
            Changed();
            return null;
        }

        /// <summary>끝난 파견 받기(받은 보상, 아직이면 null).</summary>
        public RewardBundle Claim(Active a)
        {
            if (a == null || !a.Done || !_d.active.Contains(a)) return null;
            var r = RewardOf(a.mission, a.bonus);
            r.Grant();
            _d.active.Remove(a);
            _d.finished++;
            Changed();
            return r;
        }

        /// <summary>끝난 파견을 모두 받음(합친 보상, 없으면 null).</summary>
        public RewardBundle ClaimAll()
        {
            var total = new RewardBundle();
            int n = 0;
            foreach (var a in new List<Active>(_d.active))
            {
                if (!a.Done) continue;
                var r = Claim(a);
                if (r == null) continue;
                n++;
                total.Crystals(r.crystals).Gold(r.gold);
                if (r.mats != null) foreach (var mm in r.mats) total.Mat(mm.Type, mm.count);
            }
            return n > 0 ? total : null;
        }

        /// <summary>취소: 보상 없이 돌아오고, 의뢰는 게시판으로 돌아감.</summary>
        public void Cancel(Active a)
        {
            if (a == null || !_d.active.Remove(a)) return;
            if (_d.board.Count < BoardSize) _d.board.Add(a.mission);
            Changed();
        }

        /// <summary>게시판 다시 뽑기(크리스탈). 실패 사유(성공이면 null).</summary>
        public string Reroll()
        {
            var wallet = FindAnyObjectByType<PlayerWallet>();
            if (wallet == null || !wallet.Spend(RerollCost)) return $"크리스탈이 부족합니다 (필요 {RerollCost})";
            _d.rerolls++;
            FillBoard();
            Changed();
            return null;
        }

        [ContextMenu("테스트: 진행 중인 파견 바로 끝내기")]
        private void TestFinish()
        {
            foreach (var a in _d.active) a.end = DateTime.Now.Ticks - 1;
            Changed();
        }

        [ContextMenu("테스트: 의뢰 게시판 새로")]
        private void TestRefresh() { FillBoard(); Changed(); }
    }
}
