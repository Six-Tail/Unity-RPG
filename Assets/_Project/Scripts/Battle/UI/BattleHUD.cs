using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Battle.UI
{
    /// <summary>
    /// 전투 화면 HUD. BattleController의 이벤트에 붙어 유닛 패널(HP/에너지)·턴 순서 바·
    /// 스킬/대상 입력·피해 숫자 연출·승패 화면을 코드로 구성합니다.
    /// 사용: 빈 GameObject에 추가 → controller 연결(비우면 자동 탐색).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Battle/Battle HUD")]
    public class BattleHUD : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, Tooltip("비우면 씬에서 자동으로 찾습니다.")] private BattleController controller;
        [SerializeField, Tooltip("한글 표시용 TMP 폰트(비우면 기본). CJK 권장.")] private TMP_FontAsset font;

        [Header("표시")]
        [SerializeField, Tooltip("배경 어둡게(독립 테스트용). 실제 전투 배경이 있으면 끄세요.")] private bool drawBackground = true;
        [SerializeField, Tooltip("턴 순서 바에 표시할 개수.")] private int turnPreviewCount = 8;

        private Canvas _canvas;
        private BackgroundArt _bgArt;

        /// <summary>전투 배경 그림 지정(Resources/Art/ 기준 경로 후보, 앞에서부터 처음 찾은 것). 없으면 단색 배경.</summary>
        public void SetBackground(params string[] paths)
        {
            if (_bgArt != null) _bgArt.Set(paths);
        }
        private RectTransform _stage, _enemyRow, _allyRow, _turnStrip, _skillBar;
        private RectTransform _popupLayer;   // 피해 숫자(이펙트 위)
        private SkillFx _fx;                 // 스킬 이펙트(유닛 줄 위)
        private Coroutine _shakeAnim;

        // 필살기 컷인
        private RectTransform _cut, _cutBand, _cutArtRT, _cutLines;
        private CanvasGroup _cutGroup;
        private Image _cutDim, _cutArt, _cutAccent;
        private TextMeshProUGUI _cutLabel, _cutName, _cutUser;
        private Image _resultGlow;

        // 전투 마무리(마지막 타격 → 결과 사이) + 전투 기록 창
        private TextMeshProUGUI _finishText;
        private Image _finishFlash, _finishGlow;
        private CanvasGroup _resultGroup;
        private BattleStatsPanel _stats;
        private RectTransform _statsBtnRt;
        private Coroutine _finishRoutine;

        // 상태 효과 설명 창(아이콘 클릭)
        private RectTransform _tip;
        private TextMeshProUGUI _tipText;
        private UnitPanel _tipPanel;
        private StatusType _tipType;
        private bool _tipIsBreak;
        private GameObject _skillBarGO, _resultGO;
        private TextMeshProUGUI _instruction, _resultText, _resultDetail, _spText;

        private readonly Dictionary<BattleUnit, UnitPanel> _panels = new();
        private BattleUnit _inputActor;
        private BattleSkill _pendingSkill;
        private bool _selecting;
        private BattleEngine _builtEngine;

        // 허브(내비게이터)용 나가기
        private Button _exitBtn, _resultExitBtn, _resultNextBtn;
        private RectTransform _retryRt;
        private TextMeshProUGUI _resultNextLbl;
        private System.Action _onExit, _onNext;
        private string _nextLabel;
        private Coroutine _nextRoutine;

        // 자동 전투 · 배속 (설정은 PlayerPrefs에 저장 → 다음 전투에도 유지)
        private const string AutoKey = "battle.auto", SpeedKey = "battle.speed";
        private static readonly float[] Speeds = { 1f, 2f, 3f };
        private Button _autoBtn, _speedBtn;
        private Image _autoBg, _speedBg;
        private TextMeshProUGUI _autoLabel, _speedLabel;
        private int _maxSpeedIndex = 2;              // 허브가 진행도에 따라 지정(독립 전투 씬은 전부 해금)
        private string _speedLockHint = "";          // 다음(잠긴) 배속 해금 조건
        private int _speedIndex;
        private Coroutine _hintRoutine;

        private void Awake()
        {
            if (controller == null) controller = FindAnyObjectByType<BattleController>();
            BuildUI();
            LoadBattleSettings();
        }

        private void OnEnable()
        {
            if (controller == null) return;
            controller.OnBattleStart += HandleStart;
            controller.OnTurnBegin += HandleTurnBegin;
            controller.OnPlayerInputNeeded += HandlePlayerInput;
            controller.OnActionResolved += HandleResolved;
            controller.OnActionStarting += HandleActionStarting;
            controller.OnStatusTick += HandleStatusTick;
            controller.OnBattleEnd += HandleEnd;
            controller.OnBattleFinishing += HandleFinishing;
            controller.OnSideEffects += HandleSideEffects;
            controller.OnTurnSkipped += HandleTurnSkipped;
            controller.OnAutoBattleChanged += HandleAutoChanged;
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.OnBattleStart -= HandleStart;
            controller.OnTurnBegin -= HandleTurnBegin;
            controller.OnPlayerInputNeeded -= HandlePlayerInput;
            controller.OnActionResolved -= HandleResolved;
            controller.OnActionStarting -= HandleActionStarting;
            controller.OnStatusTick -= HandleStatusTick;
            controller.OnBattleEnd -= HandleEnd;
            controller.OnBattleFinishing -= HandleFinishing;
            controller.OnSideEffects -= HandleSideEffects;
            controller.OnTurnSkipped -= HandleTurnSkipped;
            controller.OnAutoBattleChanged -= HandleAutoChanged;
        }

        private void Start()
        {
            // 컨트롤러가 먼저 시작해 OnBattleStart를 놓친 경우 대비
            if (controller != null && controller.Engine != null && _builtEngine != controller.Engine)
                HandleStart(controller.Engine);
        }

        /// <summary>표시 on/off(허브 전환용). HUD 컴포넌트 자체는 활성 유지 → 이벤트·코루틴 유지.</summary>
        public void Show(bool on) { if (_canvas != null) _canvas.gameObject.SetActive(on); }

        /// <summary>'나가기/홈으로' 버튼 동작 지정(null이면 숨김).</summary>
        public void SetExitAction(System.Action onExit)
        {
            _onExit = onExit;
            bool v = onExit != null;
            if (_exitBtn != null) _exitBtn.gameObject.SetActive(v);
            if (_resultExitBtn != null) _resultExitBtn.gameObject.SetActive(v);
            LayoutResultButtons();
        }

        /// <summary>
        /// 결과 화면 '다음' 버튼(예: 다음 스테이지로). autoSeconds초 뒤 자동 실행(0이면 수동만).
        /// onNext가 null이면 숨김. 새 전투 시작·다시·나가기 시 자동으로 취소됩니다.
        /// </summary>
        public void SetResultNext(string label, System.Action onNext, float autoSeconds = 0f)
        {
            StopNextCountdown();
            _onNext = onNext;
            _nextLabel = label ?? "";
            bool v = onNext != null;
            if (_resultNextBtn != null)
            {
                _resultNextBtn.gameObject.SetActive(v);
                _resultNextLbl.text = _nextLabel;
            }
            if (v && autoSeconds > 0f) _nextRoutine = StartCoroutine(NextCountdown(autoSeconds));
            LayoutResultButtons();
        }

        private IEnumerator NextCountdown(float seconds)
        {
            float t = seconds;
            while (t > 0f)
            {
                _resultNextLbl.text = $"{_nextLabel}  <size=75%>({Mathf.CeilToInt(t)})</size>";
                yield return null;
                // 결과 화면이 닫혔거나 전투 화면을 벗어나면 취소
                if (_canvas == null || !_canvas.gameObject.activeInHierarchy || !_resultGO.activeSelf) { _nextRoutine = null; yield break; }
                t -= Time.unscaledDeltaTime;
            }
            _nextRoutine = null;
            DoNext();
        }

        private void StopNextCountdown()
        {
            if (_nextRoutine != null) { StopCoroutine(_nextRoutine); _nextRoutine = null; }
            if (_resultNextLbl != null) _resultNextLbl.text = _nextLabel ?? "";
        }

        private void DoNext()
        {
            var action = _onNext;
            SetResultNext(null, null);
            _resultGO.SetActive(false);
            action?.Invoke();
        }

        /// <summary>결과 버튼 가운데 정렬: 다시 · (나가기) · (다음).</summary>
        private void LayoutResultButtons()
        {
            var list = new List<RectTransform>();
            if (_statsBtnRt != null) list.Add(_statsBtnRt);
            if (_retryRt != null) list.Add(_retryRt);
            if (_resultExitBtn != null && _resultExitBtn.gameObject.activeSelf) list.Add((RectTransform)_resultExitBtn.transform);
            if (_resultNextBtn != null && _resultNextBtn.gameObject.activeSelf) list.Add((RectTransform)_resultNextBtn.transform);
            const float gap = 30f;
            float total = -gap;
            foreach (var rt in list) total += rt.sizeDelta.x + gap;
            float x = -total / 2f;
            foreach (var rt in list)
            {
                rt.anchoredPosition = new Vector2(x + rt.sizeDelta.x / 2f, -170);
                x += rt.sizeDelta.x + gap;
            }
        }

        /// <summary>이 HUD가 쓰는 한글 폰트(실행 중에 만드는 다른 화면이 같은 폰트를 쓰도록).</summary>
        public TMP_FontAsset Font => font;

        private string _endFinish, _endTitle;
        private Color _endColor;

        /// <summary>다음 전투 종료 때만 마무리 글자 · 결과 제목을 바꿈(예: 주간 보스 '전투 종료'). 한 번 쓰면 원래대로.</summary>
        public void SetEndTexts(string finish, string title, Color color)
        {
            _endFinish = finish; _endTitle = title; _endColor = color;
        }

        private System.Func<string> _retryHandler;
        private TextMeshProUGUI _resultToast;
        private Coroutine _resultToastRoutine;

        /// <summary>
        /// '다시' 버튼 동작 가로채기(스테이지 모드). 반환값이 null/빈 문자열이면 성공, 아니면 그 문구를 결과 화면에 표시.
        /// null을 넘기면 기본 동작(같은 팀으로 재시작).
        /// </summary>
        public void SetRetryHandler(System.Func<string> handler) => _retryHandler = handler;

        /// <summary>결과 화면 버튼 위에 잠깐 뜨는 알림(행동력 부족 등).</summary>
        public void ShowResultToast(string msg)
        {
            if (_resultToast == null) return;
            if (_resultToastRoutine != null) StopCoroutine(_resultToastRoutine);
            _resultToastRoutine = StartCoroutine(ResultToastRoutine(msg));
        }

        private IEnumerator ResultToastRoutine(string msg)
        {
            _resultToast.text = msg;
            _resultToast.alpha = 1f;
            for (float t = 0; t < 2.2f; t += Time.unscaledDeltaTime)
            {
                _resultToast.alpha = t < 1.7f ? 1f : 1f - (t - 1.7f) / 0.5f;
                yield return null;
            }
            _resultToast.text = "";
            _resultToastRoutine = null;
        }

        /// <summary>결과 화면 '홈으로' 버튼 문구(예: 스테이지로).</summary>
        public void SetExitLabel(string label)
        {
            if (_resultExitBtn == null) return;
            var t = _resultExitBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (t != null) t.text = string.IsNullOrEmpty(label) ? "홈으로" : label;
        }

        /// <summary>결과 화면 부가 정보(별 평가·보상 등, 리치 텍스트). 새 전투 시작 시 자동으로 비워짐.</summary>
        public void SetResultDetail(string richText)
        {
            if (_resultDetail != null) _resultDetail.text = richText ?? "";
        }

        // ---------------- 자동 전투 · 배속 ----------------

        /// <summary>
        /// 해금된 최대 배속 단계(0 = ×1만, 1 = ×2까지, 2 = ×3까지). 허브에서 진행도로 지정.
        /// nextLockHint = 다음 배속 해금 조건(누르다가 잠긴 단계에 닿으면 잠깐 표시).
        /// </summary>
        public void SetSpeedLimit(int maxIndex, string nextLockHint = "")
        {
            _maxSpeedIndex = Mathf.Clamp(maxIndex, 0, Speeds.Length - 1);
            _speedLockHint = nextLockHint ?? "";
            ApplySpeed();
        }

        private int EffectiveSpeedIndex => Mathf.Min(_speedIndex, _maxSpeedIndex);

        private void LoadBattleSettings()
        {
            int savedAuto = 0, savedSpeed = 0;
            try { savedAuto = PlayerPrefs.GetInt(AutoKey, 0); savedSpeed = PlayerPrefs.GetInt(SpeedKey, 0); } catch { }
            _speedIndex = Mathf.Clamp(savedSpeed, 0, Speeds.Length - 1);
            if (controller != null) controller.AutoBattle = savedAuto != 0;
            RefreshAutoButton();
            ApplySpeed();
        }

        private void ToggleAuto()
        {
            if (controller == null) return;
            controller.AutoBattle = !controller.AutoBattle;
            PlayerPrefs.SetInt(AutoKey, controller.AutoBattle ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void CycleSpeed()
        {
            if (_maxSpeedIndex <= 0)
            {
                ShowHint(string.IsNullOrEmpty(_speedLockHint) ? "아직 배속을 쓸 수 없습니다" : _speedLockHint);
                return;
            }
            // ×1 → ×2 → ×3 → ×1 … (해금된 단계까지만, 다음 단계가 잠겨 있으면 해금 조건 안내 후 ×1)
            int next = EffectiveSpeedIndex + 1;
            if (next > _maxSpeedIndex)
            {
                if (_maxSpeedIndex < Speeds.Length - 1 && !string.IsNullOrEmpty(_speedLockHint)) ShowHint(_speedLockHint);
                next = 0;
            }
            _speedIndex = next;
            PlayerPrefs.SetInt(SpeedKey, _speedIndex);
            PlayerPrefs.Save();
            ApplySpeed();
        }

        private void ApplySpeed()
        {
            float speed = Speeds[EffectiveSpeedIndex];
            if (controller != null) controller.SetSpeed(speed);
            if (_speedLabel == null) return;
            if (_maxSpeedIndex > 0)
            {
                bool fast = speed > 1f;
                _speedLabel.text = $"배속 ×{speed:0}";
                _speedLabel.color = fast ? new Color(1f, 0.88f, 0.45f) : Color.white;
                _speedBg.color = speed >= 3f ? new Color(0.85f, 0.3f, 0.2f, 0.92f)
                    : fast ? new Color(0.85f, 0.55f, 0.15f, 0.9f) : new Color(1, 1, 1, 0.14f);
            }
            else
            {
                _speedLabel.text = "배속 ×1 <size=70%><color=#8A90A0>잠김</color></size>";
                _speedLabel.color = new Color(0.7f, 0.72f, 0.78f);
                _speedBg.color = new Color(1, 1, 1, 0.07f);
            }
        }

        private void HandleAutoChanged(bool on)
        {
            RefreshAutoButton();
            if (on && _skillBarGO != null && (_skillBarGO.activeSelf || _selecting || controller.WaitingForInput))
            {
                // 입력 대기 중 켬 → 컨트롤러가 바로 AI 행동으로 진행
                _skillBarGO.SetActive(false);
                _selecting = false; _pendingSkill = null;
                ClearTargetable();
                ClearWeakHints();
                SetInstruction("");
            }
        }

        private void RefreshAutoButton()
        {
            if (_autoLabel == null) return;
            bool on = controller != null && controller.AutoBattle;
            _autoLabel.text = on ? "자동 ON" : "자동 OFF";
            _autoLabel.color = on ? Color.white : new Color(0.8f, 0.82f, 0.88f);
            _autoBg.color = on ? new Color(0.15f, 0.55f, 0.85f, 0.92f) : new Color(1, 1, 1, 0.14f);
        }

        private void ShowHint(string text)
        {
            if (_hintRoutine != null) StopCoroutine(_hintRoutine);
            _hintRoutine = StartCoroutine(HintRoutine(text));
        }

        private IEnumerator HintRoutine(string text)
        {
            string before = _instruction.gameObject.activeSelf ? _instruction.text : "";
            SetInstruction(text);
            yield return new WaitForSecondsRealtime(1.8f);
            if (_instruction.text == text) SetInstruction(controller != null && controller.WaitingForInput ? before : "");
            _hintRoutine = null;
        }

        private void RequestExit()
        {
            SetResultNext(null, null);
            _resultGO.SetActive(false);
            _skillBarGO.SetActive(false);
            _selecting = false; _pendingSkill = null;
            ClearTargetable();
            SetInstruction("");
            _onExit?.Invoke();
        }

        // ---------------- 이벤트 핸들러 ----------------

        private void HandleStart(BattleEngine engine)
        {
            _builtEngine = engine;
            _selecting = false; _pendingSkill = null; _inputActor = null;
            _skillBarGO.SetActive(false);
            _resultGO.SetActive(false);
            SetResultDetail("");
            SetResultNext(null, null);
            SetInstruction("");
            ResetFinish();
            _fx?.Clear();
            if (_resultToast != null) _resultToast.text = "";

            HideTip();
            _turnDamage?.Clear();
            BuildPanels(engine);
            RefreshTurnStrip();
            RefreshSkillPoints();

            // 패시브 발동 알림(엔진은 전투마다 새로 만들어지므로 해제 불필요)
            _battleStartedAt = Time.unscaledTime;
            engine.PassiveTriggered += HandlePassive;
        }

        private float _battleStartedAt;
        private TurnDamageCounter _turnDamage;
        private readonly Dictionary<BattleUnit, float> _passiveNoteAt = new();

        /// <summary>패시브 발동: 유닛 위에 '패시브 이름' 알림. 같은 유닛이 연달아 발동하면 살짝 늦춰 겹치지 않게.</summary>
        private void HandlePassive(BattleUnit unit, PassiveDef p)
        {
            if (p == null || string.IsNullOrEmpty(p.name) || !_panels.TryGetValue(unit, out var panel)) return;
            if (_canvas == null || !_canvas.gameObject.activeInHierarchy) return;
            float now = Time.unscaledTime;
            // 전투 시작 직후에는 패널 배치가 끝난 뒤에 표시
            float at = Mathf.Max(now, _battleStartedAt + 0.45f);
            if (_passiveNoteAt.TryGetValue(unit, out float last) && last + 0.35f > at) at = last + 0.35f;
            _passiveNoteAt[unit] = at;
            string text = $"<size=58%><color=#C9B8FF>패시브</color></size>\n{p.name}";
            StartCoroutine(After(at - now, () =>
            {
                if (panel == null) return;
                SpawnPopup(panel, text, new Color(1f, 0.9f, 0.6f), false, 0f, 24f, new Color(0.7f, 0.5f, 1f), 0f);
                panel.Refresh();
                RefreshSkillPoints();
                RefreshTurnStrip();
            }));
        }

        /// <summary>행동 밖에서 생긴 부가 결과(턴 시작 재생·패시브 회복 등).</summary>
        private void HandleSideEffects(List<ActionResult.TargetHit> extras)
        {
            if (_fx != null) _fx.Speed = Mathf.Pow(Mathf.Max(1f, controller.Speed), 0.6f);
            _turnDamage?.Add(null, extras);
            ShowExtras(extras, 0f);
            foreach (var p in _panels.Values) p.Refresh();
        }

        private void HandleTurnSkipped(BattleUnit unit)
        {
            foreach (var p in _panels.Values) p.Refresh();
            RefreshTip();
            RefreshTurnStrip();
            if (_panels.TryGetValue(unit, out var panel))
                SpawnPopup(panel, "행동 불가", new Color(0.6f, 0.8f, 1f), false, 0f, 30f, new Color(0.3f, 0.5f, 0.9f), 0f);
        }

        /// <summary>
        /// 부가 결과 표시: 추가 공격·반격(베기 이펙트 + 머리말 붙은 피해 숫자) / 흡혈·패시브 회복(초록 숫자).
        /// 끝나는 시점을 반환.
        /// </summary>
        private float ShowExtras(List<ActionResult.TargetHit> hits, float start)
        {
            float t = start;
            if (hits == null) return t;
            foreach (var h in hits)
            {
                if (!h.extra || !_panels.TryGetValue(h.target, out var panel)) continue;
                Vector2 pos = _fx != null ? _fx.ToLocal((RectTransform)panel.transform) : Vector2.zero;
                string head = string.IsNullOrEmpty(h.label) ? "" : $"<size=55%>{h.label}</size>\n";
                float at = t;
                if (h.healed > 0)
                {
                    _fx?.Heal(pos, at);
                    SpawnPopup(panel, $"{head}+{h.healed:N0}", new Color(0.4f, 0.95f, 0.55f), false, at + 0.05f, 28f, new Color(0.3f, 1f, 0.5f));
                    t += 0.12f;
                }
                else if (h.totalDamage > 0)
                {
                    var elem = h.source != null ? h.source.element : Element.Physical;
                    _fx?.Basic(elem, pos, 1f, at);
                    var col = h.anyCrit ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.8f, 0.6f);
                    SpawnPopup(panel, $"{head}{h.totalDamage:N0}", col, h.anyCrit, at + 0.04f, 34f, new Color(1f, 0.6f, 0.3f));
                    var hitPanel = panel;
                    bool crit = h.anyCrit;
                    StartCoroutine(After(at, () => { if (hitPanel != null) hitPanel.PlayHit(crit ? new Color(1f, 0.85f, 0.4f) : Color.white, 8f, 0.25f); }));
                    // 추가 공격한 쪽도 살짝 튀어나옴
                    if (h.source != null && _panels.TryGetValue(h.source, out var src))
                    {
                        var dir = h.source.team == Team.Ally ? new Vector2(0, 26) : new Vector2(0, -26);
                        StartCoroutine(After(Mathf.Max(0f, at - 0.05f), () => { if (src != null) src.PlayLunge(dir, 0.2f); }));
                    }
                    t += 0.3f;
                }
            }
            return t;
        }

        private void HandleTurnBegin(BattleUnit unit)
        {
            _turnDamage?.BeginTurn(unit);
            // 턴 시작에 효과 턴이 줄고 브레이크가 풀리므로 아이콘·설명 창 갱신
            foreach (var p in _panels.Values) p.Refresh();
            RefreshTip();
            foreach (var p in _panels.Values) p.SetCurrent(false);
            ClearWeakHints();
            if (_panels.TryGetValue(unit, out var panel)) panel.SetCurrent(true);
            RefreshTurnStrip();
            RefreshSkillPoints();
        }

        private void HandlePlayerInput(BattleUnit unit)
        {
            _inputActor = unit;
            _selecting = false; _pendingSkill = null;
            ClearTargetable();
            BuildSkillButtons(unit);
            _skillBarGO.SetActive(true);
            SetInstruction($"{unit.Name}의 행동 선택");
            ShowWeakHints(unit);
        }

        /// <summary>행동 직전: 행동자 돌진, 스킬 이름 띠, 아군 필살기 컷인. 필요한 시간만큼 컨트롤러가 기다림.</summary>
        private void HandleActionStarting(BattleUnit actor, BattleSkill skill, BattleUnit target)
        {
            if (_canvas == null || !_canvas.gameObject.activeInHierarchy) return;
            float speed = Mathf.Max(1f, controller.Speed);
            if (_panels.TryGetValue(actor, out var p))
                p.PlayLunge(actor.team == Team.Ally ? new Vector2(0, 34) : new Vector2(0, -34), 0.28f / speed);

            if (skill != null && skill.IsUltimate && actor.team == Team.Ally)
            {
                float dur = 1.4f / Mathf.Sqrt(speed);   // 배속이어도 읽을 수 있게 덜 줄임
                StartCoroutine(UltimateCutIn(actor, skill, dur));
                controller.PresentationDelay = dur + 0.05f;
            }
            else
            {
                bool isSkill = skill != null && skill.category == SkillCategory.Skill;
                if (isSkill && p != null) ShowSkillBanner(p, actor, skill, 0.9f / speed);
                float delay = (isSkill ? 0.45f : 0.2f) / speed;
                if (isSkill && p != null && _fx != null)
                {
                    _fx.Speed = 1f;
                    _fx.Charge(skill.element, _fx.ToLocal((RectTransform)p.transform), 0.8f, delay);
                }
                controller.PresentationDelay = delay;
            }
        }

        private void HandleResolved(ActionResult result)
        {
            _turnDamage?.Add(result.actor, result.hits);
            _skillBarGO.SetActive(false);
            _selecting = false; _pendingSkill = null;
            ClearTargetable();
            ClearWeakHints();
            SetInstruction("");

            bool bigHit = false;
            var skill = result.skill;
            var cat = skill != null ? skill.category : SkillCategory.Basic;
            var elem = skill != null ? skill.element : Element.Physical;
            bool ult = cat == SkillCategory.Ultimate;
            float speed = Mathf.Max(1f, controller != null ? controller.Speed : 1f);
            float sq = Mathf.Sqrt(speed);
            float gap = 0.13f / sq;                                   // 다단 히트 간격
            float fxScale = cat == SkillCategory.Basic ? 1f : cat == SkillCategory.Skill ? 1.15f : 1.3f;
            float end = 0f;                                           // 연출이 끝나는 시점(다음 턴 대기용)
            if (_fx != null) _fx.Speed = Mathf.Pow(speed, 0.6f);

            // 연쇄 시전(처치 → 스킬 자동 재시전)이면 행동자 위에 순번과 피해 배율 표시
            if (result.recast > 0 && result.actor != null && _panels.TryGetValue(result.actor, out var recastPanel))
            {
                string pen = result.recastMul < 0.999f ? $"  <color=#FFB0B0>피해 -{(1f - result.recastMul) * 100f:0}%</color>" : "";
                SpawnPopup(recastPanel, $"<size=70%>연쇄 시전 {result.recast}회{pen}</size>", new Color(1f, 0.9f, 0.6f), false, 0f, 26f, new Color(0.8f, 0.5f, 1f), 0f);
            }

            bool anyDamage = false;
            foreach (var h in result.hits) anyDamage |= !h.extra && h.totalDamage > 0;
            if (ult && anyDamage && _fx != null) _fx.ScreenFlash(Traits.ColorOf(elem), 0.35f / sq, 0.35f);

            int i = 0;
            foreach (var h in result.hits)
            {
                if (h.extra || !_panels.TryGetValue(h.target, out var panel)) continue;
                float delay = i++ * 0.06f / sq;
                Vector2 pos = _fx != null ? _fx.ToLocal((RectTransform)panel.transform) : Vector2.zero;
                float after = delay;   // 이 대상의 타격이 끝나는 시점

                if (h.healed > 0)
                {
                    _fx?.Heal(pos, delay);
                    SpawnPopup(panel, "+" + h.healed.ToString("N0"), new Color(0.4f, 0.95f, 0.55f), false, delay + 0.05f, 0f, new Color(0.3f, 1f, 0.5f));
                    panel.PlayHit(new Color(0.4f, 1f, 0.55f), 0f, 0.4f);
                }
                else if (h.totalDamage > 0)
                {
                    // 종류별 이펙트
                    if (_fx != null)
                    {
                        if (cat == SkillCategory.Basic) _fx.Basic(elem, pos, fxScale, delay);
                        else if (cat == SkillCategory.Skill) _fx.Skill(elem, pos, fxScale, delay);
                        else _fx.Ultimate(elem, pos, fxScale, delay);
                    }

                    // 피해 숫자 스타일: 기본 32 · 스킬 38(속성 빛) · 궁극기 48(금빛)
                    float size = cat == SkillCategory.Basic ? 32f : cat == SkillCategory.Skill ? 38f : 48f;
                    Color? glow = cat == SkillCategory.Basic ? (Color?)null
                        : cat == SkillCategory.Skill ? Traits.ColorOf(elem) : new Color(1f, 0.8f, 0.3f);
                    var col = h.anyCrit ? new Color(1f, 0.85f, 0.35f)
                        : (h.weakness ? new Color(1f, 0.6f, 0.9f) : Color.white);
                    var flash = h.anyCrit ? new Color(1f, 0.85f, 0.4f) : h.weakness ? new Color(1f, 0.55f, 0.85f) : Color.white;
                    float amp = h.anyCrit || ult ? 14f : 8f;

                    int n = h.hitDamages != null ? h.hitDamages.Count : 0;
                    if (n <= 1)
                    {
                        string txt = h.totalDamage.ToString("N0");
                        if (h.anyCrit) txt = $"<size=55%>치명</size>\n{txt}";
                        else if (h.weakness) txt = $"<size=55%>약점</size>\n{txt}";
                        SpawnPopup(panel, txt, col, h.anyCrit || h.weakness || ult, delay + 0.04f, size * (h.anyCrit || h.weakness ? 1.2f : 1f), glow);
                        panel.PlayHit(flash, amp, 0.32f);
                        after = delay + 0.1f;
                    }
                    else
                    {
                        // 다단 히트: 타격마다 작은 숫자 + 추가 타격 이펙트 + 흔들림, 마지막에 총 피해
                        for (int k = 0; k < n; k++)
                        {
                            float dk = delay + k * gap;
                            bool crit = h.hitCrits != null && k < h.hitCrits.Count && h.hitCrits[k];
                            if (k > 0) _fx?.Impact(elem, cat, pos, fxScale * 0.8f, dk);
                            var hc = crit ? new Color(1f, 0.85f, 0.35f) : (h.weakness ? new Color(1f, 0.75f, 0.92f) : new Color(0.92f, 0.94f, 1f));
                            float x = -70f + 140f * (n == 1 ? 0.5f : k / (float)(n - 1)) + Random.Range(-8f, 8f);
                            SpawnPopup(panel, h.hitDamages[k].ToString("N0"), hc, false, dk + 0.03f, size * 0.72f, null, x);
                            var fcol = crit ? new Color(1f, 0.85f, 0.4f) : flash;
                            if (k == 0) panel.PlayHit(fcol, amp * 0.7f, 0.22f);
                            else StartCoroutine(After(dk, () => panel.PlayHit(fcol, amp * 0.7f, 0.22f)));
                        }
                        float dt = delay + n * gap + 0.04f;
                        string head = h.anyCrit ? "치명 · 총 피해" : h.weakness ? "약점 · 총 피해" : "총 피해";
                        SpawnPopup(panel, $"<size=50%>{head}</size>\n{h.totalDamage:N0}", col, true, dt, size * 1.15f, glow ?? new Color(1f, 1f, 1f, 0.6f), 0f);
                        after = dt;
                    }
                    bigHit |= h.anyCrit || ult;
                }
                if (h.broke)
                {
                    float db = after + 0.06f;
                    SpawnPopup(panel, "BREAK!", new Color(1f, 0.75f, 0.25f), true, db, 0f, new Color(1f, 0.7f, 0.25f));
                    StartCoroutine(After(db, () => StartCoroutine(BreakBurst(panel))));
                    bigHit = true;
                    after = db;
                }
                if (h.appliedEffect != StatusType.None) _fx?.Status(h.appliedEffect, pos, after + 0.05f);
                end = Mathf.Max(end, after);
            }
            if (bigHit) ScreenShake(ult ? 14f : 8f, ult ? 0.38f : 0.25f);

            // 부가 결과(추가 공격·반격·흡혈·패시브 회복)는 본 타격이 끝난 뒤에
            bool hasExtra = false;
            foreach (var h in result.hits) hasExtra |= h.extra;
            if (hasExtra) end = ShowExtras(result.hits, end + 0.18f / sq);

            // 다단 히트·추가 공격이 다 보이도록 다음 턴을 잠깐 미룸
            if (controller != null) controller.PostActionDelay = Mathf.Min(1.6f, Mathf.Max(0f, end - 0.1f));

            foreach (var p in _panels.Values) p.Refresh();
            RefreshSkillPoints();
            RefreshTip();
        }

        private void HandleStatusTick(BattleUnit unit, int dot)
        {
            if (dot > 0 && _panels.TryGetValue(unit, out var panel))
            {
                SpawnPopup(panel, dot.ToString(), new Color(0.75f, 0.5f, 1f), false);
                _fx?.Status(StatusType.Dot, _fx.ToLocal((RectTransform)panel.transform), 0f);
                panel.Refresh();
            }
        }

        // ---------------- 상태 효과 설명 창 ----------------

        /// <summary>상태 아이콘 클릭: 같은 아이콘이면 닫고, 아니면 그 효과 설명을 아이콘 옆에 표시.</summary>
        private void OnStatusClicked(UnitPanel panel, StatusEffect effect, RectTransform icon)
        {
            bool isBreak = effect == null;
            bool same = _tip.gameObject.activeSelf && _tipPanel == panel && _tipIsBreak == isBreak && (isBreak || _tipType == effect.type);
            if (same) { HideTip(); return; }

            _tipPanel = panel;
            _tipIsBreak = isBreak;
            if (!isBreak) _tipType = effect.type;
            _tip.gameObject.SetActive(true);
            _tip.SetAsLastSibling();
            if (_resultGO != null) _resultGO.transform.SetAsLastSibling();
            RefreshTip();

            // 아이콘이 화면 위쪽이면 아래에, 아래쪽이면 위에 (화면 밖으로 나가지 않게)
            Vector2 local = _canvas.transform.InverseTransformPoint(icon.TransformPoint(icon.rect.center));
            float h = _tip.sizeDelta.y, w = _tip.sizeDelta.x;
            float y = local.y > 0 ? local.y - 26f - h / 2f : local.y + 26f + h / 2f;
            float halfW = ((RectTransform)_canvas.transform).rect.width / 2f;
            float x = Mathf.Clamp(local.x + w / 2f - 20f, -halfW + w / 2f + 12f, halfW - w / 2f - 12f);
            _tip.anchoredPosition = new Vector2(x, y);
        }

        private void HideTip()
        {
            if (_tip != null) _tip.gameObject.SetActive(false);
            _tipPanel = null;
        }

        /// <summary>열려 있는 설명 창 내용 갱신(효과가 끝났으면 닫음).</summary>
        private void RefreshTip()
        {
            if (_tip == null || !_tip.gameObject.activeSelf) return;
            var unit = _tipPanel != null ? _tipPanel.Unit : null;
            if (unit == null || !unit.IsAlive) { HideTip(); return; }

            if (_tipIsBreak)
            {
                if (!unit.isBroken) { HideTip(); return; }
                _tipText.text = $"<color=#FFB347><b>브레이크</b></color>  <size=80%><color=#9AA3B5>{unit.Name}</color></size>\n" +
                                "받는 피해가 늘어납니다.\n" +
                                "<color=#FFD36B>이 유닛의 다음 턴이 시작되면</color> 풀리고 내구도가 회복됩니다.";
                return;
            }
            var e = _tipPanel.FindEffect(_tipType);
            if (e == null) { HideTip(); return; }
            bool buff = StatusText.IsBuff(e.type);
            string kind = buff ? "<color=#6FE0C0>버프</color>" : "<color=#FF8A8A>디버프</color>";
            if (e.type == StatusType.Stun) kind = "<color=#8FB8FF>행동 불가</color>";
            _tipText.text = $"<b>{StatusText.Name(e.type)}</b>  <size=80%>{kind} · <color=#9AA3B5>{unit.Name}</color></size>\n" +
                            $"{StatusText.Detail(e)}\n" +
                            $"<color=#FFD36B>남은 턴 {e.remaining}</color>  <size=80%><color=#9AA3B5>(이 유닛의 턴이 시작될 때마다 1 감소)</color></size>";
        }

        private void BuildTip(Transform root)
        {
            var bg = UIFactory.AddImage("StatusTip", root, new Color(0.07f, 0.08f, 0.12f, 0.97f), ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false; bg.pixelsPerUnitMultiplier = 2f;
            _tip = bg.rectTransform;
            UIFactory.Anchor(_tip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _tip.sizeDelta = new Vector2(430, 132);
            var outline = bg.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.25f); outline.effectDistance = new Vector2(1.5f, -1.5f);

            _tipText = UIFactory.AddText("Text", _tip, "", 20, font, Color.white, TextAlignmentOptions.TopLeft);
            UIFactory.Stretch(_tipText.rectTransform, 16, 10, 16, 12);
            _tipText.richText = true; _tipText.lineSpacing = 8f;
            _tipText.enableAutoSizing = true; _tipText.fontSizeMin = 13; _tipText.fontSizeMax = 20;

            // 설명 창을 누르면 닫힘
            var btn = bg.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            btn.onClick.AddListener(HideTip);
            _tip.gameObject.SetActive(false);
        }

        /// <summary>
        /// 승패가 갈린 직후(결과 화면 전): 마지막 타격 숫자·흔들림이 다 보이도록 잠깐 두고,
        /// 진 쪽 패널이 가라앉으며 'FINISH!' / '전멸...' 글자가 찍힌 뒤 결과 화면이 서서히 나타남.
        /// </summary>
        private void HandleFinishing(Team winner)
        {
            HideTip();
            _skillBarGO.SetActive(false);
            _selecting = false; _pendingSkill = null;
            ClearTargetable();
            ClearWeakHints();
            SetInstruction("");
            if (_canvas == null || !_canvas.gameObject.activeInHierarchy) return;
            float speed = Mathf.Max(1f, controller.Speed);
            float dur = 1.7f / Mathf.Sqrt(speed);    // 1배속 1.7초 · 3배속 약 1초
            controller.FinishDelay = dur;
            if (_finishRoutine != null) StopCoroutine(_finishRoutine);
            _finishRoutine = StartCoroutine(FinishRoutine(winner == Team.Ally, dur));
        }

        private IEnumerator FinishRoutine(bool win, float dur)
        {
            // 1) 마지막 타격 연출이 끝까지 보이게 잠깐 대기
            float hold = dur * 0.35f;
            for (float t = 0; t < hold; t += Time.unscaledDeltaTime) yield return null;

            // 2) 진 쪽 패널 가라앉기 + 번쩍임 + 마무리 글자
            foreach (var kv in _panels)
                if ((kv.Key.team == Team.Enemy) == win) kv.Value.PlayDefeat(dur * 0.45f);
            if (win) ScreenShake(10f, 0.3f);

            var col = win ? new Color(1f, 0.86f, 0.4f) : new Color(0.85f, 0.35f, 0.35f);
            _finishText.text = win ? "FINISH!" : "전멸...";
            if (!string.IsNullOrEmpty(_endFinish)) { _finishText.text = _endFinish; col = _endColor; }
            _finishText.color = col;
            _finishText.gameObject.SetActive(true);
            _finishFlash.gameObject.SetActive(true);
            _finishGlow.gameObject.SetActive(true);
            var rt = _finishText.rectTransform;
            float show = dur - hold;
            for (float t = 0; t < show; t += Time.unscaledDeltaTime)
            {
                float k = t / show;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(k / 0.25f), 3f);     // 처음에 확 찍힘
                rt.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, e) * (1f + 0.04f * k);
                rt.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(-8f, -3f, e));
                float fade = k > 0.85f ? 1f - (k - 0.85f) / 0.15f : 1f;
                _finishText.alpha = e * fade;
                _finishGlow.color = new Color(col.r, col.g, col.b, 0.5f * e * fade);
                float flash = win ? Mathf.Clamp01(1f - k / 0.3f) * 0.45f : Mathf.Clamp01(k / 0.4f) * 0.35f;
                _finishFlash.color = win ? new Color(1f, 0.95f, 0.8f, flash) : new Color(0.15f, 0f, 0f, flash);
                yield return null;
            }
            _finishText.gameObject.SetActive(false);
            _finishGlow.gameObject.SetActive(false);
            if (win) _finishFlash.gameObject.SetActive(false);   // 패배는 붉은 어둠이 결과 화면 뒤까지 남음
            _finishRoutine = null;
        }

        private void ResetFinish()
        {
            if (_finishRoutine != null) { StopCoroutine(_finishRoutine); _finishRoutine = null; }
            if (_finishText != null) _finishText.gameObject.SetActive(false);
            if (_finishGlow != null) _finishGlow.gameObject.SetActive(false);
            if (_finishFlash != null) _finishFlash.gameObject.SetActive(false);
            _stats?.Hide();
        }

        private void OpenStats()
        {
            if (controller == null || controller.Engine == null) return;
            StopNextCountdown();   // 기록을 보는 동안은 '다음 스테이지' 자동 진행을 멈춤(버튼은 그대로)
            _stats.Show(controller.Engine);
        }

        private void BuildFinish(Transform root)
        {
            _finishFlash = UIFactory.AddImage("FinishFlash", root, new Color(1, 1, 1, 0));
            UIFactory.Stretch(_finishFlash.rectTransform);
            _finishFlash.gameObject.SetActive(false);

            _finishGlow = UIFactory.AddImage("FinishGlow", root, new Color(1, 1, 1, 0), ProceduralSprites.Glow);
            _finishGlow.preserveAspect = false;
            var grt = _finishGlow.rectTransform;
            UIFactory.Anchor(grt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            grt.sizeDelta = new Vector2(1300, 420); grt.anchoredPosition = new Vector2(0, 60);
            _finishGlow.gameObject.SetActive(false);

            _finishText = UIFactory.AddText("FinishText", root, "", 130, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);
            var rt = _finishText.rectTransform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = new Vector2(1200, 220); rt.anchoredPosition = new Vector2(0, 60);
            _finishText.outlineWidth = 0.18f;
            _finishText.outlineColor = new Color32(20, 12, 4, 255);
            _finishText.gameObject.SetActive(false);
        }

        private void HandleEnd(Team winner)
        {
            HideTip();
            _turnDamage?.BeginTurn(null);   // 잠시 뒤 사라짐
            if (_finishRoutine != null) { StopCoroutine(_finishRoutine); _finishRoutine = null; }
            _finishText.gameObject.SetActive(false);
            _finishGlow.gameObject.SetActive(false);
            foreach (var p in _panels.Values) p.SetCurrent(false);
            bool win = winner == Team.Ally;
            _resultText.text = win ? "승리!" : "패배...";
            _resultText.color = win ? new Color(1f, 0.85f, 0.35f) : new Color(0.9f, 0.5f, 0.5f);
            if (!string.IsNullOrEmpty(_endTitle)) { _resultText.text = _endTitle; _resultText.color = _endColor; }
            _endFinish = null; _endTitle = null;
            _resultGO.SetActive(true);
            StartCoroutine(ResultIntro(win));
        }

        // ---------------- 입력 ----------------

        private void OnSkillChosen(BattleSkill skill)
        {
            if (_inputActor == null) return;
            if (!controller.Engine.CanUse(_inputActor, skill)) return;

            // 광역/자기 대상은 즉시 발동
            if (skill.target == TargetType.AllEnemies || skill.target == TargetType.AllAllies || skill.target == TargetType.Self
                || skill.target == TargetType.RandomEnemies)
            {
                Submit(skill, null);
                return;
            }

            // 단일 대상: 아군(힐/버프) 또는 적 선택
            bool ally = skill.TargetsAllies;
            var pool = ally ? AliveAllies() : AliveEnemies();
            if (pool.Count == 0) { Submit(skill, null); return; }
            if (pool.Count == 1) { Submit(skill, pool[0]); return; }

            _pendingSkill = skill;
            _selecting = true;
            SetTargetableUnits(pool);
            SetInstruction(ally ? "아군 대상을 선택하세요" : "대상을 선택하세요");
        }

        private void OnPanelClicked(BattleUnit unit)
        {
            if (!_selecting || _pendingSkill == null || unit == null || !unit.IsAlive) return;
            bool ally = _pendingSkill.TargetsAllies;
            if (ally && unit.team != Team.Ally) return;
            if (!ally && unit.team != Team.Enemy) return;
            Submit(_pendingSkill, unit);
        }

        private void Submit(BattleSkill skill, BattleUnit target)
        {
            _selecting = false; _pendingSkill = null;
            ClearTargetable();
            _skillBarGO.SetActive(false);
            SetInstruction("");
            controller.SubmitAction(skill, target);
        }

        // ---------------- UI 구성 ----------------

        private void BuildUI()
        {
            var cgo = UIFactory.NewRect("BattleCanvas", transform);
            _canvas = cgo.gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 90;
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 어떤 화면비에서도 1920×1080 설계 영역이 전부 보이게(남는 쪽은 여백)
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var rootT = _canvas.transform;

            if (drawBackground)
            {
                var bg = UIFactory.AddImage("Backdrop", rootT, new Color(0.05f, 0.06f, 0.09f, 1f), null, raycast: true);
                UIFactory.Stretch(bg.rectTransform);
                _bgArt = BackgroundArt.Create(bg.transform, 0.45f);
                _bgArt.Set("Backgrounds/battle");
            }

            _turnStrip = MakeRow("TurnStrip", rootT, new Vector2(0.5f, 1), new Vector2(0, -46), 8);
            _stage = UIFactory.NewRect("Stage", rootT);   // 화면 흔들림은 이 안(유닛 줄)만
            UIFactory.Stretch(_stage);
            _enemyRow = MakeRow("EnemyRow", _stage, new Vector2(0.5f, 1), new Vector2(0, -176), 18);
            _allyRow = MakeRow("AllyRow", _stage, new Vector2(0.5f, 0.5f), new Vector2(0, -40), 18);
            _fx = SkillFx.Create(_stage);
            _popupLayer = UIFactory.NewRect("Popups", _stage);
            UIFactory.Stretch(_popupLayer);

            _instruction = UIFactory.AddText("Instruction", rootT, "", 26, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var irt = _instruction.rectTransform;
            UIFactory.Anchor(irt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            irt.sizeDelta = new Vector2(900, 40); irt.anchoredPosition = new Vector2(0, 210);
            _instruction.gameObject.SetActive(false);

            _spText = UIFactory.AddText("SkillPoints", rootT, "", 22, font, new Color(0.55f, 0.85f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
            var sprt = _spText.rectTransform;
            UIFactory.Anchor(sprt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            sprt.sizeDelta = new Vector2(600, 32); sprt.anchoredPosition = new Vector2(0, 162);

            _skillBar = MakeRow("SkillBar", rootT, new Vector2(0.5f, 0), new Vector2(0, 90), 12);
            _skillBarGO = _skillBar.gameObject;
            _skillBarGO.SetActive(false);

            // 나가기(좌상단) — 허브에서만 표시
            _exitBtn = UIFactory.AddButton("Exit", rootT, "← 나가기", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            var exrt = (RectTransform)_exitBtn.transform;
            UIFactory.Anchor(exrt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            exrt.sizeDelta = new Vector2(150, 44); exrt.anchoredPosition = new Vector2(30, -30);
            _exitBtn.onClick.AddListener(RequestExit);
            _exitBtn.gameObject.SetActive(false);

            // 자동 전투 · 배속(우상단)
            _speedBtn = CornerButton("Speed", rootT, new Vector2(-30, -30), out _speedBg, out _speedLabel);
            _speedBtn.onClick.AddListener(CycleSpeed);
            _autoBtn = CornerButton("Auto", rootT, new Vector2(-200, -30), out _autoBg, out _autoLabel);
            _autoBtn.onClick.AddListener(ToggleAuto);

            BuildCutIn(rootT);
            BuildTip(rootT);
            BuildFinish(rootT);
            _turnDamage = TurnDamageCounter.Create(rootT, font);   // 오른쪽 '총 피해량'
            BuildResultOverlay(rootT);
        }

        private Button CornerButton(string name, Transform parent, Vector2 pos, out Image bg, out TextMeshProUGUI label)
        {
            var b = UIFactory.AddButton(name, parent, "", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            var rt = (RectTransform)b.transform;
            UIFactory.Anchor(rt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            rt.sizeDelta = new Vector2(155, 44); rt.anchoredPosition = pos;
            bg = b.GetComponent<Image>();
            label = b.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) { label.fontStyle = FontStyles.Bold; label.richText = true; }
            return b;
        }

        private RectTransform MakeRow(string name, Transform parent, Vector2 anchor, Vector2 pos, float spacing)
        {
            var rt = UIFactory.NewRect(name, parent);
            UIFactory.Anchor(rt, anchor, anchor, new Vector2(0.5f, 0.5f));
            rt.sizeDelta = new Vector2(1400, 100);
            rt.anchoredPosition = pos;
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            h.childControlWidth = true; h.childControlHeight = true;
            return rt;
        }

        private void BuildResultOverlay(Transform parent)
        {
            _resultGO = UIFactory.AddImage("Result", parent, new Color(0, 0, 0, 0.82f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_resultGO.transform);
            _resultGroup = _resultGO.AddComponent<CanvasGroup>();

            _resultGlow = UIFactory.AddImage("Glow", _resultGO.transform, new Color(1, 1, 1, 0), ProceduralSprites.Glow);
            _resultGlow.preserveAspect = false;
            var grt = _resultGlow.rectTransform;
            UIFactory.Anchor(grt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            grt.sizeDelta = new Vector2(1100, 500); grt.anchoredPosition = new Vector2(0, 150);

            _resultText = UIFactory.AddText("Text", _resultGO.transform, "", 72, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = _resultText.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            trt.sizeDelta = new Vector2(800, 120); trt.anchoredPosition = new Vector2(0, 150);

            _resultDetail = UIFactory.AddText("Detail", _resultGO.transform, "", 28, font, Color.white, TextAlignmentOptions.Center);
            var drt = _resultDetail.rectTransform;
            UIFactory.Anchor(drt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            drt.sizeDelta = new Vector2(1300, 200); drt.anchoredPosition = new Vector2(0, 0);
            _resultDetail.lineSpacing = 12f;

            var retry = UIFactory.AddButton("Retry", _resultGO.transform, "다시", 28, font, new Color(0.8f, 0.6f, 0.2f, 0.95f), Color.white);
            var rrt = (RectTransform)retry.transform;
            UIFactory.Anchor(rrt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rrt.sizeDelta = new Vector2(220, 70); rrt.anchoredPosition = new Vector2(0, -170);
            retry.onClick.AddListener(() =>
            {
                if (_retryHandler != null)
                {
                    // 스테이지 모드: 행동력 확인 후 같은 스테이지로 다시 출격(실패하면 이유 표시)
                    string err = _retryHandler();
                    if (!string.IsNullOrEmpty(err)) ShowResultToast(err);
                    return;
                }
                SetResultNext(null, null); _resultGO.SetActive(false); controller.Begin();
            });

            _resultToast = UIFactory.AddText("ResultToast", _resultGO.transform, "", 24, font, new Color(1f, 0.55f, 0.5f), TextAlignmentOptions.Center, FontStyles.Bold);
            var rtt = _resultToast.rectTransform;
            UIFactory.Anchor(rtt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rtt.sizeDelta = new Vector2(1200, 36); rtt.anchoredPosition = new Vector2(0, -114);
            _retryRt = rrt;

            var stats = UIFactory.AddButton("Stats", _resultGO.transform, "전투 기록", 28, font, new Color(0.25f, 0.45f, 0.75f, 0.95f), Color.white);
            _statsBtnRt = (RectTransform)stats.transform;
            UIFactory.Anchor(_statsBtnRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _statsBtnRt.sizeDelta = new Vector2(220, 70);
            stats.onClick.AddListener(OpenStats);

            _resultExitBtn = UIFactory.AddButton("Home", _resultGO.transform, "홈으로", 28, font, new Color(1, 1, 1, 0.16f), Color.white);
            var hrt = (RectTransform)_resultExitBtn.transform;
            UIFactory.Anchor(hrt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            hrt.sizeDelta = new Vector2(220, 70); hrt.anchoredPosition = new Vector2(125, -170);
            _resultExitBtn.onClick.AddListener(RequestExit);
            _resultExitBtn.gameObject.SetActive(false);

            _resultNextBtn = UIFactory.AddButton("Next", _resultGO.transform, "", 28, font, new Color(0.85f, 0.45f, 0.25f, 0.95f), Color.white);
            var nrt = (RectTransform)_resultNextBtn.transform;
            UIFactory.Anchor(nrt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            nrt.sizeDelta = new Vector2(340, 70);
            _resultNextLbl = _resultNextBtn.GetComponentInChildren<TextMeshProUGUI>();
            _resultNextLbl.richText = true;
            _resultNextBtn.onClick.AddListener(DoNext);
            _resultNextBtn.gameObject.SetActive(false);
            LayoutResultButtons();

            _stats = new BattleStatsPanel(_resultGO.transform, font);
            _resultGO.SetActive(false);
        }

        private void BuildPanels(BattleEngine engine)
        {
            ClearRow(_enemyRow); ClearRow(_allyRow); _panels.Clear();
            foreach (var u in engine.enemies)
            {
                var p = UnitPanel.Create(_enemyRow, font, OnPanelClicked, OnStatusClicked);
                p.Bind(u); _panels[u] = p;
            }
            foreach (var u in engine.allies)
            {
                var p = UnitPanel.Create(_allyRow, font, OnPanelClicked, OnStatusClicked);
                p.Bind(u); _panels[u] = p;
            }
        }

        private void BuildSkillButtons(BattleUnit unit)
        {
            ClearRow(_skillBar);
            foreach (var skill in unit.skills)
            {
                var skillRef = skill;
                Color bg = skill.IsUltimate ? new Color(0.8f, 0.6f, 0.2f, 0.95f)
                    : skill.category == SkillCategory.Skill ? new Color(0.45f, 0.3f, 0.75f, 0.95f)
                    : new Color(1f, 1f, 1f, 0.14f);
                string kind = skill.IsUltimate ? "궁극기" : skill.category == SkillCategory.Skill ? "스킬" : "기본 공격";
                string label = $"<size=60%><color=#FFE7A3>{kind}</color></size>  {skill.skillName}\n" +
                               $"<size=58%>{SkillText.Describe(skill)}</size>\n<size=56%><color=#BFD8FF>{SkillText.Cost(skill)}</color></size>";

                var btn = UIFactory.AddButton("Skill", _skillBar, label, 21, font, bg, Color.white);
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 270; le.preferredHeight = 92;
                var lbl = btn.GetComponentInChildren<TextMeshProUGUI>();
                lbl.richText = true; lbl.lineSpacing = -6f;
                lbl.enableAutoSizing = true; lbl.fontSizeMin = 12; lbl.fontSizeMax = 21;
                btn.interactable = controller.Engine.CanUse(unit, skill);
                btn.onClick.AddListener(() => OnSkillChosen(skillRef));
            }
        }

        // ---------------- 턴 순서 / 연출 ----------------

        private void RefreshTurnStrip()
        {
            if (controller == null || controller.Engine == null) return;
            ClearRow(_turnStrip);
            var order = controller.Engine.PreviewOrder(Mathf.Max(1, turnPreviewCount));
            bool first = true;
            foreach (var u in order)
            {
                var baseCol = u.team == Team.Ally ? new Color(0.3f, 0.55f, 0.95f) : new Color(0.9f, 0.35f, 0.4f);
                var chip = UIFactory.AddImage("Chip", _turnStrip, first ? Color.Lerp(baseCol, Color.white, 0.45f) : baseCol, ProceduralSprites.Rounded);
                chip.type = Image.Type.Sliced; chip.preserveAspect = false; chip.pixelsPerUnitMultiplier = 2.5f;
                var le = chip.gameObject.AddComponent<LayoutElement>();
                float sz = first ? 62 : 48;
                le.preferredWidth = sz; le.preferredHeight = sz;
                var inner = UIFactory.AddImage("Inner", chip.transform, new Color(0.08f, 0.09f, 0.12f, 1f));
                UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);
                Sprite sp = u.character != null ? GameArt.Thumb(u.character) : GameArt.Enemy(u.Name);
                if (sp != null)
                {
                    var face = UIFactory.AddImage("Face", inner.transform, Color.white, sp);
                    UIFactory.Stretch(face.rectTransform, 1, 1, 1, 1);
                }
                else
                {
                    var t = UIFactory.AddText("T", inner.transform, Short(u.Name), first ? 17 : 14, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                    UIFactory.Stretch(t.rectTransform);
                }
                first = false;
            }
        }

        /// <summary>
        /// 떠오르는 숫자/문구. size 0 = 기본(큰 44 · 보통 32). glow가 있으면 뒤에 그 색 빛.
        /// x가 NaN이면 좌우 무작위. 이펙트보다 위(_popupLayer)에 그림.
        /// </summary>
        private void SpawnPopup(UnitPanel panel, string text, Color color, bool big, float delay = 0f,
            float size = 0f, Color? glow = null, float x = float.NaN)
        {
            Transform parent = _popupLayer != null ? _popupLayer : panel.transform;
            var root = UIFactory.NewRect("Popup", parent);
            UIFactory.Anchor(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            root.sizeDelta = new Vector2(300, 120);
            Image g = null;
            Color gc = glow ?? Color.clear;
            if (glow.HasValue)
            {
                g = UIFactory.AddImage("Glow", root, new Color(gc.r, gc.g, gc.b, 0f), ProceduralSprites.Glow);
                g.preserveAspect = false;
                UIFactory.Stretch(g.rectTransform, 20, 10, 20, 10);
            }
            var t = UIFactory.AddText("T", root, text, size > 0f ? size : (big ? 44 : 32), font, color,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(t.rectTransform);
            t.richText = true; t.lineSpacing = -18f;
            t.alpha = 0f;
            Vector2 basePos = _popupLayer != null && _fx != null ? _fx.ToLocal((RectTransform)panel.transform) : Vector2.zero;
            root.anchoredPosition = basePos;
            StartCoroutine(FloatDamage(root, t, g, gc, basePos, float.IsNaN(x) ? Random.Range(-26f, 26f) : x, big, delay));
        }

        private IEnumerator After(float delay, System.Action action)
        {
            for (float t = 0; t < delay; t += Time.unscaledDeltaTime) yield return null;
            action?.Invoke();
        }

        /// <summary>숫자: 크게 튀어나왔다 제자리(펀치) → 위로 떠오르며 사라짐.</summary>
        private IEnumerator FloatDamage(RectTransform rt, TextMeshProUGUI t, Image glow, Color gc, Vector2 basePos, float x, bool big, float delay)
        {
            for (float d = 0; d < delay; d += Time.unscaledDeltaTime) yield return null;
            float dur = big ? 1.0f : 0.8f, time = 0f;
            while (time < dur && rt != null)
            {
                time += Time.unscaledDeltaTime;
                float k = time / dur;
                float punch = k < 0.15f ? Mathf.Lerp(big ? 1.9f : 1.5f, 1f, k / 0.15f) : 1f;
                rt.localScale = Vector3.one * punch;
                rt.anchoredPosition = basePos + new Vector2(x, Mathf.Lerp(16f, big ? 84f : 70f, Mathf.SmoothStep(0, 1, k)));
                float a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
                t.alpha = a;
                if (glow != null) glow.color = new Color(gc.r, gc.g, gc.b, 0.55f * a * (k < 0.15f ? 1.4f : 1f));
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        // ---------------- 전투 연출 ----------------

        /// <summary>유닛 줄 전체 흔들림(치명타·브레이크·필살기).</summary>
        private void ScreenShake(float amp, float dur)
        {
            if (_shakeAnim != null) StopCoroutine(_shakeAnim);
            _shakeAnim = StartCoroutine(ShakeRoutine(amp, dur));
        }

        private IEnumerator ShakeRoutine(float amp, float dur)
        {
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / dur;
                _stage.anchoredPosition = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * amp * k;
                yield return null;
            }
            _stage.anchoredPosition = Vector2.zero;
            _shakeAnim = null;
        }

        /// <summary>브레이크: 패널에서 금빛 고리가 퍼지며 사라짐.</summary>
        private IEnumerator BreakBurst(UnitPanel panel)
        {
            var ring = UIFactory.AddImage("BreakRing", panel.transform, new Color(1f, 0.75f, 0.3f, 0.9f), ProceduralSprites.Ring);
            var rt = ring.rectTransform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = new Vector2(120, 120);
            var glow = UIFactory.AddImage("BreakGlow", panel.transform, new Color(1f, 0.8f, 0.35f, 0.7f), ProceduralSprites.Glow);
            var grt = glow.rectTransform;
            UIFactory.Anchor(grt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            grt.sizeDelta = new Vector2(300, 300);
            for (float t = 0; t < 0.5f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.5f;
                rt.localScale = Vector3.one * Mathf.Lerp(0.4f, 2.6f, k);
                ring.color = new Color(1f, 0.75f, 0.3f, 0.9f * (1f - k));
                glow.color = new Color(1f, 0.8f, 0.35f, 0.7f * (1f - k));
                yield return null;
            }
            Destroy(ring.gameObject);
            Destroy(glow.gameObject);
        }

        /// <summary>스킬 사용 시 행동자 위(적은 아래)에 스킬 이름 띠.</summary>
        private void ShowSkillBanner(UnitPanel panel, BattleUnit actor, BattleSkill skill, float dur)
        {
            var band = UIFactory.AddImage("SkillBanner", panel.transform, new Color(0.25f, 0.18f, 0.45f, 0.92f), ProceduralSprites.Rounded);
            band.type = Image.Type.Sliced; band.preserveAspect = false; band.pixelsPerUnitMultiplier = 2.5f;
            var rt = band.rectTransform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = new Vector2(230, 40);
            rt.anchoredPosition = new Vector2(0, actor.team == Team.Ally ? 76 : -76);
            var t = UIFactory.AddText("T", band.transform, skill.skillName, 20, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(t.rectTransform);
            StartCoroutine(BannerRoutine(band, t, Mathf.Max(0.35f, dur)));
        }

        private IEnumerator BannerRoutine(Image band, TextMeshProUGUI t, float dur)
        {
            var rt = band.rectTransform;
            for (float time = 0; time < dur && band != null; time += Time.unscaledDeltaTime)
            {
                float k = time / dur;
                float a = k < 0.15f ? k / 0.15f : k > 0.75f ? 1f - (k - 0.75f) / 0.25f : 1f;
                band.color = new Color(band.color.r, band.color.g, band.color.b, 0.92f * a);
                t.alpha = a;
                rt.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(k / 0.15f)), 1f, 1f);
                yield return null;
            }
            if (band != null) Destroy(band.gameObject);
        }

        /// <summary>아군 필살기 컷인: 어두워짐 → 비스듬한 띠 + 속도선 → 캐릭터 일러스트(6성은 움직이는 일러스트)와 스킬 이름이 미끄러져 들어옴.</summary>
        private IEnumerator UltimateCutIn(BattleUnit actor, BattleSkill skill, float dur)
        {
            var c = actor.character;
            var accent = Traits.ColorOf(actor.element);
            _cut.gameObject.SetActive(true);
            _cut.SetAsLastSibling();
            if (_resultGO != null) _resultGO.transform.SetAsLastSibling();
            _cutGroup.alpha = 1f;
            _cutAccent.color = accent;
            _cutLabel.text = "궁극기";
            _cutLabel.color = accent;
            _cutName.text = skill.skillName;
            _cutUser.text = actor.Name;

            var sprite = c != null ? GameArt.Portrait(c) : null;
            _cutArt.sprite = sprite;
            bool live = c != null && Live2DUI.Show(_cutArt, c);
            _cutArt.enabled = !live && sprite != null;
            _cutArt.color = Color.white;

            var lines = new List<RectTransform>();
            foreach (RectTransform l in _cutLines) lines.Add(l);

            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                float inK = Mathf.Clamp01(k / 0.2f);
                float ease = 1f - Mathf.Pow(1f - inK, 3f);
                _cutDim.color = new Color(0, 0, 0, 0.6f * ease);
                _cutBand.localScale = new Vector3(1f, ease, 1f);
                _cutArtRT.anchoredPosition = new Vector2(Mathf.Lerp(-1250f, -470f, ease) + 50f * k, -10f);
                _cutName.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(1100f, 320f, ease) - 30f * k, 6f);
                _cutLabel.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(1100f, 320f, ease) - 30f * k, 82f);
                _cutUser.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(1100f, 320f, ease) - 30f * k, -62f);
                for (int i = 0; i < lines.Count; i++)
                {
                    var l = lines[i];
                    float x = Mathf.Repeat(1300f - (t * (2600f + i * 180f)) + i * 230f, 2600f) - 1300f;
                    l.anchoredPosition = new Vector2(x, l.anchoredPosition.y);
                }
                _cutGroup.alpha = k > 0.86f ? 1f - (k - 0.86f) / 0.14f : 1f;
                if (live) Live2DUI.SetAlpha(_cutArt, _cutGroup.alpha);
                yield return null;
            }
            if (live) Live2DUI.Hide(_cutArt);
            _cut.gameObject.SetActive(false);
        }

        private IEnumerator ResultIntro(bool win)
        {
            var rt = _resultText.rectTransform;
            var gc = win ? new Color(1f, 0.82f, 0.35f) : new Color(0.7f, 0.25f, 0.25f);
            for (float t = 0; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.6f;
                _resultGroup.alpha = Mathf.Clamp01(k / 0.5f);
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(k / 0.4f), 3f);
                rt.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, e);
                _resultText.alpha = e;
                _resultGlow.color = new Color(gc.r, gc.g, gc.b, 0.55f * e);
                yield return null;
            }
            rt.localScale = Vector3.one;
            _resultText.alpha = 1f;
            _resultGroup.alpha = 1f;
            if (_finishFlash != null) _finishFlash.gameObject.SetActive(false);
        }

        private void BuildCutIn(Transform root)
        {
            _cut = UIFactory.NewRect("UltimateCutIn", root);
            UIFactory.Stretch(_cut);
            _cutGroup = _cut.gameObject.AddComponent<CanvasGroup>();
            _cutGroup.blocksRaycasts = false;

            _cutDim = UIFactory.AddImage("Dim", _cut, new Color(0, 0, 0, 0));
            UIFactory.Stretch(_cutDim.rectTransform);

            var bandImg = UIFactory.AddImage("Band", _cut, new Color(0.07f, 0.08f, 0.13f, 0.94f));
            _cutBand = bandImg.rectTransform;
            UIFactory.Anchor(_cutBand, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _cutBand.sizeDelta = new Vector2(2800, 330); _cutBand.anchoredPosition = new Vector2(0, -10);
            _cutBand.localEulerAngles = new Vector3(0, 0, -6f);
            _cutAccent = UIFactory.AddImage("Accent", _cutBand, Color.white);
            var art = _cutAccent.rectTransform;
            UIFactory.Anchor(art, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            art.sizeDelta = new Vector2(0, 8); art.anchoredPosition = Vector2.zero;
            var accent2 = UIFactory.AddImage("Accent2", _cutBand, new Color(1, 1, 1, 0.5f));
            var a2 = accent2.rectTransform;
            UIFactory.Anchor(a2, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            a2.sizeDelta = new Vector2(0, 3); a2.anchoredPosition = Vector2.zero;

            _cutLines = UIFactory.NewRect("SpeedLines", _cutBand);
            UIFactory.Stretch(_cutLines);
            for (int i = 0; i < 12; i++)
            {
                var l = UIFactory.AddImage("Line", _cutLines, new Color(1, 1, 1, 0.12f + 0.1f * (i % 3)));
                var lrt = l.rectTransform;
                UIFactory.Anchor(lrt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                lrt.sizeDelta = new Vector2(260 + (i * 97) % 420, 3 + i % 3);
                lrt.anchoredPosition = new Vector2(0, -140 + i * 25);
            }

            _cutArt = UIFactory.AddImage("Art", _cut, Color.white);
            _cutArtRT = _cutArt.rectTransform;
            UIFactory.Anchor(_cutArtRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _cutArtRT.sizeDelta = new Vector2(620, 820);
            _cutArt.preserveAspect = true;

            _cutLabel = UIFactory.AddText("Label", _cut, "궁극기", 28, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            SizeLeft(_cutLabel.rectTransform, 900, 40);
            _cutName = UIFactory.AddText("Name", _cut, "", 72, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            SizeLeft(_cutName.rectTransform, 900, 96);
            _cutName.enableAutoSizing = true; _cutName.fontSizeMin = 30; _cutName.fontSizeMax = 72;
            _cutUser = UIFactory.AddText("User", _cut, "", 28, font, new Color(0.82f, 0.86f, 0.95f), TextAlignmentOptions.Left, FontStyles.Bold);
            SizeLeft(_cutUser.rectTransform, 900, 40);

            _cut.gameObject.SetActive(false);
        }

        private static void SizeLeft(RectTransform rt, float w, float h)
        {
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f));
            rt.sizeDelta = new Vector2(w, h);
        }

        // ---------------- 유틸 ----------------

        private void ClearTargetable()
        {
            foreach (var kv in _panels) kv.Value.SetTargetable(false);
        }

        /// <summary>약점 어시스트: 행동을 고르는 아군의 속성이 약점인 적에게 '속성 아이콘 + 약점!' 표시. null이면 모두 끔.</summary>
        private void ShowWeakHints(BattleUnit actor)
        {
            bool on = actor != null && actor.team == Team.Ally;
            var element = on ? actor.element : Element.Physical;
            foreach (var kv in _panels) kv.Value.SetWeakHint(on, element);
        }

        private void ClearWeakHints() => ShowWeakHints(null);

        private void SetTargetableUnits(List<BattleUnit> units)
        {
            ClearTargetable();
            foreach (var u in units)
                if (_panels.TryGetValue(u, out var p)) p.SetTargetable(true);
        }

        private List<BattleUnit> AliveEnemies()
        {
            var list = new List<BattleUnit>();
            foreach (var u in controller.Engine.enemies) if (u.IsAlive) list.Add(u);
            return list;
        }

        private List<BattleUnit> AliveAllies()
        {
            var list = new List<BattleUnit>();
            foreach (var u in controller.Engine.allies) if (u.IsAlive) list.Add(u);
            return list;
        }

        private void SetInstruction(string s)
        {
            _instruction.text = s;
            _instruction.gameObject.SetActive(!string.IsNullOrEmpty(s));
        }

        private void RefreshSkillPoints()
        {
            if (controller == null || controller.Engine == null) { _spText.text = ""; return; }
            _spText.text = $"스킬 포인트  {controller.Engine.AllySkillPoints} / {controller.Engine.MaxSkillPoints}";
        }

        private static string Short(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            return name.Length <= 2 ? name : name.Substring(0, 2);
        }

        private static void ClearRow(RectTransform row)
        {
            for (int i = row.childCount - 1; i >= 0; i--)
            {
                var ch = row.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
