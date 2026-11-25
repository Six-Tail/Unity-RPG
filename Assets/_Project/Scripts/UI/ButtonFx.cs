using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SubcultureRPG.UI
{
    /// <summary>
    /// 버튼 손맛: 마우스를 올리면 살짝 커지고(1.04), 누르면 눌리며(0.95) 부드럽게 돌아옵니다. 비활성이면 그대로.
    /// 버튼 그림의 금테(SetFrame)가 있으면 판의 투명도 · 비활성 상태에 맞춰 금테도 옅게 합니다.
    /// UIFactory.AddButton이 자동으로 붙입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        /// <summary>누를 때 낼 효과음(기본 클릭음). 꺼진 버튼은 소리 없음.</summary>
        public Sfx clickSound = Sfx.Click;
        public bool mute;

        public void OnPointerClick(PointerEventData e)
        {
            if (mute || (_sel != null && !_sel.IsInteractable())) return;
            SoundManager.Play(clickSound, 0.7f);
        }
        private Selectable _sel;
        private bool _hover, _down;
        private float _cur = 1f;
        private Graphic _plate;
        private Image _frame;
        private float _frameAlpha = -1f;

        private void Awake() => _sel = GetComponent<Selectable>();

        private void OnEnable() => SyncFrame();

        /// <summary>판 위에 겹친 금테를 등록(판이 옅어지거나 버튼이 꺼지면 같이 옅어짐).</summary>
        public void SetFrame(Image frame, Graphic plate)
        {
            _frame = frame; _plate = plate;
            _frameAlpha = -1f;
            SyncFrame();
        }

        private void SyncFrame()
        {
            if (_frame == null || _plate == null) return;
            bool active = _sel == null || _sel.IsInteractable();
            float a = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(0.05f, 0.6f, _plate.color.a)) * (active ? 1f : 0.45f);
            if (Mathf.Abs(a - _frameAlpha) < 0.004f) return;
            _frameAlpha = a;
            _frame.color = new Color(1f, 1f, 1f, a);
        }

        private void OnDisable()
        {
            _hover = _down = false;
            _cur = 1f;
            ApplyScale(1f);
        }

        // 크기는 기준점(pivot)을 중심으로 바뀌므로, 왼쪽 위 기준 버튼은 오른쪽 아래로 커져 보임.
        // 커진 만큼 위치를 반대로 옮겨 언제나 버튼 가운데를 중심으로 커지고 줄어들게 함.
        private Vector2 _shift;

        private void ApplyScale(float s)
        {
            transform.localScale = new Vector3(s, s, 1f);
            if (transform is not RectTransform rt) return;
            Vector2 want = -(s - 1f) * Vector2.Scale(new Vector2(0.5f, 0.5f) - rt.pivot, rt.rect.size);
            rt.anchoredPosition += want - _shift;
            _shift = want;
        }

        public void OnPointerEnter(PointerEventData e) => _hover = true;
        public void OnPointerExit(PointerEventData e) { _hover = false; _down = false; }
        public void OnPointerDown(PointerEventData e) => _down = true;
        public void OnPointerUp(PointerEventData e) => _down = false;

        private void Update()
        {
            SyncFrame();
            bool active = _sel == null || _sel.IsInteractable();
            float target = !active ? 1f : _down ? 0.95f : _hover ? 1.04f : 1f;
            if (Mathf.Abs(_cur - target) < 0.0005f)
            {
                if (_cur != target) { _cur = target; ApplyScale(_cur); }   // 끝에서 정확히 1로(위치 보정도 0으로)
                return;
            }
            _cur = Mathf.Lerp(_cur, target, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
            ApplyScale(_cur);
        }
    }
}
