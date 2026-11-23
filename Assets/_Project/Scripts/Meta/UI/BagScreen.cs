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
    /// 가방(인벤토리) 화면. 위쪽 탭(전체 · 경험치 · 스킬 · 돌파 · 장비 재료)으로 분류하고,
    /// 칸 테두리는 아이템 등급 색(고급 초록 · 희귀 파랑 · 영웅 보라 · 전설 금색). 재료를 누르면 등급·용도·획득처.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Bag Screen")]
    public class BagScreen : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private TMP_FontAsset font;

        private GameObject _root;
        private TextMeshProUGUI _currency, _detName, _detCount, _detDesc, _countLabel;
        private RectTransform _tabs, _content;
        private Button _backBtn;
        private System.Action _onBack;

        private int _tab = -1;   // -1 = 전체, 그 외 Materials.Category
        private MaterialType _selected = MaterialType.ExpSmall;
        private readonly Dictionary<MaterialType, Image> _hl = new();

        private static readonly Materials.Category[] Cats =
            { Materials.Category.Exp, Materials.Category.Skill, Materials.Category.Ascension, Materials.Category.Equipment };

        private void Awake()
        {
            if (inventory == null) inventory = FindAnyObjectByType<PlayerInventory>();
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            BuildUI();
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.OnChanged += Refresh;
            if (wallet != null) wallet.OnChanged += Refresh;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.OnChanged -= Refresh;
            if (wallet != null) wallet.OnChanged -= Refresh;
        }

        private void Start() => Refresh();

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

        private int Count(MaterialType t) => inventory != null ? inventory.Count(t) : 0;

        // ---------------- 갱신 ----------------

        private List<MaterialType> Items()
        {
            var list = new List<MaterialType>();
            foreach (var t in Materials.All())
                if (_tab < 0 || (int)Materials.CategoryOf(t) == _tab) list.Add(t);
            // 분류 순 → (돌파 재료는 속성·등급 순 유지) → 등급 높은 순
            list.Sort((a, b) =>
            {
                var ca = Materials.CategoryOf(a); var cb = Materials.CategoryOf(b);
                if (ca != cb) return ca.CompareTo(cb);
                if (ca == Materials.Category.Ascension) return ((int)a).CompareTo((int)b);
                int ga = Materials.Grade(a), gb = Materials.Grade(b);
                return ga != gb ? gb.CompareTo(ga) : ((int)a).CompareTo((int)b);
            });
            return list;
        }

        private void Refresh()
        {
            if (_root == null) return;
            _currency.text = wallet == null ? "" :
                $"<color=#8FE9FF>크리스탈  {wallet.Crystals:N0}</color>      <color=#FFD36B>금화  {wallet.Gold:N0}</color>";

            RebuildTabs();

            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var ch = _content.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
            _hl.Clear();

            var items = Items();
            if (!items.Contains(_selected) && items.Count > 0) _selected = items[0];
            int owned = 0;
            foreach (var t in items) { BuildCell(t); if (Count(t) > 0) owned++; }
            _countLabel.text = $"{(_tab < 0 ? "전체" : Materials.CategoryName((Materials.Category)_tab))}  <size=80%><color=#9AA3B5>보유 {owned} / {items.Count}종</color></size>";

            int g = Materials.Grade(_selected);
            _detName.text = Materials.RichName(_selected);
            _detCount.text = $"{Grades.Tag(g)}   보유  {Count(_selected):N0}";
            _detDesc.text = Materials.Description(_selected);
        }

        private void RebuildTabs()
        {
            for (int i = _tabs.childCount - 1; i >= 0; i--)
            {
                var ch = _tabs.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
            Tab("전체", -1);
            foreach (var c in Cats) Tab(Materials.CategoryName(c), (int)c);
        }

        private void Tab(string label, int id)
        {
            bool on = _tab == id;
            var b = UIFactory.AddButton("Tab", _tabs, label, 20, font,
                on ? new Color(0.78f, 0.58f, 0.22f, 0.95f) : new Color(1, 1, 1, 0.12f), Color.white);
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = id < 0 ? 110 : 170; le.preferredHeight = 44;
            b.onClick.AddListener(() => { _tab = id; Refresh(); });
        }

        private void BuildCell(MaterialType t)
        {
            int n = Count(t);
            int grade = Materials.Grade(t);
            var gcol = Grades.ColorOf(grade);

            var cell = UIFactory.NewRect("Item", _content);
            var hl = UIFactory.AddImage("HL", cell, new Color(0.3f, 0.85f, 1f, t == _selected ? 1f : 0f));
            UIFactory.Stretch(hl.rectTransform, -4, -4, -4, -4);
            _hl[t] = hl;

            var frame = UIFactory.AddImage("Frame", cell, new Color(gcol.r, gcol.g, gcol.b, n > 0 ? 0.95f : 0.35f), null, raycast: true);
            UIFactory.Stretch(frame.rectTransform);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            btn.onClick.AddListener(() => { _selected = t; Refresh(); });

            var inner = UIFactory.AddImage("Inner", frame.transform, new Color(0.08f, 0.09f, 0.12f, 0.94f));
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);
            var group = inner.gameObject.AddComponent<CanvasGroup>();
            group.alpha = n > 0 ? 1f : 0.45f;

            // 아이콘(Resources/Art/Items/{재료}) — 있으면 가운데에 그림, 없으면 글자만
            var icon = GameArt.MaterialIcon(t);
            if (icon != null)
            {
                var im = UIFactory.AddImage("Icon", inner.transform, Color.white, icon);
                UIFactory.Anchor(im.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
                im.rectTransform.sizeDelta = new Vector2(84, 84); im.rectTransform.anchoredPosition = new Vector2(0, -50);
            }

            Label(inner.transform, Grades.Tag(grade), 14, FontStyles.Bold, new Vector2(0, 1), new Vector2(8, -6), new Vector2(80, 20), TextAlignmentOptions.TopLeft);
            string sub = Materials.IsAscension(t)
                ? $"<color={Traits.Hex(Materials.ElementOf(t))}>{Traits.Name(Materials.ElementOf(t))}</color> · {Materials.TierTargetLevel(Materials.TierOf(t))} 돌파"
                : Materials.ExpValue(t) > 0 ? $"+{Materials.ExpValue(t):N0} EXP" : "";
            var name = Label(inner.transform, Materials.Name(t), 17, FontStyles.Bold, new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(160, icon != null ? 22 : 46), TextAlignmentOptions.Top);
            name.enableAutoSizing = true; name.fontSizeMin = 12; name.fontSizeMax = 17;
            if (sub != "" && icon == null) Label(inner.transform, sub, 13, FontStyles.Normal, new Vector2(0.5f, 1), new Vector2(0, -76), new Vector2(160, 20), TextAlignmentOptions.Top).color = new Color(0.8f, 0.83f, 0.9f);
            var count = Label(inner.transform, n > 0 ? $"×{n:N0}" : "0", 28, FontStyles.Bold, new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(160, 36), TextAlignmentOptions.Bottom);
            count.color = n > 0 ? Color.white : new Color(0.45f, 0.47f, 0.52f);
        }

        // ---------------- UI 구성 ----------------

        private void BuildUI()
        {
            var cgo = UIFactory.NewRect("BagCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 72;
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.05f, 0.06f, 0.09f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            BackgroundArt.Create(_root.transform, 0.72f).Set("Backgrounds/bag");
            var root = _root.transform;

            var title = UIFactory.AddText("Title", root, "가방", 40, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(600, 56), new Vector2(0, -30));

            _currency = UIFactory.AddText("Currency", root, "", 26, font, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
            Place(_currency.rectTransform, new Vector2(1, 1), new Vector2(700, 40), new Vector2(-50, -38), new Vector2(1, 1));

            // 분류 탭
            _tabs = UIFactory.NewRect("Tabs", root);
            Place(_tabs, new Vector2(0, 1), new Vector2(1200, 44), new Vector2(60, -100), new Vector2(0, 1));
            var th = _tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            th.spacing = 10; th.childAlignment = TextAnchor.MiddleLeft;
            th.childControlWidth = true; th.childControlHeight = true;
            th.childForceExpandWidth = false; th.childForceExpandHeight = false;

            _countLabel = UIFactory.AddText("Count", root, "", 22, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(_countLabel.rectTransform, new Vector2(0, 1), new Vector2(800, 32), new Vector2(60, -156), new Vector2(0, 1));

            // 목록(스크롤)
            var view = UIFactory.NewRect("View", root);
            Place(view, new Vector2(0, 1), new Vector2(1240, 860), new Vector2(50, -196), new Vector2(0, 1));
            view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.2f);
            view.gameObject.AddComponent<RectMask2D>();
            _content = UIFactory.NewRect("Content", view);
            UIFactory.Anchor(_content, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            _content.sizeDelta = Vector2.zero; _content.anchoredPosition = Vector2.zero;
            var grid = _content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(180, 180); grid.spacing = new Vector2(18, 18);
            grid.padding = new RectOffset(16, 16, 16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 6;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = _content; scroll.viewport = view;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            // 오른쪽: 상세
            var panel = UIFactory.AddImage("Detail", root, new Color(0.10f, 0.10f, 0.14f, 0.96f));
            Place(panel.rectTransform, new Vector2(1, 1), new Vector2(560, 860), new Vector2(-50, -196), new Vector2(1, 1));
            _detName = UIFactory.AddText("Name", panel.transform, "", 32, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_detName.rectTransform, new Vector2(0.5f, 1), new Vector2(520, 48), new Vector2(0, -30));
            _detCount = UIFactory.AddText("Count", panel.transform, "", 24, font, new Color(1f, 0.85f, 0.45f), TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_detCount.rectTransform, new Vector2(0.5f, 1), new Vector2(520, 36), new Vector2(0, -84));
            _detDesc = UIFactory.AddText("Desc", panel.transform, "", 21, font, new Color(0.88f, 0.9f, 0.96f), TextAlignmentOptions.TopLeft);
            UIFactory.Stretch(_detDesc.rectTransform, 32, 30, 32, 140);
            _detDesc.lineSpacing = 8f;

            _backBtn = UIFactory.AddButton("BackBtn", root, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            Place((RectTransform)_backBtn.transform, new Vector2(0, 1), new Vector2(150, 44), new Vector2(30, -30), new Vector2(0, 1));
            _backBtn.onClick.AddListener(() => _onBack?.Invoke());
            _backBtn.gameObject.SetActive(false);
        }

        private TextMeshProUGUI Label(Transform parent, string text, float size, FontStyles style, Vector2 anchor, Vector2 pos, Vector2 dim, TextAlignmentOptions align)
        {
            var t = UIFactory.AddText("T", parent, text, size, font, Color.white, align, style);
            var rt = t.rectTransform;
            UIFactory.Anchor(rt, anchor, anchor, anchor);
            rt.sizeDelta = dim; rt.anchoredPosition = pos;
            t.richText = true;
            return t;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos, Vector2? pivot = null)
        {
            UIFactory.Anchor(rt, anchor, anchor, pivot ?? new Vector2(0.5f, 1));
            rt.sizeDelta = size; rt.anchoredPosition = pos;
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
