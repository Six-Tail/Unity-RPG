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
    /// · 하루 도전 3회(05:00 초기화) · 한 번에 아군 행동 20회까지(그 뒤 '전투 종료')
    /// · 약점 2속성이 매주 바뀜(월요일 05:00)
    /// · 누적 피해 단계별 보상(주간, 수동 수령) — 주가 바뀌면 누적 · 수령 기록 초기화
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(WorldRaid.Instance).
    /// </summary>
    public class WorldRaid : MonoBehaviour
    {
        public const string BossName = "별을 삼키는 자";
        public const int AttemptsPerDay = 3, ActionLimit = 20;
        private const string SaveKey = "raid";

        /// <summary>주차별 약점(돌아가며).</summary>
        private static readonly Element[][] WeakRotation =
        {
            new[] { Element.Fire, Element.Ice }, new[] { Element.Lightning, Element.Wind }, new[] { Element.Physical, Element.Fire },
            new[] { Element.Ice, Element.Lightning }, new[] { Element.Wind, Element.Physical },
        };

        /// <summary>누적 피해 보상 단계.</summary>
        public class Milestone
        {
            public long damage;
            public string label;
            public Action<PlayerWallet, PlayerInventory> grant;
        }

        public static readonly List<Milestone> Milestones = new()
        {
            new() { damage = 50_000, label = "금화 20,000", grant = (w, i) => w?.AddGold(20000) },
            new() { damage = 150_000, label = "대형 경험치 물약 ×5", grant = (w, i) => i?.Add(MaterialType.ExpLarge, 5) },
            new() { damage = 400_000, label = "크리스탈 100", grant = (w, i) => w?.Add(100) },
            new() { damage = 1_000_000, label = "상급 스킬 교본 ×3", grant = (w, i) => i?.Add(MaterialType.SkillBookPurple, 3) },
            new() { damage = 2_500_000, label = "크리스탈 200 · 행동력 물약 ×2", grant = (w, i) => { w?.Add(200); i?.Add(MaterialType.StaminaLarge, 2); } },
            new() { damage = 6_000_000, label = "전설의 비전서 ×1", grant = (w, i) => i?.Add(MaterialType.SkillLegend, 1) },
            new() { damage = 15_000_000, label = "픽업 뽑기권 ×1 · 크리스탈 300", grant = (w, i) => { w?.AddTickets(SubcultureRPG.Gacha.GachaBannerType.Pickup, 1); w?.Add(300); } },
        };

        [Serializable]
        private class SaveData
        {
            public string week = "", day = "";
            public long total, best;
            public int used, claimed;   // claimed: 보상 단계 비트
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
                if (PlayerPrefs.HasKey(SaveKey))
                {
                    try { _d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)) ?? new SaveData(); }
                    catch { _d = new SaveData(); }
                }
            }
            // 주 · 하루가 바뀌었으면 초기화
            var now = DateTime.Now;
            string week = PlayerShop.PeriodKey(ShopReset.Weekly, now), day = PlayerShop.PeriodKey(ShopReset.Daily, now);
            bool dirty = false;
            if (_d.week != week) { _d.week = week; _d.total = 0; _d.best = 0; _d.claimed = 0; dirty = true; }
            if (_d.day != day) { _d.day = day; _d.used = 0; dirty = true; }
            if (dirty) Save();
        }

        private void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_d));
            PlayerPrefs.Save();
        }

        private void Changed() { Save(); OnChanged?.Invoke(); }

        public long TotalDamage { get { EnsureLoaded(); return _d.total; } }
        public long BestDamage { get { EnsureLoaded(); return _d.best; } }
        public int AttemptsLeft { get { EnsureLoaded(); return Mathf.Max(0, AttemptsPerDay - _d.used); } }
        public bool IsClaimed(int i) => (_d.claimed & (1 << i)) != 0;
        public bool CanClaim(int i) => !IsClaimed(i) && TotalDamage >= Milestones[i].damage;
        public int ClaimableCount { get { int n = 0; for (int i = 0; i < Milestones.Count; i++) if (CanClaim(i)) n++; return n; } }

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

        public static TimeSpan UntilWeekReset => PlayerShop.UntilReset(ShopReset.Weekly, DateTime.Now) ?? TimeSpan.Zero;

        public bool Claim(int i)
        {
            if (!CanClaim(i)) return false;
            Milestones[i].grant(FindAnyObjectByType<PlayerWallet>(), FindAnyObjectByType<PlayerInventory>());
            _d.claimed |= 1 << i;
            Changed();
            return true;
        }

        // ---------------- 보스 ----------------

        /// <summary>보스 레벨: 내 주력 4명 평균(최소 10).</summary>
        public static int BossLevel()
        {
            var roster = FindAnyObjectByType<PlayerRoster>();
            if (roster == null) return 10;
            var top = CombatPower.All(roster, FindAnyObjectByType<PlayerEquipment>());
            int n = 0, sum = 0;
            foreach (var kv in top) { if (n >= 4) break; sum += roster.LevelOf(kv.Key); n++; }
            return Mathf.Clamp(n > 0 ? sum / n : 10, 10, Leveling.MaxLevel);
        }

        private static BattleSkill Skill(string name, SkillCategory cat, TargetType t, float power, int energy, StatusType eff = StatusType.None, float mag = 0f)
        {
            var s = ScriptableObject.CreateInstance<BattleSkill>();
            s.skillName = name; s.category = cat; s.target = t; s.power = power; s.hits = 1;
            s.energyGain = energy; s.element = Element.Physical; s.breakDamage = 0;
            s.applyEffect = eff; s.effectMagnitude = mag; s.effectDuration = 2; s.effectChance = 1f;
            return s;
        }

        private BattleUnitData BuildBoss()
        {
            var u = ScriptableObject.CreateInstance<BattleUnitData>();
            u.displayName = BossName;
            u.level = BossLevel();
            u.element = Element.Physical;
            u.weaknesses = new List<Element>(Weaknesses);
            u.maxToughness = 420;
            u.baseStats = new BattleStats
            {
                maxHP = 99_999_999, hpGrowth = 0,
                atk = 120, atkGrowth = 12, def = 60, defGrowth = 9, spd = 92, critRate = 0.05f, critDmg = 0.5f,
            };
            u.skills = new List<BattleSkill>
            {
                Skill("포식", SkillCategory.Basic, TargetType.SingleEnemy, 1.1f, 25),
                Skill("별빛 붕괴", SkillCategory.Skill, TargetType.AllEnemies, 0.7f, 30, StatusType.DefDown, 0.15f),
                Skill("사건의 지평선", SkillCategory.Ultimate, TargetType.AllEnemies, 1.6f, 0, StatusType.SpdDown, 0.2f),
            };
            return u;
        }

        // ---------------- 전투 ----------------

        private BattleUnitData _boss;
        private List<CharacterData> _party;
        private bool _paid, _pending, _active;
        private int _allyActions;
        private BattleController _controller;
        private BattleHUD _hud;

        public bool Fight(IList<CharacterData> party)
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
            CleanupBoss();
            _boss = BuildBoss();
            _party = new List<CharacterData>(party);
            _pending = true;
            if (_hud != null)
            {
                _hud.SetBackground("Backgrounds/battle_raid", "Backgrounds/raid", "Backgrounds/battle");
                _hud.SetRetryHandler(() => Fight(_party) ? null : "오늘 도전 횟수를 모두 썼습니다");
                _hud.SetExitLabel("주간 보스로");
            }
            launcher.Launch(_party, new List<BattleUnitData> { _boss });
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
            _controller.OnBattleStart += _ => { _active = _pending; _pending = false; _allyActions = 0; };
            _controller.OnActionResolved += HandleAction;
            _controller.OnBattleEnd += HandleEnd;
        }

        private void HandleAction(ActionResult r)
        {
            if (!_active || r.actor == null || r.actor.team != Team.Ally) return;
            _allyActions++;
            if (_allyActions >= ActionLimit && _controller.Engine != null && !_controller.Engine.IsOver)
            {
                // 행동 제한 도달 → 전투 종료(패배 아님)
                _controller.RequestEnd(Team.Enemy);
                _hud?.SetEndTexts("TIME UP", "전투 종료", new Color(1f, 0.82f, 0.4f));
            }
        }

        private void HandleEnd(Team winner)
        {
            if (!_active) return;
            _active = false;
            _paid = false;
            long dmg = 0;
            var eng = _controller != null ? _controller.Engine : null;
            if (eng != null && eng.enemies.Count > 0) dmg = eng.enemies[0].record.taken;
            long before = _d.total;
            _d.total += dmg;
            bool newBest = dmg > _d.best;
            if (newBest) _d.best = dmg;
            Changed();

            int opened = 0;
            for (int i = 0; i < Milestones.Count; i++)
                if (before < Milestones[i].damage && _d.total >= Milestones[i].damage) opened++;
            string detail = $"<size=110%>주간 보스 — {BossName}</size>\n" +
                            $"<color=#FFD36B>이번 피해</color>  <b>{dmg:N0}</b>" + (newBest ? "  <color=#7CFF9A>최고 기록!</color>" : "") + "\n" +
                            $"<color=#9AA3B5>이번 주 누적</color>  {_d.total:N0}" +
                            (opened > 0 ? $"\n<color=#7CFF9A>누적 보상 {opened}단계 달성 — 주간 보스 화면에서 받으세요</color>" : "") +
                            $"\n<size=80%><color=#9AA3B5>오늘 남은 도전 {AttemptsLeft} / {AttemptsPerDay}  ·  아군 행동 {Mathf.Min(_allyActions, ActionLimit)} / {ActionLimit}회</color></size>";
            _hud?.SetResultDetail(detail);
            CleanupBoss();
        }

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
