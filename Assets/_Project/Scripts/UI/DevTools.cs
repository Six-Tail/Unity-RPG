using UnityEngine;

namespace SubcultureRPG.UI
{
    /// <summary>
    /// 개발 · 테스트 기능(충전 버튼 · 최대 육성 · 3장까지 클리어 등)을 켤지.
    /// · 출시 빌드(Development Build 끔): 항상 꺼짐 — 버튼이 만들어지지 않고 테스트 모드도 동작하지 않음
    /// · Development Build: 켜짐
    /// · 에디터: 켜짐. 메뉴 Tools ▸ 출시 ▸ '출시 화면 미리보기(테스트 기능 숨김)'로 꺼 볼 수 있음
    /// </summary>
    public static class DevTools
    {
#if UNITY_EDITOR
        public const string HideKey = "SubcultureRPG.HideDevTools";
        public static bool Enabled => !UnityEditor.EditorPrefs.GetBool(HideKey, false);
#elif DEVELOPMENT_BUILD
        public static bool Enabled => true;
#else
        public static bool Enabled => false;
#endif

        /// <summary>테스트 기능이 꺼져 있으면 그 오브젝트를 숨기고 false.</summary>
        public static bool Gate(GameObject go)
        {
            if (Enabled) return true;
            if (go != null) go.SetActive(false);
            return false;
        }
    }
}
