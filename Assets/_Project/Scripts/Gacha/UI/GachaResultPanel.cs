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
    ///  1) 소환 계약서(마법 스크롤)가 펼쳐짐 → 서명란을 문질러(또는 눌러) 서명 → 인장이 찍히고 서명이 빛남
    ///     → 스크롤이 아래에서부터 타올라 사라지며 마법진에서 빛기둥
    ///     (최고 등급에 따라 빛 색: 태생 6★ 금빛 · 5★ 보라 · 그 외 파랑)
    ///  2) 카드가 한 줄로 차례대로 나타나고, 카드마다 등급 색 세로 빛줄기
    ///  3) 태생 6★: 잠깐 멈칫 → 검은 화면에 대표 대사 → 금빛으로 밝아지며 일러스트 + 별 6개 → 일러스트가 카드 안으로
    /// 프리팹 없이 이 컴포넌트가 Canvas·스크롤·카드·버튼을 코드로 만듭니다.
    /// 그림: Resources/Art/Gacha/{scroll · circle · quill · seal}.png, Backgrounds/gacha_summon (없으면 단색 도형으로 대신).
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
        // 소환 계약서(마법 스크롤) 의식
        private RectTransform _ritual, _scrollClip, _scroll, _inkLayer, _quill, _seal, _circle, _pillar, _burnLine, _emberLayer;
        private CanvasGroup _ritualGroup, _scrollTextGroup, _quillGroup;
        private Image _circleImg, _pillarImg, _burnImg, _sealImg;
        private TextMeshProUGUI _hint, _scrollCount;
        private readonly List<Image> _ink = new();
        private float _inkLength;
        private Vector2 _lastInk;
        private bool _hasLastInk, _signed, _signing, _autoSign;
        private const float ScrollW = 1100f, ScrollH = 619f, ClipPad = 30f;
        private const float SignNeed = 520f;            // 이만큼(px) 그으면 서명 완료
        private static readonly Rect SignBand = new(-410f, -238f, 820f, 150f);   // 스크롤 가운데 기준 서명란
        private static readonly Color InkColor = new(0.17f, 0.09f, 0.05f, 0.95f);
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

        /// <summary>
        /// 카드 아래 덧붙일 표시(예: 풀 초월 캐릭터 → '별의 조각 +50'). null/빈 문자열이면 없음.
        /// 가챠는 육성 정보를 모르므로 메타(로스터)가 연결합니다.
        /// </summary>
        public static System.Func<CharacterData, string> ExtraNote;

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
            _footer.SetActive(false);
            _skipButton.gameObject.SetActive(true);
            _tint.color = Clear(_tint.color);
            _flash.color = Clear(_flash.color);
            _cutIn.gameObject.SetActive(false);
            _title.text = _results.Count > 1 ? "10연 뽑기 결과" : "뽑기 결과";
            BuildCards();
            ResetRitual();

            // 1) 소환 계약서: 펼쳐짐 → 서명 대기 → 인장 · 점화 · 소멸 → 빛기둥
            _ritual.gameObject.SetActive(true);
            yield return UnrollScroll();
            float idle = 0f;
            while (!_signed && !_skip)
            {
                if (_autoSign) { _autoSign = false; yield return AutoSign(); continue; }
                _hint.alpha = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
                if (!_signing)
                {
                    // 깃펜이 서명란 왼쪽에서 살짝 흔들리며 기다림
                    idle += Dt;
                    Vector2 rest = _hasLastInk ? _lastInk : new Vector2(-300f, -170f);
                    MoveQuill(rest + new Vector2(0f, 6f + 5f * Mathf.Sin(idle * 3.2f)));
                }
                yield return null;
            }
            _hint.alpha = 0f;
            yield return SealAndSummon(BestColor());

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

        // ---------------- 소환 계약서 ----------------

        private void ResetRitual()
        {
            foreach (var d in _ink) if (d != null) Destroy(d.gameObject);
            _ink.Clear();
            _inkLength = 0f; _hasLastInk = false; _signed = false; _signing = false; _autoSign = false;

            _ritualGroup.alpha = 1f;
            _scrollClip.sizeDelta = new Vector2(ScrollW + ClipPad * 2, ScrollH + ClipPad * 2);
            _scroll.localScale = new Vector3(0f, 1f, 1f);
            _scroll.anchoredPosition = new Vector2(0, -ClipPad);
            _scrollTextGroup.alpha = 0f;
            _sealImg.color = Clear(Color.white);
            _seal.localScale = Vector3.one;
            _circleImg.color = Clear(Color.white);
            _circle.localScale = Vector3.one * 0.6f; _circle.localEulerAngles = Vector3.zero;
            _pillarImg.color = Clear(Color.white);
            _burnImg.color = Clear(Color.white);
            _quillGroup.alpha = 0f;
            _hint.alpha = 0f;
            _scrollCount.text = _results != null && _results.Count > 1 ? $"—  {_results.Count}회 소환  —" : "—  1회 소환  —";
            for (int i = _emberLayer.childCount - 1; i >= 0; i--) Destroy(_emberLayer.GetChild(i).gameObject);
        }

        /// <summary>스크롤이 가운데에서 양옆으로 펼쳐지고 글씨·깃펜이 나타남.</summary>
        private IEnumerator UnrollScroll()
        {
            const float dur = 0.5f;
            for (float t = 0; t < dur && !_skip; t += Dt)
            {
                float k = t / dur;
                float back = 1f + 2.0f * Mathf.Pow(k - 1f, 3) + 1.0f * Mathf.Pow(k - 1f, 2);   // 살짝 넘쳤다 돌아옴
                _scroll.localScale = new Vector3(Mathf.Max(0f, back), 1f, 1f);
                _circleImg.color = new Color(1f, 1f, 1f, 0.12f * k);
                yield return null;
            }
            _scroll.localScale = Vector3.one;
            for (float t = 0; t < 0.3f && !_skip; t += Dt)
            {
                _scrollTextGroup.alpha = t / 0.3f;
                _quillGroup.alpha = t / 0.3f;
                MoveQuill(new Vector2(-300f, -164f + 30f * (1f - t / 0.3f)));
                yield return null;
            }
            _scrollTextGroup.alpha = 1f; _quillGroup.alpha = 1f;
        }

        // --- 서명 입력(SignaturePad → 화면 좌표) ---

        private Vector2 ToInk(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_inkLayer, screen, null, out var local);
            local.x = Mathf.Clamp(local.x, SignBand.xMin, SignBand.xMax);
            local.y = Mathf.Clamp(local.y, SignBand.yMin, SignBand.yMax);
            return local;
        }

        private void OnSignDown(Vector2 screen)
        {
            if (_signed || _sequence == null) return;
            _signing = true;
            _hasLastInk = false;
            AddInk(ToInk(screen));
        }

        private void OnSignDrag(Vector2 screen)
        {
            if (!_signing || _signed) return;
            AddInk(ToInk(screen));
            if (_inkLength >= SignNeed * 1.6f) { _signing = false; _signed = true; }   // 충분히 그었으면 손을 떼지 않아도 완료
        }

        private void OnSignUp()
        {
            if (!_signing) return;
            _signing = false;
            if (_inkLength >= SignNeed) _signed = true;
            else _autoSign = true;      // 살짝만 긋거나 눌렀다 떼면 나머지는 자동으로 서명
        }

        /// <summary>서명란 좌표 p까지 잉크를 이어 그림(5px 간격으로 점을 찍음) + 깃펜 이동.</summary>
        private void AddInk(Vector2 p)
        {
            if (_hasLastInk)
            {
                float dist = Vector2.Distance(_lastInk, p);
                int steps = Mathf.Min(40, Mathf.FloorToInt(dist / 5f));
                for (int i = 1; i <= steps; i++) Dot(Vector2.Lerp(_lastInk, p, i / (float)(steps + 1)));
                _inkLength += dist;
            }
            Dot(p);
            _lastInk = p; _hasLastInk = true;
            MoveQuill(p);
        }

        private void Dot(Vector2 p)
        {
            if (_ink.Count >= 520) return;
            var img = UIFactory.AddImage("Ink", _inkLayer, InkColor, Sprites.Circle);
            float size = Random.Range(7f, 10f);
            Center(img.rectTransform, p.x, p.y, size, size);
            _ink.Add(img);
        }

        /// <summary>깃펜 촉을 서명란 좌표 p에.</summary>
        private void MoveQuill(Vector2 inkPos)
        {
            _quill.position = _inkLayer.TransformPoint(inkPos);
        }

        /// <summary>자동 서명: 흘림체 한 줄을 0.8초에 걸쳐 씀.</summary>
        private IEnumerator AutoSign()
        {
            float x0 = SignBand.xMin + 90f, len = 600f, baseY = -172f;
            _hasLastInk = false;
            const float dur = 0.8f;
            for (float t = 0; t < dur && !_skip; t += Dt)
            {
                float k = t / dur;
                float e = EaseInOut(k);
                var pt = new Vector2(x0 + len * e + 20f * Mathf.Sin(e * 15f), baseY + 36f * Mathf.Sin(e * 19f) * (1f - 0.45f * e));
                AddInk(pt);
                yield return null;
            }
            // 마지막 밑줄 삐침
            Vector2 a = _lastInk;
            for (float t = 0; t < 0.18f && !_skip; t += Dt)
            {
                float k = t / 0.18f;
                AddInk(a + new Vector2(-260f * k, -34f - 10f * Mathf.Sin(k * Mathf.PI)));
                yield return null;
            }
            _signed = true;
        }

        /// <summary>인장 → 서명이 등급 색으로 빛남 → 스크롤이 아래에서부터 타올라 사라짐 → 마법진 빛기둥.</summary>
        private IEnumerator SealAndSummon(Color c)
        {
            if (!_skip)
            {
                // 깃펜이 물러나고 인장이 쾅
                Vector3 q0 = _quill.position;
                for (float t = 0; t < 0.22f && !_skip; t += Dt)
                {
                    float k = t / 0.22f;
                    _quill.position = q0 + new Vector3(60f, 90f, 0f) * k * _quill.lossyScale.x;
                    _quillGroup.alpha = 1f - k;
                    yield return null;
                }
                _quillGroup.alpha = 0f;
                for (float t = 0; t < 0.16f && !_skip; t += Dt)
                {
                    float k = t / 0.16f;
                    _seal.localScale = Vector3.one * Mathf.Lerp(2.6f, 1f, k * k);
                    _sealImg.color = new Color(1f, 1f, 1f, k);
                    yield return null;
                }
                _seal.localScale = Vector3.one; _sealImg.color = Color.white;
                // 찍히는 충격: 스크롤이 살짝 흔들리고 번쩍
                for (float t = 0; t < 0.22f && !_skip; t += Dt)
                {
                    float k = 1f - t / 0.22f;
                    _scroll.anchoredPosition = new Vector2(Mathf.Sin(t * 90f) * 7f * k, -ClipPad + Mathf.Cos(t * 70f) * 4f * k);
                    _flash.color = new Color(1f, 0.95f, 0.8f, 0.25f * k);
                    yield return null;
                }
                _scroll.anchoredPosition = new Vector2(0, -ClipPad);
                _flash.color = Clear(_flash.color);

                // 서명이 등급 색으로 빛나고, 글씨는 흐려지고, 뒤에서 마법진이 떠오름
                var glow = Color.Lerp(c, Color.white, 0.35f);
                for (float t = 0; t < 0.55f && !_skip; t += Dt)
                {
                    float k = t / 0.55f;
                    var ic = Color.Lerp(InkColor, glow, k);
                    foreach (var d in _ink) if (d != null) { d.color = ic; d.rectTransform.localScale = Vector3.one * (1f + 0.5f * k); }
                    _scrollTextGroup.alpha = 1f - 0.7f * k;
                    _circleImg.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.12f, 0.85f, k));
                    _circle.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, EaseOut(k));
                    _circle.Rotate(0, 0, 40f * Dt);
                    _tint.color = new Color(c.r, c.g, c.b, 0.08f * k);
                    yield return null;
                }

                // 스크롤이 아래에서부터 타올라 사라짐(불티가 위로 날림)
                float full = ScrollH + ClipPad * 2;
                const float burn = 0.95f;
                float emberAcc = 0f;
                for (float t = 0; t < burn && !_skip; t += Dt)
                {
                    float k = EaseInOut(t / burn);
                    float h = Mathf.Lerp(full, 0f, k);
                    _scrollClip.sizeDelta = new Vector2(ScrollW + ClipPad * 2, h);
                    float edgeY = _scrollClip.anchoredPosition.y - h;       // 타는 선(잘리는 아래쪽 끝)
                    _burnLine.anchoredPosition = new Vector2(0, edgeY);
                    _burnImg.color = new Color(c.r, c.g, c.b, 0.9f * Mathf.Sin(Mathf.Clamp01(t / burn) * Mathf.PI));
                    emberAcc += Dt * 90f;
                    while (emberAcc >= 1f) { emberAcc -= 1f; StartCoroutine(Ember(new Vector2(Random.Range(-ScrollW * 0.48f, ScrollW * 0.48f), edgeY), c)); }
                    _circle.Rotate(0, 0, (60f + 180f * k) * Dt);
                    _circleImg.color = new Color(c.r, c.g, c.b, 0.85f);
                    yield return null;
                }
            }
            _scrollClip.sizeDelta = new Vector2(ScrollW + ClipPad * 2, 0f);
            _burnImg.color = Clear(_burnImg.color);
            _quillGroup.alpha = 0f;

            // 마법진이 커지며 빛기둥 + 번쩍
            float dur = _skip ? 0.12f : 0.6f;
            for (float t = 0; t < dur; t += Dt)
            {
                float k = EaseOut(t / dur);
                _circle.localScale = Vector3.one * Mathf.Lerp(1f, 1.45f, k);
                _circle.Rotate(0, 0, 420f * Dt);
                _circleImg.color = new Color(c.r, c.g, c.b, 0.9f);
                _pillar.sizeDelta = new Vector2(Mathf.Lerp(120f, 520f, k), 1500f);
                _pillarImg.color = new Color(c.r, c.g, c.b, 0.85f * Mathf.Sin(k * Mathf.PI * 0.9f));
                _flash.color = new Color(c.r, c.g, c.b, 0.5f * Mathf.Sin(k * Mathf.PI));
                _tint.color = new Color(c.r, c.g, c.b, 0.12f);
                yield return null;
            }
            _flash.color = Clear(_flash.color);
            yield return Fade(_ritualGroup, 1f, 0f, _skip ? 0.08f : 0.3f);
            _ritual.gameObject.SetActive(false);
        }

        /// <summary>불티 1개: 타는 선에서 위로 흔들리며 날아오르다 사라짐.</summary>
        private IEnumerator Ember(Vector2 pos, Color c)
        {
            var img = UIFactory.AddImage("Ember", _emberLayer, Color.white, Random.value < 0.5f ? Sprites.Star : Sprites.Glow);
            var rt = img.rectTransform;
            float size = Random.Range(10f, 26f), life = Random.Range(0.5f, 1.0f);
            float vx = Random.Range(-50f, 50f), vy = Random.Range(120f, 320f), sway = Random.Range(2f, 6f);
            Center(rt, pos.x, pos.y, size, size);
            var hot = Color.Lerp(c, Color.white, 0.6f);
            for (float t = 0; t < life && img != null; t += Dt)
            {
                float k = t / life;
                rt.anchoredPosition = pos + new Vector2(vx * t + Mathf.Sin(t * sway * 3f) * 12f, vy * t);
                rt.sizeDelta = Vector2.one * size * (1f - 0.7f * k);
                var col = Color.Lerp(hot, c, k); col.a = 1f - k;
                img.color = col;
                yield return null;
            }
            if (img != null) Destroy(img.gameObject);
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

                string note = ExtraNote != null && r.character != null ? ExtraNote(r.character) : null;
                if (!string.IsNullOrEmpty(note))
                {
                    var nb = UIFactory.AddImage("Note", inner.transform, new Color(0.35f, 0.2f, 0.6f, 0.95f), Sprites.Rounded);
                    nb.type = Image.Type.Sliced; nb.preserveAspect = false;
                    var nrt = nb.rectTransform;
                    UIFactory.Anchor(nrt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
                    nrt.sizeDelta = new Vector2(w - 10, 28); nrt.anchoredPosition = new Vector2(0, -36);
                    var nt = UIFactory.AddText("T", nb.transform, note, 15, font, new Color(1f, 0.92f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
                    nt.enableAutoSizing = true; nt.fontSizeMin = 10; nt.fontSizeMax = 15;
                    UIFactory.Stretch(nt.rectTransform, 4, 0, 4, 0);
                }

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
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 어떤 화면비에서도 1920×1080 설계 영역이 전부 보이게(남는 쪽은 여백)
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", _canvas.transform, new Color(0.03f, 0.035f, 0.06f, 0.97f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            var root = _root.transform;
            // 소환 의식 배경(없으면 어두운 단색 그대로)
            SubcultureRPG.UI.BackgroundArt.Create(root, 0.62f).Set("Backgrounds/gacha_summon");

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

            BuildRitual(root);
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

        private void BuildRitual(Transform root)
        {
            _ritual = UIFactory.NewRect("Ritual", root);
            UIFactory.Stretch(_ritual);
            _ritualGroup = _ritual.gameObject.AddComponent<CanvasGroup>();
            const float cy = 20f;   // 스크롤 중심 높이

            // 마법진(스크롤 뒤) · 빛기둥
            var circleArt = GameArt.Load("Gacha/circle");
            _circleImg = UIFactory.AddImage("Circle", _ritual, Clear(Color.white), circleArt != null ? circleArt : Sprites.Ring);
            _circle = _circleImg.rectTransform;
            Center(_circle, 0, cy, 900, 900);
            _pillarImg = UIFactory.AddImage("Pillar", _ritual, Clear(Color.white), Sprites.Beam);
            _pillarImg.preserveAspect = false;
            _pillar = _pillarImg.rectTransform;
            Center(_pillar, 0, cy + 120, 120, 1500);

            // 스크롤: 위쪽을 고정한 틀(clip) 안에 두고, 틀 높이를 줄여 아래에서부터 사라지게 함
            _scrollClip = UIFactory.NewRect("ScrollClip", _ritual);
            UIFactory.Anchor(_scrollClip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f));
            _scrollClip.sizeDelta = new Vector2(ScrollW + ClipPad * 2, ScrollH + ClipPad * 2);
            _scrollClip.anchoredPosition = new Vector2(0, cy + ScrollH / 2f + ClipPad);
            _scrollClip.gameObject.AddComponent<RectMask2D>();

            _scroll = UIFactory.NewRect("Scroll", _scrollClip);
            UIFactory.Anchor(_scroll, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            _scroll.sizeDelta = new Vector2(ScrollW, ScrollH); _scroll.anchoredPosition = new Vector2(0, -ClipPad);
            var scrollArt = GameArt.Load("Gacha/scroll");
            if (scrollArt != null)
            {
                var paper = UIFactory.AddImage("Paper", _scroll, Color.white, scrollArt);
                paper.preserveAspect = false;
                UIFactory.Stretch(paper.rectTransform);
            }
            else
            {
                var paper = UIFactory.AddImage("Paper", _scroll, new Color(0.93f, 0.85f, 0.66f), Sprites.Rounded);
                paper.type = Image.Type.Sliced; paper.preserveAspect = false;
                UIFactory.Stretch(paper.rectTransform);
            }

            // 계약 문구
            var text = UIFactory.NewRect("Text", _scroll);
            UIFactory.Stretch(text);
            _scrollTextGroup = text.gameObject.AddComponent<CanvasGroup>();
            _scrollTextGroup.blocksRaycasts = false;
            var brown = new Color(0.30f, 0.17f, 0.08f);
            var ttl = UIFactory.AddText("Title", text, "소환 계약서", 46, font, brown, TextAlignmentOptions.Center, FontStyles.Bold);
            Center(ttl.rectTransform, 0, 178, 700, 60);
            var body = UIFactory.AddText("Body", text, "별의 부름에 응하는 자여,\n이 서약에 따라 나의 곁에서 함께 싸워 주기를.", 25, font,
                new Color(0.36f, 0.22f, 0.12f), TextAlignmentOptions.Center);
            body.lineSpacing = 14f;
            Center(body.rectTransform, 0, 78, 760, 100);
            _scrollCount = UIFactory.AddText("Count", text, "", 24, font, new Color(0.55f, 0.33f, 0.12f), TextAlignmentOptions.Center, FontStyles.Bold);
            Center(_scrollCount.rectTransform, 0, -22, 600, 36);
            var line = UIFactory.AddImage("SignLine", text, new Color(0.30f, 0.17f, 0.08f, 0.55f));
            Center(line.rectTransform, -40, -206, 620, 3);
            var signLbl = UIFactory.AddText("SignLabel", text, "서명", 20, font, new Color(0.36f, 0.22f, 0.12f, 0.85f), TextAlignmentOptions.Left, FontStyles.Bold);
            Center(signLbl.rectTransform, -330, -226, 100, 28);

            // 잉크(서명) · 인장
            _inkLayer = UIFactory.NewRect("Ink", _scroll);
            UIFactory.Stretch(_inkLayer);
            var sealArt = GameArt.Load("Gacha/seal");
            _sealImg = UIFactory.AddImage("Seal", _scroll, Clear(Color.white), sealArt != null ? sealArt : Sprites.Circle);
            _seal = _sealImg.rectTransform;
            Center(_seal, 372, -150, 170, 170);

            // 서명 입력(스크롤 전체를 눌러도 됨 — 잉크는 서명란 안에만 그려짐)
            var padImg = UIFactory.AddImage("SignPad", _scroll, new Color(0, 0, 0, 0), null, raycast: true);
            UIFactory.Stretch(padImg.rectTransform);
            var pad = padImg.gameObject.AddComponent<SignaturePad>();
            pad.Down = OnSignDown; pad.Drag = OnSignDrag; pad.Up = OnSignUp;

            // 타는 선 · 불티 · 깃펜(스크롤 틀 밖 — 잘리지 않음)
            _burnImg = UIFactory.AddImage("BurnLine", _ritual, Clear(Color.white), Sprites.Glow);
            _burnImg.preserveAspect = false;
            _burnLine = _burnImg.rectTransform;
            Center(_burnLine, 0, 0, ScrollW + 160, 70);
            _emberLayer = UIFactory.NewRect("Embers", _ritual);
            UIFactory.Stretch(_emberLayer);

            var quillArt = GameArt.Load("Gacha/quill");
            var quillImg = UIFactory.AddImage("Quill", _ritual, Color.white, quillArt != null ? quillArt : Sprites.Triangle);
            _quill = quillImg.rectTransform;
            UIFactory.Anchor(_quill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.07f, 0.13f));   // 촉(왼쪽 아래)이 기준점
            _quill.sizeDelta = new Vector2(150, 150);
            _quillGroup = quillImg.gameObject.AddComponent<CanvasGroup>();
            _quillGroup.blocksRaycasts = false;

            _hint = UIFactory.AddText("Hint", _ritual, "서명란을 문질러 서명하세요   <size=75%>(한 번 눌러도 자동으로 서명됩니다)</size>", 28, font,
                Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            _hint.richText = true;
            Center(_hint.rectTransform, 0, cy - ScrollH / 2f - 62, 1100, 44);
            _hint.raycastTarget = false;

            _ritual.gameObject.SetActive(false);
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
            if (_ritual != null) _ritual.gameObject.SetActive(false);
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }
    }

    /// <summary>서명 입력: 누르기·끌기·떼기를 화면 좌표로 넘겨 줌(계약 스크롤 위의 투명 판).</summary>
    public class SignaturePad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public System.Action<Vector2> Down, Drag;
        public System.Action Up;

        public void OnPointerDown(PointerEventData e) => Down?.Invoke(e.position);
        public void OnDrag(PointerEventData e) => Drag?.Invoke(e.position);
        public void OnPointerUp(PointerEventData e) => Up?.Invoke();
    }
}
