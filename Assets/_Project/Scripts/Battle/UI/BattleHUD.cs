using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;

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
        private RectTransform _enemyRow, _allyRow, _turnStrip, _skillBar;
        private GameObject _skillBarGO, _resultGO;
        private TextMeshProUGUI _instruction, _resultText, _resultDetail, _spText;

        private readonly Dictionary<BattleUnit, UnitPanel> _panels = new();
        private BattleUnit _inputActor;
        private BattleSkill _pendingSkill;
        private bool _selecting;
        private BattleEngine _builtEngine;

        // 허브(내비게이터)용 나가기
        private Button _exitBtn, _resultExitBtn;
        private RectTransform _retryRt;
        private System.Action _onExit;

        private void Awake()
        {
            if (controller == null) controller = FindAnyObjectByType<BattleController>();
            BuildUI();
        }

        private void OnEnable()
        {
            if (controller == null) return;
            controller.OnBattleStart += HandleStart;
            controller.OnTurnBegin += HandleTurnBegin;
            controller.OnPlayerInputNeeded += HandlePlayerInput;
            controller.OnActionResolved += HandleResolved;
            controller.OnStatusTick += HandleStatusTick;
            controller.OnBattleEnd += HandleEnd;
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.OnBattleStart -= HandleStart;
            controller.OnTurnBegin -= HandleTurnBegin;
            controller.OnPlayerInputNeeded -= HandlePlayerInput;
            controller.OnActionResolved -= HandleResolved;
            controller.OnStatusTick -= HandleStatusTick;
            controller.OnBattleEnd -= HandleEnd;
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
            if (_retryRt != null) _retryRt.anchoredPosition = new Vector2(v ? -125 : 0, -170);
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

        private void RequestExit()
        {
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
            SetInstruction("");

            BuildPanels(engine);
            RefreshTurnStrip();
            RefreshSkillPoints();
        }

        private void HandleTurnBegin(BattleUnit unit)
        {
            foreach (var p in _panels.Values) p.SetCurrent(false);
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
        }

        private void HandleResolved(ActionResult result)
        {
            _skillBarGO.SetActive(false);
            _selecting = false; _pendingSkill = null;
            ClearTargetable();
            SetInstruction("");

            foreach (var h in result.hits)
            {
                if (!_panels.TryGetValue(h.target, out var panel)) continue;
                if (h.healed > 0)
                    SpawnPopup(panel, "+" + h.healed, new Color(0.4f, 0.9f, 0.5f), false);
                else if (h.totalDamage > 0)
                {
                    var col = h.anyCrit ? new Color(1f, 0.85f, 0.35f)
                        : (h.weakness ? new Color(1f, 0.6f, 0.9f) : Color.white);
                    SpawnPopup(panel, h.anyCrit ? $"{h.totalDamage}!" : h.totalDamage.ToString(), col, h.anyCrit || h.weakness);
                }
                if (h.broke) SpawnPopup(panel, "BREAK!", new Color(1f, 0.8f, 0.3f), true);
            }

            foreach (var p in _panels.Values) p.Refresh();
            RefreshSkillPoints();
        }

        private void HandleStatusTick(BattleUnit unit, int dot)
        {
            if (dot > 0 && _panels.TryGetValue(unit, out var panel))
            {
                SpawnPopup(panel, dot.ToString(), new Color(0.75f, 0.5f, 1f), false);
                panel.Refresh();
            }
        }

        private void HandleEnd(Team winner)
        {
            foreach (var p in _panels.Values) p.SetCurrent(false);
            bool win = winner == Team.Ally;
            _resultText.text = win ? "승리!" : "패배...";
            _resultText.color = win ? new Color(1f, 0.85f, 0.35f) : new Color(0.9f, 0.5f, 0.5f);
            _resultGO.SetActive(true);
        }

        // ---------------- 입력 ----------------

        private void OnSkillChosen(BattleSkill skill)
        {
            if (_inputActor == null) return;
            if (!controller.Engine.CanUse(_inputActor, skill)) return;

            // 광역/자기 대상은 즉시 발동
            if (skill.target == TargetType.AllEnemies || skill.target == TargetType.AllAllies || skill.target == TargetType.Self)
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
            scaler.matchWidthOrHeight = 0.5f;
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var rootT = _canvas.transform;

            if (drawBackground)
            {
                var bg = UIFactory.AddImage("Backdrop", rootT, new Color(0.05f, 0.06f, 0.09f, 1f), null, raycast: true);
                UIFactory.Stretch(bg.rectTransform);
            }

            _turnStrip = MakeRow("TurnStrip", rootT, new Vector2(0.5f, 1), new Vector2(0, -28), 8);
            _enemyRow = MakeRow("EnemyRow", rootT, new Vector2(0.5f, 1), new Vector2(0, -150), 16);
            _allyRow = MakeRow("AllyRow", rootT, new Vector2(0.5f, 0.5f), new Vector2(0, -40), 16);

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

            BuildResultOverlay(rootT);
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
            retry.onClick.AddListener(() => { _resultGO.SetActive(false); controller.Begin(); });
            _retryRt = rrt;

            _resultExitBtn = UIFactory.AddButton("Home", _resultGO.transform, "홈으로", 28, font, new Color(1, 1, 1, 0.16f), Color.white);
            var hrt = (RectTransform)_resultExitBtn.transform;
            UIFactory.Anchor(hrt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            hrt.sizeDelta = new Vector2(220, 70); hrt.anchoredPosition = new Vector2(125, -170);
            _resultExitBtn.onClick.AddListener(RequestExit);
            _resultExitBtn.gameObject.SetActive(false);

            _resultGO.SetActive(false);
        }

        private void BuildPanels(BattleEngine engine)
        {
            ClearRow(_enemyRow); ClearRow(_allyRow); _panels.Clear();
            foreach (var u in engine.enemies)
            {
                var p = UnitPanel.Create(_enemyRow, font, OnPanelClicked);
                p.Bind(u); _panels[u] = p;
            }
            foreach (var u in engine.allies)
            {
                var p = UnitPanel.Create(_allyRow, font, OnPanelClicked);
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
                string sub = skill.IsUltimate ? "필살기" : skill.category == SkillCategory.Skill ? "SP -1" : "SP +1";
                string label = $"{skill.skillName}\n<size=62%>{sub}</size>";

                var btn = UIFactory.AddButton("Skill", _skillBar, label, 20, font, bg, Color.white);
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 185; le.preferredHeight = 64;
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
                var chip = UIFactory.AddImage("Chip", _turnStrip, first ? Color.Lerp(baseCol, Color.white, 0.35f) : baseCol);
                var le = chip.gameObject.AddComponent<LayoutElement>();
                float sz = first ? 52 : 44;
                le.preferredWidth = sz; le.preferredHeight = sz;
                var t = UIFactory.AddText("T", chip.transform, Short(u.Name), first ? 16 : 14, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Stretch(t.rectTransform);
                first = false;
            }
        }

        private void SpawnPopup(UnitPanel panel, string text, Color color, bool big)
        {
            var t = UIFactory.AddText("Popup", panel.transform, text, big ? 38 : 30, font, color,
                TextAlignmentOptions.Center, FontStyles.Bold);
            var rt = t.rectTransform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = new Vector2(200, 60);
            StartCoroutine(FloatDamage(t));
        }

        private IEnumerator FloatDamage(TextMeshProUGUI t)
        {
            var rt = t.rectTransform;
            float dur = 0.75f, time = 0f;
            while (time < dur && t != null)
            {
                time += Time.unscaledDeltaTime;
                float k = time / dur;
                rt.anchoredPosition = new Vector2(0, Mathf.Lerp(10f, 64f, k));
                var c = t.color; c.a = 1f - k; t.color = c;
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        // ---------------- 유틸 ----------------

        private void ClearTargetable()
        {
            foreach (var kv in _panels) kv.Value.SetTargetable(false);
        }

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
