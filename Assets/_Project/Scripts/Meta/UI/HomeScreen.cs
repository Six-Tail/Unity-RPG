using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;
using SubcultureRPG.Gacha;
using SubcultureRPG.Meta.Missions;
using SubcultureRPG.Meta.Codex;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 홈(로비) 화면.
    ///  · 왼쪽: 플레이어가 고른 <b>대표 캐릭터</b>가 크게 서 있음(움직이는 일러스트가 있으면 그것) — 누르면 대사 말풍선
    ///  · 오른쪽: 스테이지(크게) · 가챠 · 육성 · 임무 · 상점 · 가방 · 도감 · 대표 변경
    ///  · 오른쪽 위: 크리스탈 · 금화
    /// 대표 캐릭터는 PlayerPrefs(home.assistant)에 기억, 없으면 보유 중 가장 높은 성급.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Home Screen")]
    public class HomeScreen : MonoBehaviour
    {
        [SerializeField] private ScreenNavigator navigator;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerRoster roster;
        [SerializeField] private GachaController gacha;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private string gameTitle = "서브컬쳐 RPG";

        private const string AssistantKey = "home.assistant";

        /// <summary>홈 화면 폰트(코드로 추가하는 화면이 같은 폰트를 쓰도록).</summary>
        public TMP_FontAsset Font => font;

        private GameObject _root, _picker;
        private PlayerMissions _missions;
        private PlayerCodex _codex;
        private TextMeshProUGUI _missionLabel, _rosterText, _assistName, _bubbleText;
        private Image _assistant;
        private RectTransform _assistRt, _bubble, _pickerGrid;
        private StarRow _assistStars;
        private RectTransform _assistMask;
        private CanvasGroup _bubbleGroup;
        private Coroutine _bubbleRoutine, _bounceRoutine;
        private CharacterData _shown;

        private void Awake()
        {
            if (navigator == null) navigator = FindAnyObjectByType<ScreenNavigator>();
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            if (inventory == null) inventory = FindAnyObjectByType<PlayerInventory>();
            if (roster == null) roster = FindAnyObjectByType<PlayerRoster>();
            if (gacha == null) gacha = FindAnyObjectByType<GachaController>();
            BuildUI();
        }

        private void OnEnable()
        {
            if (wallet != null) wallet.OnChanged += Refresh;
            if (inventory != null) inventory.OnChanged += Refresh;
            if (roster != null) roster.OnRosterChanged += Refresh;
        }

        private void OnDisable()
        {
            if (wallet != null) wallet.OnChanged -= Refresh;
            if (inventory != null) inventory.OnChanged -= Refresh;
            if (roster != null) roster.OnRosterChanged -= Refresh;
        }

        private void Start()
        {
            // 임무 시스템은 내비게이터가 실행 중에 만들 수도 있어 Start에서 찾음
            _missions = FindAnyObjectByType<PlayerMissions>();
            if (_missions != null) _missions.OnChanged += RefreshMissionBadge;
            _codex = FindAnyObjectByType<PlayerCodex>();
            if (_codex != null) _codex.OnChanged += RefreshCodexButton;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_missions != null) _missions.OnChanged -= RefreshMissionBadge;
            if (_codex != null) _codex.OnChanged -= RefreshCodexButton;
        }

        public void Show(bool on)
        {
            if (_root != null) _root.SetActive(on);
            if (on) { _shown = null; Refresh(); }
            else if (_picker != null) _picker.SetActive(false);
        }

        public void Refresh()
        {
            RefreshMissionBadge();
            RefreshCodexButton();
            RefreshAssistant();
        }

        // ---------------- 대표 캐릭터 ----------------

        private List<CharacterData> OwnedCharacters()
        {
            var list = new List<CharacterData>();
            if (roster == null || roster.Database == null) return list;
            foreach (var o in roster.Owned)
            {
                var c = roster.Database.GetCharacter(o.id);
                if (c != null) list.Add(c);
            }
            // 성급(현재) 높은 순 → 초월 → 이름
            list.Sort((a, b) =>
            {
                int ga = Transcendence.GradeValue(roster.StarOf(a), roster.TranscendOf(a));
                int gb = Transcendence.GradeValue(roster.StarOf(b), roster.TranscendOf(b));
                if (ga != gb) return gb.CompareTo(ga);
                return string.CompareOrdinal(a.displayName, b.displayName);
            });
            return list;
        }

        /// <summary>대표 캐릭터: 고른 캐릭터(보유 중일 때) → 없으면 보유 중 가장 높은 성급.</summary>
        private CharacterData Assistant()
        {
            var owned = OwnedCharacters();
            if (owned.Count == 0) return null;
            string id = PlayerPrefs.GetString(AssistantKey, "");
            foreach (var c in owned)
                if (!string.IsNullOrEmpty(id) && (c.id == id || c.name == id)) return c;
            return owned[0];
        }

        private void RefreshAssistant()
        {
            if (_assistant == null) return;
            var c = Assistant();
            if (c == _shown && c != null) return;
            _shown = c;
            if (c == null)
            {
                Live2DUI.Hide(_assistant);
                _assistant.enabled = false;
                _assistName.text = "";
                return;
            }
            var sprite = GameArt.Portrait(c);
            bool live = Live2DUI.Show(_assistant, c);
            _assistant.sprite = sprite;
            _assistant.enabled = !live && sprite != null;
            _assistant.color = Color.white;
            int star = roster != null ? roster.StarOf(c) : (int)c.rarity;
            int tr = roster != null ? roster.TranscendOf(c) : 0;
            _assistStars.Set(star, tr);
            _assistName.text = $"<b>{c.displayName}</b>  <size=70%><color={Traits.Hex(c.element)}>{Traits.Name(c.element)}</color> · {Traits.Name(c.role)}</size>";
            if (_root.activeInHierarchy) StartCoroutine(Appear());
        }

        private IEnumerator Appear()
        {
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / 0.4f, 3f);
                _assistMask.anchoredPosition = new Vector2(Mathf.Lerp(-420f, -380f, k), 0f);
                SetAssistantAlpha(k);
                yield return null;
            }
            _assistMask.anchoredPosition = new Vector2(-380f, 0f);
            SetAssistantAlpha(1f);
        }

        private void SetAssistantAlpha(float a)
        {
            _assistant.color = new Color(1f, 1f, 1f, a);
            Live2DUI.SetAlpha(_assistant, a);
        }

        /// <summary>대표 캐릭터를 누르면: 톡 튀고 대표 대사 말풍선.</summary>
        private void PokeAssistant()
        {
            if (_shown == null) return;
            if (_bounceRoutine != null) StopCoroutine(_bounceRoutine);
            _bounceRoutine = StartCoroutine(Bounce());
            if (_bubbleRoutine != null) StopCoroutine(_bubbleRoutine);
            _bubbleRoutine = StartCoroutine(Bubble(CharacterLines.Random(_shown)));   // 캐릭터별 로비 대사 중 무작위
        }

        private IEnumerator Bounce()
        {
            for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.3f;
                _assistRt.localScale = new Vector3(1f + 0.02f * Mathf.Sin(k * Mathf.PI), 1f - 0.025f * Mathf.Sin(k * Mathf.PI * 2f), 1f);
                yield return null;
            }
            _assistRt.localScale = Vector3.one;
            _bounceRoutine = null;
        }

        private IEnumerator Bubble(string line)
        {
            _bubbleText.text = line;
            for (float t = 0; t < 0.2f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.2f;
                _bubbleGroup.alpha = k;
                _bubble.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, k);
                yield return null;
            }
            _bubbleGroup.alpha = 1f; _bubble.localScale = Vector3.one;
            for (float t = 0; t < 3.2f; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime) { _bubbleGroup.alpha = 1f - t / 0.35f; yield return null; }
            _bubbleGroup.alpha = 0f;
            _bubbleRoutine = null;
        }

        // ---------------- 대표 캐릭터 고르기 ----------------

        private void OpenPicker()
        {
            for (int i = _pickerGrid.childCount - 1; i >= 0; i--)
            {
                var ch = _pickerGrid.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
            var cur = Assistant();
            foreach (var c in OwnedCharacters()) PickerCell(c, c == cur);
            _picker.SetActive(true);
            _picker.transform.SetAsLastSibling();
        }

        private void PickerCell(CharacterData c, bool on)
        {
            int star = roster != null ? roster.StarOf(c) : (int)c.rarity;
            var col = StarText.ColorOf(star);
            var cell = UIFactory.AddImage("Cell", _pickerGrid, on ? new Color(1f, 0.9f, 0.5f, 1f) : new Color(col.r, col.g, col.b, 0.85f), null, raycast: true);
            var btn = cell.gameObject.AddComponent<Button>();
            btn.targetGraphic = cell;
            btn.onClick.AddListener(() =>
            {
                PlayerPrefs.SetString(AssistantKey, !string.IsNullOrEmpty(c.id) ? c.id : c.name);
                PlayerPrefs.Save();
                _picker.SetActive(false);
                _shown = null;
                RefreshAssistant();
                PokeAssistant();
            });
            cell.gameObject.AddComponent<ButtonFx>();
            var inner = UIFactory.AddImage("Inner", cell.transform, new Color(0.1f, 0.1f, 0.15f, 1f));
            UIFactory.Stretch(inner.rectTransform, on ? 5 : 3, on ? 5 : 3, on ? 5 : 3, on ? 5 : 3);
            var th = GameArt.Thumb(c);
            if (th != null)
            {
                var img = UIFactory.AddImage("Thumb", inner.transform, Color.white, th);
                img.preserveAspect = true;
                UIFactory.Stretch(img.rectTransform, 4, 46, 4, 4);
            }
            var stars = StarRow.Create(inner.transform, 17, TextAnchor.MiddleCenter, font);
            UIFactory.Anchor(stars.Rect, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            stars.Rect.sizeDelta = new Vector2(0, 18); stars.Rect.anchoredPosition = new Vector2(0, 24);
            stars.Set(star, roster != null ? roster.TranscendOf(c) : 0);
            var name = UIFactory.AddText("Name", inner.transform, c.displayName, 17, font, Color.white, TextAlignmentOptions.Bottom, FontStyles.Bold);
            name.enableAutoSizing = true; name.fontSizeMin = 11; name.fontSizeMax = 17;
            UIFactory.Anchor(name.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            name.rectTransform.sizeDelta = new Vector2(-6, 22); name.rectTransform.anchoredPosition = new Vector2(0, 2);
            if (on)
            {
                var tag = UIFactory.AddText("Now", inner.transform, "대표", 16, font, new Color(0.2f, 0.12f, 0.02f), TextAlignmentOptions.Center, FontStyles.Bold);
                var bg = UIFactory.AddImage("TagBg", inner.transform, new Color(1f, 0.85f, 0.4f, 1f), ProceduralSprites.Rounded);
                bg.type = Image.Type.Sliced; bg.preserveAspect = false;
                UIFactory.Anchor(bg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
                bg.rectTransform.sizeDelta = new Vector2(56, 26); bg.rectTransform.anchoredPosition = new Vector2(6, -6);
                tag.transform.SetParent(bg.transform, false);
                UIFactory.Stretch(tag.rectTransform);
            }
        }

        // ---------------- 메뉴 ----------------

        /// <summary>도감 버튼: 수집 현황 + 받을 수 있는 수집 보상 수.</summary>
        private void RefreshCodexButton()
        {
            if (_rosterText == null) return;
            int owned = roster != null ? roster.Owned.Count : 0;
            string count = _codex != null && _codex.Total > 0 ? $"{_codex.OwnedCount} / {_codex.Total}" : $"{owned}명";
            int n = _codex != null ? _codex.ClaimableCount() : 0;
            _rosterText.text = "도감" + (n > 0 ? $"  <color=#FFE08A><size=70%>●{n}</size></color>" : "") + $"\n<size=55%>수집 {count}</size>";
        }

        /// <summary>임무 버튼에 지금 받을 수 있는 보상 수 표시.</summary>
        private void RefreshMissionBadge()
        {
            if (_missionLabel == null) return;
            int n = _missions != null ? _missions.ClaimableCount() : 0;
            _missionLabel.text = n > 0
                ? $"임무  <color=#FFE08A><size=70%>●{n}</size></color>\n<size=55%>받을 보상 {n}개</size>"
                : "임무\n<size=55%>일일 · 주간</size>";
        }

        private void Nav(HubScreen screen)
        {
            if (navigator == null) navigator = FindAnyObjectByType<ScreenNavigator>();
            if (navigator != null) navigator.Go(screen);
        }

        // ---------------- UI 구성 ----------------

        private void BuildUI()
        {
            var cgo = UIFactory.NewRect("HomeCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 어떤 화면비에서도 1920×1080 설계 영역이 전부 보이게(남는 쪽은 여백)
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.07f, 0.06f, 0.11f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            BackgroundArt.Create(_root.transform, 0.25f).Set("Backgrounds/home");

            // 오른쪽 메뉴 뒤를 살짝 어둡게(그림 위 글씨가 잘 보이게) — 화면 오른쪽 끝까지
            var shade = UIFactory.AddImage("RightShade", _root.transform, new Color(0, 0, 0, 0.38f), ProceduralSprites.Glow);
            shade.preserveAspect = false;
            UIFactory.Anchor(shade.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f));
            shade.rectTransform.sizeDelta = new Vector2(1700, 400); shade.rectTransform.anchoredPosition = new Vector2(500, 0);

            var root = UIFactory.Frame(_root.transform);

            // 대표 캐릭터(왼쪽, 아래에 붙여 크게) — 누르면 대사
            // 일러스트는 자체 배경이 있는 직사각형이라, 가장자리를 부드럽게 지워 로비 배경에 녹임(RectMask2D softness)
            _assistMask = UIFactory.NewRect("AssistantMask", root);
            UIFactory.Anchor(_assistMask, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f));
            _assistMask.sizeDelta = new Vector2(820, 0); _assistMask.anchoredPosition = new Vector2(-380, 0);
            var soft = _assistMask.gameObject.AddComponent<RectMask2D>();
            soft.softness = new Vector2Int(170, 150);
            _assistMask.SetAsFirstSibling();

            // 뒤쪽 은은한 빛(캐릭터가 배경에서 떠 보이게)
            var halo = UIFactory.AddImage("Halo", root, new Color(1f, 0.92f, 0.78f, 0.22f), ProceduralSprites.Glow);
            halo.preserveAspect = false;
            UIFactory.Anchor(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            halo.rectTransform.sizeDelta = new Vector2(1100, 1100); halo.rectTransform.anchoredPosition = new Vector2(-380, 40);
            halo.transform.SetAsFirstSibling();

            _assistant = UIFactory.AddImage("Assistant", _assistMask, Color.white);
            _assistant.preserveAspect = true;
            _assistRt = _assistant.rectTransform;
            UIFactory.Stretch(_assistRt, 0, -40, 0, 30);   // 아래는 화면 밖까지(발끝이 잘려도 자연스럽게)
            // 누르는 영역은 따로(움직이는 일러스트를 띄우면 정적 Image가 꺼져 클릭을 못 받으므로)
            var hit = UIFactory.AddImage("Hit", _assistRt, new Color(0, 0, 0, 0), null, raycast: true);
            UIFactory.Stretch(hit.rectTransform, 160, 0, 160, 80);
            var poke = hit.gameObject.AddComponent<Button>();
            poke.transition = Selectable.Transition.None;
            poke.onClick.AddListener(PokeAssistant);

            // 말풍선(캐릭터 오른쪽 위)
            var bubble = UIFactory.AddImage("Bubble", root, new Color(1f, 1f, 1f, 0.95f), ProceduralSprites.Rounded);
            bubble.type = Image.Type.Sliced; bubble.preserveAspect = false; bubble.pixelsPerUnitMultiplier = 1.6f;
            _bubble = bubble.rectTransform;
            UIFactory.Anchor(_bubble, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f));
            _bubble.sizeDelta = new Vector2(520, 130); _bubble.anchoredPosition = new Vector2(-250, 250);
            _bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();
            _bubbleGroup.alpha = 0f; _bubbleGroup.blocksRaycasts = false;
            _bubbleText = UIFactory.AddText("T", _bubble, "", 24, font, new Color(0.15f, 0.13f, 0.2f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_bubbleText.rectTransform, 24, 14, 24, 14);
            _bubbleText.enableAutoSizing = true; _bubbleText.fontSizeMin = 15; _bubbleText.fontSizeMax = 24;

            // 타이틀(왼쪽 위) · 대표 캐릭터 이름(왼쪽 아래)
            var title = UIFactory.AddText("Title", root, gameTitle, 48, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            UIFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            title.rectTransform.sizeDelta = new Vector2(800, 66); title.rectTransform.anchoredPosition = new Vector2(50, -32);
            title.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);

            var nameBg = UIFactory.AddImage("NameBg", root, new Color(0.04f, 0.05f, 0.09f, 0.72f), ProceduralSprites.Rounded);
            nameBg.type = Image.Type.Sliced; nameBg.preserveAspect = false;
            UIFactory.Anchor(nameBg.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            nameBg.rectTransform.sizeDelta = new Vector2(440, 104); nameBg.rectTransform.anchoredPosition = new Vector2(50, 40);
            nameBg.raycastTarget = false;
            _assistStars = StarRow.Create(nameBg.transform, 26, TextAnchor.MiddleLeft, font);
            UIFactory.Anchor(_assistStars.Rect, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1));
            _assistStars.Rect.sizeDelta = new Vector2(-40, 32); _assistStars.Rect.anchoredPosition = new Vector2(20, -12);
            _assistName = UIFactory.AddText("Name", nameBg.transform, "", 30, font, Color.white, TextAlignmentOptions.Left);
            _assistName.richText = true;
            UIFactory.Anchor(_assistName.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0));
            _assistName.rectTransform.sizeDelta = new Vector2(-40, 50); _assistName.rectTransform.anchoredPosition = new Vector2(22, 8);
            _assistName.enableAutoSizing = true; _assistName.fontSizeMin = 16; _assistName.fontSizeMax = 30;

            // 재화(오른쪽 위): 크리스탈 · 금화 알약
            CurrencyBar.Create(root, font, wallet, new Vector2(-50, -36), Currency.Crystal, Currency.Gold);

            // 메뉴(오른쪽): 스테이지(크게) → 가챠 → 2열 타일
            const float cx = 590f, w = 580f;
            var stage = Tile(root, "스테이지\n<size=50%>출격하고 보상 획득</size>", new Color(0.85f, 0.42f, 0.25f, 0.96f), new Vector2(cx, 268), new Vector2(w, 170), 52, () => Nav(HubScreen.Stage));
            var gachaBtn = Tile(root, "가챠\n<size=50%>캐릭터 소환</size>", new Color(0.78f, 0.56f, 0.2f, 0.96f), new Vector2(cx, 118), new Vector2(w, 110), 42, () => Nav(HubScreen.Gacha));

            float tw = (w - 16f) / 2f, lx = cx - (tw + 16f) / 2f, rx = cx + (tw + 16f) / 2f;
            Tile(root, "육성\n<size=55%>레벨 · 스킬 · 초월</size>", new Color(0.33f, 0.55f, 0.4f, 0.95f), new Vector2(lx, -10), new Vector2(tw, 112), 34, () => Nav(HubScreen.Growth));
            var mission = Tile(root, "", new Color(0.3f, 0.45f, 0.8f, 0.95f), new Vector2(rx, -10), new Vector2(tw, 112), 34, () => Nav(HubScreen.Mission));
            _missionLabel = mission.GetComponentInChildren<TextMeshProUGUI>();
            Tile(root, "상점\n<size=55%>구매 · 교환</size>", new Color(0.72f, 0.33f, 0.5f, 0.95f), new Vector2(lx, -136), new Vector2(tw, 112), 34, () => Nav(HubScreen.Shop));
            Tile(root, "가방\n<size=55%>재료 확인</size>", new Color(0.24f, 0.48f, 0.55f, 0.95f), new Vector2(rx, -136), new Vector2(tw, 112), 34, () => Nav(HubScreen.Bag));
            var codex = Tile(root, "", new Color(0.26f, 0.3f, 0.42f, 0.95f), new Vector2(lx, -262), new Vector2(tw, 112), 34, () => Nav(HubScreen.Codex));
            _rosterText = codex.GetComponentInChildren<TextMeshProUGUI>();
            Tile(root, "대표 변경\n<size=55%>로비 캐릭터</size>", new Color(0.45f, 0.35f, 0.7f, 0.95f), new Vector2(rx, -262), new Vector2(tw, 112), 32, OpenPicker);

            BuildPicker();
        }

        private Button Tile(Transform parent, string label, Color bg, Vector2 pos, Vector2 size, float fontSize, UnityEngine.Events.UnityAction onClick)
        {
            var btn = UIFactory.AddButton("Menu", parent, label, fontSize, font, bg, Color.white);
            var rt = (RectTransform)btn.transform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            var lbl = btn.GetComponentInChildren<TextMeshProUGUI>();
            lbl.richText = true;
            lbl.lineSpacing = -6f;
            btn.onClick.AddListener(onClick);
            return btn;
        }

        private void BuildPicker()
        {
            var dim = UIFactory.AddImage("Picker", _root.transform, new Color(0, 0, 0, 0.75f), null, raycast: true);
            _picker = dim.gameObject;
            UIFactory.Stretch(dim.rectTransform);
            var close = dim.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(() => _picker.SetActive(false));

            var frame = UIFactory.Frame(dim.transform);
            var panel = UIFactory.AddImage("Panel", frame, new Color(0.09f, 0.1f, 0.15f, 0.98f), ProceduralSprites.Rounded, raycast: true);
            panel.type = Image.Type.Sliced; panel.preserveAspect = false;
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panel.rectTransform.sizeDelta = new Vector2(1280, 800);

            var title = UIFactory.AddText("Title", panel.transform, "대표 캐릭터 선택", 38, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            UIFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            title.rectTransform.sizeDelta = new Vector2(700, 56); title.rectTransform.anchoredPosition = new Vector2(40, -26);
            var sub = UIFactory.AddText("Sub", panel.transform, "<color=#9AA3B5>홈 화면에 서 있을 캐릭터를 고르세요 (보유 캐릭터)</color>", 20, font, Color.white, TextAlignmentOptions.Left);
            UIFactory.Anchor(sub.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            sub.rectTransform.sizeDelta = new Vector2(900, 30); sub.rectTransform.anchoredPosition = new Vector2(42, -84);

            var x = UIFactory.AddButton("Close", panel.transform, "닫기", 22, font, new Color(1, 1, 1, 0.14f), Color.white);
            var xr = (RectTransform)x.transform;
            UIFactory.Anchor(xr, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            xr.sizeDelta = new Vector2(130, 50); xr.anchoredPosition = new Vector2(-28, -28);
            x.onClick.AddListener(() => _picker.SetActive(false));

            var view = UIFactory.NewRect("View", panel.transform);
            UIFactory.Stretch(view, 30, 30, 30, 130);
            _pickerGrid = UIFactory.NewRect("Grid", view);
            var gl = _pickerGrid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(150, 196); gl.spacing = new Vector2(14, 14);
            gl.padding = new RectOffset(10, 10, 10, 10);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 7;
            gl.childAlignment = TextAnchor.UpperCenter;
            UIFactory.MakeVerticalScroll(view, _pickerGrid);

            _picker.SetActive(false);
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
