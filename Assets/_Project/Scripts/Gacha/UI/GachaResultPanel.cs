using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.Characters;
using Sprites = SubcultureRPG.UI.ProceduralSprites;

namespace SubcultureRPG.Gacha.UI
{
    /// <summary>
    /// 가챠 결과 연출. GachaController.OnPulled 를 구독해 자동 재생합니다.
    ///  1) 잠긴 상자 → 자물쇠를 누르면 열쇠가 나타나 돌아가고 상자가 열림
    ///     (최고 등급에 따라 빛 색: 태생 6★ 금빛 · 5★ 보라 · 그 외 파랑)
    ///  2) 카드가 한 줄로 차례대로 나타나고, 카드마다 등급 색 세로 빛줄기
    ///  3) 태생 6★: 잠깐 멈칫 → 검은 화면에 대표 대사 → 금빛으로 밝아지며 일러스트 + 별 6개 → 일러스트가 카드 안으로
    /// 프리팹 없이 이 컴포넌트가 Canvas·상자·카드·버튼을 코드로 만듭니다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Gacha/Gacha Result Panel")]
    public class GachaResultPanel : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, Tooltip("비워 두면 씬에서 자동으로 찾습니다.")]
        private GachaController controller;
        [SerializeField, Tooltip("한글·★ 포함 TMP 폰트.")]
        private TMP_FontAsset font;

        [Header("연출")]
        [SerializeField, Tooltip("뽑을 때 결과 화면을 자동으로 표시.")] private bool autoPlayOnPull = true;
        [SerializeField] private float revealInterval = 0.11f;

        private static readonly Color Gold = new(1f, 0.80f, 0.30f);
        private static readonly Color Purple = new(0.74f, 0.47f, 1f);
        private static readonly Color Blue = new(0.40f, 0.65f, 1f);
        private static readonly Color Silver = new(0.62f, 0.72f, 0.84f);

        public static Color RarityColor(Rarity r) => r switch
        {
            Rarity.R6 => Gold, Rarity.R5 => Purple, Rarity.R4 => Blue, _ => Silver,
        };

        // UI
        private Canvas _canvas;
        private GameObject _root, _footer;
        private Image _tint, _flash;
        private RectTransform _beamLayer, _cardLayer;
        private Button _skipButton, _againButton;
        private TextMeshProUGUI _againLabel, _title;

        // 상자
        private RectTransform _chest, _lid, _lockRT, _shackle, _key, _aura, _rays;
        private CanvasGroup _chestGroup, _lockGroup, _keyGroup;
        private Image _auraImg;
        private TextMeshProUGUI _hint;
        private bool _lockClicked;

        // 6성 연출
        private RectTransform _cutIn, _cutArt;
        private Image _cutBg, _cutGlow, _cutArtImg;
        private TextMeshProUGUI _cutLine, _cutName, _cutArtFallback;
        private readonly List<TextMeshProUGUI> _cutStars = new();

        private class CardView
        {
            public GachaResult result;
            public RectTransform rt;
            public CanvasGroup group;
            public Image beam, glow;
            public Vector2 home;
        }
        private readonly List<CardView> _cards = new();

        private List<GachaResult> _results;
        private Coroutine _sequence;
        private bool _skip;
        private System.Action<bool> _pullHandler;
        private int _singleCost = 160, _multiCost = 1600;

        private void Reset() => controller = GetComponent<GachaController>();

        private void Awake()
        {
            if (controller == null) controller = FindAnyObjectByType<GachaController>();
            BuildUI();
            _root.SetActive(false);
        }

        private void OnEnable() { if (controller != null) controller.OnPulled += HandlePulled; }
        private void OnDisable() { if (controller != null) controller.OnPulled -= HandlePulled; }

        private void HandlePulled(List<GachaResult> results) { if (autoPlayOnPull) Show(results); }

        /// <summary>결과 화면 '다시 뽑기'를 외부(재화 확인·차감)에 위임. null이면 컨트롤러로 바로 뽑기.</summary>
        public void SetPullHandler(System.Action<bool> handler) => _pullHandler = handler;

        /// <summary>'다시 뽑기' 버튼에 보일 비용.</summary>
        public void SetPullCosts(int single, int multi) { _singleCost = single; _multiCost = multi; }

        private System.Func<bool, string> _costLabel;

        /// <summary>'다시 뽑기' 버튼의 비용 문구(multi → 리치 텍스트). 뽑기권/크리스탈 표시용. null이면 크리스탈 비용.</summary>
        public void SetCostLabel(System.Func<bool, string> label) => _costLabel = label;

        /// <summary>결과 표시 + 연출 시작.</summary>
        public void Show(List<GachaResult> results)
        {
            if (results == null || results.Count == 0) return;
            StopSequence();
            _results = new List<GachaResult>(results);
            _root.SetActive(true);
            _sequence = StartCoroutine(Sequence());
        }

        public void Close()
        {
            StopSequence();
            _root.SetActive(false);
        }

        // ================================================================
        //  시퀀스
        // ================================================================

        private Color BestColor()
        {
            int best = 3;
            foreach (var r in _results) best = Mathf.Max(best, (int)r.rarity);
            return best >= 6 ? Gold : best == 5 ? Purple : Blue;
        }

        private IEnumerator Sequence()
        {
            _skip = false;
            _lockClicked = false;
            _footer.SetActive(false);
            _skipButton.gameObject.SetActive(true);
            _tint.color = Clear(_tint.color);
            _flash.color = Clear(_flash.color);
            _cutIn.gameObject.SetActive(false);
            _title.text = _results.Count > 1 ? "10연 뽑기 결과" : "뽑기 결과";
            BuildCards();
            ResetChest();

            // 1) 상자: 자물쇠를 누를 때까지 대기
            _chest.gameObject.SetActive(true);
            yield return Fade(_chestGroup, 0f, 1f, 0.3f);
            while (!_lockClicked && !_skip)
            {
                float k = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
                _hint.alpha = k;
                _lockRT.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.unscaledTime * 6f));
                yield return null;
            }
            _hint.alpha = 0f;
            _lockRT.localScale = Vector3.one;
            yield return OpenChest(BestColor());

            // 2) 카드 공개
            for (int i = 0; i < _cards.Count; i++)
            {
                var v = _cards[i];
                if (_skip) { RevealInstant(v); continue; }

                if (v.result.rarity == Rarity.R6)
                {
                    // 빛줄기만 먼저 솟고, 잠깐 멈칫 → 6성 연출
                    yield return RaiseBeam(v, 0.35f);
                    yield return Wait(0.55f);
                    if (_skip) { RevealInstant(v); continue; }
                    yield return SixStarCutIn(v);
                    RevealInstant(v);
                    yield return Wait(0.2f);
                }
                else
                {
                    StartCoroutine(RaiseBeam(v, 0.3f));
                    yield return RevealCard(v, 0.22f);
                    yield return Wait(v.result.rarity == Rarity.R5 ? 0.28f : revealInterval);
                }
            }

            _skipButton.gameObject.SetActive(false);
            bool again10 = _results.Count > 1;
            string cost = _costLabel != null ? _costLabel(again10)
                : $"<color=#8FE9FF>크리스탈 {(again10 ? _multiCost : _singleCost):N0}</color>";
            _againLabel.text = $"{(again10 ? "10연" : "1회")} 다시 뽑기  <size=70%>{cost}</size>";
            _footer.SetActive(true);
            _sequence = null;
        }

        // ---------------- 상자 ----------------

        private void ResetChest()
        {
            _chestGroup.alpha = 0f;
            _lid.anchoredPosition = new Vector2(0, 70); _lid.localEulerAngles = Vector3.zero;
            _lid.GetComponent<CanvasGroup>().alpha = 1f;
            _lockGroup.alpha = 1f;
            _shackle.anchoredPosition = new Vector2(0, 44);
            _keyGroup.alpha = 0f;
            _key.anchoredPosition = new Vector2(260, -150); _key.localEulerAngles = Vector3.zero; _key.localScale = Vector3.one;
            _auraImg.color = Clear(_auraImg.color); _aura.localScale = Vector3.one * 0.6f;
            foreach (Transform r in _rays) r.GetComponent<Image>().color = Clear(Color.white);
            _hint.alpha = 1f;
        }

        private IEnumerator OpenChest(Color c)
        {
            if (!_skip)
            {
                // 열쇠가 나타나 자물쇠로
                // 열쇠 끝(피벗에서 17px)이 열쇠 구멍(상자 기준 0, 56)에 닿도록
                Vector2 keyStart = new(260, -150), keyEnd = new(-17, 56);
                for (float t = 0; t < 0.45f && !_skip; t += Dt)
                {
                    float k = EaseOut(t / 0.45f);
                    _keyGroup.alpha = Mathf.Clamp01(t / 0.2f);
                    _key.anchoredPosition = Vector2.Lerp(keyStart, keyEnd, k);
                    yield return null;
                }
                _key.anchoredPosition = keyEnd;
                // 돌리기(긴 축으로 반 바퀴 — 2D에서는 위아래로 뒤집히는 모양)
                for (float t = 0; t < 0.32f && !_skip; t += Dt)
                {
                    _key.localScale = new Vector3(1f, Mathf.Cos(Mathf.PI * EaseOut(t / 0.32f)), 1f);
                    yield return null;
                }
                _key.localScale = new Vector3(1f, -1f, 1f);
                // 자물쇠가 풀림
                for (float t = 0; t < 0.22f && !_skip; t += Dt)
                {
                    float k = t / 0.22f;
                    _shackle.anchoredPosition = new Vector2(0, 44 + 30 * k);
                    _lockGroup.alpha = 1f - k;
                    _keyGroup.alpha = 1f - k;
                    yield return null;
                }
            }
            _lockGroup.alpha = 0f; _keyGroup.alpha = 0f;

            // 뚜껑이 열리며 등급 색 빛
            var lidGroup = _lid.GetComponent<CanvasGroup>();
            float dur = _skip ? 0.15f : 0.55f;
            for (float t = 0; t < dur; t += Dt)
            {
                float k = EaseOut(t / dur);
                _lid.anchoredPosition = new Vector2(-30 * k, 70 + 120 * k);
                _lid.localEulerAngles = new Vector3(0, 0, 22f * k);
                lidGroup.alpha = 1f - 0.6f * k;
                _auraImg.color = new Color(c.r, c.g, c.b, 0.95f * k);
                _aura.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.8f, k);
                _rays.localEulerAngles = new Vector3(0, 0, 25f * k);
                foreach (Transform r in _rays) r.GetComponent<Image>().color = new Color(c.r, c.g, c.b, 0.55f * k);
                _flash.color = new Color(c.r, c.g, c.b, 0.45f * Mathf.Sin(k * Mathf.PI));
                _tint.color = new Color(c.r, c.g, c.b, 0.12f * k);
                yield return null;
            }
            _flash.color = Clear(_flash.color);
            if (!_skip) yield return Wait(0.3f);
            yield return Fade(_chestGroup, 1f, 0f, _skip ? 0.1f : 0.35f);
            _chest.gameObject.SetActive(false);
        }

        // ---------------- 카드 ----------------

        private void BuildCards()
        {
            foreach (var v in _cards)
            {
                if (v.rt != null) Destroy(v.rt.gameObject);
                if (v.beam != null) Destroy(v.beam.gameObject);
            }
            _cards.Clear();

            int n = _results.Count;
            float w = n == 1 ? 270f : Mathf.Min(170f, (1760f - (n - 1) * 14f) / n);
            float h = n == 1 ? 580f : w * 2.35f;
            float total = n * w + (n - 1) * 14f;
            for (int i = 0; i < n; i++)
            {
                var r = _results[i];
                float x = -total / 2f + w / 2f + i * (w + 14f);
                var v = new CardView { result = r, home = new Vector2(x, 10) };
                var col = RarityColor(r.rarity);

                // 세로 빛줄기(카드 뒤)
                v.beam = UIFactory.AddImage("Beam", _beamLayer, new Color(col.r, col.g, col.b, 0f), Sprites.Beam);
                v.beam.preserveAspect = false;
                var brt = v.beam.rectTransform;
                UIFactory.Anchor(brt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                brt.sizeDelta = new Vector2(w * 1.5f, 1200); brt.anchoredPosition = new Vector2(x, 40);
                brt.localScale = new Vector3(1, 0, 1);

                // 카드
                v.rt = UIFactory.NewRect("Card", _cardLayer);
                UIFactory.Anchor(v.rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                v.rt.sizeDelta = new Vector2(w, h); v.rt.anchoredPosition = v.home;
                v.group = v.rt.gameObject.AddComponent<CanvasGroup>();
                v.group.alpha = 0f;

                if ((int)r.rarity >= 5)
                {
                    v.glow = UIFactory.AddImage("Glow", v.rt, new Color(col.r, col.g, col.b, 0.55f), Sprites.Glow);
                    v.glow.preserveAspect = false;
                    UIFactory.Stretch(v.glow.rectTransform, -w * 0.35f, -h * 0.12f, -w * 0.35f, -h * 0.12f);
                }

                var frame = UIFactory.AddImage("Frame", v.rt, col, Sprites.Rounded);
                frame.type = Image.Type.Sliced; frame.preserveAspect = false;
                UIFactory.Stretch(frame.rectTransform);
                var inner = UIFactory.AddImage("Inner", frame.transform, new Color(0.10f, 0.11f, 0.16f, 1f));
                UIFactory.Stretch(inner.rectTransform, 4, 4, 4, 4);

                // 일러스트
                var sprite = GameArt.Portrait(r.character);
                var art = UIFactory.AddImage("Art", inner.transform, Color.white, sprite);
                UIFactory.Stretch(art.rectTransform, 2, 2, 2, 2);
                art.enabled = sprite != null;
                if (sprite == null)
                {
                    string initial = r.character != null && !string.IsNullOrEmpty(r.character.displayName) ? r.character.displayName.Substring(0, 1) : "?";
                    var ph = UIFactory.AddText("Initial", inner.transform, initial, w * 0.5f, font, new Color(col.r, col.g, col.b, 0.5f), TextAlignmentOptions.Center, FontStyles.Bold);
                    UIFactory.Stretch(ph.rectTransform, 0, h * 0.2f, 0, 0);
                }

                // 하단 띠: 별 + 이름
                var band = UIFactory.AddImage("Band", inner.transform, new Color(0, 0, 0, 0.62f));
                var bdrt = band.rectTransform;
                UIFactory.Anchor(bdrt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
                bdrt.sizeDelta = new Vector2(0, Mathf.Max(54, h * 0.16f)); bdrt.anchoredPosition = Vector2.zero;
                string hex = "#" + ColorUtility.ToHtmlStringRGB(col);
                var stars = UIFactory.AddText("Stars", band.transform, $"<color={hex}>{new string('★', (int)r.rarity)}</color>", Mathf.Clamp(w * 0.11f, 13, 24), font, Color.white, TextAlignmentOptions.Top, FontStyles.Bold);
                UIFactory.Stretch(stars.rectTransform, 2, 0, 2, 4);
                var name = UIFactory.AddText("Name", band.transform, r.character != null ? r.character.displayName : "???", Mathf.Clamp(w * 0.105f, 13, 24), font, Color.white, TextAlignmentOptions.Bottom, FontStyles.Bold);
                UIFactory.Stretch(name.rectTransform, 2, 5, 2, 0);

                if (r.isFeatured && r.rarity == Rarity.R6)
                {
                    var tag = UIFactory.AddImage("PickUp", inner.transform, new Color(1f, 0.45f, 0.6f, 0.95f), Sprites.Rounded);
                    tag.type = Image.Type.Sliced; tag.preserveAspect = false;
                    var trt = tag.rectTransform;
                    UIFactory.Anchor(trt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
                    trt.sizeDelta = new Vector2(Mathf.Min(w - 12, 120), 30); trt.anchoredPosition = new Vector2(0, -8);
                    var tt = UIFactory.AddText("T", tag.transform, "PICK UP", 15, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                    UIFactory.Stretch(tt.rectTransform);
                }
                _cards.Add(v);
            }
        }

        private IEnumerator RaiseBeam(CardView v, float dur)
        {
            var col = RarityColor(v.result.rarity);
            float peak = v.result.rarity switch { Rarity.R6 => 0.85f, Rarity.R5 => 0.7f, Rarity.R4 => 0.5f, _ => 0.32f };
            var rt = v.beam.rectTransform;
            for (float t = 0; t < dur; t += Dt)
            {
                float k = EaseOut(t / dur);
                rt.localScale = new Vector3(1, k, 1);
                v.beam.color = new Color(col.r, col.g, col.b, peak * k);
                yield return null;
            }
            rt.localScale = Vector3.one;
            v.beam.color = new Color(col.r, col.g, col.b, peak);
        }

        private IEnumerator RevealCard(CardView v, float dur)
        {
            for (float t = 0; t < dur; t += Dt)
            {
                float k = EaseOut(t / dur);
                v.group.alpha = k;
                v.rt.anchoredPosition = v.home + new Vector2(0, 40f * (1f - k));
                v.rt.localScale = Vector3.one * Mathf.Lerp(1.1f, 1f, k);
                yield return null;
            }
            RevealInstant(v);
        }

        private void RevealInstant(CardView v)
        {
            v.group.alpha = 1f;
            v.rt.anchoredPosition = v.home;
            v.rt.localScale = Vector3.one;
            var col = RarityColor(v.result.rarity);
            float peak = v.result.rarity switch { Rarity.R6 => 0.85f, Rarity.R5 => 0.7f, Rarity.R4 => 0.5f, _ => 0.32f };
            v.beam.rectTransform.localScale = Vector3.one;
            v.beam.color = new Color(col.r, col.g, col.b, peak);
        }

        // ---------------- 6성 연출 ----------------

        private IEnumerator SixStarCutIn(CardView v)
        {
            var c = v.result.character;
            _cutIn.gameObject.SetActive(true);
            _cutBg.color = new Color(0, 0, 0, 0);
            _cutGlow.color = Clear(Gold);
            _cutLine.alpha = 0f;
            _cutName.alpha = 0f;
            foreach (var s in _cutStars) { s.alpha = 0f; s.rectTransform.localScale = Vector3.one; }
            var sprite = GameArt.Portrait(c);
            bool live = SubcultureRPG.UI.Live2DUI.Show(_cutArtImg, c);   // Live2D 모델이 있으면 움직이는 모델로 등장
            SubcultureRPG.UI.Live2DUI.SetAlpha(_cutArtImg, 0f);
            _cutArtImg.sprite = sprite;
            _cutArtImg.enabled = !live && sprite != null;
            _cutArtImg.color = new Color(1, 1, 1, 0);
            _cutArtFallback.text = !live && sprite == null && c != null ? c.displayName : "";
            _cutArtFallback.alpha = 0f;
            _cutArt.anchoredPosition = new Vector2(0, 40);
            _cutArt.sizeDelta = new Vector2(640, 860);
            _cutArt.localScale = Vector3.one;

            // 암전
            for (float t = 0; t < 0.3f; t += Dt) { _cutBg.color = new Color(0, 0, 0, t / 0.3f); yield return null; }
            _cutBg.color = Color.black;

            // 대표 대사
            _cutLine.text = c != null ? $"「 {c.SignatureLine} 」" : "";
            yield return FadeText(_cutLine, 0f, 1f, 0.5f);
            yield return Wait(1.1f);

            // 금빛으로 밝아짐 + 일러스트
            var warm = new Color(0.20f, 0.14f, 0.04f, 1f);
            for (float t = 0; t < 0.7f; t += Dt)
            {
                float k = EaseOut(t / 0.7f);
                _cutLine.alpha = 1f - Mathf.Clamp01(t / 0.3f);
                _cutBg.color = Color.Lerp(Color.black, warm, k);
                _cutGlow.color = new Color(Gold.r, Gold.g, Gold.b, 0.9f * k);
                _cutGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.25f, k);
                _cutArtImg.color = new Color(1, 1, 1, k);
                SubcultureRPG.UI.Live2DUI.SetAlpha(_cutArtImg, k);
                _cutArtFallback.alpha = k;
                _cutArt.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, k);
                yield return null;
            }
            _cutName.text = c != null ? c.displayName : "";
            StartCoroutine(FadeText(_cutName, 0f, 1f, 0.4f));

            // 별 6개가 차례로
            foreach (var s in _cutStars)
            {
                for (float t = 0; t < 0.14f; t += Dt)
                {
                    float k = EaseOut(t / 0.14f);
                    s.alpha = k;
                    s.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, k);
                    yield return null;
                }
                s.alpha = 1f; s.rectTransform.localScale = Vector3.one;
                yield return Wait(0.06f);
            }
            yield return Wait(_skip ? 0f : 1.0f);

            // 일러스트가 카드 안으로
            Vector3 world = v.rt.TransformPoint(v.rt.rect.center);
            Vector2 target = _cutIn.InverseTransformPoint(world);
            Vector2 fromPos = _cutArt.anchoredPosition, fromSize = _cutArt.sizeDelta;
            for (float t = 0; t < 0.5f; t += Dt)
            {
                float k = EaseInOut(t / 0.5f);
                _cutArt.anchoredPosition = Vector2.Lerp(fromPos, target, k);
                _cutArt.sizeDelta = Vector2.Lerp(fromSize, v.rt.sizeDelta, k);
                _cutBg.color = new Color(_cutBg.color.r, _cutBg.color.g, _cutBg.color.b, 1f - k);
                _cutGlow.color = new Color(Gold.r, Gold.g, Gold.b, 0.9f * (1f - k));
                _cutName.alpha = 1f - k;
                foreach (var s in _cutStars) s.alpha = 1f - k;
                if (k > 0.6f) v.group.alpha = (k - 0.6f) / 0.4f;   // 카드가 서서히 나타나며 일러스트를 받음
                yield return null;
            }
            SubcultureRPG.UI.Live2DUI.Hide(_cutArtImg);
            _cutIn.gameObject.SetActive(false);
        }

        // ================================================================
        //  UI 구성
        // ================================================================

        private void BuildUI()
        {
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

            _root = UIFactory.AddImage("Root", _canvas.transform, new Color(0.03f, 0.035f, 0.06f, 0.97f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            var root = _root.transform;

            _tint = UIFactory.AddImage("Tint", root, Clear(Color.white));
            UIFactory.Stretch(_tint.rectTransform);

            _title = UIFactory.AddText("Title", root, "뽑기 결과", 30, font, new Color(0.85f, 0.9f, 1f), TextAlignmentOptions.Left, FontStyles.Bold);
            var trt = _title.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            trt.sizeDelta = new Vector2(600, 44); trt.anchoredPosition = new Vector2(40, -30);

            _beamLayer = UIFactory.NewRect("Beams", root);
            UIFactory.Stretch(_beamLayer);
            _cardLayer = UIFactory.NewRect("Cards", root);
            UIFactory.Stretch(_cardLayer);

            BuildChest(root);
            BuildCutIn(root);

            _skipButton = UIFactory.AddButton("Skip", root, "SKIP ▶▶", 22, font, new Color(1, 1, 1, 0.16f), Color.white);
            var skRt = (RectTransform)_skipButton.transform;
            UIFactory.Anchor(skRt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            skRt.sizeDelta = new Vector2(160, 52); skRt.anchoredPosition = new Vector2(-30, -30);
            _skipButton.onClick.AddListener(() => _skip = true);

            BuildFooter(root);

            _flash = UIFactory.AddImage("Flash", root, Clear(Color.white));
            UIFactory.Stretch(_flash.rectTransform);
        }

        private void BuildChest(Transform root)
        {
            _chest = UIFactory.NewRect("Chest", root);
            UIFactory.Anchor(_chest, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _chest.sizeDelta = new Vector2(600, 600); _chest.anchoredPosition = new Vector2(0, -30);
            _chestGroup = _chest.gameObject.AddComponent<CanvasGroup>();

            _aura = UIFactory.NewRect("Aura", _chest);
            UIFactory.Anchor(_aura, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _aura.sizeDelta = new Vector2(760, 760); _aura.anchoredPosition = new Vector2(0, 60);
            _auraImg = _aura.gameObject.AddComponent<Image>();
            _auraImg.sprite = Sprites.Glow; _auraImg.raycastTarget = false; _auraImg.color = Clear(Color.white);

            _rays = UIFactory.NewRect("Rays", _chest);
            UIFactory.Anchor(_rays, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _rays.anchoredPosition = new Vector2(0, 70);
            for (int i = 0; i < 8; i++)
            {
                var ray = UIFactory.AddImage("Ray", _rays, Clear(Color.white), Sprites.Beam);
                ray.preserveAspect = false;
                var rrt = ray.rectTransform;
                UIFactory.Anchor(rrt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                rrt.sizeDelta = new Vector2(46, 900); rrt.localEulerAngles = new Vector3(0, 0, i * 22.5f);
            }

            var shadow = UIFactory.AddImage("Shadow", _chest, new Color(0, 0, 0, 0.5f), Sprites.Circle);
            Center(shadow.rectTransform, 0, -132, 440, 60);

            var wood = new Color(0.44f, 0.27f, 0.13f);
            var woodDark = new Color(0.30f, 0.18f, 0.08f);
            var brass = new Color(0.92f, 0.72f, 0.30f);

            // 몸체
            var body = Rounded(_chest, "Body", wood, 0, -30, 400, 200);
            Rounded(body.transform, "Panel", woodDark, 0, -6, 360, 150);
            Rounded(body.transform, "BandL", brass, -130, 0, 30, 200);
            Rounded(body.transform, "BandR", brass, 130, 0, 30, 200);
            Rounded(body.transform, "Trim", brass, 0, -92, 400, 18);

            // 뚜껑(아래쪽 경첩 기준으로 열림)
            _lid = UIFactory.NewRect("Lid", _chest);
            UIFactory.Anchor(_lid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f));
            _lid.sizeDelta = new Vector2(420, 110); _lid.anchoredPosition = new Vector2(0, 70);
            _lid.gameObject.AddComponent<CanvasGroup>();
            var lidImg = Rounded(_lid, "LidBody", new Color(0.52f, 0.32f, 0.16f), 0, 0, 420, 110);
            UIFactory.Stretch(lidImg.rectTransform);
            Rounded(_lid, "LidBandL", brass, -136, 0, 30, 110).rectTransform.anchoredPosition = new Vector2(-136, 0);
            Rounded(_lid, "LidBandR", brass, 136, 0, 30, 110).rectTransform.anchoredPosition = new Vector2(136, 0);
            var lidTrim = Rounded(_lid, "LidTrim", brass, 0, 0, 420, 16);
            UIFactory.Anchor(lidTrim.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            lidTrim.rectTransform.anchoredPosition = Vector2.zero;

            // 자물쇠(버튼)
            _lockRT = UIFactory.NewRect("Lock", _chest);
            UIFactory.Anchor(_lockRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _lockRT.sizeDelta = new Vector2(110, 150); _lockRT.anchoredPosition = new Vector2(0, 62);
            _lockGroup = _lockRT.gameObject.AddComponent<CanvasGroup>();
            var shackleImg = UIFactory.AddImage("Shackle", _lockRT, new Color(0.82f, 0.84f, 0.9f), Sprites.Ring);
            _shackle = shackleImg.rectTransform;
            Center(_shackle, 0, 44, 70, 70);
            var plate = Rounded(_lockRT, "Plate", brass, 0, -14, 92, 92);
            plate.raycastTarget = true;
            var keyhole = UIFactory.AddImage("Keyhole", plate.transform, new Color(0.12f, 0.08f, 0.04f), Sprites.Circle);
            Center(keyhole.rectTransform, 0, 8, 22, 22);
            var slot = UIFactory.AddImage("Slot", plate.transform, new Color(0.12f, 0.08f, 0.04f));
            Center(slot.rectTransform, 0, -10, 9, 26);
            var lockBtn = plate.gameObject.AddComponent<Button>();
            lockBtn.targetGraphic = plate;
            lockBtn.onClick.AddListener(() => _lockClicked = true);

            // 열쇠(처음엔 숨김, 오른쪽 아래에서 자물쇠로)
            _key = UIFactory.NewRect("Key", _chest);
            UIFactory.Anchor(_key, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f));
            _key.sizeDelta = new Vector2(150, 60);
            _keyGroup = _key.gameObject.AddComponent<CanvasGroup>();
            var keyCol = new Color(1f, 0.86f, 0.45f);
            var bow = UIFactory.AddImage("Bow", _key, keyCol, Sprites.Ring);
            Center(bow.rectTransform, 50, 0, 56, 56);
            var shaft = UIFactory.AddImage("Shaft", _key, keyCol);
            Center(shaft.rectTransform, -15, 0, 86, 12);
            var tooth1 = UIFactory.AddImage("Tooth1", _key, keyCol);
            Center(tooth1.rectTransform, -48, -12, 10, 16);
            var tooth2 = UIFactory.AddImage("Tooth2", _key, keyCol);
            Center(tooth2.rectTransform, -32, -10, 10, 12);

            _hint = UIFactory.AddText("Hint", _chest, "자물쇠를 눌러 상자를 여세요", 28, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Center(_hint.rectTransform, 0, -200, 700, 44);

            _chest.gameObject.SetActive(false);
        }

        private void BuildCutIn(Transform root)
        {
            _cutIn = UIFactory.NewRect("SixStarCutIn", root);
            UIFactory.Stretch(_cutIn);
            _cutBg = _cutIn.gameObject.AddComponent<Image>();
            _cutBg.color = Color.black; _cutBg.raycastTarget = true;

            _cutGlow = UIFactory.AddImage("Glow", _cutIn, Clear(Gold), Sprites.Glow);
            Center(_cutGlow.rectTransform, 0, 40, 1500, 1500);

            _cutArt = UIFactory.NewRect("Art", _cutIn);
            UIFactory.Anchor(_cutArt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _cutArtImg = _cutArt.gameObject.AddComponent<Image>();
            _cutArtImg.preserveAspect = true; _cutArtImg.raycastTarget = false;
            _cutArtFallback = UIFactory.AddText("Fallback", _cutArt, "", 120, font, new Color(1f, 0.85f, 0.45f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_cutArtFallback.rectTransform);
            _cutArtFallback.enableAutoSizing = true; _cutArtFallback.fontSizeMin = 20; _cutArtFallback.fontSizeMax = 120;

            _cutLine = UIFactory.AddText("Line", _cutIn, "", 40, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Center(_cutLine.rectTransform, 0, 0, 1500, 160);

            for (int i = 0; i < 6; i++)
            {
                var s = UIFactory.AddText("Star", _cutIn, "★", 58, font, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
                Center(s.rectTransform, -175 + i * 70, -380, 70, 70);
                _cutStars.Add(s);
            }
            _cutName = UIFactory.AddText("Name", _cutIn, "", 46, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Center(_cutName.rectTransform, 0, -450, 900, 60);

            // 연출 중 클릭 = 빨리 넘기기
            var skipArea = _cutIn.gameObject.AddComponent<Button>();
            skipArea.transition = Selectable.Transition.None;
            skipArea.targetGraphic = _cutBg;
            skipArea.onClick.AddListener(() => _skip = true);

            _cutIn.gameObject.SetActive(false);
        }

        private void BuildFooter(Transform root)
        {
            _footer = UIFactory.NewRect("Footer", root).gameObject;
            var frt = (RectTransform)_footer.transform;
            UIFactory.Anchor(frt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            frt.sizeDelta = new Vector2(900, 80); frt.anchoredPosition = new Vector2(0, 46);
            var layout = _footer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 30; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;

            var ok = UIFactory.AddButton("Ok", _footer.transform, "확인", 26, font, new Color(0.22f, 0.24f, 0.30f, 0.98f), Color.white);
            var le = ok.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 300; le.preferredHeight = 70;
            ok.onClick.AddListener(Close);

            _againButton = UIFactory.AddButton("Again", _footer.transform, "", 24, font, new Color(0.25f, 0.62f, 0.95f, 0.98f), Color.white);
            var le2 = _againButton.gameObject.AddComponent<LayoutElement>(); le2.preferredWidth = 360; le2.preferredHeight = 70;
            _againLabel = _againButton.GetComponentInChildren<TextMeshProUGUI>();
            _againButton.onClick.AddListener(() =>
            {
                bool multi = _results != null && _results.Count > 1;
                if (_pullHandler != null) { _pullHandler(multi); return; }
                if (controller == null) return;
                if (multi) controller.PullMulti(); else controller.PullOnce();
            });
            _footer.SetActive(false);
        }

        // ================================================================
        //  헬퍼
        // ================================================================

        private Image Rounded(Transform parent, string name, Color c, float x, float y, float w, float h)
        {
            var img = UIFactory.AddImage(name, parent, c, Sprites.Rounded);
            img.type = Image.Type.Sliced; img.preserveAspect = false;
            img.pixelsPerUnitMultiplier = 2f;
            Center(img.rectTransform, x, y, w, h);
            return img;
        }

        private static void Center(RectTransform rt, float x, float y, float w, float h)
        {
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, y);
        }

        private static Color Clear(Color c) => new(c.r, c.g, c.b, 0f);
        private static float Dt => Time.unscaledDeltaTime;
        private static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        private static float EaseInOut(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        private IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds && !_skip; t += Dt) yield return null;
        }

        private IEnumerator Fade(CanvasGroup g, float from, float to, float dur)
        {
            for (float t = 0; t < dur; t += Dt) { g.alpha = Mathf.Lerp(from, to, t / dur); yield return null; }
            g.alpha = to;
        }

        private IEnumerator FadeText(TextMeshProUGUI txt, float from, float to, float dur)
        {
            for (float t = 0; t < dur; t += Dt) { txt.alpha = Mathf.Lerp(from, to, t / dur); yield return null; }
            txt.alpha = to;
        }

        private void StopSequence()
        {
            if (_sequence != null) { StopCoroutine(_sequence); _sequence = null; }
            StopAllCoroutines();
            if (_cutIn != null) _cutIn.gameObject.SetActive(false);
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
