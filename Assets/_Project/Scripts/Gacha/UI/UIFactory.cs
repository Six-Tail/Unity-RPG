using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SubcultureRPG.Gacha.UI
{
    /// <summary>
    /// 코드로 uGUI 요소를 빠르게 만드는 내부 헬퍼. (프리팹 없이 UI 구성)
    /// 메서드 이름은 타입명(Image/Text/Button)과 겹치지 않도록 Add* 로 둡니다.
    /// </summary>
    internal static class UIFactory
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
            var img = AddImage(name, parent, bg, null, raycast: true);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var txt = AddText("Label", img.transform, label, fontSize, font, fg, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(txt.rectTransform);
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
    }
}
