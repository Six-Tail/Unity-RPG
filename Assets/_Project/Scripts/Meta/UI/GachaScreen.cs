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
        private CurrencyBar _pickupBar, _standardBar;
        private TextMeshProUGUI _bannerText, _pityText, _featuredName, _featuredStars, _lineup, _toast, _ratesText;
        private TextMeshProUGUI _singleLabel, _multiLabel, _chargeLabel;
        private RectTransform _catTabs, _bannerTabs;
        private Button _singleBtn, _multiBtn, _backBtn;
        private System.Action _onBack;
        private BackgroundArt _bg;

        // 픽업 전환 연출
        private GachaBanner _shownBanner;
        private Coroutine _switchRoutine;
        private Image _veil, _sweep;
        private CanvasGroup _featGroup;

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
            var b = gacha != null ? gacha.Banner : null;
            RebuildTabs();
            Refresh();
            // 다른 배너로 바뀌면 카드 뒤집기 연출, 처음 열 때는 바로
            if (_shownBanner != null && b != _shownBanner && isActiveAndEnabled && _root != null && _root.activeInHierarchy)
            {
                if (_switchRoutine != null) StopCoroutine(_switchRoutine);
                _switchRoutine = StartCoroutine(SwitchRoutine());
            }
            else
            {
                BuildFeatured();
                ResetSwitchVisuals();
            }
            _shownBanner = b;
        }

        private void ResetSwitchVisuals()
        {
            if (_frame != null) _frame.rectTransform.localScale = Vector3.one;
            if (_featGroup != null) _featGroup.alpha = 1f;
            if (_veil != null) _veil.color = new Color(0, 0, 0, 0);
            if (_sweep != null) _sweep.gameObject.SetActive(false);
            if (_featuredName != null) _featuredName.rectTransform.anchoredPosition = new Vector2(0, -232);
        }

        /// <summary>
        /// 배너 전환: 카드가 옆으로 뒤집히며 사라지고(배경은 살짝 어두워짐) → 새 캐릭터로 바뀐 뒤
        /// 반대편에서 뒤집혀 나타나며 살짝 튕기고, 빛줄기가 카드를 훑고, 이름이 아래에서 떠오름.
        /// </summary>
        private System.Collections.IEnumerator SwitchRoutine()
        {
            var frt = _frame.rectTransform;
            bool hadCard = _frame.gameObject.activeSelf;

            // 1) 접히며 사라짐
            const float outDur = 0.16f;
            for (float t = 0; t < outDur; t += Time.unscaledDeltaTime)
            {
                float k = t / outDur;
                float e = k * k;
                if (hadCard) frt.localScale = new Vector3(Mathf.Lerp(1f, 0f, e), Mathf.Lerp(1f, 0.94f, e), 1f);
                _featGroup.alpha = 1f - e;
                _veil.color = new Color(0, 0, 0, 0.45f * e);
                yield return null;
            }

            // 2) 내용 교체
            BuildFeatured();
            bool hasCard = _frame.gameObject.activeSelf;
            frt.localScale = hasCard ? new Vector3(0f, 0.94f, 1f) : Vector3.one;

            // 3) 펼쳐지며 나타남(살짝 튕김) + 빛줄기 + 이름 떠오름
            const float inDur = 0.34f;
            if (hasCard) _sweep.gameObject.SetActive(true);
            var nrt = _featuredName.rectTransform;
            for (float t = 0; t < inDur; t += Time.unscaledDeltaTime)
            {
                float k = t / inDur;
                float back = 1f + 2.2f * Mathf.Pow(k - 1f, 3) + 1.2f * Mathf.Pow(k - 1f, 2);   // easeOutBack
                if (hasCard) frt.localScale = new Vector3(Mathf.Max(0f, back), Mathf.Lerp(0.94f, 1f, k), 1f);
                _featGroup.alpha = Mathf.Clamp01(k * 1.6f);
                _veil.color = new Color(0, 0, 0, 0.45f * (1f - k));
                nrt.anchoredPosition = new Vector2(0, -232 - 18f * (1f - Mathf.Clamp01(k * 1.4f)));
                if (hasCard)
                {
                    // 빛줄기가 왼쪽 아래 → 오른쪽 위로 훑음
                    var srt = _sweep.rectTransform;
                    float sk = Mathf.Clamp01((k - 0.25f) / 0.75f);
                    srt.anchoredPosition = new Vector2(Mathf.Lerp(-260f, 260f, sk), 0f);
                    _sweep.color = new Color(1f, 0.97f, 0.85f, 0.55f * Mathf.Sin(sk * Mathf.PI));
                }
                yield return null;
            }
            ResetSwitchVisuals();
            _switchRoutine = null;
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
            // 지금 배너에서 쓰는 뽑기권만(픽업 ↔ 상시)
            if (_pickupBar != null) _pickupBar.gameObject.SetActive(CurType == GachaBannerType.Pickup);
            if (_standardBar != null) _standardBar.gameObject.SetActive(CurType == GachaBannerType.Standard);

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

            // 픽업: 카드 왼쪽 세로 목록(썸네일 + 이름) — 카드·제목과 겹치지 않음
            bool showSub = cur != null && cur.IsPickup && pickups.Count > 1;
            _bannerTabs.gameObject.SetActive(showSub);
            if (showSub)
                foreach (var p in pickups)
                    BannerTab(p, p == cur);
        }

        /// <summary>픽업 캐릭터 탭 1개: 썸네일 + ★6 이름 + 속성. 선택된 탭은 분홍 바탕 · 왼쪽 강조 막대 · 살짝 커지는 연출.</summary>
        private void BannerTab(GachaBanner pb, bool on)
        {
            var feat = pb.featured6;
            var btn = UIFactory.AddButton("BannerTab", _bannerTabs, "", 20, font,
                on ? new Color(0.95f, 0.5f, 0.72f, 0.96f) : new Color(0.1f, 0.1f, 0.16f, 0.82f), Color.white);
            var rt = (RectTransform)btn.transform;
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 232; le.preferredHeight = 76;
            btn.onClick.AddListener(() => { if (gacha.Banner != pb) gacha.Select(pb); });

            // 썸네일
            var thumbBg = UIFactory.AddImage("ThumbBg", rt, new Color(0, 0, 0, 0.45f));
            UIFactory.Anchor(thumbBg.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            thumbBg.rectTransform.sizeDelta = new Vector2(60, 60); thumbBg.rectTransform.anchoredPosition = new Vector2(10, 0);
            var sprite = feat != null ? GameArt.Thumb(feat) : null;
            if (sprite != null)
            {
                var th = UIFactory.AddImage("Thumb", thumbBg.transform, Color.white, sprite);
                th.preserveAspect = true;
                UIFactory.Stretch(th.rectTransform, 2, 2, 2, 2);
            }

            // 이름 · 속성
            var lbl = btn.GetComponentInChildren<TextMeshProUGUI>();
            lbl.alignment = TextAlignmentOptions.Left;
            lbl.richText = true;
            UIFactory.Stretch(lbl.rectTransform, 80, 0, 8, 0);
            string el = feat != null ? $"<size=72%><color={(on ? "#FFFFFF" : Traits.Hex(feat.element))}>{Traits.Name(feat.element)} · {Traits.Name(feat.role)}</color></size>" : "";
            lbl.text = $"<size=72%><color={(on ? "#FFF1A8" : "#FFC73F")}>★6</color></size> <b>{pb.ShortName}</b>\n{el}";
            lbl.color = on ? Color.white : new Color(0.85f, 0.87f, 0.93f);

            if (on)
            {
                // 왼쪽 강조 막대 + 선택 연출(톡 커졌다 자리잡음)
                var bar = UIFactory.AddImage("Accent", rt, new Color(1f, 0.92f, 0.6f, 1f));
                UIFactory.Anchor(bar.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f));
                bar.rectTransform.sizeDelta = new Vector2(5, -14); bar.rectTransform.anchoredPosition = new Vector2(2, 0);
                StartCoroutine(TabPop(rt));
            }
        }

        private System.Collections.IEnumerator TabPop(RectTransform rt)
        {
            const float dur = 0.28f;
            for (float t = 0; t < dur && rt != null; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                float s = k < 0.4f ? Mathf.Lerp(0.94f, 1.08f, k / 0.4f) : Mathf.Lerp(1.08f, 1.04f, (k - 0.4f) / 0.6f);
                rt.localScale = Vector3.one * s;
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one * 1.04f;
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
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 어떤 화면비에서도 1920×1080 설계 영역이 전부 보이게(남는 쪽은 여백)
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

            // 배너 전환 때 배경을 살짝 가리는 막(배경 바로 위)
            _veil = UIFactory.AddImage("SwitchVeil", root, new Color(0, 0, 0, 0));
            UIFactory.Stretch(_veil.rectTransform);
            _veil.raycastTarget = false;
            _veil.transform.SetSiblingIndex(_catTabs.GetSiblingIndex());   // 배경 바로 위, 제목·탭 아래

            // 픽업 배너 선택(픽업 분류일 때만) — 카드 왼쪽 세로 목록
            _bannerTabs = UIFactory.NewRect("BannerTabs", root);
            Place(_bannerTabs, new Vector2(0.5f, 0.5f), new Vector2(240, 420), new Vector2(-196, 32), new Vector2(1f, 0.5f));
            var vl = _bannerTabs.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 12; vl.childAlignment = TextAnchor.MiddleRight;
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = false; vl.childForceExpandHeight = false;

            // 재화(오른쪽 위): [지금 배너의 뽑기권] [크리스탈] — 배너 종류가 바뀌면 뽑기권 쪽만 바뀜
            _pickupBar = CurrencyBar.Create(root, font, wallet, new Vector2(-50, -32), Currency.PickupTicket, Currency.Crystal);
            _standardBar = CurrencyBar.Create(root, font, wallet, new Vector2(-50, -32), Currency.StandardTicket, Currency.Crystal);
            _standardBar.gameObject.SetActive(false);

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
            frameInner.gameObject.AddComponent<RectMask2D>();   // 빛줄기가 카드 밖으로 안 나가게
            _sweep = UIFactory.AddImage("Sweep", frameInner.transform, new Color(1, 1, 1, 0), ProceduralSprites.Beam);
            UIFactory.Anchor(_sweep.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _sweep.rectTransform.sizeDelta = new Vector2(90, 620);
            _sweep.rectTransform.localEulerAngles = new Vector3(0, 0, -28f);
            _sweep.raycastTarget = false;
            _sweep.gameObject.SetActive(false);
            _frameFallback = UIFactory.AddText("Fallback", frameInner.transform, "", 34, font, new Color(1f, 0.85f, 0.45f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_frameFallback.rectTransform, 10, 10, 10, 10);

            _featuredStars = UIFactory.AddText("FStars", root, "", 26, font, new Color(1f, 0.8f, 0.3f), TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_featuredStars.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(400, 32), new Vector2(0, -196), new Vector2(0.5f, 0.5f));

            _featuredName = UIFactory.AddText("FName", root, "", 30, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_featuredName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(700, 40), new Vector2(0, -232), new Vector2(0.5f, 0.5f));

            // 전환 때 함께 사라졌다 나타나는 묶음(별·이름·라인업은 아래에서 이 그룹으로 옮김)
            var featRt = UIFactory.NewRect("FeaturedInfo", root);
            UIFactory.Stretch(featRt);
            _featGroup = featRt.gameObject.AddComponent<CanvasGroup>();
            _featGroup.blocksRaycasts = false;
            _featuredStars.transform.SetParent(featRt, false);
            _featuredName.transform.SetParent(featRt, false);

            // 상시 6성 라인업(상시 분류일 때)
            _lineup = UIFactory.AddText("Lineup", featRt, "", 28, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
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
            DevTools.Gate(charge.gameObject);   // 출시 빌드에서는 없음

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
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 어떤 화면비에서도 1920×1080 설계 영역이 전부 보이게(남는 쪽은 여백)
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
            SubcultureRPG.UI.UIInput.AddModule(es);
        }
    }
}
