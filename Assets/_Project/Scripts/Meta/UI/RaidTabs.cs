using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 레이드 화면 위쪽 가운데의 큰 탭 2개: [주간 보스] [월드 레이드]. 주간 보스 화면 · 월드 레이드 화면이 같은 자리에 같은 모양으로 그려
    /// 탭을 바꾸는 것처럼 보이게 함. 탭마다 아이콘 · 이름 · 한 줄 상태(남은 도전 · 공유 체력) · 받을 보상 느낌표.
    /// </summary>
    public sealed class RaidTabs
    {
        public const float TabW = 380f, TabH = 84f, Top = -8f;   // 아래(-92)와 오른쪽 내용(-122부터) 사이 30 띄움

        private readonly Image[] _bg = new Image[2];
        private readonly TextMeshProUGUI[] _sub = new TextMeshProUGUI[2];
        private readonly NotifyBadge[] _badge = new NotifyBadge[2];
        private readonly bool _weekly;

        private static readonly Color WeeklyCol = new(0.62f, 0.4f, 0.95f), WorldCol = new(0.92f, 0.38f, 0.3f);

        private RaidTabs(bool weekly) { _weekly = weekly; }

        /// <param name="weekly">지금 화면이 주간 보스인가</param>
        /// <param name="onOther">다른 탭을 눌렀을 때(다른 화면으로 이동)</param>
        public static RaidTabs Build(RectTransform frame, TMP_FontAsset font, bool weekly, System.Action onOther)
        {
            var t = new RaidTabs(weekly);
            for (int i = 0; i < 2; i++)
            {
                bool isWeekly = i == 0, on = isWeekly == weekly;
                var col = isWeekly ? WeeklyCol : WorldCol;
                var bg = UIFactory.AddImage(isWeekly ? "WeeklyTab" : "WorldTab", frame, Color.white, ProceduralSprites.Rounded, raycast: true);
                bg.type = Image.Type.Sliced; bg.preserveAspect = false;
                var rt = bg.rectTransform;
                UIFactory.Anchor(rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
                rt.sizeDelta = new Vector2(TabW, on ? TabH : TabH - 8); rt.anchoredPosition = new Vector2((i == 0 ? -1 : 1) * (TabW * 0.5f + 8f), Top - (on ? 0 : 4));
                bg.color = on ? new Color(col.r * 0.62f, col.g * 0.62f, col.b * 0.62f, 0.97f) : new Color(0.08f, 0.09f, 0.13f, 0.88f);
                if (!on)
                {
                    var b = bg.gameObject.AddComponent<Button>();
                    b.targetGraphic = bg;
                    b.onClick.AddListener(() => onOther?.Invoke());
                    bg.gameObject.AddComponent<ButtonFx>();
                }
                // 고른 탭: 밝은 테두리 + 아래 밑줄
                var rim = UIFactory.AddImage("Rim", rt, on ? new Color(col.r, col.g, col.b, 1f) : new Color(col.r, col.g, col.b, 0.45f), ProceduralSprites.Rounded);
                rim.type = Image.Type.Sliced; rim.preserveAspect = false; rim.raycastTarget = false;
                UIFactory.Stretch(rim.rectTransform, -3, -3, -3, -3);
                rim.transform.SetAsFirstSibling();
                if (on)
                {
                    var bar = UIFactory.AddImage("Underline", rt, new Color(1f, 0.92f, 0.65f, 1f), ProceduralSprites.Rounded);
                    bar.type = Image.Type.Sliced; bar.preserveAspect = false;
                    UIFactory.Anchor(bar.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f));
                    bar.rectTransform.sizeDelta = new Vector2(TabW * 0.5f, 6); bar.rectTransform.anchoredPosition = new Vector2(0, -9);
                }

                var icon = isWeekly ? GameArt.Load("Items/RaidIcon") : GameArt.Enemy(ServerRaid.BossName);
                float textLeft = 22;
                if (icon != null)
                {
                    var im = UIFactory.AddImage("Icon", rt, on ? Color.white : new Color(0.65f, 0.65f, 0.7f), icon);
                    im.preserveAspect = true; im.raycastTarget = false;
                    UIFactory.Anchor(im.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                    im.rectTransform.sizeDelta = new Vector2(62, 62); im.rectTransform.anchoredPosition = new Vector2(12, 0);
                    textLeft = 84;
                }
                // 위 칸 = 이름, 아래 칸 = 한 줄 상태(서로 겹치지 않게 칸을 나눔)
                var name = UIFactory.AddText("Name", rt, isWeekly ? "주간 보스" : "월드 레이드", on ? 30 : 26, font,
                    on ? Color.white : new Color(0.72f, 0.75f, 0.82f), TextAlignmentOptions.BottomLeft, FontStyles.Bold);
                name.textWrappingMode = TextWrappingModes.NoWrap;
                UIFactory.Anchor(name.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0, 0));
                name.rectTransform.offsetMin = new Vector2(textLeft, 0); name.rectTransform.offsetMax = new Vector2(-14, -4);
                TextOutline.Apply(name, 0.18f, new Color32(8, 10, 18, 230));
                var sub = UIFactory.AddText("Sub", rt, "", 18, font, on ? new Color(0.95f, 0.93f, 1f) : new Color(0.6f, 0.63f, 0.7f), TextAlignmentOptions.TopLeft);
                sub.richText = true;
                sub.textWrappingMode = TextWrappingModes.NoWrap;
                sub.enableAutoSizing = true; sub.fontSizeMin = 12; sub.fontSizeMax = 18;
                UIFactory.Anchor(sub.rectTransform, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(0, 0));
                sub.rectTransform.offsetMin = new Vector2(textLeft, 4); sub.rectTransform.offsetMax = new Vector2(-14, -2);
                t._bg[i] = bg; t._sub[i] = sub;
                t._badge[i] = NotifyBadge.Attach(rt, font, 8, 8, 30);
            }
            t.Refresh();
            return t;
        }

        /// <summary>한 줄 상태 · 느낌표 갱신.</summary>
        public void Refresh()
        {
            var w = WorldRaid.Instance;
            var t = WorldRaid.UntilWeekReset;
            _sub[0].text = $"오늘 <color=#7CFF9A>{w.AttemptsLeft}</color>/{WorldRaid.AttemptsPerDay} · 교체까지 {(t.Days > 0 ? $"{t.Days}일" : $"{t.Hours}시간")}";
            _badge[0].Set(w.ClaimableCount > 0);
            var s = ServerRaid.Instance;
            _sub[1].text = ServerRaid.Online
                ? $"공유 체력 <color=#FF9C8A>{s.HpRatio * 100f:0}%</color> · 오늘 <color=#7CFF9A>{s.AttemptsLeft}</color>/{ServerRaid.AttemptsPerDay}"
                : $"공유 체력 <color=#FF9C8A>{s.HpRatio * 100f:0}%</color> · 로그인하면 도전";
            _badge[1].Set(s.ClaimableCount > 0);
        }
    }
}
