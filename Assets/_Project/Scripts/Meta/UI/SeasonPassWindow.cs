using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Meta.Missions;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 시즌 패스 창: 위 = 시즌 · 남은 기간 · 단계 · 경험치 막대 · 프리미엄 열기, 가운데 = 30단계 가로 스크롤(무료 줄 · 프리미엄 줄),
    /// 아래 = 모두 받기. 칸을 누르면 그 보상을 받습니다. 홈 위쪽 '패스' 아이콘에서 엽니다. 데이터: SeasonPass.
    /// </summary>
    public static class SeasonPassWindow
    {
        private static readonly Color Gold = new(0.85f, 0.62f, 0.22f, 0.96f), Violet = new(0.55f, 0.4f, 0.85f, 0.96f);
        private const float ColW = 132f, LabelW = 170f;

        public static void Open(TMP_FontAsset font = null)
        {
            var pass = SeasonPass.Instance;
            var w = OverlayWindow.Open("시즌 패스", 1660, 920, font);
            var body = UIFactory.NewRect("Body", w.Panel);
            UIFactory.Stretch(body);
            float keepScroll = -1f;
            ScrollRect scroll = null;

            void Rebuild()
            {
                if (!w.IsOpen) return;
                if (scroll != null) keepScroll = scroll.horizontalNormalizedPosition;
                OverlayWindow.Clear(body);
                int lv = pass.Level;
                var (cur, need) = pass.Progress;

                // ---------------- 위: 시즌 · 단계 ----------------
                w.Text(body, $"<b>시즌 {pass.SeasonNumber}</b>   <size=80%><color=#9AA3B5>남은 기간</color> <color=#FFD36B>{Shop.PlayerShop.FormatSpan(SeasonPass.UntilEnd)}</color></size>",
                    26, Color.white, TextAlignmentOptions.Left, 36, -84, 900, 38);
                w.Text(body, $"<b><color=#FFD36B>Lv.{lv}</color></b> <size=60%><color=#9AA3B5>/ {SeasonPass.MaxLevel}</color></size>",
                    44, Color.white, TextAlignmentOptions.Left, 36, -130, 260, 60);
                w.Box(body, new Color(1, 1, 1, 0.1f), 250, -152, 620, 22);
                float fill = need > 0 ? cur / (float)need : 1f;
                if (fill > 0f) w.Box(body, Gold, 250, -152, Mathf.Max(22f, 620f * fill), 22);
                w.Text(body, need > 0 ? $"{cur} / {need}" : "최대 단계", 18, new Color(0.85f, 0.88f, 0.94f), TextAlignmentOptions.Right, 250, -176, 620, 26);
                w.Text(body, $"<color=#9AA3B5>일일 임무 받기 <color=#FFD36B>+{SeasonPass.DailyExp}</color> · 주간 임무 받기 <color=#FFD36B>+{SeasonPass.WeeklyExp}</color> (보너스 임무 제외)  ·  시즌이 끝나면 받지 않은 보상은 우편으로</color>",
                    17, Color.white, TextAlignmentOptions.Left, 36, -204, 1100, 28);

                // 프리미엄
                if (pass.Premium)
                    w.Text(body, "<color=#C9B6FF><b>프리미엄 패스 사용 중</b></color>", 24, Color.white, TextAlignmentOptions.Right, 1000, -98, 620, 40);
                else
                    w.Btn(body, $"프리미엄 패스 열기\n<size=65%>크리스탈 {SeasonPass.PremiumPrice}</size>", 24, Violet, 1300, -84, 324, 84, () =>
                    {
                        Popups.Show("프리미엄 패스", $"크리스탈 {SeasonPass.PremiumPrice}개로 이번 시즌 프리미엄 보상 줄을 엽니다.\n이미 지난 단계의 프리미엄 보상도 바로 받을 수 있습니다.",
                            "열기", () =>
                            {
                                string err = pass.BuyPremium();
                                if (err != null) Popups.Show("프리미엄 패스", err);
                                Rebuild();
                            }, "취소");
                    }).GetComponentInChildren<TextMeshProUGUI>().lineSpacing = -14f;

                // ---------------- 가운데: 단계 줄 ----------------
                const float top = -250, rowH = 238, headH = 44;
                w.Text(body, "<b>무료</b>", 26, new Color(0.85f, 0.88f, 0.94f), TextAlignmentOptions.Center, 36, top - headH - rowH / 2 + 20, LabelW - 20, 40);
                w.Text(body, "<b><color=#C9B6FF>프리미엄</color></b>", 26, Color.white, TextAlignmentOptions.Center, 36, top - headH - rowH - 8 - rowH / 2 + 20, LabelW - 20, 40);

                var view = UIFactory.NewRect("Track", body);
                OverlayWindow.Place(view, 36 + LabelW, top, 1660 - 72 - LabelW, headH + rowH * 2 + 8);
                view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.2f);
                view.gameObject.AddComponent<RectMask2D>();
                var content = UIFactory.NewRect("Content", view);
                content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 0.5f);
                content.sizeDelta = new Vector2(SeasonPass.MaxLevel * ColW + 16, 0); content.anchoredPosition = Vector2.zero;
                scroll = view.gameObject.AddComponent<ScrollRect>();
                scroll.content = content; scroll.viewport = view;
                scroll.horizontal = true; scroll.vertical = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 60f;

                for (int l = 1; l <= SeasonPass.MaxLevel; l++)
                {
                    float x = 8 + (l - 1) * ColW;
                    bool reached = l <= lv;
                    w.Text(content, $"<b>Lv.{l}</b>", 20, reached ? new Color(1f, 0.85f, 0.45f) : new Color(0.55f, 0.58f, 0.66f), TextAlignmentOptions.Center, x, -8, ColW - 10, 30);
                    Cell(content, x, -headH, l, false);
                    Cell(content, x, -headH - rowH - 8, l, true);
                }
                // 처음 열 때는 지금 단계가 보이게
                if (keepScroll >= 0f) scroll.horizontalNormalizedPosition = keepScroll;
                else
                {
                    float viewW = 1660 - 72 - LabelW, contentW = SeasonPass.MaxLevel * ColW + 16;
                    scroll.horizontalNormalizedPosition = Mathf.Clamp01(((lv - 2) * ColW) / Mathf.Max(1f, contentW - viewW));
                }

                // ---------------- 아래: 모두 받기 ----------------
                int n = pass.ClaimableCount();
                w.Btn(body, n > 0 ? $"모두 받기  <size=75%>({n})</size>" : "모두 받기", 26, Gold, 1660 - 36 - 300, -836, 300, 66, () =>
                {
                    var r = pass.ClaimAll();
                    Rebuild();
                    if (r != null) Popups.Reward("시즌 패스 보상", r);
                }, n > 0);
                if (!pass.Premium)
                    w.Text(body, "<color=#9AA3B5>프리미엄 줄은 '프리미엄 패스 열기' 후 받을 수 있습니다</color>", 18, Color.white, TextAlignmentOptions.Left, 36, -852, 900, 30);

                void Cell(RectTransform parent, float x, float y, int l, bool premium)
                {
                    bool claimed = pass.Claimed(l, premium);
                    bool can = pass.CanClaim(l, premium);
                    bool locked = l > lv || (premium && !pass.Premium);
                    var bg = w.Box(parent, can ? new Color(0.85f, 0.62f, 0.22f, 0.5f)
                        : premium ? new Color(0.55f, 0.4f, 0.85f, locked ? 0.12f : 0.22f) : new Color(1, 1, 1, locked ? 0.05f : 0.09f),
                        x, y, ColW - 10, rowH, raycast: can);
                    var r = premium ? SeasonPass.PremiumReward(l) : SeasonPass.FreeReward(l);
                    w.Icon(bg.transform, r.Icon(), (ColW - 10 - 76) / 2f, -18, 76);
                    var t = w.Text(bg.transform, r.Describe("\n"), 15, Color.white, TextAlignmentOptions.Center, 4, -102, ColW - 18, 92);
                    t.textWrappingMode = TextWrappingModes.Normal;
                    t.enableAutoSizing = true; t.fontSizeMin = 11; t.fontSizeMax = 15;
                    string state = claimed ? "<color=#7BE39A>받음</color>" : can ? "<color=#FFE08A><b>받기</b></color>" : locked ? "<color=#6B7280>잠김</color>" : "";
                    w.Text(bg.transform, state, 18, Color.white, TextAlignmentOptions.Center, 0, -200, ColW - 10, 30);
                    if (claimed || locked) w.Box(bg.transform, new Color(0, 0, 0, claimed ? 0.4f : 0.3f), 0, 0, ColW - 10, rowH);
                    if (can)
                    {
                        var b = bg.gameObject.AddComponent<Button>();
                        b.targetGraphic = bg;
                        int lvl = l; bool prem = premium;
                        b.onClick.AddListener(() =>
                        {
                            var got = pass.Claim(lvl, prem);
                            Rebuild();
                            if (got != null) Popups.Reward($"시즌 패스 Lv.{lvl} {(prem ? "프리미엄" : "무료")} 보상", got);
                        });
                        bg.gameObject.AddComponent<ButtonFx>();
                    }
                }
            }

            Rebuild();
        }
    }
}
