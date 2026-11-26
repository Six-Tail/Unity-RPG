using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;
using SubcultureRPG.Gacha;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>우편함 창: 우편 목록 · 받기 · 모두 받기 · 받은 우편 삭제.</summary>
    public static class MailWindow
    {
        public static void Open(TMP_FontAsset font = null)
        {
            var mail = PlayerMail.Instance;
            var w = OverlayWindow.Open("우편함", 1240, 860, font);
            var info = w.Text(w.Panel, "", 22, new Color(0.82f, 0.85f, 0.92f), TextAlignmentOptions.Left, 36, -92, 700, 36);
            var status = w.Text(w.Panel, "", 20, new Color(0.55f, 0.92f, 0.65f), TextAlignmentOptions.Center, 30, -808, 1180, 34);
            var list = w.ScrollList(24, 70, 24, 150);

            void Rebuild()
            {
                if (!w.IsOpen) return;
                int n = mail.UnclaimedCount;
                info.text = n > 0 ? $"받지 않은 우편 <color=#FFD36B><b>{n}</b></color>통" : "<color=#9AA3B5>받지 않은 우편이 없습니다</color>";
                OverlayWindow.Clear(list);
                if (mail.Mails.Count == 0)
                {
                    var empty = w.Box(list, new Color(1, 1, 1, 0.05f), 0, 0, 0, 0);
                    empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 140;
                    var et = w.Text(empty.transform, "우편함이 비어 있습니다", 24, new Color(0.7f, 0.73f, 0.8f), TextAlignmentOptions.Center, 0, 0, 0, 0);
                    UIFactory.Stretch(et.rectTransform);
                }
                foreach (var m in mail.Mails)
                {
                    var mm = m;
                    var row = w.Box(list, m.claimed ? new Color(1, 1, 1, 0.04f) : new Color(0.2f, 0.28f, 0.42f, 0.55f), 0, 0, 0, 0);
                    row.gameObject.AddComponent<LayoutElement>().preferredHeight = 124;
                    var ic = w.Icon(row.transform, m.reward != null ? m.reward.Icon() : GameArt.Load("Items/UiMail"), 18, -20, 84);
                    if (m.claimed) ic.color = new Color(1, 1, 1, 0.4f);
                    var tc = m.claimed ? new Color(0.6f, 0.63f, 0.7f) : Color.white;
                    var t1 = w.Text(row.transform, $"<b>{m.title}</b>", 26, tc, TextAlignmentOptions.Left, 118, -10, 760, 38);
                    t1.enableAutoSizing = true; t1.fontSizeMin = 16; t1.fontSizeMax = 26;
                    var t2 = w.Text(row.transform, m.body, 17, new Color(0.72f, 0.75f, 0.82f), TextAlignmentOptions.Left, 118, -48, 820, 26);
                    t2.enableAutoSizing = true; t2.fontSizeMin = 12; t2.fontSizeMax = 17;
                    var t3 = w.Text(row.transform, m.reward != null ? m.reward.Describe() : "", 19, Color.white, TextAlignmentOptions.Left, 118, -80, 840, 32);
                    t3.enableAutoSizing = true; t3.fontSizeMin = 12; t3.fontSizeMax = 19;
                    if (m.claimed) t3.alpha = 0.5f;
                    w.Text(row.transform, m.SentAt.ToString("yyyy.MM.dd"), 16, new Color(0.6f, 0.63f, 0.7f), TextAlignmentOptions.Right, 960, -10, 200, 24);
                    w.Btn(row.transform, m.claimed ? "받음" : "받기", 24, OverlayWindow.Blue, 984, -48, 176, 58, () =>
                    {
                        if (!mail.Claim(mm)) return;
                        status.text = "받았습니다 — " + mm.reward.Describe();
                        Rebuild();
                    }, !m.claimed);
                }
            }

            w.Btn(w.Panel, "모두 받기", 22, OverlayWindow.Green, 780, -86, 200, 50, () =>
            {
                int n = mail.ClaimAll(out var total);
                status.text = n > 0 ? $"{n}통을 받았습니다 — " + total.Describe() : "받을 우편이 없습니다";
                Rebuild();
            });
            w.Btn(w.Panel, "받은 우편 삭제", 22, OverlayWindow.Dim, 992, -86, 220, 50, () =>
            {
                int n = mail.DeleteClaimed();
                status.text = n > 0 ? $"받은 우편 {n}통을 지웠습니다" : "지울 우편이 없습니다";
                Rebuild();
            });
            Rebuild();
        }
    }

    /// <summary>출석부 창: 탭 2개(출석부 28일 · 신규 지휘관 출석 7일). 오늘 칸을 누르거나 '출석 체크'로 받음.</summary>
    public static class AttendanceWindow
    {
        public static void Open(TMP_FontAsset font = null)
        {
            var a = PlayerAttendance.Instance;
            var w = OverlayWindow.Open("출석부", 1560, 970, font);
            bool showNewbie = a.NewbieActive;                       // 이 창을 연 동안은 다 받아도 탭 유지
            bool newbie = showNewbie && (a.CanClaimNewbie || !a.CanClaimMonth);
            var body = UIFactory.NewRect("Body", w.Panel);
            UIFactory.Stretch(body);

            void Claim(bool nb)
            {
                var r = nb ? a.ClaimNewbie() : a.ClaimMonth();
                if (r == null) return;
                Rebuild();
                var sel = r.Selector;
                Popups.Reward(nb ? $"신규 출석 {a.NewbieClaimed}일째 보상" : $"출석 {a.MonthClaimed}일째 보상", r,
                    sel.HasValue ? () => CharacterSelector.Open(sel.Value, w.Font) : null);
            }

            // state: 0 받음 · 1 오늘 받을 칸 · 2 아직
            void Cell(float x, float y, float cw, float ch, int day, RewardBundle r, int state, bool special, bool nb, string note)
            {
                var bg = w.Box(body, state == 1 ? new Color(0.85f, 0.62f, 0.22f, 0.6f)
                    : special ? new Color(0.5f, 0.38f, 0.8f, 0.3f) : new Color(1, 1, 1, 0.07f), x, y, cw, ch, raycast: state == 1);
                // 글자는 크게, 자간 · 줄 간격은 넉넉하게(획이 굵은 글꼴이라 붙어 보이지 않도록)
                var dayText = w.Text(bg.transform, $"<b>{day}일</b>", nb ? 27 : 23, state == 1 ? Color.white : new Color(0.8f, 0.83f, 0.9f),
                    TextAlignmentOptions.Left, 14, nb ? -12 : -8, 120, nb ? 36 : 30);
                dayText.characterSpacing = 3f;
                float icon = nb ? 168 : 66;
                w.Icon(bg.transform, r.Icon(), (cw - icon) / 2f, nb ? -78 : -36, icon);
                var rt = w.Text(bg.transform, r.Describe("\n") + (note != null ? $"\n<size=82%><color=#FFE08A>{note}</color></size>" : ""),
                    nb ? 24 : 19, Color.white, nb ? TextAlignmentOptions.Top : TextAlignmentOptions.Center,
                    6, nb ? -276 : -104, cw - 12, nb ? ch - 292 : ch - 108);
                rt.textWrappingMode = TextWrappingModes.Normal;
                rt.enableAutoSizing = true; rt.fontSizeMin = nb ? 17 : 14; rt.fontSizeMax = nb ? 24 : 19;
                rt.characterSpacing = nb ? 4f : 3f;
                rt.lineSpacing = nb ? 22f : 6f;
                if (state == 1)
                {
                    var today = w.Text(bg.transform, "<b>오늘</b>", nb ? 23 : 20, new Color(1f, 0.95f, 0.7f), TextAlignmentOptions.Right, cw - 104, nb ? -14 : -10, 90, nb ? 32 : 28);
                    today.characterSpacing = 3f;
                    var b = bg.gameObject.AddComponent<Button>();
                    b.targetGraphic = bg;
                    b.onClick.AddListener(() => Claim(nb));
                    bg.gameObject.AddComponent<ButtonFx>();
                }
                else if (state == 0)
                {
                    var shade = w.Box(bg.transform, new Color(0, 0, 0, 0.62f), 0, 0, cw, ch);
                    var done = w.Text(shade.transform, "<b>받음</b>", nb ? 36 : 30, new Color(0.55f, 0.92f, 0.65f), TextAlignmentOptions.Center, 0, 0, 0, 0);
                    UIFactory.Stretch(done.rectTransform);
                    done.characterSpacing = 6f;
                    dayText.transform.SetAsLastSibling();
                }
            }

            // 아래 안내문 · 받은 칸 수
            void Footer(string hint, int claimed, int total)
            {
                var h = w.Text(body, $"<color=#9AA3B5>{hint}</color>", 20, Color.white, TextAlignmentOptions.Left, 36, -852, 566, 84);
                h.textWrappingMode = TextWrappingModes.Normal;
                h.enableAutoSizing = true; h.fontSizeMin = 16; h.fontSizeMax = 20;
                h.characterSpacing = 2f; h.lineSpacing = 12f;
                var n = w.Text(body, $"받은 칸  <color=#FFD36B><b>{claimed}</b></color> / {total}", 27, Color.white, TextAlignmentOptions.Right, 1000, -876, 524, 40);
                n.characterSpacing = 4f;
            }

            void Rebuild()
            {
                if (!w.IsOpen) return;
                OverlayWindow.Clear(body);
                const string mark = "  <color=#FF6B6B><b>!</b></color>";
                w.Btn(body, "출석부 28일" + (a.CanClaimMonth ? mark : ""), 22, !newbie ? OverlayWindow.Blue : OverlayWindow.Dim, 36, -86, 270, 50,
                    () => { newbie = false; Rebuild(); });
                if (showNewbie)
                    w.Btn(body, "신규 지휘관 출석 7일" + (a.CanClaimNewbie ? mark : ""), 22, newbie ? OverlayWindow.Gold : OverlayWindow.Dim, 318, -86, 340, 50,
                        () => { newbie = true; Rebuild(); });

                bool can;
                if (!newbie)
                {
                    int claimed = a.MonthClaimed;
                    can = a.CanClaimMonth;
                    for (int i = 0; i < PlayerAttendance.MonthDays; i++)
                    {
                        int day = i + 1;
                        int state = day <= claimed ? 0 : (day == claimed + 1 && can) ? 1 : 2;
                        Cell(36 + (i % 7) * 214, -152 - (i / 7) * 170, 204, 160, day, PlayerAttendance.MonthReward(day), state, day % 7 == 0, false, null);
                    }
                    Footer("하루에 한 칸(매일 05:00 기준). 빠진 날이 있어도 다음 칸부터 이어서 받습니다. 28일을 모두 받으면 다음 날 1일부터 다시 시작합니다.",
                        claimed, PlayerAttendance.MonthDays);
                }
                else
                {
                    int claimed = a.NewbieClaimed;
                    can = a.CanClaimNewbie;
                    var lead = w.Text(body, "처음 <color=#FFD36B>7번의 출석</color> 동안 더 좋은 보상을 드립니다.   <color=#C9A0FF>3일째</color> 태생 5★ 캐릭터 선택권   ·   <color=#FFD36B>7일째</color> 태생 6★ 캐릭터 선택권",
                        25, Color.white, TextAlignmentOptions.Left, 36, -154, 1480, 40);
                    lead.characterSpacing = 3f;
                    for (int i = 0; i < PlayerAttendance.NewbieDays; i++)
                    {
                        int day = i + 1;
                        int state = day <= claimed ? 0 : (day == claimed + 1 && can) ? 1 : 2;
                        string note = day == 3 ? "원하는 태생 5★ 1명" : day == 7 ? "픽업을 뺀 태생 6★ 1명" : null;
                        Cell(36 + i * 214, -212, 204, 620, day, PlayerAttendance.NewbieReward(day), state, day == 3 || day == 7, true, note);
                    }
                    Footer("하루에 한 칸(매일 05:00 기준). 7칸을 모두 받으면 이 출석부는 사라집니다. 출석부 28일과는 따로 받습니다.",
                        claimed, PlayerAttendance.NewbieDays);
                }
                bool nbNow = newbie;
                w.Btn(body, can ? "출석 체크" : "오늘 출석 완료", 28, nbNow ? OverlayWindow.Gold : OverlayWindow.Blue, 620, -862, 320, 72, () => Claim(nbNow), can);
            }
            Rebuild();
        }
    }

    /// <summary>챕터 별 보상 창: 별 12 · 15 · 18개 단계 보상 받기.</summary>
    public static class ChapterRewardWindow
    {
        public static void Open(StageDatabase db, StageProgress progress, StageDifficulty d, int chapter, TMP_FontAsset font = null, System.Action onChanged = null)
        {
            if (db == null || progress == null) return;
            var w = OverlayWindow.Open($"챕터 {chapter} 별 보상", 960, 540, font);
            var body = UIFactory.NewRect("Body", w.Panel);
            UIFactory.Stretch(body);

            void Rebuild()
            {
                if (!w.IsOpen) return;
                OverlayWindow.Clear(body);
                int stars = ChapterStarRewards.Stars(db, progress, d, chapter);
                int max = db.InChapter(chapter, d).Count * 3;
                string diff = d == StageDifficulty.Easy ? "" : $"<color={Difficulties.Hex(d)}>{Difficulties.Name(d)}</color>  ";
                w.Text(body, $"{diff}{db.ChapterName(chapter)}    <color=#FFD36B>★</color> <b>{stars}</b> / {max}", 24, Color.white, TextAlignmentOptions.Left, 36, -84, 880, 36);
                for (int t = 0; t < ChapterStarRewards.Thresholds.Length; t++)
                {
                    int tier = t, need = ChapterStarRewards.Thresholds[t];
                    bool claimed = ChapterStarRewards.IsClaimed(d, chapter, t), can = !claimed && stars >= need;
                    var row = w.Box(body, claimed ? new Color(1, 1, 1, 0.04f) : can ? new Color(0.3f, 0.55f, 0.4f, 0.45f) : new Color(1, 1, 1, 0.08f), 30, -138 - t * 124, 900, 112);
                    w.Text(row.transform, $"<color=#FFD36B>★</color> <b>{need}</b>", 34, claimed ? new Color(0.6f, 0.63f, 0.7f) : Color.white, TextAlignmentOptions.Center, 10, -30, 150, 52);
                    var r = ChapterStarRewards.Reward(d, chapter, t);
                    w.Icon(row.transform, r.Icon(), 170, -22, 68);
                    var rt = w.Text(row.transform, r.Describe(), 22, claimed ? new Color(0.6f, 0.63f, 0.7f) : Color.white, TextAlignmentOptions.Left, 254, -36, 430, 40);
                    rt.enableAutoSizing = true; rt.fontSizeMin = 13; rt.fontSizeMax = 22;
                    w.Btn(row.transform, claimed ? "받음" : can ? "받기" : $"★ {stars} / {need}", 22, OverlayWindow.Blue, 700, -28, 180, 56, () =>
                    {
                        var got = ChapterStarRewards.Claim(db, progress, d, chapter, tier);
                        if (got == null) return;
                        Rebuild();
                        onChanged?.Invoke();
                        Popups.Reward($"챕터 {chapter} 별 {need}개 보상", got);
                    }, can);
                }
            }
            Rebuild();
        }
    }

    /// <summary>
    /// 캐릭터 선택권 사용 창: 태생 5★ 전체 또는 태생 6★(지금 픽업 중인 캐릭터 제외) 중 1명을 골라 영입.
    /// 이미 가진 캐릭터를 고르면 중복(합성 · 초월 재료)이 됩니다. 풀 초월한 캐릭터는 고를 수 없음.
    /// </summary>
    public static class CharacterSelector
    {
        /// <summary>선택권으로 고를 수 있는 캐릭터.</summary>
        public static List<CharacterData> Candidates(MaterialType ticket, PlayerRoster roster)
        {
            var list = new List<CharacterData>();
            int rarity = Materials.SelectorRarity(ticket);
            if (roster == null || roster.Database == null || rarity == 0) return list;
            var excluded = new HashSet<CharacterData>();
            if (ticket == MaterialType.Select6)
            {
                var gacha = Object.FindAnyObjectByType<GachaController>();
                if (gacha != null)
                    foreach (var b in gacha.Banners)
                        if (b != null && b.IsPickup && b.featured6 != null) excluded.Add(b.featured6);
            }
            foreach (var c in roster.Database.Characters)
                if (c != null && (int)c.rarity == rarity && !excluded.Contains(c)) list.Add(c);
            return list;
        }

        public static void Open(MaterialType ticket, TMP_FontAsset font = null)
        {
            var roster = Object.FindAnyObjectByType<PlayerRoster>();
            var inv = Object.FindAnyObjectByType<PlayerInventory>();
            if (roster == null || inv == null) return;
            int rarity = Materials.SelectorRarity(ticket);
            var w = OverlayWindow.Open(Materials.Name(ticket), 1380, 900, font);
            w.Text(w.Panel, ticket switch
                    {
                        MaterialType.Select6 => "<color=#9AA3B5>태생 6★ 캐릭터 1명을 고르세요. 지금 픽업 중인 캐릭터는 고를 수 없습니다.</color>",
                        MaterialType.Select6All => "<color=#9AA3B5>태생 6★ 캐릭터 1명을 고르세요.</color> <color=#FFD36B>지금 픽업 중인 캐릭터도 고를 수 있습니다.</color>",
                        _ => "<color=#9AA3B5>태생 5★ 캐릭터 1명을 고르세요.</color>",
                    },
                21, Color.white, TextAlignmentOptions.Left, 36, -84, 1000, 32);

            var view = UIFactory.NewRect("View", w.Panel);
            UIFactory.Stretch(view, 30, 130, 30, 130);
            var grid = UIFactory.NewRect("Grid", view);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(150, 196); gl.spacing = new Vector2(14, 14);
            gl.padding = new RectOffset(8, 8, 8, 8);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 8;
            gl.childAlignment = TextAnchor.UpperCenter;
            UIFactory.MakeVerticalScroll(view, grid);

            var info = w.Text(w.Panel, "", 23, Color.white, TextAlignmentOptions.Left, 36, -790, 900, 80);
            info.textWrappingMode = TextWrappingModes.Normal;
            Button confirm = null;
            CharacterData picked = null;
            var cands = Candidates(ticket, roster);

            void Rebuild()
            {
                if (!w.IsOpen) return;
                OverlayWindow.Clear(grid);
                foreach (var c in cands)
                {
                    var cc = c;
                    bool on = c == picked, owned = roster.IsOwned(c), maxed = roster.IsMaxed(c);
                    var col = StarText.ColorOf(rarity);
                    var cell = UIFactory.AddImage("Cell", grid, on ? new Color(1f, 0.9f, 0.5f, 1f) : new Color(col.r, col.g, col.b, 0.85f), null, raycast: true);
                    var btn = cell.gameObject.AddComponent<Button>();
                    btn.targetGraphic = cell;
                    btn.onClick.AddListener(() => { picked = cc; Rebuild(); });
                    cell.gameObject.AddComponent<ButtonFx>();
                    var inner = UIFactory.AddImage("Inner", cell.transform, new Color(0.1f, 0.1f, 0.15f, 1f));
                    int pad = on ? 5 : 3;
                    UIFactory.Stretch(inner.rectTransform, pad, pad, pad, pad);
                    var th = GameArt.Thumb(c);
                    if (th != null)
                    {
                        var img = UIFactory.AddImage("Thumb", inner.transform, maxed ? new Color(0.5f, 0.5f, 0.5f) : Color.white, th);
                        img.preserveAspect = true;
                        UIFactory.Stretch(img.rectTransform, 4, 46, 4, 4);
                    }
                    var el = UIFactory.AddText("El", inner.transform, $"<color={Traits.Hex(c.element)}>{Traits.Name(c.element)}</color> · {Traits.Name(c.role)}", 14, w.Font,
                        new Color(0.8f, 0.83f, 0.9f), TextAlignmentOptions.Center);
                    el.richText = true;
                    UIFactory.Anchor(el.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
                    el.rectTransform.sizeDelta = new Vector2(-6, 18); el.rectTransform.anchoredPosition = new Vector2(0, 24);
                    var name = UIFactory.AddText("Name", inner.transform, c.displayName, 17, w.Font, Color.white, TextAlignmentOptions.Bottom, FontStyles.Bold);
                    name.enableAutoSizing = true; name.fontSizeMin = 11; name.fontSizeMax = 17;
                    UIFactory.Anchor(name.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
                    name.rectTransform.sizeDelta = new Vector2(-6, 22); name.rectTransform.anchoredPosition = new Vector2(0, 2);
                    if (owned)
                    {
                        var tag = UIFactory.AddImage("Owned", inner.transform, maxed ? new Color(0.5f, 0.5f, 0.55f, 1f) : new Color(0.3f, 0.6f, 0.9f, 1f), ProceduralSprites.Rounded);
                        tag.type = Image.Type.Sliced; tag.preserveAspect = false;
                        UIFactory.Anchor(tag.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
                        tag.rectTransform.sizeDelta = new Vector2(maxed ? 84 : 56, 24); tag.rectTransform.anchoredPosition = new Vector2(6, -6);
                        var tt = UIFactory.AddText("T", tag.transform, maxed ? "풀 초월" : "보유", 15, w.Font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                        UIFactory.Stretch(tt.rectTransform);
                    }
                }

                int have = inv.Count(ticket);
                if (picked == null)
                    info.text = cands.Count == 0 ? "<color=#FF8A8A>고를 수 있는 캐릭터가 없습니다</color>" : $"캐릭터를 눌러 고르세요   <color=#9AA3B5>보유 선택권 {have}장</color>";
                else
                {
                    bool maxed = roster.IsMaxed(picked);
                    info.text = $"<b>{picked.displayName}</b>   <color={Traits.Hex(picked.element)}>{Traits.Name(picked.element)}</color> · {Traits.Name(picked.role)}\n" +
                                (maxed ? "<color=#FF8A8A>이미 풀 초월한 캐릭터는 고를 수 없습니다</color>"
                                    : roster.IsOwned(picked) ? "<color=#9AD4FF>이미 보유 — 중복(합성 · 초월 재료)으로 들어옵니다</color>"
                                    : "<color=#7CFF9A>새 캐릭터로 합류합니다</color>");
                }
                if (confirm != null)
                {
                    bool ok = picked != null && have > 0 && !roster.IsMaxed(picked);
                    confirm.interactable = ok;
                    confirm.image.color = ok ? OverlayWindow.Gold : new Color(1, 1, 1, 0.08f);
                    confirm.GetComponentInChildren<TextMeshProUGUI>().text = have > 0 ? "이 캐릭터 영입" : "선택권 없음";
                }
            }

            confirm = w.Btn(w.Panel, "이 캐릭터 영입", 26, OverlayWindow.Gold, 1040, -796, 304, 72, () =>
            {
                var c = picked;
                if (c == null || inv.Count(ticket) <= 0 || roster.IsMaxed(c)) return;
                Popups.Show("캐릭터 영입", $"<b>{c.displayName}</b>을(를) 영입할까요?\n{Materials.Name(ticket)} 1장을 사용합니다.", "영입", () =>
                {
                    if (!inv.Spend(ticket, 1)) return;
                    bool wasOwned = roster.IsOwned(c);
                    roster.Add(c);
                    w.Close();
                    Popups.Show("영입 완료", $"<b>{c.displayName}</b>" + (wasOwned ? " 중복 1개를 얻었습니다.\n육성 화면에서 합성 · 초월에 쓸 수 있습니다." : "이(가) 합류했습니다!\n육성 · 편성 화면에서 만나 보세요."));
                }, "취소");
            });
            Rebuild();
        }
    }
}
