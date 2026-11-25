using System.Collections;
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
    /// 게임을 켰을 때의 타이틀 흐름(메인 씬에서 자동으로 만들어져 모든 화면 위에 뜸).
    ///  1) 검은 화면에 로고 엠블럼 + 게임 이름
    ///  2) 키 아트(픽업 캐릭터 일러스트) 위에서 리소스 로딩(진행 막대 · 퍼센트 · 도움말)
    ///  3) 로딩이 끝나면 'TAP TO START' — 화면을 눌러야 홈(로비)으로
    /// 그림: Resources/Art/Title/logo.png(엠블럼, 글자 없음) · Title/keyart.jpg. 없으면 글자·홈 배경으로 대신.
    /// 게임 이름은 여기 상수 한 곳에서 바꿉니다.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public const string GameName = "스텔라 앵커";
        public const string GameNameEn = "STELLAR ANCHOR";
        public const string Tagline = "별의 부름에 응한 이들의 이야기";

        private static readonly string[] Tips =
        {
            "적의 약점 속성으로 공격하면 피해가 늘고 브레이크 게이지가 훨씬 빨리 깎입니다.",
            "별 3개로 클리어한 스테이지는 보상 배수와 반복 입장을 쓸 수 있습니다.",
            "초월 +1 · +3 · +6에서는 캐릭터의 고유 패시브가 강화됩니다.",
            "이미 풀 초월한 캐릭터를 또 뽑으면 별의 조각으로 바뀌고, 교환소에서 뽑기권이 됩니다.",
            "홈의 대표 캐릭터는 '대표 변경'에서 바꿀 수 있습니다. 눌러서 말을 걸어 보세요.",
            "행동력이 최대치를 넘은 동안에는 자연 회복이 멈춥니다. 물약은 필요할 때 쓰세요.",
        };

        private TMP_FontAsset _font;
        private GameObject _root;
        private CanvasGroup _logoGroup, _artGroup, _loadGroup, _startGroup, _rootGroup;
        private RectTransform _logo, _barFill;
        private Image _shade, _glow;
        private TextMeshProUGUI _loadLabel, _tip, _tap;
        private bool _tapped;

        /// <summary>메인 씬(홈 허브)이 뜨면 자동으로 타이틀을 먼저 보여 줌.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;   // 클라우드 세이브를 받아 씬을 다시 열 때도
            Spawn();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Spawn();

        private static void Spawn()
        {
            if (FindAnyObjectByType<ScreenNavigator>() == null) return;   // 테스트용 단독 씬에서는 안 띄움
            if (FindAnyObjectByType<TitleScreen>() != null) return;
            new GameObject("TitleScreen").AddComponent<TitleScreen>();
        }

        private void Start()
        {
            var home = FindAnyObjectByType<HomeScreen>();
            _font = home != null ? home.Font : null;
            BuildUI();
            StartCoroutine(Run());
        }

        // ---------------- 흐름 ----------------

        private IEnumerator Run()
        {
            bool quick = SubcultureRPG.Meta.Cloud.CloudAccount.Synced;   // 서버 세이브를 받아 다시 불러온 직후
            // 1) 검은 화면: 로고 + 이름
            _logoGroup.alpha = 0f; _artGroup.alpha = 0f; _loadGroup.alpha = 0f; _startGroup.alpha = 0f;
            yield return null;
            if (!quick) {
            for (float t = 0; t < 0.7f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.7f, e = 1f - Mathf.Pow(1f - k, 3f);
                _logoGroup.alpha = e;
                _logo.localScale = Vector3.one * Mathf.Lerp(0.82f, 1f, e);
                _glow.color = new Color(1f, 0.85f, 0.5f, 0.35f * e);
                yield return null;
            }
            _logoGroup.alpha = 1f; _logo.localScale = Vector3.one;
            _tapped = false;
            for (float t = 0; t < 1.5f && !_tapped; t += Time.unscaledDeltaTime)
            {
                _glow.color = new Color(1f, 0.85f, 0.5f, 0.3f + 0.08f * Mathf.Sin(t * 4f));
                yield return null;
            }

            // 2) 키 아트가 떠오르고(로고는 아래 가운데로 작게 — 캐릭터 얼굴을 가리지 않게) 리소스 로딩
            Vector2 from = _logoGroup.GetComponent<RectTransform>().anchoredPosition, to = new Vector2(0, -130);
            var lrt = _logoGroup.GetComponent<RectTransform>();
            for (float t = 0; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / 0.6f);
                _artGroup.alpha = k;
                _loadGroup.alpha = k;
                lrt.anchoredPosition = Vector2.Lerp(from, to, k);
                lrt.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, k);
                _glow.color = new Color(1f, 0.85f, 0.5f, 0.3f * (1f - k));
                yield return null;
            }
            _artGroup.alpha = 1f; _loadGroup.alpha = 1f;
            lrt.anchoredPosition = to; lrt.localScale = Vector3.one * 0.6f;
            _glow.color = new Color(1, 1, 1, 0);
            }
            else
            {
                _artGroup.alpha = 1f; _loadGroup.alpha = 1f; _logoGroup.alpha = 1f;
                var qrt = _logoGroup.GetComponent<RectTransform>();
                qrt.anchoredPosition = new Vector2(0, -130); qrt.localScale = Vector3.one * 0.6f;
                _glow.color = new Color(1, 1, 1, 0);
            }

            yield return LoadResources();

            // 3) TAP TO START
            for (float t = 0; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.45f;
                _loadGroup.alpha = 1f - k;
                _startGroup.alpha = k;
                _shade.color = new Color(0, 0, 0, Mathf.Lerp(0.5f, 0.16f, k));   // 그림이 밝아짐
                yield return null;
            }
            _loadGroup.alpha = 0f; _startGroup.alpha = 1f;
            _tapped = false;
            while (!_tapped && !quick)
            {
                _tap.alpha = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3.2f);
                _tap.rectTransform.localScale = Vector3.one * (1f + 0.02f * Mathf.Sin(Time.unscaledTime * 3.2f));
                yield return null;
            }

            // 계정: 로그인 → 서버 세이브 맞추기. 서버 세이브로 바뀌면 게임을 다시 불러옴(이 타이틀도 새로 뜸)
            _startGroup.alpha = 0f;
            bool reload = false;
            yield return SubcultureRPG.Meta.Cloud.CloudLogin.Run(_font, () => PlayerProfile.Instance.HasName, () => reload = true);
            if (reload) { SaveStore.ReloadGame(); yield break; }

            // 처음 시작이면 지휘관 이름부터 정함
            if (!PlayerProfile.Instance.HasName)
            {
                bool named = false;
                _startGroup.alpha = 0f;
                ProfilePanel.AskName(_font, () => named = true);
                while (!named) yield return null;
            }

            // 홈으로: 대표 캐릭터 등장 연출이 다시 보이도록 홈을 새로 고친 뒤 타이틀을 걷음
            var home = FindAnyObjectByType<HomeScreen>();
            if (home != null) home.Show(true);
            for (float t = 0; t < 0.55f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.55f;
                _rootGroup.alpha = 1f - k;
                _root.transform.localScale = Vector3.one * (1f + 0.04f * k);
                yield return null;
            }
            Destroy(gameObject);
        }

        /// <summary>자주 쓰는 그림을 미리 불러옴(비동기 — 화면이 멈추지 않음). 진행 막대에 반영.</summary>
        private IEnumerator LoadResources()
        {
            var paths = ResourcePaths();
            float started = Time.unscaledTime, shown = 0f;
            int done = 0;
            const int batch = 6;
            _tip.text = Tips[Random.Range(0, Tips.Length)];
            float tipAt = Time.unscaledTime;

            for (int i = 0; i < paths.Count; i += batch)
            {
                var reqs = new List<ResourceRequest>();
                for (int j = i; j < Mathf.Min(paths.Count, i + batch); j++)
                    reqs.Add(Resources.LoadAsync<Sprite>("Art/" + paths[j]));
                foreach (var r in reqs)
                {
                    while (!r.isDone)
                    {
                        shown = Mathf.MoveTowards(shown, done / (float)paths.Count, Time.unscaledDeltaTime * 1.5f);
                        SetProgress(shown);
                        yield return null;
                    }
                    done++;
                }
                if (Time.unscaledTime - tipAt > 2.5f) { _tip.text = Tips[Random.Range(0, Tips.Length)]; tipAt = Time.unscaledTime; }
                yield return null;
            }
            foreach (var p in paths) GameArt.Load(p);   // 게임 쪽 캐시에 등록(이미 메모리에 있어 즉시)

            // 너무 빨리 끝나도 막대가 끝까지 차는 모습은 보여 줌
            while (shown < 0.999f || Time.unscaledTime - started < 1.2f)
            {
                shown = Mathf.MoveTowards(shown, 1f, Time.unscaledDeltaTime * 1.2f);
                SetProgress(shown);
                yield return null;
            }
            SetProgress(1f);
        }

        private void SetProgress(float k)
        {
            k = Mathf.Clamp01(k);
            _barFill.anchorMax = new Vector2(Mathf.Max(0.01f, k), 1f);
            _loadLabel.text = k >= 0.999f ? "준비 완료" : $"리소스를 불러오는 중...  {Mathf.FloorToInt(k * 100f)}%";
        }

        private static List<string> ResourcePaths()
        {
            var list = new List<string>
            {
                "Backgrounds/home", "Backgrounds/stage", "Backgrounds/gacha", "Backgrounds/growth", "Backgrounds/party",
                "Backgrounds/bag", "Backgrounds/shop", "Backgrounds/mission", "Backgrounds/codex", "Backgrounds/gacha_summon", "Backgrounds/battle",
                "Banners/standard",
                "Gacha/scroll", "Gacha/circle", "Gacha/quill", "Gacha/seal",
                "Items/Gold", "Items/Crystal", "Items/Stamina", "Items/PickupTicket", "Items/StandardTicket", "Status/Break",
            };
            foreach (var t in Materials.All()) list.Add("Items/" + t);
            foreach (var e in Traits.AllElements) list.Add("Elements/" + e);
            foreach (SubcultureRPG.Battle.StatusType s in System.Enum.GetValues(typeof(SubcultureRPG.Battle.StatusType)))
                if (s != SubcultureRPG.Battle.StatusType.None) list.Add("Status/" + s);

            var roster = FindAnyObjectByType<PlayerRoster>();
            if (roster != null && roster.Database != null)
                foreach (var c in roster.Database.Characters)
                {
                    if (c == null) continue;
                    string key = !string.IsNullOrEmpty(c.id) ? c.id : c.name;
                    list.Add("Thumbs/" + key);
                    list.Add("Characters/" + key);
                    if (c.rarity == Rarity.R6) list.Add("Banners/" + key);
                }
            return list;
        }

        // ---------------- UI 구성 ----------------

        private void BuildUI()
        {
            var cgo = UIFactory.NewRect("TitleCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;   // 모든 화면 위
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                SubcultureRPG.UI.UIInput.AddModule(es);
            }

            // 검은 바탕 = 화면 전체 누르기
            var black = UIFactory.AddImage("Root", canvas.transform, Color.black, null, raycast: true);
            _root = black.gameObject;
            UIFactory.Stretch(black.rectTransform);
            _rootGroup = _root.AddComponent<CanvasGroup>();
            var tap = _root.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() => _tapped = true);

            // 키 아트(로딩 · 시작 화면 배경)
            var art = UIFactory.NewRect("Art", _root.transform);
            UIFactory.Stretch(art);
            _artGroup = art.gameObject.AddComponent<CanvasGroup>();
            _artGroup.blocksRaycasts = false;
            BackgroundArt.Create(art, 0f).Set("Title/keyart", "Backgrounds/home");
            _shade = UIFactory.AddImage("Shade", art, new Color(0, 0, 0, 0.5f));
            UIFactory.Stretch(_shade.rectTransform);
            // 아래쪽 글씨가 잘 보이게 어두운 띠
            var bottom = UIFactory.AddImage("Bottom", art, new Color(0, 0, 0, 0.75f), ProceduralSprites.Glow);
            bottom.preserveAspect = false;
            UIFactory.Anchor(bottom.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0.5f));
            bottom.rectTransform.sizeDelta = new Vector2(600, 1000); bottom.rectTransform.anchoredPosition = new Vector2(0, 0);

            var frame = UIFactory.Frame(_root.transform);

            // 로고 묶음: 엠블럼 + 이름 + 영문 + 한 줄 소개
            var logoGroup = UIFactory.NewRect("Logo", frame);
            UIFactory.Anchor(logoGroup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            logoGroup.sizeDelta = new Vector2(1200, 620); logoGroup.anchoredPosition = new Vector2(0, 20);
            _logoGroup = logoGroup.gameObject.AddComponent<CanvasGroup>();
            _logoGroup.blocksRaycasts = false;

            _glow = UIFactory.AddImage("Glow", logoGroup, new Color(1, 1, 1, 0), ProceduralSprites.Glow);
            _glow.preserveAspect = false;
            Center(_glow.rectTransform, 0, 110, 760, 760);

            var emblem = GameArt.Load("Title/logo");
            var em = UIFactory.AddImage("Emblem", logoGroup, emblem != null ? Color.white : new Color(1f, 0.85f, 0.45f), emblem != null ? emblem : ProceduralSprites.Star);
            em.preserveAspect = true;
            _logo = em.rectTransform;
            Center(_logo, 0, 130, 320, 320);

            var name = UIFactory.AddText("Name", logoGroup, GameName, 104, _font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Center(name.rectTransform, 0, -90, 1200, 130);
            name.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            var en = UIFactory.AddText("NameEn", logoGroup, GameNameEn, 36, _font, new Color(1f, 0.86f, 0.5f), TextAlignmentOptions.Center, FontStyles.Bold);
            en.characterSpacing = 28f;
            Center(en.rectTransform, 0, -176, 1200, 48);
            var line = UIFactory.AddImage("Line", logoGroup, new Color(1f, 0.86f, 0.5f, 0.7f));
            Center(line.rectTransform, 0, -210, 520, 2);
            var tag = UIFactory.AddText("Tagline", logoGroup, Tagline, 26, _font, new Color(0.85f, 0.88f, 0.96f), TextAlignmentOptions.Center);
            Center(tag.rectTransform, 0, -244, 1200, 40);

            // 로딩: 막대 + 퍼센트 + 도움말
            var load = UIFactory.NewRect("Loading", frame);
            UIFactory.Anchor(load, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            load.sizeDelta = new Vector2(1400, 200); load.anchoredPosition = new Vector2(0, 50);
            _loadGroup = load.gameObject.AddComponent<CanvasGroup>();
            _loadGroup.blocksRaycasts = false;
            _loadLabel = UIFactory.AddText("Label", load, "", 26, _font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Center(_loadLabel.rectTransform, 0, 56, 1200, 36);
            var barBg = UIFactory.AddImage("BarBg", load, new Color(1, 1, 1, 0.16f), ProceduralSprites.Rounded);
            barBg.type = Image.Type.Sliced; barBg.preserveAspect = false; barBg.pixelsPerUnitMultiplier = 4f;
            Center(barBg.rectTransform, 0, 14, 1200, 18);
            var fill = UIFactory.AddImage("Fill", barBg.transform, new Color(1f, 0.82f, 0.4f), ProceduralSprites.Rounded);
            fill.type = Image.Type.Sliced; fill.preserveAspect = false; fill.pixelsPerUnitMultiplier = 4f;
            _barFill = fill.rectTransform;
            _barFill.anchorMin = Vector2.zero; _barFill.anchorMax = new Vector2(0.01f, 1f);
            _barFill.offsetMin = Vector2.zero; _barFill.offsetMax = Vector2.zero;
            _tip = UIFactory.AddText("Tip", load, "", 22, _font, new Color(0.82f, 0.85f, 0.93f), TextAlignmentOptions.Center);
            Center(_tip.rectTransform, 0, -36, 1400, 34);
            _tip.enableAutoSizing = true; _tip.fontSizeMin = 14; _tip.fontSizeMax = 22;

            // 시작 안내
            var start = UIFactory.NewRect("Start", frame);
            UIFactory.Anchor(start, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            start.sizeDelta = new Vector2(1400, 200); start.anchoredPosition = new Vector2(0, 50);
            _startGroup = start.gameObject.AddComponent<CanvasGroup>();
            _startGroup.blocksRaycasts = false;
            _tap = UIFactory.AddText("Tap", start, "TAP TO START", 38, GameFonts.Display(_font), Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
            _tap.characterSpacing = 16f;
            Center(_tap.rectTransform, 0, 36, 1200, 72);
            _tap.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            var sub = UIFactory.AddText("Sub", start, "화면을 눌러 시작", 20, _font, new Color(1f, 0.88f, 0.6f), TextAlignmentOptions.Center);
            Center(sub.rectTransform, 0, -12, 1200, 30);

            var ver = UIFactory.AddText("Version", frame, $"v{Application.version}", 18, _font, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.BottomRight);
            UIFactory.Anchor(ver.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
            ver.rectTransform.sizeDelta = new Vector2(300, 30); ver.rectTransform.anchoredPosition = new Vector2(-24, 16);
        }

        private static void Center(RectTransform rt, float x, float y, float w, float h)
        {
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, y);
        }
    }
}
