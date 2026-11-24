using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;

namespace SubcultureRPG.Battle.UI
{
    /// <summary>
    /// 결과 화면의 '전투 기록' 창: 아군·적 유닛별 준 피해(막대) · 받은 피해 · 회복량 · 치명 · 브레이크 · 격파.
    /// 아군 중 준 피해가 가장 큰 유닛에 MVP 표시. 바깥(어두운 배경)이나 닫기 버튼을 누르면 닫힘.
    /// </summary>
    public class BattleStatsPanel
    {
        private const float WinW = 1560f, WinH = 900f, Pad = 40f;
        private const float TitleH = 96f, FooterH = 96f, SectionH = 40f, HeaderH = 30f, MaxRowH = 62f;

        // 열: (이름, x, 너비) — 창 안쪽 왼쪽 기준
        private const float XPortrait = 0, XName = 70, XDealt = 300, WDealtBar = 300, XTaken = 790, XHeal = 970,
            XCrit = 1150, XBreak = 1260, XKill = 1380, ColEnd = 1480;

        private readonly TMP_FontAsset _font;
        private readonly RectTransform _root, _content;
        private readonly TextMeshProUGUI _summary;

        public bool IsOpen => _root.gameObject.activeSelf;

        public BattleStatsPanel(Transform parent, TMP_FontAsset font)
        {
            _font = font;

            // 어두운 배경(누르면 닫힘)
            _root = UIFactory.NewRect("BattleStats", parent);
            UIFactory.Stretch(_root);
            var dim = UIFactory.AddImage("Dim", _root, new Color(0, 0, 0, 0.7f), null, raycast: true);
            UIFactory.Stretch(dim.rectTransform);
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Hide);

            // 창(배경과 형제 → 창을 눌러도 닫히지 않음)
            var win = Rounded("Window", _root, new Color(0.09f, 0.10f, 0.15f, 0.98f), true);
            var wrt = win.rectTransform;
            UIFactory.Anchor(wrt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            wrt.sizeDelta = new Vector2(WinW, WinH);
            var rim = Rounded("Rim", wrt, new Color(1f, 0.85f, 0.45f, 0.35f));
            UIFactory.Stretch(rim.rectTransform, -2, -2, -2, -2);
            rim.transform.SetAsFirstSibling();

            var title = UIFactory.AddText("Title", wrt, "전투 기록", 40, font, new Color(1f, 0.88f, 0.55f), TextAlignmentOptions.Left, FontStyles.Bold);
            var trt = title.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            trt.sizeDelta = new Vector2(400, 60); trt.anchoredPosition = new Vector2(Pad, -22);

            _summary = UIFactory.AddText("Summary", wrt, "", 22, font, new Color(0.82f, 0.85f, 0.92f), TextAlignmentOptions.Right);
            _summary.richText = true;
            var srt = _summary.rectTransform;
            UIFactory.Anchor(srt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            srt.sizeDelta = new Vector2(1000, 60); srt.anchoredPosition = new Vector2(-Pad, -22);

            _content = UIFactory.NewRect("Content", wrt);
            UIFactory.Stretch(_content, Pad, FooterH, Pad, TitleH);

            var close = UIFactory.AddButton("Close", wrt, "닫기", 26, font, new Color(1, 1, 1, 0.16f), Color.white);
            var crt = (RectTransform)close.transform;
            UIFactory.Anchor(crt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            crt.sizeDelta = new Vector2(220, 62); crt.anchoredPosition = new Vector2(0, 20);
            close.onClick.AddListener(Hide);

            _root.gameObject.SetActive(false);
        }

        public void Hide() => _root.gameObject.SetActive(false);

        public void Show(BattleEngine engine)
        {
            if (engine == null) return;
            Clear(_content);
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();

            long maxDealt = 1, allyDealt = 0, allyHeal = 0, enemyDealt = 0;
            int actions = 0;
            BattleUnit mvp = null;
            foreach (var u in engine.allies)
            {
                maxDealt = System.Math.Max(maxDealt, u.record.dealt);
                allyDealt += u.record.dealt; allyHeal += u.record.healed; actions += u.record.actions;
                if (u.record.dealt > 0 && (mvp == null || u.record.dealt > mvp.record.dealt)) mvp = u;
            }
            foreach (var u in engine.enemies)
            {
                maxDealt = System.Math.Max(maxDealt, u.record.dealt);
                enemyDealt += u.record.dealt; actions += u.record.actions;
            }
            _summary.text = $"총 행동 <b>{actions}</b>회   ·   아군 피해 <color=#8FC8FF><b>{allyDealt:N0}</b></color>   ·   " +
                            $"아군 회복 <color=#7FE39A><b>{allyHeal:N0}</b></color>   ·   적 피해 <color=#FF8F8F><b>{enemyDealt:N0}</b></color>";

            int rows = engine.allies.Count + engine.enemies.Count;
            float avail = WinH - TitleH - FooterH - 2 * (SectionH + HeaderH) - 16f;
            float rowH = Mathf.Clamp(avail / Mathf.Max(1, rows), 34f, MaxRowH);

            float y = 0f;
            y = Section(y, "아군", new Color(0.45f, 0.72f, 1f), allyDealt, engine.allies, rowH, maxDealt, mvp, true);
            y -= 16f;
            Section(y, "적", new Color(1f, 0.5f, 0.5f), enemyDealt, engine.enemies, rowH, maxDealt, null, false);
        }

        // ---------------- 구성 ----------------

        private float Section(float y, string label, Color color, long total, List<BattleUnit> units, float rowH,
            long maxDealt, BattleUnit mvp, bool ally)
        {
            var bar = UIFactory.AddImage("Accent", _content, color);
            Place(bar.rectTransform, 0, y - 8, 6, SectionH - 14);
            var t = Text(label, 26, color, FontStyles.Bold, TextAlignmentOptions.Left);
            Place(t.rectTransform, 16, y, 300, SectionH);
            var tt = Text($"합계 피해 {total:N0}", 20, new Color(0.75f, 0.78f, 0.86f), FontStyles.Normal, TextAlignmentOptions.Right);
            Place(tt.rectTransform, ColEnd - 400, y, 400, SectionH);
            y -= SectionH;

            var hc = new Color(0.6f, 0.63f, 0.72f);
            Header("이름", XName, 220, y, hc, TextAlignmentOptions.Left);
            Header("준 피해", XDealt, WDealtBar + 170, y, hc, TextAlignmentOptions.Left);
            Header("받은 피해", XTaken, 160, y, hc, TextAlignmentOptions.Right);
            Header("회복량", XHeal, 160, y, hc, TextAlignmentOptions.Right);
            Header("치명", XCrit, 90, y, hc, TextAlignmentOptions.Right);
            Header("브레이크", XBreak, 100, y, hc, TextAlignmentOptions.Right);
            Header("격파", XKill, 90, y, hc, TextAlignmentOptions.Right);
            y -= HeaderH;

            for (int i = 0; i < units.Count; i++)
            {
                Row(units[i], y, rowH, i, maxDealt, units[i] == mvp, ally ? new Color(0.4f, 0.68f, 1f) : new Color(1f, 0.42f, 0.42f));
                y -= rowH;
            }
            return y;
        }

        private void Header(string s, float x, float w, float y, Color c, TextAlignmentOptions align)
        {
            var t = Text(s, 17, c, FontStyles.Bold, align);
            Place(t.rectTransform, x, y, w, HeaderH);
        }

        private void Row(BattleUnit u, float y, float h, int index, long maxDealt, bool mvp, Color barColor)
        {
            var r = u.record;
            bool alive = u.IsAlive;

            var bg = Rounded("Row", _content, new Color(1, 1, 1, index % 2 == 0 ? 0.05f : 0.025f));
            Place(bg.rectTransform, -10, y - 2, ColEnd + 20, h - 4);

            // 초상
            float ps = h - 10;
            var frame = Rounded("Frame", _content, new Color(1, 1, 1, 0.08f));
            Place(frame.rectTransform, XPortrait, y - 5, ps, ps);
            Sprite sprite = u.character != null ? GameArt.Thumb(u.character) : GameArt.Enemy(u.Name);
            if (sprite != null)
            {
                var img = UIFactory.AddImage("Portrait", frame.transform, alive ? Color.white : new Color(0.5f, 0.5f, 0.5f), sprite);
                img.preserveAspect = true;
                UIFactory.Stretch(img.rectTransform, 2, 2, 2, 2);
            }

            // 이름(+MVP / 쓰러짐)
            string name = u.Name;
            if (!alive) name += "  <size=70%><color=#FF8A8A>쓰러짐</color></size>";
            var nt = Text(name, 21, alive ? Color.white : new Color(0.65f, 0.67f, 0.72f), FontStyles.Bold, TextAlignmentOptions.Left);
            nt.richText = true;
            Place(nt.rectTransform, XName, y, mvp ? 160 : 220, h - 4);
            if (mvp)
            {
                var badge = Rounded("MVP", _content, new Color(1f, 0.78f, 0.25f));
                Place(badge.rectTransform, XName + 160, y - (h - 30) / 2f - 2, 56, 26);
                var bt = UIFactory.AddText("T", badge.transform, "MVP", 16, _font, new Color(0.18f, 0.12f, 0.02f), TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Stretch(bt.rectTransform);
            }

            // 준 피해 막대 + 숫자
            float bh = Mathf.Min(18f, h * 0.34f);
            var barBg = UIFactory.AddImage("BarBG", _content, new Color(0, 0, 0, 0.45f));
            Place(barBg.rectTransform, XDealt, y - (h - 4 - bh) / 2f, WDealtBar, bh);
            float k = Mathf.Clamp01((float)r.dealt / maxDealt);
            if (k > 0f)
            {
                var fill = UIFactory.AddImage("Fill", _content, mvp ? new Color(1f, 0.8f, 0.35f) : barColor);
                Place(fill.rectTransform, XDealt, y - (h - 4 - bh) / 2f, Mathf.Max(3f, WDealtBar * k), bh);
            }
            Num(r.dealt, XDealt + WDealtBar + 10, 160, y, h, Color.white, TextAlignmentOptions.Left);

            Num(r.taken, XTaken, 160, y, h, new Color(1f, 0.72f, 0.72f), TextAlignmentOptions.Right);
            Num(r.healed, XHeal, 160, y, h, new Color(0.55f, 0.92f, 0.65f), TextAlignmentOptions.Right);
            Num(r.crits, XCrit, 90, y, h, new Color(1f, 0.85f, 0.45f), TextAlignmentOptions.Right);
            Num(r.breaks, XBreak, 100, y, h, new Color(1f, 0.75f, 0.4f), TextAlignmentOptions.Right);
            Num(r.kills, XKill, 90, y, h, Color.white, TextAlignmentOptions.Right);
        }

        private void Num(long v, float x, float w, float y, float h, Color c, TextAlignmentOptions align)
        {
            var t = Text(v > 0 ? v.ToString("N0") : "-", 21, v > 0 ? c : new Color(0.45f, 0.47f, 0.53f), FontStyles.Bold, align);
            Place(t.rectTransform, x, y, w, h - 4);
        }

        // ---------------- 유틸 ----------------

        private TextMeshProUGUI Text(string s, float size, Color c, FontStyles style, TextAlignmentOptions align)
        {
            var t = UIFactory.AddText("T", _content, s, size, _font, c, align, style);
            t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = Mathf.Max(10, size * 0.6f);
            return t;
        }

        /// <summary>콘텐츠 왼쪽 위 기준 배치(y는 위에서 아래로 음수).</summary>
        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            UIFactory.Anchor(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
        }

        private static Image Rounded(string name, Transform parent, Color c, bool raycast = false)
        {
            var img = UIFactory.AddImage(name, parent, c, ProceduralSprites.Rounded, raycast);
            img.type = Image.Type.Sliced; img.preserveAspect = false; img.pixelsPerUnitMultiplier = 2.2f;
            return img;
        }

        private static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var ch = t.GetChild(i);
                ch.SetParent(null, false);
                Object.Destroy(ch.gameObject);
            }
        }
    }
}
