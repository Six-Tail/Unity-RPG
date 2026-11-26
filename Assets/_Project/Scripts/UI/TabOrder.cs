using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace SubcultureRPG.UI
{
    /// <summary>
    /// 입력칸 Tab 이동(PC · 에디터): 칸에 입력 중 Tab = 다음 칸, Shift+Tab = 이전 칸(끝에서는 처음으로).
    /// 로그인 · 회원가입 · 비밀번호 변경 창에서 씀. 칸에 Tab 글자가 들어가지 않게 막음.
    ///   TabOrder.Attach(창.gameObject, 아이디칸, 비밀번호칸);
    /// </summary>
    public class TabOrder : MonoBehaviour
    {
        private readonly List<TMP_InputField> _fields = new();

        public static TabOrder Attach(GameObject host, params TMP_InputField[] fields)
        {
            if (host == null) return null;
            var t = host.GetComponent<TabOrder>();
            if (t == null) t = host.AddComponent<TabOrder>();
            t._fields.Clear();
            foreach (var f in fields)
            {
                if (f == null) continue;
                t._fields.Add(f);
                f.onValidateInput = (text, index, c) => c == '\t' ? '\0' : c;   // Tab 글자는 넣지 않음
            }
            return t;
        }

        private void Update()
        {
            if (_fields.Count < 2 || !TabPressed(out bool back)) return;
            int cur = _fields.FindIndex(f => f != null && f.isFocused);
            if (cur < 0) return;
            int next = cur;
            for (int k = 0; k < _fields.Count; k++)
            {
                next = (next + (back ? -1 : 1) + _fields.Count) % _fields.Count;
                var f = _fields[next];
                if (f != null && f.gameObject.activeInHierarchy && f.interactable) break;
            }
            if (next == cur) return;
            _fields[cur].DeactivateInputField();
            var target = _fields[next];
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target.gameObject);
            target.ActivateInputField();
            target.MoveTextEnd(false);
        }

        /// <summary>이번 프레임에 Tab을 눌렀는가(새 Input System · 옛 Input Manager 둘 다).</summary>
        private static bool TabPressed(out bool shift)
        {
            shift = false;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (!kb.tabKey.wasPressedThisFrame) return false;
                shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!Input.GetKeyDown(KeyCode.Tab)) return false;
            shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            return true;
#else
            return false;
#endif
        }
    }
}
