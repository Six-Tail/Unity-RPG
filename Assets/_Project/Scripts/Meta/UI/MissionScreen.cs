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
    /// 임무 화면(2026-11 개편 — 상점과 같은 틀).
    ///  · 왼쪽: 세로 탭(일일 임무 · 주간 임무 · 업적 — 아이콘 · 받을 보상 수 · 빨간 느낌표)
    ///  · 오른쪽 위 머리띠: 탭 이름 · 완료 진행 막대 · 초기화까지 남은 시간 · '모두 받기'
    ///  · 목록: 임무 카드(행동 아이콘 · 이름 · 진행 막대 · 보상 아이콘 칸 · 받기). 받을 것 → 진행 중 → 받은 것 순
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
        private TextMeshProUGUI _headTitle, _headSub, _headCount, _toast, _claimAllLabel;
        private RectTransform _tabs, _content, _headFill;
        private Image _headIcon;
        private Button _backBtn, _claimAllBtn;
        private Action _onBack;

        private MissionPeriod _tab = MissionPeriod.Daily;
        private bool _ach;                       // '업적' 탭
        private PlayerAchievements _achievements;
        private float _timerAcc;
        private string _periodSeen;
        private Coroutine _toastCo;
        private bool _dirty;
        private ScrollRect _scroll;
        private string _shownKey;

        private static readonly MissionPeriod[] Tabs = { MissionPeriod.Daily, MissionPeriod.Weekly };

        private static readonly Color Gold = new(0.85f, 0.65f, 0.25f, 0.98f), Blue = new(0.25f, 0.62f, 0.95f, 0.98f), Violet = new(0.58f, 0.42f, 0.9f, 0.98f);

        private void Awake()
        {
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            if (missions == null) missions = FindAnyObjectByType<PlayerMissions>();
            if (missions == null)
            {
                var host = wallet != null ? wallet.gameObject : gameObject;
                missions = host.AddComponent<PlayerMissions>();
            }
            _achievements = PlayerAchievements.Instance;
            BuildUI();
        }

        private void OnEnable()
        {
            if (missions != null) missions.OnChanged += MarkDirty;
            if (wallet != null) wallet.OnChanged += MarkDirty;
            if (_achievements != null) _achievements.OnChanged += MarkDirty;
        }

        private void OnDisable()
        {
            if (missions != null) missions.OnChanged -= MarkDirty;
            if (wallet != null) wallet.OnChanged -= MarkDirty;
            if (_achievements != null) _achievements.OnChanged -= MarkDirty;
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
            else UpdateHeader();
        }

        // ---------------- 갱신 ----------------

        private void Refresh()
        {
            _dirty = false;
            if (_root == null || missions == null) return;
            _periodSeen = Shop.PlayerShop.PeriodKey(Shop.ShopReset.Daily, DateTime.Now);

            RebuildTabs();
            Clear(_content);
            int claimable;
            if (_ach)
            {
                // 받을 수 있는 것 → 진행 중 → 다 받은 것 순(같으면 목록 순서)
                var all = PlayerAchievements.All;
                var order = new List<int>();
                for (int i = 0; i < all.Count; i++) order.Add(i);
                order.Sort((x, y) => { int c = AchOrder(all[x]).CompareTo(AchOrder(all[y])); return c != 0 ? c : x.CompareTo(y); });
                foreach (int i in order) BuildAchievementRow(all[i]);
                claimable = _achievements.ClaimableCount();
            }
            else
            {
                var list = MissionCatalog.Of(_tab);
                // 받을 수 있는 것 → 진행 중 → 받은 것 순
                list.Sort((a, b) => Order(a).CompareTo(Order(b)));
                foreach (var m in list) BuildRow(m);
                claimable = missions.ClaimableCount(_tab);
            }
            _claimAllBtn.interactable = claimable > 0;
            _claimAllBtn.image.color = claimable > 0 ? Gold : new Color(1, 1, 1, 0.12f);
            _claimAllLabel.text = claimable > 0 ? $"모두 받기  <size=75%>({claimable})</size>" : "모두 받기";
            UpdateHeader();

            // 탭을 바꾸면 맨 위부터
            string key = _ach ? "ach" : _tab.ToString();
            if (_scroll != null && key != _shownKey) { Canvas.ForceUpdateCanvases(); _scroll.verticalNormalizedPosition = 1f; }
            _shownKey = key;
        }

        private int Order(Mission m) => missions.CanClaim(m) ? 0 : missions.IsClaimed(m) ? 2 : 1;

        private int AchOrder(Achievement a) => _achievements.CanClaim(a) ? 0 : _achievements.IsComplete(a) ? 2 : 1;

        /// <summary>오른쪽 위 머리띠: 탭 이름 · 설명 · 완료 수와 막대 · 초기화까지.</summary>
        private void UpdateHeader()
        {
            int done, total;
            if (_ach)
            {
                done = _achievements.ClaimedTiers(); total = PlayerAchievements.TotalTiers();
                _headTitle.text = "업적";
                _headSub.text = "<color=#9AA3B5>계정 누적 기록 · 초기화 없음 · 마지막 단계는 칭호</color>";
                _headIcon.sprite = GameArt.Load(TabIcon(true, _tab));
            }
            else
            {
                var list = MissionCatalog.Of(_tab);
                total = list.Count; done = 0;
                foreach (var m in list) if (missions.IsClaimed(m)) done++;
                var left = PlayerMissions.UntilReset(_tab);
                string when = _tab == MissionPeriod.Daily ? "매일 05:00 초기화" : "매주 월요일 05:00 초기화";
                _headTitle.text = MissionCatalog.PeriodName(_tab);
                _headSub.text = $"<color=#9AA3B5>{when}</color>   <color=#FFD36B>초기화까지 {Shop.PlayerShop.FormatSpan(left)}</color>";
                _headIcon.sprite = GameArt.Load(TabIcon(false, _tab));
            }
            _headIcon.enabled = _headIcon.sprite != null;
            _headCount.text = $"<color=#9AA3B5>{(_ach ? "달성 단계" : "받은 보상")}</color>  <b>{done}</b> <color=#9AA3B5>/ {total}</color>";
            _headFill.anchorMax = new Vector2(total > 0 ? Mathf.Clamp01(done / (float)total) : 0f, 1f);
        }

        private static string TabIcon(bool ach, MissionPeriod p) => ach ? "Items/MissionTabAch" : p == MissionPeriod.Daily ? "Items/MissionTabDaily" : "Items/MissionTabWeekly";

        /// <summary>왼쪽 세로 탭(아이콘 · 이름 · 받을 수 · 느낌표).</summary>
        private void RebuildTabs()
        {
            Clear(_tabs);
            foreach (var t in Tabs)
            {
                var tab = t;
                int n = missions.ClaimableCount(t);
                int total = MissionCatalog.Of(t).Count, done = 0;
                foreach (var m in MissionCatalog.Of(t)) if (missions.IsClaimed(m)) done++;
                TabButton(TabIcon(false, t), MissionCatalog.PeriodName(t),
                    n > 0 ? $"<color=#FFD36B>받을 보상 {n}</color>" : $"<color=#9AA3B5>완료 {done}/{total}</color>",
                    !_ach && _tab == t, Gold, n > 0, () => { _tab = tab; _ach = false; Refresh(); });
            }
            int an = _achievements != null ? _achievements.ClaimableCount() : 0;
            TabButton(TabIcon(true, _tab), "업적",
                an > 0 ? $"<color=#FFD36B>받을 보상 {an}</color>" : $"<color=#9AA3B5>{_achievements.ClaimedTiers()}/{PlayerAchievements.TotalTiers()} 단계</color>",
                _ach, Violet, an > 0, () => { _ach = true; Refresh(); });
        }

        private void TabButton(string icon, string name, string sub, bool on, Color accent, bool badge, UnityEngine.Events.UnityAction onClick)
        {
            var bg = UIFactory.AddImage("Tab", _tabs, on ? accent : new Color(0.08f, 0.09f, 0.13f, 0.9f), ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight = 104;
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(onClick);
            bg.gameObject.AddComponent<ButtonFx>();
            var rt = bg.rectTransform;
            var sp = GameArt.Load(icon);
            if (sp != null)
            {
                var im = UIFactory.AddImage("Icon", rt, Color.white, sp);
                im.preserveAspect = true; im.raycastTarget = false;
                UIFactory.Anchor(im.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                im.rectTransform.sizeDelta = new Vector2(76, 76); im.rectTransform.anchoredPosition = new Vector2(12, 0);
            }
            var t = Label(rt, $"<b>{name}</b>\n<size=72%>{sub}</size>", 25, FontStyles.Normal, TextAlignmentOptions.Left);
            t.lineSpacing = -4f;
            t.enableAutoSizing = true; t.fontSizeMin = 14; t.fontSizeMax = 25;
            UIFactory.Stretch(t.rectTransform, 98, 8, 10, 8);
            if (badge) NotifyBadge.Attach(rt, font, 4, 4, 26).Set(true);
        }

        // ---------------- 카드 ----------------

        /// <summary>임무 행동별 아이콘(기존 그림 재사용).</summary>
        private static string GoalIcon(MissionGoal g) => g switch
        {
            MissionGoal.Login => "Items/UiAttendance",
            MissionGoal.BattleWin => "Items/UiSortie",
            MissionGoal.DungeonClear => "Items/TowerIcon",
            MissionGoal.GachaPull => "Items/UiSummon",
            MissionGoal.Growth => "Items/UiCharacter",
            MissionGoal.ShopBuy => "Items/UiShop",
            _ => "Items/UiMission",
        };

        private static (string icon, string count) RewardIcon(MissionReward r) => r.kind switch
        {
            MissionRewardKind.Crystal => ("Items/Crystal", r.amount.ToString("N0")),
            MissionRewardKind.Gold => ("Items/Gold", r.amount >= 10000 ? $"{r.amount / 1000}K" : r.amount.ToString("N0")),
            MissionRewardKind.Ticket => (r.ticket == SubcultureRPG.Gacha.GachaBannerType.Pickup ? "Items/PickupTicket" : "Items/StandardTicket", "×" + r.amount),
            _ => ("Items/" + r.material, "×" + r.amount),
        };

        /// <summary>카드 바탕: 받을 수 있으면 금색 테두리 · 받은 것은 흐리게.</summary>
        private RectTransform Card(string name, float height, bool can, bool faded, Color accent)
        {
            var edge = UIFactory.AddImage(name, _content, can ? accent : new Color(1, 1, 1, 0.08f), ProceduralSprites.Rounded);
            edge.type = Image.Type.Sliced; edge.preserveAspect = false;
            edge.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            if (faded) edge.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;
            var inner = UIFactory.AddImage("Inner", edge.transform, can ? new Color(0.12f, 0.13f, 0.2f, 0.97f) : new Color(0.07f, 0.08f, 0.12f, 0.94f), ProceduralSprites.Rounded);
            inner.type = Image.Type.Sliced; inner.preserveAspect = false;
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);
            return inner.rectTransform;
        }

        private void IconTile(RectTransform card, string icon, float size, Color tint)
        {
            var tile = UIFactory.AddImage("Goal", card, new Color(tint.r, tint.g, tint.b, 0.18f), ProceduralSprites.Rounded);
            tile.type = Image.Type.Sliced; tile.preserveAspect = false;
            UIFactory.Anchor(tile.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            tile.rectTransform.sizeDelta = new Vector2(size, size); tile.rectTransform.anchoredPosition = new Vector2(16, 0);
            var sp = GameArt.Load(icon);
            if (sp == null) return;
            var im = UIFactory.AddImage("Icon", tile.rectTransform, Color.white, sp);
            im.preserveAspect = true;
            UIFactory.Stretch(im.rectTransform, 8, 8, 8, 8);
        }

        private void ProgressBar(RectTransform card, float x, float y, float w, float ratio, Color fillColor, string text)
        {
            var bg = UIFactory.AddImage("BarBg", card, new Color(1, 1, 1, 0.10f), ProceduralSprites.Rounded);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false; bg.pixelsPerUnitMultiplier = 4f;
            UIFactory.Anchor(bg.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            bg.rectTransform.sizeDelta = new Vector2(w, 14); bg.rectTransform.anchoredPosition = new Vector2(x, y);
            if (ratio > 0f)
            {
                var fill = UIFactory.AddImage("Fill", bg.rectTransform, ratio >= 1f ? new Color(0.45f, 0.85f, 0.55f) : fillColor, ProceduralSprites.Rounded);
                fill.type = Image.Type.Sliced; fill.preserveAspect = false; fill.pixelsPerUnitMultiplier = 4f;
                var frt = fill.rectTransform;
                frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1); frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            }
            var t = Label(card, text, 19, FontStyles.Bold, TextAlignmentOptions.Left);
            UIFactory.Anchor(t.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            t.rectTransform.sizeDelta = new Vector2(220, 26); t.rectTransform.anchoredPosition = new Vector2(x + w + 14, y - 6);
            t.color = ratio >= 1f ? new Color(0.55f, 0.95f, 0.65f) : new Color(0.8f, 0.83f, 0.9f);
        }

        /// <summary>보상 아이콘 칸 줄(오른쪽 버튼 왼쪽에 오른쪽 정렬).</summary>
        private void RewardTiles(RectTransform card, List<(string icon, string count, string hex)> parts, float rightEdge)
        {
            const float size = 78f, gap = 8f;
            float x = rightEdge;
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var (icon, count, hex) = parts[i];
                var tile = UIFactory.AddImage("Reward", card, new Color(1, 1, 1, 0.07f), ProceduralSprites.Rounded);
                tile.type = Image.Type.Sliced; tile.preserveAspect = false;
                UIFactory.Anchor(tile.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
                tile.rectTransform.sizeDelta = new Vector2(size, size); tile.rectTransform.anchoredPosition = new Vector2(-x, 0);
                var sp = GameArt.Load(icon);
                if (sp != null)
                {
                    var im = UIFactory.AddImage("Icon", tile.rectTransform, Color.white, sp);
                    im.preserveAspect = true;
                    UIFactory.Stretch(im.rectTransform, 10, 18, 10, 4);
                }
                var c = Label(tile.rectTransform, count, 17, FontStyles.Bold, TextAlignmentOptions.Bottom);
                UIFactory.Stretch(c.rectTransform, 2, 2, 2, 0);
                c.color = ColorUtility.TryParseHtmlString(hex, out var col) ? col : Color.white;
                TextOutline.Apply(c, 0.22f, new Color32(8, 10, 18, 240));
                x += size + gap;
            }
        }

        private Button ClaimButton(RectTransform card, string label, bool can, Color color)
        {
            var btn = UIFactory.AddButton("Claim", card, label, 24, font, can ? color : new Color(1, 1, 1, 0.12f), Color.white);
            var brt = (RectTransform)btn.transform;
            UIFactory.Anchor(brt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            brt.sizeDelta = new Vector2(170, 66); brt.anchoredPosition = new Vector2(-22, 0);
            btn.interactable = can;
            btn.GetComponentInChildren<TextMeshProUGUI>().richText = true;
            return btn;
        }

        /// <summary>업적 카드: 트로피 · 이름 · 단계 점 · 다음 목표 · 진행 막대 · 보상 칸 · 받기.</summary>
        private void BuildAchievementRow(Achievement a)
        {
            int reached = _achievements.Reached(a), claimed = _achievements.Claimed(a);
            bool can = claimed < reached, done = claimed >= a.Tiers;
            int next = Mathf.Min(claimed, a.Tiers - 1);   // 지금 보여 줄 단계(받을 단계 또는 다음 목표)
            long cur = _achievements.Value(a.stat);
            int target = a.targets[next];

            var card = Card("Achievement", 124, can, done, Violet);
            IconTile(card, "Items/MissionTabAch", 92, new Color(0.75f, 0.6f, 1f));

            // 이름 + 단계(점)
            var pips = new System.Text.StringBuilder();
            for (int i = 0; i < a.Tiers; i++) pips.Append(i < claimed ? "<color=#FFD36B>●</color>" : "<color=#4B5060>●</color>");
            var t = Label(card, $"<b>{a.name}</b>   <size=70%>{pips}  <color=#9AA3B5>{claimed}/{a.Tiers}</color></size>", 25, FontStyles.Normal, TextAlignmentOptions.Left);
            UIFactory.Anchor(t.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            t.rectTransform.sizeDelta = new Vector2(760, 36); t.rectTransform.anchoredPosition = new Vector2(124, -12);
            var goal = Label(card, done ? "<color=#9AA3B5>모든 단계 완료</color>" : $"<color=#C8CEDC>{next + 1}단계 · {a.Goal(next)}</color>", 19, FontStyles.Normal, TextAlignmentOptions.Left);
            UIFactory.Anchor(goal.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            goal.rectTransform.sizeDelta = new Vector2(760, 28); goal.rectTransform.anchoredPosition = new Vector2(124, -48);
            float ratio = target > 0 ? Mathf.Clamp01((float)cur / target) : 1f;
            ProgressBar(card, 124, 20, 480, done ? 1f : ratio, new Color(0.62f, 0.45f, 0.95f), done ? "완료" : $"{Mathf.Min(cur, target):N0} / {target:N0}");

            // 보상(받을 단계들 또는 다음 단계) + 마지막 단계 칭호
            if (!done)
            {
                var r = new RewardBundle();
                for (int i = claimed; i < Mathf.Max(claimed + 1, reached); i++)
                {
                    var p = PlayerAchievements.RewardOf(a, i);
                    r.crystals += p.crystals; r.standard += p.standard;
                }
                var parts = new List<(string, string, string)>();
                foreach (var (icon, _, count, hex) in r.Parts()) parts.Add((icon, count >= 1000 ? count.ToString("N0") : "×" + count, hex));
                RewardTiles(card, parts, 210);
                if (a.titleId != null && Mathf.Max(claimed + 1, reached) >= a.Tiers)
                {
                    var title = Label(card, $"<size=80%>칭호</size> <color=#FFD36B>「{ProfileRewards.Title(a.titleId).name}」</color>", 18, FontStyles.Bold, TextAlignmentOptions.Right);
                    UIFactory.Anchor(title.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
                    title.rectTransform.sizeDelta = new Vector2(360, 24); title.rectTransform.anchoredPosition = new Vector2(-210, 4);
                }
            }

            string label = done ? "완료" : can ? (reached - claimed > 1 ? $"받기 <size=75%>×{reached - claimed}</size>" : "받기") : "진행 중";
            var btn = ClaimButton(card, label, can, Violet);
            var ach = a;
            btn.onClick.AddListener(() =>
            {
                var got = _achievements.ClaimOne(ach);
                if (got == null) return;
                bool titled = ach.titleId != null && _achievements.IsComplete(ach);
                Toast($"업적 보상!  {got.Describe("  ·  ")}" + (titled ? $"   칭호 「{ProfileRewards.Title(ach.titleId).name}」 해금" : ""));
                Refresh();
            });
        }

        /// <summary>임무 카드: 행동 아이콘 · 이름 · 진행 막대 · 보상 칸 · 받기.</summary>
        private void BuildRow(Mission m)
        {
            int cur = missions.Progress(m);
            bool claimed = missions.IsClaimed(m);
            bool can = missions.CanClaim(m);
            var accent = m.IsBonus ? Gold : Blue;

            var card = Card("Mission", 116, can, claimed, accent);
            IconTile(card, GoalIcon(m.goal), 88, m.IsBonus ? new Color(1f, 0.83f, 0.42f) : new Color(0.45f, 0.7f, 1f));

            string title = m.IsBonus ? $"<color=#FFD36B>보너스</color>  <b>{m.title}</b>" : $"<b>{m.title}</b>";
            var t = Label(card, title, 25, FontStyles.Normal, TextAlignmentOptions.Left);
            UIFactory.Anchor(t.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            t.rectTransform.sizeDelta = new Vector2(760, 36); t.rectTransform.anchoredPosition = new Vector2(120, -18);
            t.enableAutoSizing = true; t.fontSizeMin = 16; t.fontSizeMax = 25;

            float ratio = m.target > 0 ? Mathf.Clamp01((float)cur / m.target) : 1f;
            ProgressBar(card, 120, 24, 480, ratio, new Color(0.35f, 0.65f, 1f), $"{Mathf.Min(cur, m.target):N0} / {m.target:N0}");

            var parts = new List<(string, string, string)>();
            foreach (var r in m.rewards)
            {
                var (icon, count) = RewardIcon(r);
                string hex = r.kind switch { MissionRewardKind.Crystal => "#8FE9FF", MissionRewardKind.Gold => "#FFD36B", MissionRewardKind.Ticket => "#FFB3E6", _ => Grades.Hex(Materials.Grade(r.material)) };
                parts.Add((icon, count, hex));
            }
            RewardTiles(card, parts, 210);

            var btn = ClaimButton(card, claimed ? "받음" : can ? "받기" : "진행 중", can, accent);
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
            if (_ach)
            {
                var r = _achievements != null ? _achievements.ClaimAll() : null;
                if (r == null) return;
                Toast($"업적 보상!  {r.Describe("  ·  ")}");
                Refresh();
                return;
            }
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
            BackgroundArt.Create(_root.transform, 0.7f).Set("Backgrounds/mission", "Backgrounds/party");
            var root = UIFactory.Frame(_root.transform);

            var title = UIFactory.AddText("Title", root, "임무", 40, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(400, 56), new Vector2(200, -24), new Vector2(0, 1));

            CurrencyBar.Create(root, font, wallet, new Vector2(-50, -32), Currency.Crystal, Currency.Gold);
            CurrencyBar.Create(root, font, wallet, new Vector2(-50, -94), Currency.PickupTicket, Currency.StandardTicket);   // 임무 보상에 뽑기권이 있어 함께

            // 왼쪽: 세로 탭
            _tabs = UIFactory.NewRect("Tabs", root);
            Place(_tabs, new Vector2(0, 1), new Vector2(300, 600), new Vector2(40, -170), new Vector2(0, 1));
            var tv = _tabs.gameObject.AddComponent<VerticalLayoutGroup>();
            tv.spacing = 12; tv.childAlignment = TextAnchor.UpperLeft;
            tv.childControlWidth = true; tv.childControlHeight = true;
            tv.childForceExpandWidth = true; tv.childForceExpandHeight = false;

            // 오른쪽 위: 머리띠(탭 이름 · 진행 · 초기화 · 모두 받기)
            var head = UIFactory.AddImage("Header", root, new Color(0.07f, 0.08f, 0.12f, 0.92f), ProceduralSprites.Rounded);
            head.type = Image.Type.Sliced; head.preserveAspect = false;
            Place(head.rectTransform, new Vector2(0, 1), new Vector2(1500, 118), new Vector2(370, -170), new Vector2(0, 1));
            var hrt = head.rectTransform;
            _headIcon = UIFactory.AddImage("Icon", hrt, Color.white);
            _headIcon.preserveAspect = true;
            UIFactory.Anchor(_headIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            _headIcon.rectTransform.sizeDelta = new Vector2(92, 92); _headIcon.rectTransform.anchoredPosition = new Vector2(16, 0);
            _headTitle = Label(hrt, "", 34, FontStyles.Bold, TextAlignmentOptions.Left);
            UIFactory.Anchor(_headTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            _headTitle.rectTransform.sizeDelta = new Vector2(400, 46); _headTitle.rectTransform.anchoredPosition = new Vector2(124, -12);
            _headSub = Label(hrt, "", 20, FontStyles.Normal, TextAlignmentOptions.Left);
            UIFactory.Anchor(_headSub.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            _headSub.rectTransform.sizeDelta = new Vector2(700, 30); _headSub.rectTransform.anchoredPosition = new Vector2(126, 14);
            _headCount = Label(hrt, "", 22, FontStyles.Normal, TextAlignmentOptions.Right);
            UIFactory.Anchor(_headCount.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            _headCount.rectTransform.sizeDelta = new Vector2(380, 32); _headCount.rectTransform.anchoredPosition = new Vector2(-370, -22);
            var barBg = UIFactory.AddImage("BarBg", hrt, new Color(1, 1, 1, 0.12f), ProceduralSprites.Rounded);
            barBg.type = Image.Type.Sliced; barBg.preserveAspect = false; barBg.pixelsPerUnitMultiplier = 4f;
            UIFactory.Anchor(barBg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
            barBg.rectTransform.sizeDelta = new Vector2(380, 14); barBg.rectTransform.anchoredPosition = new Vector2(-370, 30);
            var fill = UIFactory.AddImage("Fill", barBg.rectTransform, new Color(1f, 0.83f, 0.42f), ProceduralSprites.Rounded);
            fill.type = Image.Type.Sliced; fill.preserveAspect = false; fill.pixelsPerUnitMultiplier = 4f;
            _headFill = fill.rectTransform;
            _headFill.anchorMin = Vector2.zero; _headFill.anchorMax = new Vector2(0, 1); _headFill.offsetMin = Vector2.zero; _headFill.offsetMax = Vector2.zero;

            _claimAllBtn = UIFactory.AddButton("ClaimAll", hrt, "모두 받기", 26, font, Gold, Color.white);
            var crt = (RectTransform)_claimAllBtn.transform;
            UIFactory.Anchor(crt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            crt.sizeDelta = new Vector2(300, 76); crt.anchoredPosition = new Vector2(-24, 0);
            _claimAllLabel = _claimAllBtn.GetComponentInChildren<TextMeshProUGUI>();
            _claimAllLabel.richText = true;
            _claimAllBtn.onClick.AddListener(OnClaimAll);

            // 목록(스크롤)
            var view = UIFactory.NewRect("View", root);
            Place(view, new Vector2(0, 1), new Vector2(1500, 760), new Vector2(370, -300), new Vector2(0, 1));
            _content = UIFactory.NewRect("Content", view);
            var vl = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 10; vl.padding = new RectOffset(4, 4, 4, 12);
            vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            _scroll = UIFactory.MakeVerticalScroll(view, _content);

            // 아래: 알림
            _toast = UIFactory.AddText("Toast", root, "", 24, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_toast.rectTransform, new Vector2(0.5f, 0), new Vector2(1200, 40), new Vector2(180, 16), new Vector2(0.5f, 0));
            _toast.richText = true;
            _toast.alpha = 0f;
            TextOutline.Apply(_toast, 0.22f, new Color32(8, 10, 18, 240));

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
            SubcultureRPG.UI.UIInput.AddModule(es);
        }
    }
}
