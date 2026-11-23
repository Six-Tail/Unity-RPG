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
        }

        private void OnEnable()
        {
            if (controller == null) return;
            controller.OnBattleStart += HandleStart;
            controller.OnActionResolved += HandleAction;
            controller.OnBattleEnd += HandleEnd;
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.OnBattleStart -= HandleStart;
            controller.OnActionResolved -= HandleAction;
            controller.OnBattleEnd -= HandleEnd;
        }

        /// <summary>스테이지 출격.</summary>
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
                    enemies.Add(u);
                    _spawned.Add(u);
                }
            if (enemies.Count == 0) { Debug.LogWarning($"[StageRunner] {stage.Code} 적 구성이 비어 있습니다."); return false; }

            Current = stage;
            _party.Clear();
            _party.AddRange(party);
            launcher.Launch(party, enemies);
            return true;
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
            result.firstClear = progress != null && progress.RecordClear(stage, result.stars);
            if (result.firstClear && stage.firstClearCrystals > 0)
            {
                result.crystals = stage.firstClearCrystals;
                if (wallet != null) wallet.Add(stage.firstClearCrystals);
            }
            if (inventory != null && stage.rewards != null)
                foreach (var m in stage.rewards)
                    if (m != null && m.count > 0) inventory.Add(m.type, m.count);
            if (roster != null && stage.characterExp > 0)
                foreach (var c in _party)
                    if (c != null) roster.AddExp(c, stage.characterExp);

            if (hud != null) hud.SetResultDetail(BuildRewardText(result, allAlive, fast));
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
            if (s.rewards != null)
                foreach (var m in s.rewards)
                    if (m != null && m.count > 0) parts.Add($"{Materials.Name(m.type)} ×{m.count}");
            if (s.characterExp > 0) parts.Add($"캐릭터 경험치 +{s.characterExp}");
            sb.Append(parts.Count > 0 ? string.Join("  ·  ", parts) : "없음");
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
