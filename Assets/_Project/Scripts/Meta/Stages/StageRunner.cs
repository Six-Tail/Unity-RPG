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
        public int multiplier;      // 보상 배수(1~3)
        public int stamina;         // 쓴 행동력
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

        // 행동력 · 보상 배수 · 반복 입장
        private int _paidStamina;       // 이번 전투에 낸 행동력(패배·이탈 시 반환)
        private int _multiplier = 1;    // 이번 전투 보상 배수
        private int _repeatLeft;        // 남은 반복 횟수(-1 = 행동력 소진까지)
        private int _repeatRun;         // 반복 중 몇 번째 입장인지(1부터)

        /// <summary>마지막 출격 실패 이유(행동력 부족 등). 성공하면 null.</summary>
        public string LastError { get; private set; }

        /// <summary>반복 입장 중 승리 후 다음 입장까지 대기(초).</summary>
        [SerializeField] private float repeatDelaySeconds = 3f;

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

        /// <summary>
        /// 스테이지 출격(편성 화면에서). 행동력을 쓰고, ★3 던전이면 고른 보상 배수 · 반복 입장 설정을 적용.
        /// 행동력이 부족하면 false(LastError에 이유).
        /// </summary>
        public bool Launch(StageData stage, IList<CharacterData> party)
        {
            _repeatLeft = StageEntry.RepeatFor(stage, progress);
            _repeatRun = 1;

            // 처음 출격하는 스테이지에 스토리가 있으면: 스토리 → 전투
            var before = StoryLibrary.Before(stage);
            if (before != null && !StoryLibrary.Seen(before) && stage != null && party != null && party.Count > 0)
            {
                int cost = StageEntry.CostOf(stage, progress);
                if (wallet != null && wallet.Stamina < cost)
                {
                    LastError = $"행동력이 부족합니다 (보유 {wallet.Stamina} / 필요 {cost})";
                    return false;
                }
                var team = new List<CharacterData>(party);
                Current = stage;   // 스토리 중에도 '스테이지 전투 중'으로 취급(나가면 스테이지 화면으로)
                _party.Clear();
                _party.AddRange(team);
                Story.Play(before, () =>
                {
                    StoryLibrary.MarkSeen(before);
                    if (Current != stage) return;   // 스토리 도중 다른 화면으로 나감
                    if (!LaunchInternal(stage, team) && hud != null) hud.ShowResultToast(LastError);
                });
                return true;
            }
            return LaunchInternal(stage, party);
        }

        // ---------------- 스토리 ----------------

        private SubcultureRPG.Meta.UI.StoryScreen _story;

        /// <summary>스토리 화면(씬에 없으면 만들어 씀).</summary>
        private SubcultureRPG.Meta.UI.StoryScreen Story
        {
            get
            {
                if (_story == null) _story = SubcultureRPG.Meta.UI.StoryScreen.Ensure(hud != null ? hud.Font : null);
                return _story;
            }
        }

        /// <summary>
        /// 이 스테이지에 다시 볼 수 있는 스토리가 있는가.
        /// 한 번 본 스토리, 또는 스토리가 추가되기 전에 이미 클리어한 스테이지(cleared)의 스토리.
        /// </summary>
        public static bool HasReplay(StageData stage, bool cleared = false)
        {
            var before = StoryLibrary.Before(stage);
            var after = StoryLibrary.After(stage);
            return StoryLibrary.Seen(before) || StoryLibrary.Seen(after) || (cleared && (before != null || after != null));
        }

        /// <summary>스토리 다시 보기(전 → 후 순서). 이미 클리어한 스테이지면 아직 안 본 편도 볼 수 있고, 본 것으로 기록.</summary>
        public void ReplayStory(StageData stage)
        {
            var before = StoryLibrary.Before(stage);
            var after = StoryLibrary.After(stage);
            bool cleared = progress != null && progress.IsCleared(stage);
            bool b = before != null && (cleared || StoryLibrary.Seen(before));
            bool a = after != null && (cleared || StoryLibrary.Seen(after));
            void PlayAfter() { if (a) Story.Play(after, () => StoryLibrary.MarkSeen(after)); }
            if (b) Story.Play(before, () => { StoryLibrary.MarkSeen(before); PlayAfter(); });
            else PlayAfter();
        }

        [ContextMenu("스토리 기록 초기화(다시 자동 재생)")]
        private void ResetStories() => StoryLibrary.ResetSeen();

        /// <summary>같은 스테이지·파티로 다시(결과 화면 '다시'). 반복 설정은 처음부터.</summary>
        private string RetryCurrent()
        {
            if (Current == null || _party.Count == 0) return "다시 시작할 스테이지가 없습니다";
            return Launch(Current, new List<CharacterData>(_party)) ? null : LastError;
        }

        /// <summary>반복 입장: 같은 스테이지·파티로 다음 회차.</summary>
        private void LaunchRepeat()
        {
            if (Current == null || _party.Count == 0) return;
            _repeatRun++;
            if (!LaunchInternal(Current, new List<CharacterData>(_party)) && hud != null) hud.ShowResultToast(LastError);
        }

        private bool LaunchInternal(StageData stage, IList<CharacterData> party)
        {
            LastError = null;
            if (stage == null || launcher == null || party == null || party.Count == 0) { LastError = "출격할 수 없습니다"; return false; }

            // 지난 전투가 끝나지 않은 채 다시 출격하면 그 행동력은 돌려줌
            RefundStamina();

            int mult = StageEntry.MultiplierFor(stage, progress);
            int cost = StageEntry.BaseCost(stage) * mult;
            if (wallet != null && !wallet.SpendStamina(cost))
            {
                LastError = $"행동력이 부족합니다 (보유 {wallet.Stamina} / 필요 {cost})";
                return false;
            }
            _paidStamina = wallet != null ? cost : 0;
            _multiplier = mult;

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
            if (enemies.Count == 0)
            {
                Debug.LogWarning($"[StageRunner] {stage.Code} 적 구성이 비어 있습니다.");
                RefundStamina();
                LastError = "적 구성이 비어 있습니다";
                return false;
            }

            Current = stage;
            var copy = new List<CharacterData>(party);   // party가 _party 자신일 수 있음
            _party.Clear();
            _party.AddRange(copy);
            if (hud != null)
            {
                hud.SetBackground(BattleBackgrounds(stage));
                hud.SetRetryHandler(RetryCurrent);
            }
            // 반복 입장 중에는 자동 전투로
            if (_repeatLeft != 0 && controller != null) controller.AutoBattle = true;
            launcher.Launch(copy, enemies);
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
            if (!Launch(next, new List<CharacterData>(_party)) && hud != null) hud.ShowResultToast(LastError);
        }

        /// <summary>낸 행동력 반환(패배·이탈).</summary>
        private void RefundStamina()
        {
            if (_paidStamina > 0 && wallet != null) wallet.AddStamina(_paidStamina);
            _paidStamina = 0;
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
            RefundStamina();   // 끝나지 않은 전투에서 나가면 행동력 반환
            Current = null;
            _party.Clear();
            _repeatLeft = 0;
            if (hud != null) hud.SetRetryHandler(null);
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

            var result = new StageResult
            {
                stage = stage, win = winner == Team.Ally, allyActions = _allyActions,
                multiplier = Mathf.Max(1, _multiplier), stamina = _paidStamina,
            };

            if (!result.win)
            {
                int refund = _paidStamina;
                RefundStamina();
                _repeatLeft = 0;
                if (hud != null)
                    hud.SetResultDetail($"<size=110%>{stage.Code} {stage.stageName}</size>\n" +
                                        "<color=#9AA3B5>보상 없음 — 캐릭터를 육성하거나 파티를 바꿔 다시 도전하세요</color>" +
                                        (refund > 0 ? $"\n<color=#7CFFB2>행동력 {refund} 반환</color>" : ""));
                OnStageFinished?.Invoke(result);
                return;
            }
            _paidStamina = 0;   // 승리 → 행동력 소모 확정
            int m = result.multiplier;

            // 지휘관 경험치: 쓴 행동력 × 10
            int accountExp = result.stamina * PlayerProfile.ExpPerStamina;
            int levelBefore = PlayerProfile.Instance.Level;
            int levelUps = accountExp > 0 ? PlayerProfile.Instance.AddExp(accountExp) : 0;

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
            // 매 클리어 보상 × 배수(첫 클리어 크리스탈은 배수 없음)
            if (stage.gold > 0)
            {
                result.gold = stage.gold * m;
                if (wallet != null) wallet.AddGold(result.gold);
            }
            if (inventory != null && stage.rewards != null)
                foreach (var mr in stage.rewards)
                    if (mr != null && mr.count > 0) inventory.Add(mr.type, mr.count * m);
            if (roster != null && stage.characterExp > 0)
                foreach (var c in _party)
                    if (c != null) roster.AddExp(c, stage.characterExp * m);
            if (equipment != null && stage.equipmentDrops > 0 && stage.equipmentSets != null && stage.equipmentSets.Count > 0)
            {
                result.equipment = new List<EquipmentItem>();
                for (int i = 0; i < stage.equipmentDrops * m; i++)
                {
                    var it = equipment.AddRandom(stage.equipmentSets, stage.equipmentRarityWeights);
                    if (it != null) result.equipment.Add(it);
                }
            }

            string detail = BuildRewardText(result, allAlive, fast);
            if (accountExp > 0)
                detail += $"\n<size=80%><color=#9AD4FF>지휘관 경험치 +{accountExp:N0}</color></size>" +
                          (levelUps > 0 ? $"  <color=#FFD36B>지휘관 Lv.{levelBefore} → Lv.{PlayerProfile.Instance.Level}!</color>" : "");
            if (MaxSpeedIndex > speedBefore)
                detail += $"\n<color=#FFB347>{MaxSpeedIndex + 1}배속 해금!</color>  <size=80%>전투 화면 오른쪽 위 '배속' 버튼을 눌러 조절</size>";
            if (hardWasLocked && progress.IsDifficultyUnlocked(database, StageDifficulty.Hard))
                detail += $"\n<color={Difficulties.Hex(StageDifficulty.Hard)}>하드 난이도 개방!</color>  <size=80%>스테이지 화면 위쪽 난이도 버튼</size>";
            if (hellWasLocked && progress.IsDifficultyUnlocked(database, StageDifficulty.Hell))
                detail += $"\n<color={Difficulties.Hex(StageDifficulty.Hell)}>극악 난이도 개방!</color>  <size=80%>스테이지 화면 위쪽 난이도 버튼</size>";
            // 처음 본 클리어 후 스토리가 있으면 결과 화면 위에서 먼저 재생(자동 진행은 그 뒤에)
            var afterStory = StoryLibrary.After(stage);
            bool playAfter = afterStory != null && !StoryLibrary.Seen(afterStory);
            if (playAfter) _repeatLeft = 0;

            if (hud != null)
            {
                var next = NextStageOf(stage);
                if (_repeatLeft != 0)
                {
                    // 반복 입장: 횟수가 남았고 행동력이 되면 잠시 뒤 같은 던전으로
                    if (_repeatLeft > 0) _repeatLeft--;
                    int cost = StageEntry.BaseCost(stage) * StageEntry.MultiplierFor(stage, progress);
                    bool afford = wallet == null || wallet.Stamina >= cost;
                    string total = _repeatLeft < 0 ? "최대" : $"{_repeatRun + _repeatLeft}";
                    if (afford && (_repeatLeft != 0))
                    {
                        detail += $"\n<color=#7FE3D6>반복 입장 {_repeatRun} / {total}회 완료</color>";
                        string left = _repeatLeft < 0 ? "행동력 소진까지" : $"남은 {_repeatLeft}회";
                        hud.SetResultNext($"반복 입장  ·  {left}", LaunchRepeat, repeatDelaySeconds);
                    }
                    else
                    {
                        detail += afford
                            ? $"\n<color=#7FE3D6>반복 입장 {_repeatRun}회 완료</color>"
                            : $"\n<color=#FF8A8A>행동력이 부족해 반복 입장을 멈췄습니다</color>  <size=80%>({_repeatRun}회 완료 · 필요 {cost})</size>";
                        _repeatLeft = 0;
                    }
                }
                else if (next != null)
                {
                    int cost = StageEntry.CostOf(next, progress);
                    if (wallet == null || wallet.Stamina >= cost)
                    {
                        if (!playAfter) hud.SetResultNext($"다음 스테이지로  ·  {next.Code}", () => LaunchNext(next), autoNextSeconds);
                    }
                    else
                        detail += $"\n<color=#FF8A8A>행동력 부족</color>  <size=80%>다음 스테이지 {next.Code} 필요 {cost} · 보유 {wallet.Stamina}</size>";
                }
                hud.SetResultDetail(detail);

                if (playAfter)
                {
                    Story.Play(afterStory, () =>
                    {
                        StoryLibrary.MarkSeen(afterStory);
                        // 스토리가 끝난 뒤에는 자동으로 넘기지 않고 버튼만 보여 줌
                        if (Current == stage && next != null && (wallet == null || wallet.Stamina >= StageEntry.CostOf(next, progress)))
                            hud.SetResultNext($"다음 스테이지로  ·  {next.Code}", () => LaunchNext(next), 0f);
                    });
                }
            }
            else if (playAfter) StoryLibrary.MarkSeen(afterStory);
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
            if (r.stamina > 0) sb.Append($"   <color=#7CFFB2>행동력 -{r.stamina}</color>");
            sb.Append("</size>\n");

            int mul = Mathf.Max(1, r.multiplier);
            sb.Append("<color=#FFD36B>획득</color>");
            if (mul > 1) sb.Append($" <color=#FFB347>(보상 ×{mul})</color>");
            sb.Append("  ");
            var parts = new List<string>();
            if (r.crystals > 0) parts.Add($"<color=#8FE9FF>크리스탈 {r.crystals}</color> <size=75%>(첫 클리어)</size>");
            if (r.gold > 0) parts.Add($"<color=#FFD36B>금화 {r.gold:N0}</color>");
            if (s.rewards != null)
                foreach (var m in s.rewards)
                    if (m != null && m.count > 0) parts.Add($"{Materials.RichName(m.type)} ×{m.count * mul}");
            if (s.characterExp > 0) parts.Add($"캐릭터 경험치 +{s.characterExp * mul}");
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
