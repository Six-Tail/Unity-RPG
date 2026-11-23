using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Gacha;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 가챠 뽑기 화면. 위쪽 분류 탭(픽업 뽑기 · 상시 뽑기), 픽업은 아래 줄에서 픽업 캐릭터별 배너 선택.
    /// 뽑기는 해당 종류의 뽑기권(픽업/상시, 1장 = 1회)이 있으면 뽑기권, 없으면 크리스탈로 진행 — 확인 창을 거칩니다.
    /// 뽑기 → GachaController → 결과 연출(GachaResultPanel, OnPulled 구독) + 로스터 반영.
    /// 코드로 자동 구성 — 빈 GameObject에 추가 → gacha/wallet 연결(비우면 자동 탐색).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Gacha Screen")]
    public class GachaScreen : MonoBehaviour
    {
        [SerializeField] private GachaController gacha;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private int singleCost = 160;
        [SerializeField] private int multiCost = 1600;
        [SerializeField, Tooltip("테스트 충전 버튼이 주는 크리스탈.")] private int chargeAmount = 3200;
        [SerializeField, Tooltip("테스트 충전 버튼이 주는 뽑기권(현재 종류).")] private int chargeTickets = 10;

        private GameObject _root, _ratesOverlay;
        private Image _portrait, _frame;
        private TextMeshProUGUI _frameFallback;
        private TextMeshProUGUI _crystalText, _ticketText, _bannerText, _pityText, _featuredName, _featuredStars, _lineup, _toast, _ratesText;
        private TextMeshProUGUI _singleLabel, _multiLabel, _chargeLabel;
        private RectTransform _catTabs, _bannerTabs;
        private Button _singleBtn, _multiBtn, _backBtn;
        private System.Action _onBack;
        private BackgroundArt _bg;

        // 뽑기 확인 창(별도 캔버스 — 결과 화면 위에도 뜸)
        private GameObject _confirmRoot;
        private TextMeshProUGUI _confirmTitle, _confirmBody;
        private Button _confirmOk;
        private bool _confirmMulti;

        private GachaBannerType CurType => gacha != null && gacha.Banner != null ? gacha.Banner.type : GachaBannerType.Pickup;
        private int PullCount(bool multi) => multi ? (gacha != null ? gacha.MultiPullCount : 10) : 1;

        private void Awake()
        {
            if (gacha == null) gacha = FindAnyObjectByType<GachaController>();
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            BuildUI();
        }

        private void OnEnable()
        {
            if (wallet != null) wallet.OnChanged += Refresh;
            if (gacha != null) { gacha.OnPulled += OnPulled; gacha.OnBannerChanged += OnBannerChanged; }
        }

        private void OnDisable()
        {
            if (wallet != null) wallet.OnChanged -= Refresh;
            if (gacha != null) { gacha.OnPulled -= OnPulled; gacha.OnBannerChanged -= OnBannerChanged; }
        }

        private void Start()
        {
            OnBannerChanged();

            // 결과 화면의 재뽑기도 이 화면을 거쳐 비용 차감(부족하면 결과 화면을 닫고 안내)
            var panel = FindAnyObjectByType<SubcultureRPG.Gacha.UI.GachaResultPanel>();
            if (panel != null)
            {
                panel.SetPullHandler(multi => RequestPull(multi));
                panel.SetPullCosts(singleCost, multiCost);
                panel.SetCostLabel(multi => CostText(multi));
            }
        }

        public void Show(bool on)
        {
            if (_root != null) _root.SetActive(on);
            if (on) OnBannerChanged();
        }

        /// <summary>'← 홈' 버튼 동작 지정(null이면 버튼 숨김).</summary>
        public void SetBackAction(System.Action onBack)
        {
            _onBack = onBack;
            if (_backBtn != null) _backBtn.gameObject.SetActive(onBack != null);
        }

        private void OnPulled(List<GachaResult> _) => Refresh();

        private void OnBannerChanged()
        {
            RebuildTabs();
            BuildFeatured();
            Refresh();
        }

        // ---------------- 비용 ----------------

        /// <summary>이번 뽑기에 뽑기권을 쓰는가(해당 종류 뽑기권이 충분하면 우선 사용).</summary>
        private bool UsesTickets(bool multi) =>
            wallet != null && wallet.CanAffordTickets(CurType, PullCount(multi));

        private int CrystalCost(bool multi) => multi ? multiCost : singleCost;

        private bool CanPay(bool multi) => wallet == null || UsesTickets(multi) || wallet.CanAfford(CrystalCost(multi));

        /// <summary>'픽업 뽑기권 10' 또는 '크리스탈 1,600'(리치 텍스트).</summary>
        private string CostText(bool multi) => UsesTickets(multi)
            ? $"<color=#FFB3E6>{PlayerWallet.TicketName(CurType)} {PullCount(multi)}</color>"
            : $"<color=#8FE9FF>크리스탈 {CrystalCost(multi):N0}</color>";

        /// <summary>뽑기 요청(결과 화면 재뽑기 등 외부용): 재화 사용 확인 창을 띄웁니다.</summary>
        public void RequestPull(bool multi) => ShowConfirm(multi);

        /// <summary>1회/10연 버튼 → 뽑기권·크리스탈 사용 확인 창.</summary>
        private void ShowConfirm(bool multi)
        {
            if (gacha == null || gacha.Banner == null) return;
            _confirmMulti = multi;
            string what = multi ? $"{PullCount(true)}연 뽑기" : "1회 뽑기";
            string banner = gacha.Banner.IsPickup ? $"픽업 · {gacha.Banner.ShortName}" : "상시";
            _confirmTitle.text = $"{what}  <size=70%><color=#9AA3B5>{banner}</color></size>";

            bool ok = CanPay(multi);
            if (UsesTickets(multi))
            {
                int have = wallet.Tickets(CurType), n = PullCount(multi);
                _confirmBody.text = $"{CostText(multi)}장을 사용해 <b>{what}</b>를 진행할까요?\n" +
                                    $"<size=80%><color=#9AA3B5>보유 {have:N0}  →  {have - n:N0}</color></size>";
            }
            else
            {
                int cost = CrystalCost(multi);
                int have = wallet != null ? wallet.Crystals : int.MaxValue;
                string noTicket = $"<size=75%><color=#9AA3B5>{PlayerWallet.TicketName(CurType)} 부족 → 크리스탈 사용</color></size>\n";
                _confirmBody.text = ok
                    ? noTicket + $"{CostText(multi)}을(를) 사용해 <b>{what}</b>를 진행할까요?\n" +
                      $"<size=80%><color=#9AA3B5>보유 {have:N0}  →  {have - cost:N0}</color></size>"
                    : $"<color=#FF8A8A>{PlayerWallet.TicketName(CurType)}과 크리스탈이 부족합니다</color>\n" +
                      $"<size=80%><color=#9AA3B5>크리스탈 보유 {have:N0} / 필요 {cost:N0}</color></size>";
            }
            _confirmOk.interactable = ok;
            _confirmRoot.SetActive(true);
        }

        private void OnConfirm()
        {
            _confirmRoot.SetActive(false);
            DoPull(_confirmMulti);
        }

        private bool DoPull(bool multi)
        {
            if (gacha == null || gacha.Banner == null) return false;
            if (wallet != null)
            {
                bool paid = UsesTickets(multi)
                    ? wallet.SpendTickets(CurType, PullCount(multi))
                    : wallet.Spend(CrystalCost(multi));
                if (!paid) { Toast("뽑기권과 크리스탈이 부족합니다"); return false; }
            }
            if (multi) gacha.PullMulti(); else gacha.PullOnce();
            Refresh();
            return true;
        }

        // ---------------- 갱신 ----------------

        private void Refresh()
        {
            if (_root == null) return;
            if (_crystalText != null)
                _crystalText.text = wallet != null ? $"크리스탈  {wallet.Crystals:N0}" : "";
            if (_ticketText != null)
                _ticketText.text = wallet == null ? "" :
                    $"<color={(CurType == GachaBannerType.Pickup ? "#FFB3E6" : "#9AA3B5")}>픽업 뽑기권  {wallet.PickupTickets:N0}</color>      " +
                    $"<color={(CurType == GachaBannerType.Standard ? "#FFB3E6" : "#9AA3B5")}>상시 뽑기권  {wallet.StandardTickets:N0}</color>";

            var b = gacha != null ? gacha.Banner : null;
            if (_bannerText != null) _bannerText.text = b != null ? b.bannerName : "배너 없음";

            if (_pityText != null && b != null && b.config != null)
            {
                var st = gacha.State;
                int since = st != null ? st.pullsSince6 : 0;
                string s = $"6성 천장까지 {Mathf.Max(0, b.config.sixStarHardPity - since)}회";
                if (b.IsPickup)
                {
                    s += "  <color=#9AA3B5>· 픽업 배너끼리 공유</color>";
                    if (st != null && st.guaranteedFeatured6) s += "\n<color=#FFD36B>다음 6성 픽업 확정</color>";
                }
                _pityText.text = s;
            }

            if (_singleLabel != null) _singleLabel.text = $"1회 뽑기\n<size=65%>{CostText(false)}</size>";
            if (_multiLabel != null) _multiLabel.text = $"{PullCount(true)}연 뽑기\n<size=65%>{CostText(true)}</size>";
            if (_singleBtn != null) _singleBtn.interactable = CanPay(false);
            if (_multiBtn != null) _multiBtn.interactable = CanPay(true);
            if (_chargeLabel != null) _chargeLabel.text = $"충전(테스트)  크리스탈 +{chargeAmount:N0} · {PlayerWallet.TicketName(CurType)} +{chargeTickets}";
        }

        private void RebuildTabs()
        {
            if (_catTabs == null || gacha == null) return;
            Clear(_catTabs);
            Clear(_bannerTabs);

            var pickups = gacha.BannersOf(GachaBannerType.Pickup);
            var standards = gacha.BannersOf(GachaBannerType.Standard);
            var cur = gacha.Banner;

            if (pickups.Count > 0)
                CatTab("픽업 뽑기", cur != null && cur.IsPickup, () => { if (cur == null || !cur.IsPickup) gacha.Select(pickups[0]); });
            if (standards.Count > 0)
                CatTab("상시 뽑기", cur != null && !cur.IsPickup, () => gacha.Select(standards[0]));

            // 픽업: 아래 줄에 픽업 캐릭터별 배너
            bool showSub = cur != null && cur.IsPickup && pickups.Count > 1;
            _bannerTabs.gameObject.SetActive(showSub);
            if (showSub)
                foreach (var p in pickups)
                {
                    var pb = p;
                    bool on = pb == cur;
                    var btn = UIFactory.AddButton("BannerTab", _bannerTabs, $"<size=80%>★6</size>  {pb.ShortName}", 20, font,
                        on ? new Color(0.95f, 0.55f, 0.75f, 0.95f) : new Color(1, 1, 1, 0.12f), Color.white);
                    var le = btn.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = 200; le.preferredHeight = 44;
                    btn.onClick.AddListener(() => gacha.Select(pb));
                }
        }

        private void CatTab(string label, bool on, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIFactory.AddButton("CatTab", _catTabs, label, 24, font,
                on ? new Color(0.78f, 0.58f, 0.22f, 0.95f) : new Color(1, 1, 1, 0.12f), Color.white);
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 220; le.preferredHeight = 54;
            b.onClick.AddListener(onClick);
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

        private void BuildFeatured()
        {
            var b = gacha != null ? gacha.Banner : null;
            var feat = b != null ? b.featured6 : null;
            _lineup.text = "";

            // 배경: 배너 그림(Banners/{픽업 캐릭터 id} · Banners/standard) → 없으면 공용 가챠 배경
            string bannerKey = b == null ? null : (feat != null ? "Banners/" + (!string.IsNullOrEmpty(feat.id) ? feat.id : feat.name) : "Banners/standard");
            if (_bg != null) _bg.Set(bannerKey, "Backgrounds/gacha");
            _frame.gameObject.SetActive(feat != null);
            if (feat != null)
            {
                // 가운데 액자: 픽업 캐릭터 일러스트(Live2D 모델이 있으면 움직이는 모델)
                var sprite = GameArt.Portrait(feat);
                var starColor = StarText.ColorOf((int)feat.rarity);
                _frame.color = new Color(starColor.r, starColor.g, starColor.b, 1f);
                bool live = Live2DUI.Show(_portrait, feat);
                _portrait.sprite = sprite;
                _portrait.enabled = !live && sprite != null;
                _portrait.color = Color.white;
                _frameFallback.text = !live && sprite == null ? feat.displayName : "";
                _featuredName.text = $"{feat.displayName}  <size=70%><color={Traits.Hex(feat.element)}>{Traits.Name(feat.element)}</color> · {Traits.Name(feat.role)}</size>";
                _featuredStars.text = new string('★', (int)feat.rarity) + "  <size=70%>픽업</size>";
            }
            else
            {
                // 상시: 6성 라인업
                Live2DUI.Hide(_portrait);
                _portrait.enabled = false;
                _featuredStars.text = "";
                _featuredName.text = "";
                if (b != null)
                {
                    var sb = new System.Text.StringBuilder("<color=#FFC73F>★6 상시 캐릭터</color>\n\n");
                    foreach (var c in b.pool6)
                        if (c != null)
                            sb.Append($"{c.displayName}  <size=75%><color={Traits.Hex(c.element)}>{Traits.Name(c.element)}</color> · {Traits.Name(c.role)}</size>\n");
                    _lineup.text = sb.ToString();
                }
            }
        }

        private void Toast(string msg)
        {
            if (_toast == null) return;
            _toast.text = msg;
            CancelInvoke(nameof(ClearToast));
            Invoke(nameof(ClearToast), 1.6f);
        }

        private void ClearToast() { if (_toast != null) _toast.text = ""; }

        // ---------------- UI 구성 ----------------

        private void BuildUI()
        {
            var cgo = UIFactory.NewRect("GachaScreenCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.06f, 0.05f, 0.09f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            _bg = BackgroundArt.Create(_root.transform, 0.5f);
            var root = _root.transform;

            // 분류 탭(상단 중앙): 픽업 뽑기 · 상시 뽑기
            _catTabs = Row("CatTabs", root, new Vector2(0, -28), 52, 12);

            _bannerText = UIFactory.AddText("Banner", root, "", 34, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_bannerText.rectTransform, new Vector2(0.5f, 1), new Vector2(900, 48), new Vector2(0, -96));

            _pityText = UIFactory.AddText("Pity", root, "", 20, font, new Color(1f, 0.8f, 0.4f), TextAlignmentOptions.Center);
            Place(_pityText.rectTransform, new Vector2(0.5f, 1), new Vector2(800, 56), new Vector2(0, -146));

            // 픽업 배너 선택(픽업 분류일 때만)
            _bannerTabs = Row("BannerTabs", root, new Vector2(0, -206), 44, 10);

            _crystalText = UIFactory.AddText("Crystal", root, "", 24, font, new Color(0.6f, 0.9f, 1f), TextAlignmentOptions.Right, FontStyles.Bold);
            Place(_crystalText.rectTransform, new Vector2(1, 1), new Vector2(520, 36), new Vector2(-30, -30), new Vector2(1, 1));
            _ticketText = UIFactory.AddText("Tickets", root, "", 20, font, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
            Place(_ticketText.rectTransform, new Vector2(1, 1), new Vector2(620, 30), new Vector2(-30, -70), new Vector2(1, 1));

            // 픽업 캐릭터 액자(중앙, 이름 위) — 성급 색 테두리 + 그림자, 안쪽 3:4
            _frame = UIFactory.AddImage("FeaturedFrame", root, new Color(1f, 0.8f, 0.3f, 1f));
            Place(_frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(318, 420), new Vector2(0, 32), new Vector2(0.5f, 0.5f));
            var frameShadow = _frame.gameObject.AddComponent<Shadow>();
            frameShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            frameShadow.effectDistance = new Vector2(0f, -6f);
            var frameInner = UIFactory.AddImage("Inner", _frame.transform, new Color(0.08f, 0.08f, 0.12f, 1f));
            UIFactory.Stretch(frameInner.rectTransform, 5, 5, 5, 5);
            _portrait = UIFactory.AddImage("Featured", frameInner.transform, Color.white);
            UIFactory.Stretch(_portrait.rectTransform, 1, 1, 1, 1);
            _portrait.preserveAspect = true;
            _frameFallback = UIFactory.AddText("Fallback", frameInner.transform, "", 34, font, new Color(1f, 0.85f, 0.45f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_frameFallback.rectTransform, 10, 10, 10, 10);

            _featuredStars = UIFactory.AddText("FStars", root, "", 26, font, new Color(1f, 0.8f, 0.3f), TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_featuredStars.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(400, 32), new Vector2(0, -196), new Vector2(0.5f, 0.5f));

            _featuredName = UIFactory.AddText("FName", root, "", 30, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_featuredName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(700, 40), new Vector2(0, -232), new Vector2(0.5f, 0.5f));

            // 상시 6성 라인업(상시 분류일 때)
            _lineup = UIFactory.AddText("Lineup", root, "", 28, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_lineup.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(700, 420), new Vector2(0, -20), new Vector2(0.5f, 0.5f));
            _lineup.lineSpacing = 12f;

            _toast = UIFactory.AddText("Toast", root, "", 22, font, new Color(1f, 0.6f, 0.5f), TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_toast.rectTransform, new Vector2(0.5f, 0), new Vector2(600, 30), new Vector2(0, 220), new Vector2(0.5f, 0));

            // 뽑기 버튼
            _singleBtn = PullButton(new Color(0.45f, 0.3f, 0.75f, 0.95f), new Vector2(-170, 90), () => ShowConfirm(false));
            _singleLabel = _singleBtn.GetComponentInChildren<TextMeshProUGUI>();
            _multiBtn = PullButton(new Color(0.8f, 0.6f, 0.2f, 0.95f), new Vector2(170, 90), () => ShowConfirm(true));
            _multiLabel = _multiBtn.GetComponentInChildren<TextMeshProUGUI>();

            var charge = UIFactory.AddButton("Charge", root, "", 17, font, new Color(1, 1, 1, 0.14f), Color.white);
            Place((RectTransform)charge.transform, new Vector2(0.5f, 0), new Vector2(460, 48), new Vector2(0, 30), new Vector2(0.5f, 0));
            _chargeLabel = charge.GetComponentInChildren<TextMeshProUGUI>();
            charge.onClick.AddListener(() =>
            {
                if (wallet == null) return;
                wallet.Add(chargeAmount);
                wallet.AddTickets(CurType, chargeTickets);
            });

            // 홈 버튼(좌상단) — 내비게이터가 연결할 때만 표시
            _backBtn = UIFactory.AddButton("BackBtn", root, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            Place((RectTransform)_backBtn.transform, new Vector2(0, 1), new Vector2(150, 44), new Vector2(30, -34), new Vector2(0, 1));
            _backBtn.onClick.AddListener(() => _onBack?.Invoke());
            _backBtn.gameObject.SetActive(false);

            // 확률 보기 버튼(좌상단, 홈 버튼 옆)
            var ratesBtn = UIFactory.AddButton("RatesBtn", root, "확률 보기", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            Place((RectTransform)ratesBtn.transform, new Vector2(0, 1), new Vector2(150, 44), new Vector2(196, -34), new Vector2(0, 1));
            ratesBtn.onClick.AddListener(() => ShowRates(true));

            BuildRatesOverlay();
            BuildConfirm();
        }

        /// <summary>상단 중앙 가로 버튼 줄.</summary>
        private RectTransform Row(string name, Transform parent, Vector2 pos, float height, float spacing)
        {
            var rt = UIFactory.NewRect(name, parent);
            Place(rt, new Vector2(0.5f, 1), new Vector2(1200, height), pos);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return rt;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos, Vector2? pivot = null)
        {
            UIFactory.Anchor(rt, anchor, anchor, pivot ?? new Vector2(0.5f, 1));
            rt.sizeDelta = size; rt.anchoredPosition = pos;
        }

        private void BuildConfirm()
        {
            var cgo = UIFactory.NewRect("GachaConfirmCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;   // 가챠 결과 화면(100) 위
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            cgo.gameObject.AddComponent<GraphicRaycaster>();

            _confirmRoot = UIFactory.AddImage("Dim", canvas.transform, new Color(0, 0, 0, 0.7f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_confirmRoot.transform);

            var panel = UIFactory.AddImage("Panel", _confirmRoot.transform, new Color(0.12f, 0.12f, 0.17f, 0.98f), ProceduralSprites.Rounded, raycast: true);
            panel.type = Image.Type.Sliced; panel.preserveAspect = false;
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(660, 360), Vector2.zero, new Vector2(0.5f, 0.5f));

            _confirmTitle = UIFactory.AddText("Title", panel.transform, "", 34, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_confirmTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(620, 50), new Vector2(0, -26));

            _confirmBody = UIFactory.AddText("Body", panel.transform, "", 25, font, Color.white, TextAlignmentOptions.Center);
            Place(_confirmBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(600, 150), new Vector2(0, 14), new Vector2(0.5f, 0.5f));
            _confirmBody.lineSpacing = 10f;

            var cancel = UIFactory.AddButton("Cancel", panel.transform, "취소", 26, font, new Color(0.35f, 0.37f, 0.44f, 0.98f), Color.white);
            Place((RectTransform)cancel.transform, new Vector2(0.5f, 0), new Vector2(240, 68), new Vector2(-135, 30), new Vector2(0.5f, 0));
            cancel.onClick.AddListener(() => _confirmRoot.SetActive(false));

            _confirmOk = UIFactory.AddButton("Ok", panel.transform, "확인", 26, font, new Color(0.25f, 0.62f, 0.95f, 0.98f), Color.white);
            Place((RectTransform)_confirmOk.transform, new Vector2(0.5f, 0), new Vector2(240, 68), new Vector2(135, 30), new Vector2(0.5f, 0));
            _confirmOk.onClick.AddListener(OnConfirm);

            _confirmRoot.SetActive(false);
        }

        private void BuildRatesOverlay()
        {
            _ratesOverlay = UIFactory.AddImage("RatesOverlay", _root.transform, new Color(0, 0, 0, 0.88f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_ratesOverlay.transform);

            var title = UIFactory.AddText("Title", _ratesOverlay.transform, "확률 안내", 34, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(600, 50), new Vector2(0, -90));

            _ratesText = UIFactory.AddText("Rates", _ratesOverlay.transform, "", 22, font, Color.white, TextAlignmentOptions.TopLeft);
            Place(_ratesText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900, 640), new Vector2(0, 20), new Vector2(0.5f, 0.5f));
            _ratesText.lineSpacing = 6f;

            var close = UIFactory.AddButton("Close", _ratesOverlay.transform, "닫기", 24, font, new Color(1, 1, 1, 0.16f), Color.white);
            Place((RectTransform)close.transform, new Vector2(0.5f, 0), new Vector2(220, 64), new Vector2(0, 60), new Vector2(0.5f, 0));
            close.onClick.AddListener(() => ShowRates(false));

            _ratesOverlay.SetActive(false);
        }

        private void ShowRates(bool on)
        {
            if (_ratesOverlay == null) return;
            if (on) _ratesText.text = BuildRatesText();
            _ratesOverlay.SetActive(on);
        }

        private string BuildRatesText()
        {
            var b = gacha != null ? gacha.Banner : null;
            var cfg = b != null ? b.config : null;
            if (cfg == null) return "확률 정보를 찾을 수 없습니다.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>{b.bannerName}</b>  <size=75%><color=#9AA3B5>{(b.IsPickup ? "픽업 뽑기 — 픽업 뽑기권 사용" : "상시 뽑기 — 상시 뽑기권 사용")}</color></size>");
            sb.AppendLine("");
            sb.AppendLine($"<color=#FFC73F>6성</color>   {cfg.rate6:0.##}%");
            sb.AppendLine($"<size=68%><color=#B8B8B8>   소프트 천장 {cfg.sixStarSoftPityStart}회부터 상승 · 하드 천장 {cfg.sixStarHardPity}회 확정 · {(b.IsPickup ? "픽업 배너끼리 천장 공유" : "상시 천장은 따로")}</color></size>");
            if (b.IsPickup && b.featured6 != null)
                sb.AppendLine($"<size=68%><color=#FF9ED6>   6성의 {cfg.featured6Chance * 100f:0}%가 픽업 {b.featured6.displayName} (실패 시 다음 6성 픽업 확정)</color></size>");
            sb.AppendLine($"<size=68%><color=#B8B8B8>   {(b.IsPickup ? "픽업 실패 시: " : "")}{Names(b.pool6)}</color></size>");
            sb.AppendLine("");
            sb.AppendLine($"<color=#B86BFF>5성</color>   {cfg.rate5:0.##}%   <size=68%><color=#B8B8B8>{cfg.fiveStarHardPity}연 내 5성 이상 보장</color></size>");
            sb.AppendLine($"<size=68%><color=#B8B8B8>   {Names(b.pool5)}</color></size>");
            sb.AppendLine("");
            sb.AppendLine($"<color=#549EFF>4성</color>   {cfg.rate4:0.##}%");
            sb.AppendLine($"<size=68%><color=#B8B8B8>   {Names(b.pool4)}</color></size>");
            sb.AppendLine("");
            sb.AppendLine($"<color=#9EAABF>3성</color>   {cfg.Rate3:0.##}%");
            sb.AppendLine($"<size=68%><color=#B8B8B8>   {Names(b.pool3)}</color></size>");
            return sb.ToString();
        }

        private static string Names(List<CharacterData> pool)
        {
            if (pool == null || pool.Count == 0) return "-";
            var names = new List<string>();
            foreach (var c in pool) if (c != null) names.Add(c.displayName);
            return string.Join(" · ", names);
        }

        private Button PullButton(Color bg, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var btn = UIFactory.AddButton("Pull", _root.transform, "", 24, font, bg, Color.white);
            Place((RectTransform)btn.transform, new Vector2(0.5f, 0), new Vector2(300, 90), pos, new Vector2(0.5f, 0));
            btn.onClick.AddListener(onClick);
            return btn;
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
