using UnityEditor;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 다음 작업 알림: pull 받고 Unity를 열면(스크립트가 다시 컴파일되면) 한 번 창으로 알려 줌.
    /// '다시 보지 않기'를 누를 때까지 Unity를 열 때마다 보여 줌. 내용은 docs/NEXT.md.
    /// 작업을 끝내면 이 파일과 docs/NEXT.md를 지우면 됨.
    /// </summary>
    [InitializeOnLoad]
    public static class NextTaskReminder
    {
        private const string Key = "SubcultureRPG.NextTask.RealGuild";
        private const string SessionKey = "SubcultureRPG.NextTask.Shown";

        static NextTaskReminder()
        {
            if (EditorPrefs.GetBool(Key, false) || SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.delayCall += Show;
        }

        private static void Show()
        {
            SessionState.SetBool(SessionKey, true);   // 이번 실행에선 한 번만
            int pick = EditorUtility.DisplayDialogComplex("다음 작업 안내",
                "실제 길드 개발을 이어서 진행해야 합니다.\n\n" +
                "Claude에게 \"길드 이어서 진행\"이라고 말하면 바로 시작합니다.\n" +
                "· 시작 전 결정: 지금 가상 길드에 가입된 세이브 처리 방법\n" +
                "· 대시보드 Cloud Save 인덱스 3개 추가 예정: g_id · g_open · g_req\n\n" +
                "자세한 내용: docs/NEXT.md",
                "확인", "다시 보지 않기", "");
            if (pick == 1) EditorPrefs.SetBool(Key, true);
        }
    }
}
