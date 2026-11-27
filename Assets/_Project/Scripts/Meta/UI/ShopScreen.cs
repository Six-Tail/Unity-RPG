using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Meta.Shop;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 상점 화면. 왼쪽 세로 탭(패키지 · 월정액 · 일반 · 크리스탈 · 길드 · 교환소, 탭 아이콘 Items/ShopTab*).
    /// 일반 · 크리스탈 · 길드 · 교환소 = 상품 칸(테두리 = 등급 색) + 오른쪽 상세 · 수량 · 구매.
    /// 크리스탈 충전 · 패키지 = 넓은 카드(인앱 결제 가격 버튼), 월정액 = 세로 카드 4장(바로 · 매일 보상 · 남은 기간) — 카드에서 바로 구매.
    /// 크리스탈 구매는 확인 창을 거칩니다. 코드로 자동 구성 — 참조는 비우면 자동 탐색(PlayerShop이 없으면 지갑 오브젝트에 추가).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Shop Screen")]
    public class ShopScreen : MonoBehaviour
    {
        [SerializeField] private PlayerShop shop;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private TMP_FontAsset font;

        private GameObject _root, _confirmRoot;
        private TextMeshProUGUI _hint, _detName, _detSub, _detDesc, _detLimit, _qtyText, _total, _toast;
        private TextMeshProUGUI _confirmBody, _confirmTitle;
        private Action _confirmOk;
        private RectTransform _tabs, _content, _view;
        private GridLayoutGroup _grid;
        private GameObject _detailPanel;
        private Button _backBtn, _buyBtn;
        private TextMeshProUGUI _buyLabel;
        private Action _onBack;

        private ShopTab _tab = ShopTab.Package;
        private ShopItem _selected;
        private int _qty = 1;
        private float _timerAcc;
        private string _periodSeen;
        private Coroutine _toastCo;

        private static readonly ShopTab[] Tabs = { ShopTab.TopUp, ShopTab.Package, ShopTab.Monthly, ShopTab.General, ShopTab.Crystal, ShopTab.Guild, ShopTab.Exchange };

        private static string TabIcon(ShopTab t) => "Items/ShopTab" + t;
        private bool Wide => _tab == ShopTab.Package || _tab == ShopTab.Monthly || _tab == ShopTab.TopUp;

        private void Awake()
        {
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            if (inventory == null) inventory = FindAnyObjectByType<PlayerInventory>();
            if (shop == null) shop = FindAnyObjectByType<PlayerShop>();
            if (shop == null)
            {
                var host = wallet != null ? wallet.gameObject : gameObject;
                shop = host.AddComponent<PlayerShop>();
                shop.Bind(wallet, inventory);
            }
            BuildUI();
        }

        private void OnEnable()
        {
            if (shop != null) shop.OnChanged += Refresh;
            if (wallet != null) wallet.OnChanged += Refresh;
            if (inventory != null) inventory.OnChanged += Refresh;
            MonthlyPass.Instance.OnChanged += Refresh;
        }

        private void OnDisable()
        {
            if (shop != null) shop.OnChanged -= Refresh;
            if (wallet != null) wallet.OnChanged -= Refresh;
            if (inventory != null) inventory.OnChanged -= Refresh;
            if (FindAnyObjectByType<MonthlyPass>() is MonthlyPass mp) mp.OnChanged -= Refresh;
        }

        private void Start() => Refresh();

        /// <summary>코드로 생성할 때 폰트 지정(Awake 전, 비활성 상태에서 호출).</summary>
        public void SetFont(TMP_FontAsset f) { if (f != null) font = f; }

        public void Show(bool on)
        {
            if (_root != null) _root.SetActive(on);
            if (!on && _confirmRoot != null) _confirmRoot.SetActive(false);
            if (on) { _qty = 1; Refresh(); }
        }

        /// <summary>지정 탭(+ 그 재료를 주는 상품 선택)으로 열 준비 — 가방 '바로 가기'. 이어서 Show(true).</summary>
        public void Focus(ShopTab tab, MaterialType? reward = null)
        {
            _tab = tab;
            _selected = null;
            if (reward.HasValue)
                foreach (var it in ShopCatalog.Of(tab))
                    if (!it.IsBundle && !it.IsGold && !it.rewardTicket && it.reward == reward.Value) { _selected = it; break; }
        }

        public void SetBackAction(Action onBack)
        {
            _onBack = onBack;
            if (_backBtn != null) _backBtn.gameObject.SetActive(onBack != null);
        }

        private void Update()
        {
            if (_root == null || !_root.activeSelf) return;
            _timerAcc += Time.unscaledDeltaTime;
            if (_timerAcc < 1f) return;
            _timerAcc = 0f;
            // 초기화 시각을 넘기면 제한 표시 갱신
            if (PlayerShop.PeriodKey(ShopReset.Daily, DateTime.Now) != _periodSeen) Refresh();
            else UpdateHint();
        }

        private int Count(MaterialType t) => inventory != null ? inventory.Count(t) : 0;

        // ---------------- 갱신 ----------------

        private void Refresh()
        {
            if (_root == null || shop == null) return;
            _periodSeen = PlayerShop.PeriodKey(ShopReset.Daily, DateTime.Now);

            RebuildTabs();

            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var ch = _content.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }

            // 탭에 따라 배치: 넓은 카드(패키지 · 월정액, 상세 없음) / 상품 칸 3열 + 오른쪽 상세
            bool wide = Wide;
            _detailPanel.SetActive(!wide);
            _view.sizeDelta = new Vector2(wide ? 1610 : 1060, _view.sizeDelta.y);
            if (_tab == ShopTab.Package) { _grid.cellSize = new Vector2(780, 300); _grid.constraintCount = 2; }
            else if (_tab == ShopTab.Monthly) { _grid.cellSize = new Vector2(380, 740); _grid.constraintCount = 4; }
            else if (_tab == ShopTab.TopUp) { _grid.cellSize = new Vector2(515, 360); _grid.constraintCount = 3; }
            else { _grid.cellSize = new Vector2(330, 250); _grid.constraintCount = 3; }

            if (_tab == ShopTab.Monthly)
            {
                foreach (var p in MonthlyPass.Passes) BuildPassCard(p);
            }
            else if (_tab == ShopTab.TopUp)
            {
                foreach (var t in IapCatalog.TopUps) BuildTopUpCard(t);
            }
            else
            {
                var items = ShopCatalog.Of(_tab);
                if (_selected == null || _selected.tab != _tab) { _selected = items.Count > 0 ? items[0] : null; _qty = 1; }
                foreach (var it in items) { if (_tab == ShopTab.Package) BuildPackageCard(it); else BuildCell(it); }
            }

            UpdateHint();
            if (!wide) RefreshDetail();
        }

        private void UpdateHint()
        {
            string hint = ShopCatalog.TabHint(_tab);
            var left = PlayerShop.UntilReset(ShopCatalog.TabReset(_tab), DateTime.Now);
            if (left.HasValue) hint += $"   <color=#FFD36B>초기화까지 {PlayerShop.FormatSpan(left.Value)}</color>";
            if ((_tab == ShopTab.TopUp || _tab == ShopTab.Package || _tab == ShopTab.Monthly) && IapStore.Instance.Mode == IapStore.StoreMode.Unavailable)
                hint += $"   <color=#FF8A8A>{IapStore.Instance.UnavailableReason.Replace("\n", " ")}</color>";
            else if ((_tab == ShopTab.TopUp || _tab == ShopTab.Package || _tab == ShopTab.Monthly) && IapStore.Instance.Mode == IapStore.StoreMode.TestGrant)
                hint += "   <color=#FFD36B>[테스트 결제 — 결제 없이 바로 지급(개발 환경)]</color>";
            if (_tab == ShopTab.Guild)
                hint += PlayerGuild.Instance.Joined ? $"   <color=#FFD98A>보유 길드 코인 {Count(MaterialType.GuildCoin):N0}</color>" : "   <color=#FF8A8A>길드에 가입해야 이용할 수 있습니다</color>";
            _hint.text = $"{ShopCatalog.TabName(_tab)}  <size=80%><color=#9AA3B5>{hint}</color></size>";
        }

        private void RefreshDetail()
        {
            var it = _selected;
            if (it == null)
            {
                _detName.text = ""; _detSub.text = ""; _detDesc.text = ""; _detLimit.text = "";
                _qtyText.text = ""; _total.text = "";
                _buyBtn.interactable = false; _buyLabel.text = "구매";
                return;
            }

            int max = shop.MaxBuyable(it);
            _qty = Mathf.Clamp(_qty, 1, Mathf.Max(1, max));

            _detName.text = $"{it.RewardRichName}  <size=75%>×{it.amount:N0}</size>";
            _detSub.text = $"{Grades.Tag(it.Grade)}   1회  {it.PriceText()}";

            _detDesc.text = Describe(it);
            _detLimit.text = LimitText(it, detailed: true);

            _qtyText.text = $"{_qty}";
            string got = it.GotText(_qty);
            _total.text = $"획득  {got}\n가격  {it.PriceText(_qty)}";

            string block = shop.BlockReason(it, _qty);
            _buyBtn.interactable = block == null;
            _buyLabel.text = block == null ? (it.currency == ShopCurrency.Material ? "교환" : "구매") : $"<size=75%>{block}</size>";
        }

        private string Describe(ShopItem it)
        {
            var sb = new System.Text.StringBuilder();
            if (it.IsTicket)
                sb.Append($"<color=#FFD36B>용도</color>\n{(it.ticketType == SubcultureRPG.Gacha.GachaBannerType.Pickup ? "픽업" : "상시")} 뽑기 1회 (크리스탈 대신 사용)\n\n");
            else if (it.IsGold)
                sb.Append("<color=#FFD36B>용도</color>\n레벨업 · 돌파 · 스킬 · 합성/초월 · 장비 강화에 쓰는 공용 재화\n\n");
            else
            {
                // 재료 설명에서 '획득처' 앞까지만(상점 화면이므로 용도 위주)
                string d = Materials.Description(it.reward);
                int cut = d.IndexOf("<color=#FFD36B>획득처", StringComparison.Ordinal);
                sb.Append(cut > 0 ? d.Substring(0, cut).TrimEnd('\n') : d).Append("\n\n");
            }
            sb.Append("<color=#FFD36B>보유</color>\n");
            sb.Append(it.IsTicket ? $"{it.RewardName} {(wallet != null ? wallet.Tickets(it.ticketType) : 0):N0}"
                : it.IsGold ? $"금화 {(wallet != null ? wallet.Gold : 0):N0}" : $"{Materials.Name(it.reward)} {Count(it.reward):N0}");
            if (it.currency == ShopCurrency.Material)
                sb.Append($"\n{Materials.Name(it.costMaterial)} {Count(it.costMaterial):N0}  <color=#9AA3B5>(교환 재료)</color>");
            return sb.ToString();
        }

        private string LimitText(ShopItem it, bool detailed)
        {
            if (it.limit <= 0) return detailed ? "<color=#9AA3B5>구매 제한 없음</color>" : "무제한";
            string per = it.reset == ShopReset.Daily ? "오늘" : it.reset == ShopReset.Weekly ? "이번 주" : "총";
            int left = shop.Remaining(it);
            string col = left > 0 ? "#FFFFFF" : "#FF8A8A";
            return detailed
                ? $"{per} 남은 구매  <color={col}><b>{left}</b></color> / {it.limit}"
                : $"{per} <color={col}>{left}/{it.limit}</color>";
        }

        // ---------------- 탭 · 칸 ----------------

        private void RebuildTabs()
        {
            for (int i = _tabs.childCount - 1; i >= 0; i--)
            {
                var ch = _tabs.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
            foreach (var t in Tabs)
            {
                bool on = _tab == t;
                var tab = t;
                var b = UIFactory.AddButton("Tab", _tabs, "", 23, font,
                    on ? new Color(0.78f, 0.58f, 0.22f, 0.95f) : new Color(1, 1, 1, 0.12f), Color.white);
                var le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 210; le.preferredHeight = 78;
                b.onClick.AddListener(() => { _tab = tab; Refresh(); });
                // 아이콘 + 이름(아이콘이 없으면 이름만 가운데)
                var lbl = b.GetComponentInChildren<TextMeshProUGUI>();
                var ic = GameArt.Load(TabIcon(t));
                if (ic != null)
                {
                    var im = UIFactory.AddImage("Icon", b.transform, Color.white, ic);
                    im.preserveAspect = true; im.raycastTarget = false;
                    UIFactory.Anchor(im.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                    im.rectTransform.sizeDelta = new Vector2(60, 60); im.rectTransform.anchoredPosition = new Vector2(8, 0);
                    UIFactory.Stretch(lbl.rectTransform, 70, 0, 6, 0);
                    lbl.alignment = TextAlignmentOptions.Left;
                }
                lbl.text = ShopCatalog.TabName(t);
                lbl.enableAutoSizing = true; lbl.fontSizeMin = 16; lbl.fontSizeMax = 23;
                // 월정액: 쓰는 중인 것 수
                if (t == ShopTab.Monthly)
                {
                    int active = 0;
                    foreach (var p in MonthlyPass.Passes) if (MonthlyPass.Instance.Active(p)) active++;
                    if (active > 0) lbl.text += $"\n<size=70%><color=#7CFF9A>사용 중 {active}</color></size>";
                    lbl.richText = true;
                }
            }
        }

        private void BuildCell(ShopItem it)
        {
            int left = shop.Remaining(it);
            bool soldOut = left == 0;
            bool afford = shop.Affordable(it) > 0;
            var gcol = Grades.ColorOf(it.Grade);

            var cell = UIFactory.NewRect("Item", _content);
            var hl = UIFactory.AddImage("HL", cell, new Color(0.3f, 0.85f, 1f, it == _selected ? 1f : 0f));
            UIFactory.Stretch(hl.rectTransform, -4, -4, -4, -4);

            var frame = UIFactory.AddImage("Frame", cell, new Color(gcol.r, gcol.g, gcol.b, soldOut ? 0.35f : 0.95f), null, raycast: true);
            UIFactory.Stretch(frame.rectTransform);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            btn.onClick.AddListener(() => { if (_selected != it) { _selected = it; _qty = 1; } Refresh(); });

            var inner = UIFactory.AddImage("Inner", frame.transform, new Color(0.08f, 0.09f, 0.12f, 0.94f));
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);
            var group = inner.gameObject.AddComponent<CanvasGroup>();
            group.alpha = soldOut ? 0.45f : 1f;

            // 아이콘(Resources/Art/Items/{재료} · 금화는 Items/Gold) — 있으면 왼쪽에 그림, 수량은 오른쪽
            var icon = GameArt.Load(it.IconPath);
            if (icon != null)
            {
                var im = UIFactory.AddImage("Icon", inner.transform, Color.white, icon);
                UIFactory.Anchor(im.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                im.rectTransform.sizeDelta = new Vector2(88, 88); im.rectTransform.anchoredPosition = new Vector2(-58, -4);
            }
            float tx = icon != null ? 60 : 0;   // 아이콘이 있으면 수량·보유 표시를 오른쪽으로

            Label(inner.transform, Grades.Tag(it.Grade), 14, FontStyles.Bold, new Vector2(0, 1), new Vector2(10, -8), new Vector2(90, 20), TextAlignmentOptions.TopLeft);
            Label(inner.transform, LimitText(it, detailed: false), 14, FontStyles.Bold, new Vector2(1, 1), new Vector2(-10, -8), new Vector2(150, 20), TextAlignmentOptions.TopRight);

            var name = Label(inner.transform, it.RewardName, 21, FontStyles.Bold, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(300, icon != null ? 28 : 52), TextAlignmentOptions.Top);
            name.enableAutoSizing = true; name.fontSizeMin = 13; name.fontSizeMax = 20;
            if (it.IsGold) name.color = new Color(1f, 0.83f, 0.42f);

            Label(inner.transform, $"×{it.amount:N0}", 34, FontStyles.Bold, new Vector2(0.5f, 0.5f), new Vector2(tx, icon != null ? -2 : 0), new Vector2(icon != null ? 130 : 250, 44), TextAlignmentOptions.Center);

            if (it.currency == ShopCurrency.Material)
            {
                var src = Label(inner.transform, $"보유 {Count(it.costMaterial):N0}", 13, FontStyles.Normal, new Vector2(0.5f, 0.5f), new Vector2(tx, -34), new Vector2(icon != null ? 130 : 250, 20), TextAlignmentOptions.Center);
                src.color = new Color(0.75f, 0.78f, 0.85f);
            }

            // 가격 띠
            var bar = UIFactory.AddImage("Price", inner.transform, new Color(1, 1, 1, 0.07f));
            var brt = bar.rectTransform;
            UIFactory.Anchor(brt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            brt.sizeDelta = new Vector2(0, 46); brt.anchoredPosition = Vector2.zero;
            var price = Label(bar.transform, soldOut ? "<color=#FF8A8A>품절</color>" : it.PriceText(), 20, FontStyles.Bold, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 40), TextAlignmentOptions.Center);
            price.enableAutoSizing = true; price.fontSizeMin = 12; price.fontSizeMax = 18;
            if (!soldOut && !afford) price.alpha = 0.5f;
        }

        // ---------------- 패키지 · 월정액 카드 ----------------

        private Image Card(Color edge, bool dim)
        {
            var frame = UIFactory.AddImage("Card", _content, new Color(edge.r, edge.g, edge.b, dim ? 0.35f : 0.9f), ProceduralSprites.Rounded);
            frame.type = Image.Type.Sliced;
            var inner = UIFactory.AddImage("Inner", frame.transform, new Color(0.07f, 0.08f, 0.12f, 0.96f), ProceduralSprites.Rounded);
            inner.type = Image.Type.Sliced;
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);
            if (dim) inner.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;
            return inner;
        }

        private Button CardButton(Transform parent, string label, Color color, bool enabled, UnityEngine.Events.UnityAction onClick, Vector2 size, Vector2 pos, Vector2 anchor)
        {
            var b = UIFactory.AddButton("Buy", parent, label, 24, font, enabled ? color : new Color(1, 1, 1, 0.1f), Color.white);
            var rt = (RectTransform)b.transform;
            UIFactory.Anchor(rt, anchor, anchor, anchor);
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            b.interactable = enabled;
            var t = b.GetComponentInChildren<TextMeshProUGUI>();
            t.richText = true; t.enableAutoSizing = true; t.fontSizeMin = 14; t.fontSizeMax = 24;
            b.onClick.AddListener(onClick);
            return b;
        }

        /// <summary>패키지 카드: 아이콘 · 이름 · 묶음 내용 · 남은 구매 · (출시 결제 가격) · 크리스탈 구매.</summary>
        private void BuildPackageCard(ShopItem it)
        {
            int left = shop.Remaining(it);
            bool soldOut = left == 0;
            var inner = Card(new Color(1f, 0.8f, 0.4f), soldOut).transform;

            var icon = GameArt.Load(it.IconPath);
            if (icon != null)
            {
                var im = UIFactory.AddImage("Icon", inner, Color.white, icon);
                im.preserveAspect = true;
                UIFactory.Anchor(im.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                im.rectTransform.sizeDelta = new Vector2(220, 220); im.rectTransform.anchoredPosition = new Vector2(20, 8);
            }
            Label(inner, $"<b>{it.title}</b>", 30, FontStyles.Normal, new Vector2(0, 1), new Vector2(260, -20), new Vector2(490, 42), TextAlignmentOptions.TopLeft).color = new Color(1f, 0.88f, 0.6f);
            var contents = Label(inner, it.BundleText("\n"), 21, FontStyles.Normal, new Vector2(0, 1), new Vector2(260, -70), new Vector2(490, 140), TextAlignmentOptions.TopLeft);
            contents.lineSpacing = 6f; contents.enableAutoSizing = true; contents.fontSizeMin = 14; contents.fontSizeMax = 21;
            string limit = it.limit <= 0 ? "" : it.reset switch
            {
                ShopReset.Daily => $"오늘 {left}/{it.limit}", ShopReset.Weekly => $"이번 주 {left}/{it.limit}",
                ShopReset.Monthly => $"이번 달 {left}/{it.limit}", _ => $"계정당 {left}/{it.limit}",
            };
            Label(inner, $"<color=#9AA3B5>{limit}</color>", 18, FontStyles.Bold, new Vector2(1, 1), new Vector2(-18, -20), new Vector2(200, 26), TextAlignmentOptions.TopRight);
            var prod = IapCatalog.Find(it.id);
            string block = prod != null ? prod.block() : shop.BlockReason(it, 1);
            CardButton(inner, soldOut ? "품절" : $"구매  <b>{IapStore.Instance.PriceOf(prod)}</b>", new Color(0.25f, 0.62f, 0.95f, 0.98f), block == null && prod != null,
                () => BuyIap(prod, $"<color=#7CFF9A>{it.title} 구매!</color> 보상을 확인하세요"), new Vector2(300, 62), new Vector2(-18, 18), new Vector2(1, 0));
        }

        /// <summary>월정액 카드: 아이콘 · 이름 · 바로 · 매일(30일) · 총 · 남은 기간 · 구매/연장.</summary>
        private void BuildPassCard(PassDef p)
        {
            var mp = MonthlyPass.Instance;
            int daysLeft = mp.DaysLeft(p);
            var inner = Card(p.color, false).transform;

            Label(inner, $"<b>{p.name}</b>", 26, FontStyles.Normal, new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(350, 38), TextAlignmentOptions.Top).color = p.color;
            Label(inner, $"<color=#9AA3B5>{p.desc}</color>", 17, FontStyles.Normal, new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(350, 26), TextAlignmentOptions.Top);
            var icon = GameArt.Load(p.icon);
            if (icon != null)
            {
                var im = UIFactory.AddImage("Icon", inner, Color.white, icon);
                im.preserveAspect = true;
                UIFactory.Anchor(im.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
                im.rectTransform.sizeDelta = new Vector2(210, 210); im.rectTransform.anchoredPosition = new Vector2(0, -88);
            }
            var body = Label(inner,
                $"<color=#FFD36B>구매 즉시</color>\n{p.now().Describe("\n")}\n\n<color=#FFD36B>매일 05:00 우편 · {MonthlyPass.Days}일</color>\n{p.daily().Describe("\n")}",
                20, FontStyles.Normal, new Vector2(0.5f, 1), new Vector2(0, -310), new Vector2(340, 250), TextAlignmentOptions.Top);
            body.lineSpacing = 4f; body.enableAutoSizing = true; body.fontSizeMin = 13; body.fontSizeMax = 20;

            string state = daysLeft > 0
                ? $"<color=#7CFF9A>사용 중 · 남은 {daysLeft}일</color>" + (mp.ReceivedToday(p) ? "  <size=85%><color=#9AA3B5>오늘 보상 받음(우편)</color></size>" : "")
                : "<color=#9AA3B5>미사용</color>";
            Label(inner, state, 19, FontStyles.Bold, new Vector2(0.5f, 0), new Vector2(0, 128), new Vector2(350, 52), TextAlignmentOptions.Bottom).textWrappingMode = TextWrappingModes.Normal;
            var prod = IapCatalog.Find(p.id);
            string block = mp.PaidBlockReason(p);
            if (block != null)
                Label(inner, $"<color=#FF8A8A>{block}</color>", 15, FontStyles.Normal, new Vector2(0.5f, 0), new Vector2(0, 96), new Vector2(350, 22), TextAlignmentOptions.Bottom);
            CardButton(inner, $"{(daysLeft > 0 ? "30일 연장" : "구매")}  <b>{IapStore.Instance.PriceOf(prod)}</b>", new Color(0.25f, 0.62f, 0.95f, 0.98f), block == null && prod != null,
                () => BuyIap(prod, $"<color=#7CFF9A>{p.name} 구매!</color> 오늘 보상은 우편으로"), new Vector2(340, 66), new Vector2(0, 20), new Vector2(0.5f, 0));
        }

        /// <summary>크리스탈 충전 카드: 그림 · 개수 · 첫 구매 2배(또는 보너스) · 가격 버튼.</summary>
        private void BuildTopUpCard(IapCatalog.TopUpDef t)
        {
            bool first = IapCatalog.FirstBonusAvailable(t);
            var inner = Card(new Color(0.45f, 0.85f, 1f), false).transform;
            var icon = GameArt.Load(t.icon) ?? GameArt.Load("Items/Crystal");
            if (icon != null)
            {
                var im = UIFactory.AddImage("Icon", inner, Color.white, icon);
                im.preserveAspect = true;
                UIFactory.Anchor(im.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
                im.rectTransform.sizeDelta = new Vector2(170, 170); im.rectTransform.anchoredPosition = new Vector2(0, -14);
            }
            Label(inner, $"<b>크리스탈 {t.crystals:N0}</b>", 32, FontStyles.Normal, new Vector2(0.5f, 1), new Vector2(0, -186), new Vector2(480, 42), TextAlignmentOptions.Top).color = new Color(0.62f, 0.92f, 1f);
            string bonus = first ? $"<color=#FFD36B><b>첫 구매 2배!</b>  +{t.crystals:N0}</color>"
                : t.bonus > 0 ? $"<color=#9BE28A>보너스 +{t.bonus:N0}</color>" : "<color=#6B7280>보너스 없음</color>";
            Label(inner, bonus, 22, FontStyles.Normal, new Vector2(0.5f, 1), new Vector2(0, -230), new Vector2(480, 30), TextAlignmentOptions.Top);
            if (first)
            {
                var tag = UIFactory.AddImage("FirstTag", inner, new Color(0.95f, 0.35f, 0.3f, 1f), ProceduralSprites.Rounded);
                tag.type = Image.Type.Sliced; tag.preserveAspect = false;
                UIFactory.Anchor(tag.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
                tag.rectTransform.sizeDelta = new Vector2(110, 36); tag.rectTransform.anchoredPosition = new Vector2(12, -12);
                Label(tag.transform, "<b>2배</b>", 20, FontStyles.Normal, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 36), TextAlignmentOptions.Center);
            }
            var prod = IapCatalog.Find(t.id);
            int got = IapCatalog.TopUpAmount(t);
            CardButton(inner, $"<b>{IapStore.Instance.PriceOf(prod)}</b>", new Color(0.25f, 0.62f, 0.95f, 0.98f), prod != null,
                () => BuyIap(prod, $"<color=#8FE9FF>크리스탈 {got:N0}</color> 충전!"), new Vector2(300, 60), new Vector2(0, 18), new Vector2(0.5f, 0));
        }

        /// <summary>결제 시작 → 끝나면 알림 · 갱신(결제 창은 스토어가 띄움).</summary>
        private void BuyIap(IapProduct p, string okMsg)
        {
            if (p == null) return;
            if (IapStore.Instance.Busy) { Toast("<color=#FF8A8A>다른 결제가 진행 중입니다</color>"); return; }
            Toast("<color=#9AA3B5>결제 창을 여는 중…</color>".Replace("…", "..."));
            IapStore.Instance.Buy(p, err =>
            {
                if (this == null) return;
                Toast(err == null ? okMsg : $"<color=#FF8A8A>{err}</color>");
                Refresh();
            });
        }

        private void ShowConfirm(string title, string body, Action ok)
        {
            _confirmTitle.text = title;
            _confirmBody.text = body;
            _confirmOk = ok;
            _confirmRoot.SetActive(true);
        }

        // ---------------- 수량 · 구매 ----------------

        private void AddQty(int delta)
        {
            if (_selected == null) return;
            int max = Mathf.Max(1, shop.MaxBuyable(_selected));
            _qty = Mathf.Clamp(_qty + delta, 1, max);
            RefreshDetail();
        }

        private void SetMaxQty()
        {
            if (_selected == null) return;
            _qty = Mathf.Max(1, shop.MaxBuyable(_selected));
            RefreshDetail();
        }

        private void OnBuyClicked()
        {
            var it = _selected;
            if (it == null || shop.BlockReason(it, _qty) != null) return;
            if (it.currency == ShopCurrency.Crystal)
            {
                int have = wallet != null ? wallet.Crystals : 0;
                long cost = (long)it.price * _qty;
                string what = it.IsBundle ? $"<b>{it.RewardRichName}</b>\n<size=80%>{it.BundleText()}</size>" : $"<b>{it.RewardRichName} ×{(long)it.amount * _qty:N0}</b>";
                ShowConfirm("크리스탈 사용",
                    $"<color=#8FE9FF>크리스탈 {cost:N0}</color>을(를) 사용해\n{what}을(를) 구매할까요?\n" +
                    $"<size=80%><color=#9AA3B5>보유 {have:N0}  →  {have - cost:N0}</color></size>",
                    DoBuy);
                return;
            }
            DoBuy();
        }

        private void DoBuy()
        {
            if (_confirmRoot != null) _confirmRoot.SetActive(false);
            var it = _selected;
            if (it == null) return;
            int qty = _qty;
            string block = shop.BlockReason(it, qty);
            if (block != null) { Toast($"<color=#FF8A8A>{block}</color>"); RefreshDetail(); return; }
            if (!shop.Buy(it, qty)) { Toast("<color=#FF8A8A>구매하지 못했습니다</color>"); return; }

            string got = it.GotText(qty);
            Toast($"{got}  획득!");
            _qty = 1;
            Refresh();
        }

        private void Toast(string msg)
        {
            if (_toast == null) return;
            if (_toastCo != null) StopCoroutine(_toastCo);
            _toastCo = StartCoroutine(ToastRoutine(msg));
        }

        private IEnumerator ToastRoutine(string msg)
        {
            _toast.text = msg;
            _toast.alpha = 1f;
            yield return new WaitForSecondsRealtime(1.4f);
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                _toast.alpha = 1f - t / 0.4f;
                yield return null;
            }
            _toast.alpha = 0f;
            _toastCo = null;
        }

        // ---------------- UI 구성 ----------------

        private void BuildUI()
        {
            var cgo = UIFactory.NewRect("ShopCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 73;
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 어떤 화면비에서도 1920×1080 설계 영역이 전부 보이게(남는 쪽은 여백)
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.05f, 0.06f, 0.09f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            BackgroundArt.Create(_root.transform, 0.72f).Set("Backgrounds/shop");
            Transform root = UIFactory.Frame(_root.transform);   // 넓은 화면에서도 1920 폭 설계를 가운데에

            var title = UIFactory.AddText("Title", root, "상점", 40, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(600, 56), new Vector2(0, -30));

            CurrencyBar.Create(root, font, wallet, new Vector2(-50, -32), Currency.Crystal, Currency.Gold);

            // 왼쪽 세로 탭(아이콘 + 이름)
            _tabs = UIFactory.NewRect("Tabs", root);
            Place(_tabs, new Vector2(0, 1), new Vector2(210, 560), new Vector2(40, -150), new Vector2(0, 1));
            var th = _tabs.gameObject.AddComponent<VerticalLayoutGroup>();
            th.spacing = 10; th.childAlignment = TextAnchor.UpperCenter;
            th.childControlWidth = true; th.childControlHeight = true;
            th.childForceExpandWidth = false; th.childForceExpandHeight = false;

            _hint = UIFactory.AddText("Hint", root, "", 22, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(_hint.rectTransform, new Vector2(0, 1), new Vector2(1610, 34), new Vector2(270, -104), new Vector2(0, 1));

            // 상품 목록(스크롤)
            var view = UIFactory.NewRect("View", root);
            Place(view, new Vector2(0, 1), new Vector2(1060, 890), new Vector2(270, -150), new Vector2(0, 1));
            _view = view;
            view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.2f);
            view.gameObject.AddComponent<RectMask2D>();
            _content = UIFactory.NewRect("Content", view);
            UIFactory.Anchor(_content, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            _content.sizeDelta = Vector2.zero; _content.anchoredPosition = Vector2.zero;
            var grid = _content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(330, 250); grid.spacing = new Vector2(18, 18);
            grid.padding = new RectOffset(16, 16, 16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            _grid = grid;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = _content; scroll.viewport = view;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            // 오른쪽: 상세
            var panel = UIFactory.AddImage("Detail", root, new Color(0.10f, 0.10f, 0.14f, 0.96f));
            Place(panel.rectTransform, new Vector2(1, 1), new Vector2(530, 890), new Vector2(-40, -150), new Vector2(1, 1));
            _detailPanel = panel.gameObject;
            var pt = panel.transform;

            _detName = UIFactory.AddText("Name", pt, "", 30, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_detName.rectTransform, new Vector2(0.5f, 1), new Vector2(520, 46), new Vector2(0, -26));
            _detSub = UIFactory.AddText("Sub", pt, "", 22, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_detSub.rectTransform, new Vector2(0.5f, 1), new Vector2(520, 34), new Vector2(0, -78));

            _detDesc = UIFactory.AddText("Desc", pt, "", 20, font, new Color(0.88f, 0.9f, 0.96f), TextAlignmentOptions.TopLeft);
            Place(_detDesc.rectTransform, new Vector2(0.5f, 1), new Vector2(496, 330), new Vector2(0, -126));
            _detDesc.lineSpacing = 6f;

            _detLimit = UIFactory.AddText("Limit", pt, "", 22, font, new Color(0.85f, 0.87f, 0.93f), TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_detLimit.rectTransform, new Vector2(0.5f, 0), new Vector2(520, 32), new Vector2(0, 360), new Vector2(0.5f, 0));

            // 수량 선택: [-10] [-] 수량 [+] [+10] [최대]
            QtyButton(pt, "-10", -205, () => AddQty(-10));
            QtyButton(pt, "-", -125, () => AddQty(-1));
            _qtyText = UIFactory.AddText("Qty", pt, "1", 30, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_qtyText.rectTransform, new Vector2(0.5f, 0), new Vector2(90, 56), new Vector2(-45, 290), new Vector2(0.5f, 0));
            QtyButton(pt, "+", 35, () => AddQty(1));
            QtyButton(pt, "+10", 115, () => AddQty(10));
            QtyButton(pt, "최대", 205, SetMaxQty, 90);

            _total = UIFactory.AddText("Total", pt, "", 22, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_total.rectTransform, new Vector2(0.5f, 0), new Vector2(520, 72), new Vector2(0, 200), new Vector2(0.5f, 0));
            _total.lineSpacing = 6f;

            _buyBtn = UIFactory.AddButton("Buy", pt, "구매", 30, font, new Color(0.25f, 0.62f, 0.95f, 0.98f), Color.white);
            Place((RectTransform)_buyBtn.transform, new Vector2(0.5f, 0), new Vector2(420, 84), new Vector2(0, 90), new Vector2(0.5f, 0));
            _buyLabel = _buyBtn.GetComponentInChildren<TextMeshProUGUI>();
            _buyLabel.richText = true;
            _buyBtn.onClick.AddListener(OnBuyClicked);

            _toast = UIFactory.AddText("Toast", pt, "", 22, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_toast.rectTransform, new Vector2(0.5f, 0), new Vector2(520, 40), new Vector2(0, 34), new Vector2(0.5f, 0));
            _toast.richText = true;
            _toast.alpha = 0f;

            _backBtn = UIFactory.AddButton("BackBtn", root, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            Place((RectTransform)_backBtn.transform, new Vector2(0, 1), new Vector2(150, 44), new Vector2(30, -30), new Vector2(0, 1));
            _backBtn.onClick.AddListener(() => _onBack?.Invoke());
            _backBtn.gameObject.SetActive(false);

            BuildConfirm(canvas.transform);
        }

        private void QtyButton(Transform parent, string label, float x, UnityEngine.Events.UnityAction onClick, float width = 70)
        {
            var b = UIFactory.AddButton("Qty" + label, parent, label, 22, font, new Color(1, 1, 1, 0.16f), Color.white);
            Place((RectTransform)b.transform, new Vector2(0.5f, 0), new Vector2(width, 56), new Vector2(x, 290), new Vector2(0.5f, 0));
            b.onClick.AddListener(onClick);
        }

        private void BuildConfirm(Transform canvas)
        {
            _confirmRoot = UIFactory.AddImage("ConfirmDim", canvas, new Color(0, 0, 0, 0.7f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_confirmRoot.transform);

            var panel = UIFactory.AddImage("Panel", _confirmRoot.transform, new Color(0.12f, 0.12f, 0.17f, 0.98f), ProceduralSprites.Rounded, raycast: true);
            panel.type = Image.Type.Sliced; panel.preserveAspect = false;
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(700, 420), Vector2.zero, new Vector2(0.5f, 0.5f));

            var title = UIFactory.AddText("Title", panel.transform, "크리스탈 사용", 34, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(600, 50), new Vector2(0, -26));
            _confirmTitle = title;

            _confirmBody = UIFactory.AddText("Body", panel.transform, "", 26, font, Color.white, TextAlignmentOptions.Center);
            Place(_confirmBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(640, 210), new Vector2(0, 20), new Vector2(0.5f, 0.5f));
            _confirmBody.textWrappingMode = TextWrappingModes.Normal;
            _confirmBody.lineSpacing = 10f;

            var cancel = UIFactory.AddButton("Cancel", panel.transform, "취소", 26, font, new Color(0.35f, 0.37f, 0.44f, 0.98f), Color.white);
            Place((RectTransform)cancel.transform, new Vector2(0.5f, 0), new Vector2(240, 68), new Vector2(-135, 30), new Vector2(0.5f, 0));
            cancel.onClick.AddListener(() => _confirmRoot.SetActive(false));

            var ok = UIFactory.AddButton("Ok", panel.transform, "구매", 26, font, new Color(0.25f, 0.62f, 0.95f, 0.98f), Color.white);
            Place((RectTransform)ok.transform, new Vector2(0.5f, 0), new Vector2(240, 68), new Vector2(135, 30), new Vector2(0.5f, 0));
            ok.onClick.AddListener(() => { _confirmRoot.SetActive(false); var a = _confirmOk; _confirmOk = null; a?.Invoke(); });

            _confirmRoot.SetActive(false);
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
            SubcultureRPG.UI.UIInput.AddModule(es);
        }
    }
}
