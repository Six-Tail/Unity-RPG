using UnityEngine;
using UnityEngine.UI;
using SubcultureRPG.Meta.UI;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 플랫폼별 실행 설정(게임 시작 때 스스로 생김).
    /// · 모바일(안드로이드 · iOS): 60프레임 · 화면 꺼짐 방지 · 가로 화면 고정(왼쪽 · 오른쪽 가로만 회전)
    /// · PC: 수직 동기화
    /// · 뒤로 가기(안드로이드 뒤로 버튼 · PC Esc): 맨 위 창 '닫기' → 화면의 '← 뒤로' → 홈 · 타이틀이면 종료 확인 (iOS는 해당 없음)
    /// · 앱이 백그라운드로 가면 세이브를 디스크에 씀
    /// </summary>
    public class PlatformRuntime : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<PlatformRuntime>() != null) return;
            var go = new GameObject("PlatformRuntime");
            DontDestroyOnLoad(go);
            go.AddComponent<PlatformRuntime>();
        }

        public static bool Mobile => Application.isMobilePlatform;

        /// <summary>앱을 스스로 끌 수 있는 플랫폼인가(iOS는 앱이 스스로 종료하면 안 됨).</summary>
        public static bool CanQuit => Application.platform != RuntimePlatform.IPhonePlayer;

        private void Awake()
        {
            if (Mobile)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }
            else
            {
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = -1;
                DisplaySettings.ApplySaved();   // PC: 저장한 화면 모드 · 해상도
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveStore.Save();
        }

        private void Update()
        {
            if (!CanQuit) return;
            if (Input.GetKeyDown(KeyCode.Escape)) Back();
        }

        /// <summary>뒤로 가기 한 번.</summary>
        public static void Back()
        {
            if (Popups.IsOpen) return;   // 알림 창은 버튼으로 고르게
            var target = TopBackButton();
            if (target != null) { target.onClick.Invoke(); return; }

            var nav = FindAnyObjectByType<ScreenNavigator>();
            if (nav != null && nav.Current == HubScreen.Battle) return;   // 전투 중엔 무시(나가기는 전투 화면 버튼으로)
            AskQuit();
        }

        public static void AskQuit()
        {
            if (!CanQuit || Popups.IsOpen) return;
            Popups.Show("게임 종료", "게임을 종료할까요?\n<size=85%><color=#9AA3B5>진행 상황은 저장되어 있습니다.</color></size>", "종료", Quit, "취소");
        }

        public static void Quit()
        {
            SaveStore.Save();
            Application.Quit();
        }

        /// <summary>지금 보이는 '닫기'(창) 또는 '← 뒤로'(화면) 버튼 중 가장 위의 것.</summary>
        private static Button TopBackButton()
        {
            Button best = null;
            int bestScore = int.MinValue;
            foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (b == null || !b.isActiveAndEnabled || !b.IsInteractable()) continue;
                string n = b.name;
                bool close = n == "Close", back = n == "BackBtn" || n == "Back";
                if (!close && !back) continue;
                if (Hidden(b.transform)) continue;
                var canvas = b.GetComponentInParent<Canvas>();
                if (canvas == null || !canvas.isActiveAndEnabled) continue;
                int score = canvas.rootCanvas.sortingOrder * 4 + (close ? 2 : 0);
                if (score > bestScore) { bestScore = score; best = b; }
            }
            return best;
        }

        /// <summary>투명하게 숨겨 둔 묶음(CanvasGroup alpha 0) 안이면 true.</summary>
        private static bool Hidden(Transform t)
        {
            foreach (var g in t.GetComponentsInParent<CanvasGroup>())
                if (g.alpha <= 0.01f) return true;
            return false;
        }
    }
}
