using TMPro;
using UnityEngine;

namespace SubcultureRPG.UI
{
    /// <summary>
    /// TMP 글자 외곽선을 안전하게 넣기. 화면이 꺼진 상태(비활성)에서 막 만든 글자는 내부 재질이 아직 없어
    /// outlineWidth를 바로 바꾸면 NullReferenceException이 남 → 그럴 땐 글자가 켜질 때(OnEnable) 넣음.
    /// </summary>
    public class TextOutline : MonoBehaviour
    {
        private float _width;
        private Color32 _color;

        public static void Apply(TMP_Text t, float width, Color32 color)
        {
            if (t == null) return;
            if (t.isActiveAndEnabled && TryApply(t, width, color)) return;
            var o = t.GetComponent<TextOutline>();
            if (o == null) o = t.gameObject.AddComponent<TextOutline>();
            o._width = width; o._color = color;
            if (o.isActiveAndEnabled) o.OnEnable();
        }

        private static bool TryApply(TMP_Text t, float width, Color32 color)
        {
            try
            {
                if (t.font == null) return false;
                t.outlineWidth = width;
                t.outlineColor = color;
                return true;
            }
            catch (System.NullReferenceException) { return false; }
        }

        private void OnEnable()
        {
            var t = GetComponent<TMP_Text>();
            if (t != null && TryApply(t, _width, _color)) Destroy(this);
        }
    }
}
