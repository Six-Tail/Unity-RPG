using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SubcultureRPG.UI
{
    /// <summary>
    /// 코드로 uGUI 요소를 빠르게 만드는 공용 헬퍼(프리팹 없이 UI 구성).
    /// 메서드는 타입명(Image/Text/Button)과 겹치지 않도록 Add* 로 둡니다.
    /// </summary>
    public static class UIFactory
    {
        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        public static Image AddImage(string name, Transform parent, Color color, Sprite sprite = null, bool raycast = false)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.raycastTarget = raycast;
            if (sprite != null) img.preserveAspect = true;
            return img;
        }

        public static TextMeshProUGUI AddText(string name, Transform parent, string content, float size,
            TMP_FontAsset font, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Truncate;   // 폰트에 말줄임표(…) 글리프가 없어 Ellipsis는 경고만 남김
            return t;
        }

        public static Button AddButton(string name, Transform parent, string label, float fontSize,
            TMP_FontAsset font, Color bg, Color fg)
        {
            // 둥근 그라데이션 버튼(9-slice) + 그림자 + 눌림 효과. 색은 bg로 입힘(흰 스프라이트 × 색).
            var img = AddImage(name, parent, bg, null, raycast: true);
            img.sprite = ProceduralSprites.Button;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1.5f;
            img.preserveAspect = false;
            var shadow = img.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -3f);

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.58f, 0.55f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            img.gameObject.AddComponent<ButtonFx>();

            var txt = AddText("Label", img.transform, label, fontSize, font, fg, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(txt.rectTransform, 8, 2, 8, 2);
            return btn;
        }

        // ---- 앵커/크기 헬퍼 ----

        public static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static RectTransform Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            return rt;
        }

        public static RectTransform Size(RectTransform rt, float w, float h)
        {
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }
    }
}
