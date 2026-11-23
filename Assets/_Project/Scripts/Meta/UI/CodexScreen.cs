using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;
using SubcultureRPG.Meta.Codex;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 캐릭터 도감. 왼쪽: 전체 캐릭터 칸(보유 = 컬러, 미보유 = 어둡게) + 성급 탭, 아래 수집 보상.
    /// 오른쪽: 선택한 캐릭터의 일러스트 · 속성/역할 · 대표 대사 · 획득처 · 보유 정보. 보유 캐릭터는 일러스트를 크게 볼 수 있습니다.
    /// 코드로 자동 구성 — 참조는 비우면 자동 탐색(PlayerCodex가 없으면 로스터 오브젝트에 추가).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Codex Screen")]
    public class CodexScreen : MonoBehaviour
    {
        [SerializeField] private PlayerCodex codex;
        [SerializeField] private TMP_FontAsset font;

        private GameObject _root, _viewer;
        private Image _portrait, _viewerImage;
        private TextMeshProUGUI _progress, _name, _stars, _traits, _line, _source, _owned, _lockLabel, _toast;
        private RectTransform _tabs, _grid, _milestones;
        private Button _backBtn, _viewBtn;
        private Action _onBack;

        private int _rarityTab;   // 0 = 전체, 그 외 성급(3~6)
        private CharacterData _selected;
        private bool _dirty;
        private Coroutine _toastCo;

        private static readonly int[] RarityTabs = { 0, 6, 5, 4, 3 };
        private static readonly Color Locked = new(0.16f, 0.18f, 0.24f, 1f);

        private void Awake()
        {
            if (codex == null) codex = FindAnyObjectByType<PlayerCodex>();
            if (codex == null)
            {
                var roster = FindAnyObjectByType<PlayerRoster>();
                var host = roster != null ? roster.gameObject : gameObject;
                codex = host.AddComponent<PlayerCodex>();
            }
            BuildUI();
        }

        private void OnEnable() { if (codex != null) codex.OnChanged += MarkDirty; }
        private void OnDisable() { if (codex != null) codex.OnChanged -= MarkDirty; }

        private void Start() => Refresh();

        /// <summary>코드로 생성할 때 폰트 지정(Awake 전, 비활성 상태에서 호출).</summary>
        public void SetFont(TMP_FontAsset f) { if (f != null) font = f; }

        public void Show(bool on)
        {
            if (_root != null) _root.SetActive(on);
            if (_viewer != null) _viewer.SetActive(false);
            if (on) Refresh();
        }

        public void SetBackAction(Action onBack)
        {
            _onBack = onBack;
            if (_backBtn != null) _backBtn.gameObject.SetActive(onBack != null);
        }

        private void MarkDirty() => _dirty = true;

        private void Update()
        {
            if (_dirty && _root != null && _root.activeSelf) Refresh();
        }

        // ---------------- 갱신 ----------------

        private void Refresh()
        {
            _dirty = false;
            if (_root == null || codex == null) return;

            var all = codex.All();
            int owned = 0;
            foreach (var c in all) if (codex.IsOwned(c)) owned++;
            int pct = all.Count > 0 ? Mathf.RoundToInt(100f * owned / all.Count) : 0;
            _progress.text = $"수집  <color=#FFD36B>{owned}</color> / {all.Count}   <size=80%><color=#9AA3B5>{pct}%</color></size>";

            RebuildTabs(all);

            Clear(_grid);
            var shown = new List<CharacterData>();
            foreach (var c in all) if (_rarityTab == 0 || (int)c.rarity == _rarityTab) shown.Add(c);
            if (_selected == null || !shown.Contains(_selected)) _selected = shown.Count > 0 ? shown[0] : null;
            foreach (var c in shown) BuildCard(c);

            RebuildMilestones(owned);
            RefreshDetail();
        }

        private void RebuildTabs(List<CharacterData> all)
        {
            Clear(_tabs);
            foreach (int r in RarityTabs)
            {
                int rarity = r;
                int total = 0, have = 0;
                foreach (var c in all)
                    if (rarity == 0 || (int)c.rarity == rarity) { total++; if (codex.IsOwned(c)) have++; }
                if (total == 0) continue;
                bool on = _rarityTab == rarity;
                string name = rarity == 0 ? "전체" : $"<color=#{ColorUtility.ToHtmlStringRGB(StarText.ColorOf(rarity))}>★</color>{rarity}";
                var b = UIFactory.AddButton("Tab", _tabs, $"{name}  <size=75%>{have}/{total}</size>", 20, font,
                    on ? new Color(0.78f, 0.58f, 0.22f, 0.95f) : new Color(1, 1, 1, 0.12f), Color.white);
                var le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 170; le.preferredHeight = 44;
                b.onClick.AddListener(() => { _rarityTab = rarity; Refresh(); });
            }
        }

        private void BuildCard(CharacterData c)
        {
            bool have = codex.IsOwned(c);
            int star = (int)c.rarity;
            var starColor = StarText.ColorOf(star);

            var card = UIFactory.NewRect("Card", _grid);
            var hl = UIFactory.AddImage("HL", card, new Color(0.3f, 0.85f, 1f, c == _selected ? 1f : 0f));
            UIFactory.Stretch(hl.rectTransform, -4, -4, -4, -4);

            var frame = UIFactory.AddImage("Frame", card, new Color(starColor.r, starColor.g, starColor.b, have ? 0.95f : 0.4f), null, raycast: true);
            UIFactory.Stretch(frame.rectTransform);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            var cc = c;
            btn.onClick.AddListener(() => { _selected = cc; Refresh(); });

            var inner = UIFactory.AddImage("Inner", frame.transform, new Color(0.10f, 0.11f, 0.14f, 1f));
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);

            var sprite = GameArt.Thumb(c);
            var portrait = UIFactory.AddImage("Portrait", inner.transform, have ? Color.white : Locked);
            UIFactory.Stretch(portrait.rectTransform, 5, 32, 5, 5);
            portrait.sprite = sprite; portrait.enabled = sprite != null; portrait.preserveAspect = true;

            if (!have)
            {
                var q = UIFactory.AddText("Lock", inner.transform, "미보유", 16, font, new Color(0.75f, 0.78f, 0.86f), TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Stretch(q.rectTransform, 4, 32, 4, 4);
            }

            var bar = UIFactory.AddImage("Bar", inner.transform, new Color(starColor.r, starColor.g, starColor.b, have ? 0.9f : 0.3f));
            UIFactory.Anchor(bar.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            bar.rectTransform.sizeDelta = new Vector2(0, 28); bar.rectTransform.anchoredPosition = Vector2.zero;
            var name = UIFactory.AddText("Name", bar.transform, c.displayName, 14, font, have ? Color.white : new Color(0.75f, 0.77f, 0.82f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(name.rectTransform);

            var shade = UIFactory.AddImage("StarShade", inner.transform, new Color(0f, 0f, 0f, 0.6f));
            UIFactory.Anchor(shade.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            shade.rectTransform.sizeDelta = new Vector2(46, 22); shade.rectTransform.anchoredPosition = Vector2.zero;
            var st = UIFactory.AddText("Star", shade.transform, $"★{star}", 15, font, starColor, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(st.rectTransform);
        }

        private void RebuildMilestones(int owned)
        {
            Clear(_milestones);
            foreach (var ms in PlayerCodex.Milestones)
            {
                var m = ms;
                bool claimed = codex.IsClaimed(m);
                bool can = codex.CanClaim(m);

                var chip = UIFactory.AddImage("Milestone", _milestones,
                    can ? new Color(0.20f, 0.26f, 0.36f, 0.96f) : new Color(0.10f, 0.11f, 0.15f, 0.94f), ProceduralSprites.Rounded);
                chip.type = Image.Type.Sliced; chip.preserveAspect = false;
                var le = chip.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 190; le.preferredHeight = 168;
                chip.gameObject.AddComponent<CanvasGroup>().alpha = claimed ? 0.5f : 1f;

                var head = UIFactory.AddText("Head", chip.transform, $"{m.count}명 수집", 20, font, new Color(1f, 0.85f, 0.45f), TextAlignmentOptions.Center, FontStyles.Bold);
                Place(head.rectTransform, new Vector2(0.5f, 1), new Vector2(180, 28), new Vector2(0, -10));

                var rew = UIFactory.AddText("Reward", chip.transform, m.RewardText, 16, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                Place(rew.rectTransform, new Vector2(0.5f, 1), new Vector2(180, 56), new Vector2(0, -42));
                rew.richText = true; rew.enableAutoSizing = true; rew.fontSizeMin = 12; rew.fontSizeMax = 16;

                string label = claimed ? "받음" : can ? "받기" : $"{Mathf.Min(owned, m.count)} / {m.count}";
                var btn = UIFactory.AddButton("Claim", chip.transform, label, 20, font,
                    can ? new Color(0.25f, 0.62f, 0.95f, 0.98f) : new Color(1, 1, 1, 0.12f), Color.white);
                Place((RectTransform)btn.transform, new Vector2(0.5f, 0), new Vector2(160, 48), new Vector2(0, 12), new Vector2(0.5f, 0));
                btn.interactable = can;
                btn.onClick.AddListener(() =>
                {
                    if (!codex.Claim(m)) return;
                    Toast($"수집 보상 획득!  {m.RewardText.Replace("\n", "   ")}");
                    Refresh();
                });
            }
        }

        private void RefreshDetail()
        {
            var c = _selected;
            if (c == null)
            {
                _portrait.enabled = false;
                _name.text = ""; _stars.text = ""; _traits.text = ""; _line.text = ""; _source.text = ""; _owned.text = "";
                _lockLabel.text = "";
                _viewBtn.gameObject.SetActive(false);
                return;
            }

            bool have = codex.IsOwned(c);
            var sprite = GameArt.Portrait(c);
            _portrait.sprite = sprite;
            _portrait.enabled = sprite != null;
            _portrait.color = have ? Color.white : Locked;
            _lockLabel.text = have ? "" : "미보유";

            int star = (int)c.rarity;
            string starHex = "#" + ColorUtility.ToHtmlStringRGB(StarText.ColorOf(star));
            _name.text = c.displayName;
            _stars.text = $"<color={starHex}>{new string('★', star)}</color>  <size=70%><color=#9AA3B5>태생 {star}성</color></size>";
            _traits.text = $"<color={Traits.Hex(c.element)}>{Traits.Name(c.element)}</color> · {Traits.Name(c.role)}";
            _line.text = have ? $"「 {c.SignatureLine} 」" : "<color=#9AA3B5>획득하면 대표 대사를 볼 수 있습니다</color>";
            _source.text = $"<color=#FFD36B>획득처</color>\n{codex.SourceOf(c)}";

            var o = have && codex.Roster != null ? codex.Roster.GetOwned(c) : null;
            if (o != null)
            {
                int curStar = Transcendence.CurrentStar(c.rarity, o.star);
                _owned.text = "<color=#FFD36B>보유 정보</color>\n" +
                              $"Lv.{o.level}" + (o.ascension > 0 ? $"  ·  돌파 {o.ascension}" : "") + "\n" +
                              $"현재 {StarText.Rich(curStar, o.transcend)}\n" +
                              $"중복 {o.Duplicates}장";
            }
            else _owned.text = "<color=#FFD36B>보유 정보</color>\n<color=#9AA3B5>아직 획득하지 못한 캐릭터입니다</color>";

            _viewBtn.gameObject.SetActive(have && sprite != null);
        }

        private void OpenViewer()
        {
            var sprite = GameArt.Portrait(_selected);
            if (sprite == null || !codex.IsOwned(_selected)) return;
            _viewerImage.sprite = sprite;
            _viewer.SetActive(true);
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
            yield return new WaitForSecondsRealtime(1.8f);
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
            var cgo = UIFactory.NewRect("CodexCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.05f, 0.06f, 0.09f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            BackgroundArt.Create(_root.transform, 0.74f).Set("Backgrounds/codex", "Backgrounds/growth");
            var root = _root.transform;

            var title = UIFactory.AddText("Title", root, "캐릭터 도감", 40, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(600, 56), new Vector2(0, -30));

            _progress = UIFactory.AddText("Progress", root, "", 28, font, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
            Place(_progress.rectTransform, new Vector2(1, 1), new Vector2(700, 40), new Vector2(-50, -38), new Vector2(1, 1));

            // 성급 탭
            _tabs = UIFactory.NewRect("Tabs", root);
            Place(_tabs, new Vector2(0, 1), new Vector2(1010, 44), new Vector2(50, -104), new Vector2(0, 1));
            var th = _tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            th.spacing = 10; th.childAlignment = TextAnchor.MiddleLeft;
            th.childControlWidth = true; th.childControlHeight = true;
            th.childForceExpandWidth = false; th.childForceExpandHeight = false;

            // 캐릭터 목록(스크롤)
            var view = UIFactory.NewRect("View", root);
            Place(view, new Vector2(0, 1), new Vector2(1010, 620), new Vector2(50, -160), new Vector2(0, 1));
            view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.25f);
            view.gameObject.AddComponent<RectMask2D>();
            _grid = UIFactory.NewRect("Grid", view);
            UIFactory.Anchor(_grid, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            _grid.sizeDelta = Vector2.zero; _grid.anchoredPosition = Vector2.zero;
            var gl = _grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(150, 186); gl.spacing = new Vector2(12, 12);
            gl.padding = new RectOffset(16, 16, 16, 16);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 6;
            _grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = _grid; scroll.viewport = view;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            // 수집 보상(목록 아래)
            var msTitle = UIFactory.AddText("MsTitle", root, "수집 보상", 22, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(msTitle.rectTransform, new Vector2(0, 1), new Vector2(400, 30), new Vector2(54, -792), new Vector2(0, 1));
            _toast = UIFactory.AddText("Toast", root, "", 20, font, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
            Place(_toast.rectTransform, new Vector2(0, 1), new Vector2(820, 30), new Vector2(240, -792), new Vector2(0, 1));
            _toast.richText = true; _toast.alpha = 0f;

            _milestones = UIFactory.NewRect("Milestones", root);
            Place(_milestones, new Vector2(0, 1), new Vector2(1010, 168), new Vector2(50, -830), new Vector2(0, 1));
            var mh = _milestones.gameObject.AddComponent<HorizontalLayoutGroup>();
            mh.spacing = 15; mh.childAlignment = TextAnchor.MiddleLeft;
            mh.childControlWidth = true; mh.childControlHeight = true;
            mh.childForceExpandWidth = false; mh.childForceExpandHeight = false;

            // 오른쪽: 상세
            var panel = UIFactory.AddImage("Detail", root, new Color(0.10f, 0.10f, 0.14f, 0.96f));
            Place(panel.rectTransform, new Vector2(1, 1), new Vector2(790, 894), new Vector2(-50, -104), new Vector2(1, 1));
            var pt = panel.transform;

            // 일러스트(왼쪽 위, 3:4)
            var artBg = UIFactory.AddImage("ArtBg", pt, new Color(0, 0, 0, 0.35f));
            Place(artBg.rectTransform, new Vector2(0, 1), new Vector2(420, 560), new Vector2(24, -24), new Vector2(0, 1));
            _portrait = UIFactory.AddImage("Portrait", artBg.transform, Color.white);
            UIFactory.Stretch(_portrait.rectTransform);
            _portrait.preserveAspect = true;
            _lockLabel = UIFactory.AddText("Lock", artBg.transform, "", 34, font, new Color(0.8f, 0.83f, 0.9f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_lockLabel.rectTransform);

            // 정보(일러스트 오른쪽)
            const float ix = 468, iw = 300;
            _name = UIFactory.AddText("Name", pt, "", 38, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(_name.rectTransform, new Vector2(0, 1), new Vector2(iw, 50), new Vector2(ix, -26), new Vector2(0, 1));
            _name.enableAutoSizing = true; _name.fontSizeMin = 22; _name.fontSizeMax = 38;
            _stars = UIFactory.AddText("Stars", pt, "", 24, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(_stars.rectTransform, new Vector2(0, 1), new Vector2(iw, 34), new Vector2(ix, -84), new Vector2(0, 1));
            _stars.enableAutoSizing = true; _stars.fontSizeMin = 14; _stars.fontSizeMax = 24;
            _traits = UIFactory.AddText("Traits", pt, "", 26, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(_traits.rectTransform, new Vector2(0, 1), new Vector2(iw, 36), new Vector2(ix, -128), new Vector2(0, 1));

            _source = UIFactory.AddText("Source", pt, "", 20, font, new Color(0.88f, 0.9f, 0.96f), TextAlignmentOptions.TopLeft);
            Place(_source.rectTransform, new Vector2(0, 1), new Vector2(iw, 130), new Vector2(ix, -196), new Vector2(0, 1));
            _source.lineSpacing = 6f;
            _owned = UIFactory.AddText("Owned", pt, "", 20, font, new Color(0.88f, 0.9f, 0.96f), TextAlignmentOptions.TopLeft);
            Place(_owned.rectTransform, new Vector2(0, 1), new Vector2(iw, 170), new Vector2(ix, -346), new Vector2(0, 1));
            _owned.lineSpacing = 6f;

            _viewBtn = UIFactory.AddButton("View", pt, "일러스트 크게 보기", 20, font, new Color(0.25f, 0.62f, 0.95f, 0.98f), Color.white);
            Place((RectTransform)_viewBtn.transform, new Vector2(0, 1), new Vector2(iw, 56), new Vector2(ix, -528), new Vector2(0, 1));
            _viewBtn.onClick.AddListener(OpenViewer);

            // 대표 대사(일러스트 아래)
            var lineBg = UIFactory.AddImage("LineBg", pt, new Color(1, 1, 1, 0.05f), ProceduralSprites.Rounded);
            lineBg.type = Image.Type.Sliced; lineBg.preserveAspect = false;
            Place(lineBg.rectTransform, new Vector2(0.5f, 1), new Vector2(742, 250), new Vector2(0, -612));
            _line = UIFactory.AddText("Line", lineBg.transform, "", 26, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_line.rectTransform, 30, 20, 30, 20);
            _line.lineSpacing = 10f;
            _line.overflowMode = TextOverflowModes.Overflow;
            _line.enableAutoSizing = true; _line.fontSizeMin = 16; _line.fontSizeMax = 26;

            _backBtn = UIFactory.AddButton("BackBtn", root, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            Place((RectTransform)_backBtn.transform, new Vector2(0, 1), new Vector2(150, 44), new Vector2(30, -30), new Vector2(0, 1));
            _backBtn.onClick.AddListener(() => _onBack?.Invoke());
            _backBtn.gameObject.SetActive(false);

            BuildViewer(canvas.transform);
        }

        /// <summary>일러스트 크게 보기(화면을 누르면 닫힘).</summary>
        private void BuildViewer(Transform canvas)
        {
            var dim = UIFactory.AddImage("Viewer", canvas, new Color(0, 0, 0, 0.92f), null, raycast: true);
            UIFactory.Stretch(dim.rectTransform);
            _viewer = dim.gameObject;
            var close = _viewer.AddComponent<Button>();
            close.targetGraphic = dim;
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(() => _viewer.SetActive(false));

            _viewerImage = UIFactory.AddImage("Image", dim.transform, Color.white);
            UIFactory.Stretch(_viewerImage.rectTransform, 40, 60, 40, 30);
            _viewerImage.preserveAspect = true;

            var hint = UIFactory.AddText("Hint", dim.transform, "화면을 누르면 닫힙니다", 18, font, new Color(0.7f, 0.73f, 0.8f), TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(600, 28), new Vector2(0, 18), new Vector2(0.5f, 0));

            _viewer.SetActive(false);
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos, Vector2? pivot = null)
        {
            UIFactory.Anchor(rt, anchor, anchor, pivot ?? new Vector2(0.5f, 1));
            rt.sizeDelta = size; rt.anchoredPosition = pos;
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

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
