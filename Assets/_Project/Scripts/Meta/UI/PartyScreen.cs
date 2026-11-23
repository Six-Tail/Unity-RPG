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
        private TextMeshProUGUI _countText;
        private Button _startButton, _backBtn;
        private System.Action _onBack;
        private RosterFilterBar _filterBar;

        /// <summary>'전투 시작'으로 전투에 들어갔을 때.</summary>
        public event System.Action OnLaunched;

        private readonly Vector2 _cell = new(150, 190);
        private readonly Vector2 _spacing = new(12, 12);
        private readonly List<CharacterData> _selected = new();
        private readonly Dictionary<CharacterData, Image> _highlights = new();

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
            if (on) Rebuild();
            if (_root != null) _root.SetActive(on);
        }

        /// <summary>'← 홈' 버튼 동작 지정(null이면 버튼 숨김).</summary>
        public void SetBackAction(System.Action onBack)
        {
            _onBack = onBack;
            if (_backBtn != null) _backBtn.gameObject.SetActive(onBack != null);
        }

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
            scaler.matchWidthOrHeight = 0.5f;
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.04f, 0.05f, 0.08f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);

            var title = UIFactory.AddText("Title", _root.transform, "파티 편성", 40, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = title.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            trt.sizeDelta = new Vector2(600, 60); trt.anchoredPosition = new Vector2(0, -40);

            _grid = UIFactory.NewRect("Grid", _root.transform);
            UIFactory.Anchor(_grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _gridLayout = _grid.gameObject.AddComponent<GridLayoutGroup>();
            _gridLayout.cellSize = _cell;
            _gridLayout.spacing = _spacing;
            _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _gridLayout.constraintCount = 5;
            _gridLayout.childAlignment = TextAnchor.MiddleCenter;
            _grid.anchoredPosition = new Vector2(0, -40);

            // 정렬/필터 바(타이틀 아래)
            _filterBar = RosterFilterBar.Build(_root.transform, new Vector2(0, -112), 1000, font);
            _filterBar.OnChanged += Rebuild;

            // 하단 바: 선택 수 + 전투 시작
            _countText = UIFactory.AddText("Count", _root.transform, "", 24, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var crt = _countText.rectTransform;
            UIFactory.Anchor(crt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            crt.sizeDelta = new Vector2(400, 36); crt.anchoredPosition = new Vector2(0, 130);

            _startButton = UIFactory.AddButton("Start", _root.transform, "전투 시작", 26, font, new Color(0.8f, 0.6f, 0.2f, 0.95f), Color.white);
            var srt = (RectTransform)_startButton.transform;
            UIFactory.Anchor(srt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            srt.sizeDelta = new Vector2(280, 72); srt.anchoredPosition = new Vector2(0, 60);
            _startButton.onClick.AddListener(OnStart);

            // 홈 버튼(좌상단) — 내비게이터가 연결할 때만 표시
            _backBtn = UIFactory.AddButton("BackBtn", _root.transform, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            var bkrt = (RectTransform)_backBtn.transform;
            UIFactory.Anchor(bkrt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            bkrt.sizeDelta = new Vector2(150, 44); bkrt.anchoredPosition = new Vector2(30, -34);
            _backBtn.onClick.AddListener(() => _onBack?.Invoke());
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
            _highlights.Clear();

            var entries = _filterBar != null ? _filterBar.filter.Apply(roster, database) : new List<RosterEntry>();
            foreach (var e in entries) BuildCard(e.character, e.owned);
            if (_filterBar != null) _filterBar.SetCount(entries.Count, roster != null ? roster.Owned.Count : 0);

            int n = _grid.childCount;
            int cols = Mathf.Clamp(n, 1, 5);
            int rows = Mathf.Max(1, Mathf.CeilToInt(n / 5f));
            _grid.sizeDelta = new Vector2(
                cols * _cell.x + (cols - 1) * _spacing.x,
                rows * _cell.y + (rows - 1) * _spacing.y);

            UpdateBottom();
        }

        private void BuildCard(CharacterData c, OwnedCharacter owned)
        {
            var card = UIFactory.NewRect("Card", _grid);
            int star = Transcendence.CurrentStar(c.rarity, owned.star);
            int tr = owned.transcend;
            var starColor = StarText.ColorOf(star);

            var hl = UIFactory.AddImage("HL", card, new Color(0.3f, 0.8f, 1f, 0f));
            UIFactory.Stretch(hl.rectTransform, -4, -4, -4, -4);
            _highlights[c] = hl;

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
            var sprite = c.thumbnail != null ? c.thumbnail : c.portrait;
            portrait.sprite = sprite;
            portrait.enabled = sprite != null;
            portrait.preserveAspect = true;

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

            SetHighlight(c, _selected.Contains(c));
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
            SetHighlight(c, _selected.Contains(c));
            UpdateBottom();
        }

        private void SetHighlight(CharacterData c, bool on)
        {
            if (_highlights.TryGetValue(c, out var hl))
                hl.color = new Color(0.3f, 0.8f, 1f, on ? 0.9f : 0f);
        }

        private void UpdateBottom()
        {
            _countText.text = $"선택 {_selected.Count} / {maxParty}";
            _startButton.interactable = _selected.Count >= 1;
        }

        private void OnStart()
        {
            if (_selected.Count == 0 || launcher == null) return;
            launcher.Launch(new List<CharacterData>(_selected));
            Show(false);
            OnLaunched?.Invoke();
        }

        // ---------------- 유틸 ----------------

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
