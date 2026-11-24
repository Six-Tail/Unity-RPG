using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 스토리 대화 화면(비주얼 노벨 형식). 다른 화면 위에 뜨는 전용 캔버스.
    ///  · 배경 그림 + 좌우 인물(말하는 쪽은 밝게, 나머지는 어둡게)
    ///  · 아래 대화 창: 이름표 + 대사(한 글자씩), 내레이션은 이름표 없이 가운데 정렬
    ///  · 화면을 누르면 다음(글자가 나오는 중이면 한 번에 표시) · 자동 진행 · 건너뛰기
    ///  · 장 제목 카드(제1장 / 그림자 숲)
    /// 씬에 없어도 StageRunner가 필요할 때 만들어 씁니다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Story Screen")]
    public class StoryScreen : MonoBehaviour
    {
        [SerializeField, Tooltip("한글 표시용 TMP 폰트.")] private TMP_FontAsset font;
        [SerializeField, Tooltip("1초에 나오는 글자 수.")] private float charsPerSecond = 38f;
        [SerializeField, Tooltip("자동 진행: 대사가 다 나온 뒤 기다리는 시간(초).")] private float autoDelay = 1.6f;

        private GameObject _root;
        private BackgroundArt _bg;
        private Image _veil, _nameBg, _nextMark, _titleDim;
        private readonly Image[] _portraits = new Image[2];        // 0 = 왼쪽, 1 = 오른쪽
        private readonly string[] _portraitKey = new string[2];
        private RectTransform _box;
        private TextMeshProUGUI _name, _text, _autoLabel, _titleTop, _titleMain;
        private CanvasGroup _boxGroup, _titleGroup;

        private StoryScript _script;
        private System.Action _onDone;
        private Coroutine _routine;
        private bool _advance, _skip, _auto;

        public bool IsPlaying => _routine != null;

        /// <summary>씬에서 찾고, 없으면 만들어서 돌려줌.</summary>
        public static StoryScreen Ensure(TMP_FontAsset fallbackFont)
        {
            var s = FindAnyObjectByType<StoryScreen>();
            if (s == null)
            {
                var go = new GameObject("StoryScreen");
                s = go.AddComponent<StoryScreen>();
            }
            if (s.font == null) s.font = fallbackFont;
            s.BuildIfNeeded();
            return s;
        }

        private void BuildIfNeeded()
        {
            if (_root == null) BuildUI();
        }

        /// <summary>스토리 재생. 끝나거나 건너뛰면 onDone.</summary>
        public void Play(StoryScript script, System.Action onDone)
        {
            BuildIfNeeded();
            if (script == null || script.lines.Count == 0) { onDone?.Invoke(); return; }
            if (_routine != null) StopCoroutine(_routine);
            _script = script;
            _onDone = onDone;
            _root.SetActive(true);
            _routine = StartCoroutine(Run());
        }

        // ---------------- 진행 ----------------

        private IEnumerator Run()
        {
            _advance = false; _skip = false;
            _veil.color = Color.black;
            _boxGroup.alpha = 0f;
            _titleGroup.alpha = 0f;
            _bg.Set((Sprite)null);
            for (int i = 0; i < 2; i++) { _portraits[i].enabled = false; _portraitKey[i] = null; }
            RefreshAutoLabel();

            bool first = true;
            foreach (var line in _script.lines)
            {
                if (_skip) break;
                switch (line.kind)
                {
                    case StoryLine.Kind.Background:
                        // 어둡게 → 배경 교체 → 밝게 (인물은 치움)
                        if (!first) yield return FadeVeil(1f, 0.25f);
                        _bg.Set(line.sub);
                        for (int i = 0; i < 2; i++) { _portraits[i].enabled = false; _portraitKey[i] = null; }
                        _boxGroup.alpha = 0f;
                        yield return FadeVeil(0f, 0.45f);
                        break;

                    case StoryLine.Kind.Title:
                        yield return ShowTitle(line.text, line.sub);
                        break;

                    default:
                        yield return ShowLine(line);
                        break;
                }
                first = false;
            }

            yield return FadeVeil(1f, 0.3f);
            _root.SetActive(false);
            _routine = null;
            var done = _onDone;
            _onDone = null;
            done?.Invoke();
        }

        private IEnumerator ShowLine(StoryLine line)
        {
            bool talk = line.kind == StoryLine.Kind.Dialogue;

            // 인물: 말하는 쪽 그림을 세우고 밝게, 반대쪽은 어둡게. 내레이션이면 둘 다 어둡게
            int side = line.right ? 1 : 0;
            if (talk && !string.IsNullOrEmpty(line.portrait))
            {
                if (_portraitKey[side] != line.portrait)
                {
                    var sp = GameArt.Load(line.portrait);
                    _portraits[side].sprite = sp;
                    _portraits[side].enabled = sp != null;
                    _portraitKey[side] = line.portrait;
                    if (sp != null) StartCoroutine(SlideIn(_portraits[side].rectTransform, line.right));
                }
            }
            for (int i = 0; i < 2; i++)
            {
                bool speaking = talk && i == side && !string.IsNullOrEmpty(line.portrait);
                _portraits[i].color = speaking ? Color.white : new Color(0.42f, 0.42f, 0.5f, 1f);
                _portraits[i].rectTransform.localScale = Vector3.one * (speaking ? 1.03f : 0.98f);
            }

            // 대화 창
            _nameBg.gameObject.SetActive(talk);
            if (talk)
            {
                _name.text = line.speaker;
                _nameBg.color = new Color(line.accent.r * 0.55f, line.accent.g * 0.55f, line.accent.b * 0.55f, 0.96f);
            }
            _text.alignment = talk ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center;
            _text.fontStyle = talk ? FontStyles.Normal : FontStyles.Italic;
            _text.color = talk ? Color.white : new Color(0.86f, 0.88f, 0.96f);
            _text.text = line.text;
            _text.maxVisibleCharacters = 0;
            _nextMark.enabled = false;
            if (_boxGroup.alpha < 1f) yield return Fade(_boxGroup, 1f, 0.2f);

            // 한 글자씩 — 누르면 한 번에
            _text.ForceMeshUpdate();
            int total = _text.textInfo.characterCount;
            _advance = false;
            float shown = 0f;
            while (shown < total && !_advance && !_skip)
            {
                shown += Time.unscaledDeltaTime * Mathf.Max(5f, charsPerSecond);
                _text.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }
            _text.maxVisibleCharacters = total;
            _advance = false;

            // 다음으로: 누르거나(자동이면 잠시 뒤)
            _nextMark.enabled = true;
            float wait = 0f, hold = autoDelay + total * 0.035f;
            while (!_advance && !_skip)
            {
                wait += Time.unscaledDeltaTime;
                if (_auto && wait >= hold) break;
                _nextMark.rectTransform.anchoredPosition = new Vector2(-34, 26 + 5f * Mathf.Sin(Time.unscaledTime * 6f));
                yield return null;
            }
            _advance = false;
            _nextMark.enabled = false;
        }

        private IEnumerator ShowTitle(string top, string main)
        {
            _boxGroup.alpha = 0f;
            _titleTop.text = top ?? "";
            _titleMain.text = main ?? "";
            _advance = false;
            yield return Fade(_titleGroup, 1f, 0.6f);
            for (float t = 0; t < 1.8f && !_advance && !_skip; t += Time.unscaledDeltaTime) yield return null;
            _advance = false;
            yield return Fade(_titleGroup, 0f, 0.45f);
        }

        private IEnumerator SlideIn(RectTransform rt, bool right)
        {
            float x1 = right ? 520f : -520f, x0 = x1 + (right ? 70f : -70f);
            for (float t = 0; t < 0.22f; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / 0.22f, 3f);
                rt.anchoredPosition = new Vector2(Mathf.Lerp(x0, x1, k), rt.anchoredPosition.y);
                yield return null;
            }
            rt.anchoredPosition = new Vector2(x1, rt.anchoredPosition.y);
        }

        private IEnumerator FadeVeil(float to, float dur)
        {
            float from = _veil.color.a;
            for (float t = 0; t < dur && !_skip; t += Time.unscaledDeltaTime)
            {
                _veil.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t / dur));
                yield return null;
            }
            _veil.color = new Color(0, 0, 0, to);
        }

        private IEnumerator Fade(CanvasGroup g, float to, float dur)
        {
            float from = g.alpha;
            for (float t = 0; t < dur && !_skip; t += Time.unscaledDeltaTime)
            {
                g.alpha = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }
            g.alpha = to;
        }

        private void RefreshAutoLabel()
        {
            if (_autoLabel == null) return;
            _autoLabel.text = _auto ? "자동 ON" : "자동 OFF";
            _autoLabel.color = _auto ? new Color(1f, 0.9f, 0.55f) : Color.white;
        }

        // ---------------- UI 구성 ----------------

        private void BuildUI()
        {
            var cgo = UIFactory.NewRect("StoryCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;   // 전투 HUD(90)·가챠 결과(100) 위
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

            // 화면 전체 = 다음으로 넘기는 버튼
            var rootImg = UIFactory.AddImage("Root", canvas.transform, Color.black, null, raycast: true);
            _root = rootImg.gameObject;
            UIFactory.Stretch(rootImg.rectTransform);
            var next = _root.AddComponent<Button>();
            next.transition = Selectable.Transition.None;
            next.onClick.AddListener(() => _advance = true);
            _bg = BackgroundArt.Create(_root.transform, 0.18f);

            var frame = UIFactory.Frame(_root.transform);

            // 인물(좌 · 우) — 아래쪽 기준, 대화 창 뒤
            for (int i = 0; i < 2; i++)
            {
                var img = UIFactory.AddImage(i == 0 ? "PortraitL" : "PortraitR", frame, Color.white);
                img.preserveAspect = true;
                var rt = img.rectTransform;
                UIFactory.Anchor(rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
                rt.sizeDelta = new Vector2(640, 900); rt.anchoredPosition = new Vector2(i == 0 ? -520 : 520, 60);
                img.enabled = false;
                _portraits[i] = img;
            }

            // 대화 창
            var box = UIFactory.AddImage("Box", frame, new Color(0.04f, 0.05f, 0.09f, 0.86f), ProceduralSprites.Rounded);
            box.type = Image.Type.Sliced; box.preserveAspect = false; box.pixelsPerUnitMultiplier = 1.4f;
            _box = box.rectTransform;
            UIFactory.Anchor(_box, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            _box.sizeDelta = new Vector2(1560, 270); _box.anchoredPosition = new Vector2(0, 44);
            _boxGroup = box.gameObject.AddComponent<CanvasGroup>();
            _boxGroup.blocksRaycasts = false;   // 창을 눌러도 다음으로

            var rim = UIFactory.AddImage("Rim", _box, new Color(1f, 0.86f, 0.5f, 0.5f));
            UIFactory.Anchor(rim.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            rim.rectTransform.sizeDelta = new Vector2(-60, 2); rim.rectTransform.anchoredPosition = new Vector2(0, -4);

            _nameBg = UIFactory.AddImage("Name", _box, new Color(0.3f, 0.25f, 0.5f, 0.96f), ProceduralSprites.Rounded);
            _nameBg.type = Image.Type.Sliced; _nameBg.preserveAspect = false; _nameBg.pixelsPerUnitMultiplier = 2f;
            UIFactory.Anchor(_nameBg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f));
            _nameBg.rectTransform.sizeDelta = new Vector2(260, 58); _nameBg.rectTransform.anchoredPosition = new Vector2(50, 0);
            _name = UIFactory.AddText("T", _nameBg.transform, "", 30, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_name.rectTransform, 8, 0, 8, 0);
            _name.enableAutoSizing = true; _name.fontSizeMin = 18; _name.fontSizeMax = 30;

            _text = UIFactory.AddText("Text", _box, "", 32, font, Color.white, TextAlignmentOptions.TopLeft);
            UIFactory.Stretch(_text.rectTransform, 64, 40, 80, 54);
            _text.lineSpacing = 14f;
            _text.enableAutoSizing = true; _text.fontSizeMin = 22; _text.fontSizeMax = 32;   // 긴 대사도 창 안에

            _nextMark = UIFactory.AddImage("Next", _box, new Color(1f, 0.88f, 0.55f), ProceduralSprites.Triangle);
            UIFactory.Anchor(_nextMark.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(0.5f, 0.5f));
            _nextMark.rectTransform.sizeDelta = new Vector2(24, 24); _nextMark.rectTransform.anchoredPosition = new Vector2(-34, 26);
            _nextMark.rectTransform.localEulerAngles = new Vector3(0, 0, 180f);
            _nextMark.enabled = false;

            // 장 제목 카드
            _titleDim = UIFactory.AddImage("TitleCard", _root.transform, new Color(0, 0, 0, 0.72f));
            UIFactory.Stretch(_titleDim.rectTransform);
            _titleGroup = _titleDim.gameObject.AddComponent<CanvasGroup>();
            _titleGroup.blocksRaycasts = false;
            _titleTop = UIFactory.AddText("Top", _titleDim.transform, "", 40, font, new Color(1f, 0.86f, 0.5f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(_titleTop.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _titleTop.rectTransform.sizeDelta = new Vector2(1200, 60); _titleTop.rectTransform.anchoredPosition = new Vector2(0, 70);
            var bar = UIFactory.AddImage("Bar", _titleDim.transform, new Color(1f, 0.86f, 0.5f, 0.8f));
            UIFactory.Anchor(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            bar.rectTransform.sizeDelta = new Vector2(420, 3); bar.rectTransform.anchoredPosition = new Vector2(0, 30);
            _titleMain = UIFactory.AddText("Main", _titleDim.transform, "", 86, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(_titleMain.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _titleMain.rectTransform.sizeDelta = new Vector2(1600, 120); _titleMain.rectTransform.anchoredPosition = new Vector2(0, -44);

            // 전환용 검은 막(눌러도 통과)
            _veil = UIFactory.AddImage("Veil", _root.transform, Color.black);
            UIFactory.Stretch(_veil.rectTransform);

            // 자동 · 건너뛰기(오른쪽 위 — 막 위에 있어 언제든 누를 수 있음)
            var skip = UIFactory.AddButton("Skip", frame, "건너뛰기 >>", 22, font, new Color(0f, 0f, 0f, 0.55f), Color.white);
            var srt = (RectTransform)skip.transform;
            UIFactory.Anchor(srt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            srt.sizeDelta = new Vector2(190, 52); srt.anchoredPosition = new Vector2(-30, -30);
            skip.onClick.AddListener(() => _skip = true);
            skip.transform.SetParent(_root.transform, true);
            skip.transform.SetAsLastSibling();

            var auto = UIFactory.AddButton("Auto", frame, "", 22, font, new Color(0f, 0f, 0f, 0.55f), Color.white);
            var art = (RectTransform)auto.transform;
            UIFactory.Anchor(art, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            art.sizeDelta = new Vector2(150, 52); art.anchoredPosition = new Vector2(-232, -30);
            _autoLabel = auto.GetComponentInChildren<TextMeshProUGUI>();
            auto.onClick.AddListener(() => { _auto = !_auto; RefreshAutoLabel(); });
            auto.transform.SetParent(_root.transform, true);
            auto.transform.SetAsLastSibling();

            _root.SetActive(false);
        }
    }
}
