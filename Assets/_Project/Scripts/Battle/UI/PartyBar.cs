using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.Characters;
using SubcultureRPG.UI;

namespace SubcultureRPG.Battle.UI
{
    /// <summary>
    /// 전투 화면 아래 왼쪽 아군 파티 바: 캐릭터마다 초상 · 이름 · 체력 · 에너지(궁극기 준비) 카드.
    /// 차례인 캐릭터는 금색 테두리, 쓰러지면 어둡게. 누르면 onClick(유닛) — 아군 대상 스킬 고르기에도 씀.
    /// 값은 매 프레임 유닛에서 직접 읽어 갱신합니다(이벤트 연결 없음).
    /// </summary>
    public class PartyBar : MonoBehaviour
    {
        public const float CardW = 226f, CardH = 112f, Gap = 10f;

        private class Card
        {
            public BattleUnit unit;
            public RectTransform rt;
            public Image border, hpFill, enFill, dim, glow;
            public TextMeshProUGUI hpText, ready;
            public float hpShown = 1f;
        }

        private readonly List<Card> _cards = new();
        private RectTransform _rt;
        private TMP_FontAsset _font;
        private Action<BattleUnit> _onClick;
        private BattleUnit _current;

        public static PartyBar Create(Transform parent, TMP_FontAsset font, Action<BattleUnit> onClick)
        {
            var rt = UIFactory.NewRect("PartyBar", parent);
            UIFactory.Anchor(rt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            rt.sizeDelta = new Vector2(4 * (CardW + Gap), CardH + 20);
            rt.anchoredPosition = new Vector2(24, 16);
            var bar = rt.gameObject.AddComponent<PartyBar>();
            bar._rt = rt; bar._font = font; bar._onClick = onClick;
            return bar;
        }

        public void SetCurrent(BattleUnit u) => _current = u;

        public void Bind(IList<BattleUnit> allies)
        {
            for (int i = _rt.childCount - 1; i >= 0; i--) Destroy(_rt.GetChild(i).gameObject);
            _cards.Clear();
            _current = null;
            if (allies == null) return;
            for (int i = 0; i < allies.Count; i++) _cards.Add(BuildCard(allies[i], i));
        }

        private Image Rounded(string name, Transform parent, Color c, bool raycast = false)
        {
            var img = UIFactory.AddImage(name, parent, c, ProceduralSprites.Rounded, raycast);
            img.type = Image.Type.Sliced; img.preserveAspect = false; img.pixelsPerUnitMultiplier = 2.2f;
            return img;
        }

        private static RectTransform Bar(Transform parent, float y, float h, Color bg, out Image fill, Color fc)
        {
            var b = UIFactory.NewRect("Bar", parent);
            Row(b, y, h, -12);
            var bgi = UIFactory.AddImage("BG", b, bg);
            UIFactory.Stretch(bgi.rectTransform);
            fill = UIFactory.AddImage("Fill", b, fc);
            UIFactory.Stretch(fill.rectTransform);
            return b;
        }

        /// <summary>카드 위쪽 기준 한 줄(왼쪽 110 ~ 오른쪽 right, 위에서 -top부터 높이 h).</summary>
        private static void Row(RectTransform rt, float top, float h, float right)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1);
            rt.offsetMin = new Vector2(110, top - h); rt.offsetMax = new Vector2(right, top);
        }

        private Card BuildCard(BattleUnit u, int index)
        {
            var c = new Card { unit = u };
            var glow = UIFactory.AddImage("Glow", _rt, new Color(1f, 0.8f, 0.3f, 0f), ProceduralSprites.Glow);
            glow.preserveAspect = false;
            UIFactory.Anchor(glow.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0.5f));
            glow.rectTransform.sizeDelta = new Vector2(CardW + 70, CardH + 70);
            glow.rectTransform.anchoredPosition = new Vector2(index * (CardW + Gap) + CardW / 2f, CardH / 2f);
            c.glow = glow;

            c.border = Rounded("Card", _rt, new Color(0.35f, 0.4f, 0.55f, 0.9f), raycast: true);
            c.rt = c.border.rectTransform;
            UIFactory.Anchor(c.rt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            c.rt.sizeDelta = new Vector2(CardW, CardH);
            c.rt.anchoredPosition = new Vector2(index * (CardW + Gap), 0);
            var btn = c.border.gameObject.AddComponent<Button>();
            btn.targetGraphic = c.border;
            btn.onClick.AddListener(() => _onClick?.Invoke(u));
            c.border.gameObject.AddComponent<ButtonFx>();

            var inner = Rounded("Inner", c.rt, new Color(0.06f, 0.07f, 0.11f, 0.96f));
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);

            // 초상
            var frame = Rounded("Face", c.rt, new Color(1, 1, 1, 0.08f));
            UIFactory.Anchor(frame.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            frame.rectTransform.sizeDelta = new Vector2(92, 92); frame.rectTransform.anchoredPosition = new Vector2(10, 0);
            var sp = u.character != null ? GameArt.Thumb(u.character) : null;
            if (sp != null)
            {
                var face = UIFactory.AddImage("Thumb", frame.transform, Color.white, sp);
                face.preserveAspect = true;
                UIFactory.Stretch(face.rectTransform, 2, 2, 2, 2);
            }
            // 속성 띠(초상 아래)
            var el = UIFactory.AddImage("Element", frame.transform, Traits.ColorOf(u.element));
            UIFactory.Anchor(el.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            el.rectTransform.sizeDelta = new Vector2(-4, 5); el.rectTransform.anchoredPosition = new Vector2(0, 2);

            var name = UIFactory.AddText("Name", c.rt, u.Name, 20, _font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Row(name.rectTransform, -10, 28, -8);
            name.enableAutoSizing = true; name.fontSizeMin = 12; name.fontSizeMax = 20;

            Bar(c.rt, -44, 14, new Color(0, 0, 0, 0.6f), out c.hpFill, new Color(0.3f, 0.85f, 0.4f));
            c.hpText = UIFactory.AddText("HP", c.rt, "", 13, _font, new Color(0.85f, 0.88f, 0.94f), TextAlignmentOptions.Left);
            Row(c.hpText.rectTransform, -60, 18, -8);
            Bar(c.rt, -82, 8, new Color(0, 0, 0, 0.6f), out c.enFill, new Color(0.35f, 0.75f, 1f));
            c.ready = UIFactory.AddText("Ready", c.rt, "궁극기 준비", 13, _font, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Left, FontStyles.Bold);
            Row(c.ready.rectTransform, -92, 18, -8);

            c.dim = Rounded("Dead", c.rt, new Color(0, 0, 0, 0.62f));
            UIFactory.Stretch(c.dim.rectTransform);
            var dt = UIFactory.AddText("T", c.dim.transform, "전투 불능", 18, _font, new Color(1f, 0.55f, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(dt.rectTransform);
            c.dim.gameObject.SetActive(false);
            c.hpShown = u.HPRatio;
            return c;
        }

        private static void SetBar(Image fill, float r)
        {
            var rt = fill.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(Mathf.Clamp01(r), 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            foreach (var c in _cards)
            {
                var u = c.unit;
                if (u == null || c.rt == null) continue;
                c.hpShown = Mathf.MoveTowards(c.hpShown, u.HPRatio, Time.unscaledDeltaTime * 2.5f);
                SetBar(c.hpFill, c.hpShown);
                c.hpFill.color = c.hpShown > 0.5f ? new Color(0.3f, 0.85f, 0.4f) : c.hpShown > 0.25f ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.95f, 0.35f, 0.35f);
                c.hpText.text = $"{u.currentHP:N0} / {u.maxHP:N0}";
                SetBar(c.enFill, u.EnergyRatio);
                bool ready = u.IsAlive && u.UltimateReady;
                c.enFill.color = ready ? new Color(1f, 0.85f, 0.35f) : new Color(0.35f, 0.75f, 1f);
                c.ready.gameObject.SetActive(ready);
                if (ready) c.ready.alpha = 0.7f + 0.3f * Mathf.Sin(t * 5f);
                bool cur = u == _current && u.IsAlive;
                c.border.color = cur ? new Color(1f, 0.82f, 0.35f, 1f) : ready ? new Color(0.85f, 0.65f, 0.3f, 0.9f) : new Color(0.35f, 0.4f, 0.55f, 0.9f);
                c.glow.color = new Color(1f, 0.8f, 0.3f, cur ? 0.45f : 0f);
                c.rt.anchoredPosition = new Vector2(c.rt.anchoredPosition.x, cur ? 10f : 0f);
                c.dim.gameObject.SetActive(!u.IsAlive);
            }
        }
    }
}

namespace SubcultureRPG.Battle.UI
{
    /// <summary>행동 버튼에 손(마우스)을 올리면 설명을 보여 주기 위한 작은 도우미.</summary>
    public class SkillHover : UnityEngine.MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public System.Action onEnter, onExit;
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) => onEnter?.Invoke();
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => onExit?.Invoke();
    }
}
