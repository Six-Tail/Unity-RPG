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

        /// <summary>
        /// 가운데 고정 폭 프레임(세로는 부모에 맞춤). 좌우 끝에 붙는 화면(목록 왼쪽 · 상세 오른쪽)을
        /// 넓은 화면에서도 1920 폭 설계 그대로 가운데에 모아 줍니다. 배경·전체 화면 덮개는 프레임 밖(부모)에 둡니다.
        /// </summary>
        public static RectTransform Frame(Transform parent, float width = 1920f)
        {
            var rt = NewRect("Frame", parent);
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, 0f); rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        /// <summary>
        /// view 안에서 content를 세로로 스크롤(내용이 view보다 길 때만 움직임).
        /// content는 위쪽에 붙어 폭을 채우고, 높이는 내용(레이아웃 그룹·텍스트)에 맞춰 늘어납니다.
        /// </summary>
        public static ScrollRect MakeVerticalScroll(RectTransform view, RectTransform content)
        {
            if (view.GetComponent<Graphic>() == null)
            {
                var hit = view.gameObject.AddComponent<Image>();   // 빈 곳을 잡고 끌 수 있도록
                hit.color = new Color(0, 0, 0, 0);
            }
            if (view.GetComponent<RectMask2D>() == null) view.gameObject.AddComponent<RectMask2D>();
            content.SetParent(view, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero; content.anchoredPosition = Vector2.zero;
            var fit = content.GetComponent<ContentSizeFitter>();
            if (fit == null) fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content; scroll.viewport = view;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return scroll;
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
