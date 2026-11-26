using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 월드 레이드 화면(서버 공유 보스):
    /// 왼쪽 = 보스 · 공유 체력 막대(20%마다 보상 표시) · 약점, 오른쪽 = 내 기록 · 도전 · 공유 체력 보상(5단계) · 이번 주 순위(1~100위).
    /// 로그인하지 않았으면(게스트) 보기만 되고 도전 · 기록은 안 됨.
    /// </summary>
    public class ServerRaidScreen : ModeScreenBase
    {
        public event System.Action OnChallenge;
        public event System.Action OnWeekly;

        private TextMeshProUGUI _summary, _hpText, _weak, _left, _rankTitle, _status;
        private RectTransform _hpFill, _rewards, _ranking;

        private void Awake()
        {
            BuildBase("월드 레이드", "Backgrounds/worldraid", 73);
            CurrencyBar.Create(_frame, font, null, new Vector2(-50, -32), Currency.Crystal, Currency.Gold);

            // ---- 왼쪽: 보스 · 공유 체력 ----
            var halo = UIFactory.AddImage("Halo", _frame, new Color(1f, 0.45f, 0.3f, 0.32f), ProceduralSprites.Glow);
            halo.preserveAspect = false;
            Top(halo.rectTransform, -480, -150, 780, 700, new Vector2(0.5f, 1));
            var boss = GameArt.Enemy(ServerRaid.BossName);
            if (boss != null)
            {
                var img = UIFactory.AddImage("Boss", _frame, Color.white, boss);
                img.preserveAspect = true;
                Top(img.rectTransform, -480, -160, 620, 620, new Vector2(0.5f, 1));
            }
            var name = Text(_frame, $"<b>{ServerRaid.BossName}</b>  <size=60%><color=#FFB3A0>Lv.{ServerRaid.BossLevel}</color></size>", 44, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            Top(name.rectTransform, -480, -108, 760, 56, new Vector2(0.5f, 1));

            // 공유 체력 막대 + 20%마다 표시
            var barBg = UIFactory.AddImage("HpBg", _frame, new Color(0, 0, 0, 0.6f), ProceduralSprites.Rounded);
            barBg.type = Image.Type.Sliced; barBg.preserveAspect = false;
            Top(barBg.rectTransform, -480, -790, 780, 38, new Vector2(0.5f, 1));
            var fill = UIFactory.AddImage("HpFill", barBg.transform, new Color(0.9f, 0.25f, 0.25f, 1f), ProceduralSprites.Rounded);
            fill.type = Image.Type.Sliced; fill.preserveAspect = false;
            _hpFill = fill.rectTransform;
            _hpFill.anchorMin = Vector2.zero; _hpFill.anchorMax = Vector2.one;
            _hpFill.offsetMin = new Vector2(3, 3); _hpFill.offsetMax = new Vector2(-3, -3);
            for (int i = 1; i < ServerRaid.HpRewards.Length; i++)
            {
                var tick = UIFactory.AddImage("Tick", barBg.transform, new Color(1f, 0.85f, 0.45f, 0.9f));
                float x = 1f - i / (float)ServerRaid.HpRewards.Length;   // 체력은 오른쪽부터 줄어듦 → 20% 깎일 때마다 표시
                tick.rectTransform.anchorMin = new Vector2(x, 0); tick.rectTransform.anchorMax = new Vector2(x, 1);
                tick.rectTransform.sizeDelta = new Vector2(3, 6); tick.rectTransform.anchoredPosition = Vector2.zero;
            }
            _hpText = Text(barBg.transform, "", 20, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            UIFactory.Stretch(_hpText.rectTransform);
            TextOutline.Apply(_hpText, 0.2f, new Color32(0, 0, 0, 255));

            _weak = Text(_frame, "", 24, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            Top(_weak.rectTransform, -480, -838, 780, 38, new Vector2(0.5f, 1));
            _left = Text(_frame, "", 19, new Color(0.75f, 0.78f, 0.86f), FontStyles.Normal, TextAlignmentOptions.Center);
            _left.textWrappingMode = TextWrappingModes.Normal;
            Top(_left.rectTransform, -480, -878, 780, 56, new Vector2(0.5f, 1));

            var weekly = UIFactory.AddButton("Weekly", _frame, "주간 보스로 ▶", 22, font, new Color(0.45f, 0.3f, 0.7f, 0.95f), Color.white);
            Top((RectTransform)weekly.transform, -480, -946, 300, 56, new Vector2(0.5f, 1));
            weekly.onClick.AddListener(() => OnWeekly?.Invoke());

            // ---- 오른쪽: 내 기록 · 도전 ----
            var info = Panel(_frame, new Color(0.06f, 0.07f, 0.11f, 0.9f));
            Top(info.rectTransform, 420, -104, 860, 186, new Vector2(0.5f, 1));
            _summary = Text(info.transform, "", 23, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _summary.lineSpacing = 10f;
            UIFactory.Stretch(_summary.rectTransform, 28, 16, 310, 16);
            var go = UIFactory.AddButton("Fight", info.transform, "도전", 32, font, new Color(0.85f, 0.35f, 0.3f, 0.96f), Color.white);
            UIFactory.Anchor((RectTransform)go.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            ((RectTransform)go.transform).sizeDelta = new Vector2(270, 96); ((RectTransform)go.transform).anchoredPosition = new Vector2(-24, -18);
            go.onClick.AddListener(() =>
            {
                var r = ServerRaid.Instance;
                if (!ServerRaid.Online) { Toast("월드 레이드는 로그인해야 도전할 수 있습니다 (설정 → 로그인하기)"); return; }
                if (r.AttemptsLeft <= 0) { Toast("오늘 도전 횟수를 모두 썼습니다 (매일 05:00 충전)"); return; }
                OnChallenge?.Invoke();
            });
            var refresh = UIFactory.AddButton("Refresh", info.transform, "새로고침", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            UIFactory.Anchor((RectTransform)refresh.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
            ((RectTransform)refresh.transform).sizeDelta = new Vector2(270, 44); ((RectTransform)refresh.transform).anchoredPosition = new Vector2(-24, 14);
            refresh.onClick.AddListener(() => _ = ServerRaid.Instance.RefreshAsync());

            // ---- 공유 체력 보상(5단계) ----
            var rt = Text(_frame, "<b>공유 체력 보상</b>  <size=75%><color=#9AA3B5>서버 전체가 20% 깎을 때마다 · 참여하지 않아도 모두 받음</color></size>", 25, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Top(rt.rectTransform, 420, -300, 860, 38, new Vector2(0.5f, 1));
            _rewards = UIFactory.NewRect("Rewards", _frame);
            Top(_rewards, 420, -342, 860, 300, new Vector2(0.5f, 1));
            var vl = _rewards.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 6; vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandHeight = false;

            // ---- 순위 ----
            _rankTitle = Text(_frame, "", 25, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Top(_rankTitle.rectTransform, 420 - 110, -654, 640, 38, new Vector2(0.5f, 1));
            var tiers = UIFactory.AddButton("Tiers", _frame, "순위 보상 보기", 19, font, new Color(0.8f, 0.58f, 0.22f, 0.95f), Color.white);
            Top((RectTransform)tiers.transform, 420 + 320, -652, 210, 42, new Vector2(0.5f, 1));
            tiers.onClick.AddListener(ShowTiers);
            var view = UIFactory.NewRect("RankView", _frame);
            Top(view, 420, -698, 860, 290, new Vector2(0.5f, 1));
            _ranking = UIFactory.NewRect("Ranking", view);
            var rl = _ranking.gameObject.AddComponent<VerticalLayoutGroup>();
            rl.spacing = 4; rl.padding = new RectOffset(2, 2, 2, 2);
            rl.childControlWidth = true; rl.childControlHeight = true; rl.childForceExpandHeight = false;
            UIFactory.MakeVerticalScroll(view, _ranking);
            _status = Text(view, "", 22, new Color(0.75f, 0.78f, 0.86f), FontStyles.Normal, TextAlignmentOptions.Center);
            _status.textWrappingMode = TextWrappingModes.Normal;
            UIFactory.Stretch(_status.rectTransform, 20, 20, 20, 20);

            _root.SetActive(false);
            ServerRaid.Instance.OnChanged += () => { if (_root != null && _root.activeSelf) Refresh(); };
        }

        protected override void Refresh()
        {
            var r = ServerRaid.Instance;
            // 열 때마다(30초 넘게 지났으면) 서버에서 다시 불러옴
            if (ServerRaid.Online && !r.Fetching && (System.DateTime.Now - r.FetchedAt).TotalSeconds > 30) { _ = r.RefreshAsync(); }

            _hpFill.anchorMax = new Vector2(Mathf.Max(0.002f, r.HpRatio), 1f);
            _hpText.text = r.HpLeft > 0
                ? $"남은 체력  {r.HpLeft:N0} / {ServerRaid.MaxHp:N0}  ({r.HpRatio * 100f:0.0}%)"
                : $"<color=#7CFF9A>토벌 완료!</color>  준 피해 {r.ServerDamage:N0}";

            var parts = new List<string>();
            foreach (var e in ServerRaid.Weaknesses) parts.Add($"<color={Traits.Hex(e)}><b>{Traits.Name(e)}</b></color>");
            _weak.text = $"이번 주 약점  {string.Join("  ·  ", parts)}";
            var t = WorldRaid.UntilWeekReset;
            _left.text = $"초기화까지 {(t.Days > 0 ? $"{t.Days}일 " : "")}{t.Hours}시간 {t.Minutes}분 (월요일 05:00 — 그때 순위로 보상)  ·  제한 시간 {WorldRaid.TimeLimit / 60f:0}분 · 격노는 주간 보스 극악과 같음";

            string online = ServerRaid.Online ? "" : "\n<color=#FFB347>게스트(오프라인) — 로그인해야 도전 · 기록할 수 있습니다</color>";
            int myRank = r.MyRank;
            _summary.text = $"<color=#9AA3B5>오늘 남은 도전</color>   <color={(r.AttemptsLeft > 0 ? "#7CFF9A" : "#FF8A8A")}><b>{r.AttemptsLeft}</b></color> / {ServerRaid.AttemptsPerDay}" +
                            (WorldRaid.FreeAttempts ? "  <color=#FFD36B>(최대 육성 테스트 중 · 소모 안 함)</color>" : "") + "\n" +
                            $"<color=#9AA3B5>이번 주 내 피해</color>   <color=#FFD36B><b>{r.MyDamage:N0}</b></color>   <size=80%><color=#9AA3B5>최고 1회 {r.MyBest:N0}</color></size>\n" +
                            $"<color=#9AA3B5>내 순위</color>   {(myRank > 0 ? $"<b>{myRank}위</b> / {r.Ranking.Count}명" : "<color=#9AA3B5>기록 없음</color>")}" +
                            (r.LastRank > 0 ? $"   <size=80%><color=#9AA3B5>지난주 {r.LastRank}위</color></size>" : "") + online;

            Clear(_rewards);
            for (int i = 0; i < ServerRaid.HpRewards.Length; i++) RewardRow(i);

            _rankTitle.text = $"<b>이번 주 순위</b>  <size=75%><color=#9AA3B5>준 피해 순 · {r.Ranking.Count}명 참여" +
                              (r.FetchedAt > System.DateTime.MinValue ? $" · {r.FetchedAt:HH:mm} 기준" : "") + "</color></size>";
            Clear(_ranking);
            if (!ServerRaid.Online) _status.text = "로그인하면 서버 순위를 볼 수 있습니다.";
            else if (r.Fetching && r.Ranking.Count == 0) _status.text = "서버에서 불러오는 중...";
            else if (!string.IsNullOrEmpty(r.LastError)) _status.text = $"<color=#FF8A8A>{r.LastError}</color>";
            else if (r.Ranking.Count == 0) _status.text = "아직 이번 주 기록이 없습니다. 첫 번째로 도전해 보세요!";
            else _status.text = "";
            for (int i = 0; i < r.Ranking.Count && i < ServerRaid.RankLimit; i++) RankRow(i + 1, r.Ranking[i]);
        }

        private void RewardRow(int i)
        {
            var r = ServerRaid.Instance;
            bool claimed = r.IsClaimed(i), can = r.CanClaim(i);
            bool reached = r.ServerDamage >= ServerRaid.Threshold(i);
            var row = Panel(_rewards, claimed ? new Color(1, 1, 1, 0.04f) : can ? new Color(0.3f, 0.55f, 0.4f, 0.45f) : new Color(1, 1, 1, 0.07f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
            var label = Text(row.transform, $"<color=#FFD36B><b>{(i + 1) * 20}%</b></color>  <size=80%><color=#9AA3B5>({ServerRaid.Threshold(i):N0})</color></size>   {ServerRaid.HpRewards[i].label}",
                21, claimed ? new Color(0.55f, 0.58f, 0.64f) : Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            UIFactory.Stretch(label.rectTransform, 18, 4, 170, 4);
            var btn = UIFactory.AddButton("Claim", row.transform, claimed ? "수령 완료" : can ? "받기" : reached ? "받기" : "진행 중", 19, font,
                can ? new Color(0.25f, 0.6f, 0.95f, 0.96f) : new Color(1, 1, 1, 0.1f), Color.white);
            UIFactory.Anchor((RectTransform)btn.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            ((RectTransform)btn.transform).sizeDelta = new Vector2(150, 42); ((RectTransform)btn.transform).anchoredPosition = new Vector2(-12, 0);
            btn.interactable = can;
            int idx = i;
            btn.onClick.AddListener(() =>
            {
                var got = ServerRaid.Instance.Claim(idx);
                if (got != null) Popups.Reward($"월드 레이드 — 체력 {(idx + 1) * 20}% 보상", got);
            });
        }

        private void RankRow(int rank, ServerRaid.Entry e)
        {
            var row = Panel(_ranking, e.me ? new Color(0.45f, 0.35f, 0.15f, 0.75f) : new Color(1, 1, 1, rank % 2 == 0 ? 0.05f : 0.08f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            string rc = rank switch { 1 => "#FFD36B", 2 => "#DDE6FF", 3 => "#E0A070", _ => "#C8CEDA" };
            var tier = ServerRaid.TierOf(rank);
            var rt = Text(row.transform, $"<color={rc}><b>{rank}</b></color>", 24, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            Place(rt.rectTransform, 6, -4, 70, 36);
            var nm = Text(row.transform, $"{(e.me ? "<color=#FFD36B>" : "")}{e.name}{(e.me ? " (나)</color>" : "")}" +
                                         (rank <= 3 && tier?.title != null ? $"  <size=75%><color=#C9A0FF>{ProfileRewards.Title(tier.title).name}</color></size>" : ""),
                21, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(nm.rectTransform, 86, -4, 480, 36);
            var dm = Text(row.transform, $"{e.damage:N0}", 21, new Color(1f, 0.85f, 0.55f), FontStyles.Bold, TextAlignmentOptions.Right);
            UIFactory.Anchor(dm.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            dm.rectTransform.sizeDelta = new Vector2(260, 36); dm.rectTransform.anchoredPosition = new Vector2(-16, -4);
            // 누르면 그 지휘관 정보창(전투력 상위 5명 · 장비)
            row.raycastTarget = true;
            var btn = row.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            row.gameObject.AddComponent<ButtonFx>();
            string line = $"<color=#FFD36B>월드 레이드 {rank}위</color>  <color=#FFB347>{e.damage:N0}</color>";
            btn.onClick.AddListener(() =>
            {
                if (e.me) PlayerInfoWindow.Open(SubcultureRPG.Meta.Cloud.PublicProfile.BuildMine(), font, line);
                else if (e.Virtual) PlayerInfoWindow.Open(Commanders.FromSeed(e.seed, FindAnyObjectByType<PlayerRoster>()?.Database), font, line);
                else PlayerInfoWindow.OpenPlayer(e.id, font, line);
            });
        }

        private void ShowTiers()
        {
            var p = OpenDialog("월드 레이드 순위 보상", 1040, 720);
            var lines = new System.Text.StringBuilder();
            lines.Append("<color=#9AA3B5>매주 월요일 05:00에 지난주 순위(준 피해 순)로 정해지고, 다음에 월드 레이드를 열 때 우편함으로 보내 드립니다.</color>\n\n");
            foreach (var t in ServerRaid.RankTiers)
            {
                string range = t.from == t.to ? $"{t.from}위" : $"{t.from} ~ {t.to}위";
                string col = t.from switch { 1 => "#FFD36B", 2 => "#DDE6FF", 3 => "#E0A070", _ => "#C8CEDA" };
                lines.Append($"<color={col}><b>{range}</b></color>   {t.label}\n");
            }
            lines.Append("\n<size=85%><color=#9AA3B5>1 · 2 · 3위 칭호 · 테두리는 한 번 받으면 계속 쓸 수 있습니다(프로필 → 칭호 · 테두리).</color></size>");
            var body = Text(p, lines.ToString(), 24, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = 14f;
            UIFactory.Stretch(body.rectTransform, 40, 30, 40, 90);

            // 칭호 · 테두리 그림
            float x = 40;
            for (int i = 1; i <= 3; i++)
            {
                var fr = GameArt.Load($"Frames/fw_{i}");
                if (fr == null) continue;
                var img = UIFactory.AddImage("Frame", p, Color.white, fr);
                img.preserveAspect = true;
                UIFactory.Anchor(img.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
                img.rectTransform.sizeDelta = new Vector2(110, 110);
                img.rectTransform.anchoredPosition = new Vector2(-40 - (3 - i) * 124, 30);
                x += 124;
            }
        }
    }
}
