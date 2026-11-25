using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.Characters;
using SubcultureRPG.UI;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 옷장: 캐릭터의 의상(기본 · 개화 · 추후 스킨)을 골라 입힘. 육성 화면 상세 오른쪽 위 '옷장'에서 엶.
    /// 잠긴 의상은 어둡게 + 해금 조건. 의상 일러스트가 아직 없으면 '준비 중'.
    /// </summary>
    public static class WardrobeWindow
    {
        private const float CardW = 340, CardH = 600;

        public static void Open(CharacterData c, TMP_FontAsset font = null)
        {
            if (c == null) return;
            var roster = Object.FindAnyObjectByType<PlayerRoster>();
            string key = PlayerRoster.KeyOf(c);
            var w = OverlayWindow.Open($"옷장 · {c.displayName}", 1200, 800, font);
            var body = UIFactory.NewRect("Body", w.Panel);
            UIFactory.Stretch(body);

            void Rebuild()
            {
                if (!w.IsOpen) return;
                OverlayWindow.Clear(body);
                int level = roster != null ? roster.LevelOf(c) : 1;
                var cur = CharacterOutfits.Current(c);
                float x = 40;
                foreach (var o in CharacterOutfits.All)
                {
                    bool unlocked = CharacterOutfits.Unlocked(c, o, level);
                    bool hasArt = CharacterOutfits.HasArt(c, o);
                    bool wearing = cur.id == o.id;
                    Color frame = wearing ? new Color(1f, 0.82f, 0.35f, 1f) : o.id == CharacterOutfits.BloomId ? new Color(0.78f, 0.6f, 1f, 0.9f) : new Color(1, 1, 1, 0.25f);
                    var rim = w.Box(body, frame, x - 3, -87, CardW + 6, CardH + 6);
                    var card = w.Box(body, new Color(0.06f, 0.07f, 0.1f, 1f), x, -90, CardW, CardH);

                    // 그림(3:4): 그 의상 그림 → 없으면 기본
                    var sp = GameArt.Load("Characters/" + key + o.suffix);
                    if (sp == null) sp = GameArt.Load("Characters/" + key);
                    var art = UIFactory.AddImage("Art", card.transform, unlocked ? Color.white : new Color(0.18f, 0.18f, 0.24f, 1f), sp);
                    art.preserveAspect = true;
                    OverlayWindow.Place(art.rectTransform, 10, -10, CardW - 20, (CardW - 20) * 4f / 3f);
                    art.enabled = sp != null;

                    var title = w.Text(card.transform, $"<b>{o.name}</b>", 30, Color.white, TextAlignmentOptions.Center, 0, -462, CardW, 40);
                    if (o.id == CharacterOutfits.BloomId && unlocked) title.gameObject.AddComponent<RainbowText>();

                    string state;
                    if (!unlocked) state = $"<color=#9AA3B5>잠김 · {o.unlockText}</color>";
                    else if (!hasArt) state = "<color=#9AA3B5>일러스트 준비 중</color>";
                    else state = wearing ? "<color=#FFD36B>착용 중</color>" : "<color=#7BE39A>입을 수 있음</color>";
                    w.Text(card.transform, state, 18, Color.white, TextAlignmentOptions.Center, 10, -504, CardW - 20, 28).textWrappingMode = TextWrappingModes.Normal;

                    if (!unlocked)
                    {
                        var lockT = w.Text(card.transform, "<b>잠김</b>", 40, new Color(0.85f, 0.88f, 0.95f, 0.9f), TextAlignmentOptions.Center, 10, -200, CardW - 20, 60);
                        lockT.characterSpacing = 8f;
                    }
                    var oo = o;
                    w.Btn(card.transform, wearing ? "착용 중" : "착용", 24, wearing ? OverlayWindow.Gold : OverlayWindow.Blue, 40, -538, CardW - 80, 52, () =>
                    {
                        CharacterOutfits.Set(c, oo.id);
                        SoundManager.Play(Sfx.Confirm);
                        Rebuild();
                    }, unlocked && hasArt && !wearing);
                    x += CardW + 30;
                }

                // 추후 스킨 자리
                var more = w.Box(body, new Color(1, 1, 1, 0.04f), x, -90, CardW, CardH);
                w.Text(more.transform, "<color=#6B7280><b>?</b></color>", 90, Color.white, TextAlignmentOptions.Center, 0, -200, CardW, 120);
                w.Text(more.transform, "<color=#6B7280>새 의상 준비 중\n<size=80%>추후 스킨이 이곳에 추가됩니다</size></color>", 22, Color.white,
                    TextAlignmentOptions.Center, 10, -380, CardW - 20, 80).textWrappingMode = TextWrappingModes.Normal;

                w.Text(body, "<color=#9AA3B5>고른 의상은 일러스트 · 썸네일 · 전투 그림 · 움직이는 일러스트에 모두 적용됩니다.</color>", 18, Color.white,
                    TextAlignmentOptions.Left, 40, -716, 1100, 30);
            }

            Rebuild();
        }
    }
}
