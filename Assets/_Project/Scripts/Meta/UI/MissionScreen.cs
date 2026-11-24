using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Meta.Missions;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 임무 화면. 위쪽 탭(일일 임무 · 주간 임무), 임무마다 진행도 막대 · 보상 · 받기 버튼, 아래에 '모두 받기'.
    /// 코드로 자동 구성 — 참조는 비우면 자동 탐색(PlayerMissions가 없으면 지갑 오브젝트에 추가).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Mission Screen")]
    public class MissionScreen : MonoBehaviour
    {
        [SerializeField] private PlayerMissions missions;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private TMP_FontAsset font;

        private GameObject _root;
        private TextMeshProUGUI _hint, _toast, _claimAllLabel;
        private RectTransform _tabs, _content;
        private Button _backBtn, _claimAllBtn;
        private Action _onBack;

        private MissionPeriod _tab = MissionPeriod.Daily;
        private float _timerAcc;
        private string _periodSeen;
        private Coroutine _toastCo;
        private bool _dirty;

        private static readonly MissionPeriod[] Tabs = { MissionPeriod.Daily, MissionPeriod.Weekly };

        private void Awake()
        {
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            if (missions == null) missions = FindAnyObjectByType<PlayerMissions>();
            if (missions == null)
            {
                var host = wallet != null ? wallet.gameObject : gameObject;
                missions = host.AddComponent<PlayerMissions>();
            }
            BuildUI();
        }

        private void OnEnable()
        {
            if (missions != null) missions.OnChanged += MarkDirty;
            if (wallet != null) wallet.OnChanged += MarkDirty;
        }

        private void OnDisable()
        {
            if (missions != null) missions.OnChanged -= MarkDirty;
            if (wallet != null) wallet.OnChanged -= MarkDirty;
        }

        private void Start() => Refresh();

        /// <summary>코드로 생성할 때 폰트 지정(Awake 전, 비활성 상태에서 호출).</summary>
        public void SetFont(TMP_FontAsset f) { if (f != null) font = f; }

        public void Show(bool on)
        {
            if (_root != null) _root.SetActive(on);
            if (on) Refresh();
        }

        public void SetBackAction(Action onBack)
        {
            _onBack = onBack;
            if (_backBtn != null) _backBtn.gameObject.SetActive(onBack != null);
        }

        // 보상을 받는 동안 지갑·임무 이벤트가 여러 번 오므로, 다음 프레임에 한 번만 다시 그림
        private void MarkDirty() => _dirty = true;

        private void Update()
        {
            if (_root == null || !_root.activeSelf) return;
            if (_dirty) { Refresh(); return; }
            _timerAcc += Time.unscaledDeltaTime;
            if (_timerAcc < 1f) return;
            _timerAcc = 0f;
            // 초기화 시각을 넘기면 목록 갱신
            if (Shop.PlayerShop.PeriodKey(Shop.ShopReset.Daily, DateTime.Now) != _periodSeen) Refresh();
            else UpdateHint();
        }

        // ---------------- 갱신 ----------------

        private void Refresh()
        {
            _dirty = false;
            if (_root == null || missions == null) return;
            _periodSeen = Shop.PlayerShop.PeriodKey(Shop.ShopReset.Daily, DateTime.Now);


            RebuildTabs();
            Clear(_content);
            var list = MissionCatalog.Of(_tab);
            // 받을 수 있는 것 → 진행 중 → 받은 것 순
            list.Sort((a, b) => Order(a).CompareTo(Order(b)));
            foreach (var m in list) BuildRow(m);

            int claimable = missions.ClaimableCount(_tab);
            _claimAllBtn.interactable = claimable > 0;
            _claimAllLabel.text = claimable > 0 ? $"모두 받기  <size=75%>({claimable})</size>" : "모두 받기";
            UpdateHint();
        }

        private int Order(Mission m) => missions.CanClaim(m) ? 0 : missions.IsClaimed(m) ? 2 : 1;

        private void UpdateHint()
        {
            var left = PlayerMissions.UntilReset(_tab);
            string when = _tab == MissionPeriod.Daily ? "매일 05:00 초기화" : "매주 월요일 05:00 초기화";
            _hint.text = $"{MissionCatalog.PeriodName(_tab)}  <size=80%><color=#9AA3B5>{when}</color>" +
                         $"   <color=#FFD36B>초기화까지 {Shop.PlayerShop.FormatSpan(left)}</color></size>";
        }

        private void RebuildTabs()
        {
            Clear(_tabs);
            foreach (var t in Tabs)
            {
                var tab = t;
                bool on = _tab == t;
                int n = missions.ClaimableCount(t);
                string label = MissionCatalog.PeriodName(t) + (n > 0 ? "  <color=#FF6B6B><b>!</b></color>" : "");
                var b = UIFactory.AddButton("Tab", _tabs, label, 20, font,
                    on ? new Color(0.78f, 0.58f, 0.22f, 0.95f) : new Color(1, 1, 1, 0.12f), Color.white);
                var le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 210; le.preferredHeight = 44;
                b.onClick.AddListener(() => { _tab = tab; Refresh(); });
            }
        }

        private void BuildRow(Mission m)
        {
            int cur = missions.Progress(m);
            bool claimed = missions.IsClaimed(m);
            bool can = missions.CanClaim(m);

            var row = UIFactory.AddImage("Mission", _content,
                can ? new Color(0.20f, 0.26f, 0.36f, 0.96f) : new Color(0.10f, 0.11f, 0.15f, 0.94f), ProceduralSprites.Rounded);
            row.type = Image.Type.Sliced; row.preserveAspect = false;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 104;
            var group = row.gameObject.AddComponent<CanvasGroup>();
            group.alpha = claimed ? 0.5f : 1f;

            // 제목 (+ 보너스 표시)
            string title = m.IsBonus ? $"<color=#FFD36B>보너스</color>  {m.title}" : m.title;
            var t = Label(row.transform, title, 25, FontStyles.Bold, TextAlignmentOptions.Left);
            UIFactory.Anchor(t.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            t.rectTransform.sizeDelta = new Vector2(640, 36); t.rectTransform.anchoredPosition = new Vector2(28, -16);

            // 진행도 막대
            var barBg = UIFactory.AddImage("BarBg", row.transform, new Color(1, 1, 1, 0.10f));
            UIFactory.Anchor(barBg.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            barBg.rectTransform.sizeDelta = new Vector2(520, 14); barBg.rectTransform.anchoredPosition = new Vector2(28, 22);
            float ratio = m.target > 0 ? Mathf.Clamp01((float)cur / m.target) : 1f;
            var fill = UIFactory.AddImage("Fill", barBg.transform, ratio >= 1f ? new Color(0.45f, 0.85f, 0.55f) : new Color(0.35f, 0.65f, 1f));
            var frt = fill.rectTransform;
            frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(ratio, 1); frt.pivot = new Vector2(0, 0.5f);
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;

            var prog = Label(row.transform, $"{cur:N0} / {m.target:N0}", 20, FontStyles.Bold, TextAlignmentOptions.Left);
            UIFactory.Anchor(prog.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            prog.rectTransform.sizeDelta = new Vector2(160, 28); prog.rectTransform.anchoredPosition = new Vector2(564, 14);
            prog.color = ratio >= 1f ? new Color(0.55f, 0.95f, 0.65f) : new Color(0.8f, 0.83f, 0.9f);

            // 보상
            var rew = Label(row.transform, m.RewardText, 22, FontStyles.Bold, TextAlignmentOptions.Right);
            UIFactory.Anchor(rew.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            rew.rectTransform.sizeDelta = new Vector2(460, 60); rew.rectTransform.anchoredPosition = new Vector2(-250, 0);
            rew.enableAutoSizing = true; rew.fontSizeMin = 15; rew.fontSizeMax = 22;

            // 받기 버튼
            string label = claimed ? "받음" : can ? "받기" : "진행 중";
            var btn = UIFactory.AddButton("Claim", row.transform, label, 24, font,
                can ? new Color(0.25f, 0.62f, 0.95f, 0.98f) : new Color(1, 1, 1, 0.12f), Color.white);
            var brt = (RectTransform)btn.transform;
            UIFactory.Anchor(brt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            brt.sizeDelta = new Vector2(190, 66); brt.anchoredPosition = new Vector2(-28, 0);
            btn.interactable = can;
            var mission = m;
            btn.onClick.AddListener(() => OnClaim(mission));
        }

        // ---------------- 수령 ----------------

        private void OnClaim(Mission m)
        {
            if (missions == null || !missions.Claim(m)) return;
            Toast($"획득!  {m.RewardText}");
            Refresh();
        }

        private void OnClaimAll()
        {
            if (missions == null) return;
            var got = missions.ClaimAll(_tab);
            if (got.Count == 0) return;
            Toast($"임무 {got.Count}개 보상을 받았습니다");
            Refresh();
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
            var cgo = UIFactory.NewRect("MissionCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 74;
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 어떤 화면비에서도 1920×1080 설계 영역이 전부 보이게(남는 쪽은 여백)
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.05f, 0.06f, 0.09f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            BackgroundArt.Create(_root.transform, 0.74f).Set("Backgrounds/mission", "Backgrounds/party");
            var root = _root.transform;

            var title = UIFactory.AddText("Title", root, "임무", 40, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(600, 56), new Vector2(0, -30));

            CurrencyBar.Create(root, font, wallet, new Vector2(-50, -32), Currency.Crystal, Currency.Gold);
            CurrencyBar.Create(root, font, wallet, new Vector2(-50, -94), Currency.PickupTicket, Currency.StandardTicket);   // 임무 보상에 뽑기권이 있어 함께

            // 탭
            _tabs = UIFactory.NewRect("Tabs", root);
            Place(_tabs, new Vector2(0.5f, 1), new Vector2(1400, 44), new Vector2(0, -112));
            var th = _tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            th.spacing = 10; th.childAlignment = TextAnchor.MiddleLeft;
            th.childControlWidth = true; th.childControlHeight = true;
            th.childForceExpandWidth = false; th.childForceExpandHeight = false;

            _hint = UIFactory.AddText("Hint", root, "", 22, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(_hint.rectTransform, new Vector2(0.5f, 1), new Vector2(1400, 32), new Vector2(0, -168));

            // 임무 목록(스크롤)
            var view = UIFactory.NewRect("View", root);
            Place(view, new Vector2(0.5f, 1), new Vector2(1400, 700), new Vector2(0, -210));
            view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.25f);
            view.gameObject.AddComponent<RectMask2D>();
            _content = UIFactory.NewRect("Content", view);
            UIFactory.Anchor(_content, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            _content.sizeDelta = Vector2.zero; _content.anchoredPosition = Vector2.zero;
            var vl = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 10; vl.padding = new RectOffset(14, 14, 14, 14);
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = _content; scroll.viewport = view;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            // 아래: 알림 + 모두 받기
            _toast = UIFactory.AddText("Toast", root, "", 24, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(_toast.rectTransform, new Vector2(0.5f, 0), new Vector2(1000, 40), new Vector2(-200, 62), new Vector2(0.5f, 0));
            _toast.richText = true;
            _toast.alpha = 0f;

            _claimAllBtn = UIFactory.AddButton("ClaimAll", root, "모두 받기", 26, font, new Color(0.8f, 0.6f, 0.2f, 0.95f), Color.white);
            Place((RectTransform)_claimAllBtn.transform, new Vector2(0.5f, 0), new Vector2(300, 72), new Vector2(550, 46), new Vector2(0.5f, 0));
            _claimAllLabel = _claimAllBtn.GetComponentInChildren<TextMeshProUGUI>();
            _claimAllLabel.richText = true;
            _claimAllBtn.onClick.AddListener(OnClaimAll);

            _backBtn = UIFactory.AddButton("BackBtn", root, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            Place((RectTransform)_backBtn.transform, new Vector2(0, 1), new Vector2(150, 44), new Vector2(30, -30), new Vector2(0, 1));
            _backBtn.onClick.AddListener(() => _onBack?.Invoke());
            _backBtn.gameObject.SetActive(false);
        }

        private TextMeshProUGUI Label(Transform parent, string text, float size, FontStyles style, TextAlignmentOptions align)
        {
            var t = UIFactory.AddText("T", parent, text, size, font, Color.white, align, style);
            t.richText = true;
            return t;
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
