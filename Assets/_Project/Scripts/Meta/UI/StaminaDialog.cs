using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 행동력 물약 사용 창(어느 화면 위에서나 뜨는 전용 캔버스).
    /// 지금 행동력 · 물약별(소형 +60 · 일반 +120) 보유 수 · 수량 선택 · 사용 후 미리보기.
    /// 최대치(240)를 넘겨 쓸 수 있고(최대 999), 넘은 동안은 자연 회복이 멈춘다는 안내를 함께 보여 줌.
    ///   StaminaDialog.Open(font);                       // 스테이지 행동력 '+'
    ///   StaminaDialog.Open(font, MaterialType.StaminaSmall);   // 가방 '사용'(그 물약을 먼저 선택)
    /// </summary>
    public class StaminaDialog : MonoBehaviour
    {
        private TMP_FontAsset _font;
        private PlayerWallet _wallet;
        private PlayerInventory _inventory;
        private GameObject _root;
        private TextMeshProUGUI _now, _after, _note, _toast;

        private class Row
        {
            public MaterialType type;
            public int qty;
            public Image bg;
            public TextMeshProUGUI owned, qtyText;
            public Button use;
        }

        private readonly List<Row> _rows = new();
        private Row _sel;

        public static StaminaDialog Open(TMP_FontAsset font, MaterialType? select = null)
        {
            var d = FindAnyObjectByType<StaminaDialog>();
            if (d == null) d = new GameObject("StaminaDialog").AddComponent<StaminaDialog>();
            if (d._font == null) d._font = font;
            d.Build();
            d.Show(select);
            return d;
        }

        private void Build()
        {
            if (_root != null) return;
            _wallet = FindAnyObjectByType<PlayerWallet>();
            _inventory = FindAnyObjectByType<PlayerInventory>();

            var cgo = UIFactory.NewRect("StaminaCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 115;
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<StandaloneInputModule>();
            }

            var dim = UIFactory.AddImage("Dim", canvas.transform, new Color(0, 0, 0, 0.72f), null, raycast: true);
            _root = dim.gameObject;
            UIFactory.Stretch(dim.rectTransform);
            var close = dim.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(Hide);

            var panel = UIFactory.AddImage("Panel", dim.transform, new Color(0.09f, 0.1f, 0.15f, 0.98f), ProceduralSprites.Rounded, raycast: true);
            panel.type = Image.Type.Sliced; panel.preserveAspect = false;
            var prt = panel.rectTransform;
            UIFactory.Anchor(prt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            prt.sizeDelta = new Vector2(900, 620);

            var title = Text(prt, "행동력 보충", 38, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            Place(title.rectTransform, new Vector2(40, -28), new Vector2(500, 52));

            var x = UIFactory.AddButton("Close", prt, "닫기", 22, _font, new Color(1, 1, 1, 0.14f), Color.white);
            var xr = (RectTransform)x.transform;
            UIFactory.Anchor(xr, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            xr.sizeDelta = new Vector2(130, 50); xr.anchoredPosition = new Vector2(-28, -28);
            x.onClick.AddListener(Hide);

            _now = Text(prt, "", 30, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            _now.richText = true;
            Place(_now.rectTransform, new Vector2(40, -96), new Vector2(820, 44));

            float y = -160;
            foreach (var t in Materials.StaminaPotions) { BuildRow(prt, t, y); y -= 130; }

            _after = Text(prt, "", 26, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            _after.richText = true;
            Place(_after.rectTransform, new Vector2(40, y - 10), new Vector2(820, 40));
            _note = Text(prt, "", 19, new Color(0.7f, 0.74f, 0.82f), FontStyles.Normal, TextAlignmentOptions.Left);
            _note.richText = true;
            Place(_note.rectTransform, new Vector2(40, y - 54), new Vector2(820, 56));
            _toast = Text(prt, "", 22, new Color(0.55f, 1f, 0.7f), FontStyles.Bold, TextAlignmentOptions.Center);
            UIFactory.Anchor(_toast.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            _toast.rectTransform.sizeDelta = new Vector2(820, 34); _toast.rectTransform.anchoredPosition = new Vector2(0, 22);

            _root.SetActive(false);
        }

        private void BuildRow(RectTransform panel, MaterialType type, float y)
        {
            var row = new Row { type = type, qty = 1 };
            var bg = UIFactory.AddImage("Row", panel, new Color(1, 1, 1, 0.06f), ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            Place(bg.rectTransform, new Vector2(30, y), new Vector2(840, 116));
            row.bg = bg;
            var pick = bg.gameObject.AddComponent<Button>();
            pick.transition = Selectable.Transition.None;
            pick.onClick.AddListener(() => { _sel = row; Refresh(); });

            var icon = GameArt.MaterialIcon(type);
            if (icon != null)
            {
                var im = UIFactory.AddImage("Icon", bg.rectTransform, Color.white, icon);
                im.preserveAspect = true;
                Place(im.rectTransform, new Vector2(14, -10), new Vector2(96, 96));
            }
            var name = Text(bg.rectTransform, $"{Materials.RichName(type)}\n<size=80%><color=#7CFFB2>행동력 +{Materials.StaminaValue(type)}</color></size>", 24, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            name.richText = true;
            Place(name.rectTransform, new Vector2(124, -14), new Vector2(300, 64));
            row.owned = Text(bg.rectTransform, "", 19, new Color(0.75f, 0.78f, 0.86f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(row.owned.rectTransform, new Vector2(124, -78), new Vector2(300, 28));

            // 수량 - n +  [사용]
            Step(bg.rectTransform, "-", new Vector2(440, -32), () => { row.qty = Mathf.Max(1, row.qty - 1); _sel = row; Refresh(); });
            row.qtyText = Text(bg.rectTransform, "1", 28, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Place(row.qtyText.rectTransform, new Vector2(496, -32), new Vector2(70, 52));
            Step(bg.rectTransform, "+", new Vector2(568, -32), () => { row.qty = Mathf.Min(MaxQty(row), row.qty + 1); _sel = row; Refresh(); });
            row.use = UIFactory.AddButton("Use", bg.rectTransform, "사용", 24, _font, new Color(0.25f, 0.6f, 0.45f, 0.95f), Color.white);
            Place((RectTransform)row.use.transform, new Vector2(650, -32), new Vector2(170, 52));
            row.use.onClick.AddListener(() => Use(row));
            _rows.Add(row);
        }

        private void Step(RectTransform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIFactory.AddButton("Step", parent, label, 28, _font, new Color(1, 1, 1, 0.14f), Color.white);
            Place((RectTransform)b.transform, pos, new Vector2(52, 52));
            b.onClick.AddListener(onClick);
        }

        private int Owned(MaterialType t) => _inventory != null ? _inventory.Count(t) : 0;

        /// <summary>이 물약을 최대 몇 개 쓸 수 있나(보유 수 · 행동력 999 한도).</summary>
        private int MaxQty(Row r)
        {
            int cur = _wallet != null ? _wallet.Stamina : 0;
            int room = Mathf.Max(0, PlayerWallet.StaminaHardCap - cur);
            return Mathf.Max(1, Mathf.Min(Owned(r.type), room / Mathf.Max(1, Materials.StaminaValue(r.type))));
        }

        private void Show(MaterialType? select)
        {
            foreach (var r in _rows) r.qty = 1;
            _sel = _rows.Find(r => select.HasValue && r.type == select.Value) ?? _rows.Find(r => Owned(r.type) > 0) ?? _rows[0];
            _toast.text = "";
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            Refresh();
        }

        private void Hide() { if (_root != null) _root.SetActive(false); }

        private void Refresh()
        {
            int cur = _wallet != null ? _wallet.Stamina : 0, max = _wallet != null ? _wallet.MaxStamina : 240;
            _now.text = $"<color=#7CFFB2>현재 행동력</color>  {(cur > max ? $"<color=#FFD36B>{cur}</color>" : cur.ToString())} / {max}";
            foreach (var r in _rows)
            {
                int own = Owned(r.type);
                r.qty = Mathf.Clamp(r.qty, 1, MaxQty(r));
                r.owned.text = $"보유 {own:N0}개";
                r.qtyText.text = r.qty.ToString();
                r.use.interactable = own >= r.qty && own > 0 && cur + Materials.StaminaValue(r.type) <= PlayerWallet.StaminaHardCap;
                r.bg.color = r == _sel ? new Color(0.3f, 0.55f, 0.45f, 0.35f) : new Color(1, 1, 1, 0.06f);
            }
            if (_sel != null)
            {
                int add = Materials.StaminaValue(_sel.type) * _sel.qty;
                int after = Mathf.Min(PlayerWallet.StaminaHardCap, cur + add);
                _after.text = $"사용 후  {(after > max ? $"<color=#FFD36B>{after}</color>" : after.ToString())} / {max}   <size=80%><color=#9AA3B5>({Materials.Name(_sel.type)} ×{_sel.qty} · +{add})</color></size>";
                _note.text = after > max
                    ? $"<color=#FFD36B>최대치를 {after - max} 넘게 됩니다.</color> 넘은 동안은 6분마다 오르는 자연 회복이 멈추고, {max} 아래로 쓰면 다시 시작됩니다."
                    : "남는 물약은 가방 '기타' 탭에서 볼 수 있습니다.";
            }
        }

        private void Use(Row r)
        {
            if (_wallet == null || _inventory == null) return;
            int qty = Mathf.Min(r.qty, MaxQty(r));
            if (qty <= 0 || !_inventory.Spend(r.type, qty)) { _toast.text = "<color=#FF8A8A>물약이 부족합니다</color>"; Refresh(); return; }
            int add = Materials.StaminaValue(r.type) * qty;
            _wallet.AddStamina(add);
            _toast.text = $"행동력 +{add}!";
            r.qty = 1;
            Refresh();
        }

        // ---------------- 도우미 ----------------

        private TextMeshProUGUI Text(Transform parent, string s, float size, Color c, FontStyles style, TextAlignmentOptions align)
        {
            var t = UIFactory.AddText("T", parent, s, size, _font, c, align, style);
            t.enableAutoSizing = true; t.fontSizeMin = Mathf.Max(12, size * 0.6f); t.fontSizeMax = size;
            return t;
        }

        /// <summary>왼쪽 위 기준 배치.</summary>
        private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            UIFactory.Anchor(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            rt.sizeDelta = size; rt.anchoredPosition = pos;
        }
    }
}
