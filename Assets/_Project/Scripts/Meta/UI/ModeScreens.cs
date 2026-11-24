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
            if (_root != null) _root.SetActive(on);
            if (on) Refresh();
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
                es.AddComponent<StandaloneInputModule>();
            }

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.05f, 0.05f, 0.08f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            BackgroundArt.Create(_root.transform, 0.6f).Set(background);
            _frame = UIFactory.Frame(_root.transform);

            var t = Text(_frame, title, 40, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Top(t.rectTransform, 0, -30, 600, 56, new Vector2(0.5f, 1));

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

        protected Image Thumb(Transform parent, CharacterData c, float x, float y, float size, int star)
        {
            var col = StarText.ColorOf(star);
            var cell = UIFactory.AddImage("Cell", parent, new Color(col.r, col.g, col.b, 0.9f));
            Place(cell.rectTransform, x, y, size, size);
            var inner = UIFactory.AddImage("Inner", cell.transform, new Color(0.1f, 0.1f, 0.15f, 1f));
            UIFactory.Stretch(inner.rectTransform, 2, 2, 2, 2);
            var th = c != null ? GameArt.Thumb(c) : null;
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

        private TextMeshProUGUI _rank, _tickets, _myPower;
        private RectTransform _list;

        private void Awake()
        {
            BuildBase("결투장", "Backgrounds/arena", 73);
            CurrencyBar.Create(_frame, font, null, new Vector2(-50, -32), Currency.Crystal, Currency.Gold);

            var head = Panel(_frame, new Color(0.06f, 0.07f, 0.11f, 0.88f));
            Top(head.rectTransform, 0, -108, 1500, 120, new Vector2(0.5f, 1));
            var icon = GameArt.Load("Items/ArenaCoin");
            if (icon != null)
            {
                var im = UIFactory.AddImage("Icon", head.transform, Color.white, icon);
                im.preserveAspect = true;
                Place(im.rectTransform, 24, -12, 96, 96);
            }
            _rank = Text(head.transform, "", 34, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            Place(_rank.rectTransform, 140, -14, 760, 50);
            _myPower = Text(head.transform, "", 22, new Color(0.8f, 0.83f, 0.9f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(_myPower.rectTransform, 140, -66, 760, 36);
            _tickets = Text(head.transform, "", 26, Color.white, FontStyles.Bold, TextAlignmentOptions.Right);
            Place(_tickets.rectTransform, 900, -14, 380, 44);
            var refresh = UIFactory.AddButton("Refresh", head.transform, "상대 새로고침", 22, font, new Color(1, 1, 1, 0.16f), Color.white);
            Place((RectTransform)refresh.transform, 1300, -34, 176, 52);
            refresh.onClick.AddListener(() => { PlayerArena.Instance.Refresh(); Refresh(); Toast("새 상대를 찾았습니다"); });

            _list = UIFactory.NewRect("List", _frame);
            Top(_list, 0, -250, 1500, 700, new Vector2(0.5f, 1));

            var rule = Text(_frame, $"<color=#9AA3B5>도전권은 매일 05:00에 {PlayerArena.MaxTickets}장으로 충전 · 이기면 새 상대가 나타납니다 · 상대는 AI가 조종합니다</color>",
                20, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            UIFactory.Anchor(rule.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            rule.rectTransform.sizeDelta = new Vector2(1400, 30); rule.rectTransform.anchoredPosition = new Vector2(0, 70);

            _root.SetActive(false);
            PlayerArena.Instance.OnChanged += () => { if (_root != null && _root.activeSelf) Refresh(); };
        }

        protected override void Refresh()
        {
            var a = PlayerArena.Instance;
            var roster = FindAnyObjectByType<PlayerRoster>();
            var db = roster != null ? roster.Database : null;
            var (tier, color) = PlayerArena.Tier(a.Points);
            _rank.text = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{tier}</color>   {a.Points:N0} 점   <size=65%><color=#9AA3B5>{a.Wins}승 {a.Losses}패</color></size>";
            long top4 = 0; int n = 0;
            foreach (var kv in CombatPower.All(roster, FindAnyObjectByType<PlayerEquipment>())) { if (n++ >= 4) break; top4 += kv.Value; }
            _myPower.text = $"내 주력 4명 전투력  <color=#FFB347><b>{top4:N0}</b></color>";
            _tickets.text = $"도전권  <color={(a.Tickets > 0 ? "#7CFF9A" : "#FF8A8A")}>{a.Tickets}</color> / {PlayerArena.MaxTickets}";

            Clear(_list);
            float y = 0;
            foreach (var o in a.Opponents) { Row(o, db, y); y -= 226; }
        }

        private void Row(PlayerArena.Opponent o, GameDatabase db, float y)
        {
            var dc = PlayerArena.DifficultyColor(o.Diff);
            var row = Panel(_list, new Color(0.07f, 0.08f, 0.12f, 0.9f));
            Place(row.rectTransform, 0, y, 1500, 210);
            var bar = UIFactory.AddImage("Accent", row.transform, dc);
            Place(bar.rectTransform, 0, -14, 8, 182);

            // 상대 대표 · 이름 · 티어
            var leader = PlayerArena.Instance.Leader(o, db);
            var av = new ProfileAvatar(row.transform, 150);
            Place(av.Rect, 24, -30, 150, 150);
            av.Set(leader, o.Diff == PlayerArena.Difficulty.Hard ? "f_gold" : o.Diff == PlayerArena.Difficulty.Normal ? "f_silver" : "f_bronze");
            var (tier, tc) = PlayerArena.Tier(o.points);
            var name = Text(row.transform, $"<b>{o.name}</b>   <size=70%><color=#{ColorUtility.ToHtmlStringRGB(dc)}>{PlayerArena.DifficultyName(o.Diff)}</color></size>", 30, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(name.rectTransform, 196, -24, 420, 44);
            var info = Text(row.transform, $"<color=#{ColorUtility.ToHtmlStringRGB(tc)}>{tier}</color> {o.points:N0}점   ·   평균 Lv.{o.level}" + (o.transcend > 0 ? $"   ·   초월 +{o.transcend}" : "") +
                $"\n전투력  <color=#FFB347><b>{o.power:N0}</b></color>", 22, new Color(0.85f, 0.88f, 0.94f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
            info.lineSpacing = 10f;
            Place(info.rectTransform, 196, -76, 440, 90);

            // 팀
            float tx = 660;
            foreach (var id in o.team)
            {
                var c = db != null ? db.GetCharacter(id) : null;
                if (c == null) continue;
                Thumb(row.transform, c, tx, -38, 112, o.transcend > 0 ? 6 : (int)c.rarity);
                var lv = Text(row.transform, $"Lv.{o.level}", 16, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                Place(lv.rectTransform, tx, -154, 112, 24);
                tx += 124;
            }

            // 보상 · 도전
            int d = (int)o.Diff;
            var rew = Text(row.transform, $"<color=#9AA3B5>승리 보상</color>\n<color=#7CFF9A>+{PlayerArena.WinPoints[d]}점</color>  <color=#8FE9FF>크리스탈 {PlayerArena.WinCrystals[d]}</color>\n<color=#FFD36B>금화 {PlayerArena.WinGold[d]:N0}</color>",
                20, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            rew.lineSpacing = 6f;
            Place(rew.rectTransform, 1170, -24, 300, 96);
            var go = UIFactory.AddButton("Fight", row.transform, "도전", 28, font, new Color(0.85f, 0.4f, 0.3f, 0.96f), Color.white);
            Place((RectTransform)go.transform, 1170, -126, 300, 64);
            var opp = o;
            go.onClick.AddListener(() =>
            {
                if (PlayerArena.Instance.Tickets <= 0) { Toast("도전권이 없습니다 (매일 05:00 충전)"); return; }
                OnChallenge?.Invoke(opp);
            });
        }
    }

    /// <summary>
    /// 주간 보스(월드 레이드) 화면: 보스 그림 · 이번 주 약점 · 남은 시간 · 오늘 남은 도전 · 누적/최고 피해 · 누적 보상(수령).
    /// '도전' → 편성(출격 모드) → 전투(아군 행동 20회 제한).
    /// </summary>
    public class RaidScreen : ModeScreenBase
    {
        public event System.Action OnChallenge;

        private TextMeshProUGUI _summary, _weak, _left;
        private RectTransform _rewards;

        private void Awake()
        {
            BuildBase("주간 보스", "Backgrounds/raid", 73);
            CurrencyBar.Create(_frame, font, null, new Vector2(-50, -32), Currency.Crystal, Currency.Gold);

            // 왼쪽: 보스
            var boss = GameArt.Enemy(WorldRaid.BossName);
            var halo = UIFactory.AddImage("Halo", _frame, new Color(0.7f, 0.45f, 1f, 0.35f), ProceduralSprites.Glow);
            halo.preserveAspect = false;
            Top(halo.rectTransform, -480, -140, 820, 820, new Vector2(0.5f, 1));
            if (boss != null)
            {
                var img = UIFactory.AddImage("Boss", _frame, Color.white, boss);
                img.preserveAspect = true;
                Top(img.rectTransform, -480, -150, 700, 700, new Vector2(0.5f, 1));
            }
            var name = Text(_frame, $"<b>{WorldRaid.BossName}</b>", 44, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            Top(name.rectTransform, -480, -112, 700, 56, new Vector2(0.5f, 1));
            _weak = Text(_frame, "", 24, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            Top(_weak.rectTransform, -480, -846, 760, 40, new Vector2(0.5f, 1));
            _left = Text(_frame, "", 20, new Color(0.75f, 0.78f, 0.86f), FontStyles.Normal, TextAlignmentOptions.Center);
            Top(_left.rectTransform, -480, -888, 760, 30, new Vector2(0.5f, 1));

            // 오른쪽: 기록 · 도전
            var info = Panel(_frame, new Color(0.06f, 0.07f, 0.11f, 0.9f));
            Top(info.rectTransform, 420, -108, 860, 210, new Vector2(0.5f, 1));
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

            // 누적 보상
            var title = Text(_frame, "<b>이번 주 누적 피해 보상</b>  <size=75%><color=#9AA3B5>월요일 05:00 초기화</color></size>", 26, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Top(title.rectTransform, 420, -336, 860, 40, new Vector2(0.5f, 1));
            var view = UIFactory.NewRect("RewardView", _frame);
            Top(view, 420, -384, 860, 600, new Vector2(0.5f, 1));
            _rewards = UIFactory.NewRect("Rewards", view);
            var vl = _rewards.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 8; vl.padding = new RectOffset(4, 4, 4, 4);
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandHeight = false;
            UIFactory.MakeVerticalScroll(view, _rewards);

            _root.SetActive(false);
            WorldRaid.Instance.OnChanged += () => { if (_root != null && _root.activeSelf) Refresh(); };
        }

        protected override void Refresh()
        {
            var r = WorldRaid.Instance;
            var weak = WorldRaid.Weaknesses;
            var parts = new List<string>();
            foreach (var e in weak) parts.Add($"<color={Traits.Hex(e)}><b>{Traits.Name(e)}</b></color>");
            _weak.text = $"이번 주 약점  {string.Join("  ·  ", parts)}   <size=80%><color=#9AA3B5>Lv.{WorldRaid.BossLevel()}</color></size>";
            var t = WorldRaid.UntilWeekReset;
            _left.text = $"교체까지 {(t.Days > 0 ? $"{t.Days}일 " : "")}{t.Hours}시간 {t.Minutes}분  ·  한 번에 아군 행동 {WorldRaid.ActionLimit}회까지";
            _summary.text = $"<color=#9AA3B5>오늘 남은 도전</color>   <color={(r.AttemptsLeft > 0 ? "#7CFF9A" : "#FF8A8A")}><b>{r.AttemptsLeft}</b></color> / {WorldRaid.AttemptsPerDay}\n" +
                            $"<color=#9AA3B5>이번 주 누적 피해</color>   <color=#FFD36B><b>{r.TotalDamage:N0}</b></color>\n" +
                            $"<color=#9AA3B5>최고 기록(1회)</color>   {r.BestDamage:N0}";

            Clear(_rewards);
            for (int i = 0; i < WorldRaid.Milestones.Count; i++) RewardRow(i);
        }

        private void RewardRow(int i)
        {
            var r = WorldRaid.Instance;
            var m = WorldRaid.Milestones[i];
            bool claimed = r.IsClaimed(i), can = r.CanClaim(i);
            var row = Panel(_rewards, claimed ? new Color(1, 1, 1, 0.04f) : can ? new Color(0.3f, 0.55f, 0.4f, 0.4f) : new Color(1, 1, 1, 0.07f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 74;
            var label = Text(row.transform, $"<color=#FFD36B><b>{m.damage:N0}</b></color>  <size=80%><color=#9AA3B5>피해</color></size>   {m.label}", 22,
                claimed ? new Color(0.55f, 0.58f, 0.64f) : Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            UIFactory.Stretch(label.rectTransform, 20, 26, 200, 6);
            // 진행 막대
            var bg = UIFactory.AddImage("Bar", row.transform, new Color(0, 0, 0, 0.4f));
            UIFactory.Anchor(bg.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0));
            bg.rectTransform.sizeDelta = new Vector2(-220, 8); bg.rectTransform.anchoredPosition = new Vector2(20, 12);
            var fill = UIFactory.AddImage("Fill", bg.transform, can || claimed ? new Color(0.45f, 0.95f, 0.55f) : new Color(1f, 0.8f, 0.4f));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(r.TotalDamage / (float)m.damage), 1f);
            fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;

            var btn = UIFactory.AddButton("Claim", row.transform, claimed ? "수령 완료" : can ? "받기" : "진행 중", 20, font,
                can ? new Color(0.25f, 0.6f, 0.95f, 0.96f) : new Color(1, 1, 1, 0.1f), Color.white);
            UIFactory.Anchor((RectTransform)btn.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            ((RectTransform)btn.transform).sizeDelta = new Vector2(160, 52); ((RectTransform)btn.transform).anchoredPosition = new Vector2(-16, 0);
            btn.interactable = can;
            int idx = i;
            btn.onClick.AddListener(() => { if (WorldRaid.Instance.Claim(idx)) Toast($"{m.label} 획득!"); });
        }
    }
}
