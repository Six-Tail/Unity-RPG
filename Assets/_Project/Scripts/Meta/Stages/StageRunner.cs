using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;
using SubcultureRPG.Battle.UI;

namespace SubcultureRPG.Meta
{
    /// <summary>스테이지 전투 1회 결과.</summary>
    public struct StageResult
    {
        public StageData stage;
        public bool win;
        public int stars;
        public bool firstClear;
        public int crystals;
        public int gold;
        public List<EquipmentItem> equipment;
        public int allyActions;
        public bool allSurvived;
    }

    /// <summary>
    /// 스테이지 전투 진행: 적 구성(레벨 적용) → 전투 시작 → 종료 시 별 평가·보상 지급·진행도 기록.
    /// 결과(별/보상)는 전투 HUD의 결과 화면에 함께 표시합니다. '다시'로 재도전해도 다시 평가·지급됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Stage Runner")]
    public class StageRunner : MonoBehaviour
    {
        [SerializeField] private BattleController controller;
        [SerializeField] private BattleLauncher launcher;
        [SerializeField] private BattleHUD hud;
        [SerializeField] private StageProgress progress;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerRoster roster;
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField, Tooltip("배속 해금 판정용(비우면 StageScreen의 것을 사용).")] private StageDatabase database;

        [SerializeField, Tooltip("승리 후 '다음 스테이지로' 자동 진행까지 초(0 = 자동 진행 안 함).")]
        private float autoNextSeconds = 5f;

        /// <summary>이 챕터를 클리어하면 전투 배속(×2) 해금.</summary>
        public const int SpeedUnlockChapter = 1;
        /// <summary>이 챕터를 클리어하면 3배속 해금.</summary>
        public const int Speed3UnlockChapter = 3;

        /// <summary>전투 배속 해금 여부(진행도 기준).</summary>
        public bool SpeedUnlocked => MaxSpeedIndex >= 1;

        /// <summary>해금된 최대 배속 단계: 0 = ×1, 1 = ×2 (챕터 1), 2 = ×3 (챕터 3).</summary>
        public int MaxSpeedIndex
        {
            get
            {
                if (database == null || progress == null) return 2;
                if (progress.IsChapterCleared(database, Speed3UnlockChapter)) return 2;
                return progress.IsChapterCleared(database, SpeedUnlockChapter) ? 1 : 0;
            }
        }

        private string ChapterLabel(int ch)
        {
            string name = database != null ? database.ChapterName(ch) : "";
            return $"챕터 {ch}" + (string.IsNullOrEmpty(name) ? "" : $" '{name}'");
        }

        /// <summary>진행 중인 스테이지(스테이지 전투가 아니면 null).</summary>
        public StageData Current { get; private set; }

        /// <summary>스테이지 전투가 끝났을 때(보상 지급 후).</summary>
        public event Action<StageResult> OnStageFinished;

        private readonly List<CharacterData> _party = new();
        private readonly List<BattleUnitData> _spawned = new();
        private int _allyActions;

        private void Awake()
        {
            if (controller == null) controller = FindAnyObjectByType<BattleController>();
            if (launcher == null) launcher = FindAnyObjectByType<BattleLauncher>();
            if (hud == null) hud = FindAnyObjectByType<BattleHUD>();
            if (progress == null) progress = FindAnyObjectByType<StageProgress>();
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            if (inventory == null) inventory = FindAnyObjectByType<PlayerInventory>();
            if (roster == null) roster = FindAnyObjectByType<PlayerRoster>();
            if (equipment == null) equipment = FindAnyObjectByType<PlayerEquipment>();
            if (database == null)
            {
                var screen = FindAnyObjectByType<SubcultureRPG.Meta.UI.StageScreen>();
                if (screen != null) database = screen.Database;
            }
        }

        private void Start() => ApplySpeedUnlock();

        /// <summary>진행도에 맞춰 전투 HUD의 배속 버튼 잠금/해금.</summary>
        public void ApplySpeedUnlock()
        {
            if (hud == null) return;
            int max = MaxSpeedIndex;
            string hint = max == 0 ? $"{ChapterLabel(SpeedUnlockChapter)} 클리어 시 2배속 해금"
                : max == 1 ? $"{ChapterLabel(Speed3UnlockChapter)} 클리어 시 3배속 해금" : "";
            hud.SetSpeedLimit(max, hint);
        }

        private void OnEnable()
        {
            if (progress != null) progress.OnChanged += ApplySpeedUnlock;
            if (controller == null) return;
            controller.OnBattleStart += HandleStart;
            controller.OnActionResolved += HandleAction;
            controller.OnBattleEnd += HandleEnd;
        }

        private void OnDisable()
        {
            if (progress != null) progress.OnChanged -= ApplySpeedUnlock;
            if (controller == null) return;
            controller.OnBattleStart -= HandleStart;
            controller.OnActionResolved -= HandleAction;
            controller.OnBattleEnd -= HandleEnd;
        }

        /// <summary>스테이지 출격.</summary>
        /// <summary>전투 배경 후보: 메인 = battle_ch{챕터}, 던전 = battle_dungeon_{종류} → 없으면 공용 battle.</summary>
        private static string[] BattleBackgrounds(StageData s)
        {
            string key = s.kind switch
            {
                StageKind.AscensionDungeon => "battle_dungeon_ascension",
                StageKind.GoldDungeon => "battle_dungeon_gold",
                StageKind.SkillBookDungeon => "battle_dungeon_book",
                StageKind.EquipmentDungeon => "battle_dungeon_equipment",
                StageKind.ExpDungeon => "battle_dungeon_exp",
                _ => "battle_ch" + s.chapter,
            };
            return new[] { "Backgrounds/" + key, "Backgrounds/battle" };
        }

        public bool Launch(StageData stage, IList<CharacterData> party)
        {
            if (stage == null || launcher == null || party == null || party.Count == 0) return false;

            CleanupSpawned();
            var enemies = new List<BattleUnitData>();
            if (stage.enemies != null)
                foreach (var e in stage.enemies)
                {
                    if (e == null || e.unit == null) continue;
                    var u = Instantiate(e.unit);              // 저작 에셋은 그대로 두고 레벨만 바꾼 복제본
                    u.name = $"{e.unit.name}_Lv{e.level}";
                    u.level = Mathf.Max(1, e.level);
                    if (!Mathf.Approximately(stage.enemyStatMultiplier, 1f)) ScaleStats(u.baseStats, stage.enemyStatMultiplier);
                    enemies.Add(u);
                    _spawned.Add(u);
                }
            if (enemies.Count == 0) { Debug.LogWarning($"[StageRunner] {stage.Code} 적 구성이 비어 있습니다."); return false; }

            Current = stage;
            _party.Clear();
            _party.AddRange(party);
            if (hud != null) hud.SetBackground(BattleBackgrounds(stage));
            launcher.Launch(party, enemies);
            return true;
        }

        /// <summary>메인 스테이지의 다음 스테이지(해금된 경우). 던전·마지막 스테이지는 null.</summary>
        public StageData NextStageOf(StageData stage)
        {
            if (stage == null || stage.IsDungeon || database == null) return null;
            var next = database.Next(stage);
            if (next == null) return null;
            return progress == null || progress.IsUnlocked(next, database) ? next : null;
        }

        /// <summary>같은 파티로 다음 스테이지 출격.</summary>
        private void LaunchNext(StageData next)
        {
            if (next == null || _party.Count == 0) return;
            Launch(next, new List<CharacterData>(_party));
        }

        /// <summary>난이도 보정: 복제본의 HP·ATK·DEF(기본치·성장치)에 배율.</summary>
        private static void ScaleStats(BattleStats s, float m)
        {
            if (s == null) return;
            s.maxHP = Mathf.RoundToInt(s.maxHP * m); s.hpGrowth = Mathf.RoundToInt(s.hpGrowth * m);
            s.atk = Mathf.RoundToInt(s.atk * m); s.atkGrowth = Mathf.RoundToInt(s.atkGrowth * m);
            s.def = Mathf.RoundToInt(s.def * m); s.defGrowth = Mathf.RoundToInt(s.defGrowth * m);
        }

        /// <summary>스테이지 모드 종료(허브 복귀 시).</summary>
        public void Clear()
        {
            Current = null;
            _party.Clear();
        }

        private void CleanupSpawned()
        {
            foreach (var u in _spawned) if (u != null) Destroy(u);
            _spawned.Clear();
        }

        private void OnDestroy() => CleanupSpawned();

        // ---------------- 전투 이벤트 ----------------

        private void HandleStart(BattleEngine engine) => _allyActions = 0;

        private void HandleAction(ActionResult r)
        {
            if (r.actor != null && r.actor.team == Team.Ally) _allyActions++;
        }

        private void HandleEnd(Team winner)
        {
            var stage = Current;
            if (stage == null) return;

            var result = new StageResult { stage = stage, win = winner == Team.Ally, allyActions = _allyActions };

            if (!result.win)
            {
                if (hud != null)
                    hud.SetResultDetail($"<size=110%>{stage.Code} {stage.stageName}</size>\n" +
                                        "<color=#9AA3B5>보상 없음 — 캐릭터를 육성하거나 파티를 바꿔 다시 도전하세요</color>");
                OnStageFinished?.Invoke(result);
                return;
            }

            // 별 평가
            bool allAlive = controller != null && controller.Engine != null &&
                            controller.Engine.allies.TrueForAll(u => u.IsAlive);
            bool fast = _allyActions <= stage.turnLimit;
            result.allSurvived = allAlive;
            result.stars = 1 + (allAlive ? 1 : 0) + (fast ? 1 : 0);

            // 진행도 + 보상
            int speedBefore = MaxSpeedIndex;
            bool hardWasLocked = database != null && progress != null && !progress.IsDifficultyUnlocked(database, StageDifficulty.Hard);
            bool hellWasLocked = database != null && progress != null && !progress.IsDifficultyUnlocked(database, StageDifficulty.Hell);
            result.firstClear = progress != null && progress.RecordClear(stage, result.stars);
            if (result.firstClear && stage.firstClearCrystals > 0)
            {
                result.crystals = stage.firstClearCrystals;
                if (wallet != null) wallet.Add(stage.firstClearCrystals);
            }
            if (stage.gold > 0)
            {
                result.gold = stage.gold;
                if (wallet != null) wallet.AddGold(stage.gold);
            }
            if (inventory != null && stage.rewards != null)
                foreach (var m in stage.rewards)
                    if (m != null && m.count > 0) inventory.Add(m.type, m.count);
            if (roster != null && stage.characterExp > 0)
                foreach (var c in _party)
                    if (c != null) roster.AddExp(c, stage.characterExp);
            if (equipment != null && stage.equipmentDrops > 0 && stage.equipmentSets != null && stage.equipmentSets.Count > 0)
            {
                result.equipment = new List<EquipmentItem>();
                for (int i = 0; i < stage.equipmentDrops; i++)
                {
                    var it = equipment.AddRandom(stage.equipmentSets, stage.equipmentRarityWeights);
                    if (it != null) result.equipment.Add(it);
                }
            }

            string detail = BuildRewardText(result, allAlive, fast);
            if (MaxSpeedIndex > speedBefore)
                detail += $"\n<color=#FFB347>{MaxSpeedIndex + 1}배속 해금!</color>  <size=80%>전투 화면 오른쪽 위 '배속' 버튼을 눌러 조절</size>";
            if (hardWasLocked && progress.IsDifficultyUnlocked(database, StageDifficulty.Hard))
                detail += $"\n<color={Difficulties.Hex(StageDifficulty.Hard)}>하드 난이도 개방!</color>  <size=80%>스테이지 화면 위쪽 난이도 버튼</size>";
            if (hellWasLocked && progress.IsDifficultyUnlocked(database, StageDifficulty.Hell))
                detail += $"\n<color={Difficulties.Hex(StageDifficulty.Hell)}>극악 난이도 개방!</color>  <size=80%>스테이지 화면 위쪽 난이도 버튼</size>";
            if (hud != null)
            {
                hud.SetResultDetail(detail);
                var next = NextStageOf(stage);
                if (next != null)
                    hud.SetResultNext($"다음 스테이지로  ·  {next.Code}", () => LaunchNext(next), autoNextSeconds);
            }
            OnStageFinished?.Invoke(result);
        }

        private string BuildRewardText(StageResult r, bool allAlive, bool fast)
        {
            var s = r.stage;
            var sb = new StringBuilder();
            sb.Append("<size=110%>").Append(s.Code).Append(' ').Append(s.stageName).Append("</size>   ");
            sb.Append(Stars(r.stars)).Append('\n');

            sb.Append("<size=80%>");
            sb.Append(Check(true)).Append(" 클리어   ");
            sb.Append(Check(allAlive)).Append(" 아군 전원 생존   ");
            sb.Append(Check(fast)).Append($" 아군 행동 {s.turnLimit}회 이내 ({r.allyActions}회)");
            sb.Append("</size>\n");

            sb.Append("<color=#FFD36B>획득</color>  ");
            var parts = new List<string>();
            if (r.crystals > 0) parts.Add($"<color=#8FE9FF>크리스탈 {r.crystals}</color> <size=75%>(첫 클리어)</size>");
            if (r.gold > 0) parts.Add($"<color=#FFD36B>금화 {r.gold:N0}</color>");
            if (s.rewards != null)
                foreach (var m in s.rewards)
                    if (m != null && m.count > 0) parts.Add($"{Materials.RichName(m.type)} ×{m.count}");
            if (s.characterExp > 0) parts.Add($"캐릭터 경험치 +{s.characterExp}");
            sb.Append(parts.Count > 0 ? string.Join("  ·  ", parts) : "없음");
            if (r.equipment != null && r.equipment.Count > 0)
            {
                var eq = new List<string>();
                foreach (var it in r.equipment)
                    eq.Add($"{EquipStats.Stars(it.Rarity)} {EquipSets.Rich(it.set)} {EquipStats.SlotName(it.slot)} <size=80%>({EquipStats.Name(it.main)})</size>");
                sb.Append("\n<color=#7FE3D6>장비</color>  ").Append(string.Join("  ·  ", eq));
            }
            return sb.ToString();
        }

        /// <summary>★★☆ 형태(획득 = 금색, 미획득 = 회색).</summary>
        public static string Stars(int stars)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 3; i++)
                sb.Append(i < stars ? "<color=#FFD36B>★</color>" : "<color=#4A4F5C>★</color>");
            return sb.ToString();
        }

        /// <summary>달성 ● / 미달성 ○ (게임 폰트에 ✔가 없어 도형 사용).</summary>
        public static string Check(bool ok) => ok ? "<color=#6EE07A>●</color>" : "<color=#6B7280>○</color>";
    }
}
