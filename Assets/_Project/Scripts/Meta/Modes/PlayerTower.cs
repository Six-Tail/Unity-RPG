using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;
using SubcultureRPG.Battle.UI;
using SubcultureRPG.Meta.UI;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 무한의 탑: 1층부터 끝없이 올라가는 도전 콘텐츠. 행동력 · 도전 횟수 없음, 지면 다시 도전.
    /// · 층의 적은 늘 같습니다(층 번호로 정해짐). 메인 스테이지의 적 유닛을 빌려 층에 맞게 레벨 · 배율만 조정
    /// · 레벨: 2 + 층 × 0.8(최대 Lv.80), 95층부터는 층마다 능력치 +4%. 10층마다 보스(장 보스 유닛 + 강화)
    /// · 층마다 첫 클리어 보상(금화 · 경험치 재료, 5층마다 크리스탈, 10층마다 크리스탈 · 교본, 50층마다 픽업 뽑기권)
    /// · 이기면 결과 창에서 '다음 층' 버튼(3초 뒤 자동)
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(PlayerTower.Instance).
    /// </summary>
    public class PlayerTower : MonoBehaviour
    {
        public const int BossEvery = 10;
        private const string SaveKey = "tower";

        [Serializable] private class SaveData { public int cleared; public long tries; }

        private SaveData _d = new();
        private bool _loaded;
        public event Action OnChanged;

        private static PlayerTower _instance;
        public static PlayerTower Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<PlayerTower>();
                if (_instance == null) _instance = new GameObject("PlayerTower").AddComponent<PlayerTower>();
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
            if (_loaded) return;
            _loaded = true;
            if (!SaveStore.HasKey(SaveKey)) return;
            try { _d = JsonUtility.FromJson<SaveData>(SaveStore.GetString(SaveKey)) ?? new SaveData(); }
            catch { _d = new SaveData(); }
        }

        private void Save()
        {
            SaveStore.SetString(SaveKey, JsonUtility.ToJson(_d));
            SaveStore.Save();
        }

        private void Changed() { Save(); OnChanged?.Invoke(); }

        /// <summary>깬 가장 높은 층(0 = 아직 없음).</summary>
        public int Cleared { get { EnsureLoaded(); return _d.cleared; } }
        /// <summary>지금 도전할 층.</summary>
        public int Next => Cleared + 1;

        // ---------------- 층 규칙(튜닝 지점) ----------------

        public static bool IsBoss(int floor) => floor % BossEvery == 0;

        public static int EnemyLevel(int floor) => Mathf.Clamp(Mathf.RoundToInt(2 + floor * 0.8f), 1, Leveling.MaxLevel) + (IsBoss(floor) ? 2 : 0);

        /// <summary>능력치 배율: 95층부터 층마다 +4%, 보스는 ×1.3.</summary>
        public static float StatMul(int floor, bool boss) => (1f + 0.04f * Mathf.Max(0, floor - 95)) * (boss ? 1.3f : 1f);

        public static int EnemyCount(int floor) => floor < 10 ? 2 : floor < 30 ? 3 : 4;

        /// <summary>첫 클리어 보상.</summary>
        public static RewardBundle Reward(int floor)
        {
            var r = new RewardBundle().Gold(2000 + 150 * floor);
            r.Mat(floor < 40 ? MaterialType.ExpPotion : MaterialType.ExpLarge, floor < 40 ? 2 : 1 + floor / 60);
            if (floor % 5 == 0) r.Crystals(40);
            if (floor % 10 == 0) r.Crystals(100).Mat(floor < 40 ? MaterialType.SkillBookBlue : MaterialType.SkillBookPurple, 2);
            if (floor % 50 == 0) r.Pickup(1);
            if (floor % 25 == 0) r.Mat(MaterialType.RefineStone, 1);   // 재련석(희귀 재료)
            return r;
        }

        // ---------------- 적 ----------------

        private struct Pool { public List<BattleUnitData> minions, bosses; }
        private static Pool _pool;

        /// <summary>메인 스테이지 적 유닛을 장 순서대로 모음(일반 · 장 보스).</summary>
        private static Pool Units()
        {
            if (_pool.minions != null && _pool.minions.Count > 0) return _pool;
            var p = new Pool { minions = new List<BattleUnitData>(), bosses = new List<BattleUnitData>() };
            var screen = FindAnyObjectByType<StageScreen>();
            var db = screen != null ? screen.Database : null;
            if (db != null)
                foreach (var s in db.Ordered(StageDifficulty.Easy))
                {
                    if (s == null || s.enemies == null) continue;
                    bool bossStage = s.number >= 6;
                    BattleUnitData strongest = null;
                    foreach (var e in s.enemies)
                    {
                        if (e == null || e.unit == null) continue;
                        if (strongest == null || e.unit.baseStats.maxHP > strongest.baseStats.maxHP) strongest = e.unit;
                        if (!p.minions.Contains(e.unit)) p.minions.Add(e.unit);
                    }
                    if (bossStage && strongest != null && !p.bosses.Contains(strongest)) p.bosses.Add(strongest);
                }
            p.minions.RemoveAll(u => p.bosses.Contains(u));
            _pool = p;
            return p;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _pool = default;

        /// <summary>층의 적 구성(원본 유닛, 레벨 · 배율 적용 전). 같은 층은 늘 같음.</summary>
        public static List<(BattleUnitData unit, bool boss)> Lineup(int floor)
        {
            var list = new List<(BattleUnitData, bool)>();
            var p = Units();
            if (p.minions.Count == 0) return list;
            var rng = new System.Random(floor * 92821 + 7);
            // 층이 오를수록 뒤쪽 장의 적이 나옴(80층 근처에서 마지막 장)
            int span = Mathf.Max(4, p.minions.Count / 4);
            int center = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(floor, 80) / 80f * (p.minions.Count - 1)), 0, p.minions.Count - 1);
            int lo = Mathf.Clamp(center - span / 2, 0, Mathf.Max(0, p.minions.Count - span));
            int hi = Mathf.Min(p.minions.Count, lo + span);
            int count = EnemyCount(floor);
            bool boss = IsBoss(floor) && p.bosses.Count > 0;
            for (int i = 0; i < count - (boss ? 1 : 0); i++) list.Add((p.minions[rng.Next(lo, hi)], false));
            if (boss)
            {
                int b = Mathf.Clamp((floor / BossEvery - 1) % p.bosses.Count, 0, p.bosses.Count - 1);
                list.Insert(list.Count / 2, (p.bosses[b], true));   // 보스는 가운데
            }
            return list;
        }

        /// <summary>층의 약점(적 전원 합집합, 화면 표시용).</summary>
        public static List<Element> Weaknesses(int floor)
        {
            var set = new List<Element>();
            foreach (var (u, _) in Lineup(floor))
                if (u.weaknesses != null) foreach (var e in u.weaknesses) if (!set.Contains(e)) set.Add(e);
            return set;
        }

        // ---------------- 전투 ----------------

        private readonly List<BattleUnitData> _spawned = new();
        private List<CharacterData> _party;
        private int _floor;
        private bool _pending, _active;
        private BattleController _controller;
        private BattleHUD _hud;

        /// <summary>도전(편성 화면의 '출격!'). floor = 0이면 지금 층.</summary>
        public bool Fight(int floor, IList<CharacterData> party)
        {
            if (party == null || party.Count == 0) return false;
            var launcher = FindAnyObjectByType<BattleLauncher>();
            if (launcher == null) return false;
            floor = Mathf.Clamp(floor <= 0 ? Next : floor, 1, Next);
            var lineup = Lineup(floor);
            if (lineup.Count == 0) { _hud?.ShowResultToast("적 데이터가 없습니다"); return false; }

            Hook();
            Cleanup();
            _floor = floor;
            _party = new List<CharacterData>(party);
            var enemies = new List<BattleUnitData>();
            foreach (var (src, boss) in lineup)
            {
                var u = Instantiate(src);
                u.name = $"Tower{floor}_{src.DisplayName}";
                u.level = EnemyLevel(floor);
                float m = StatMul(floor, boss);
                if (!Mathf.Approximately(m, 1f))
                {
                    var s = u.baseStats;
                    s.maxHP = Mathf.RoundToInt(s.maxHP * m); s.hpGrowth = Mathf.RoundToInt(s.hpGrowth * m);
                    s.atk = Mathf.RoundToInt(s.atk * m); s.atkGrowth = Mathf.RoundToInt(s.atkGrowth * m);
                    s.def = Mathf.RoundToInt(s.def * m); s.defGrowth = Mathf.RoundToInt(s.defGrowth * m);
                }
                _spawned.Add(u);
                enemies.Add(u);
            }
            _d.tries++;
            Save();
            _pending = true;
            if (_hud != null)
            {
                int ch = Mathf.Clamp(1 + (Mathf.Min(floor, 80) - 1) * 15 / 80, 1, 15);
                _hud.SetBackground("Backgrounds/battle_tower", "Backgrounds/tower", $"Backgrounds/battle_ch{ch}", "Backgrounds/battle");
                int f = floor;
                _hud.SetRetryHandler(() => Fight(f, _party) ? null : "도전할 수 없습니다");
                _hud.SetExitLabel("탑으로");
                _hud.SetRoundLimit(0);
            }
            launcher.Launch(_party, enemies);
            return true;
        }

        public void Leave()
        {
            _active = false; _pending = false;
            if (_hud != null) { _hud.SetRetryHandler(null); _hud.SetResultNext(null, null); }
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
            int floor = _floor;
            string head = $"<size=110%>무한의 탑 — {floor}층{(IsBoss(floor) ? "  <color=#FF9A6B>보스</color>" : "")}</size>\n";
            if (winner != Team.Ally)
            {
                _hud?.SetResultDetail(head + "<color=#9AA3B5>패배 — 육성하거나 약점 속성 캐릭터로 편성을 바꿔 다시 도전해 보세요</color>");
                Cleanup();
                return;
            }
            bool first = floor > _d.cleared;
            string detail = head;
            if (first)
            {
                _d.cleared = floor;
                var r = Reward(floor);
                r.Grant();
                detail += $"<color=#7CFF9A>{floor}층 돌파!</color>\n<color=#FFD36B>첫 클리어 보상</color>  {r.Describe()}";
            }
            else detail += "<color=#9AA3B5>이미 깬 층입니다 (보상은 첫 클리어 때만)</color>";
            Changed();
            _hud?.SetResultDetail(detail + $"\n<size=80%><color=#9AA3B5>최고 기록 {_d.cleared}층</color></size>");
            var party = _party;
            int next = Next;
            _hud?.SetResultNext($"{next}층 도전", () => Fight(next, party), 3f);
            Cleanup();
        }

        private void Cleanup()
        {
            foreach (var u in _spawned) if (u != null) Destroy(u, 1f);
            _spawned.Clear();
        }

        [ContextMenu("테스트: 10층 올리기")]
        private void TestUp() { _d.cleared += 10; Changed(); }

        [ContextMenu("테스트: 탑 기록 초기화")]
        private void TestReset() { _d = new SaveData(); Changed(); }
    }
}
