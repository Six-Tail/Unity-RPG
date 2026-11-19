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

        private GameObject _root;
        private RectTransform _grid;
        private GridLayoutGroup _gridLayout;
        private TextMeshProUGUI _countText;
        private Button _startButton;

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
            Show(true);
        }

        public void Show(bool on) { if (_root != null) _root.SetActive(on); }

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

            if (roster != null && database != null)
            {
                foreach (var o in roster.Owned)
                {
                    var c = database.GetCharacter(o.id);
                    if (c != null) BuildCard(c, o.count);
                }
            }

            int n = _grid.childCount;
            int cols = Mathf.Clamp(n, 1, 5);
            int rows = Mathf.Max(1, Mathf.CeilToInt(n / 5f));
            _grid.sizeDelta = new Vector2(
                cols * _cell.x + (cols - 1) * _spacing.x,
                rows * _cell.y + (rows - 1) * _spacing.y);

            UpdateBottom();
        }

        private void BuildCard(CharacterData c, int count)
        {
            var card = UIFactory.NewRect("Card", _grid);

            var hl = UIFactory.AddImage("HL", card, new Color(0.3f, 0.8f, 1f, 0f));
            UIFactory.Stretch(hl.rectTransform, -4, -4, -4, -4);
            _highlights[c] = hl;

            var frame = UIFactory.AddImage("Frame", card, RarityColor(c.rarity), null, raycast: true);
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

            var bar = UIFactory.AddImage("Bar", inner.transform, new Color(RarityColor(c.rarity).r, RarityColor(c.rarity).g, RarityColor(c.rarity).b, 0.9f));
            var brt = bar.rectTransform;
            UIFactory.Anchor(brt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            brt.sizeDelta = new Vector2(0, 30); brt.anchoredPosition = Vector2.zero;
            var nameT = UIFactory.AddText("Name", bar.transform, c.displayName, 13, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(nameT.rectTransform);

            var stars = UIFactory.AddText("Stars", inner.transform, new string('★', (int)c.rarity), 13, font, RarityColor(c.rarity), TextAlignmentOptions.TopLeft);
            var strt = stars.rectTransform;
            UIFactory.Anchor(strt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            strt.sizeDelta = new Vector2(0, 20); strt.anchoredPosition = new Vector2(6, -2);

            if (count > 1)
            {
                var badge = UIFactory.AddText("Count", inner.transform, "x" + count, 12, font, new Color(1f, 0.9f, 0.5f), TextAlignmentOptions.TopRight);
                var cbrt = badge.rectTransform;
                UIFactory.Anchor(cbrt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
                cbrt.sizeDelta = new Vector2(44, 20); cbrt.anchoredPosition = new Vector2(-4, -2);
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
        }

        // ---------------- 유틸 ----------------

        private static Color RarityColor(Rarity r) => r switch
        {
            Rarity.R6 => new Color(1f, 0.78f, 0.25f),
            Rarity.R5 => new Color(0.72f, 0.42f, 1f),
            Rarity.R4 => new Color(0.33f, 0.62f, 1f),
            _ => new Color(0.62f, 0.67f, 0.75f),
        };

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
