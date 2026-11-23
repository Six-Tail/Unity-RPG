using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SubcultureRPG.UI
{
    /// <summary>
    /// 버튼 손맛: 마우스를 올리면 살짝 커지고(1.04), 누르면 눌리며(0.95) 부드럽게 돌아옵니다. 비활성이면 그대로.
    /// UIFactory.AddButton이 자동으로 붙입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Selectable _sel;
        private bool _hover, _down;
        private float _cur = 1f;

        private void Awake() => _sel = GetComponent<Selectable>();

        private void OnDisable()
        {
            _hover = _down = false;
            _cur = 1f;
            transform.localScale = Vector3.one;
        }

        public void OnPointerEnter(PointerEventData e) => _hover = true;
        public void OnPointerExit(PointerEventData e) { _hover = false; _down = false; }
        public void OnPointerDown(PointerEventData e) => _down = true;
        public void OnPointerUp(PointerEventData e) => _down = false;

        private void Update()
        {
            bool active = _sel == null || _sel.IsInteractable();
            float target = !active ? 1f : _down ? 0.95f : _hover ? 1.04f : 1f;
            if (Mathf.Abs(_cur - target) < 0.0005f) return;
            _cur = Mathf.Lerp(_cur, target, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(_cur, _cur, 1f);
        }
    }
}
