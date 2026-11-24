using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.Characters;
using SubcultureRPG.UI;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 호감도 창: 왼쪽 = 배경 없는 일러스트 · 단계 · 진행 막대 · 능력치 보너스, 오른쪽 = 선물 · 단계 보상 · 개인 스토리.
    /// 도감 상세의 '호감도' 버튼과 홈의 대표 캐릭터 이름표에서 엽니다. 데이터: PlayerBond.
    /// </summary>
    public static class BondWindow
    {
        private static readonly Color Pink = new(0.86f, 0.38f, 0.56f, 0.96f);
        private const string PinkHex = "#FF9EC0";

        public static void Open(CharacterData c, TMP_FontAsset font = null)
        {
            if (c == null) return;
            var bond = PlayerBond.Instance;
            var w = OverlayWindow.Open($"호감도 · {c.displayName}", 1500, 910, font);
            var body = UIFactory.NewRect("Body", w.Panel);
            UIFactory.Stretch(body);
            string message = "";

            void Rebuild()
            {
                if (!w.IsOpen) return;
                OverlayWindow.Clear(body);
                var inv = Object.FindAnyObjectByType<PlayerInventory>();
                int lv = bond.Level(c);
                var (cur, need) = bond.Progress(c);

                // ---------------- 왼쪽: 일러스트 · 단계 ----------------
                var artBg = w.Box(body, new Color(0, 0, 0, 0.3f), 36, -90, 420, 600);
                var art = UIFactory.AddImage("Art", artBg.transform, Color.white);
                UIFactory.Stretch(art.rectTransform, 6, 0, 6, 6);
                var cut = GameArt.Cutout(c);
                art.sprite = cut != null ? cut : GameArt.Portrait(c);
                art.preserveAspect = true;
                art.enabled = art.sprite != null;

                var lvText = w.Text(body, $"<b>호감도 <color={PinkHex}>Lv.{lv}</color></b>  <size=60%><color=#9AA3B5>/ {PlayerBond.MaxLevel}</color></size>",
                    32, Color.white, TextAlignmentOptions.Left, 36, -704, 420, 44);
                lvText.characterSpacing = 2f;
                w.Box(body, new Color(1, 1, 1, 0.1f), 36, -756, 420, 20);
                float fill = need > 0 ? Mathf.Clamp01(cur / (float)need) : 1f;
                if (fill > 0f) w.Box(body, Pink, 36, -756, Mathf.Max(20f, 420f * fill), 20);
                w.Text(body, need > 0 ? $"{cur} / {need}" : "최대 단계", 18, new Color(0.85f, 0.88f, 0.94f), TextAlignmentOptions.Right, 36, -780, 420, 26);
                int pct = Mathf.RoundToInt(bond.StatBonusPct(c) * 100f);
                w.Text(body, $"<color=#FFD36B>호감도 보너스</color>  체력 · 공격 · 방어 <b>+{pct}%</b>", 20, Color.white,
                    TextAlignmentOptions.Left, 36, -812, 420, 30);
                var how = w.Text(body, $"<color=#9AA3B5>전투 승리 +{PlayerBond.VictoryExp} (출전 캐릭터)  ·  홈 터치 +{PlayerBond.TouchExp} (오늘 {bond.TouchesLeft}/{PlayerBond.TouchPerDay}번 남음)</color>",
                    16, Color.white, TextAlignmentOptions.Left, 36, -846, 420, 44);
                how.textWrappingMode = TextWrappingModes.Normal;

                // ---------------- 오른쪽 ① 선물 ----------------
                const float X = 490, W = 974;
                w.Text(body, "<b>선물하기</b>", 24, new Color(1f, 0.9f, 0.62f), TextAlignmentOptions.Left, X, -88, 300, 34);
                if (!string.IsNullOrEmpty(message))
                    w.Text(body, message, 20, Color.white, TextAlignmentOptions.Right, X + 300, -90, W - 300, 32);
                for (int i = 0; i < Materials.Gifts.Length; i++)
                {
                    var gift = Materials.Gifts[i];
                    float gx = X + i * 330;
                    var card = w.Box(body, new Color(1, 1, 1, 0.06f), gx, -130, 314, 200);
                    w.Icon(card.transform, GameArt.MaterialIcon(gift), 14, -14, 96);
                    w.Text(card.transform, $"<color={Grades.Hex(Materials.Grade(gift))}><b>{Materials.Name(gift)}</b></color>", 21, Color.white,
                        TextAlignmentOptions.Left, 120, -16, 186, 30).enableAutoSizing = true;
                    int have = inv != null ? inv.Count(gift) : 0;
                    w.Text(card.transform, $"호감도 <color={PinkHex}><b>+{Materials.GiftExp(gift)}</b></color>\n<color=#9AA3B5>보유 {have}개</color>", 19, Color.white,
                        TextAlignmentOptions.Left, 120, -52, 186, 60).lineSpacing = 6f;
                    var g = gift;
                    bool can = bond.GiftUsable(c, g, 1, inv) > 0;
                    w.Btn(card.transform, "1개", 20, Pink, 14, -130, 140, 54, () => Give(g, 1), can);
                    w.Btn(card.transform, "최대", 20, Pink, 160, -130, 140, 54, () => Give(g, int.MaxValue), can);
                }

                // ---------------- 오른쪽 ② 단계 보상 ----------------
                int claimable = bond.ClaimableCount(c);
                w.Text(body, "<b>단계 보상</b>", 24, new Color(1f, 0.9f, 0.62f), TextAlignmentOptions.Left, X, -352, 300, 34);
                w.Btn(body, claimable > 0 ? $"모두 받기 ({claimable})" : "모두 받기", 20, OverlayWindow.Gold, X + W - 200, -346, 200, 46, () =>
                {
                    var r = bond.ClaimAll(c);
                    Rebuild();
                    if (r != null) Popups.Reward("호감도 단계 보상", r);
                }, claimable > 0);
                int claimed = bond.Claimed(c);
                const float cw = 100, gap = (W - cw * 9) / 8f;
                for (int l = 2; l <= PlayerBond.MaxLevel; l++)
                {
                    float cx = X + (l - 2) * (cw + gap);
                    bool got = l <= claimed, ready = !got && l <= lv;
                    var chip = w.Box(body, ready ? new Color(0.85f, 0.62f, 0.22f, 0.55f) : new Color(1, 1, 1, got ? 0.04f : 0.07f), cx, -404, cw, 172);
                    w.Text(chip.transform, $"<b>Lv.{l}</b>", 20, ready ? Color.white : new Color(0.8f, 0.83f, 0.9f), TextAlignmentOptions.Center, 0, -8, cw, 28);
                    var r = PlayerBond.LevelReward(l);
                    w.Icon(chip.transform, r.Icon(), (cw - 56) / 2f, -40, 56);
                    var desc = w.Text(chip.transform, r.Describe("\n"), 13, Color.white, TextAlignmentOptions.Center, 4, -100, cw - 8, 44);
                    desc.textWrappingMode = TextWrappingModes.Normal;
                    desc.enableAutoSizing = true; desc.fontSizeMin = 10; desc.fontSizeMax = 13;
                    string state = got ? "<color=#7BE39A>받음</color>" : ready ? "<color=#FFE08A>받기</color>" : $"<color=#7A8194>잠김</color>";
                    w.Text(chip.transform, state, 16, Color.white, TextAlignmentOptions.Center, 0, -144, cw, 24);
                    if (got) w.Box(chip.transform, new Color(0, 0, 0, 0.35f), 0, 0, cw, 172);
                }

                // ---------------- 오른쪽 ③ 개인 스토리 ----------------
                w.Text(body, "<b>개인 스토리</b>", 24, new Color(1f, 0.9f, 0.62f), TextAlignmentOptions.Left, X, -600, 300, 34);
                for (int ep = 1; ep <= StoryLibrary.BondEpisodes; ep++)
                {
                    float ry = -642 - (ep - 1) * 84;
                    var s = PlayerBond.Story(c, ep);
                    bool open = bond.StoryUnlocked(c, ep);
                    bool seen = s != null && StoryLibrary.Seen(s);
                    var row = w.Box(body, new Color(1, 1, 1, open ? 0.07f : 0.035f), X, ry, W, 76);
                    string title = s != null ? s.title : $"{ep}화";
                    w.Text(row.transform, open ? $"<b>{title}</b>" + (!seen ? "  <color=#FF6B6B><size=80%>NEW</size></color>" : "")
                                               : $"<color=#7A8194><b>{ep}화 · ???</b></color>",
                        24, Color.white, TextAlignmentOptions.Left, 22, -8, 560, 34);
                    string sub = !open ? $"호감도 Lv.{PlayerBond.StoryLevels[ep - 1]}에 열립니다"
                               : seen ? "다시 보기" : $"처음 보면 <color=#8FE9FF>크리스탈 {PlayerBond.StoryRewardCrystals}</color>";
                    w.Text(row.transform, $"<color=#9AA3B5>{sub}</color>", 18, Color.white, TextAlignmentOptions.Left, 22, -42, 560, 28);
                    int e = ep;
                    w.Btn(row.transform, seen ? "다시 보기" : "보기", 20, open ? OverlayWindow.Blue : OverlayWindow.Dim, W - 190, -12, 170, 52,
                        () => PlayStory(e), open && s != null);
                }
            }

            void Give(MaterialType gift, int count)
            {
                var inv = Object.FindAnyObjectByType<PlayerInventory>();
                int before = bond.Level(c);
                int got = bond.Gift(c, gift, count, inv);
                if (got <= 0) return;
                int after = bond.Level(c);
                message = $"<color={PinkHex}>호감도 +{got}</color>" + (after > before ? $"   <color=#FFE08A><b>Lv.{after} 달성!</b></color>" : "");
                Rebuild();
            }

            void PlayStory(int ep)
            {
                var s = PlayerBond.Story(c, ep);
                if (s == null) return;
                var f = w.Font;
                w.Close();
                StoryScreen.Ensure(f).Play(s, () =>
                {
                    var r = bond.FinishStory(c, ep);
                    Open(c, f);   // 창으로 돌아옴
                    if (r != null) Popups.Reward("개인 스토리 첫 감상", r);
                });
            }

            Rebuild();
        }
    }
}
