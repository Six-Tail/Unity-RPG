using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 스테이지 선택 화면. 챕터 탭 → 스테이지 노드(별·잠금) → 상세(적·별 조건·보상) → '편성 후 출격'.
    /// '돌파 던전' 탭: 속성(행) × 등급(열: 초급 40 / 중급 60 / 상급 80 돌파) 격자, 스테이지 진행으로 개방.
    /// 코드 자동 구성 — database/progress 연결(progress는 비우면 자동 탐색).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Stage Screen")]
    public class StageScreen : MonoBehaviour
    {
        [SerializeField] private StageDatabase database;
        [SerializeField] private StageProgress progress;
        [SerializeField, Tooltip("행동력(비우면 자동 탐색).")] private PlayerWallet wallet;
        [SerializeField] private TMP_FontAsset font;

        public StageDatabase Database => database;

        /// <summary>'편성 후 출격'을 눌렀을 때(선택 스테이지).</summary>
        public event System.Action<StageData> OnSortie;

        private GameObject _root, _detPanel;
        private RectTransform _modeTabs, _tabs, _nodes;
        private GridLayoutGroup _grid;
        private bool _dungeonMode;
        private StageKind _dungeonKind = StageKind.AscensionDungeon;
        private StageDifficulty _difficulty = StageDifficulty.Easy;
        private TextMeshProUGUI _totalStars, _chapterTitle, _detTitle, _detBody, _sortieLbl;
        private Button _backBtn, _sortieBtn;
        private System.Action _onBack;

        private int _chapter = 1;
        private Button _starRewardBtn;
        private NotifyBadge _starRewardBadge;
        private StageData _selected;
        private readonly Dictionary<StageData, Image> _nodeHL = new();

        private static readonly Color Gold = new(1f, 0.83f, 0.42f);

        private void Awake()
        {
            if (progress == null) progress = FindAnyObjectByType<StageProgress>();
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            BuildUI();
        }

        private void OnEnable()
        {
            if (progress != null) progress.OnChanged += Refresh;
            if (wallet != null) wallet.OnChanged += OnWalletChanged;
        }

        private void OnDisable()
        {
            if (progress != null) progress.OnChanged -= Refresh;
            if (wallet != null) wallet.OnChanged -= OnWalletChanged;
        }

        private void OnWalletChanged()
        {
            RefreshStamina();
            RefreshEntry();
        }

        // ---------------- 행동력 · 보상 배수 · 반복 입장 ----------------

        private TextMeshProUGUI _costText, _boostHint;
        private ScrollRect _detScroll;
        private StageData _detShown;
        private Button _storyBtn;
        private StageRunner _runner;
        private GameObject _multRow, _repeatRow;
        private Button _sweep1, _sweepN;
        private TextMeshProUGUI _sweep1Lbl, _sweepNLbl;
        private int _sweepNRuns;
        private readonly List<(Button btn, int value)> _multBtns = new(), _repeatBtns = new();
        private float _staminaClock;

        private void Update()
        {
            // 다음 회복까지 남은 시간 표시(1초마다)
            if (_root == null || !_root.activeInHierarchy) return;
            _staminaClock -= Time.unscaledDeltaTime;
            if (_staminaClock > 0f) return;
            _staminaClock = 1f;
            RefreshStamina();
        }

        private void RefreshStamina()
        {
            // 행동력 표시는 CurrencyBar가 스스로 갱신(다음 회복까지 남은 시간 포함)
        }

        /// <summary>상세 패널 아래쪽: 행동력 비용 · 보상 배수 · 반복 입장 · 출격 버튼 상태.</summary>
        private void RefreshEntry()
        {
            if (_costText == null || _selected == null) return;
            var s = _selected;
            bool unlocked = progress == null || progress.IsUnlocked(s, database);
            bool boost = StageEntry.CanBoost(s, progress);
            int mult = StageEntry.MultiplierFor(s, progress);
            int baseCost = StageEntry.BaseCost(s);
            int cost = baseCost * mult;
            int have = wallet != null ? wallet.Stamina : int.MaxValue;
            bool afford = wallet == null || have >= cost;

            _multRow.SetActive(boost);
            _repeatRow.SetActive(boost);
            _boostHint.gameObject.SetActive(!boost);
            _boostHint.text = "<color=#9AA3B5>★3으로 클리어하면 <color=#FFD36B>보상 배수(x2 · x3)</color>와 <color=#7FE3D6>반복 입장</color>을 쓸 수 있습니다</color>";

            foreach (var (btn, v) in _multBtns)
            {
                bool on = v == StageEntry.Multiplier;
                btn.GetComponent<Image>().color = on ? new Color(0.85f, 0.6f, 0.2f, 0.98f) : new Color(1, 1, 1, 0.12f);
                btn.transform.localScale = Vector3.one * (on ? 1.06f : 1f);
            }
            foreach (var (btn, v) in _repeatBtns)
            {
                bool on = v == StageEntry.Repeat;
                btn.GetComponent<Image>().color = on ? new Color(0.2f, 0.6f, 0.6f, 0.98f) : new Color(1, 1, 1, 0.12f);
                btn.transform.localScale = Vector3.one * (on ? 1.06f : 1f);
            }

            string costPart = mult > 1 ? $"{baseCost} × {mult} = <b>{cost}</b>" : $"<b>{cost}</b>";
            string bonus = mult > 1 ? $"   <color=#FFD36B>보상 ×{mult}</color>" : "";
            string haveText = wallet != null ? $"   <size=85%><color={(afford ? "#9AA3B5" : "#FF8A8A")}>(보유 {have})</color></size>" : "";
            _costText.text = $"<color=#7CFFB2>행동력</color> {costPart}{bonus}{haveText}";

            RefreshSweep(s, unlocked && boost, cost);
            if (!unlocked) return;   // 잠김 문구는 RefreshDetail이 씀
            _sortieBtn.interactable = afford;
            _sortieLbl.text = afford ? $"편성 후 출격  <size=70%>· 행동력 {cost}</size>" : $"행동력 부족  <size=70%>({have} / {cost})</size>";
        }

        // ---------------- 소탕(★3 스테이지) ----------------

        /// <summary>★3 스테이지면 출격 버튼을 왼쪽으로 줄이고 오른쪽에 '소탕 1회' · '소탕 N회'.</summary>
        private void RefreshSweep(StageData s, bool show, int cost)
        {
            if (_sweep1 == null) return;
            _sweep1.gameObject.SetActive(show);
            _sweepN.gameObject.SetActive(show);
            var srt = (RectTransform)_sortieBtn.transform;
            srt.sizeDelta = new Vector2(show ? 300 : 420, 76);
            srt.anchoredPosition = new Vector2(show ? -150 : 0, 30);
            if (!show) return;
            if (_runner == null) _runner = FindAnyObjectByType<StageRunner>();
            int can = _runner != null ? _runner.SweepAffordable(s) : 0;
            int tickets = _runner != null ? _runner.SweepTickets : 0;
            _sweepNRuns = Mathf.Max(2, can);
            // 소탕권이 없어도 눌러서 이유(획득처)를 볼 수 있게 1회 버튼은 켜 둠
            _sweep1.interactable = true;
            _sweepN.interactable = can >= 2;
            string ticketHex = tickets > 0 ? "#C9B6FF" : "#FF8A8A";
            _sweep1Lbl.text = $"소탕 1회\n<size=60%><color={ticketHex}>소탕권 {tickets}</color></size>";
            _sweepNLbl.text = can >= 2 ? $"소탕 {can}회\n<size=60%>행동력 {cost * can}</size>"
                                       : $"소탕 {StageRunner.MaxSweep}회\n<size=60%>{(tickets < 2 ? "소탕권 부족" : "행동력 부족")}</size>";
        }

        private void DoSweep(int runs)
        {
            if (_selected == null) return;
            if (_runner == null) _runner = FindAnyObjectByType<StageRunner>();
            if (_runner == null) return;
            string text = _runner.Sweep(_selected, runs);
            if (text == null) { Popups.Show("소탕", _runner.LastError ?? "소탕할 수 없습니다"); return; }
            Popups.Show("소탕 완료", text);
            RefreshEntry();
        }

        private Button SweepButton(Transform panel, float x, out TextMeshProUGUI label, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIFactory.AddButton("Sweep", panel, "", 22, font, new Color(0.25f, 0.6f, 0.55f, 0.96f), Color.white);
            var rt = (RectTransform)b.transform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            rt.sizeDelta = new Vector2(140, 76); rt.anchoredPosition = new Vector2(x, 30);
            label = b.GetComponentInChildren<TextMeshProUGUI>();
            label.richText = true; label.lineSpacing = -10f;
            b.onClick.AddListener(onClick);
            b.gameObject.SetActive(false);
            return b;
        }

        private void SetMultiplier(int v)
        {
            StageEntry.Multiplier = v;
            RefreshEntry();
        }

        private void SetRepeat(int v)
        {
            StageEntry.Repeat = v;
            RefreshEntry();
        }

        private void BuildEntryControls(Transform panel)
        {
            _costText = UIFactory.AddText("Cost", panel, "", 21, font, Color.white, TextAlignmentOptions.Center);
            var crt = _costText.rectTransform;
            UIFactory.Anchor(crt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            crt.sizeDelta = new Vector2(-40, 32); crt.anchoredPosition = new Vector2(0, 212);

            _multRow = OptionRow(panel, "보상 배수", 160, out var multHolder);
            for (int m = 1; m <= StageEntry.MaxMultiplier; m++)
            {
                int v = m;
                var b = OptionButton(multHolder, $"x{m}", 96, () => SetMultiplier(v));
                _multBtns.Add((b, v));
            }
            _repeatRow = OptionRow(panel, "반복 입장", 112, out var repHolder);
            foreach (var r in StageEntry.RepeatOptions)
            {
                int v = r;
                var b = OptionButton(repHolder, StageEntry.RepeatLabel(r), 76, () => SetRepeat(v));
                _repeatBtns.Add((b, v));
            }

            _boostHint = UIFactory.AddText("BoostHint", panel, "", 18, font, Color.white, TextAlignmentOptions.Center);
            var hrt = _boostHint.rectTransform;
            UIFactory.Anchor(hrt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            hrt.sizeDelta = new Vector2(-60, 80); hrt.anchoredPosition = new Vector2(0, 118);
            _boostHint.enableAutoSizing = true; _boostHint.fontSizeMin = 13; _boostHint.fontSizeMax = 18;
        }

        private GameObject OptionRow(Transform panel, string label, float y, out RectTransform holder)
        {
            var row = UIFactory.NewRect(label, panel);
            UIFactory.Anchor(row, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            row.sizeDelta = new Vector2(-40, 44); row.anchoredPosition = new Vector2(0, y);
            var lbl = UIFactory.AddText("L", row, label, 19, font, new Color(0.8f, 0.83f, 0.9f), TextAlignmentOptions.Left, FontStyles.Bold);
            UIFactory.Anchor(lbl.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f));
            lbl.rectTransform.sizeDelta = new Vector2(120, 0); lbl.rectTransform.anchoredPosition = new Vector2(20, 0);
            holder = UIFactory.NewRect("Buttons", row);
            UIFactory.Stretch(holder, 140, 0, 10, 0);
            var hl = holder.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 8; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            return row.gameObject;
        }

        private Button OptionButton(RectTransform holder, string label, float width, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIFactory.AddButton("Opt", holder, label, 20, font, new Color(1, 1, 1, 0.12f), Color.white);
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.preferredHeight = 44;
            b.onClick.AddListener(onClick);
            return b;
        }

        private void Start()
        {
            JumpToFrontier();
            Refresh();
        }

        /// <summary>테스트: 쉬움 1 ~ 3장의 모든 스테이지를 별 3개로 클리어 처리(첫 클리어 보상 · 스토리는 건너뜀).</summary>
        private void TestClearToChapter3()
        {
            if (database == null || progress == null) return;
            int n = 0;
            foreach (var s in database.Ordered(StageDifficulty.Easy))
            {
                if (s == null || s.chapter > 3) continue;
                if (progress.StarsOf(s) >= 3) continue;
                progress.RecordClear(s, 3);
                n++;
            }
            JumpToFrontier();
            Refresh();
            Popups.Show("3장까지 클리어 (테스트)",
                n > 0 ? $"쉬움 1 ~ 3장 스테이지 {n}개를 별 3개로 클리어 처리했습니다.\n결투장 · 주간 보스 · 월드 레이드 · 이벤트 · 파견 · 무한의 탑이 열립니다.\n<size=85%><color=#9AA3B5>첫 클리어 보상과 스토리는 건너뜁니다. 챕터 별 보상은 스테이지 화면에서 받을 수 있습니다.</color></size>"
                      : "이미 3장까지 모두 별 3개로 클리어되어 있습니다.");
        }

        public void Show(bool on)
        {
            if (_root != null) _root.SetActive(on);
            if (on) Refresh();
        }

        public void SetBackAction(System.Action onBack)
        {
            _onBack = onBack;
            if (_backBtn != null) _backBtn.gameObject.SetActive(onBack != null);
        }

        /// <summary>지정 스테이지를 선택(챕터/던전 탭도 맞춤).</summary>
        public void Focus(StageData s)
        {
            if (s == null) return;
            _dungeonMode = s.IsDungeon;
            if (s.IsDungeon) _dungeonKind = s.kind;
            else { _chapter = s.chapter; _difficulty = s.difficulty; }
            _selected = s;
        }

        /// <summary>특수 던전 탭으로 열기(HubScreen.Dungeon).</summary>
        public void OpenSpecial(StageKind kind = StageKind.AscensionDungeon)
        {
            _dungeonMode = true;
            if (kind != StageKind.Main) _dungeonKind = kind;
            _selected = null;
        }

        /// <summary>가장 앞선 해금 스테이지(아직 안 깬 것)로 챕터·선택 이동.</summary>
        public void JumpToFrontier()
        {
            if (database == null) return;
            StageData target = null;
            foreach (var s in database.Ordered())
            {
                if (progress != null && !progress.IsUnlocked(s, database)) break;
                target = s;
                if (progress == null || !progress.IsCleared(s)) break;
            }
            if (target != null) { _chapter = target.chapter; _difficulty = target.difficulty; _selected = target; }
        }

        // ---------------- 갱신 ----------------

        private void Refresh()
        {
            if (_root == null) return;
            if (database == null)
            {
                _chapterTitle.text = "StageDatabase가 연결되지 않았습니다";
                _detPanel.SetActive(false);
                return;
            }

            if (progress != null && !progress.IsDifficultyUnlocked(database, _difficulty)) _difficulty = StageDifficulty.Easy;
            var chapters = database.Chapters(_difficulty);
            if (chapters.Count > 0 && !chapters.Contains(_chapter)) _chapter = chapters[0];

            int total = 0, max = 0;
            foreach (var s in database.Ordered(_difficulty)) { total += progress != null ? progress.StarsOf(s) : 0; max += 3; }
            _totalStars.text = $"<size=75%><color={Difficulties.Hex(_difficulty)}>{Difficulties.Name(_difficulty)}</color></size>  <color=#FFD36B>★</color> {total} / {max}";
            RefreshStamina();

            if (_dungeonMode)
            {
                _chapterTitle.text = $"<color={SpecialDungeons.Hex(_dungeonKind)}>{SpecialDungeons.Name(_dungeonKind)}</color>  " +
                                     $"<size=60%><color=#AEB6C8>{SpecialDungeons.Summary(_dungeonKind)}</color></size>";
            }
            else
            {
                int chStars = progress != null ? progress.TotalStars(database, _chapter, _difficulty) : 0;
                int chMax = database.InChapter(_chapter, _difficulty).Count * 3;
                string diff = _difficulty == StageDifficulty.Easy ? "" : $"<color={Difficulties.Hex(_difficulty)}>{Difficulties.Name(_difficulty)}</color>  ";
                _chapterTitle.text = $"{diff}챕터 {_chapter}  ·  {database.ChapterName(_chapter)}    <size=70%><color=#FFD36B>★</color> {chStars} / {chMax}</size>";
            }

            if (_starRewardBtn != null)
            {
                _starRewardBtn.gameObject.SetActive(!_dungeonMode);
                _starRewardBadge.Set(!_dungeonMode && ChapterStarRewards.ClaimableIn(database, progress, _difficulty, _chapter) > 0);
            }

            RebuildTabs(chapters);
            RebuildNodes();
            if (_dungeonMode)
            {
                if (_selected == null || _selected.kind != _dungeonKind)
                {
                    var list = database.Dungeons(_dungeonKind);
                    _selected = list.Find(d => progress == null || progress.IsUnlocked(d, database));
                    if (_selected == null && list.Count > 0) _selected = list[0];
                }
            }
            else if (_selected == null || _selected.IsDungeon || _selected.chapter != _chapter || _selected.difficulty != _difficulty)
            {
                var list = database.InChapter(_chapter, _difficulty);
                _selected = list.Count > 0 ? list[0] : null;
            }
            RefreshHighlights();
            RefreshDetail();
        }

        private void RebuildTabs(List<int> chapters)
        {
            Clear(_modeTabs);
            Clear(_tabs);

            // 난이도
            foreach (var d in Difficulties.All)
            {
                var dd = d;
                if (database.Ordered(d).Count == 0) continue;
                bool open = progress == null || progress.IsDifficultyUnlocked(database, d);
                bool on = !_dungeonMode && d == _difficulty;
                var col = Difficulties.ColorOf(d);
                string label = open ? Difficulties.Name(d) : $"{Difficulties.Name(d)} <size=70%>({database.UnlockChapterOf(d)}챕터 클리어)</size>";
                var b = UIFactory.AddButton("Diff", _modeTabs, label, 20, font,
                    on ? new Color(col.r, col.g, col.b, 0.95f) : new Color(col.r, col.g, col.b, open ? 0.3f : 0.1f), Color.white);
                var le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = open ? 140 : 230; le.preferredHeight = 44;
                b.interactable = open;
                b.onClick.AddListener(() =>
                {
                    _dungeonMode = false;
                    if (_difficulty != dd) { _difficulty = dd; _selected = null; JumpToFrontierIn(dd); }
                    Refresh();
                });
            }

            // 두 번째 줄: 메인 = 챕터 번호(1~15), 특수 던전 = 던전 종류
            if (_dungeonMode)
                foreach (var k in SpecialDungeons.All)
                {
                    var kk = k;
                    var ofKind = database.Dungeons(k);
                    if (ofKind.Count == 0) continue;
                    bool open = ofKind.Exists(d => progress == null || progress.IsUnlocked(d, database));
                    bool on = k == _dungeonKind;
                    var col = SpecialDungeons.ColorOf(k);
                    var b = UIFactory.AddButton("Kind", _tabs, open ? SpecialDungeons.Name(k) : $"{SpecialDungeons.Name(k)} <size=70%>(잠김)</size>", 19, font,
                        on ? new Color(col.r, col.g, col.b, 0.95f) : new Color(col.r, col.g, col.b, open ? 0.3f : 0.1f), Color.white);
                    var le = b.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = 210; le.preferredHeight = 40;
                    b.onClick.AddListener(() => { _dungeonKind = kk; _selected = null; Refresh(); });
                }
            if (!_dungeonMode)
                foreach (int ch in chapters)
                {
                    int c = ch;
                    var first = database.InChapter(ch, _difficulty);
                    bool open = first.Count > 0 && (progress == null || progress.IsUnlocked(first[0], database));
                    bool on = ch == _chapter;
                    var cleared = progress != null && progress.IsChapterCleared(database, ch, _difficulty);
                    var b = UIFactory.AddButton("Tab", _tabs, ch.ToString(), 19, font,
                        on ? new Color(0.78f, 0.58f, 0.22f, 0.95f)
                           : (cleared ? new Color(1f, 0.83f, 0.42f, 0.28f) : new Color(1, 1, 1, open ? 0.12f : 0.04f)),
                        open ? Color.white : new Color(0.45f, 0.45f, 0.5f));
                    var le = b.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = 58; le.preferredHeight = 40;
                    b.interactable = open;
                    b.onClick.AddListener(() => { _chapter = c; _selected = null; Refresh(); });
                    if (ChapterStarRewards.ClaimableIn(database, progress, _difficulty, ch) > 0)
                        NotifyBadge.Attach(b, font, 6, 6, 20).Set(true);   // 받을 별 보상이 있는 챕터
                }

            var dungeons = database.Dungeons();
            if (dungeons.Count == 0) return;
            var spacer = UIFactory.NewRect("Spacer", _modeTabs);
            spacer.gameObject.AddComponent<LayoutElement>().preferredWidth = 40;
            int openCount = dungeons.FindAll(d => progress == null || progress.IsUnlocked(d, database)).Count;
            var db = UIFactory.AddButton("DungeonTab", _modeTabs, "특수 던전", 20, font,
                _dungeonMode ? new Color(0.55f, 0.35f, 0.85f, 0.95f) : new Color(0.55f, 0.35f, 0.85f, openCount > 0 ? 0.35f : 0.12f), Color.white);
            var dle = db.gameObject.AddComponent<LayoutElement>();
            dle.preferredWidth = 230; dle.preferredHeight = 44;
            db.onClick.AddListener(() => { _dungeonMode = true; _selected = null; Refresh(); });
        }

        private void RebuildNodes()
        {
            Clear(_nodes);
            _nodeHL.Clear();
            _grid.constraintCount = 3;
            if (_dungeonMode && _dungeonKind == StageKind.EquipmentDungeon)
            {
                // 세트 묶음 5줄 × 1~5단계
                _grid.constraintCount = 5;
                _grid.cellSize = new Vector2(186, 118); _grid.spacing = new Vector2(17, 14);
                foreach (var s in database.Dungeons(_dungeonKind)) BuildNode(s);
            }
            else if (_dungeonMode && _dungeonKind == StageKind.AscensionDungeon)
            {
                // 5속성 × 3등급 → 작은 칸
                _grid.cellSize = new Vector2(300, 118); _grid.spacing = new Vector2(30, 14);
                foreach (var s in database.Dungeons(_dungeonKind)) BuildNode(s);
            }
            else if (_dungeonMode)
            {
                _grid.cellSize = new Vector2(300, 170); _grid.spacing = new Vector2(30, 30);
                foreach (var s in database.Dungeons(_dungeonKind)) BuildNode(s);
            }
            else
            {
                _grid.cellSize = new Vector2(300, 170); _grid.spacing = new Vector2(30, 30);
                foreach (var s in database.InChapter(_chapter, _difficulty)) BuildNode(s);
            }
        }

        private void BuildNode(StageData s)
        {
            bool unlocked = progress == null || progress.IsUnlocked(s, database);
            bool cleared = progress != null && progress.IsCleared(s);
            int stars = progress != null ? progress.StarsOf(s) : 0;

            var node = UIFactory.NewRect("Node", _nodes);

            var hl = UIFactory.AddImage("HL", node, new Color(0.3f, 0.8f, 1f, 0f));
            UIFactory.Stretch(hl.rectTransform, -4, -4, -4, -4);
            _nodeHL[s] = hl;

            Color frameCol = s.isBoss ? new Color(0.85f, 0.32f, 0.36f) : (cleared ? Gold : new Color(0.45f, 0.5f, 0.62f));
            if (s.IsDungeon)
                frameCol = (s.IsAscensionDungeon ? Traits.ColorOf(s.dungeonElement) : SpecialDungeons.ColorOf(s.kind))
                           * new Color(1, 1, 1, cleared ? 1f : 0.75f);
            if (!unlocked) frameCol = new Color(0.25f, 0.26f, 0.3f);
            var frame = UIFactory.AddImage("Frame", node, frameCol, null, raycast: true);
            UIFactory.Stretch(frame.rectTransform);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            var ss = s;
            btn.onClick.AddListener(() => { _selected = ss; RefreshHighlights(); RefreshDetail(); });

            var inner = UIFactory.AddImage("Inner", frame.transform, unlocked ? new Color(0.11f, 0.12f, 0.16f, 1f) : new Color(0.07f, 0.07f, 0.09f, 1f));
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);

            if (s.IsDungeon) { BuildDungeonNodeText(inner.transform, s, unlocked, stars); return; }

            var code = UIFactory.AddText("Code", inner.transform, s.isBoss ? $"{s.Code}  <size=60%><color=#FF8A8A>BOSS</color></size>" : s.Code,
                34, font, unlocked ? Color.white : new Color(0.5f, 0.5f, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);
            var crt = code.rectTransform;
            UIFactory.Anchor(crt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            crt.sizeDelta = new Vector2(0, 44); crt.anchoredPosition = new Vector2(0, -12);

            var nm = UIFactory.AddText("Name", inner.transform, s.stageName, 18, font,
                unlocked ? new Color(0.85f, 0.88f, 0.95f) : new Color(0.45f, 0.45f, 0.5f), TextAlignmentOptions.Center);
            var nrt = nm.rectTransform;
            UIFactory.Anchor(nrt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            nrt.sizeDelta = new Vector2(-12, 26); nrt.anchoredPosition = new Vector2(0, -58);

            string bottom = unlocked ? StageRunner.Stars(stars) : "<color=#6B7280>잠김</color>";
            var st = UIFactory.AddText("Stars", inner.transform, bottom, unlocked ? 26 : 18, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var srt = st.rectTransform;
            UIFactory.Anchor(srt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            srt.sizeDelta = new Vector2(0, 34); srt.anchoredPosition = new Vector2(0, 10);
        }

        /// <summary>던전 칸: '화염 · 초급' / 재료 이름 · N 돌파 / 별 또는 개방 조건.</summary>
        private void BuildDungeonNodeText(Transform inner, StageData s, bool unlocked, int stars)
        {
            var dim = new Color(0.5f, 0.5f, 0.55f);
            bool asc = s.IsAscensionDungeon;
            string head = asc
                ? (unlocked ? $"<color={Traits.Hex(s.dungeonElement)}>{Traits.Name(s.dungeonElement)}</color>" : Traits.Name(s.dungeonElement))
                : (unlocked ? $"<color={SpecialDungeons.Hex(s.kind)}>{s.Code}</color>" : s.Code);   // 종류는 위 탭에 있으므로 단계만
            bool equip = s.kind == StageKind.EquipmentDungeon;
            if (equip && s.equipmentSets != null && s.equipmentSets.Count >= 2)
                head = $"{EquipSets.Short(s.equipmentSets[0])}·{EquipSets.Short(s.equipmentSets[1])}  " + head;
            var top = UIFactory.AddText("Code", inner, asc ? $"{head}  ·  {s.Code}" : head, asc ? 26 : (equip ? 22 : 32), font, unlocked ? Color.white : dim,
                TextAlignmentOptions.Center, FontStyles.Bold);
            top.enableAutoSizing = true; top.fontSizeMin = 14; top.fontSizeMax = asc ? 26 : 32;
            var trt = top.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            trt.sizeDelta = new Vector2(0, 34); trt.anchoredPosition = new Vector2(0, asc ? -8 : -16);

            string sub = asc ? $"{Materials.Name(s.DungeonMaterial)}  <size=80%>· {Materials.TierTargetLevel(s.dungeonTier)} 돌파</size>"
                : s.kind == StageKind.GoldDungeon ? $"금화 {s.gold:N0}  <size=80%>· 권장 Lv.{s.recommendedLevel}</size>"
                : s.kind == StageKind.EquipmentDungeon ? $"최대 {EquipStats.Stars(s.MaxEquipRarity)}  <size=80%>· Lv.{s.recommendedLevel}</size>"
                : s.kind == StageKind.ExpDungeon ? $"캐릭터 EXP +{s.characterExp:N0}  <size=78%><color=#9AA3B5>· 재료 {ExpTotal(s):N0} · 권장 Lv.{s.recommendedLevel}</color></size>"
                : $"교본 ×{BookCount(s)}  <size=80%>· 권장 Lv.{s.recommendedLevel}</size>";
            var mat = UIFactory.AddText("Mat", inner, sub,
                17, font, unlocked ? new Color(0.85f, 0.88f, 0.95f) : new Color(0.45f, 0.45f, 0.5f), TextAlignmentOptions.Center);
            mat.enableAutoSizing = true; mat.fontSizeMin = 12; mat.fontSizeMax = 17;
            var mrt = mat.rectTransform;
            UIFactory.Anchor(mrt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            mrt.sizeDelta = new Vector2(-12, 24); mrt.anchoredPosition = new Vector2(0, asc ? -44 : -62);

            string bottom = unlocked ? StageRunner.Stars(stars)
                : $"<color=#6B7280>{(s.unlockAfter != null ? s.unlockAfter.Code + " 클리어 시 개방" : "잠김")}</color>";
            var st = UIFactory.AddText("Stars", inner, bottom, unlocked ? 22 : 16, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var srt = st.rectTransform;
            UIFactory.Anchor(srt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            srt.sizeDelta = new Vector2(0, 28); srt.anchoredPosition = new Vector2(0, 8);
        }

        private static int BookCount(StageData s)
        {
            int n = 0;
            if (s.rewards != null) foreach (var m in s.rewards) if (m != null && Materials.IsSkillBook(m.type)) n += m.count;
            return n;
        }

        private static int ExpTotal(StageData s)
        {
            int n = 0;
            if (s.rewards != null) foreach (var m in s.rewards) if (m != null) n += m.count * Materials.ExpValue(m.type);
            return n;
        }

        private void RefreshHighlights()
        {
            foreach (var kv in _nodeHL)
                kv.Value.color = new Color(0.3f, 0.8f, 1f, kv.Key == _selected ? 0.9f : 0f);
        }

        private void RefreshDetail()
        {
            bool has = _selected != null;
            _detPanel.SetActive(has);
            if (!has) return;

            var s = _selected;
            bool unlocked = progress == null || progress.IsUnlocked(s, database);
            bool cleared = progress != null && progress.IsCleared(s);
            int best = progress != null ? progress.StarsOf(s) : 0;

            _detTitle.text = s.IsAscensionDungeon
                ? $"<color={Traits.Hex(s.dungeonElement)}>{Traits.Name(s.dungeonElement)}</color> 돌파 던전  <size=70%>{s.Code}</size>"
                : s.IsDungeon
                ? $"<color={SpecialDungeons.Hex(s.kind)}>{SpecialDungeons.Name(s.kind)}</color>  <size=70%>{s.Code}</size>"
                : $"{s.Code}  {s.stageName}" + (s.isBoss ? "  <size=60%><color=#FF8A8A>BOSS</color></size>" : "");

            var sb = new StringBuilder();
            if (s.IsDungeon)
            {
                if (s.IsAscensionDungeon)
                    sb.Append($"<color=#AEB6C8>{Traits.Name(s.dungeonElement)} 속성 캐릭터의 <b>{Materials.TierTargetLevel(s.dungeonTier)} 돌파</b> 재료 ")
                      .Append(Materials.RichName(s.DungeonMaterial)).Append("<color=#AEB6C8> 획득 · 반복 도전 가능</color>\n");
                else
                    sb.Append($"<color=#AEB6C8>{SpecialDungeons.Summary(s.kind)} · 반복 도전 가능</color>\n");
                if (s.kind == StageKind.EquipmentDungeon && s.equipmentRarityWeights != null)
                {
                    int total = 0;
                    foreach (var w in s.equipmentRarityWeights) total += w;
                    var rates = new List<string>();
                    for (int i = 0; i < s.equipmentRarityWeights.Count; i++)
                        if (s.equipmentRarityWeights[i] > 0 && total > 0)
                            rates.Add($"{EquipStats.Stars(3 + i)} {Grades.Tag(3 + i)} {Mathf.RoundToInt(100f * s.equipmentRarityWeights[i] / total)}%");
                    sb.Append("<color=#FFD36B>드롭 성급</color>  ").Append(string.Join("  ·  ", rates))
                      .Append("  <size=80%><color=#9AA3B5>(최대 강화 3★ +9 · 4★ +12 · 5★ +16 · 6★ +20)</color></size>\n");
                }
                if (s.kind == StageKind.EquipmentDungeon && s.equipmentSets != null)
                    foreach (var id in s.equipmentSets)
                    {
                        var set = EquipSets.Get(id);
                        if (set == null) continue;
                        sb.Append($"<b>{EquipSets.Rich(id)}</b>  <size=85%>2세트 {set.desc2} / 4세트 {set.desc4}</size>\n");
                    }
                if (!unlocked && s.unlockAfter != null)
                    sb.Append($"<color=#FF8A8A>개방 조건: 스테이지 {s.unlockAfter.Code} {s.unlockAfter.stageName} 클리어</color>\n");
                sb.Append("\n");
            }
            else if (!string.IsNullOrEmpty(s.description)) sb.Append("<color=#AEB6C8>").Append(s.description).Append("</color>\n\n");

            if (!s.IsDungeon && s.difficulty != StageDifficulty.Easy)
                sb.Append($"<color={Difficulties.Hex(s.difficulty)}>{Difficulties.Name(s.difficulty)} 난이도</color>  " +
                          $"<size=85%>적 능력치 +{Mathf.RoundToInt((s.enemyStatMultiplier - 1f) * 100)}%</size>\n");
            sb.Append($"<color=#FFD36B>권장 레벨</color>  Lv.{s.recommendedLevel}");
            if (cleared) sb.Append($"      <color=#FFD36B>최고 기록</color>  {StageRunner.Stars(best)}  <size=80%>({progress.ClearsOf(s)}회 클리어)</size>");
            sb.Append("\n\n<color=#FFD36B>적 구성</color>\n");
            if (s.enemies != null)
                foreach (var e in s.enemies)
                {
                    if (e == null || e.unit == null) continue;
                    sb.Append("  · ").Append(e.unit.DisplayName).Append($"  Lv.{e.level}");
                    if (e.unit.weaknesses != null && e.unit.weaknesses.Count > 0)
                    {
                        sb.Append("   <size=85%>약점 ");
                        for (int i = 0; i < e.unit.weaknesses.Count; i++)
                        {
                            var w = e.unit.weaknesses[i];
                            if (i > 0) sb.Append(' ');
                            sb.Append($"<color={Traits.Hex(w)}>{Traits.Name(w)}</color>");
                        }
                        sb.Append("</size>");
                    }
                    sb.Append('\n');
                }

            sb.Append("\n<color=#FFD36B>별 조건</color>\n");
            sb.Append($"  ★ 클리어\n  ★ 아군 전원 생존\n  ★ {s.StarRoundLimit}턴 이내 클리어\n");

            sb.Append("\n<color=#FFD36B>보상</color>\n");
            if (s.firstClearCrystals > 0)
                sb.Append(cleared
                    ? $"  <color=#6B7280>첫 클리어  크리스탈 {s.firstClearCrystals}  (획득 완료)</color>\n"
                    : $"  첫 클리어  <color=#8FE9FF>크리스탈 {s.firstClearCrystals}</color>\n");
            var parts = new List<string>();
            if (s.gold > 0) parts.Add($"<color=#FFD36B>금화 {s.gold:N0}</color>");
            if (s.rewards != null)
                foreach (var m in s.rewards)
                    if (m != null && m.count > 0) parts.Add($"{Materials.RichName(m.type)} ×{m.count}");
            if (s.characterExp > 0) parts.Add($"캐릭터 경험치 +{s.characterExp:N0} <size=85%><color=#9AA3B5>(출전 캐릭터마다)</color></size>");
            if (s.equipmentDrops > 0) parts.Insert(0, $"<color=#7FE3D6>장비 ×{s.equipmentDrops}</color>");
            if (parts.Count > 0) sb.Append("  매 클리어  ").Append(string.Join(" · ", parts)).Append('\n');

            if (!s.IsDungeon)
            {
                var opens = database.DungeonsUnlockedBy(s);
                if (opens.Count > 0)
                {
                    // 종류별로 한 줄씩: 돌파 던전은 등급, 금화/교본 던전은 단계
                    var shown = new HashSet<StageKind>();
                    sb.Append("\n<color=#B48CFF>클리어 시 개방</color>");
                    foreach (var d in opens)
                    {
                        if (!shown.Add(d.kind)) continue;
                        sb.Append("\n  · ").Append($"<color={SpecialDungeons.Hex(d.kind)}>{SpecialDungeons.Name(d.kind)}</color> ");
                        sb.Append(d.IsAscensionDungeon
                            ? $"{Materials.TierName(d.dungeonTier)} <size=85%>({Materials.TierTargetLevel(d.dungeonTier)} 돌파 재료 · 전 속성)</size>"
                            : d.Code);
                    }
                    sb.Append("\n");
                }
            }

            _detBody.text = sb.ToString();
            if (_detScroll != null && _detShown != s) _detScroll.verticalNormalizedPosition = 1f;   // 다른 스테이지를 고르면 맨 위부터
            _detShown = s;
            if (_storyBtn != null) _storyBtn.gameObject.SetActive(StageRunner.HasReplay(s, cleared));

            _sortieBtn.interactable = unlocked;
            _sortieLbl.text = unlocked ? "편성 후 출격"
                : (s.IsDungeon && s.unlockAfter != null ? $"{s.unlockAfter.Code} 클리어 시 개방" : "이전 스테이지를 클리어하세요");
            RefreshEntry();
        }

        // ---------------- UI 구성 ----------------

        private void BuildUI()
        {
            var cgo = UIFactory.NewRect("StageCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 어떤 화면비에서도 1920×1080 설계 영역이 전부 보이게(남는 쪽은 여백)
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.05f, 0.06f, 0.09f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            BackgroundArt.Create(_root.transform, 0.7f).Set("Backgrounds/stage");

            var title = UIFactory.AddText("Title", _root.transform, "스테이지", 40, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = title.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            trt.sizeDelta = new Vector2(600, 56); trt.anchoredPosition = new Vector2(0, -30);

            _totalStars = UIFactory.AddText("TotalStars", _root.transform, "", 26, font, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
            var tsrt = _totalStars.rectTransform;
            UIFactory.Anchor(tsrt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            tsrt.sizeDelta = new Vector2(300, 34); tsrt.anchoredPosition = new Vector2(-50, -112);   // 행동력 알약 아래

            // 행동력(오른쪽 위, 다른 화면의 재화 알약과 같은 모양) + 테스트 충전(왼쪽 옆)
            var staminaBar = CurrencyBar.Create(_root.transform, font, wallet, new Vector2(-50, -32), Currency.Stamina);
            staminaBar.AddPlus(Currency.Stamina, font, () => StaminaDialog.Open(font));   // 행동력 물약 사용
            var refill = UIFactory.AddButton("StaminaTest", _root.transform, "행동력 +120 (테스트)", 16, font, new Color(1, 1, 1, 0.12f), Color.white);
            var rfrt = (RectTransform)refill.transform;
            UIFactory.Anchor(rfrt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            rfrt.sizeDelta = new Vector2(196, 40); rfrt.anchoredPosition = new Vector2(-50 - CurrencyBar.PillW - 14, -38);
            refill.onClick.AddListener(() => { if (wallet != null) wallet.AddStamina(120); });
            // 테스트: 쉬움 1 ~ 3장 모든 스테이지를 별 3개로 클리어 처리(결투장 · 주간 보스 · 월드 레이드 · 이벤트 · 파견 · 탑 개방 확인용)
            var clear3 = UIFactory.AddButton("Clear3Test", _root.transform, "3장까지 클리어 (테스트)", 16, font, new Color(1, 1, 1, 0.12f), Color.white);
            var c3rt = (RectTransform)clear3.transform;
            UIFactory.Anchor(c3rt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            c3rt.sizeDelta = new Vector2(220, 40); c3rt.anchoredPosition = new Vector2(-50 - CurrencyBar.PillW - 14 - 196 - 12, -38);
            clear3.onClick.AddListener(TestClearToChapter3);
            DevTools.Gate(refill.gameObject); DevTools.Gate(clear3.gameObject);   // 출시 빌드에서는 없음

            // 위: 난이도 + 돌파 던전 · 아래: 챕터 번호
            _modeTabs = TabRow("ModeTabs", -98, 44, 10);
            _tabs = TabRow("Tabs", -150, 40, 6);

            _chapterTitle = UIFactory.AddText("Chapter", _root.transform, "", 30, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            var chrt = _chapterTitle.rectTransform;
            UIFactory.Anchor(chrt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            chrt.sizeDelta = new Vector2(1000, 44); chrt.anchoredPosition = new Vector2(-380, -200);

            // 챕터 별 보상(별 12 · 15 · 18개) — 받을 것이 있으면 빨간 느낌표
            _starRewardBtn = UIFactory.AddButton("StarReward", _root.transform, "별 보상", 20, font, new Color(0.8f, 0.58f, 0.22f, 0.95f), Color.white);
            var srrt = (RectTransform)_starRewardBtn.transform;
            UIFactory.Anchor(srrt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(1, 1));
            srrt.sizeDelta = new Vector2(150, 44); srrt.anchoredPosition = new Vector2(120, -198);
            _starRewardBtn.onClick.AddListener(() => ChapterRewardWindow.Open(database, progress, _difficulty, _chapter, font, Refresh));
            _starRewardBadge = NotifyBadge.Attach(_starRewardBtn, font, 8, 8, 28);

            // 스테이지 노드(3열)
            // 노드 목록은 스크롤(던전 단계가 많아도 아래로 넘치지 않음)
            var nodesView = UIFactory.NewRect("NodesView", _root.transform);
            UIFactory.Anchor(nodesView, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            nodesView.sizeDelta = new Vector2(1016, 780); nodesView.anchoredPosition = new Vector2(-380, -248);
            _nodes = UIFactory.NewRect("Nodes", nodesView);
            var gl = _nodes.gameObject.AddComponent<GridLayoutGroup>();
            gl.padding = new RectOffset(8, 8, 8, 8);   // 선택 테두리가 잘리지 않게
            UIFactory.MakeVerticalScroll(nodesView, _nodes);
            _grid = gl;
            gl.cellSize = new Vector2(300, 170); gl.spacing = new Vector2(30, 30);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 3;
            gl.childAlignment = TextAnchor.UpperLeft;

            BuildDetail();

            _backBtn = UIFactory.AddButton("BackBtn", _root.transform, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            var bkrt = (RectTransform)_backBtn.transform;
            UIFactory.Anchor(bkrt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            bkrt.sizeDelta = new Vector2(150, 44); bkrt.anchoredPosition = new Vector2(30, -30);
            _backBtn.onClick.AddListener(() => _onBack?.Invoke());
            _backBtn.gameObject.SetActive(false);
        }

        private void BuildDetail()
        {
            var panel = UIFactory.AddImage("Detail", _root.transform, new Color(0.10f, 0.10f, 0.14f, 0.96f));
            _detPanel = panel.gameObject;
            var prt = panel.rectTransform;
            UIFactory.Anchor(prt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            prt.sizeDelta = new Vector2(640, 860); prt.anchoredPosition = new Vector2(560, -40);

            _detTitle = UIFactory.AddText("Title", panel.transform, "", 34, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = _detTitle.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            trt.sizeDelta = new Vector2(-40, 50); trt.anchoredPosition = new Vector2(0, -44);
            _detTitle.enableAutoSizing = true; _detTitle.fontSizeMin = 20; _detTitle.fontSizeMax = 34;

            // 본문은 글자를 줄이지 않고 스크롤(내용이 길면 위아래로 끌어서 봄)
            var bodyView = UIFactory.NewRect("BodyView", panel.transform);
            UIFactory.Stretch(bodyView, 28, 252, 20, 90);
            _detBody = UIFactory.AddText("Body", bodyView, "", 20, font, Color.white, TextAlignmentOptions.TopLeft);
            _detBody.lineSpacing = 6f;
            _detBody.margin = new Vector4(8, 4, 14, 12);
            _detScroll = UIFactory.MakeVerticalScroll(bodyView, _detBody.rectTransform);

            _sortieBtn = UIFactory.AddButton("Sortie", panel.transform, "", 28, font, new Color(0.85f, 0.45f, 0.25f, 0.95f), Color.white);
            TutorialTargets.Set("stage.sortie", _sortieBtn);
            var srt = (RectTransform)_sortieBtn.transform;
            UIFactory.Anchor(srt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            srt.sizeDelta = new Vector2(420, 76); srt.anchoredPosition = new Vector2(0, 30);
            _sortieBtn.onClick.AddListener(() => { if (_selected != null) OnSortie?.Invoke(_selected); });
            _sortieLbl = _sortieBtn.GetComponentInChildren<TextMeshProUGUI>();
            _sortieLbl.richText = true;
            _sortieLbl.enableAutoSizing = true; _sortieLbl.fontSizeMin = 18; _sortieLbl.fontSizeMax = 28;
            _sweep1 = SweepButton(panel.transform, 82, out _sweep1Lbl, () => DoSweep(1));
            _sweepN = SweepButton(panel.transform, 232, out _sweepNLbl, () => DoSweep(_sweepNRuns));

            BuildEntryControls(panel.transform);

            // 스토리 다시 보기(이미 본 스토리가 있는 스테이지만)
            _storyBtn = UIFactory.AddButton("Story", panel.transform, "스토리 다시 보기", 18, font, new Color(0.45f, 0.35f, 0.75f, 0.95f), Color.white);
            var strt2 = (RectTransform)_storyBtn.transform;
            UIFactory.Anchor(strt2, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            strt2.sizeDelta = new Vector2(176, 36); strt2.anchoredPosition = new Vector2(-14, -14);   // 제목 줄 오른쪽 끝
            _storyBtn.onClick.AddListener(() =>
            {
                if (_runner == null) _runner = FindAnyObjectByType<StageRunner>();
                if (_runner != null && _selected != null) _runner.ReplayStory(_selected);
            });
            _storyBtn.gameObject.SetActive(false);

            _detPanel.SetActive(false);
        }

        private RectTransform TabRow(string name, float y, float h, float spacing)
        {
            var rt = UIFactory.NewRect(name, _root.transform);
            UIFactory.Anchor(rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            rt.sizeDelta = new Vector2(1000, h); rt.anchoredPosition = new Vector2(-380, y);
            var hl = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = spacing; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            return rt;
        }

        /// <summary>난이도를 바꿨을 때 그 난이도의 진행 지점으로 이동.</summary>
        private void JumpToFrontierIn(StageDifficulty d)
        {
            StageData target = null;
            foreach (var s in database.Ordered(d))
            {
                if (progress != null && !progress.IsUnlocked(s, database)) break;
                target = s;
                if (progress == null || !progress.IsCleared(s)) break;
            }
            if (target != null) { _chapter = target.chapter; _selected = target; }
            else _chapter = 1;
        }

        private static void Clear(RectTransform rt)
        {
            for (int i = rt.childCount - 1; i >= 0; i--)
            {
                var ch = rt.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            SubcultureRPG.UI.UIInput.AddModule(es);
        }
    }
}
