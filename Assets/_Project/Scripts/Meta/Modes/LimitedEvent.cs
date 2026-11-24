using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;
using SubcultureRPG.Battle.UI;
using SubcultureRPG.Meta.Shop;
using SubcultureRPG.Meta.UI;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 기간 한정 이벤트(오프라인 순환): 2주마다 테마가 바뀌는 축제.
    /// · 이벤트 전투(초급 · 중급 · 상급) 승리 → 축제 코인. 테마 속성 캐릭터를 데려가면 1명당 +10%(최대 +40%)
    /// · 하루 도전 5회(05:00 충전, 끝나기 전에 나가면 반환)
    /// · 누적 획득 코인 단계 보상(이벤트 기간 동안, 수동 수령)
    /// · 이벤트 상점: 코인으로 교환(이벤트마다 구매 한도 초기화). 이벤트가 끝나면 남은 코인은 사라짐
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(LimitedEvent.Instance).
    /// </summary>
    public class LimitedEvent : MonoBehaviour
    {
        public const int AttemptsPerDay = 5;
        public const int CycleDays = 14;
        public const float BonusPerMember = 0.10f;
        public const string CoinIcon = "Items/EventCoin";
        private const string SaveKey = "event";

        // ---------------- 테마 ----------------

        public class Theme
        {
            public string id, name, tagline;
            public Element element;
            public string minion, guardian;   // 적 이름(스테이지 데이터의 같은 이름 유닛을 빌려 씀)
            public string background;         // 전투 배경(Resources/Art/Backgrounds/…) — 이벤트 전용 그림이 없을 때 화면 배경으로도 씀
            public string host;               // 축제 진행 캐릭터(일러스트의 주인공) 이름
            public Color color;

            // 이벤트 전용 그림(Resources/Art/Events/…): 화면 배경 · 진행 캐릭터 일러스트 · 엠블럼
            public string ArtBackground => "Events/" + id + "_bg";
            public string ArtHost => "Events/" + id + "_host";
            public string ArtEmblem => "Events/" + id + "_emblem";
        }

        public static readonly Theme[] Themes =
        {
            new() { id = "ember", name = "홍염 축제", tagline = "꺼지지 않는 불꽃의 밤", element = Element.Fire,
                    minion = "불꽃 정령", guardian = "화염 수호자", background = "battle_ch11", host = "에스텔", color = new Color(1f, 0.5f, 0.3f) },
            new() { id = "frost", name = "서리 축제", tagline = "얼어붙은 별빛 아래에서", element = Element.Ice,
                    minion = "서리 정령", guardian = "서리 수호자", background = "battle_ch10", host = "오로라", color = new Color(0.55f, 0.8f, 1f) },
            new() { id = "thunder", name = "뇌명 축제", tagline = "천둥이 울리는 왕좌의 연회", element = Element.Lightning,
                    minion = "번개 정령", guardian = "뇌광 수호자", background = "battle_ch12", host = "레나", color = new Color(0.78f, 0.6f, 1f) },
        };

        // ---------------- 난이도 ----------------

        public class Difficulty
        {
            public string name;
            public int levelOffset, coins;
            public float statMul;
            public bool guardian;   // 수호자(보스) 포함
            public int minions;
        }

        public static readonly Difficulty[] Difficulties =
        {
            new() { name = "초급", levelOffset = -6, statMul = 1.0f, minions = 2, guardian = false, coins = 60 },
            new() { name = "중급", levelOffset = 0, statMul = 1.0f, minions = 2, guardian = true, coins = 120 },
            new() { name = "상급", levelOffset = 8, statMul = 1.25f, minions = 2, guardian = true, coins = 200 },
        };

        // ---------------- 누적 보상 ----------------

        public class Milestone
        {
            public int coins;
            public RewardBundle reward;
        }

        public static readonly List<Milestone> Milestones = new()
        {
            new() { coins = 500, reward = new RewardBundle().Gold(30000) },
            new() { coins = 1500, reward = new RewardBundle().Mat(MaterialType.SkillBookPurple, 3) },
            new() { coins = 3000, reward = new RewardBundle().Crystals(200) },
            new() { coins = 5000, reward = new RewardBundle().Mat(MaterialType.SkillLegend, 1) },
            new() { coins = 8000, reward = new RewardBundle().Pickup(2) },
            new() { coins = 12000, reward = new RewardBundle().Crystals(300).Mat(MaterialType.StaminaLarge, 2) },
        };

        // ---------------- 상점 ----------------

        public class ShopEntry
        {
            public RewardBundle reward;
            public int price, limit;
        }

        public static readonly List<ShopEntry> ShopItems = new()
        {
            new() { reward = new RewardBundle().Mat(MaterialType.SkillLegend, 1), price = 600, limit = 3 },
            new() { reward = new RewardBundle().Pickup(1), price = 400, limit = 3 },
            new() { reward = new RewardBundle().Standard(1), price = 250, limit = 5 },
            new() { reward = new RewardBundle().Crystals(100), price = 200, limit = 5 },
            new() { reward = new RewardBundle().Mat(MaterialType.StaminaLarge, 1), price = 120, limit = 5 },
            new() { reward = new RewardBundle().Mat(MaterialType.SkillBookPurple, 2), price = 150, limit = 10 },
            new() { reward = new RewardBundle().Mat(MaterialType.ExpCrystal, 2), price = 150, limit = 10 },
            new() { reward = new RewardBundle().Gold(20000), price = 100, limit = 20 },
        };

        // ---------------- 세이브 ----------------

        [Serializable]
        private class SaveData
        {
            public int cycle = -1;
            public string day = "";
            public int coins, earned, used, claimed;   // claimed: 누적 보상 단계 비트
            public List<int> bought = new();           // 상점 항목별 구매 수(이번 이벤트)
        }

        private SaveData _d = new();
        private bool _loaded;
        public event Action OnChanged;

        private static LimitedEvent _instance;
        public static LimitedEvent Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<LimitedEvent>();
                if (_instance == null) _instance = new GameObject("LimitedEvent").AddComponent<LimitedEvent>();
                _instance.EnsureLoaded();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
            EnsureLoaded();
        }

        // ---------------- 기간 ----------------

        /// <summary>기준일(월요일). 이 날 05:00부터 2주 단위로 이벤트가 바뀜.</summary>
        private static readonly DateTime Epoch = new(2024, 1, 1);

        private static DateTime GameDay(DateTime now) => now.AddHours(-ShopCatalog.ResetHour).Date;

        /// <summary>지금 이벤트 번호(2주마다 +1).</summary>
        public static int CycleIndex(DateTime now) => (int)Math.Floor((GameDay(now) - Epoch).TotalDays / CycleDays);

        public static Theme CurrentTheme => Themes[((CycleIndex(DateTime.Now) % Themes.Length) + Themes.Length) % Themes.Length];

        /// <summary>이번 이벤트가 끝나는 시각.</summary>
        public static DateTime EndsAt
        {
            get
            {
                int c = CycleIndex(DateTime.Now);
                return Epoch.AddDays((c + 1) * CycleDays).AddHours(ShopCatalog.ResetHour);
            }
        }

        public static TimeSpan Remaining => EndsAt - DateTime.Now;

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
            int cycle = CycleIndex(now);
            string day = PlayerShop.PeriodKey(ShopReset.Daily, now);
            bool dirty = false;
            if (_d.cycle != cycle)
            {
                // 새 이벤트: 코인 · 누적 · 수령 · 상점 한도 초기화
                _d.cycle = cycle; _d.coins = 0; _d.earned = 0; _d.claimed = 0; _d.bought = new List<int>();
                dirty = true;
            }
            if (_d.day != day) { _d.day = day; _d.used = 0; dirty = true; }
            _d.bought ??= new List<int>();
            while (_d.bought.Count < ShopItems.Count) { _d.bought.Add(0); dirty = true; }
            if (dirty) Save();
        }

        private void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_d));
            PlayerPrefs.Save();
        }

        private void Changed() { Save(); OnChanged?.Invoke(); }

        // ---------------- 조회 ----------------

        public int Coins { get { EnsureLoaded(); return _d.coins; } }
        public int Earned { get { EnsureLoaded(); return _d.earned; } }
        public int AttemptsLeft { get { EnsureLoaded(); return Mathf.Max(0, AttemptsPerDay - _d.used); } }

        public bool IsClaimed(int i) { EnsureLoaded(); return (_d.claimed & (1 << i)) != 0; }
        public bool CanClaim(int i) => !IsClaimed(i) && Earned >= Milestones[i].coins;

        public int ClaimableCount
        {
            get { int n = 0; for (int i = 0; i < Milestones.Count; i++) if (CanClaim(i)) n++; return n; }
        }

        /// <summary>홈 빨간 느낌표: 받을 누적 보상이 있거나 오늘 도전이 남아 있음.</summary>
        public bool HasSomethingToDo => ClaimableCount > 0 || AttemptsLeft > 0;

        public int Bought(int i) { EnsureLoaded(); return i < _d.bought.Count ? _d.bought[i] : 0; }
        public int ShopLeft(int i) => Mathf.Max(0, ShopItems[i].limit - Bought(i));

        public bool Claim(int i)
        {
            if (!CanClaim(i)) return false;
            Milestones[i].reward.Grant();
            _d.claimed |= 1 << i;
            Changed();
            return true;
        }

        /// <summary>상점 구매. 실패 사유(성공이면 null).</summary>
        public string Buy(int i)
        {
            EnsureLoaded();
            if (i < 0 || i >= ShopItems.Count) return "없는 상품입니다";
            var e = ShopItems[i];
            if (ShopLeft(i) <= 0) return "이번 이벤트 구매 한도에 도달했습니다";
            if (_d.coins < e.price) return "축제 코인이 부족합니다";
            _d.coins -= e.price;
            _d.bought[i]++;
            e.reward.Grant();
            Changed();
            return null;
        }

        /// <summary>파티의 테마 속성 보너스(0 ~ 0.4).</summary>
        public static float PartyBonus(IList<CharacterData> party)
        {
            if (party == null) return 0f;
            var el = CurrentTheme.element;
            int n = 0;
            foreach (var c in party) if (c != null && c.element == el) n++;
            return Mathf.Min(4, n) * BonusPerMember;
        }

        public static int CoinsFor(int difficulty, IList<CharacterData> party) =>
            Mathf.RoundToInt(Difficulties[Mathf.Clamp(difficulty, 0, Difficulties.Length - 1)].coins * (1f + PartyBonus(party)));

        /// <summary>적 레벨(난이도 반영).</summary>
        public static int EnemyLevel(int difficulty) =>
            Mathf.Clamp(WorldRaid.BossLevel() + Difficulties[Mathf.Clamp(difficulty, 0, Difficulties.Length - 1)].levelOffset, 1, Leveling.MaxLevel);

        // ---------------- 적 ----------------

        /// <summary>스테이지 데이터에서 같은 이름의 적 유닛을 찾음(이미 조정된 능력치를 그대로 씀).</summary>
        private static BattleUnitData FindUnit(string name)
        {
            var screen = FindAnyObjectByType<StageScreen>();
            var db = screen != null ? screen.Database : null;
            if (db == null) return null;
            BattleUnitData Search(List<StageData> list)
            {
                if (list == null) return null;
                foreach (var s in list)
                {
                    if (s == null || s.enemies == null) continue;
                    foreach (var e in s.enemies)
                        if (e != null && e.unit != null && e.unit.DisplayName == name) return e.unit;
                }
                return null;
            }
            return Search(db.Dungeons()) ?? Search(db.stages);
        }

        private BattleUnitData MakeEnemy(string name, int level, float statMul, bool guardian)
        {
            var src = FindUnit(name);
            BattleUnitData u;
            if (src != null) u = Instantiate(src);
            else
            {
                // 스테이지 데이터가 없는 씬: 간단한 유닛을 직접 만듦
                u = ScriptableObject.CreateInstance<BattleUnitData>();
                u.displayName = name;
                u.element = CurrentTheme.element;
                u.maxToughness = guardian ? 110 : 45;
                u.baseStats = guardian
                    ? new BattleStats { maxHP = 1600, atk = 116, def = 80, spd = 95, hpGrowth = 112, atkGrowth = 8, defGrowth = 6 }
                    : new BattleStats { maxHP = 800, atk = 108, def = 50, spd = 105, hpGrowth = 56, atkGrowth = 8, defGrowth = 4 };
                u.skills = new List<BattleSkill>();
            }
            u.name = $"Event_{name}_Lv{level}";
            u.level = level;
            if (!Mathf.Approximately(statMul, 1f))
            {
                var s = u.baseStats;
                s.maxHP = Mathf.RoundToInt(s.maxHP * statMul); s.hpGrowth = Mathf.RoundToInt(s.hpGrowth * statMul);
                s.atk = Mathf.RoundToInt(s.atk * statMul); s.atkGrowth = Mathf.RoundToInt(s.atkGrowth * statMul);
                s.def = Mathf.RoundToInt(s.def * statMul); s.defGrowth = Mathf.RoundToInt(s.defGrowth * statMul);
            }
            _spawned.Add(u);
            return u;
        }

        /// <summary>적의 약점(첫 적 기준, 화면 표시용).</summary>
        public static List<Element> Weaknesses
        {
            get
            {
                var u = FindUnit(CurrentTheme.minion);
                return u != null && u.weaknesses != null ? new List<Element>(u.weaknesses) : new List<Element>();
            }
        }

        // ---------------- 전투 ----------------

        private readonly List<BattleUnitData> _spawned = new();
        private List<CharacterData> _party;
        private int _difficulty;
        private bool _paid, _pending, _active;
        private BattleController _controller;
        private BattleHUD _hud;

        public bool Fight(int difficulty, IList<CharacterData> party)
        {
            if (party == null || party.Count == 0) return false;
            var launcher = FindAnyObjectByType<BattleLauncher>();
            if (launcher == null) return false;
            Refund();
            if (AttemptsLeft <= 0) { _hud?.ShowResultToast("오늘 도전 횟수를 모두 썼습니다 (매일 05:00 충전)"); return false; }
            _d.used++;
            _paid = true;
            Changed();

            Hook();
            CleanupEnemies();
            _difficulty = Mathf.Clamp(difficulty, 0, Difficulties.Length - 1);
            _party = new List<CharacterData>(party);
            var diff = Difficulties[_difficulty];
            var theme = CurrentTheme;
            int lv = EnemyLevel(_difficulty);
            var enemies = new List<BattleUnitData>();
            for (int i = 0; i < diff.minions; i++)
            {
                if (diff.guardian && i == 1) enemies.Add(MakeEnemy(theme.guardian, lv + 2, diff.statMul, true));   // 수호자는 가운데
                enemies.Add(MakeEnemy(theme.minion, lv, diff.statMul, false));
            }

            _pending = true;
            if (_hud != null)
            {
                _hud.SetBackground("Backgrounds/" + theme.background, "Backgrounds/battle");
                int d = _difficulty;
                _hud.SetRetryHandler(() => Fight(d, _party) ? null : "오늘 도전 횟수를 모두 썼습니다");
                _hud.SetExitLabel("이벤트로");
                _hud.SetRoundLimit(0);
            }
            launcher.Launch(_party, enemies);
            return true;
        }

        public void Leave()
        {
            Refund();
            _active = false; _pending = false;
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
            _controller.OnBattleStart += _ => { _active = _pending; _pending = false; };
            _controller.OnBattleEnd += HandleEnd;
        }

        private void HandleEnd(Team winner)
        {
            if (!_active) return;
            _active = false;
            _paid = false;   // 끝까지 싸웠으면 도전 횟수는 쓴 것

            var theme = CurrentTheme;
            var diff = Difficulties[_difficulty];
            string head = $"<size=110%>{theme.name} — {diff.name}</size>\n";
            if (winner != Team.Ally)
            {
                Changed();
                _hud?.SetResultDetail(head + "<color=#9AA3B5>보상 없음 — 테마 속성 캐릭터를 데려가거나 난이도를 낮춰 보세요</color>\n" +
                                      $"<size=80%><color=#9AA3B5>오늘 남은 도전 {AttemptsLeft} / {AttemptsPerDay}</color></size>");
                CleanupEnemies();
                return;
            }

            float bonus = PartyBonus(_party);
            int coins = CoinsFor(_difficulty, _party);
            int before = _d.earned;
            _d.coins += coins;
            _d.earned += coins;
            Changed();

            int opened = 0;
            foreach (var m in Milestones) if (before < m.coins && _d.earned >= m.coins) opened++;
            _hud?.SetResultDetail(head +
                $"<color=#FFD36B>축제 코인</color>  <b>+{coins:N0}</b>" +
                (bonus > 0f ? $"  <size=80%><color=#7CFF9A>({Traits.Name(theme.element)} 보너스 +{bonus * 100f:0}%)</color></size>" : "") + "\n" +
                $"<color=#9AA3B5>보유 {_d.coins:N0}  ·  이번 이벤트 누적 {_d.earned:N0}</color>" +
                (opened > 0 ? $"\n<color=#7CFF9A>누적 보상 {opened}단계 달성 — 이벤트 화면에서 받으세요</color>" : "") +
                $"\n<size=80%><color=#9AA3B5>오늘 남은 도전 {AttemptsLeft} / {AttemptsPerDay}</color></size>");
            CleanupEnemies();
        }

        private void CleanupEnemies()
        {
            // 전투가 끝난 뒤 정리(전투 중에는 엔진이 참조) — 복제본만 지움
            foreach (var u in _spawned) if (u != null) Destroy(u, 1f);
            _spawned.Clear();
        }

        // ---------------- 테스트 ----------------

        [ContextMenu("테스트: 도전 횟수 초기화")]
        private void ResetAttempts() { _d.used = 0; Changed(); }

        [ContextMenu("테스트: 축제 코인 +3,000")]
        private void GrantCoins() { _d.coins += 3000; _d.earned += 3000; Changed(); }
    }
}
