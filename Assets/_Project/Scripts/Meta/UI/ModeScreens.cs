using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>결투장 · 주간 보스 화면 공통 틀(캔버스 · 배경 · 제목 · 홈 버튼 · 재화).</summary>
    public abstract class ModeScreenBase : MonoBehaviour
    {
        [SerializeField] protected TMP_FontAsset font;
        protected GameObject _root;
        protected RectTransform _frame;
        protected Button _backBtn;
        protected TextMeshProUGUI _titleText;
        private System.Action _onBack;
        private TextMeshProUGUI _toast;

        public void SetFont(TMP_FontAsset f) { if (font == null) font = f; }

        public void SetBackAction(System.Action onBack)
        {
            _onBack = onBack;
            if (_backBtn != null) _backBtn.gameObject.SetActive(onBack != null);
        }

        public void Show(bool on)
        {
            if (!on) CloseDialog();
            if (_root != null) _root.SetActive(on);
            if (on) Refresh();
        }

        // ---------------- 대화 상자 ----------------

        private GameObject _dialog;

        protected void CloseDialog()
        {
            if (_dialog != null) Destroy(_dialog);
            _dialog = null;
        }

        /// <summary>화면 위 창(어두운 바탕을 누르면 닫힘). 반환 = 창 안쪽(제목 아래부터 쓰기).</summary>
        protected RectTransform OpenDialog(string title, float w, float h)
        {
            CloseDialog();
            var holder = UIFactory.NewRect("Dialog", _root.transform);
            UIFactory.Stretch(holder);
            _dialog = holder.gameObject;
            var dim = UIFactory.AddImage("Dim", holder, new Color(0, 0, 0, 0.74f), null, raycast: true);
            UIFactory.Stretch(dim.rectTransform);
            var db = dim.gameObject.AddComponent<Button>();
            db.targetGraphic = dim; db.transition = Selectable.Transition.None;
            db.onClick.AddListener(CloseDialog);
            var panel = Panel(holder, new Color(0.08f, 0.09f, 0.14f, 0.98f));   // 바탕과 형제(창을 눌러도 닫히지 않게)
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panel.rectTransform.sizeDelta = new Vector2(w, h);
            var t = Text(panel.transform, $"<b>{title}</b>", 32, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(t.rectTransform, 30, -18, w - 220, 48);
            var close = UIFactory.AddButton("Close", panel.transform, "닫기", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            UIFactory.Anchor((RectTransform)close.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            ((RectTransform)close.transform).sizeDelta = new Vector2(120, 44); ((RectTransform)close.transform).anchoredPosition = new Vector2(-20, -20);
            close.onClick.AddListener(CloseDialog);
            return panel.rectTransform;
        }

        /// <summary>확인 창: 본문 + [yes] / 취소.</summary>
        protected void Confirm(string message, string yes, System.Action onYes)
        {
            var p = OpenDialog("확인", 760, 330);
            var body = Text(p, message, 26, new Color(0.9f, 0.92f, 0.96f), FontStyles.Normal, TextAlignmentOptions.Center);
            body.textWrappingMode = TextWrappingModes.Normal;
            UIFactory.Stretch(body.rectTransform, 40, 110, 40, 80);
            var ok = UIFactory.AddButton("Yes", p, yes, 24, font, new Color(0.25f, 0.55f, 0.9f, 0.96f), Color.white);
            UIFactory.Anchor((RectTransform)ok.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            ((RectTransform)ok.transform).sizeDelta = new Vector2(220, 60); ((RectTransform)ok.transform).anchoredPosition = new Vector2(-124, 30);
            ok.onClick.AddListener(() => { CloseDialog(); onYes?.Invoke(); });
            var no = UIFactory.AddButton("No", p, "취소", 24, font, new Color(1, 1, 1, 0.14f), Color.white);
            UIFactory.Anchor((RectTransform)no.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            ((RectTransform)no.transform).sizeDelta = new Vector2(220, 60); ((RectTransform)no.transform).anchoredPosition = new Vector2(124, 30);
            no.onClick.AddListener(CloseDialog);
        }

        /// <summary>알림 창: 본문 + 확인.</summary>
        protected RectTransform Notice(string title, string message, float h = 420)
        {
            var p = OpenDialog(title, 820, h);
            var body = Text(p, message, 26, new Color(0.9f, 0.92f, 0.96f), FontStyles.Normal, TextAlignmentOptions.Center);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = 12f;
            UIFactory.Stretch(body.rectTransform, 40, 110, 40, 80);
            var ok = UIFactory.AddButton("Ok", p, "확인", 24, font, new Color(0.25f, 0.55f, 0.9f, 0.96f), Color.white);
            UIFactory.Anchor((RectTransform)ok.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            ((RectTransform)ok.transform).sizeDelta = new Vector2(240, 60); ((RectTransform)ok.transform).anchoredPosition = new Vector2(0, 30);
            ok.onClick.AddListener(CloseDialog);
            return p;
        }

        /// <summary>입력 칸(TMP_InputField)을 코드로 구성.</summary>
        protected TMP_InputField Input(Transform parent, string placeholder, int limit, float size)
        {
            var bg = UIFactory.AddImage("Input", parent, new Color(1, 1, 1, 0.12f), ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            var area = UIFactory.NewRect("Text Area", bg.transform);
            UIFactory.Stretch(area, 20, 6, 20, 6);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = UIFactory.AddText("Placeholder", area, placeholder, size, font, new Color(0.6f, 0.63f, 0.7f), TextAlignmentOptions.Left, FontStyles.Italic);
            UIFactory.Stretch(ph.rectTransform);
            var text = UIFactory.AddText("Text", area, "", size, font, Color.white, TextAlignmentOptions.Left);
            UIFactory.Stretch(text.rectTransform);
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;
            var field = bg.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = ph;
            field.characterLimit = limit;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.richText = false;
            if (font != null) field.fontAsset = font;
            field.pointSize = size;
            field.caretColor = Color.white;
            field.customCaretColor = true;
            field.selectionColor = new Color(0.4f, 0.6f, 1f, 0.5f);
            InputFocusFx.Attach(field);   // 입력 중 표시(밝은 칸 · 금빛 밑줄 · 굵은 커서)
            return field;
        }

        protected abstract void Refresh();

        protected void BuildBase(string title, string background, int order)
        {
            var cgo = UIFactory.NewRect(title + "Canvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
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

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.05f, 0.05f, 0.08f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            BackgroundArt.Create(_root.transform, 0.6f).Set(background);
            _frame = UIFactory.Frame(_root.transform);

            var t = Text(_frame, title, 40, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Top(t.rectTransform, 0, -30, 600, 56, new Vector2(0.5f, 1));
            _titleText = t;

            _backBtn = UIFactory.AddButton("Back", _frame, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            Top((RectTransform)_backBtn.transform, 30, -30, 150, 44, new Vector2(0, 1));
            _backBtn.onClick.AddListener(() => _onBack?.Invoke());
            _backBtn.gameObject.SetActive(false);

            _toast = Text(_frame, "", 24, new Color(1f, 0.6f, 0.5f), FontStyles.Bold, TextAlignmentOptions.Center);
            UIFactory.Anchor(_toast.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            _toast.rectTransform.sizeDelta = new Vector2(1200, 36); _toast.rectTransform.anchoredPosition = new Vector2(0, 26);
        }

        protected void Toast(string msg)
        {
            if (_toast == null) return;
            _toast.text = msg;
            CancelInvoke(nameof(ClearToast));
            Invoke(nameof(ClearToast), 2f);
        }

        private void ClearToast() { if (_toast != null) _toast.text = ""; }

        // ---------------- 도우미 ----------------

        protected TextMeshProUGUI Text(Transform parent, string s, float size, Color c, FontStyles style, TextAlignmentOptions align)
        {
            var t = UIFactory.AddText("T", parent, s, size, font, c, align, style);
            t.richText = true;
            t.enableAutoSizing = true; t.fontSizeMin = Mathf.Max(11, size * 0.6f); t.fontSizeMax = size;
            return t;
        }

        /// <summary>위 기준 배치(anchor = 위쪽 점, pivot 같음).</summary>
        protected static void Top(RectTransform rt, float x, float y, float w, float h, Vector2 anchor)
        {
            UIFactory.Anchor(rt, anchor, anchor, anchor);
            rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>왼쪽 위 기준 배치.</summary>
        protected static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            UIFactory.Anchor(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, y);
        }

        protected Image Panel(Transform parent, Color c)
        {
            var img = UIFactory.AddImage("Panel", parent, c, ProceduralSprites.Rounded, raycast: true);
            img.type = Image.Type.Sliced; img.preserveAspect = false;
            return img;
        }

        protected static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var ch = t.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
        }

        /// <param name="mine">내 캐릭터면 내 옷장(개화 등) 그림, 아니면(결투장 상대 · 보너스 캐릭터 소개) 기본 의상</param>
        protected Image Thumb(Transform parent, CharacterData c, float x, float y, float size, int star, bool mine = true)
        {
            var col = StarText.ColorOf(star);
            var cell = UIFactory.AddImage("Cell", parent, new Color(col.r, col.g, col.b, 0.9f));
            Place(cell.rectTransform, x, y, size, size);
            var inner = UIFactory.AddImage("Inner", cell.transform, new Color(0.1f, 0.1f, 0.15f, 1f));
            UIFactory.Stretch(inner.rectTransform, 2, 2, 2, 2);
            var th = c != null ? GameArt.Thumb(c, GameArt.SuffixFor(c, mine)) : null;
            if (th != null)
            {
                var img = UIFactory.AddImage("Thumb", inner.transform, Color.white, th);
                img.preserveAspect = true;
                UIFactory.Stretch(img.rectTransform, 1, 1, 1, 1);
            }
            return cell;
        }
    }

    /// <summary>
    /// 결투장(PVP) 화면: 내 랭크(티어 · 점수 · 전적) · 도전권 · 상대 3명(쉬움/보통/어려움 — 팀 · 전투력 · 보상) · 새로고침.
    /// '도전' → 편성(출격 모드) → 전투.
    /// </summary>
    public class ArenaScreen : ModeScreenBase
    {
        /// <summary>'도전'을 눌렀을 때(상대).</summary>
        public event System.Action<PlayerArena.Opponent> OnChallenge;

        private TextMeshProUGUI _rank, _points, _rankLine, _record, _tickets, _ticketSub, _myPower, _season, _refreshLabel;
        private Image _tierIcon, _tierGlow;
        private float _refreshTick;
        private RectTransform _list;

        private const float CardW = 440f, CardH = 742f, CardGap = 20f, RightX = 520f;

        private void Awake()
        {
            BuildBase("결투장", "Backgrounds/arena", 73);
            CurrencyBar.Create(_frame, font, null, new Vector2(-50, -32), Currency.Crystal, Currency.Gold);

            // ---- 왼쪽: 내 랭크 카드 ----
            var me = Panel(_frame, new Color(0.06f, 0.07f, 0.11f, 0.9f));
            Place(me.rectTransform, 40, -130, 460, 880);
            var mt = me.transform;
            _tierGlow = UIFactory.AddImage("Glow", mt, Color.white, ProceduralSprites.Glow);
            Top(_tierGlow.rectTransform, 0, -4, 300, 260, new Vector2(0.5f, 1));
            _tierIcon = UIFactory.AddImage("TierIcon", mt, Color.white, GameArt.Load("Items/ArenaCoin"));
            _tierIcon.preserveAspect = true;
            Top(_tierIcon.rectTransform, 0, -24, 210, 210, new Vector2(0.5f, 1));
            _rank = Text(mt, "", 36, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Top(_rank.rectTransform, 0, -238, 420, 48, new Vector2(0.5f, 1));
            _points = Text(mt, "", 30, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Top(_points.rectTransform, 0, -286, 420, 40, new Vector2(0.5f, 1));
            _rankLine = Text(mt, "", 22, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            Top(_rankLine.rectTransform, 0, -330, 420, 32, new Vector2(0.5f, 1));
            _record = Text(mt, "", 22, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            Top(_record.rectTransform, 0, -364, 420, 32, new Vector2(0.5f, 1));
            var line = UIFactory.AddImage("Line", mt, new Color(1, 1, 1, 0.1f));
            Top(line.rectTransform, 0, -410, 400, 2, new Vector2(0.5f, 1));
            _myPower = Text(mt, "", 22, new Color(0.85f, 0.88f, 0.94f), FontStyles.Normal, TextAlignmentOptions.Center);
            Top(_myPower.rectTransform, 0, -424, 420, 34, new Vector2(0.5f, 1));
            _season = Text(mt, "", 20, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            Top(_season.rectTransform, 0, -460, 420, 30, new Vector2(0.5f, 1));

            // 도전권 상자
            var tb = UIFactory.AddImage("TicketBox", mt, new Color(1, 1, 1, 0.06f), ProceduralSprites.Rounded);
            tb.type = Image.Type.Sliced; tb.preserveAspect = false;
            Top(tb.rectTransform, 0, -508, 416, 128, new Vector2(0.5f, 1));
            var tIcon = GameArt.Load("Items/ArenaTicket");
            float tx0 = 20;
            if (tIcon != null)
            {
                var ti = UIFactory.AddImage("TicketIcon", tb.transform, Color.white, tIcon);
                ti.preserveAspect = true;
                Place(ti.rectTransform, 12, -10, 80, 80);
                tx0 = 100;
            }
            var tl = Text(tb.transform, "<color=#9AA3B5>도전권</color>", 22, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            Place(tl.rectTransform, tx0, -12, 160, 30);
            _tickets = Text(tb.transform, "", 44, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            Place(_tickets.rectTransform, tx0, -40, 160, 56);
            _ticketSub = Text(tb.transform, "", 17, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(_ticketSub.rectTransform, 20, -94, 380, 26);
            var buy = UIFactory.AddButton("BuyTicket", tb.transform, "+ 구매", 24, font, new Color(0.25f, 0.55f, 0.9f, 0.96f), Color.white);
            Place((RectTransform)buy.transform, 266, -24, 130, 60);
            buy.onClick.AddListener(OpenBuyTicket);

            var rankBtn = UIFactory.AddButton("Ranking", mt, "시즌 랭킹", 24, font, new Color(0.8f, 0.58f, 0.22f, 0.96f), Color.white);
            Place((RectTransform)rankBtn.transform, 22, -656, 202, 66);
            rankBtn.onClick.AddListener(OpenRanking);
            var tierBtn = UIFactory.AddButton("Tiers", mt, "티어 · 보상", 24, font, new Color(1, 1, 1, 0.16f), Color.white);
            Place((RectTransform)tierBtn.transform, 236, -656, 202, 66);
            tierBtn.onClick.AddListener(OpenTiers);

            var rule = Text(mt, $"<color=#9AA3B5>도전권은 매일 05:00에 {PlayerArena.MaxTickets}장으로 충전\n모자라면 크리스탈로 구매(하루 {PlayerArena.BuyCosts.Length}장)\n이기면 새 상대 · <color=#7CFF9A>● 플레이어</color>는 실제 지휘관의\n파티(AI 조종)</color>",
                18, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            rule.lineSpacing = 6f;
            UIFactory.Anchor(rule.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            rule.rectTransform.sizeDelta = new Vector2(420, 130); rule.rectTransform.anchoredPosition = new Vector2(0, 14);

            // ---- 오른쪽: 상대 선택 ----
            var head = Text(_frame, "<b>상대 선택</b>  <size=70%><color=#9AA3B5>쉬움 · 보통 · 어려움 — 어려울수록 점수 · 보상이 큼</color></size>", 30, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(head.rectTransform, RightX, -132, 900, 50);
            var refresh = UIFactory.AddButton("Refresh", _frame, "상대 새로고침", 22, font, new Color(1, 1, 1, 0.16f), Color.white);
            Place((RectTransform)refresh.transform, RightX + 3 * CardW + 2 * CardGap - 260, -128, 260, 56);
            _refreshLabel = refresh.GetComponentInChildren<TextMeshProUGUI>();
            _refreshLabel.richText = true;
            refresh.onClick.AddListener(OnRefreshClicked);

            _list = UIFactory.NewRect("List", _frame);
            Place(_list, RightX, -198, 3 * CardW + 2 * CardGap, CardH);

            _root.SetActive(false);
            PlayerArena.Instance.OnChanged += () => { if (_root != null && _root.activeSelf) Refresh(); };
            SubcultureRPG.Meta.Cloud.PublicProfile.Instance.OnChanged += () => { if (_root != null && _root.activeSelf) Refresh(); };
        }

        private float _arenaFetchAt = -999f;
        private bool _autoMatched;

        /// <summary>상대 새로고침: 무료(3분에 한 번) 또는 쿨타임 중이면 크리스탈 확인 창.</summary>
        private void OnRefreshClicked()
        {
            var a = PlayerArena.Instance;
            int left = a.RefreshSecondsLeft;
            if (left <= 0)
            {
                a.ManualRefresh(false);
                Toast("새 상대를 찾았습니다");
                return;
            }
            var wallet = FindAnyObjectByType<PlayerWallet>();
            int have = wallet != null ? wallet.Crystals : 0;
            Confirm($"무료 새로고침까지 <color=#FFD36B>{left / 60}:{left % 60:00}</color> 남았습니다.\n<color=#8FE9FF>크리스탈 {PlayerArena.RefreshCost}</color>개로 지금 새로고침할까요?\n" +
                    $"<size=80%><color=#9AA3B5>보유 크리스탈 {have:N0} · 이기면 상대가 무료로 새로 나옵니다</color></size>",
                "새로고침", () =>
                {
                    string err = a.ManualRefresh(true);
                    Toast(err ?? "새 상대를 찾았습니다");
                });
        }

        /// <summary>새로고침 버튼 글자(남은 쿨타임 · 크리스탈) — 1초마다.</summary>
        private void Update()
        {
            if (_refreshLabel == null || _root == null || !_root.activeSelf) return;
            if (Time.unscaledTime < _refreshTick) return;
            _refreshTick = Time.unscaledTime + 0.5f;
            int left = PlayerArena.Instance.RefreshSecondsLeft;
            _refreshLabel.text = left <= 0 ? "상대 새로고침"
                : $"새로고침 <size=80%><color=#8FE9FF>{PlayerArena.RefreshCost}</color> <color=#9AA3B5>{left / 60}:{left % 60:00}</color></size>";
        }

        /// <summary>크리스탈로 도전권 1장 구매 확인 창.</summary>
        private void OpenBuyTicket()
        {
            var a = PlayerArena.Instance;
            int cost = a.NextBuyCost, bought = a.BoughtToday, max = PlayerArena.BuyCosts.Length;
            if (cost < 0) { Toast($"오늘은 도전권을 모두 구매했습니다 ({max}/{max}, 매일 05:00 초기화)"); return; }
            var wallet = FindAnyObjectByType<PlayerWallet>();
            int have = wallet != null ? wallet.Crystals : 0;
            string next = bought + 1 < max ? $"다음 1장은 <color=#8FE9FF>{PlayerArena.BuyCosts[bought + 1]:N0}</color>" : "오늘 마지막 구매";
            Confirm($"<color=#8FE9FF>크리스탈 {cost:N0}</color>개로 결투장 도전권 1장을 구매할까요?\n" +
                    $"<size=80%><color=#9AA3B5>보유 크리스탈 <color={(have >= cost ? "#FFFFFF" : "#FF8A8A")}>{have:N0}</color>  ·  오늘 구매 {bought} / {max}  ·  {next}</color></size>",
                "구매", () =>
                {
                    string err = a.BuyTicket();
                    Toast(err ?? "도전권 1장을 구매했습니다");
                    Refresh();
                });
        }

        protected override void Refresh()
        {
            // 서버 순위(실제 플레이어)를 오래됐으면 다시 불러옴 — 끝나면 OnChanged로 다시 그림
            var pub = SubcultureRPG.Meta.Cloud.PublicProfile.Instance;
            if (SubcultureRPG.Meta.Cloud.PublicProfile.Online && !pub.ArenaFetching && Time.unscaledTime - _arenaFetchAt > 60f)
            {
                _arenaFetchAt = Time.unscaledTime;
                _ = pub.RefreshArenaAsync();
            }
            var a = PlayerArena.Instance;
            var roster = FindAnyObjectByType<PlayerRoster>();
            var db = roster != null ? roster.Database : null;
            // 서버에서 실제 플레이어를 처음 불러왔는데 상대가 모두 가상이면, 한 번 상대를 다시 뽑아 실제 플레이어를 섞음
            if (!_autoMatched && !pub.ArenaFetching && pub.ArenaPlayers.Count > 0 && !a.HasRealOpponent)
            {
                _autoMatched = true;
                a.Refresh();   // OnChanged → 다시 그림
                return;
            }
            int myRank = a.MyRank, total = a.RankTotal;
            var (tier, color) = PlayerArena.Tier(a.Points, myRank);
            var tierSprite = PlayerArena.Icon(a.Points, myRank);
            if (_tierIcon != null && tierSprite != null) _tierIcon.sprite = tierSprite;
            if (_tierGlow != null) _tierGlow.color = new Color(color.r, color.g, color.b, 0.45f);

            // 승급 축하(한 번)
            var promo = a.TakePromotion();
            if (promo.HasValue)
            {
                var (from, to, reward) = promo.Value;
                bool newTier = from < 0 || to / 3 > from / 3;
                Popups.Show(newTier ? "새로운 티어 달성!" : "승급!",
                    $"<size=120%><b>{PlayerArena.LevelName(to)}</b></size>" + (from >= 0 ? $"   <color=#9AA3B5>({PlayerArena.LevelName(from)}에서)</color>" : "") + "\n\n" +
                    $"<color=#FFD36B>승급 보상</color>  <color=#8FE9FF>{reward.Describe()}</color>\n<size=80%><color=#9AA3B5>보상은 이미 지급되었습니다</color></size>",
                    "확인", null, null, null, PlayerArena.LevelIconKey(to));
            }
            _rank.text = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{tier}</color>";
            _points.text = $"{a.Points:N0} <size=70%><color=#9AA3B5>점</color></size>";
            _rankLine.text = $"<color=#FFD36B>시즌 {myRank:N0}위</color>  <color=#9AA3B5>({PlayerArena.Percent(myRank, total)} · {total:N0}명)</color>";
            int games = a.Wins + a.Losses;
            _record.text = $"<color=#7CFF9A>{a.Wins}승</color> <color=#FF8A8A>{a.Losses}패</color>" + (games > 0 ? $"  <color=#9AA3B5>· 승률 {a.Wins * 100 / games}%</color>" : "");
            var se = PlayerArena.UntilSeasonEnd;
            _season.text = $"<color=#9AA3B5>시즌 종료까지</color> {(se.Days > 0 ? $"{se.Days}일 " : "")}{se.Hours}시간 {se.Minutes}분";

            // 지난 시즌 정산(한 번만)
            var st = a.TakeSettlement();
            if (st != null)
            {
                string frame = st.frameId != null ? $"\n<color=#FFD36B>한정 테두리 '{ProfileRewards.Frame(st.frameId).name}' 획득!</color>" : "";
                if (st.tierIndex >= PlayerArena.PointTiers)
                    frame += $"\n<color=#{ColorUtility.ToHtmlStringRGB(PlayerArena.Tiers[st.tierIndex].color)}>{PlayerArena.Tiers[st.tierIndex].name}</color> 달성 — 테두리 '{ProfileRewards.Frame(PlayerArena.Tiers[st.tierIndex].frameId).name}' 획득!";
                Notice("시즌 정산", $"지난 시즌 최종 순위  <color=#FFD36B><b>{st.rank:N0}위</b></color> / {st.total:N0}명 ({PlayerArena.Percent(st.rank, st.total)})  ({st.points:N0}점)\n" +
                                  $"<color=#8FE9FF>크리스탈 {st.crystals:N0}</color> 지급{frame}\n" +
                                  $"<size=80%><color=#9AA3B5>새 시즌 시작 점수 {st.newPoints:N0}점</color></size>");
            }
            long top4 = 0; int n = 0;
            foreach (var kv in CombatPower.All(roster, FindAnyObjectByType<PlayerEquipment>())) { if (n++ >= 4) break; top4 += kv.Value; }
            _myPower.text = $"<color=#9AA3B5>주력 4명 전투력</color>  <color=#FFB347><b>{top4:N0}</b></color>";
            _tickets.text = $"<color={(a.Tickets > 0 ? "#7CFF9A" : "#FF8A8A")}>{a.Tickets}</color> <size=60%><color=#9AA3B5>/ {PlayerArena.MaxTickets}</color></size>";
            int nextCost = a.NextBuyCost;
            _ticketSub.text = nextCost < 0 ? $"<color=#9AA3B5>오늘 구매 완료 ({PlayerArena.BuyCosts.Length}/{PlayerArena.BuyCosts.Length})</color>"
                : $"<color=#9AA3B5>오늘 구매 {a.BoughtToday}/{PlayerArena.BuyCosts.Length} · 다음</color> <color=#8FE9FF>{nextCost:N0}</color>";

            Clear(_list);
            int col = 0;
            foreach (var o in a.Opponents) { Card(o, db, col * (CardW + CardGap)); col++; }
        }

        private int _rankTab;   // 0 전체 · 1 친구

        /// <summary>시즌 랭킹 창: 탭(전체 · 친구) · 상위 100명 + 내 주변 · 아래에 내 순위 고정.</summary>
        private void OpenRanking()
        {
            var a = PlayerArena.Instance;
            var board = a.BuildBoard();
            var p = OpenDialog("시즌 랭킹", 1100, 960);
            var t = PlayerArena.UntilSeasonEnd;
            var sub = Text(p, $"<color=#9AA3B5>시즌 종료까지 {(t.Days > 0 ? $"{t.Days}일 " : "")}{t.Hours}시간 {t.Minutes}분 · 월요일 05:00 정산 · 참가 {board.total:N0}명</color>",
                20, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(sub.rectTransform, 30, -68, 640, 30);
            var pubp = SubcultureRPG.Meta.Cloud.PublicProfile.Instance;
            var srv = Text(p, !SubcultureRPG.Meta.Cloud.PublicProfile.Online ? "<color=#FFB347>게스트 — 서버 플레이어는 로그인하면 함께 보입니다</color>"
                : !string.IsNullOrEmpty(pubp.ArenaError) ? $"<color=#FF8A8A>{pubp.ArenaError}</color>"
                : $"<color=#7CFF9A>● 플레이어</color> <color=#9AA3B5>서버 플레이어 {pubp.ArenaPlayers.Count}명 + 다른 지휘관</color>", 18, Color.white, FontStyles.Normal, TextAlignmentOptions.Right);
            Place(srv.rectTransform, 680, -70, 390, 26);

            // 탭
            string[] tabs = { "전체", $"친구 ({board.friends.Count - 1}명)" };
            for (int i = 0; i < 2; i++)
            {
                int idx = i;
                var tb = UIFactory.AddButton("Tab", p, tabs[i], 21, font, i == _rankTab ? new Color(0.25f, 0.55f, 0.9f, 0.96f) : new Color(1, 1, 1, 0.12f), Color.white);
                Place((RectTransform)tb.transform, 30 + i * 200, -106, 190, 44);
                tb.onClick.AddListener(() => { if (_rankTab == idx) return; _rankTab = idx; CloseDialog(); OpenRanking(); });
            }

            var view = UIFactory.NewRect("View", p);
            UIFactory.Stretch(view, 24, 160, 24, 182);
            var content = UIFactory.NewRect("Content", view);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 4; vl.padding = new RectOffset(4, 4, 4, 4);
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandHeight = false;
            var scroll = UIFactory.MakeVerticalScroll(view, content);

            int mine = 0, count = 0;
            void Add(PlayerArena.RankEntry e)
            {
                if (e.me) mine = count;
                var row = RankRow(content, e);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
                count++;
            }
            if (_rankTab == 0)
            {
                foreach (var e in board.top) Add(e);
                if (board.around.Count > 0)
                {
                    var gap = Text(content, "<color=#6B7280>· · ·</color>", 24, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                    gap.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
                    count++;
                    foreach (var e in board.around) Add(e);
                }
            }
            else
            {
                foreach (var e in board.friends) Add(e);
                if (board.friends.Count <= 1)
                {
                    var none = Text(content, "<color=#9AA3B5>친구가 없거나 이번 시즌에 결투장을 한 친구가 없습니다.</color>", 20, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
                    none.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
                }
            }
            // 내 줄이 보이도록 스크롤
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = count > 1 ? Mathf.Clamp01(1f - (mine - 6) / (float)Mathf.Max(1, count - 14)) : 1f;

            // 아래에 내 순위 고정
            var pin = RankRow(p, board.me);
            UIFactory.Anchor(pin.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            pin.rectTransform.sizeDelta = new Vector2(-48, 56); pin.rectTransform.anchoredPosition = new Vector2(0, 112);
            var pct = Text(pin.transform, $"<color=#FFD36B>{PlayerArena.Percent(board.me.rank, board.total)}</color>", 19, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            Place(pct.rectTransform, 360, -8, 200, 40);

            var foot = Text(p, "<color=#FFD36B>정산 보상</color>  1위 1,500 · 2위 1,000 · 3위 700 · 10위 500 · 100위 300 · 상위 5% 200 · 10% 150 · 30% 120 · 그 밖 100 크리스탈\n" +
                               $"<color=#FFD36B>다이아 이상</color>이면서 {PlayerArena.NebulaRank}위 성운 · {PlayerArena.SupernovaRank}위 초신성 · {PlayerArena.AnchorRank}위 앵커(정산 때 테두리) · 1 · 2 · 3위 한정 테두리 · 정산 뒤 1,000점 초과분의 75%만 이어짐",
                17, new Color(0.82f, 0.85f, 0.92f), FontStyles.Normal, TextAlignmentOptions.Center);
            foot.textWrappingMode = TextWrappingModes.Normal;
            UIFactory.Anchor(foot.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            foot.rectTransform.sizeDelta = new Vector2(-60, 80); foot.rectTransform.anchoredPosition = new Vector2(0, 18);
        }

        /// <summary>랭킹 한 줄(순위 · 이름 · 티어 · 점수). 다른 지휘관 줄을 누르면 정보창.</summary>
        private Image RankRow(Transform parent, PlayerArena.RankEntry e)
        {
            var (tier, tc) = PlayerArena.Tier(e.points, e.rank);
            var row = Panel(parent, e.me ? new Color(0.25f, 0.45f, 0.75f, 0.7f) : e.rank <= 3 ? new Color(0.5f, 0.4f, 0.15f, 0.45f) : new Color(1, 1, 1, 0.05f));
            string rc = e.rank == 1 ? "#FFD36B" : e.rank == 2 ? "#DCE6FF" : e.rank == 3 ? "#E0A070" : "#FFFFFF";
            var r1 = Text(row.transform, $"<color={rc}><b>{e.rank:N0}</b></color>", e.rank >= 1000 ? 20 : 26, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            Place(r1.rectTransform, 6, -6, 100, 40);
            var r2 = Text(row.transform, $"<b>{e.name}</b>" + (e.me ? "  <size=75%><color=#9AD4FF>나</color></size>" : e.Real ? "  <size=70%><color=#7CFF9A>● 플레이어</color></size>" : ""), 24, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(r2.rectTransform, 120, -6, 460, 40);
            var ti = UIFactory.AddImage("TierIcon", row.transform, Color.white, PlayerArena.Icon(e.points, e.rank));
            ti.preserveAspect = true; ti.raycastTarget = false;
            Place(ti.rectTransform, 596, -2, 48, 48);
            var r3 = Text(row.transform, $"<color=#{ColorUtility.ToHtmlStringRGB(tc)}>{tier}</color>", 22, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            Place(r3.rectTransform, 650, -6, 170, 40);
            var r4 = Text(row.transform, $"{e.points:N0} 점", 24, Color.white, FontStyles.Normal, TextAlignmentOptions.Right);
            UIFactory.Anchor(r4.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            r4.rectTransform.sizeDelta = new Vector2(210, 40); r4.rectTransform.anchoredPosition = new Vector2(-20, -6);
            if (!e.me)
            {
                row.raycastTarget = true;
                var btn = row.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                row.gameObject.AddComponent<ButtonFx>();
                var entry = e;
                string line = $"<color=#FFD36B>결투장 {entry.rank:N0}위</color>  <color=#{ColorUtility.ToHtmlStringRGB(tc)}>{tier}</color>  {entry.points:N0}점";
                btn.onClick.AddListener(() =>
                {
                    if (entry.Real)
                    {
                        if (entry.snap != null) PlayerInfoWindow.Open(entry.snap, font, line);
                        else PlayerInfoWindow.OpenPlayer(entry.pid, font, line);
                        return;
                    }
                    var who = Commanders.FromSeed(entry.seed, FindAnyObjectByType<PlayerRoster>()?.Database);
                    // 결투장 점수에 맞춰 레벨 · 전투력을 보정(상위권일수록 강함)
                    who.level = Mathf.Clamp(20 + entry.points / 55, who.level, 80);
                    who.power = (long)(4 * (12000 + who.level * who.level * 60) * (0.95 + (entry.seed % 10) * 0.01));
                    PlayerInfoWindow.Open(who, font, line);
                });
            }
            return row;
        }

        /// <summary>티어 · 테두리 안내 창.</summary>
        private void OpenTiers()
        {
            var a = PlayerArena.Instance;
            var p = OpenDialog("티어 · 테두리 보상", 1400, 800);
            var note = Text(p, "<color=#9AA3B5>처음 그 티어에 오르면 테두리를 받습니다(프로필 → 테두리 변경). 시즌 1 · 2 · 3위는 한정 테두리.</color>", 20, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(note.rectTransform, 30, -68, 1200, 30);
            var who = ProfileAvatar.CharacterOf(PlayerProfile.Instance, FindAnyObjectByType<PlayerRoster>());
            int cur = PlayerArena.TierIndex(a.Points, a.MyRank);
            void Cell(int col, int rowIdx, string frameId, string title, string cond, Color color, bool got, bool now)
            {
                var cell = Panel(p, now ? new Color(0.25f, 0.45f, 0.75f, 0.6f) : new Color(1, 1, 1, 0.06f));
                Place(cell.rectTransform, 30 + col * 228, -112 - rowIdx * 330, 216, 316);
                var av = new ProfileAvatar(cell.transform, 150);
                UIFactory.Anchor(av.Rect, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
                av.Rect.anchoredPosition = new Vector2(0, -16);
                av.Set(who, string.IsNullOrEmpty(frameId) ? ProfileRewards.DefaultFrame : frameId);
                av.Rect.gameObject.AddComponent<CanvasGroup>().alpha = got ? 1f : 0.4f;
                var tx = Text(cell.transform, $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{title}</b></color>\n<size=72%>{cond}\n<color={(got ? "#7CFF9A" : "#9AA3B5")}>{(now ? "현재 티어" : got ? "획득" : "미획득")}</color></size>",
                    26, Color.white, FontStyles.Normal, TextAlignmentOptions.Bottom);
                UIFactory.Stretch(tx.rectTransform, 6, 12, 6, 0);
            }
            for (int i = 0; i < PlayerArena.Tiers.Length; i++)
            {
                var t = PlayerArena.Tiers[i];
                bool rankTier = i >= PlayerArena.PointTiers;
                int bit = i == PlayerArena.AnchorIndex ? 8 : i == PlayerArena.SupernovaIndex ? 16 : 32;
                bool got = rankTier ? (a.RankFrameBits & bit) != 0 : a.BestPoints >= t.min;
                string cond = i == 0 ? "시작 티어 (3 → 2 → 1단계)" : rankTier ? PlayerArena.RankTierCondition(i) : $"{t.min:N0}점 이상 (3 → 2 → 1단계)";
                Cell(i % 6, i / 6, t.frameId, t.name, cond, t.color, got, i == cur);
            }
            string[] rn = { "시즌 1위", "시즌 2위", "시즌 3위" };
            Color[] rcol = { new(1f, 0.83f, 0.42f), new(0.85f, 0.9f, 1f), new(0.85f, 0.6f, 0.4f) };
            for (int i = 0; i < 3; i++)
                Cell(3 + i, 1, PlayerArena.RankFrames[i], rn[i], "시즌 정산 한정", rcol[i], (a.RankFrameBits & (1 << i)) != 0, false);
        }

        /// <summary>상대 카드(세로): 대표 캐릭터 그림 · 난이도 띠 · 이름 · 티어 · 전투력 · 팀 4명 · 승리 보상 · 도전.</summary>
        private void Card(PlayerArena.Opponent o, GameDatabase db, float x)
        {
            var dc = PlayerArena.DifficultyColor(o.Diff);
            var edge = Panel(_list, new Color(dc.r, dc.g, dc.b, 0.85f));
            Place(edge.rectTransform, x, 0, CardW, CardH);
            var card = Panel(edge.transform, new Color(0.06f, 0.07f, 0.11f, 0.97f));
            UIFactory.Stretch(card.rectTransform, 3, 3, 3, 3);
            var ct = card.transform;

            // 위: 대표 캐릭터 그림(상대는 기본 의상, 실제 플레이어는 그 사람의 의상)
            var leader = PlayerArena.Instance.Leader(o, db);
            var art = UIFactory.AddImage("Art", ct, new Color(dc.r * 0.35f, dc.g * 0.35f, dc.b * 0.35f, 1f), ProceduralSprites.Rounded);
            art.type = Image.Type.Sliced; art.preserveAspect = false;
            Place(art.rectTransform, 6, -6, CardW - 18, 300);
            art.gameObject.AddComponent<RectMask2D>();
            var glow = UIFactory.AddImage("Glow", art.transform, new Color(dc.r, dc.g, dc.b, 0.5f), ProceduralSprites.Glow);
            Top(glow.rectTransform, 0, 20, 420, 360, new Vector2(0.5f, 1));
            if (leader != null)
            {
                string key = !string.IsNullOrEmpty(leader.id) ? leader.id : leader.name;
                string sfx = o.Real ? (o.avatarOutfit ?? "") : "";
                var sp = GameArt.Outfitted("Cutouts/", key, sfx) ?? GameArt.Outfitted("Cutouts/", key, "") ?? GameArt.Portrait(leader, "");
                if (sp != null)
                {
                    var im = UIFactory.AddImage("Leader", art.transform, Color.white, sp);
                    im.preserveAspect = true;
                    Top(im.rectTransform, 0, 6, 420, 640, new Vector2(0.5f, 1));
                }
            }
            var fade = UIFactory.AddImage("Fade", art.transform, new Color(0.06f, 0.07f, 0.11f, 0.85f));
            UIFactory.Anchor(fade.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            fade.rectTransform.sizeDelta = new Vector2(0, 54); fade.rectTransform.anchoredPosition = Vector2.zero;

            // 난이도 띠 · 실제 플레이어 표시
            var pill = UIFactory.AddImage("Diff", art.transform, dc, ProceduralSprites.Rounded);
            pill.type = Image.Type.Sliced; pill.preserveAspect = false;
            Place(pill.rectTransform, 12, -12, 120, 42);
            var pt = Text(pill.transform, $"<b>{PlayerArena.DifficultyName(o.Diff)}</b>", 22, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            UIFactory.Stretch(pt.rectTransform);
            TextOutline.Apply(pt, 0.2f, new Color32(8, 10, 18, 220));
            if (o.Real)
            {
                var real = Text(art.transform, "<color=#7CFF9A>● 플레이어</color>", 20, Color.white, FontStyles.Bold, TextAlignmentOptions.Right);
                UIFactory.Anchor(real.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
                real.rectTransform.sizeDelta = new Vector2(200, 30); real.rectTransform.anchoredPosition = new Vector2(-14, -18);
                TextOutline.Apply(real, 0.22f, new Color32(8, 10, 18, 230));
                // 그림을 누르면 그 플레이어 정보창
                var snap = o.Snap;
                art.raycastTarget = true;
                var ib = art.gameObject.AddComponent<Button>();
                ib.transition = Selectable.Transition.None;
                ib.onClick.AddListener(() => { if (snap != null) PlayerInfoWindow.Open(snap, font); });
            }

            // 이름 · 티어 · 레벨
            var name = Text(art.transform, $"<b>{o.name}</b>", 32, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            UIFactory.Anchor(name.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            name.rectTransform.sizeDelta = new Vector2(CardW - 50, 44); name.rectTransform.anchoredPosition = new Vector2(16, 6);
            TextOutline.Apply(name, 0.22f, new Color32(8, 10, 18, 240));

            var (tier, tc) = PlayerArena.Tier(o.points);
            var ti = UIFactory.AddImage("TierIcon", ct, Color.white, PlayerArena.Icon(o.points, 0));
            ti.preserveAspect = true;
            Place(ti.rectTransform, 16, -316, 52, 52);
            var info = Text(ct, $"<color=#{ColorUtility.ToHtmlStringRGB(tc)}><b>{tier}</b></color>  {o.points:N0}점\n<size=85%><color=#9AA3B5>평균 Lv.{o.level}" + (o.transcend > 0 ? $" · 초월 +{o.transcend}" : "") + "</color></size>",
                22, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Place(info.rectTransform, 78, -314, CardW - 100, 60);
            var pw = Text(ct, $"<color=#9AA3B5>전투력</color>  <color=#FFB347><b>{o.power:N0}</b></color>", 28, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(pw.rectTransform, 16, -380, CardW - 40, 40);

            // 팀 4명
            float tx = 16;
            for (int k = 0; k < o.team.Count && k < 4; k++)
            {
                var c = db != null ? db.GetCharacter(o.team[k]) : null;
                if (c == null) continue;
                bool per = o.Real && k < o.levels.Count && k < o.stars.Count;   // 실제 플레이어는 캐릭터마다 레벨 · 성급
                Thumb(ct, c, tx, -430, 94, per ? o.stars[k] : o.transcend > 0 ? 6 : (int)c.rarity, mine: false);
                var lv = Text(ct, $"Lv.{(per ? o.levels[k] : o.level)}", 16, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                Place(lv.rectTransform, tx, -526, 94, 22);
                tx += 102;
            }

            // 승리 보상(점수 · 크리스탈 · 금화)
            int d = (int)o.Diff;
            var rl = Text(ct, "<color=#9AA3B5>승리 보상</color>", 18, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            Place(rl.rectTransform, 16, -560, 200, 26);
            Chip(ct, 16, -590, "Items/ArenaCoin", $"+{PlayerArena.WinPoints[d]}점", "#7CFF9A");
            Chip(ct, 152, -590, "Items/Crystal", $"{PlayerArena.WinCrystals[d]}", "#8FE9FF");
            Chip(ct, 288, -590, "Items/Gold", $"{PlayerArena.WinGold[d]:N0}", "#FFD36B");

            var go = UIFactory.AddButton("Fight", ct, "도전", 30, font, new Color(0.85f, 0.4f, 0.3f, 0.96f), Color.white);
            var grt = (RectTransform)go.transform;
            UIFactory.Anchor(grt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            grt.sizeDelta = new Vector2(CardW - 40, 66); grt.anchoredPosition = new Vector2(0, 14);
            var opp = o;
            go.onClick.AddListener(() =>
            {
                if (PlayerArena.Instance.Tickets <= 0) { OpenBuyTicket(); return; }   // 없으면 바로 구매 창
                OnChallenge?.Invoke(opp);
            });
        }

        /// <summary>보상 칩(아이콘 + 수치).</summary>
        private void Chip(Transform parent, float x, float y, string icon, string value, string hex)
        {
            var bg = UIFactory.AddImage("Chip", parent, new Color(1, 1, 1, 0.07f), ProceduralSprites.Rounded);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            Place(bg.rectTransform, x, y, 128, 50);
            var sp = GameArt.Load(icon);
            if (sp != null)
            {
                var im = UIFactory.AddImage("Icon", bg.transform, Color.white, sp);
                im.preserveAspect = true;
                Place(im.rectTransform, 4, -5, 40, 40);
            }
            var t = Text(bg.transform, $"<color={hex}><b>{value}</b></color>", 21, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(t.rectTransform, 46, -8, 80, 34);
        }
    }

    /// <summary>
    /// 주간 보스 화면: 난이도 탭(쉬움 · 보통 · 어려움 · 극악) · 보스 그림 · 이번 주 약점 · 남은 시간 · 오늘 남은 도전 ·
    /// 고른 난이도의 누적/최고 점수 · 누적 보상(수령).
    /// '도전' → 편성(출격 모드) → 전투(아군 행동 20회 제한).
    /// </summary>
    public class RaidScreen : ModeScreenBase
    {
        public event System.Action OnChallenge;
        public event System.Action OnWorld;

        private TextMeshProUGUI _summary, _weak, _left, _rewardTitle;
        private RectTransform _rewards, _featured;
        private readonly List<(Image bg, TextMeshProUGUI label, NotifyBadge badge)> _tabs = new();
        private RaidTabs _raidTabs;

        private void Awake()
        {
            BuildBase("주간 보스", "Backgrounds/raid", 73);
            CurrencyBar.Create(_frame, font, null, new Vector2(-50, -32), Currency.Crystal, Currency.Gold);
            // 위쪽 가운데: [주간 보스] [월드 레이드] 큰 탭(제목 대신)
            _titleText.gameObject.SetActive(false);
            _raidTabs = RaidTabs.Build(_frame, font, true, () => OnWorld?.Invoke());

            // 왼쪽: 보스
            var boss = GameArt.Enemy(WorldRaid.BossName);
            var halo = UIFactory.AddImage("Halo", _frame, new Color(0.7f, 0.45f, 1f, 0.35f), ProceduralSprites.Glow);
            halo.preserveAspect = false;
            Top(halo.rectTransform, -480, -150, 860, 860, new Vector2(0.5f, 1));
            if (boss != null)
            {
                var img = UIFactory.AddImage("Boss", _frame, Color.white, boss);
                img.preserveAspect = true;
                Top(img.rectTransform, -480, -170, 760, 740, new Vector2(0.5f, 1));
            }
            var name = Text(_frame, $"<b>{WorldRaid.BossName}</b>", 44, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            Top(name.rectTransform, -480, -116, 700, 56, new Vector2(0.5f, 1));
            TextOutline.Apply(name, 0.2f, new Color32(8, 10, 18, 230));
            // 보스 아래: 약점 · 교체 · 격노 안내(어두운 판 위)
            var foot = Panel(_frame, new Color(0.05f, 0.05f, 0.09f, 0.78f));
            Top(foot.rectTransform, -480, -900, 820, 110, new Vector2(0.5f, 1));
            _weak = Text(foot.transform, "", 25, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            Place(_weak.rectTransform, 10, -10, 800, 40);
            _left = Text(foot.transform, "", 18, new Color(0.75f, 0.78f, 0.86f), FontStyles.Normal, TextAlignmentOptions.Center);
            _left.textWrappingMode = TextWrappingModes.Normal;
            Place(_left.rectTransform, 14, -52, 792, 52);

            // 오른쪽 맨 위: 난이도 탭
            for (int d = 0; d < WorldRaid.DifficultyCount; d++)
            {
                int dd = d;
                var df = WorldRaid.Diff(d);
                var tab = UIFactory.AddButton("Diff" + d, _frame, "", 22, font, new Color(1, 1, 1, 0.1f), Color.white);
                Top((RectTransform)tab.transform, 420 - 323 + d * 215, -122, 205, 58, new Vector2(0.5f, 1));
                var lbl = tab.GetComponentInChildren<TextMeshProUGUI>();
                lbl.richText = true;
                lbl.text = $"<color={df.hex}><b>{df.name}</b></color>  <size=75%><color=#C8CEDA>Lv.{df.level}</color></size>";
                var badge = NotifyBadge.Attach(tab, font, 4, 4, 26);
                tab.onClick.AddListener(() => { WorldRaid.Instance.Selected = dd; Refresh(); });
                _tabs.Add((tab.GetComponent<Image>(), lbl, badge));
            }

            // 오른쪽: 기록 · 도전
            var info = Panel(_frame, new Color(0.06f, 0.07f, 0.11f, 0.9f));
            Top(info.rectTransform, 420, -192, 860, 196, new Vector2(0.5f, 1));
            _summary = Text(info.transform, "", 24, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _summary.lineSpacing = 12f;
            UIFactory.Stretch(_summary.rectTransform, 30, 20, 330, 20);
            var go = UIFactory.AddButton("Fight", info.transform, "도전", 32, font, new Color(0.6f, 0.35f, 0.85f, 0.96f), Color.white);
            UIFactory.Anchor((RectTransform)go.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            ((RectTransform)go.transform).sizeDelta = new Vector2(270, 96); ((RectTransform)go.transform).anchoredPosition = new Vector2(-30, 0);
            go.onClick.AddListener(() =>
            {
                if (WorldRaid.Instance.AttemptsLeft <= 0) { Toast("오늘 도전 횟수를 모두 썼습니다 (매일 05:00 충전)"); return; }
                OnChallenge?.Invoke();
            });

            // 이번 주 보너스 캐릭터
            var fp = Panel(_frame, new Color(0.12f, 0.08f, 0.18f, 0.9f));
            Top(fp.rectTransform, 420, -400, 860, 156, new Vector2(0.5f, 1));
            var ft = Text(fp.transform, $"<b>이번 주 보너스 캐릭터</b>   <size=80%><color=#7CFF9A>파티에 1명당 점수 +{WorldRaid.FeaturedBonus * 100f:0}%  (최대 {WorldRaid.FeaturedMax}명, +{WorldRaid.FeaturedBonus * WorldRaid.FeaturedMax * 100f:0}%)</color></size>",
                22, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(ft.rectTransform, 24, -10, 820, 34);
            _featured = UIFactory.NewRect("Featured", fp.transform);
            Place(_featured, 24, -46, 820, 104);

            // 누적 보상
            _rewardTitle = Text(_frame, "", 26, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Top(_rewardTitle.rectTransform, 420, -568, 860, 40, new Vector2(0.5f, 1));
            var view = UIFactory.NewRect("RewardView", _frame);
            Top(view, 420, -612, 860, 392, new Vector2(0.5f, 1));
            _rewards = UIFactory.NewRect("Rewards", view);
            var vl = _rewards.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 8; vl.padding = new RectOffset(4, 4, 4, 4);
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandHeight = false;
            UIFactory.MakeVerticalScroll(view, _rewards);

            _root.SetActive(false);
            WorldRaid.Instance.OnChanged += () => { if (_root != null && _root.activeSelf) Refresh(); };
            ServerRaid.Instance.OnChanged += () => { if (_root != null && _root.activeSelf) _raidTabs.Refresh(); };
        }

        protected override void Refresh()
        {
            _raidTabs?.Refresh();
            var r = WorldRaid.Instance;
            int sel = r.Selected;
            var df = WorldRaid.Diff(sel);
            for (int d = 0; d < _tabs.Count; d++)
            {
                var dfd = WorldRaid.Diff(d);
                ColorUtility.TryParseHtmlString(dfd.hex, out var c);
                _tabs[d].bg.color = d == sel ? new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 0.95f) : new Color(1, 1, 1, 0.08f);
                _tabs[d].badge.Set(r.ClaimableIn(d) > 0);
            }
            var weak = WorldRaid.Weaknesses;
            var parts = new List<string>();
            foreach (var e in weak) parts.Add($"<color={Traits.Hex(e)}><b>{Traits.Name(e)}</b></color>");
            _weak.text = $"이번 주 약점  {string.Join("  ·  ", parts)}   <size=80%><color={df.hex}>{df.name}</color> <color=#9AA3B5>Lv.{df.level}</color></size>";
            var t = WorldRaid.UntilWeekReset;
            _left.text = $"교체까지 {(t.Days > 0 ? $"{t.Days}일 " : "")}{t.Hours}시간 {t.Minutes}분  ·  제한 시간 {WorldRaid.TimeLimit / 60f:0}분 · {WorldRaid.EnrageEvery:0}초마다 격노(공격력 +{df.enrageAtk * 100f:0}% · 즉시 '사건의 지평선' — 최대 체력 비례 피해)";
            _summary.text = $"<color=#9AA3B5>오늘 남은 도전</color>   <color={(r.AttemptsLeft > 0 ? "#7CFF9A" : "#FF8A8A")}><b>{r.AttemptsLeft}</b></color> / {WorldRaid.AttemptsPerDay}{(WorldRaid.FreeAttempts ? "  <color=#FFD36B>(최대 육성 테스트 중 · 소모 안 함)</color>" : "")}\n" +
                            $"<color=#9AA3B5>이번 주 누적 점수</color> <size=80%><color={df.hex}>{df.name}</color></size>   <color=#FFD36B><b>{r.TotalDamage(sel):N0}</b></color>\n" +
                            $"<color=#9AA3B5>최고 기록(1회)</color>   {r.BestDamage(sel):N0}";
            _rewardTitle.text = $"<b><color={df.hex}>{df.name}</color> 누적 점수 보상</b>  <size=75%><color=#9AA3B5>난이도마다 따로 · 월요일 05:00 초기화 · 점수 = 준 피해 × 보너스</color></size>";

            Clear(_featured);
            var roster = FindAnyObjectByType<PlayerRoster>();
            float fx = 0;
            foreach (var c in WorldRaid.Featured)
            {
                bool owned = roster != null && roster.IsOwned(c);
                var cell = Thumb(_featured, c, fx, 0, 86, roster != null && owned ? roster.StarOf(c) : (int)c.rarity, mine: false);
                if (!owned) cell.color = new Color(0.4f, 0.4f, 0.45f, 0.9f);
                var nm = Text(_featured, $"<b>{c.displayName}</b>\n<size=80%><color={Traits.Hex(c.element)}>{Traits.Name(c.element)}</color> · {(owned ? "<color=#7CFF9A>보유</color>" : "<color=#9AA3B5>미보유</color>")}</size>",
                    17, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                Place(nm.rectTransform, fx + 92, -16, 112, 60);
                fx += 208;
            }

            Clear(_rewards);
            for (int i = 0; i < df.milestones.Count; i++) RewardRow(sel, i);
        }

        private void RewardRow(int d, int i)
        {
            var r = WorldRaid.Instance;
            var m = WorldRaid.Diff(d).milestones[i];
            bool claimed = r.IsClaimed(d, i), can = r.CanClaim(d, i);
            var row = Panel(_rewards, claimed ? new Color(1, 1, 1, 0.04f) : can ? new Color(0.3f, 0.55f, 0.4f, 0.4f) : new Color(1, 1, 1, 0.07f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 74;
            var label = Text(row.transform, $"<color=#FFD36B><b>{m.damage:N0}</b></color>  <size=80%><color=#9AA3B5>점수</color></size>   {m.label}", 22,
                claimed ? new Color(0.55f, 0.58f, 0.64f) : Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            UIFactory.Stretch(label.rectTransform, 20, 26, 200, 6);
            // 진행 막대
            var bg = UIFactory.AddImage("Bar", row.transform, new Color(0, 0, 0, 0.4f));
            UIFactory.Anchor(bg.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0));
            bg.rectTransform.sizeDelta = new Vector2(-220, 8); bg.rectTransform.anchoredPosition = new Vector2(20, 12);
            var fill = UIFactory.AddImage("Fill", bg.transform, can || claimed ? new Color(0.45f, 0.95f, 0.55f) : new Color(1f, 0.8f, 0.4f));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(r.TotalDamage(d) / (float)m.damage), 1f);
            fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;

            var btn = UIFactory.AddButton("Claim", row.transform, claimed ? "수령 완료" : can ? "받기" : "진행 중", 20, font,
                can ? new Color(0.25f, 0.6f, 0.95f, 0.96f) : new Color(1, 1, 1, 0.1f), Color.white);
            UIFactory.Anchor((RectTransform)btn.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            ((RectTransform)btn.transform).sizeDelta = new Vector2(160, 52); ((RectTransform)btn.transform).anchoredPosition = new Vector2(-16, 0);
            btn.interactable = can;
            int idx = i;
            btn.onClick.AddListener(() => { if (WorldRaid.Instance.Claim(d, idx)) Toast($"{m.label} 획득!"); });
        }
    }
}
