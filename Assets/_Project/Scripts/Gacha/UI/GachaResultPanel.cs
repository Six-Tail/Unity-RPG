using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Gacha.UI
{
    /// <summary>
    /// 가챠 결과 화면. GachaController.OnPulled 를 구독해 카드 그리드로 공개 연출합니다.
    /// 프리팹/씬 배치 없이, 이 컴포넌트 하나가 Canvas·카드·버튼을 코드로 만듭니다.
    /// 사용: 빈 GameObject에 추가 → controller 연결(비우면 자동 탐색) → 뽑으면 자동 표시.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Gacha/Gacha Result Panel")]
    public class GachaResultPanel : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, Tooltip("비워 두면 씬에서 자동으로 찾습니다.")]
        private GachaController controller;
        [SerializeField, Tooltip("성급 색 팔레트. 비우면 기본값 사용.")]
        private GachaRarityPalette palette;
        [SerializeField, Tooltip("한글 이름 표시에 쓸 TMP 폰트(비우면 TMP 기본 폰트). CJK + ★ 포함 폰트 권장.")]
        private TMP_FontAsset font;

        [Header("연출 타이밍")]
        [SerializeField] private float revealInterval = 0.12f;
        [SerializeField, Tooltip("5성 이상 공개 뒤 추가 여유(초).")] private float revealIntervalHigh = 0.45f;
        [SerializeField, Tooltip("뽑을 때 결과 화면을 자동으로 표시.")] private bool autoPlayOnPull = true;

        [Header("레이아웃")]
        [SerializeField] private int columns = 5;
        [SerializeField] private Vector2 cellSize = new(150, 200);
        [SerializeField] private Vector2 spacing = new(12, 12);

        private Canvas _canvas;
        private GameObject _root;
        private RectTransform _grid;
        private GridLayoutGroup _gridLayout;
        private Image _screenFlash;
        private TextMeshProUGUI _bannerText;
        private RectTransform _banner;
        private Button _skipButton;
        private GameObject _footer;

        private readonly List<GachaResultCard> _cards = new();
        private Coroutine _sequence;
        private bool _skipRequested;

        private void Reset() => controller = GetComponent<GachaController>();

        private void Awake()
        {
            if (controller == null) controller = FindAnyObjectByType<GachaController>();
            if (palette == null) palette = GachaRarityPalette.CreateDefault();
            if (font == null && TMP_Settings.defaultFontAsset == null)
                Debug.LogWarning("[GachaResultPanel] TMP 기본 폰트가 없습니다. 'Window > TextMeshPro > Import TMP Essential Resources' 실행 후, 한글은 CJK 폰트를 font 필드에 연결하세요.");

            BuildUI();
            HideImmediate();
        }

        private void OnEnable()
        {
            if (controller != null) controller.OnPulled += HandlePulled;
        }

        private void OnDisable()
        {
            if (controller != null) controller.OnPulled -= HandlePulled;
        }

        private void HandlePulled(List<GachaResult> results)
        {
            if (autoPlayOnPull) Show(results);
        }

        /// <summary>결과 표시 + 공개 연출 시작.</summary>
        public void Show(List<GachaResult> results)
        {
            if (results == null || results.Count == 0) return;
            StopSequence();

            _root.SetActive(true);
            _footer.SetActive(false);
            _skipButton.gameObject.SetActive(true);
            _screenFlash.color = new Color(1, 1, 1, 0);
            SetBannerAlpha(0f);

            BuildCards(results);
            _sequence = StartCoroutine(RevealSequence());
        }

        /// <summary>화면 닫기.</summary>
        public void Close()
        {
            StopSequence();
            HideImmediate();
        }

        // ---------------- 공개 시퀀스 ----------------

        private IEnumerator RevealSequence()
        {
            _skipRequested = false;
            yield return Wait(0.15f);

            var gold = new Color(1f, 0.8f, 0.3f);
            var purple = new Color(0.75f, 0.45f, 1f);

            GachaResultCard best = null;
            int bestRarity = 0;

            foreach (var card in _cards)
            {
                var r = card.Result;

                if (_skipRequested)
                {
                    card.Reveal(instant: true);
                }
                else
                {
                    card.Reveal(instant: false);

                    if (r.rarity == Rarity.R6 && r.isFeatured)
                        yield return EmphasizePickup("PICK UP 획득!");
                    else if (r.rarity == Rarity.R6)
                        yield return EmphasizeRare(gold);
                    else if (r.rarity == Rarity.R5)
                        yield return EmphasizeRare(purple);

                    float gap = (int)r.rarity >= 5 ? revealIntervalHigh : revealInterval;
                    yield return Wait(gap);
                }

                if ((int)r.rarity > bestRarity)
                {
                    bestRarity = (int)r.rarity;
                    best = card;
                }
            }

            // 스킵이었다면 강조를 건너뛰었으니, 최고 등급만 가볍게 마무리 강조
            if (_skipRequested && best != null && bestRarity >= 5)
            {
                bool feat6 = best.Result.rarity == Rarity.R6 && best.Result.isFeatured;
                yield return feat6 ? EmphasizePickup("PICK UP 획득!") : EmphasizeRare(gold);
            }

            _skipButton.gameObject.SetActive(false);
            _footer.SetActive(true);
            _sequence = null;
        }

        private IEnumerator EmphasizeRare(Color tint)
        {
            float t = 0f, dur = 0.25f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                var c = tint; c.a = 0.18f * Mathf.Sin(k * Mathf.PI); _screenFlash.color = c;
                yield return null;
            }
            _screenFlash.color = new Color(1, 1, 1, 0);
        }

        private IEnumerator EmphasizePickup(string text)
        {
            _bannerText.text = text;
            var gold = new Color(1f, 0.85f, 0.4f);

            float t = 0f, dur = 0.5f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                var c = gold; c.a = 0.6f * (1f - k); _screenFlash.color = c;
                SetBannerAlpha(Mathf.Sin(k * Mathf.PI));
                _banner.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.1f, k);
                yield return null;
            }
            _screenFlash.color = new Color(1, 1, 1, 0);

            yield return Wait(0.35f);

            t = 0f; dur = 0.3f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                SetBannerAlpha(1f - Mathf.Clamp01(t / dur));
                yield return null;
            }
            SetBannerAlpha(0f);
            _banner.localScale = Vector3.one;
        }

        private void RequestSkip() => _skipRequested = true;

        // ---------------- UI 구성 ----------------

        private void BuildUI()
        {
            // 전용 오버레이 Canvas (게임 UI 위에 뜨도록 sortingOrder 높게)
            var cgo = UIFactory.NewRect("GachaResultCanvas", transform);
            _canvas = cgo.gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", _canvas.transform, new Color(0, 0, 0, 0)).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);

            // 딤 배경(뒤쪽 클릭 차단)
            var dim = UIFactory.AddImage("Dim", _root.transform, new Color(0.03f, 0.03f, 0.05f, 0.88f), null, raycast: true);
            UIFactory.Stretch(dim.rectTransform);

            // 타이틀
            var title = UIFactory.AddText("Title", _root.transform, "뽑기 결과", 40, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = title.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            trt.sizeDelta = new Vector2(600, 60);
            trt.anchoredPosition = new Vector2(0, -40);

            // 카드 그리드
            _grid = UIFactory.NewRect("Grid", _root.transform);
            UIFactory.Anchor(_grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _gridLayout = _grid.gameObject.AddComponent<GridLayoutGroup>();
            _gridLayout.cellSize = cellSize;
            _gridLayout.spacing = spacing;
            _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _gridLayout.constraintCount = Mathf.Max(1, columns);
            _gridLayout.childAlignment = TextAnchor.MiddleCenter;

            // 픽업 배너
            _banner = UIFactory.NewRect("Banner", _root.transform);
            UIFactory.Anchor(_banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _banner.sizeDelta = new Vector2(1000, 120);
            _banner.anchoredPosition = new Vector2(0, 260);
            _bannerText = UIFactory.AddText("Text", _banner.transform, "PICK UP!", 64, font, new Color(1f, 0.85f, 0.35f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_bannerText.rectTransform);
            SetBannerAlpha(0f);

            // 스킵 버튼(우상단)
            _skipButton = UIFactory.AddButton("Skip", _root.transform, "SKIP ▶▶", 22, font, new Color(1, 1, 1, 0.12f), Color.white);
            var skRt = (RectTransform)_skipButton.transform;
            UIFactory.Anchor(skRt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            skRt.sizeDelta = new Vector2(150, 50);
            skRt.anchoredPosition = new Vector2(-30, -34);
            _skipButton.onClick.AddListener(RequestSkip);

            BuildFooter();

            // 화면 플래시(최상단, 클릭 통과)
            _screenFlash = UIFactory.AddImage("ScreenFlash", _root.transform, new Color(1, 1, 1, 0), null, raycast: false);
            UIFactory.Stretch(_screenFlash.rectTransform);
        }

        private void BuildFooter()
        {
            _footer = UIFactory.NewRect("Footer", _root.transform).gameObject;
            var frt = (RectTransform)_footer.transform;
            UIFactory.Anchor(frt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            frt.sizeDelta = new Vector2(760, 80);
            frt.anchoredPosition = new Vector2(0, 50);

            var layout = _footer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var purple = new Color(0.45f, 0.30f, 0.75f, 0.95f);
            var gold = new Color(0.80f, 0.60f, 0.20f, 0.95f);
            var gray = new Color(1, 1, 1, 0.14f);

            MakeFooterButton("1회 뽑기", purple, () => RequestPull(false));
            MakeFooterButton("10연 뽑기", gold, () => RequestPull(true));
            MakeFooterButton("닫기", gray, Close);
        }

        private System.Action<bool> _pullHandler;

        /// <summary>하단 재뽑기 버튼을 외부(재화 차감 등)에 위임. null이면 컨트롤러로 바로 뽑기.</summary>
        public void SetPullHandler(System.Action<bool> handler) => _pullHandler = handler;

        private void RequestPull(bool multi)
        {
            if (_pullHandler != null) { _pullHandler(multi); return; }
            if (controller == null) return;
            if (multi) controller.PullMulti(); else controller.PullOnce();
        }

        private void MakeFooterButton(string label, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var btn = UIFactory.AddButton(label, _footer.transform, label, 24, font, bg, Color.white);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 220;
            le.preferredHeight = 64;
            btn.onClick.AddListener(onClick);
        }

        private void BuildCards(List<GachaResult> results)
        {
            // 기존 카드 즉시 분리 후 파괴(1프레임 레이아웃 깜빡임 방지)
            for (int i = _grid.childCount - 1; i >= 0; i--)
            {
                var ch = _grid.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
            _cards.Clear();

            int cols = Mathf.Max(1, Mathf.Min(columns, results.Count));
            _gridLayout.constraintCount = cols;
            int rows = Mathf.CeilToInt(results.Count / (float)cols);
            _grid.sizeDelta = new Vector2(
                cols * cellSize.x + (cols - 1) * spacing.x,
                rows * cellSize.y + (rows - 1) * spacing.y);

            foreach (var r in results)
            {
                var card = GachaResultCard.Create(_grid, palette, font);
                card.Bind(r);
                _cards.Add(card);
            }
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }

        // ---------------- 유틸 ----------------

        private void SetBannerAlpha(float a)
        {
            var c = _bannerText.color; c.a = a; _bannerText.color = c;
        }

        private void HideImmediate()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void StopSequence()
        {
            if (_sequence != null) { StopCoroutine(_sequence); _sequence = null; }
        }

        private static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }
    }
}
