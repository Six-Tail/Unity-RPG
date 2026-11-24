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

        /// <summary>자동 전투(아군 턴도 AI). 입력 대기 중에 켜면 그 턴부터 바로 자동 진행.</summary>
        public bool AutoBattle
        {
            get => autoBattle;
            set { if (autoBattle == value) return; autoBattle = value; OnAutoBattleChanged?.Invoke(value); }
        }

        /// <summary>진행 배속(턴 간 대기 시간에 적용). 1 = 기본.</summary>
        public float Speed => _speed;
        public void SetSpeed(float speed) => _speed = Mathf.Clamp(speed, 0.25f, 4f);
        private float _speed = 1f;

        public event Action<bool> OnAutoBattleChanged;
        public bool WaitingForInput { get; private set; }
        public BattleUnit CurrentActor { get; private set; }

        public event Action<BattleEngine> OnBattleStart;
        public event Action<BattleUnit> OnTurnBegin;          // 이 유닛의 차례 시작
        public event Action<BattleUnit> OnPlayerInputNeeded;  // 플레이어 입력 대기
        public event Action<ActionResult> OnActionResolved;   // 행동 처리됨
        /// <summary>행동 직전(행동자, 스킬, 지정 대상). 연출이 필요하면 구독자가 PresentationDelay(실시간 초)를 설정.</summary>
        public event Action<BattleUnit, BattleSkill, BattleUnit> OnActionStarting;

        /// <summary>OnActionStarting 구독자가 요청한 연출 시간(실시간 초, 배속 반영은 요청 쪽에서). 한 번 쓰고 0으로 돌아감.</summary>
        public float PresentationDelay { get; set; }
        public event Action<BattleUnit, int> OnStatusTick;    // 턴 시작 도트 피해(유닛, 피해량)
        public event Action<Team> OnBattleEnd;                // 승리 팀

        /// <summary>
        /// 승패가 정해진 직후, 결과(OnBattleEnd) 전. 마지막 타격 연출이 끝까지 보이도록
        /// 구독자가 FinishDelay(실시간 초)를 설정하면 그만큼 기다린 뒤 OnBattleEnd를 보냄.
        /// </summary>
        public event Action<Team> OnBattleFinishing;
        public float FinishDelay { get; set; }

        /// <summary>OnActionResolved 구독자가 요청한 행동 후 대기(실시간 초). 다단 히트 연출이 끝날 때까지 다음 턴을 미룸.</summary>
        public float PostActionDelay { get; set; }

        /// <summary>행동 밖에서 생긴 부가 결과(턴 시작 재생·패시브 회복 등).</summary>
        public event Action<List<ActionResult.TargetHit>> OnSideEffects;
        /// <summary>기절 등으로 턴을 넘김.</summary>
        public event Action<BattleUnit> OnTurnSkipped;

        private Coroutine _loop;
        private bool _inputSubmitted;
        private BattleSkill _inputSkill;
        private BattleUnit _inputTarget;

        private void Start()
        {
            if (startOnPlay) Begin();
        }

        /// <summary>전투 시작(진행 중이면 중단 후 새로).</summary>
        /// <summary>
        /// 승패와 관계없이 전투를 끝냄(예: 주간 보스 행동 제한). 지금 행동이 끝난 뒤 winner로 종료 처리.
        /// </summary>
        public void RequestEnd(Team winner) => _forcedWinner = winner;

        /// <summary>전투가 진행 중인지(시작 ~ 승패 결정 전).</summary>
        public bool IsRunning => _loop != null && Engine != null && !Engine.IsOver && !_forcedWinner.HasValue;
        private Team? _forcedWinner;

        public void Begin()
        {
            _forcedWinner = null;
            Stop();
            if (config == null) config = BattleConfig.CreateDefault();
            Engine = new BattleEngine(allyTeam, enemyTeam, config);
            OnBattleStart?.Invoke(Engine);
            Engine.StartBattle();    // 전투 시작 패시브(HUD가 엔진 이벤트를 구독한 뒤)
            FlushSideEffects();
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
            while (!Engine.IsOver && !_forcedWinner.HasValue)
            {
                var actor = Engine.NextActor();
                if (actor == null) break;

                CurrentActor = actor;

                // 턴 시작: 브레이크 회복 + 도트/효과 틱
                int dot = Engine.StartTurn(actor);
                if (dot > 0) OnStatusTick?.Invoke(actor, dot);
                FlushSideEffects();
                if (Engine.IsOver) break;
                if (!actor.IsAlive) { yield return Wait(betweenTurnDelay); continue; }

                if (actor.skipTurn)
                {
                    // 기절: 행동 없이 턴 넘김
                    OnTurnSkipped?.Invoke(actor);
                    yield return Wait(betweenTurnDelay + 0.35f);
                    continue;
                }

                OnTurnBegin?.Invoke(actor);

                if (actor.team == Team.Ally && !autoBattle)
                {
                    // 플레이어 입력 대기
                    WaitingForInput = true;
                    _inputSubmitted = false;
                    OnPlayerInputNeeded?.Invoke(actor);
                    yield return new WaitUntil(() => _inputSubmitted || autoBattle || _forcedWinner.HasValue);
                    WaitingForInput = false;
                    if (_forcedWinner.HasValue) break;   // 입력 대기 중 강제 종료(시간 제한 등)

                    // 입력 대기 중 자동 전투를 켰으면 AI가 대신 선택
                    var (skill, target) = _inputSubmitted ? (_inputSkill, _inputTarget) : Engine.PickAutoAction(actor);
                    yield return Present(actor, skill, target);
                    var result = Engine.ResolveAction(actor, skill, target);
                    yield return Resolved(result);
                    yield return Recasts(actor);
                }
                else
                {
                    // 적 → 적 AI / 아군 자동 전투 → 아군 AI
                    yield return Wait(enemyTurnDelay);
                    var (skill, target) = actor.team == Team.Ally ? Engine.PickAutoAction(actor) : Engine.PickAction(actor);
                    yield return Present(actor, skill, target);
                    var result = Engine.ResolveAction(actor, skill, target);
                    yield return Resolved(result);
                    yield return Recasts(actor);
                }

                if (Engine.IsOver || _forcedWinner.HasValue) break;
                yield return Wait(betweenTurnDelay);
            }

            CurrentActor = null;
            WaitingForInput = false;
            var endWinner = _forcedWinner ?? Engine.Winner;
            _forcedWinner = null;
            if (endWinner.HasValue)
            {
                var winner = endWinner.Value;
                FinishDelay = 0f;
                OnBattleFinishing?.Invoke(winner);
                float d = FinishDelay;
                FinishDelay = 0f;
                for (float t = 0f; t < d; t += Time.unscaledDeltaTime) yield return null;
                _loop = null;
                OnBattleEnd?.Invoke(winner);
            }
            _loop = null;
        }

        /// <summary>행동 직전 연출(돌진·필살기 컷인 등)이 끝날 때까지 대기. 실시간 기준.</summary>
        private IEnumerator Present(BattleUnit actor, BattleSkill skill, BattleUnit target)
        {
            PresentationDelay = 0f;
            OnActionStarting?.Invoke(actor, skill ?? actor.BasicSkill, target);
            float d = PresentationDelay;
            PresentationDelay = 0f;
            for (float t = 0f; t < d; t += Time.unscaledDeltaTime) yield return null;
        }

        /// <summary>연쇄 시전(처치 → 스킬 자동 재시전)이 예약돼 있는 동안 이어서 행동.</summary>
        private IEnumerator Recasts(BattleUnit actor)
        {
            while (!Engine.IsOver && Engine.NextRecast(actor, out var skill, out var target))
            {
                yield return Wait(0.2f);
                yield return Present(actor, skill, target);
                var result = Engine.ResolveAction(actor, skill, target);
                yield return Resolved(result);
            }
        }

        private void FlushSideEffects()
        {
            var extras = Engine.DrainExtras();
            if (extras.Count > 0) OnSideEffects?.Invoke(extras);
        }

        /// <summary>행동 결과 알림 + 구독자가 요청한 연출 시간만큼 대기(실시간).</summary>
        private IEnumerator Resolved(ActionResult result)
        {
            PostActionDelay = 0f;
            OnActionResolved?.Invoke(result);
            float d = PostActionDelay;
            PostActionDelay = 0f;
            for (float t = 0f; t < d; t += Time.unscaledDeltaTime) yield return null;
        }

        /// <summary>배속 적용 대기(Time.timeScale 일시정지도 존중).</summary>
        private IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime * _speed;
                yield return null;
            }
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
            AutoBattle = true;
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
