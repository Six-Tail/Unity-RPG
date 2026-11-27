using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 파견 화면(2026-11 개편).
    ///  · 위: 파견 중인 팀 카드 4칸(장소 그림 · 등급 · 남은 시간 막대 · 팀 · 보상 · 받기/취소, 빈 칸은 안내)
    ///  · 아래: 의뢰 게시판 카드 6장(장소 그림 · 등급 · 시간 · 인원 · 레벨 · 추천 조건 · 보상 · 보내기)
    /// '보내기' → 캐릭터 고르기 창(인원 · 최소 레벨 · 추천 조건 표시) → 출발.
    /// </summary>
    public class DispatchScreen : ModeScreenBase
    {
        private RectTransform _slots, _board;
        private TextMeshProUGUI _slotInfo, _boardInfo;
        private float _tick;

        private const float X0 = 50f, W = 1820f, SlotW = 440f, SlotH = 380f, SlotY = -170f, BoardY = -616f, BoardW = 593.33f, BoardH = 206f;

        private void Awake()
        {
            BuildBase("파견", "Backgrounds/dispatch", 73);
            CurrencyBar.Create(_frame, font, null, new Vector2(-50, -32), Currency.Crystal, Currency.Gold);

            _slotInfo = Text(_frame, "", 28, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(_slotInfo.rectTransform, X0, -116, 1200, 44);
            var claimAll = UIFactory.AddButton("ClaimAll", _frame, "모두 받기", 22, font, new Color(0.3f, 0.62f, 0.42f, 0.96f), Color.white);
            Place((RectTransform)claimAll.transform, X0 + W - 200, -110, 200, 52);
            claimAll.onClick.AddListener(() =>
            {
                var r = PlayerDispatch.Instance.ClaimAll();
                if (r == null) { Toast("끝난 파견이 없습니다"); return; }
                Popups.Reward("파견 보상", r);
            });
            _slots = UIFactory.NewRect("Slots", _frame);
            Place(_slots, X0, SlotY, W, SlotH);

            _boardInfo = Text(_frame, "", 28, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(_boardInfo.rectTransform, X0, -564, 1300, 44);
            var reroll = UIFactory.AddButton("Reroll", _frame, $"새 의뢰  <size=80%><color=#8FE9FF>크리스탈 {PlayerDispatch.RerollCost}</color></size>", 21, font, new Color(1, 1, 1, 0.16f), Color.white);
            Place((RectTransform)reroll.transform, X0 + W - 240, -558, 240, 52);
            reroll.GetComponentInChildren<TextMeshProUGUI>().richText = true;
            reroll.onClick.AddListener(() => Confirm($"크리스탈 {PlayerDispatch.RerollCost}으로 의뢰 6개를 새로 뽑을까요?\n(파견 중인 의뢰는 그대로)", "새로 뽑기", () =>
            {
                string err = PlayerDispatch.Instance.Reroll();
                Toast(err ?? "새 의뢰가 도착했습니다");
            }));
            _board = UIFactory.NewRect("Board", _frame);
            Place(_board, X0, BoardY, W, 2 * BoardH + 12);

            _root.SetActive(false);
            PlayerDispatch.Instance.OnChanged += () => { if (_root != null && _root.activeSelf) Refresh(); };
        }

        private void Update()
        {
            if (_root == null || !_root.activeSelf) return;
            _tick -= Time.unscaledDeltaTime;
            if (_tick > 0f) return;
            _tick = 1f;
            RefreshSlots();   // 남은 시간 갱신
        }

        protected override void Refresh()
        {
            RefreshSlots();
            RefreshBoard();
        }

        private static string LeftText(System.TimeSpan t) =>
            t.TotalHours >= 1 ? $"{(int)t.TotalHours}시간 {t.Minutes:00}분" : $"{t.Minutes}분 {t.Seconds:00}초";

        private string Conditions(PlayerDispatch.Mission m)
        {
            var parts = new List<string>();
            if (m.element >= 0) { var e = Traits.AllElements[m.element]; parts.Add($"<color={Traits.Hex(e)}>{Traits.Name(e)}</color>"); }
            if (m.role >= 0) parts.Add(Traits.Name((Role)m.role));
            return parts.Count == 0 ? "" : "추천 " + string.Join(" + ", parts);
        }

        private static string KindIcon(PlayerDispatch.Kind k) => k switch
        {
            PlayerDispatch.Kind.Gold => "Items/Gold",
            PlayerDispatch.Kind.Exp => "Items/ExpLarge",
            PlayerDispatch.Kind.Skill => "Items/SkillBookBlue",
            PlayerDispatch.Kind.Ascension => "Items/FireAsc2",
            PlayerDispatch.Kind.Equip => "Items/EquipOre",
            _ => "Items/Crystal",
        };

        /// <summary>장소 그림(가운데를 잘라 칸에 꽉 채움) + 등급 배지. 반환 = 그림 칸.</summary>
        private RectTransform PlaceArt(Transform parent, PlayerDispatch.Mission m, float x, float y, float w, float h)
        {
            var g = PlayerDispatch.GradeColor(m.grade);
            var box = UIFactory.AddImage("Art", parent, new Color(g.r * 0.3f, g.g * 0.3f, g.b * 0.3f, 1f), ProceduralSprites.Rounded);
            box.type = Image.Type.Sliced; box.preserveAspect = false;
            Place(box.rectTransform, x, y, w, h);
            box.gameObject.AddComponent<RectMask2D>();
            var sp = GameArt.Load($"Dispatch/place_{PlayerDispatch.PlaceIndex(m)}");
            if (sp != null)
            {
                var im = UIFactory.AddImage("Place", box.transform, Color.white, sp);
                float aspect = sp.rect.width / Mathf.Max(1f, sp.rect.height);
                float iw = Mathf.Max(w, h * aspect), ih = iw / aspect;
                UIFactory.Anchor(im.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                im.rectTransform.sizeDelta = new Vector2(iw, ih); im.rectTransform.anchoredPosition = Vector2.zero;
            }
            var pill = UIFactory.AddImage("Grade", box.transform, g, ProceduralSprites.Rounded);
            pill.type = Image.Type.Sliced; pill.preserveAspect = false;
            Place(pill.rectTransform, 10, -10, 48, 42);
            var gt = Text(pill.transform, $"<b>{PlayerDispatch.GradeName(m.grade)}</b>", 28, new Color(0.12f, 0.1f, 0.15f), FontStyles.Normal, TextAlignmentOptions.Center);
            UIFactory.Stretch(gt.rectTransform);
            return box.rectTransform;
        }

        private void SmallIcon(Transform parent, string key, float x, float y, float size)
        {
            var sp = GameArt.Load(key);
            if (sp == null) return;
            var im = UIFactory.AddImage("Icon", parent, Color.white, sp);
            im.preserveAspect = true;
            Place(im.rectTransform, x, y, size, size);
        }

        private void RefreshSlots()
        {
            var d = PlayerDispatch.Instance;
            var roster = FindAnyObjectByType<PlayerRoster>();
            int done = 0;
            foreach (var a in d.Actives) if (a.Done) done++;
            _slotInfo.text = $"<b>파견 중</b>  <color=#FFD36B>{d.Actives.Count}</color> <size=80%><color=#9AA3B5>/ {PlayerDispatch.MaxActive}팀</color></size>" +
                             (done > 0 ? $"   <size=75%><color=#7CFF9A>완료 {done}팀</color></size>" : "");
            Clear(_slots);
            for (int i = 0; i < PlayerDispatch.MaxActive; i++)
            {
                float x = i * (SlotW + 20);
                if (i >= d.Actives.Count)
                {
                    var empty = Panel(_slots, new Color(1, 1, 1, 0.05f));
                    Place(empty.rectTransform, x, 0, SlotW, SlotH);
                    var plus = Text(empty.transform, "<color=#4B5262>+</color>", 96, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                    Top(plus.rectTransform, 0, -90, 200, 110, new Vector2(0.5f, 1));
                    var e = Text(empty.transform, "<color=#7B8294><b>빈 자리</b></color>", 22, Color.white, FontStyles.Normal, TextAlignmentOptions.Top);
                    Top(e.rectTransform, 0, -210, 400, 110, new Vector2(0.5f, 1));
                    continue;
                }
                var a = d.Actives[i];
                var m = a.mission;
                var g = PlayerDispatch.GradeColor(m.grade);
                var edge = Panel(_slots, a.Done ? new Color(0.45f, 0.95f, 0.55f, 0.9f) : new Color(g.r, g.g, g.b, 0.6f));
                Place(edge.rectTransform, x, 0, SlotW, SlotH);
                var card = Panel(edge.transform, new Color(0.06f, 0.07f, 0.11f, 0.97f));
                UIFactory.Stretch(card.rectTransform, 3, 3, 3, 3);
                var ct = card.transform;

                // 장소 그림 + 아래에 남은 시간 막대
                var art = PlaceArt(ct, m, 3, -3, SlotW - 12, 140);
                var shade = UIFactory.AddImage("Shade", art, new Color(0, 0, 0, 0.55f));
                UIFactory.Anchor(shade.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
                shade.rectTransform.sizeDelta = new Vector2(0, 40); shade.rectTransform.anchoredPosition = Vector2.zero;
                var pbg = UIFactory.AddImage("Prog", art, new Color(1, 1, 1, 0.15f), ProceduralSprites.Rounded);
                pbg.type = Image.Type.Sliced; pbg.preserveAspect = false; pbg.pixelsPerUnitMultiplier = 4f;
                UIFactory.Anchor(pbg.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
                pbg.rectTransform.sizeDelta = new Vector2(220, 12); pbg.rectTransform.anchoredPosition = new Vector2(12, 14);
                var fill = UIFactory.AddImage("Fill", pbg.transform, a.Done ? new Color(0.45f, 0.95f, 0.55f) : new Color(0.45f, 0.75f, 1f), ProceduralSprites.Rounded);
                fill.type = Image.Type.Sliced; fill.preserveAspect = false; fill.pixelsPerUnitMultiplier = 4f;
                fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(a.Progress), 1f);
                fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;
                var left = Text(art, a.Done ? "<color=#7CFF9A><b>완료!</b></color>" : $"<b>{LeftText(a.Left)}</b>", 20, Color.white, FontStyles.Normal, TextAlignmentOptions.Right);
                UIFactory.Anchor(left.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
                left.rectTransform.sizeDelta = new Vector2(180, 30); left.rectTransform.anchoredPosition = new Vector2(-12, 5);

                var t1 = Text(ct, $"<b>{PlayerDispatch.PlaceName(m)}</b>", 24, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                Place(t1.rectTransform, 16, -150, SlotW - 40, 34);
                SmallIcon(ct, KindIcon(m.K), 14, -184, 30);
                var t2 = Text(ct, $"<color=#9AA3B5>{m.hours}시간 · {PlayerDispatch.KindName(m.K)}</color>" + (a.bonus ? "  <color=#7CFF9A>추천 +50%</color>" : ""), 18, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                Place(t2.rectTransform, 50, -186, SlotW - 70, 28);
                // 팀
                float tx = 16;
                foreach (var id in a.team)
                {
                    var c = roster != null && roster.Database != null ? roster.Database.GetCharacter(id) : null;
                    if (c == null) continue;
                    Thumb(ct, c, tx, -220, 68, roster.StarOf(c));
                    tx += 76;
                }
                var rw = PlayerDispatch.RewardOf(m, a.bonus);
                var t3 = Text(ct, rw.Describe(), 17, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                Place(t3.rectTransform, 16, -294, SlotW - 40, 26);

                var aa = a;
                if (a.Done)
                {
                    var b = UIFactory.AddButton("Claim", ct, "보상 받기", 24, font, new Color(0.25f, 0.6f, 0.95f, 0.96f), Color.white);
                    Place((RectTransform)b.transform, 16, -324, SlotW - 38, 46);
                    b.onClick.AddListener(() =>
                    {
                        var r = PlayerDispatch.Instance.Claim(aa);
                        if (r != null) Popups.Reward("파견 보상", r);
                    });
                    NotifyBadge.Attach(b, font, 6, 6, 24).Set(true);
                }
                else
                {
                    var b = UIFactory.AddButton("Cancel", ct, "파견 취소", 20, font, new Color(1, 1, 1, 0.12f), Color.white);
                    Place((RectTransform)b.transform, 16, -324, SlotW - 38, 46);
                    b.onClick.AddListener(() => Confirm("파견을 취소할까요?\n보상 없이 돌아오고, 의뢰는 게시판으로 돌아갑니다.", "취소하기", () => PlayerDispatch.Instance.Cancel(aa)));
                }
            }
        }

        private void RefreshBoard()
        {
            var d = PlayerDispatch.Instance;
            var t = PlayerDispatch.UntilNewDay;
            _boardInfo.text = $"<b>의뢰 게시판</b>   <size=70%><color=#9AA3B5>새 의뢰까지 {t.Hours}시간 {t.Minutes}분</color></size>";
            Clear(_board);
            if (d.Board.Count == 0)
            {
                var row = Panel(_board, new Color(1, 1, 1, 0.05f));
                Place(row.rectTransform, 0, 0, W, 140);
                var e = Text(row.transform, "남은 의뢰가 없습니다. 내일 05:00에 새 의뢰가 도착합니다.", 22, new Color(0.75f, 0.78f, 0.86f), FontStyles.Normal, TextAlignmentOptions.Center);
                UIFactory.Stretch(e.rectTransform, 20, 10, 20, 10);
                return;
            }
            bool full = d.Actives.Count >= PlayerDispatch.MaxActive;
            for (int i = 0; i < d.Board.Count && i < 6; i++)
            {
                var m = d.Board[i];
                var g = PlayerDispatch.GradeColor(m.grade);
                var edge = Panel(_board, new Color(g.r, g.g, g.b, 0.45f));
                Place(edge.rectTransform, (i % 3) * (BoardW + 20), -(i / 3) * (BoardH + 12), BoardW, BoardH);
                var card = Panel(edge.transform, new Color(0.07f, 0.08f, 0.12f, 0.96f));
                UIFactory.Stretch(card.rectTransform, 2, 2, 2, 2);
                var ct = card.transform;
                PlaceArt(ct, m, 4, -4, 196, BoardH - 12);

                const float tx = 214;
                var t1 = Text(ct, $"<b>{PlayerDispatch.PlaceName(m)}</b>", 24, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                Place(t1.rectTransform, tx, -10, BoardW - tx - 16, 34);
                SmallIcon(ct, KindIcon(m.K), tx - 2, -44, 30);
                var t2 = Text(ct, $"<color=#9AA3B5>{m.hours}시간 · {m.members}명 · Lv.{m.minLevel} 이상</color>", 18, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                Place(t2.rectTransform, tx + 34, -46, BoardW - tx - 50, 28);
                var cond = Conditions(m);
                var t3 = Text(ct, cond != "" ? $"{cond}  <color=#7CFF9A>+50%</color>" : "<color=#6B7280>추천 조건 없음</color>", 18, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                Place(t3.rectTransform, tx, -78, BoardW - tx - 16, 28);
                var t4 = Text(ct, PlayerDispatch.RewardOf(m, false).Describe(), 17, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                t4.textWrappingMode = TextWrappingModes.Normal;
                Place(t4.rectTransform, tx, -108, BoardW - tx - 16, 30);
                var mm = m;
                var b = UIFactory.AddButton("Send", ct, full ? "자리 없음" : "보내기", 22, font,
                    full ? new Color(1, 1, 1, 0.08f) : new Color(0.85f, 0.55f, 0.25f, 0.96f), Color.white);
                var brt = (RectTransform)b.transform;
                UIFactory.Anchor(brt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
                brt.sizeDelta = new Vector2(170, 50); brt.anchoredPosition = new Vector2(-12, 12);
                b.interactable = !full;
                b.onClick.AddListener(() => OpenPicker(mm));
            }
        }

        /// <summary>파견 보낼 캐릭터 고르기.</summary>
        private void OpenPicker(PlayerDispatch.Mission m)
        {
            var roster = FindAnyObjectByType<PlayerRoster>();
            if (roster == null || roster.Database == null) return;
            var d = PlayerDispatch.Instance;
            var w = OverlayWindow.Open($"{PlayerDispatch.PlaceName(m)}  ·  {m.members}명 파견", 1400, 900, font);
            var cond = Conditions(m);
            w.Text(w.Panel, $"<color=#9AA3B5>{m.hours}시간 · Lv.{m.minLevel} 이상</color>" + (cond != "" ? $"   {cond} <color=#7CFF9A>(모두 맞추면 보상 +50%)</color>" : ""), 21, Color.white, TextAlignmentOptions.Left, 36, -82, 1200, 34);

            var view = UIFactory.NewRect("View", w.Panel);
            UIFactory.Stretch(view, 30, 140, 30, 130);
            var grid = UIFactory.NewRect("Grid", view);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(150, 196); gl.spacing = new Vector2(14, 14);
            gl.padding = new RectOffset(8, 8, 8, 8);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 8;
            UIFactory.MakeVerticalScroll(view, grid);
            var info = w.Text(w.Panel, "", 22, Color.white, TextAlignmentOptions.Left, 36, -790, 960, 80);
            info.textWrappingMode = TextWrappingModes.Normal;

            var picked = new List<CharacterData>();
            var owned = new List<CharacterData>();
            foreach (var o in roster.Owned)
            {
                var c = roster.Database.GetCharacter(o.id);
                if (c != null) owned.Add(c);
            }
            // 추천 조건에 맞는 캐릭터 · 레벨 높은 순
            int Score(CharacterData c)
            {
                int s = roster.LevelOf(c);
                if (m.element >= 0 && c.element == Traits.AllElements[m.element]) s += 1000;
                if (m.role >= 0 && (int)c.role == m.role) s += 500;
                if (d.IsBusy(c) || !PlayerDispatch.MeetsLevel(m, c, roster)) s -= 5000;
                return s;
            }
            owned.Sort((a, b) => Score(b).CompareTo(Score(a)));
            Button go = null;

            void Rebuild()
            {
                if (!w.IsOpen) return;
                OverlayWindow.Clear(grid);
                foreach (var c in owned)
                {
                    var cc = c;
                    bool busy = d.IsBusy(c), low = !PlayerDispatch.MeetsLevel(m, c, roster), on = picked.Contains(c);
                    bool match = (m.element >= 0 && c.element == Traits.AllElements[m.element]) || (m.role >= 0 && (int)c.role == m.role);
                    var col = StarText.ColorOf(roster.StarOf(c));
                    var cell = UIFactory.AddImage("Cell", grid, on ? new Color(1f, 0.9f, 0.5f, 1f) : new Color(col.r, col.g, col.b, busy || low ? 0.35f : 0.85f), null, raycast: true);
                    var btn = cell.gameObject.AddComponent<Button>();
                    btn.targetGraphic = cell;
                    btn.onClick.AddListener(() =>
                    {
                        if (busy) return;
                        if (low) return;
                        if (picked.Contains(cc)) picked.Remove(cc);
                        else if (picked.Count < m.members) picked.Add(cc);
                        Rebuild();
                    });
                    var inner = UIFactory.AddImage("Inner", cell.transform, new Color(0.1f, 0.1f, 0.15f, 1f));
                    int pad = on ? 5 : 3;
                    UIFactory.Stretch(inner.rectTransform, pad, pad, pad, pad);
                    var th = GameArt.Thumb(c);
                    if (th != null)
                    {
                        var img = UIFactory.AddImage("Thumb", inner.transform, busy || low ? new Color(0.45f, 0.45f, 0.45f) : Color.white, th);
                        img.preserveAspect = true;
                        UIFactory.Stretch(img.rectTransform, 4, 46, 4, 4);
                    }
                    var el = UIFactory.AddText("El", inner.transform, $"<color={Traits.Hex(c.element)}>{Traits.Name(c.element)}</color> · {Traits.Name(c.role)}", 14, font, new Color(0.8f, 0.83f, 0.9f), TextAlignmentOptions.Center);
                    el.richText = true;
                    UIFactory.Anchor(el.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
                    el.rectTransform.sizeDelta = new Vector2(-6, 18); el.rectTransform.anchoredPosition = new Vector2(0, 24);
                    var nm = UIFactory.AddText("Name", inner.transform, $"{c.displayName} <size=80%>Lv.{roster.LevelOf(c)}</size>", 16, font, Color.white, TextAlignmentOptions.Bottom, FontStyles.Bold);
                    nm.richText = true; nm.enableAutoSizing = true; nm.fontSizeMin = 10; nm.fontSizeMax = 16;
                    UIFactory.Anchor(nm.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
                    nm.rectTransform.sizeDelta = new Vector2(-6, 22); nm.rectTransform.anchoredPosition = new Vector2(0, 2);
                    string tag = busy ? "파견 중" : low ? $"Lv.{m.minLevel} 필요" : match ? "추천" : null;
                    if (tag != null)
                    {
                        var tb = UIFactory.AddImage("Tag", inner.transform, busy || low ? new Color(0.45f, 0.45f, 0.5f) : new Color(0.3f, 0.7f, 0.45f), ProceduralSprites.Rounded);
                        tb.type = Image.Type.Sliced; tb.preserveAspect = false;
                        UIFactory.Anchor(tb.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
                        tb.rectTransform.sizeDelta = new Vector2(busy || low ? 96 : 56, 24); tb.rectTransform.anchoredPosition = new Vector2(6, -6);
                        var tt = UIFactory.AddText("T", tb.transform, tag, 14, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                        UIFactory.Stretch(tt.rectTransform);
                    }
                }
                bool bonus = PlayerDispatch.BonusMet(m, picked);
                info.text = $"고른 캐릭터 <b>{picked.Count}</b> / {m.members}" +
                            (picked.Count > 0 && (m.element >= 0 || m.role >= 0) ? (bonus ? "   <color=#7CFF9A>추천 조건 달성 — 보상 +50%</color>" : "   <color=#9AA3B5>추천 조건 미달성</color>") : "") +
                            $"\n<size=85%>예상 보상  {PlayerDispatch.RewardOf(m, bonus && picked.Count > 0).Describe()}</size>";
                if (go != null)
                {
                    bool ok = picked.Count == m.members;
                    go.interactable = ok;
                    go.image.color = ok ? OverlayWindow.Gold : new Color(1, 1, 1, 0.08f);
                }
            }

            w.Btn(w.Panel, "자동 선택", 22, OverlayWindow.Blue, 1010, -796, 160, 72, () =>
            {
                picked.Clear();
                foreach (var c in owned)
                {
                    if (picked.Count >= m.members) break;
                    if (!d.IsBusy(c) && PlayerDispatch.MeetsLevel(m, c, roster)) picked.Add(c);
                }
                Rebuild();
            });
            go = w.Btn(w.Panel, "출발", 26, OverlayWindow.Gold, 1184, -796, 180, 72, () =>
            {
                string err = PlayerDispatch.Instance.Send(m, picked);
                if (err != null) { Popups.Show("파견", err); return; }
                w.Close();
                Toast($"{PlayerDispatch.PlaceName(m)}에 파견을 보냈습니다");
            });
            Rebuild();
        }
    }

    /// <summary>
    /// 무한의 탑 화면(2026-11 개편).
    ///  · 왼쪽 위: 최고 기록 · 다음 큰 보상층까지 진행 막대
    ///  · 왼쪽: 층 목록(층 번호 배지 · 보스층은 보스 얼굴 · 그 층 보상 아이콘 · 상태)
    ///  · 오른쪽: 고른 층 — 제목 · 약점(속성 아이콘) · 등장하는 적 카드 · 첫 클리어 보상 칸 · 도전
    /// </summary>
    public class TowerScreen : ModeScreenBase
    {
        public event System.Action<int> OnChallenge;

        private RectTransform _floors, _detail, _bestFill;
        private TextMeshProUGUI _best, _bestSub;
        private int _selected;

        private static readonly Color Boss = new(1f, 0.6f, 0.42f), Accent = new(0.85f, 0.62f, 0.22f, 0.96f);

        private void Awake()
        {
            BuildBase("무한의 탑", "Backgrounds/tower", 73);
            CurrencyBar.Create(_frame, font, null, new Vector2(-50, -32), Currency.Crystal, Currency.Gold);

            // 왼쪽 위: 기록
            var head = Panel(_frame, new Color(0.06f, 0.07f, 0.11f, 0.92f));
            Place(head.rectTransform, 40, -110, 560, 150);
            SmallIcon(head.transform, "Items/TowerIcon", 14, -16, 118);
            _best = Text(head.transform, "", 34, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(_best.rectTransform, 144, -14, 400, 50);
            _bestSub = Text(head.transform, "", 19, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(_bestSub.rectTransform, 144, -66, 400, 28);
            var bg = UIFactory.AddImage("Bar", head.transform, new Color(1, 1, 1, 0.12f), ProceduralSprites.Rounded);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false; bg.pixelsPerUnitMultiplier = 4f;
            Place(bg.rectTransform, 146, -106, 390, 14);
            var fill = UIFactory.AddImage("Fill", bg.transform, new Color(1f, 0.83f, 0.42f), ProceduralSprites.Rounded);
            fill.type = Image.Type.Sliced; fill.preserveAspect = false; fill.pixelsPerUnitMultiplier = 4f;
            _bestFill = fill.rectTransform;
            _bestFill.anchorMin = Vector2.zero; _bestFill.anchorMax = new Vector2(0, 1); _bestFill.offsetMin = Vector2.zero; _bestFill.offsetMax = Vector2.zero;

            var view = UIFactory.NewRect("FloorView", _frame);
            Place(view, 40, -274, 560, 780);
            _floors = UIFactory.NewRect("Floors", view);
            var vl = _floors.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 8; vl.padding = new RectOffset(4, 4, 4, 4);
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandHeight = false;
            UIFactory.MakeVerticalScroll(view, _floors);

            var panel = Panel(_frame, new Color(0.06f, 0.07f, 0.11f, 0.92f));
            Place(panel.rectTransform, 620, -110, 1260, 944);
            _detail = panel.rectTransform;

            _root.SetActive(false);
            PlayerTower.Instance.OnChanged += () => { if (_root != null && _root.activeSelf) Refresh(); };
        }

        private void SmallIcon(Transform parent, string key, float x, float y, float size, Color? tint = null)
        {
            var sp = GameArt.Load(key);
            if (sp == null) return;
            var im = UIFactory.AddImage("Icon", parent, tint ?? Color.white, sp);
            im.preserveAspect = true; im.raycastTarget = false;
            Place(im.rectTransform, x, y, size, size);
        }

        protected override void Refresh()
        {
            var t = PlayerTower.Instance;
            if (_selected <= 0 || _selected > t.Next) _selected = t.Next;
            int nextBig = ((t.Cleared / 10) + 1) * 10;
            _best.text = $"<color=#9AA3B5><size=70%>최고 기록</size></color>  <color=#FFD36B><b>{t.Cleared}층</b></color>";
            _bestSub.text = $"<color=#9AA3B5>다음 보스층</color> <color=#FF9A6B>{nextBig}층</color>";
            _bestFill.anchorMax = new Vector2(Mathf.Clamp01((t.Cleared % 10) / 10f), 1f);

            // 층 목록: 위가 높은 층(다음 층 + 5층 ~ 깬 층 - 20층)
            Clear(_floors);
            int top = t.Next + 5, bottom = Mathf.Max(1, t.Cleared - 20);
            for (int f = top; f >= bottom; f--) FloorRow(f, t);
            ScrollToSelected(top);
            BuildDetail(_selected);
        }

        private void ScrollToSelected(int top)
        {
            var scroll = _floors.parent.GetComponent<ScrollRect>();
            if (scroll == null) return;
            Canvas.ForceUpdateCanvases();
            int idx = top - _selected;
            int count = _floors.childCount;
            scroll.verticalNormalizedPosition = count > 8 ? Mathf.Clamp01(1f - (idx - 2) / (float)(count - 8)) : 1f;
        }

        private void FloorRow(int f, PlayerTower t)
        {
            bool cleared = f <= t.Cleared, next = f == t.Next, locked = f > t.Next, boss = PlayerTower.IsBoss(f), on = f == _selected;
            var row = Panel(_floors, on ? new Color(0.85f, 0.62f, 0.22f, 0.55f)
                : next ? new Color(0.25f, 0.45f, 0.75f, 0.55f)
                : boss ? new Color(0.42f, 0.18f, 0.22f, 0.7f)
                : cleared ? new Color(0.08f, 0.09f, 0.13f, 0.85f) : new Color(0.06f, 0.07f, 0.1f, 0.6f));
            float h = boss ? 100 : 78;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
            var rt = row.rectTransform;

            // 층 번호 배지
            var badge = UIFactory.AddImage("No", rt, boss ? Boss : cleared ? new Color(0.3f, 0.55f, 0.38f) : next ? new Color(0.35f, 0.6f, 1f) : new Color(0.25f, 0.27f, 0.33f), ProceduralSprites.Circle);
            UIFactory.Anchor(badge.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            badge.rectTransform.sizeDelta = new Vector2(h - 18, h - 18); badge.rectTransform.anchoredPosition = new Vector2(10, 0);
            var no = Text(badge.transform, $"<b>{f}</b>", boss ? 30 : 26, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            UIFactory.Stretch(no.rectTransform, 4, 4, 4, 4);
            TextOutline.Apply(no, 0.2f, new Color32(8, 10, 18, 220));

            float x = h + 4;
            if (boss)
            {
                // 보스 얼굴
                foreach (var (u, isBoss) in PlayerTower.Lineup(f))
                {
                    if (!isBoss) continue;
                    var art = GameArt.Enemy(u.DisplayName);
                    if (art != null)
                    {
                        var im = UIFactory.AddImage("Boss", rt, locked ? new Color(0.4f, 0.4f, 0.45f) : Color.white, art);
                        im.preserveAspect = true; im.raycastTarget = false;
                        UIFactory.Anchor(im.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                        im.rectTransform.sizeDelta = new Vector2(84, 84); im.rectTransform.anchoredPosition = new Vector2(x, 0);
                        x += 92;
                    }
                    break;
                }
            }
            var label = Text(rt, $"<b>{f}층</b>" + (boss ? "  <color=#FF9A6B>BOSS</color>" : "") + $"\n<size=70%><color=#9AA3B5>적 Lv.{PlayerTower.EnemyLevel(f)}</color></size>",
                boss ? 26 : 24, locked ? new Color(0.5f, 0.52f, 0.58f) : Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            label.lineSpacing = -6f;
            UIFactory.Stretch(label.rectTransform, x, 6, 220, 6);

            // 보상 아이콘(앞의 2종) + 상태
            float rx = -112;
            int n = 0;
            foreach (var (icon, _, _, _) in PlayerTower.Reward(f).Parts())
            {
                if (n++ >= 2) break;
                var sp = GameArt.Load(icon);
                if (sp == null) continue;
                var im = UIFactory.AddImage("Reward", rt, cleared ? new Color(1, 1, 1, 0.35f) : Color.white, sp);
                im.preserveAspect = true; im.raycastTarget = false;
                UIFactory.Anchor(im.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
                im.rectTransform.sizeDelta = new Vector2(44, 44); im.rectTransform.anchoredPosition = new Vector2(rx, 0);
                rx -= 50;
            }
            var st = Text(rt, cleared ? "<color=#7CFF9A>돌파</color>" : next ? "<color=#FFD36B><b>도전</b></color>" : "<color=#6B7280>잠김</color>",
                20, Color.white, FontStyles.Normal, TextAlignmentOptions.Right);
            UIFactory.Anchor(st.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            st.rectTransform.sizeDelta = new Vector2(90, 30); st.rectTransform.anchoredPosition = new Vector2(-14, 0);
            if (locked) return;
            int ff = f;
            var b = row.gameObject.AddComponent<Button>();
            b.targetGraphic = row;
            b.onClick.AddListener(() => { _selected = ff; Refresh(); });
            row.gameObject.AddComponent<ButtonFx>();
        }

        private void BuildDetail(int f)
        {
            Clear(_detail);
            var t = PlayerTower.Instance;
            bool boss = PlayerTower.IsBoss(f), cleared = f <= t.Cleared;

            // 제목 띠
            var band = UIFactory.AddImage("Band", _detail, boss ? new Color(0.45f, 0.16f, 0.2f, 0.9f) : new Color(0.14f, 0.2f, 0.32f, 0.9f), ProceduralSprites.Rounded);
            band.type = Image.Type.Sliced; band.preserveAspect = false;
            Place(band.rectTransform, 16, -16, 1228, 96);
            var title = Text(band.transform, $"<b>{f}층</b>" + (boss ? "  <color=#FFB08A><b>보스층</b></color>" : "") + (cleared ? "  <size=60%><color=#7CFF9A>돌파함</color></size>" : ""),
                44, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(title.rectTransform, 24, -18, 600, 60);
            var lv = Text(band.transform, $"<color=#9AA3B5>적</color> Lv.{PlayerTower.EnemyLevel(f)}" +
                (PlayerTower.StatMul(f, false) > 1.001f ? $"   <color=#9AA3B5>능력치</color> ×{PlayerTower.StatMul(f, false):0.00}" : ""), 24, Color.white, FontStyles.Normal, TextAlignmentOptions.Right);
            UIFactory.Anchor(lv.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            lv.rectTransform.sizeDelta = new Vector2(520, 40); lv.rectTransform.anchoredPosition = new Vector2(-24, 0);

            // 약점(속성 아이콘 칩)
            var wh = Text(_detail, "<b>약점</b>", 24, new Color(0.85f, 0.88f, 0.94f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(wh.rectTransform, 32, -132, 100, 40);
            var weak = PlayerTower.Weaknesses(f);
            float wx = 120;
            if (weak.Count == 0)
            {
                var none = Text(_detail, "<color=#9AA3B5>없음</color>", 22, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                Place(none.rectTransform, wx, -134, 200, 36);
            }
            foreach (var e in weak)
            {
                var chip = UIFactory.AddImage("Weak", _detail, new Color(1, 1, 1, 0.08f), ProceduralSprites.Rounded);
                chip.type = Image.Type.Sliced; chip.preserveAspect = false;
                Place(chip.rectTransform, wx, -128, 150, 48);
                SmallIcon(chip.transform, "Elements/" + e, 6, -4, 40);
                var et = Text(chip.transform, $"<color={Traits.Hex(e)}><b>{Traits.Name(e)}</b></color>", 22, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                Place(et.rectTransform, 52, -6, 96, 36);
                wx += 162;
            }

            // 적
            var head = Text(_detail, "<b>등장하는 적</b>", 24, new Color(0.85f, 0.88f, 0.94f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(head.rectTransform, 32, -194, 400, 36);
            float x = 32;
            foreach (var (u, isBoss) in PlayerTower.Lineup(f))
            {
                float w = isBoss ? 250 : 190, h = isBoss ? 300 : 250;
                var cell = Panel(_detail, isBoss ? new Color(0.5f, 0.18f, 0.22f, 0.85f) : new Color(1, 1, 1, 0.07f));
                Place(cell.rectTransform, x, -236 - (isBoss ? 0 : 50), w, h);
                var glow = UIFactory.AddImage("Glow", cell.transform, isBoss ? new Color(1f, 0.45f, 0.35f, 0.35f) : new Color(1, 1, 1, 0.08f), ProceduralSprites.Glow);
                UIFactory.Stretch(glow.rectTransform, 0, 0, 0, 40);
                var art = GameArt.Enemy(u.DisplayName);
                if (art != null)
                {
                    var img = UIFactory.AddImage("Art", cell.transform, Color.white, art);
                    img.preserveAspect = true;
                    UIFactory.Stretch(img.rectTransform, 10, 58, 10, 10);
                }
                SmallIcon(cell.transform, "Elements/" + u.element, 8, -8, 36);
                if (isBoss)
                {
                    var bt = Text(cell.transform, "<color=#FFB08A><b>BOSS</b></color>", 20, Color.white, FontStyles.Normal, TextAlignmentOptions.Right);
                    UIFactory.Anchor(bt.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
                    bt.rectTransform.sizeDelta = new Vector2(100, 30); bt.rectTransform.anchoredPosition = new Vector2(-10, -10);
                }
                var nm = Text(cell.transform, $"<b>{u.DisplayName}</b>\n<size=80%><color={Traits.Hex(u.element)}>{Traits.Name(u.element)}</color></size>", 18, Color.white, FontStyles.Normal, TextAlignmentOptions.Bottom);
                UIFactory.Stretch(nm.rectTransform, 4, 6, 4, 0);
                x += w + 16;
            }

            // 보상
            var r = PlayerTower.Reward(f);
            var rh = Text(_detail, cleared ? "<b>첫 클리어 보상</b>  <color=#7CFF9A>받음</color>" : "<b>첫 클리어 보상</b>", 24, new Color(0.85f, 0.88f, 0.94f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(rh.rectTransform, 32, -560, 600, 36);
            float rx = 32;
            foreach (var (icon, name, count, hex) in r.Parts())
            {
                var cell = Panel(_detail, new Color(1, 1, 1, cleared ? 0.04f : 0.08f));
                Place(cell.rectTransform, rx, -602, 150, 150);
                var sp = GameArt.Load(icon);
                if (sp != null)
                {
                    var img = UIFactory.AddImage("Icon", cell.transform, cleared ? new Color(1, 1, 1, 0.4f) : Color.white, sp);
                    img.preserveAspect = true;
                    UIFactory.Stretch(img.rectTransform, 30, 54, 30, 10);
                }
                var ct = Text(cell.transform, $"<color={hex}>{name}</color>\n<b>{(count >= 1000 ? count.ToString("N0") : "×" + count)}</b>", 16, Color.white, FontStyles.Normal, TextAlignmentOptions.Bottom);
                UIFactory.Stretch(ct.rectTransform, 4, 6, 4, 0);
                rx += 162;
            }


            bool can = f <= t.Next;
            var go = UIFactory.AddButton("Fight", _detail, cleared ? $"{f}층 다시 도전" : $"{f}층 도전", 32, font,
                can ? new Color(0.85f, 0.45f, 0.25f, 0.96f) : new Color(1, 1, 1, 0.08f), Color.white);
            UIFactory.Anchor((RectTransform)go.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
            ((RectTransform)go.transform).sizeDelta = new Vector2(420, 96); ((RectTransform)go.transform).anchoredPosition = new Vector2(-32, 30);
            go.interactable = can;
            int ff = f;
            go.onClick.AddListener(() => OnChallenge?.Invoke(ff));
        }
    }
}
