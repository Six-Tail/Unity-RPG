using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SubcultureRPG.Battle
{
    /// <summary>
    /// 씬에 두고 쓰는 전투 진입점. 아군/적 팀으로 BattleEngine을 돌리고,
    /// 이벤트로 UI에 알리며 플레이어 입력을 받습니다.
    /// 플레이어 턴에는 SubmitAction 호출을 기다리고, 적/자동 턴은 스스로 진행합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Battle/Battle Controller")]
    public class BattleController : MonoBehaviour
    {
        [Header("팀")]
        [SerializeField] private List<BattleUnitData> allyTeam = new();
        [SerializeField] private List<BattleUnitData> enemyTeam = new();
        [SerializeField, Tooltip("비우면 기본값 사용.")] private BattleConfig config;

        [Header("진행")]
        [SerializeField, Tooltip("켜면 플레이어 턴도 AI가 자동 진행(자동 전투/스킵).")]
        private bool autoBattle = false;
        [SerializeField] private bool startOnPlay = true;
        [SerializeField] private float enemyTurnDelay = 0.6f;
        [SerializeField] private float betweenTurnDelay = 0.35f;

        public BattleEngine Engine { get; private set; }
        public bool WaitingForInput { get; private set; }
        public BattleUnit CurrentActor { get; private set; }

        public event Action<BattleEngine> OnBattleStart;
        public event Action<BattleUnit> OnTurnBegin;          // 이 유닛의 차례 시작
        public event Action<BattleUnit> OnPlayerInputNeeded;  // 플레이어 입력 대기
        public event Action<ActionResult> OnActionResolved;   // 행동 처리됨
        public event Action<BattleUnit, int> OnStatusTick;    // 턴 시작 도트 피해(유닛, 피해량)
        public event Action<Team> OnBattleEnd;                // 승리 팀

        private Coroutine _loop;
        private bool _inputSubmitted;
        private BattleSkill _inputSkill;
        private BattleUnit _inputTarget;

        private void Start()
        {
            if (startOnPlay) Begin();
        }

        /// <summary>전투 시작(진행 중이면 중단 후 새로).</summary>
        public void Begin()
        {
            Stop();
            if (config == null) config = BattleConfig.CreateDefault();
            Engine = new BattleEngine(allyTeam, enemyTeam, config);
            OnBattleStart?.Invoke(Engine);
            _loop = StartCoroutine(RunLoop());
        }

        /// <summary>팀을 지정하고(기본값: 즉시 시작) 전투 시작. 가챠↔전투 연동 진입점.</summary>
        public void SetTeams(List<BattleUnitData> allies, List<BattleUnitData> enemies, bool begin = true)
        {
            allyTeam = allies != null ? new List<BattleUnitData>(allies) : new List<BattleUnitData>();
            enemyTeam = enemies != null ? new List<BattleUnitData>(enemies) : new List<BattleUnitData>();
            if (begin) Begin();
        }

        public void Stop()
        {
            if (_loop != null) { StopCoroutine(_loop); _loop = null; }
            WaitingForInput = false;
            CurrentActor = null;
        }

        private IEnumerator RunLoop()
        {
            while (!Engine.IsOver)
            {
                var actor = Engine.NextActor();
                if (actor == null) break;

                CurrentActor = actor;

                // 턴 시작: 브레이크 회복 + 도트/효과 틱
                int dot = Engine.StartTurn(actor);
                if (dot > 0) OnStatusTick?.Invoke(actor, dot);
                if (Engine.IsOver) break;
                if (!actor.IsAlive) { yield return new WaitForSeconds(betweenTurnDelay); continue; }

                OnTurnBegin?.Invoke(actor);

                if (actor.team == Team.Ally && !autoBattle)
                {
                    // 플레이어 입력 대기
                    WaitingForInput = true;
                    _inputSubmitted = false;
                    OnPlayerInputNeeded?.Invoke(actor);
                    yield return new WaitUntil(() => _inputSubmitted);
                    WaitingForInput = false;

                    var result = Engine.ResolveAction(actor, _inputSkill, _inputTarget);
                    OnActionResolved?.Invoke(result);
                }
                else
                {
                    // 적 또는 자동 전투 → AI
                    yield return new WaitForSeconds(enemyTurnDelay);
                    var (skill, target) = Engine.PickAction(actor);
                    var result = Engine.ResolveAction(actor, skill, target);
                    OnActionResolved?.Invoke(result);
                }

                if (Engine.IsOver) break;
                yield return new WaitForSeconds(betweenTurnDelay);
            }

            CurrentActor = null;
            WaitingForInput = false;
            _loop = null;
            if (Engine.Winner.HasValue) OnBattleEnd?.Invoke(Engine.Winner.Value);
        }

        /// <summary>플레이어 행동 제출. 입력 대기 중일 때만 유효(true=접수됨).</summary>
        public bool SubmitAction(BattleSkill skill, BattleUnit target)
        {
            if (!WaitingForInput || CurrentActor == null) return false;
            _inputSkill = skill != null ? skill : CurrentActor.BasicSkill;
            _inputTarget = target;
            _inputSubmitted = true;
            return true;
        }

        /// <summary>기본 공격 제출(가장 체력 낮은 적 대상).</summary>
        public bool SubmitBasicAttack()
        {
            if (!WaitingForInput || CurrentActor == null) return false;
            var target = BattleEngine.LowestHP(Engine.Opponents(CurrentActor));
            return SubmitAction(CurrentActor.BasicSkill, target);
        }

        // ---------------- 디버그 ----------------

        [ContextMenu("자동 전투 테스트(로그)")]
        private void DebugAutoBattle()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[BattleController] 플레이 모드에서 실행하세요.");
                return;
            }
            autoBattle = true;
            OnActionResolved -= LogAction; OnActionResolved += LogAction;
            OnBattleEnd -= LogEnd; OnBattleEnd += LogEnd;
            Begin();
        }

        private void LogAction(ActionResult r)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(r.actor.Name).Append(" → ").Append(r.skill.skillName);
            foreach (var h in r.hits)
            {
                sb.Append("  |  ").Append(h.target.Name).Append(" -").Append(h.totalDamage);
                if (h.anyCrit) sb.Append("(치명)");
                if (h.defeated) sb.Append(" 격파");
                sb.Append($" [HP {h.target.currentHP}/{h.target.maxHP}]");
            }
            Debug.Log(sb.ToString());
        }

        private void LogEnd(Team winner) => Debug.Log($"[전투 종료] 승리: {winner}");
    }
}
