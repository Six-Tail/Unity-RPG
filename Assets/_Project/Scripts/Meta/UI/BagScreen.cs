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
    /// 가방(인벤토리) 화면. 보유 재화(크리스탈·금화)와 모든 재료를 분류별로 보여 주고,
    /// 재료를 누르면 용도·획득처를 표시합니다. 코드 자동 구성 — inventory/wallet은 비우면 자동 탐색.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Bag Screen")]
    public class BagScreen : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private TMP_FontAsset font;

        private GameObject _root;
        private TextMeshProUGUI _currency, _detName, _detCount, _detDesc;
        private Button _backBtn;
        private System.Action _onBack;
        private MaterialType _selected = MaterialType.ExpSmall;

        private class Cell { public MaterialType type; public Image frame, hl; public TextMeshProUGUI count; public CanvasGroup group; }
        private readonly List<Cell> _cells = new();

        private static readonly Color ExpTint = new(0.30f, 0.55f, 0.70f);
        private static readonly Color BookTint = new(0.50f, 0.36f, 0.78f);

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

        private void Refresh()
        {
            if (_root == null) return;
            _currency.text = wallet == null ? "" :
                $"<color=#8FE9FF>크리스탈  {wallet.Crystals:N0}</color>      <color=#FFD36B>금화  {wallet.Gold:N0}</color>";

            foreach (var c in _cells)
            {
                int n = Count(c.type);
                c.count.text = n > 0 ? $"×{n:N0}" : "<color=#6B7280>0</color>";
                c.group.alpha = n > 0 ? 1f : 0.45f;
                c.hl.color = new Color(0.3f, 0.8f, 1f, c.type == _selected ? 0.9f : 0f);
            }

            _detName.text = Materials.RichName(_selected);
            _detCount.text = $"보유  {Count(_selected):N0}";
            _detDesc.text = Materials.Description(_selected);
        }

        private void Select(MaterialType t)
        {
            _selected = t;
            Refresh();
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
            var root = _root.transform;

            var title = UIFactory.AddText("Title", root, "가방", 40, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(600, 56), new Vector2(0, -30));

            _currency = UIFactory.AddText("Currency", root, "", 26, font, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
            Place(_currency.rectTransform, new Vector2(1, 1), new Vector2(700, 40), new Vector2(-50, -38), new Vector2(1, 1));

            // 왼쪽: 경험치 재료(2×2) · 육성 재료
            Header(root, "경험치 재료", -823, -140);
            for (int i = 0; i < Materials.ExpMaterials.Length; i++)
                AddCell(root, Materials.ExpMaterials[i], ExpTint, new Vector2(-735 + (i % 2) * 190, -190 - (i / 2) * 190), new Vector2(176, 176));

            Header(root, "육성 재료", -823, -590);
            AddCell(root, MaterialType.SkillBook, BookTint, new Vector2(-735, -640), new Vector2(176, 176));

            // 가운데: 돌파 재료 (속성 행 × 등급 열)
            Header(root, "돌파 재료", -325, -140);
            for (int t = 1; t <= Materials.AscensionTiers; t++)
            {
                var lbl = UIFactory.AddText("Tier", root, $"{Materials.TierName(t)}  <size=80%><color=#9AA3B5>{Materials.TierTargetLevel(t)} 돌파</color></size>",
                    18, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                Place(lbl.rectTransform, new Vector2(0.5f, 1), new Vector2(190, 28), new Vector2(-230 + (t - 1) * 200, -184));
            }
            foreach (var e in Traits.AllElements)
            {
                int row = (int)e;
                for (int t = 1; t <= Materials.AscensionTiers; t++)
                    AddCell(root, Materials.Ascension(e, t), Traits.ColorOf(e),
                        new Vector2(-230 + (t - 1) * 200, -220 - row * 128), new Vector2(190, 116), compact: true);
            }

            // 오른쪽: 상세
            var panel = UIFactory.AddImage("Detail", root, new Color(0.10f, 0.10f, 0.14f, 0.96f));
            Place(panel.rectTransform, new Vector2(0.5f, 1), new Vector2(520, 760), new Vector2(640, -140));
            _detName = UIFactory.AddText("Name", panel.transform, "", 32, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_detName.rectTransform, new Vector2(0.5f, 1), new Vector2(480, 48), new Vector2(0, -30));
            _detCount = UIFactory.AddText("Count", panel.transform, "", 24, font, new Color(1f, 0.85f, 0.45f), TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_detCount.rectTransform, new Vector2(0.5f, 1), new Vector2(480, 36), new Vector2(0, -84));
            _detDesc = UIFactory.AddText("Desc", panel.transform, "", 21, font, new Color(0.88f, 0.9f, 0.96f), TextAlignmentOptions.TopLeft);
            UIFactory.Stretch(_detDesc.rectTransform, 32, 30, 32, 140);
            _detDesc.lineSpacing = 8f;

            _backBtn = UIFactory.AddButton("BackBtn", root, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            Place((RectTransform)_backBtn.transform, new Vector2(0, 1), new Vector2(150, 44), new Vector2(30, -30), new Vector2(0, 1));
            _backBtn.onClick.AddListener(() => _onBack?.Invoke());
            _backBtn.gameObject.SetActive(false);
        }

        private Cell AddCell(Transform parent, MaterialType type, Color tint, Vector2 pos, Vector2 size, bool compact = false)
        {
            var holder = UIFactory.NewRect("Item", parent);
            Place(holder, new Vector2(0.5f, 1), size, pos);

            var hl = UIFactory.AddImage("HL", holder, new Color(0.3f, 0.8f, 1f, 0f));
            UIFactory.Stretch(hl.rectTransform, -4, -4, -4, -4);

            var frame = UIFactory.AddImage("Frame", holder, tint, null, raycast: true);
            UIFactory.Stretch(frame.rectTransform);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            btn.onClick.AddListener(() => Select(type));
            var group = frame.gameObject.AddComponent<CanvasGroup>();

            var inner = UIFactory.AddImage("Inner", frame.transform, new Color(0.08f, 0.09f, 0.12f, 0.94f));
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);

            var name = UIFactory.AddText("Name", inner.transform, Materials.Name(type), compact ? 17 : 18, font,
                Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var nrt = name.rectTransform;
            UIFactory.Anchor(nrt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            nrt.sizeDelta = new Vector2(-10, compact ? 30 : 52); nrt.anchoredPosition = new Vector2(0, compact ? -10 : -12);

            if (!compact)
            {
                int exp = Materials.ExpValue(type);
                if (exp > 0)
                {
                    var sub = UIFactory.AddText("Sub", inner.transform, $"+{exp:N0} EXP", 15, font,
                        Color.Lerp(tint, Color.white, 0.5f), TextAlignmentOptions.Center);
                    var srt = sub.rectTransform;
                    UIFactory.Anchor(srt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
                    srt.sizeDelta = new Vector2(-10, 22); srt.anchoredPosition = new Vector2(0, -66);
                }
            }

            var count = UIFactory.AddText("Count", inner.transform, "", compact ? 26 : 32, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var crt = count.rectTransform;
            UIFactory.Anchor(crt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            crt.sizeDelta = new Vector2(0, 40); crt.anchoredPosition = new Vector2(0, compact ? 10 : 14);

            var cell = new Cell { type = type, frame = frame, hl = hl, count = count, group = group };
            _cells.Add(cell);
            return cell;
        }

        /// <summary>구역 제목(왼쪽 끝 x 기준).</summary>
        private void Header(Transform parent, string text, float leftX, float y)
        {
            var t = UIFactory.AddText("Header", parent, text, 26, font, new Color(1f, 0.83f, 0.42f), TextAlignmentOptions.Left, FontStyles.Bold);
            Place(t.rectTransform, new Vector2(0.5f, 1), new Vector2(380, 36), new Vector2(leftX + 190, y));
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
