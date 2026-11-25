using UnityEngine;
using UnityEngine.EventSystems;

namespace SubcultureRPG.UI
{
    /// <summary>
    /// UI 입력(클릭 · 터치 · 키보드 내비게이션) 모듈을 붙이는 곳. 화면마다 EventSystem을 코드로 만들 때 여기를 거칩니다.
    /// 새 Input System(패키지)을 쓰면 InputSystemUIInputModule + 기본 UI 조작(DefaultInputActions),
    /// 옛 Input Manager만 켜진 프로젝트라면 StandaloneInputModule.
    /// </summary>
    public static class UIInput
    {
        /// <summary>EventSystem 오브젝트에 입력 모듈을 붙임(이미 있으면 그대로).</summary>
        public static void AddModule(GameObject eventSystem)
        {
            if (eventSystem == null) return;
#if ENABLE_INPUT_SYSTEM
            var old = eventSystem.GetComponent<StandaloneInputModule>();
            if (old != null) Object.Destroy(old);
            var m = eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (m == null) m = eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (m.actionsAsset == null) m.AssignDefaultActions();   // 코드로 붙이면 조작이 비어 있으므로 기본 UI 조작을 연결
#else
            if (eventSystem.GetComponent<StandaloneInputModule>() == null) eventSystem.AddComponent<StandaloneInputModule>();
#endif
        }

        /// <summary>씬에 EventSystem이 없으면 만들고, 있으면 입력 모듈만 맞춤.</summary>
        public static EventSystem Ensure()
        {
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null) es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            AddModule(es.gameObject);
            return es;
        }
    }
}
