using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 스테이지 화면의 '메인 스토리' 쪽.
    ///  · 왼쪽: 챕터 지도 — 챕터 그림 위에 6개 스테이지가 길을 따라 이어짐(지나온 길은 금색, 진행 중 스테이지 표시, 보스는 크게 + 보스 그림)
    ///    위쪽 띠: 챕터 이름 · 별 진행 막대(12 · 15 · 18 보상 지점) · '별 보상', 양옆 '<' '>'로 이전 · 다음 챕터
    ///  · 오른쪽 상세: 권장 레벨 · 기록 → 적 구성(그림 카드) → 별 조건(받은 별은 금색) → 보상(아이콘) → 클리어 시 개방 → 출격
    /// </summary>
    public partial class StageScreen
    {
        private RectTransform _mapPanel, _mapNodes;
        private BackgroundArt _mapArt;
        private Image _mapBorder;
        private TextMeshProUGUI _mapChapter, _mapName, _mapStars;
        private RectTransform _mapBar, _mapBarFill;
        private Button _prevCh, _nextCh;
        private RectTransform _mainDet;
        private GameObject _mainDetView, _detBodyView;

        private const float MapW = 1040f, MapH = 814f;

        /// <summary>스테이지 1 ~ 6 위치(지도 왼쪽 위 기준, 가운데 점). 6 = 보스.</summary>
        private static readonly Vector2[] NodePos =
        {
            new(140, 660), new(310, 500), new(495, 645), new(645, 445), new(812, 610), new(850, 290),
        };

        // ---------------- 구성 ----------------

        private void BuildChapterMap()
        {
            var bg = UIFactory.AddImage("ChapterMap", _root.transform, new Color(0.85f, 0.65f, 0.3f, 1f), ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            _mapBorder = bg;
            _mapPanel = bg.rectTransform;
            UIFactory.Anchor(_mapPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            _mapPanel.sizeDelta = new Vector2(MapW, MapH); _mapPanel.anchoredPosition = new Vector2(-380, -196);
            _nodesView = bg.gameObject;

            var art = UIFactory.NewRect("Art", _mapPanel);
            UIFactory.Stretch(art, 4, 4, 4, 4);
            _mapArt = BackgroundArt.Create(art, 0.22f);
            _mapArt.Set("Backgrounds/stage");
            // 아래쪽을 살짝 어둡게(스테이지 이름이 그림에 묻히지 않게)
            var shade = UIFactory.AddImage("Shade", art, new Color(0, 0, 0, 0.55f), Fade());
            shade.preserveAspect = false;
            UIFactory.Anchor(shade.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            shade.rectTransform.sizeDelta = new Vector2(0, 360); shade.rectTransform.anchoredPosition = Vector2.zero;

            // 위쪽 띠: 챕터 · 이름 · 별 진행
            var head = UIFactory.AddImage("Head", art, new Color(0.04f, 0.05f, 0.09f, 0.78f));
            P(head.rectTransform, 0, 0, MapW - 8, 118);
            _mapChapter = UIFactory.AddText("Chapter", head.rectTransform, "", 22, font, new Color(1f, 0.83f, 0.42f), TextAlignmentOptions.Left, FontStyles.Bold);
            _mapChapter.richText = true;
            P(_mapChapter.rectTransform, 28, 14, 520, 30);
            _mapName = UIFactory.AddText("Name", head.rectTransform, "", 42, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            _mapName.richText = true;
            P(_mapName.rectTransform, 26, 44, 520, 58);
            _mapName.enableAutoSizing = true; _mapName.fontSizeMin = 24; _mapName.fontSizeMax = 42;

            _mapStars = UIFactory.AddText("Stars", head.rectTransform, "", 24, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            _mapStars.richText = true;
            P(_mapStars.rectTransform, 560, 18, 260, 32);
            var barBg = UIFactory.AddImage("BarBg", head.rectTransform, new Color(1, 1, 1, 0.12f), ProceduralSprites.Rounded);
            barBg.type = Image.Type.Sliced; barBg.preserveAspect = false; barBg.pixelsPerUnitMultiplier = 4f;
            _mapBar = barBg.rectTransform;
            P(_mapBar, 560, 62, 260, 14);
            var fill = UIFactory.AddImage("Fill", _mapBar, new Color(1f, 0.83f, 0.42f), ProceduralSprites.Rounded);
            fill.type = Image.Type.Sliced; fill.preserveAspect = false; fill.pixelsPerUnitMultiplier = 4f;
            _mapBarFill = fill.rectTransform;
            _mapBarFill.anchorMin = Vector2.zero; _mapBarFill.anchorMax = new Vector2(0, 1);
            _mapBarFill.offsetMin = Vector2.zero; _mapBarFill.offsetMax = Vector2.zero;

            // 별 보상 버튼을 띠 오른쪽으로
            var srrt = (RectTransform)_starRewardBtn.transform;
            srrt.SetParent(head.transform, false);
            P(srrt, MapW - 8 - 196, 30, 170, 56);
            _starRewardBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 22;

            _mapNodes = UIFactory.NewRect("Nodes", art);
            UIFactory.Stretch(_mapNodes);

            // 이전 · 다음 챕터
            _prevCh = UIFactory.AddButton("PrevCh", _mapPanel, "<", 40, font, new Color(0, 0, 0, 0.55f), Color.white);
            P((RectTransform)_prevCh.transform, 10, 360, 58, 110);
            _prevCh.onClick.AddListener(() => StepChapter(-1));
            _nextCh = UIFactory.AddButton("NextCh", _mapPanel, ">", 40, font, new Color(0, 0, 0, 0.55f), Color.white);
            P((RectTransform)_nextCh.transform, MapW - 68, 360, 58, 110);
            _nextCh.onClick.AddListener(() => StepChapter(1));
        }

        /// <summary>오른쪽 상세의 메인 스테이지용 칸(그림 카드 · 아이콘). 던전은 원래 글 상세를 씀.</summary>
        private void BuildMainDetail(Transform panel, RectTransform bodyView)
        {
            _detBodyView = bodyView.gameObject;
            var view = UIFactory.NewRect("MainView", panel);
            UIFactory.Stretch(view, 20, 252, 20, 96);
            _mainDetView = view.gameObject;
            _mainDet = UIFactory.NewRect("Content", view);
            _mainDet.gameObject.AddComponent<LayoutElement>();
            UIFactory.MakeVerticalScroll(view, _mainDet);
            _mainDetView.SetActive(false);
        }

        private void StepChapter(int dir)
        {
            var chapters = database.Chapters(_difficulty);
            int i = chapters.IndexOf(_chapter) + dir;
            if (i < 0 || i >= chapters.Count || !ChapterOpen(chapters[i])) return;
            _chapter = chapters[i];
            _selected = null;
            Refresh();
        }

        private bool ChapterOpen(int ch)
        {
            var first = database.InChapter(ch, _difficulty);
            return first.Count > 0 && (progress == null || progress.IsUnlocked(first[0], database));
        }

        // ---------------- 챕터 지도 ----------------

        /// <summary>지금 챕터의 그림 · 머리띠 · 스테이지 길을 다시 그림.</summary>
        private void RebuildChapterMap()
        {
            Clear(_mapNodes);
            _nodeHL.Clear();
            var stages = database.InChapter(_chapter, _difficulty);
            var dcol = Difficulties.ColorOf(_difficulty);
            _mapBorder.color = _difficulty == StageDifficulty.Easy ? new Color(0.85f, 0.65f, 0.3f, 1f) : new Color(dcol.r, dcol.g, dcol.b, 1f);
            _mapArt.Set($"Backgrounds/battle_ch{_chapter}", "Backgrounds/stage");

            string diff = _difficulty == StageDifficulty.Easy ? "" : $"  <color={Difficulties.Hex(_difficulty)}>{Difficulties.Name(_difficulty)}</color>";
            _mapChapter.text = $"CHAPTER {_chapter}{diff}";
            _mapName.text = database.ChapterName(_chapter);
            int stars = progress != null ? progress.TotalStars(database, _chapter, _difficulty) : 0, max = stages.Count * 3;
            int nextGoal = 0;
            foreach (var t in ChapterStarRewards.Thresholds) if (stars < t) { nextGoal = t; break; }
            _mapStars.text = $"<color=#FFD36B>★</color> {stars} / {max}" + (nextGoal > 0 ? $"  <size=72%><color=#9AA3B5>다음 보상 ★{nextGoal}</color></size>" : "  <size=72%><color=#7CFF9A>모두 달성</color></size>");
            _mapBarFill.anchorMax = new Vector2(max > 0 ? Mathf.Clamp01(stars / (float)max) : 0f, 1f);
            // 별 보상 지점 눈금
            for (int i = _mapBar.childCount - 1; i >= 1; i--) Destroy(_mapBar.GetChild(i).gameObject);
            if (max > 0)
                foreach (var t in ChapterStarRewards.Thresholds)
                {
                    float x = 260f * Mathf.Clamp01(t / (float)max);
                    var mk = UIFactory.AddImage("Mark", _mapBar, stars >= t ? new Color(1f, 0.95f, 0.7f) : new Color(0.35f, 0.37f, 0.45f), ProceduralSprites.Star);
                    UIFactory.Anchor(mk.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f));
                    mk.rectTransform.sizeDelta = new Vector2(24, 24); mk.rectTransform.anchoredPosition = new Vector2(Mathf.Min(x, 250f), 0);
                }

            var chapters = database.Chapters(_difficulty);
            int ci = chapters.IndexOf(_chapter);
            _prevCh.gameObject.SetActive(ci > 0);
            _nextCh.gameObject.SetActive(ci >= 0 && ci + 1 < chapters.Count);
            _nextCh.interactable = ci + 1 < chapters.Count && ChapterOpen(chapters[ci + 1]);

            // 진행 중 = 열렸지만 아직 못 깬 첫 스테이지
            StageData frontier = stages.Find(s => (progress == null || progress.IsUnlocked(s, database)) && (progress == null || !progress.IsCleared(s)));

            // 길(점선): 앞 스테이지를 깼으면 금색
            for (int i = 0; i + 1 < stages.Count && i + 1 < NodePos.Length; i++)
            {
                bool walked = progress != null && progress.IsCleared(stages[i]);
                PathDots(NodePos[i], NodePos[i + 1], walked);
            }
            for (int i = 0; i < stages.Count && i < NodePos.Length; i++)
                MapNode(stages[i], NodePos[i], stages[i] == frontier);
        }

        private void PathDots(Vector2 a, Vector2 b, bool walked)
        {
            var dir = b - a;
            float len = dir.magnitude;
            const float gap = 26f, skip = 80f;
            for (float d = skip; d < len - skip; d += gap)
            {
                var p = a + dir * (d / len);
                var dot = UIFactory.AddImage("Dot", _mapNodes, walked ? new Color(1f, 0.85f, 0.45f, 0.95f) : new Color(1f, 1f, 1f, 0.4f), ProceduralSprites.Circle);
                UIFactory.Anchor(dot.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f));
                dot.rectTransform.sizeDelta = walked ? new Vector2(14, 14) : new Vector2(10, 10);
                dot.rectTransform.anchoredPosition = new Vector2(p.x, -p.y);
                var sh = dot.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.6f); sh.effectDistance = new Vector2(0, -2);
            }
        }

        private void MapNode(StageData s, Vector2 pos, bool current)
        {
            bool open = Unlocked(s), cleared = progress != null && progress.IsCleared(s);
            int stars = progress != null ? progress.StarsOf(s) : 0;
            bool boss = s.isBoss;
            float size = boss ? 190f : 140f;
            var node = UIFactory.NewRect("Stage " + s.Code, _mapNodes);
            UIFactory.Anchor(node, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f));
            node.sizeDelta = new Vector2(size, size); node.anchoredPosition = new Vector2(pos.x, -pos.y);

            // 고른 스테이지: 뒤에 빛
            var glow = UIFactory.AddImage("Glow", node, new Color(0.3f, 0.8f, 1f, 0f), ProceduralSprites.Glow);
            glow.preserveAspect = false;
            UIFactory.Stretch(glow.rectTransform, -60, -60, -60, -60);
            _nodeHL[s] = glow;

            Color ring = !open ? new Color(0.36f, 0.38f, 0.44f) : current ? new Color(0.4f, 0.88f, 1f) : boss ? new Color(0.95f, 0.38f, 0.38f) : new Color(1f, 0.83f, 0.42f);
            var outer = UIFactory.AddImage("Ring", node, ring, ProceduralSprites.Circle, raycast: true);
            UIFactory.Stretch(outer.rectTransform);
            var sh = outer.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.7f); sh.effectDistance = new Vector2(0, -5);
            var btn = outer.gameObject.AddComponent<Button>();
            btn.targetGraphic = outer;
            var ss = s;
            btn.onClick.AddListener(() => { _selected = ss; RefreshHighlights(); RefreshDetail(); });
            outer.gameObject.AddComponent<ButtonFx>();

            var inner = UIFactory.AddImage("Inner", outer.transform, open ? new Color(0.08f, 0.09f, 0.14f, 1f) : new Color(0.06f, 0.06f, 0.08f, 1f), ProceduralSprites.Circle);
            UIFactory.Stretch(inner.rectTransform, 7, 7, 7, 7);

            if (boss)
            {
                // 보스: 동그란 틀 안에 보스 그림
                var bossUnit = s.enemies != null ? s.enemies.Find(e => e != null && e.unit != null && e.level == s.MaxEnemyLevel) : null;
                var sp = bossUnit != null ? GameArt.Enemy(bossUnit.unit.DisplayName) : null;
                if (sp != null)
                {
                    inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                    var im = UIFactory.AddImage("Boss", inner.transform, open ? Color.white : new Color(0.3f, 0.3f, 0.34f), sp);
                    im.preserveAspect = true;
                    UIFactory.Stretch(im.rectTransform, -10, -30, -10, 0);
                }
                var tag = UIFactory.AddImage("BossTag", node, new Color(0.85f, 0.2f, 0.25f, 1f), ProceduralSprites.Rounded);
                tag.type = Image.Type.Sliced; tag.preserveAspect = false;
                UIFactory.Anchor(tag.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f));
                tag.rectTransform.sizeDelta = new Vector2(96, 32); tag.rectTransform.anchoredPosition = new Vector2(0, 2);
                var tt = UIFactory.AddText("T", tag.rectTransform, "BOSS", 20, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Stretch(tt.rectTransform);
            }
            var code = UIFactory.AddText("Code", inner.transform, s.Code.Replace(Difficulties.Name(s.difficulty) + " ", ""), boss ? 34 : 38, font,
                open ? Color.white : new Color(0.5f, 0.52f, 0.58f), boss ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(code.rectTransform, 0, boss ? 30 : 0, 0, 0);
            TextOutline.Apply(code, 0.22f, new Color32(8, 10, 18, 240));

            // 별(또는 잠김) — 동그라미 아래쪽에 걸친 알약
            var pill = UIFactory.AddImage("StarPill", node, new Color(0.04f, 0.05f, 0.09f, 0.92f), ProceduralSprites.Rounded);
            pill.type = Image.Type.Sliced; pill.preserveAspect = false;
            UIFactory.Anchor(pill.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f));
            pill.rectTransform.sizeDelta = new Vector2(112, 34); pill.rectTransform.anchoredPosition = new Vector2(0, 2);
            string starText = !open ? "<color=#8A8FA0>잠김</color>"
                : $"<color=#FFD36B>{new string('★', stars)}</color><color=#4B5060>{new string('★', 3 - Mathf.Clamp(stars, 0, 3))}</color>";
            var st = UIFactory.AddText("Stars", pill.rectTransform, starText, 22, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            st.richText = true;
            UIFactory.Stretch(st.rectTransform);

            // 이름(아래)
            var nm = UIFactory.AddText("Name", node, s.stageName, 20, font, open ? Color.white : new Color(0.62f, 0.64f, 0.7f), TextAlignmentOptions.Top, FontStyles.Bold);
            UIFactory.Anchor(nm.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 1));
            nm.rectTransform.sizeDelta = new Vector2(220, 30); nm.rectTransform.anchoredPosition = new Vector2(0, -20);
            nm.textWrappingMode = TextWrappingModes.NoWrap;
            nm.enableAutoSizing = true; nm.fontSizeMin = 13; nm.fontSizeMax = 20;
            TextOutline.Apply(nm, 0.22f, new Color32(8, 10, 18, 240));

            // 진행 중 표시
            if (current)
            {
                var flag = UIFactory.AddImage("Now", node, new Color(0.25f, 0.75f, 0.95f, 1f), ProceduralSprites.Rounded);
                flag.type = Image.Type.Sliced; flag.preserveAspect = false;
                UIFactory.Anchor(flag.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0));
                flag.rectTransform.sizeDelta = new Vector2(96, 32); flag.rectTransform.anchoredPosition = new Vector2(0, boss ? 22 : 8);
                var ft = UIFactory.AddText("T", flag.rectTransform, "진행 중", 18, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Stretch(ft.rectTransform);
                flag.gameObject.AddComponent<Bob>();
            }
            else if (cleared && stars < 3)
            {
                var hint = UIFactory.AddText("Hint", node, "<color=#FFD36B>★3 도전</color>", 16, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                hint.richText = true;
                UIFactory.Anchor(hint.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0));
                hint.rectTransform.sizeDelta = new Vector2(120, 24); hint.rectTransform.anchoredPosition = new Vector2(0, boss ? 24 : 4);
                TextOutline.Apply(hint, 0.22f, new Color32(8, 10, 18, 240));
            }
        }

        /// <summary>'진행 중' 깃발이 위아래로 살짝 움직임.</summary>
        private class Bob : MonoBehaviour
        {
            private RectTransform _rt;
            private float _y;
            private void Awake() { _rt = (RectTransform)transform; _y = _rt.anchoredPosition.y; }
            private void Update() => _rt.anchoredPosition = new Vector2(_rt.anchoredPosition.x, _y + 5f * Mathf.Sin(Time.unscaledTime * 3.2f));
        }

        // ---------------- 오른쪽 상세(메인) ----------------

        /// <summary>메인 스테이지 상세를 그림 카드 · 아이콘으로. 던전이면 숨김(글 상세 사용).</summary>
        private void RefreshMainDetail(StageData s)
        {
            bool main = s != null && !s.IsDungeon;
            _mainDetView.SetActive(main);
            _detBodyView.SetActive(!main);
            if (!main) return;

            Clear(_mainDet);
            _paint = _mainDet; _paintW = 600f;
            bool open = Unlocked(s), cleared = progress != null && progress.IsCleared(s);
            int best = progress != null ? progress.StarsOf(s) : 0;
            float y = 0;

            // 한 줄 정보
            string diff = s.difficulty == StageDifficulty.Easy ? "" : $"<color={Difficulties.Hex(s.difficulty)}>{Difficulties.Name(s.difficulty)}</color> <size=85%>적 능력치 +{Mathf.RoundToInt((s.enemyStatMultiplier - 1f) * 100)}%</size>  ·  ";
            string rec = cleared ? $"최고 기록 {StageRunner.Stars(best)} <size=85%><color=#9AA3B5>({progress.ClearsOf(s)}회 클리어)</color></size>"
                : open ? "<color=#7FE3D6>아직 클리어하지 않음</color>" : "<color=#FF9C9C>이전 스테이지를 클리어하세요</color>";
            var info = UIFactory.AddText("Info", _mainDet, $"{diff}<color=#FFD36B>권장</color> Lv.{s.recommendedLevel}   ·   {rec}", 20, font, Color.white, TextAlignmentOptions.Left);
            info.richText = true;
            info.enableAutoSizing = true; info.fontSizeMin = 14; info.fontSizeMax = 20;
            info.textWrappingMode = TextWrappingModes.NoWrap;
            P(info.rectTransform, 8, y, 590, 30);
            y += 34;
            if (!string.IsNullOrEmpty(s.description))
            {
                var desc = UIFactory.AddText("Desc", _mainDet, $"<color=#AEB6C8>{s.description}</color>", 18, font, Color.white, TextAlignmentOptions.Left);
                desc.richText = true;
                P(desc.rectTransform, 8, y, 590, 26);
                y += 30;
            }
            y += 6;

            // 적 구성
            Section("적 구성", y); y += 36;
            int n = 0;
            if (s.enemies != null) foreach (var e in s.enemies) if (e != null && e.unit != null) n++;
            if (n > 0)
            {
                float cardW = Mathf.Min(150f, (594f - (n - 1) * 8f) / n);
                int k = 0;
                foreach (var e in s.enemies)
                {
                    if (e == null || e.unit == null) continue;
                    EnemyCard(e, 3 + k * (cardW + 8), y, cardW, 176);
                    k++;
                }
                y += 186;
            }

            // 별 조건(받은 별은 금색)
            Section("별 조건", y); y += 34;
            string[] cond = { "클리어", "아군 전원 생존", $"{s.StarRoundLimit}턴 이내 클리어" };
            for (int i = 0; i < 3; i++)
            {
                bool got = i < best;
                var row = UIFactory.AddText("Cond", _mainDet, $"<color={(got ? "#FFD36B" : "#4B5060")}>★</color>  <color={(got ? "#FFFFFF" : "#AEB6C8")}>{cond[i]}</color>" + (got ? "  <size=80%><color=#7CFF9A>달성</color></size>" : ""),
                    20, font, Color.white, TextAlignmentOptions.Left);
                row.richText = true;
                P(row.rectTransform, 12, y, 580, 28);
                y += 28;
            }
            y += 10;

            // 보상
            Section("보상", y); y += 36;
            var tiles = new List<(Sprite icon, string label, string count, Color tint)>();
            if (s.firstClearCrystals > 0)
                tiles.Add((GameArt.Load("Items/Crystal"), cleared ? "첫 클리어(받음)" : "첫 클리어", $"{s.firstClearCrystals}", cleared ? new Color(0.45f, 0.47f, 0.53f) : new Color(0.56f, 0.91f, 1f)));
            if (s.gold > 0) tiles.Add((GameArt.Load("Items/Gold"), "금화", $"{s.gold:N0}", new Color(1f, 0.83f, 0.42f)));
            if (s.rewards != null)
                foreach (var m in s.rewards)
                    if (m != null && m.count > 0) tiles.Add((GameArt.MaterialIcon(m.type), Materials.Name(m.type), $"×{m.count}", Grades.ColorOf(Materials.Grade(m.type))));
            float tw = Mathf.Min(116f, (594f - (tiles.Count - 1) * 8f) / Mathf.Max(1, tiles.Count));
            for (int i = 0; i < tiles.Count; i++) RewardTile(tiles[i].icon, tiles[i].label, tiles[i].count, tiles[i].tint, 3 + i * (tw + 8), y, tw);
            y += 130;
            if (s.characterExp > 0)
            {
                var ex = UIFactory.AddText("CharExp", _mainDet, $"<color=#9AA3B5>출전 캐릭터마다 경험치</color> +{s.characterExp:N0}", 18, font, Color.white, TextAlignmentOptions.Left);
                ex.richText = true;
                P(ex.rectTransform, 10, y, 580, 26);
                y += 32;
            }

            // 클리어 시 개방(특수 던전)
            var opens = database.DungeonsUnlockedBy(s);
            if (opens.Count > 0)
            {
                Section("클리어 시 개방", y); y += 34;
                var shown = new HashSet<StageKind>();
                foreach (var d in opens)
                {
                    if (!shown.Add(d.kind)) continue;
                    string what = d.IsAscensionDungeon ? $"{Materials.TierName(d.dungeonTier)} <size=85%>({Materials.TierTargetLevel(d.dungeonTier)} 돌파 재료 · 전 속성)</size>" : d.Code;
                    var row = UIFactory.AddText("Open", _mainDet, $"·  <color={SpecialDungeons.Hex(d.kind)}>{SpecialDungeons.Name(d.kind)}</color> {what}", 19, font, Color.white, TextAlignmentOptions.Left);
                    row.richText = true;
                    P(row.rectTransform, 12, y, 580, 28);
                    y += 28;
                }
                y += 8;
            }

            _mainDet.GetComponent<LayoutElement>().preferredHeight = y + 8;
            var scroll = _mainDetView.GetComponent<ScrollRect>();
            if (scroll != null && _detShown != s) scroll.verticalNormalizedPosition = 1f;
        }
    }
}
