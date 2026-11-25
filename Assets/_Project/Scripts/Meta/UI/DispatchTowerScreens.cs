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
    /// 파견 화면: 왼쪽 = 파견 중인 팀(최대 4, 남은 시간 · 받기 · 취소) / 오른쪽 = 의뢰 게시판(6개, 보내기).
    /// '보내기' → 캐릭터 고르기 창(인원 · 최소 레벨 · 추천 조건 표시) → 출발.
    /// </summary>
    public class DispatchScreen : ModeScreenBase
    {
        private RectTransform _slots, _board;
        private TextMeshProUGUI _boardInfo;
        private float _tick;

        private void Awake()
        {
            BuildBase("파견", "Backgrounds/dispatch", 73);
            CurrencyBar.Create(_frame, font, null, new Vector2(-50, -32), Currency.Crystal, Currency.Gold);

            var lt = Text(_frame, $"<b>파견 중</b>  <size=75%><color=#9AA3B5>최대 {PlayerDispatch.MaxActive}팀 · 접속하지 않아도 시간이 흐릅니다</color></size>", 26, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Top(lt.rectTransform, -480, -104, 860, 40, new Vector2(0.5f, 1));
            var claimAll = UIFactory.AddButton("ClaimAll", _frame, "모두 받기", 20, font, new Color(0.3f, 0.62f, 0.42f, 0.96f), Color.white);
            Top((RectTransform)claimAll.transform, -140, -100, 170, 46, new Vector2(0.5f, 1));
            claimAll.onClick.AddListener(() =>
            {
                var r = PlayerDispatch.Instance.ClaimAll();
                if (r == null) { Toast("끝난 파견이 없습니다"); return; }
                Popups.Reward("파견 보상", r);
            });
            _slots = UIFactory.NewRect("Slots", _frame);
            Top(_slots, -480, -156, 860, 840, new Vector2(0.5f, 1));

            var rt = Text(_frame, "<b>의뢰 게시판</b>", 26, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Top(rt.rectTransform, 460, -104, 860, 40, new Vector2(0.5f, 1));
            _boardInfo = Text(_frame, "", 18, new Color(0.75f, 0.78f, 0.86f), FontStyles.Normal, TextAlignmentOptions.Left);
            Top(_boardInfo.rectTransform, 460, -140, 620, 28, new Vector2(0.5f, 1));
            var reroll = UIFactory.AddButton("Reroll", _frame, $"새 의뢰  <size=80%>크리스탈 {PlayerDispatch.RerollCost}</size>", 19, font, new Color(1, 1, 1, 0.16f), Color.white);
            Top((RectTransform)reroll.transform, 790, -100, 220, 46, new Vector2(0.5f, 1));
            reroll.GetComponentInChildren<TextMeshProUGUI>().richText = true;
            reroll.onClick.AddListener(() => Confirm($"크리스탈 {PlayerDispatch.RerollCost}으로 의뢰 6개를 새로 뽑을까요?\n(파견 중인 의뢰는 그대로)", "새로 뽑기", () =>
            {
                string err = PlayerDispatch.Instance.Reroll();
                Toast(err ?? "새 의뢰가 도착했습니다");
            }));
            _board = UIFactory.NewRect("Board", _frame);
            Top(_board, 460, -176, 860, 820, new Vector2(0.5f, 1));

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

        private void RefreshSlots()
        {
            var d = PlayerDispatch.Instance;
            var roster = FindAnyObjectByType<PlayerRoster>();
            Clear(_slots);
            for (int i = 0; i < PlayerDispatch.MaxActive; i++)
            {
                float y = -i * 206;
                var row = Panel(_slots, new Color(0.06f, 0.07f, 0.11f, 0.88f));
                Place(row.rectTransform, 0, y, 860, 194);
                if (i >= d.Actives.Count)
                {
                    var e = Text(row.transform, "<color=#6B7280>빈 자리 — 오른쪽 게시판에서 의뢰를 골라 보내세요</color>", 22, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
                    UIFactory.Stretch(e.rectTransform, 20, 10, 20, 10);
                    continue;
                }
                var a = d.Actives[i];
                var m = a.mission;
                var g = PlayerDispatch.GradeColor(m.grade);
                var bar = UIFactory.AddImage("Grade", row.transform, g);
                Place(bar.rectTransform, 0, -14, 8, 166);
                var t1 = Text(row.transform, $"<color=#{ColorUtility.ToHtmlStringRGB(g)}><b>{PlayerDispatch.GradeName(m.grade)}</b></color>  <b>{PlayerDispatch.PlaceName(m)}</b>   <size=75%><color=#9AA3B5>{m.hours}시간 · {PlayerDispatch.KindName(m.K)}</color></size>",
                    24, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                Place(t1.rectTransform, 24, -12, 560, 36);
                // 팀
                float tx = 24;
                foreach (var id in a.team)
                {
                    var c = roster != null && roster.Database != null ? roster.Database.GetCharacter(id) : null;
                    if (c == null) continue;
                    Thumb(row.transform, c, tx, -56, 76, roster.StarOf(c));
                    tx += 84;
                }
                var rw = PlayerDispatch.RewardOf(m, a.bonus);
                var t2 = Text(row.transform, rw.Describe() + (a.bonus ? "  <color=#7CFF9A>(추천 조건 +50%)</color>" : ""), 18, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                Place(t2.rectTransform, 290, -62, 330, 64);
                t2.textWrappingMode = TextWrappingModes.Normal;
                // 진행
                var pbg = UIFactory.AddImage("Prog", row.transform, new Color(0, 0, 0, 0.5f));
                Place(pbg.rectTransform, 24, -152, 600, 14);
                var fill = UIFactory.AddImage("Fill", pbg.transform, a.Done ? new Color(0.45f, 0.95f, 0.55f) : new Color(0.45f, 0.75f, 1f));
                fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(a.Progress, 1f);
                fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;
                var t3 = Text(row.transform, a.Done ? "<color=#7CFF9A><b>완료!</b></color>" : $"남은 시간 <b>{LeftText(a.Left)}</b>", 18, Color.white, FontStyles.Normal, TextAlignmentOptions.Right);
                Place(t3.rectTransform, 380, -122, 244, 28);
                var aa = a;
                if (a.Done)
                {
                    var b = UIFactory.AddButton("Claim", row.transform, "받기", 24, font, new Color(0.25f, 0.6f, 0.95f, 0.96f), Color.white);
                    Place((RectTransform)b.transform, 650, -46, 186, 100);
                    b.onClick.AddListener(() =>
                    {
                        var r = PlayerDispatch.Instance.Claim(aa);
                        if (r != null) Popups.Reward("파견 보상", r);
                    });
                    NotifyBadge.Attach(b, font, 6, 6, 26).Set(true);
                }
                else
                {
                    var b = UIFactory.AddButton("Cancel", row.transform, "취소", 20, font, new Color(1, 1, 1, 0.12f), Color.white);
                    Place((RectTransform)b.transform, 650, -60, 186, 70);
                    b.onClick.AddListener(() => Confirm("파견을 취소할까요?\n보상 없이 돌아오고, 의뢰는 게시판으로 돌아갑니다.", "취소하기", () => PlayerDispatch.Instance.Cancel(aa)));
                }
            }
        }

        private void RefreshBoard()
        {
            var d = PlayerDispatch.Instance;
            var t = PlayerDispatch.UntilNewDay;
            _boardInfo.text = $"새 의뢰까지 {t.Hours}시간 {t.Minutes}분  ·  추천 조건을 모두 맞추면 보상 +50%";
            Clear(_board);
            if (d.Board.Count == 0)
            {
                var row = Panel(_board, new Color(1, 1, 1, 0.05f));
                Place(row.rectTransform, 0, 0, 860, 140);
                var e = Text(row.transform, "남은 의뢰가 없습니다. 내일 05:00에 새 의뢰가 도착합니다.", 22, new Color(0.75f, 0.78f, 0.86f), FontStyles.Normal, TextAlignmentOptions.Center);
                UIFactory.Stretch(e.rectTransform, 20, 10, 20, 10);
                return;
            }
            for (int i = 0; i < d.Board.Count; i++)
            {
                var m = d.Board[i];
                var g = PlayerDispatch.GradeColor(m.grade);
                var row = Panel(_board, new Color(0.07f, 0.08f, 0.12f, 0.9f));
                Place(row.rectTransform, 0, -i * 134, 860, 124);
                var badge = Panel(row.transform, new Color(g.r, g.g, g.b, 0.9f));
                Place(badge.rectTransform, 16, -24, 76, 76);
                var gt = Text(badge.transform, $"<b>{PlayerDispatch.GradeName(m.grade)}</b>", 42, new Color(0.12f, 0.1f, 0.15f), FontStyles.Normal, TextAlignmentOptions.Center);
                UIFactory.Stretch(gt.rectTransform);
                var t1 = Text(row.transform, $"<b>{PlayerDispatch.PlaceName(m)}</b>   <size=75%><color=#9AA3B5>{m.hours}시간 · {m.members}명 · Lv.{m.minLevel} 이상</color></size>", 24, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                Place(t1.rectTransform, 108, -10, 540, 36);
                var cond = Conditions(m);
                var t2 = Text(row.transform, PlayerDispatch.RewardOf(m, false).Describe() + (cond != "" ? $"\n<size=90%>{cond}  <color=#7CFF9A>+50%</color></size>" : ""), 18, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                Place(t2.rectTransform, 108, -50, 540, 66);
                var mm = m;
                bool full = d.Actives.Count >= PlayerDispatch.MaxActive;
                var b = UIFactory.AddButton("Send", row.transform, full ? "자리 없음" : "보내기", 22, font,
                    full ? new Color(1, 1, 1, 0.08f) : new Color(0.85f, 0.55f, 0.25f, 0.96f), Color.white);
                Place((RectTransform)b.transform, 670, -30, 170, 64);
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
    /// 무한의 탑 화면: 왼쪽 = 층 목록(지금 층 근처, 보스층 표시) / 오른쪽 = 고른 층 정보(적 · 레벨 · 약점 · 첫 클리어 보상) · 도전.
    /// </summary>
    public class TowerScreen : ModeScreenBase
    {
        public event System.Action<int> OnChallenge;

        private RectTransform _floors, _detail;
        private TextMeshProUGUI _best;
        private int _selected;

        private void Awake()
        {
            BuildBase("무한의 탑", "Backgrounds/tower", 73);
            CurrencyBar.Create(_frame, font, null, new Vector2(-50, -32), Currency.Crystal, Currency.Gold);

            _best = Text(_frame, "", 26, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Top(_best.rectTransform, -560, -104, 700, 40, new Vector2(0.5f, 1));
            var view = UIFactory.NewRect("FloorView", _frame);
            Top(view, -560, -152, 700, 840, new Vector2(0.5f, 1));
            _floors = UIFactory.NewRect("Floors", view);
            var vl = _floors.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 8; vl.padding = new RectOffset(4, 4, 4, 4);
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandHeight = false;
            UIFactory.MakeVerticalScroll(view, _floors);

            var panel = Panel(_frame, new Color(0.06f, 0.07f, 0.11f, 0.9f));
            Top(panel.rectTransform, 380, -104, 1000, 880, new Vector2(0.5f, 1));
            _detail = panel.rectTransform;

            _root.SetActive(false);
            PlayerTower.Instance.OnChanged += () => { if (_root != null && _root.activeSelf) Refresh(); };
        }

        protected override void Refresh()
        {
            var t = PlayerTower.Instance;
            if (_selected <= 0 || _selected > t.Next) _selected = t.Next;
            _best.text = $"<b>최고 기록</b>  <color=#FFD36B><b>{t.Cleared}층</b></color>   <size=75%><color=#9AA3B5>행동력 · 도전 횟수 없이 몇 번이든 도전</color></size>";

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
            var row = Panel(_floors, on ? new Color(0.85f, 0.62f, 0.22f, 0.6f)
                : next ? new Color(0.25f, 0.45f, 0.75f, 0.55f)
                : boss ? new Color(0.45f, 0.2f, 0.25f, 0.6f)
                : cleared ? new Color(1, 1, 1, 0.05f) : new Color(1, 1, 1, 0.03f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = boss ? 92 : 76;
            var label = Text(row.transform,
                $"<b>{f}층</b>" + (boss ? "  <color=#FF9A6B>BOSS</color>" : "") +
                $"   <size=72%><color=#9AA3B5>Lv.{PlayerTower.EnemyLevel(f)}</color></size>",
                boss ? 32 : 28, locked ? new Color(0.5f, 0.52f, 0.58f) : Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            UIFactory.Stretch(label.rectTransform, 22, 6, 200, 6);
            var st = Text(row.transform, cleared ? "<color=#7CFF9A>돌파</color>" : next ? "<color=#FFD36B><b>도전 가능</b></color>" : "<color=#6B7280>잠김</color>",
                20, Color.white, FontStyles.Normal, TextAlignmentOptions.Right);
            UIFactory.Stretch(st.rectTransform, 400, 6, 22, 6);
            if (locked) return;
            int ff = f;
            var b = row.gameObject.AddComponent<Button>();
            b.targetGraphic = row;
            b.onClick.AddListener(() => { _selected = ff; Refresh(); });
        }

        private void BuildDetail(int f)
        {
            Clear(_detail);
            var t = PlayerTower.Instance;
            bool boss = PlayerTower.IsBoss(f), cleared = f <= t.Cleared;
            var title = Text(_detail, $"<b>{f}층</b>" + (boss ? "  <color=#FF9A6B><b>보스층</b></color>" : "") + $"   <size=65%><color=#9AA3B5>적 Lv.{PlayerTower.EnemyLevel(f)}" +
                (PlayerTower.StatMul(f, false) > 1.001f ? $" · 능력치 ×{PlayerTower.StatMul(f, false):0.00}" : "") + "</color></size>", 40, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(title.rectTransform, 36, -24, 900, 56);

            // 적
            var head = Text(_detail, "<b>등장하는 적</b>", 22, new Color(0.85f, 0.88f, 0.94f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(head.rectTransform, 36, -100, 400, 32);
            float x = 36;
            foreach (var (u, isBoss) in PlayerTower.Lineup(f))
            {
                float size = isBoss ? 190 : 150;
                var cell = Panel(_detail, isBoss ? new Color(0.5f, 0.2f, 0.22f, 0.8f) : new Color(1, 1, 1, 0.07f));
                Place(cell.rectTransform, x, -140 - (isBoss ? 0 : 40), size, size + 46);
                var art = GameArt.Enemy(u.DisplayName);
                if (art != null)
                {
                    var img = UIFactory.AddImage("Art", cell.transform, Color.white, art);
                    img.preserveAspect = true;
                    UIFactory.Stretch(img.rectTransform, 8, 46, 8, 8);
                }
                var nm = Text(cell.transform, $"<b>{u.DisplayName}</b>\n<size=80%><color={Traits.Hex(u.element)}>{Traits.Name(u.element)}</color></size>", 17, Color.white, FontStyles.Normal, TextAlignmentOptions.Bottom);
                UIFactory.Stretch(nm.rectTransform, 4, 4, 4, 0);
                x += size + 16;
            }

            // 약점
            var weak = PlayerTower.Weaknesses(f);
            var parts = new List<string>();
            foreach (var e in weak) parts.Add($"<color={Traits.Hex(e)}><b>{Traits.Name(e)}</b></color>");
            var wk = Text(_detail, "<b>약점</b>   " + (parts.Count > 0 ? string.Join("  ·  ", parts) : "<color=#9AA3B5>없음</color>"), 24, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(wk.rectTransform, 36, -410, 900, 36);

            // 보상
            var r = PlayerTower.Reward(f);
            var rh = Text(_detail, cleared ? "<b>첫 클리어 보상</b>  <color=#7CFF9A>받음</color>" : "<b>첫 클리어 보상</b>", 22, new Color(0.85f, 0.88f, 0.94f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(rh.rectTransform, 36, -476, 600, 32);
            float rx = 36;
            foreach (var (icon, name, count, hex) in r.Parts())
            {
                var cell = Panel(_detail, new Color(1, 1, 1, cleared ? 0.04f : 0.08f));
                Place(cell.rectTransform, rx, -516, 150, 150);
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

            // 다음 큰 보상 안내
            int nextBig = ((t.Cleared / 10) + 1) * 10;
            var hint = Text(_detail, $"<color=#9AA3B5>5층마다 크리스탈 · 10층마다 크리스탈과 스킬 교본 · 50층마다 픽업 뽑기권    다음 보스층 <color=#FF9A6B>{nextBig}층</color></color>", 18, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(hint.rectTransform, 36, -690, 930, 30);

            bool can = f <= t.Next;
            var go = UIFactory.AddButton("Fight", _detail, cleared ? $"{f}층 다시 도전" : $"{f}층 도전", 32, font,
                can ? new Color(0.85f, 0.45f, 0.25f, 0.96f) : new Color(1, 1, 1, 0.08f), Color.white);
            UIFactory.Anchor((RectTransform)go.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            ((RectTransform)go.transform).sizeDelta = new Vector2(420, 96); ((RectTransform)go.transform).anchoredPosition = new Vector2(0, 36);
            go.interactable = can;
            int ff = f;
            go.onClick.AddListener(() => OnChallenge?.Invoke(ff));
        }
    }
}
