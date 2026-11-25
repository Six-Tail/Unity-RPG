using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace SubcultureRPG.UI
{
    /// <summary>
    /// 입력칸 표시: 입력 중(포커스)이면 칸이 밝아지고 아래에 금빛 줄이 펼쳐지며, 굵은 금빛 커서가 깜빡임.
    /// 마우스를 올리면 살짝 밝아짐. 입력칸(TMP_InputField)을 만든 뒤 <see cref="Attach"/>로 붙입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class InputFocusFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Color Gold = new(1f, 0.82f, 0.4f, 1f);

        private TMP_InputField _field;
        private Image _bg, _line;
        private Color _idle;
        private bool _hover;
        private float _k = -1f;   // 0 = 평소, 1 = 입력 중

        public static InputFocusFx Attach(TMP_InputField field)
        {
            if (field == null) return null;
            var fx = field.GetComponent<InputFocusFx>();
            if (fx == null) fx = field.gameObject.AddComponent<InputFocusFx>();
            fx.Setup(field);
            return fx;
        }

        private void Setup(TMP_InputField field)
        {
            _field = field;
            _bg = field.targetGraphic as Image;
            _idle = _bg != null ? _bg.color : new Color(1, 1, 1, 0.1f);

            // 커서: 굵게 · 금빛 · 또렷하게 깜빡임
            field.caretWidth = 3;
            field.caretBlinkRate = 1.1f;
            field.customCaretColor = true;
            field.caretColor = Gold;
            field.selectionColor = new Color(1f, 0.8f, 0.35f, 0.35f);

            // 아래 금빛 줄(입력 중일 때 가운데에서 양옆으로 펼쳐짐)
            if (_line == null)
            {
                _line = UIFactory.AddImage("FocusLine", transform, Gold);
                _line.raycastTarget = false;
                UIFactory.Anchor(_line.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f));
                _line.rectTransform.sizeDelta = new Vector2(-20, 3);
                _line.rectTransform.anchoredPosition = new Vector2(0, 2);
            }
            _k = -1f;
        }

        public void OnPointerEnter(PointerEventData e) => _hover = true;
        public void OnPointerExit(PointerEventData e) => _hover = false;

        private void LateUpdate()
        {
            if (_field == null) return;
            float target = _field.isFocused ? 1f : 0f;
            float k = _k < 0f ? target : Mathf.MoveTowards(_k, target, Time.unscaledDeltaTime * 8f);
            if (Mathf.Approximately(k, _k) && !_hover) return;
            _k = k;

            var lc = Gold; lc.a = k;
            _line.color = lc;
            _line.rectTransform.localScale = new Vector3(Mathf.Lerp(0.2f, 1f, k), 1f, 1f);
            if (_bg != null)
            {
                float a = _idle.a + (_hover ? 0.05f : 0f) + 0.1f * k;
                _bg.color = new Color(Mathf.Lerp(_idle.r, 1f, k * 0.25f), Mathf.Lerp(_idle.g, 0.94f, k * 0.25f), Mathf.Lerp(_idle.b, 0.8f, k * 0.25f), a);
            }
        }
    }
}
