using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 파티 편성 화면. 보유 캐릭터를 카드로 보여주고, 최대 N명을 골라 전투를 시작합니다.
    /// 코드로 자동 구성 — 빈 GameObject에 추가 → roster/database/launcher 연결(비우면 자동 탐색).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Party Screen")]
    public class PartyScreen : MonoBehaviour
    {
        [SerializeField] private PlayerRoster roster;
        [SerializeField] private GameDatabase database;
        [SerializeField] private BattleLauncher launcher;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private int maxParty = 4;
        [SerializeField, Tooltip("시작 시 화면 표시. 허브(내비게이터)에서 쓸 땐 끄세요.")] private bool showOnStart = true;

        private GameObject _root;
        private RectTransform _grid;
        private GridLayoutGroup _gridLayout;
        private TextMeshProUGUI _countText, _titleText, _startLabel, _backLabel;
        private Button _startButton, _backBtn;
        private System.Action _onBack;
        private System.Action<List<CharacterData>> _launchHandler;   // null이면 launcher 기본 전투
        private RosterFilterBar _filterBar;
        private readonly List<TextMeshProUGUI> _presetLabels = new();
        private Button _autoEquipBtn, _saveBtn;
        private PlayerEquipment _equipment;

        /// <summary>'전투 시작'으로 전투에 들어갔을 때.</summary>
        public event System.Action OnLaunched;

        private readonly Vector2 _cell = new(150, 190);
        private readonly Vector2 _spacing = new(12, 12);
        private const int Columns = 7;   // 한 줄 칸 수
        private readonly List<CharacterData> _selected = new();
        // 카드별 선택 표시 요소
        private class CardView
        {
            public RectTransform card;
            public Image glow, wash;
            public GameObject ribbon;
            public TextMeshProUGUI ribbonText;
            public CanvasGroup group;
        }
        private readonly Dictionary<CharacterData, CardView> _cards = new();
        private static readonly Color SelectColor = new(0.25f, 0.85f, 1f);

        private void Awake()
        {
            if (roster == null) roster = FindAnyObjectByType<PlayerRoster>();
            if (launcher == null) launcher = FindAnyObjectByType<BattleLauncher>();
            BuildUI();
        }

        private void OnEnable() { if (roster != null) roster.OnRosterChanged += Rebuild; }
        private void OnDisable() { if (roster != null) roster.OnRosterChanged -= Rebuild; }

        private void Start()
        {
            Rebuild();
            if (_root != null) _root.SetActive(showOnStart);
        }

        public void Show(bool on)
        {
            // 선택이 비어 있으면 직전에 출격한 파티로 채움
            if (on && _selected.Count == 0 && roster != null)
                foreach (var c in PartyPresets.Last(roster)) if (_selected.Count < maxParty) _selected.Add(c);
            if (on) Rebuild();
            if (_root != null) _root.SetActive(on);
        }

        /// <summary>뒤로 버튼 동작/문구 지정(onBack이 null이면 버튼 숨김).</summary>
        public void SetBackAction(System.Action onBack, string label = "← 홈")
        {
            _onBack = onBack;
            if (_backBtn != null) _backBtn.gameObject.SetActive(onBack != null);
            if (_backLabel != null) _backLabel.text = label;
        }

        /// <summary>
        /// 출격 모드(예: 스테이지): 시작 버튼이 handler(선택 파티)를 호출. 제목·버튼 문구 변경.
        /// handler가 null이면 기본 모드(launcher의 기본 적과 전투).
        /// </summary>
        public void SetLaunchMode(System.Action<List<CharacterData>> handler, string title, string startLabel)
        {
            _launchHandler = handler;
            if (_titleText != null) _titleText.text = string.IsNullOrEmpty(title) ? "파티 편성" : title;
            if (_startLabel != null) _startLabel.text = string.IsNullOrEmpty(startLabel) ? "전투 시작" : startLabel;
        }

        /// <summary>기본 모드로 복귀.</summary>
        public void ClearLaunchMode() => SetLaunchMode(null, "파티 편성", "연습 전투 시작");

        // ---------------- UI 구성 ----------------

        private void BuildUI()
        {
            var cgo = UIFactory.NewRect("PartyCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 어떤 화면비에서도 1920×1080 설계 영역이 전부 보이게(남는 쪽은 여백)
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.04f, 0.05f, 0.08f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            BackgroundArt.Create(_root.transform, 0.7f).Set("Backgrounds/party");

            _titleText = UIFactory.AddText("Title", _root.transform, "파티 편성", 40, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = _titleText.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            trt.sizeDelta = new Vector2(1100, 60); trt.anchoredPosition = new Vector2(0, -40);

            // 캐릭터 목록: 필터 바 아래 ~ 하단 바 위 영역에서 스크롤(캐릭터가 많아져도 필터·출격 버튼과 겹치지 않음)
            const int Pad = 16;
            var view = UIFactory.NewRect("GridView", _root.transform);
            UIFactory.Anchor(view, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            view.sizeDelta = new Vector2(Columns * _cell.x + (Columns - 1) * _spacing.x + Pad * 2, 660);
            view.anchoredPosition = new Vector2(0, -238);   // 필터 바(3줄, -112 ~ -228) 아래
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);   // 빈 곳을 잡고 끌 수 있도록
            view.gameObject.AddComponent<RectMask2D>();

            _grid = UIFactory.NewRect("Grid", view);
            UIFactory.Anchor(_grid, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            _grid.sizeDelta = Vector2.zero; _grid.anchoredPosition = Vector2.zero;
            _gridLayout = _grid.gameObject.AddComponent<GridLayoutGroup>();
            _gridLayout.cellSize = _cell;
            _gridLayout.spacing = _spacing;
            _gridLayout.padding = new RectOffset(Pad, Pad, Pad, Pad);   // 선택 테두리·확대가 잘리지 않게
            _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _gridLayout.constraintCount = Columns;
            _gridLayout.childAlignment = TextAnchor.UpperCenter;
            _grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = _grid; scroll.viewport = view;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            // 정렬/필터 바(타이틀 아래)
            _filterBar = RosterFilterBar.Build(_root.transform, new Vector2(0, -112), 1000, font);
            _filterBar.OnChanged += Rebuild;

            // 하단 바: 선택 수 + 전투 시작
            _countText = UIFactory.AddText("Count", _root.transform, "", 24, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var crt = _countText.rectTransform;
            UIFactory.Anchor(crt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            crt.sizeDelta = new Vector2(1200, 36); crt.anchoredPosition = new Vector2(0, 130);

            _startButton = UIFactory.AddButton("Start", _root.transform, "전투 시작", 26, font, new Color(0.8f, 0.6f, 0.2f, 0.95f), Color.white);
            TutorialTargets.Set("party.start", _startButton);
            var srt = (RectTransform)_startButton.transform;
            UIFactory.Anchor(srt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            srt.sizeDelta = new Vector2(280, 72); srt.anchoredPosition = new Vector2(0, 60);
            _startButton.onClick.AddListener(OnStart);
            _startLabel = _startButton.GetComponentInChildren<TextMeshProUGUI>();

            // 프리셋(왼쪽 아래): 1 ~ 5 불러오기 + 저장
            var presetTitle = UIFactory.AddText("PresetTitle", _root.transform, "편성 프리셋", 18, font, new Color(0.8f, 0.83f, 0.9f), TextAlignmentOptions.Left, FontStyles.Bold);
            UIFactory.Anchor(presetTitle.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            presetTitle.rectTransform.sizeDelta = new Vector2(300, 28); presetTitle.rectTransform.anchoredPosition = new Vector2(44, 128);
            for (int i = 0; i < PartyPresets.Slots; i++)
            {
                int slot = i;
                var pb = UIFactory.AddButton("Preset", _root.transform, "", 20, font, new Color(0.3f, 0.42f, 0.62f, 0.95f), Color.white);
                var prt = (RectTransform)pb.transform;
                UIFactory.Anchor(prt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
                prt.sizeDelta = new Vector2(100, 66); prt.anchoredPosition = new Vector2(40 + i * 108, 54);
                var pl = pb.GetComponentInChildren<TextMeshProUGUI>();
                pl.richText = true; pl.lineSpacing = -12f;
                _presetLabels.Add(pl);
                pb.onClick.AddListener(() => LoadPreset(slot));
            }
            _saveBtn = UIFactory.AddButton("SavePreset", _root.transform, "저장", 22, font, new Color(0.25f, 0.6f, 0.55f, 0.96f), Color.white);
            var svrt = (RectTransform)_saveBtn.transform;
            UIFactory.Anchor(svrt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            svrt.sizeDelta = new Vector2(100, 66); svrt.anchoredPosition = new Vector2(40 + PartyPresets.Slots * 108 + 8, 54);
            _saveBtn.onClick.AddListener(OpenSavePreset);

            // 장비 자동 장착(오른쪽 아래)
            _autoEquipBtn = UIFactory.AddButton("AutoEquip", _root.transform, "장비 자동 장착\n<size=65%>선택한 파티</size>", 22, font, new Color(0.55f, 0.4f, 0.75f, 0.96f), Color.white);
            var aert = (RectTransform)_autoEquipBtn.transform;
            UIFactory.Anchor(aert, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
            aert.sizeDelta = new Vector2(250, 72); aert.anchoredPosition = new Vector2(-40, 52);
            var ael = _autoEquipBtn.GetComponentInChildren<TextMeshProUGUI>();
            ael.richText = true; ael.lineSpacing = -14f;
            _autoEquipBtn.onClick.AddListener(AutoEquipParty);

            // 홈 버튼(좌상단) — 내비게이터가 연결할 때만 표시
            _backBtn = UIFactory.AddButton("BackBtn", _root.transform, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            var bkrt = (RectTransform)_backBtn.transform;
            UIFactory.Anchor(bkrt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            bkrt.sizeDelta = new Vector2(150, 44); bkrt.anchoredPosition = new Vector2(30, -34);
            _backBtn.onClick.AddListener(() => _onBack?.Invoke());
            _backLabel = _backBtn.GetComponentInChildren<TextMeshProUGUI>();
            _backBtn.gameObject.SetActive(false);
        }

        // ---------------- 목록 갱신 ----------------

        private void Rebuild()
        {
            // 더 이상 보유하지 않은 선택 제거
            if (roster != null) _selected.RemoveAll(c => !roster.IsOwned(c));

            for (int i = _grid.childCount - 1; i >= 0; i--)
            {
                var ch = _grid.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
            _cards.Clear();

            var entries = _filterBar != null ? _filterBar.filter.Apply(roster, database) : new List<RosterEntry>();
            foreach (var e in entries) BuildCard(e.character, e.owned);
            if (_filterBar != null) _filterBar.SetCount(entries.Count, roster != null ? roster.Owned.Count : 0);

            UpdateBottom();   // 목록 높이는 ContentSizeFitter 가 맞춤(넘치면 스크롤)
        }

        private void BuildCard(CharacterData c, OwnedCharacter owned)
        {
            var card = UIFactory.NewRect("Card", _grid);
            int star = Transcendence.CurrentStar(c.rarity, owned.star);
            int tr = owned.transcend;
            var starColor = StarText.ColorOf(star);

            var view = new CardView { card = card, group = card.gameObject.AddComponent<CanvasGroup>() };
            view.glow = UIFactory.AddImage("Glow", card, new Color(SelectColor.r, SelectColor.g, SelectColor.b, 0f));
            UIFactory.Stretch(view.glow.rectTransform, -8, -8, -8, -8);
            _cards[c] = view;

            var frame = UIFactory.AddImage("Frame", card, StarText.FrameColor(star, tr), null, raycast: true);
            UIFactory.Stretch(frame.rectTransform);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            var cc = c;
            btn.onClick.AddListener(() => ToggleSelect(cc));

            var inner = UIFactory.AddImage("Inner", frame.transform, new Color(0.10f, 0.11f, 0.14f, 1f));
            UIFactory.Stretch(inner.rectTransform, 4, 4, 4, 4);

            var portrait = UIFactory.AddImage("Portrait", inner.transform, new Color(1, 1, 1, 0.12f));
            UIFactory.Stretch(portrait.rectTransform, 6, 34, 6, 6);
            var sprite = GameArt.Thumb(c);
            portrait.sprite = sprite;
            portrait.enabled = sprite != null;
            portrait.preserveAspect = true;
            if (sprite != null)
            {
                portrait.color = Color.white;
                // 그림 위에 겹치는 글자(별·중복 / 속성·역할)가 읽히도록 어두운 띠
                var top = UIFactory.AddImage("TopShade", inner.transform, new Color(0f, 0f, 0f, 0.6f));
                UIFactory.Anchor(top.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
                top.rectTransform.sizeDelta = new Vector2(0, 24); top.rectTransform.anchoredPosition = Vector2.zero;
                var low = UIFactory.AddImage("TraitShade", inner.transform, new Color(0f, 0f, 0f, 0.6f));
                UIFactory.Anchor(low.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
                low.rectTransform.sizeDelta = new Vector2(0, 20); low.rectTransform.anchoredPosition = new Vector2(0, 31);
            }

            var bar = UIFactory.AddImage("Bar", inner.transform, new Color(starColor.r, starColor.g, starColor.b, 0.9f));
            var brt = bar.rectTransform;
            UIFactory.Anchor(brt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            brt.sizeDelta = new Vector2(0, 30); brt.anchoredPosition = Vector2.zero;
            var nameT = UIFactory.AddText("Name", bar.transform, c.displayName, 13, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(nameT.rectTransform);

            var stars = StarRow.Create(inner.transform, 20, TextAnchor.MiddleLeft, font);
            var strt = stars.Rect;
            UIFactory.Anchor(strt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            strt.sizeDelta = new Vector2(-6, 20); strt.anchoredPosition = new Vector2(3, -2);
            stars.Set(star, tr);

            // 속성(색) · 역할
            var traits = UIFactory.AddText("Traits", inner.transform,
                $"<color={Traits.Hex(c.element)}>{Traits.Name(c.element)}</color> · {Traits.Name(c.role)}",
                12, font, new Color(0.88f, 0.88f, 0.92f), TextAlignmentOptions.Center, FontStyles.Bold);
            var trrt = traits.rectTransform;
            UIFactory.Anchor(trrt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            trrt.sizeDelta = new Vector2(0, 16); trrt.anchoredPosition = new Vector2(0, 33);

            if (owned.Duplicates > 0)
            {
                var badge = UIFactory.AddText("Count", inner.transform, "중복 " + owned.Duplicates, 11, font, new Color(1f, 0.9f, 0.5f), TextAlignmentOptions.TopRight);
                var cbrt = badge.rectTransform;
                UIFactory.Anchor(cbrt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
                cbrt.sizeDelta = new Vector2(54, 20); cbrt.anchoredPosition = new Vector2(-4, -20);
            }

            // 선택 시: 초상화 위 하늘색 덮개 + 가운데 '출전 N' 리본
            view.wash = UIFactory.AddImage("Wash", inner.transform, new Color(SelectColor.r, SelectColor.g, SelectColor.b, 0f));
            UIFactory.Stretch(view.wash.rectTransform);
            var ribbon = UIFactory.AddImage("Ribbon", inner.transform, new Color(0.05f, 0.45f, 0.65f, 0.95f));
            var rrt = ribbon.rectTransform;
            UIFactory.Anchor(rrt, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f));
            rrt.sizeDelta = new Vector2(0, 40); rrt.anchoredPosition = new Vector2(0, 14);
            view.ribbonText = UIFactory.AddText("Text", ribbon.transform, "", 22, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(view.ribbonText.rectTransform);
            view.ribbon = ribbon.gameObject;

            RefreshSelection();
        }

        // ---------------- 선택 ----------------

        private void ToggleSelect(CharacterData c)
        {
            if (_selected.Contains(c)) _selected.Remove(c);
            else
            {
                if (_selected.Count >= maxParty) return;
                _selected.Add(c);
            }
            RefreshSelection();
            UpdateBottom();
        }

        /// <summary>선택 표시 갱신: 출전 순서 리본, 두꺼운 테두리, 덮개, 살짝 확대. 파티가 차면 나머지는 흐리게.</summary>
        private void RefreshSelection()
        {
            bool full = _selected.Count >= maxParty;
            foreach (var kv in _cards)
            {
                var v = kv.Value;
                int order = _selected.IndexOf(kv.Key);
                bool on = order >= 0;
                v.glow.color = new Color(SelectColor.r, SelectColor.g, SelectColor.b, on ? 1f : 0f);
                if (v.wash != null) v.wash.color = new Color(SelectColor.r, SelectColor.g, SelectColor.b, on ? 0.22f : 0f);
                if (v.ribbon != null)
                {
                    v.ribbon.SetActive(on);
                    v.ribbonText.text = on ? $"출전 {order + 1}" : "";
                }
                v.card.localScale = on ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
                v.group.alpha = on || !full ? 1f : 0.45f;
            }
        }

        private void UpdateBottom()
        {
            var names = new List<string>();
            foreach (var c in _selected) names.Add(c.displayName);
            _countText.text = _selected.Count == 0 ? $"선택 0 / {maxParty}   <size=80%><color=#9AA3B5>캐릭터를 눌러 파티에 넣으세요</color></size>"
                : $"선택 {_selected.Count} / {maxParty}   <size=80%><color=#7FE3FF>{string.Join(" · ", names)}</color></size>";
            _startButton.interactable = _selected.Count >= 1;
            if (_autoEquipBtn != null) _autoEquipBtn.interactable = _selected.Count >= 1;
            if (_saveBtn != null) _saveBtn.interactable = _selected.Count >= 1;
            for (int i = 0; i < _presetLabels.Count; i++)
            {
                int n = PartyPresets.CountOf(i);
                _presetLabels[i].text = $"<b>{i + 1}</b>\n<size=62%>{(n > 0 ? $"{n}명" : "<color=#9AA3B5>비어 있음</color>")}</size>";
            }
        }

        // ---------------- 프리셋 ----------------

        private void LoadPreset(int slot)
        {
            var party = PartyPresets.Get(slot, roster);
            if (party.Count == 0)
            {
                Popups.Show($"프리셋 {slot + 1}", "비어 있는 프리셋입니다.\n파티를 고른 뒤 '저장'을 눌러 이 칸에 저장하세요.");
                return;
            }
            _selected.Clear();
            foreach (var c in party) if (_selected.Count < maxParty) _selected.Add(c);
            RefreshSelection();
            UpdateBottom();
        }

        private void OpenSavePreset()
        {
            if (_selected.Count == 0) return;
            var w = OverlayWindow.Open("편성 프리셋에 저장", 980, 640, font);
            var names = new List<string>();
            foreach (var c in _selected) names.Add(c.displayName);
            w.Text(w.Panel, $"<color=#9AA3B5>지금 파티</color>  <color=#7FE3FF>{string.Join(" · ", names)}</color>", 22, Color.white,
                TextAlignmentOptions.Left, 36, -86, 900, 34);
            for (int i = 0; i < PartyPresets.Slots; i++)
            {
                int slot = i;
                float y = -136 - i * 96;
                var row = w.Box(w.Panel, new Color(1, 1, 1, 0.06f), 36, y, 908, 84);
                var members = PartyPresets.Get(i, roster);
                var mn = new List<string>();
                foreach (var c in members) mn.Add(c.displayName);
                w.Text(row.transform, $"<b>프리셋 {i + 1}</b>", 24, Color.white, TextAlignmentOptions.Left, 22, -10, 200, 34);
                w.Text(row.transform, mn.Count > 0 ? string.Join(" · ", mn) : "<color=#9AA3B5>비어 있음</color>", 19, new Color(0.85f, 0.88f, 0.94f),
                    TextAlignmentOptions.Left, 22, -46, 640, 30);
                w.Btn(row.transform, mn.Count > 0 ? "덮어쓰기" : "여기에 저장", 20, OverlayWindow.Green, 908 - 200, -14, 180, 56, () =>
                {
                    PartyPresets.Set(slot, _selected);
                    w.Close();
                    UpdateBottom();
                });
            }
        }

        // ---------------- 장비 자동 장착 ----------------

        private void AutoEquipParty()
        {
            if (_selected.Count == 0) return;
            if (_equipment == null) _equipment = FindAnyObjectByType<PlayerEquipment>();
            if (_equipment == null || roster == null) return;
            var lines = new List<string>();
            int total = 0;
            foreach (var c in _selected)   // 출전 순서대로 좋은 장비를 먼저 가져감
            {
                var (changed, before, after) = _equipment.AutoEquip(c, roster);
                total += changed;
                string diff = after > before ? $"<color=#7BE39A>+{after - before:N0}</color>" : "<color=#9AA3B5>그대로</color>";
                lines.Add($"{c.displayName}  <size=85%>{(changed > 0 ? $"{changed}부위 교체" : "바꿀 장비 없음")} · 전투력 {after:N0} ({diff})</size>");
            }
            Popups.Show("장비 자동 장착", (total > 0 ? "" : "<color=#9AA3B5>이미 가장 좋은 장비를 끼고 있습니다</color>\n\n") + string.Join("\n", lines) +
                "\n\n<size=80%><color=#9AA3B5>장착하지 않은 장비와 그 캐릭터가 낀 장비 중에서 고릅니다(다른 캐릭터의 장비는 그대로)</color></size>");
        }

        private void OnStart()
        {
            if (_selected.Count == 0) return;
            var team = new List<CharacterData>(_selected);
            PartyPresets.SetLast(team);   // 다음에 편성 화면을 열면 이 파티로
            if (_launchHandler != null) _launchHandler(team);
            else if (launcher != null) launcher.Launch(team);
            else return;
            Show(false);
            OnLaunched?.Invoke();
        }

        // ---------------- 유틸 ----------------

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            SubcultureRPG.UI.UIInput.AddModule(es);
        }
    }
}
