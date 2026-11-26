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

        private float _safeTick;

        private void Update()
        {
            if (Time.unscaledTime >= _safeTick) { _safeTick = Time.unscaledTime + 0.5f; ApplySafeArea(); }
            if (!CanQuit) return;
            if (Input.GetKeyDown(KeyCode.Escape)) Back();
        }

        /// <summary>
        /// 안전 영역(노치 · 둥근 모서리 · 홈 표시줄): 화면 전체가 안전하지 않으면(iPhone 등) 모든 화면 캔버스의 내용을
        /// 'SafeArea' 칸으로 옮겨 그 안에만 그림 — 버튼이 노치에 가려지지 않음(노치 쪽은 검은 여백). 0.5초마다 확인(회전 · 새 창).
        /// 안드로이드는 출시 설정에서 노치 부분을 아예 그리지 않게 해 두어 여기서는 할 일이 없음. PC는 늘 전체.
        /// </summary>
        private static void ApplySafeArea()
        {
            var sa = Screen.safeArea;
            float w = Screen.width, h = Screen.height;
            if (w <= 0 || h <= 0) return;
            // 가로 화면이라 노치 · 둥근 모서리는 양옆. 위 · 아래(홈 표시줄) 여백까지 빼면 화면 높이가 줄어 가운데 기준 창이
            // 위 글자를 덮으므로(스테이지 상세 ↔ 행동력) 좌우만 비움 — 위아래 배치는 설계 그대로
            bool full = sa.xMin <= 0.5f && sa.width >= w - 1f;
            var min = full ? Vector2.zero : new Vector2(sa.xMin / w, 0f);
            var max = full ? Vector2.one : new Vector2(sa.xMax / w, 1f);
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (c == null || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                var safe = c.transform.Find("SafeArea") as RectTransform;
                if (full && safe == null) continue;
                if (safe == null)
                {
                    safe = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
                    safe.SetParent(c.transform, false);
                }
                safe.anchorMin = min; safe.anchorMax = max;
                safe.offsetMin = Vector2.zero; safe.offsetMax = Vector2.zero;
                safe.pivot = new Vector2(0.5f, 0.5f);
                if (c.transform.childCount <= 1) continue;
                // 캔버스 바로 아래의 다른 것들을 순서대로 SafeArea 안으로(앵커 · 위치는 SafeArea 기준으로 그대로)
                var move = new System.Collections.Generic.List<Transform>();
                for (int i = 0; i < c.transform.childCount; i++) { var ch = c.transform.GetChild(i); if (ch != safe) move.Add(ch); }
                foreach (var ch in move) ch.SetParent(safe, false);
            }
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
