using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Gacha;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 가챠 뽑기 홈 화면. 재화·천장 표시 + 1회/10연 뽑기 버튼.
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

        private GameObject _root, _ratesOverlay;
        private Image _portrait;
        private TextMeshProUGUI _crystalText, _bannerText, _pityText, _featuredName, _featuredStars, _toast, _ratesText;
        private Button _singleBtn, _multiBtn, _backBtn;
        private System.Action _onBack;

        // 뽑기 확인 창(별도 캔버스 — 결과 화면 위에도 뜸)
        private GameObject _confirmRoot;
        private TextMeshProUGUI _confirmTitle, _confirmBody;
        private Button _confirmOk;
        private bool _confirmMulti;

        private void Awake()
        {
            if (gacha == null) gacha = FindAnyObjectByType<GachaController>();
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            BuildUI();
        }

        private void OnEnable()
        {
            if (wallet != null) wallet.OnChanged += Refresh;
            if (gacha != null) gacha.OnPulled += OnPulled;
        }

        private void OnDisable()
        {
            if (wallet != null) wallet.OnChanged -= Refresh;
            if (gacha != null) gacha.OnPulled -= OnPulled;
        }

        private void Start()
        {
            BuildFeatured();
            Refresh();

            // 결과 화면의 재뽑기도 이 화면을 거쳐 크리스탈 차감(부족하면 결과 화면을 닫고 안내)
            var panel = FindAnyObjectByType<SubcultureRPG.Gacha.UI.GachaResultPanel>();
            if (panel != null)
            {
                panel.SetPullHandler(multi => RequestPull(multi));
                panel.SetPullCosts(singleCost, multiCost);
            }
        }

        public void Show(bool on)
        {
            if (_root != null) _root.SetActive(on);
            if (on) Refresh();
        }

        /// <summary>'← 홈' 버튼 동작 지정(null이면 버튼 숨김).</summary>
        public void SetBackAction(System.Action onBack)
        {
            _onBack = onBack;
            if (_backBtn != null) _backBtn.gameObject.SetActive(onBack != null);
        }

        private void OnPulled(List<GachaResult> _) => Refresh();

        /// <summary>뽑기 요청(결과 화면 재뽑기 등 외부용): 재화 사용 확인 창을 띄웁니다.</summary>
        public void RequestPull(bool multi) => ShowConfirm(multi);

        /// <summary>1회/10연 버튼 → 크리스탈 사용 확인 창.</summary>
        private void ShowConfirm(bool multi)
        {
            if (gacha == null) return;
            _confirmMulti = multi;
            int cost = multi ? multiCost : singleCost;
            int have = wallet != null ? wallet.Crystals : int.MaxValue;
            bool ok = wallet == null || wallet.CanAfford(cost);
            _confirmTitle.text = multi ? "10연 뽑기" : "1회 뽑기";
            _confirmBody.text = ok
                ? $"<color=#8FE9FF>크리스탈 {cost:N0}</color>을(를) 사용해 <b>{(multi ? "10연 뽑기" : "1회 뽑기")}</b>를 진행할까요?\n" +
                  $"<size=80%><color=#9AA3B5>보유 {have:N0}  →  {have - cost:N0}</color></size>"
                : $"<color=#FF8A8A>크리스탈이 부족합니다</color>\n<size=80%><color=#9AA3B5>보유 {have:N0} / 필요 {cost:N0}</color></size>";
            _confirmOk.interactable = ok;
            _confirmRoot.SetActive(true);
        }

        private void OnConfirm()
        {
            _confirmRoot.SetActive(false);
            DoPull(_confirmMulti ? multiCost : singleCost, _confirmMulti);
        }

        private bool DoPull(int cost, bool multi)
        {
            if (gacha == null) return false;
            if (wallet != null && !wallet.Spend(cost)) { Toast("크리스탈이 부족합니다"); return false; }
            if (multi) gacha.PullMulti(); else gacha.PullOnce();
            Refresh();
            return true;
        }

        private void Refresh()
        {
            if (_crystalText != null)
                _crystalText.text = wallet != null ? $"크리스탈  {wallet.Crystals:N0}" : "";

            if (_bannerText != null)
                _bannerText.text = gacha != null && gacha.Banner != null ? gacha.Banner.bannerName : "배너 없음";

            if (_pityText != null && gacha != null && gacha.Banner != null && gacha.Banner.config != null)
            {
                int since = gacha.State != null ? gacha.State.pullsSince6 : 0;
                int hard = gacha.Banner.config.sixStarHardPity;
                _pityText.text = $"6성 천장까지 {Mathf.Max(0, hard - since)}회";
            }

            if (_singleBtn != null) _singleBtn.interactable = wallet == null || wallet.CanAfford(singleCost);
            if (_multiBtn != null) _multiBtn.interactable = wallet == null || wallet.CanAfford(multiCost);
        }

        private void BuildFeatured()
        {
            var feat = gacha != null && gacha.Banner != null ? gacha.Banner.featured6 : null;
            if (feat != null)
            {
                var sprite = feat.thumbnail != null ? feat.thumbnail : feat.portrait;
                _portrait.sprite = sprite;
                _portrait.enabled = sprite != null;
                _featuredName.text = feat.displayName;
                _featuredStars.text = new string('★', (int)feat.rarity);
            }
            else
            {
                _portrait.enabled = false;
                _featuredName.text = "픽업 없음";
                _featuredStars.text = "";
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

            _bannerText = UIFactory.AddText("Banner", _root.transform, "", 38, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var brt = _bannerText.rectTransform;
            UIFactory.Anchor(brt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            brt.sizeDelta = new Vector2(900, 52); brt.anchoredPosition = new Vector2(0, -36);

            _pityText = UIFactory.AddText("Pity", _root.transform, "", 20, font, new Color(1f, 0.8f, 0.4f), TextAlignmentOptions.Center);
            var prt = _pityText.rectTransform;
            UIFactory.Anchor(prt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            prt.sizeDelta = new Vector2(600, 28); prt.anchoredPosition = new Vector2(0, -84);

            _crystalText = UIFactory.AddText("Crystal", _root.transform, "", 24, font, new Color(0.6f, 0.9f, 1f), TextAlignmentOptions.Right, FontStyles.Bold);
            var crt = _crystalText.rectTransform;
            UIFactory.Anchor(crt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            crt.sizeDelta = new Vector2(420, 36); crt.anchoredPosition = new Vector2(-30, -34);

            // 픽업 캐릭터 미리보기(중앙)
            _portrait = UIFactory.AddImage("Featured", _root.transform, new Color(1, 1, 1, 0.1f));
            var frt = _portrait.rectTransform;
            UIFactory.Anchor(frt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            frt.sizeDelta = new Vector2(340, 440); frt.anchoredPosition = new Vector2(0, 30);
            _portrait.preserveAspect = true;

            _featuredStars = UIFactory.AddText("FStars", _root.transform, "", 26, font, new Color(1f, 0.8f, 0.3f), TextAlignmentOptions.Center, FontStyles.Bold);
            var fsr = _featuredStars.rectTransform;
            UIFactory.Anchor(fsr, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            fsr.sizeDelta = new Vector2(400, 32); fsr.anchoredPosition = new Vector2(0, -200);

            _featuredName = UIFactory.AddText("FName", _root.transform, "", 30, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var fnr = _featuredName.rectTransform;
            UIFactory.Anchor(fnr, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            fnr.sizeDelta = new Vector2(500, 40); fnr.anchoredPosition = new Vector2(0, -236);

            _toast = UIFactory.AddText("Toast", _root.transform, "", 22, font, new Color(1f, 0.6f, 0.5f), TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = _toast.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            trt.sizeDelta = new Vector2(600, 30); trt.anchoredPosition = new Vector2(0, 220);

            // 뽑기 버튼
            _singleBtn = PullButton($"1회 뽑기\n<size=65%>크리스탈 {singleCost}</size>", new Color(0.45f, 0.3f, 0.75f, 0.95f), new Vector2(-170, 90), () => ShowConfirm(false));
            _multiBtn = PullButton($"10연 뽑기\n<size=65%>크리스탈 {multiCost}</size>", new Color(0.8f, 0.6f, 0.2f, 0.95f), new Vector2(170, 90), () => ShowConfirm(true));

            var charge = UIFactory.AddButton("Charge", _root.transform, $"충전(테스트) +{chargeAmount}", 18, font, new Color(1, 1, 1, 0.14f), Color.white);
            var chrt = (RectTransform)charge.transform;
            UIFactory.Anchor(chrt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            chrt.sizeDelta = new Vector2(260, 48); chrt.anchoredPosition = new Vector2(0, 30);
            charge.onClick.AddListener(() => { if (wallet != null) wallet.Add(chargeAmount); });

            // 홈 버튼(좌상단) — 내비게이터가 연결할 때만 표시
            _backBtn = UIFactory.AddButton("BackBtn", _root.transform, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            var bkrt = (RectTransform)_backBtn.transform;
            UIFactory.Anchor(bkrt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            bkrt.sizeDelta = new Vector2(150, 44); bkrt.anchoredPosition = new Vector2(30, -34);
            _backBtn.onClick.AddListener(() => _onBack?.Invoke());
            _backBtn.gameObject.SetActive(false);

            // 확률 보기 버튼(좌상단, 홈 버튼 옆)
            var ratesBtn = UIFactory.AddButton("RatesBtn", _root.transform, "확률 보기", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            var rbrt = (RectTransform)ratesBtn.transform;
            UIFactory.Anchor(rbrt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            rbrt.sizeDelta = new Vector2(150, 44); rbrt.anchoredPosition = new Vector2(196, -34);
            ratesBtn.onClick.AddListener(() => ShowRates(true));

            BuildRatesOverlay();
            BuildConfirm();
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
            var prt = panel.rectTransform;
            UIFactory.Anchor(prt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            prt.sizeDelta = new Vector2(640, 340); prt.anchoredPosition = Vector2.zero;

            _confirmTitle = UIFactory.AddText("Title", panel.transform, "", 34, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = _confirmTitle.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            trt.sizeDelta = new Vector2(600, 50); trt.anchoredPosition = new Vector2(0, -26);

            _confirmBody = UIFactory.AddText("Body", panel.transform, "", 26, font, Color.white, TextAlignmentOptions.Center);
            var brt = _confirmBody.rectTransform;
            UIFactory.Anchor(brt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            brt.sizeDelta = new Vector2(580, 110); brt.anchoredPosition = new Vector2(0, 14);
            _confirmBody.lineSpacing = 10f;

            var cancel = UIFactory.AddButton("Cancel", panel.transform, "취소", 26, font, new Color(0.35f, 0.37f, 0.44f, 0.98f), Color.white);
            var crt = (RectTransform)cancel.transform;
            UIFactory.Anchor(crt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            crt.sizeDelta = new Vector2(240, 68); crt.anchoredPosition = new Vector2(-135, 30);
            cancel.onClick.AddListener(() => _confirmRoot.SetActive(false));

            _confirmOk = UIFactory.AddButton("Ok", panel.transform, "확인", 26, font, new Color(0.25f, 0.62f, 0.95f, 0.98f), Color.white);
            var ort = (RectTransform)_confirmOk.transform;
            UIFactory.Anchor(ort, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            ort.sizeDelta = new Vector2(240, 68); ort.anchoredPosition = new Vector2(135, 30);
            _confirmOk.onClick.AddListener(OnConfirm);

            _confirmRoot.SetActive(false);
        }

        private void BuildRatesOverlay()
        {
            _ratesOverlay = UIFactory.AddImage("RatesOverlay", _root.transform, new Color(0, 0, 0, 0.88f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_ratesOverlay.transform);

            var title = UIFactory.AddText("Title", _ratesOverlay.transform, "확률 안내", 34, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var ttrt = title.rectTransform;
            UIFactory.Anchor(ttrt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            ttrt.sizeDelta = new Vector2(600, 50); ttrt.anchoredPosition = new Vector2(0, -120);

            _ratesText = UIFactory.AddText("Rates", _ratesOverlay.transform, "", 24, font, Color.white, TextAlignmentOptions.TopLeft);
            var rtrt = _ratesText.rectTransform;
            UIFactory.Anchor(rtrt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rtrt.sizeDelta = new Vector2(780, 440); rtrt.anchoredPosition = new Vector2(0, 10);
            _ratesText.lineSpacing = 8f;

            var close = UIFactory.AddButton("Close", _ratesOverlay.transform, "닫기", 24, font, new Color(1, 1, 1, 0.16f), Color.white);
            var crt = (RectTransform)close.transform;
            UIFactory.Anchor(crt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            crt.sizeDelta = new Vector2(220, 64); crt.anchoredPosition = new Vector2(0, 110);
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
            var cfg = gacha != null && gacha.Banner != null ? gacha.Banner.config : null;
            if (cfg == null) return "확률 정보를 찾을 수 없습니다.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<color=#FFC73F>6성</color>   {cfg.rate6:0.##}%");
            sb.AppendLine($"<size=68%><color=#B8B8B8>   소프트 천장 {cfg.sixStarSoftPityStart}회부터 상승 · 하드 천장 {cfg.sixStarHardPity}회 확정</color></size>");
            sb.AppendLine("");
            sb.AppendLine($"<color=#B86BFF>5성</color>   {cfg.rate5:0.##}%");
            sb.AppendLine($"<size=68%><color=#B8B8B8>   {cfg.fiveStarHardPity}연 내 5성 이상 보장</color></size>");
            sb.AppendLine("");
            sb.AppendLine($"<color=#549EFF>4성</color>   {cfg.rate4:0.##}%");
            sb.AppendLine("");
            sb.AppendLine($"<color=#9EAABF>3성</color>   {cfg.Rate3:0.##}%");
            sb.AppendLine("");
            sb.AppendLine($"<color=#FF9ED6>픽업</color>   6성의 {cfg.featured6Chance * 100f:0}%가 픽업 (실패 시 다음 6성 픽업 확정)");
            return sb.ToString();
        }

        private Button PullButton(string label, Color bg, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var btn = UIFactory.AddButton("Pull", _root.transform, label, 24, font, bg, Color.white);
            var rt = (RectTransform)btn.transform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            rt.sizeDelta = new Vector2(300, 90); rt.anchoredPosition = pos;
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
