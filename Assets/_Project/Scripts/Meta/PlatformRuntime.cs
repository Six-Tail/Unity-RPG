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
                Screen.fullScreen = true;   // 상태 표시줄(시간 · 알림) · 내비게이션 바 숨김(몰입 모드)
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
        /// 'SafeArea' 칸으로 옮겨 그 안에만 그림 — 버튼 · 글자가 노치 · 펀치홀 카메라에 가려지지 않음. 0.5초마다 확인(회전 · 새 창).
        /// 대신 화면 배경 그림(BackgroundArt) · 화면 바탕색 · 팝업의 어두운 막은 카메라 쪽 여백까지 넓혀 그려 검은 띠가 생기지 않게(Bleed).
        /// 안드로이드 · iOS 모두 화면 전체에 그리고(출시 설정 '노치 부분까지 그림') 여기서 UI만 안쪽으로. PC는 늘 전체.
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
                if (c.transform.childCount > 1)
                {
                    // 캔버스 바로 아래의 다른 것들을 순서대로 SafeArea 안으로(앵커 · 위치는 SafeArea 기준으로 그대로)
                    var move = new System.Collections.Generic.List<Transform>();
                    for (int i = 0; i < c.transform.childCount; i++) { var ch = c.transform.GetChild(i); if (ch != safe) move.Add(ch); }
                    foreach (var ch in move) ch.SetParent(safe, false);
                }
                // 카메라 쪽 여백(캔버스 단위)만큼 배경을 넓힘
                // (막 만든 캔버스는 레이아웃 전이라 rect 가 0일 수 있어 화면 폭 ÷ 배율로)
                float cw = c.scaleFactor > 0f ? w / c.scaleFactor : ((RectTransform)c.transform).rect.width;
                Bleed(safe, cw * min.x, cw * (1f - max.x));
            }
        }

        /// <summary>
        /// 창 · 팝업의 어두운 막(화면 전체를 덮고 가운데에 창이 있는 것) 이름. 이것들은 안전 영역이 아니라 '화면 전체' 기준으로 넓혀
        /// 카메라 쪽까지 어둡게 덮고, 안의 창은 화면 정가운데에 둠(안전 영역 가운데에 두면 카메라 반대쪽으로 밀려 보임).
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<string> Modals = new()
        {
            "Dim", "ConfirmDim", "Picker", "RatesOverlay", "Viewer", "Close",
        };

        private static bool FullStretch(RectTransform r) => r != null && r.anchorMin == Vector2.zero && r.anchorMax == Vector2.one;
        private static bool Exact(RectTransform r) => FullStretch(r) && r.offsetMin == Vector2.zero && r.offsetMax == Vector2.zero;

        /// <summary>
        /// SafeArea 안의 화면들: 화면 바탕색은 여백까지 칠하고(SafeBleed) 화면 배경 그림(BackgroundArt)은 여백까지 넓힘,
        /// 창 · 팝업의 어두운 막(Modals)은 화면 전체로 넓힘. 버튼 · 글자는 그대로 안쪽.
        /// </summary>
        private static void Bleed(RectTransform safe, float left, float right)
        {
            bool inset = left > 0.5f || right > 0.5f;
            for (int i = 0; i < safe.childCount; i++)
            {
                if (!(safe.GetChild(i) is RectTransform d) || !FullStretch(d)) continue;
                if (Modals.Contains(d.name)) { WidenSelf(d, left, right); continue; }   // 공용 창(OverlayWindow) · 확인 창 등

                var img = d.GetComponent<Image>();
                var bleed = d.Find("SafeBleed") as RectTransform;
                bool need = inset && img != null && img.color.a > 0.01f;
                if (need && bleed == null)
                {
                    var go = new GameObject("SafeBleed", typeof(RectTransform), typeof(Image));
                    bleed = (RectTransform)go.transform;
                    bleed.SetParent(d, false);
                    bleed.SetAsFirstSibling();   // 부모 바탕 바로 위, 다른 내용 아래
                    go.GetComponent<Image>().raycastTarget = false;
                }
                if (bleed != null)
                {
                    bleed.gameObject.SetActive(need);
                    if (need)
                    {
                        bleed.anchorMin = Vector2.zero; bleed.anchorMax = Vector2.one; bleed.pivot = new Vector2(0.5f, 0.5f);
                        bleed.offsetMin = new Vector2(-left, 0f); bleed.offsetMax = new Vector2(right, 0f);
                        bleed.GetComponent<Image>().color = img.color;
                    }
                }
                Widen(d.Find("BackgroundArt") as RectTransform, left, right);
                Inner(d, left, right, 0);
            }
        }

        /// <summary>화면 안쪽(화면 전체 칸을 따라 3단계까지): 배경 그림은 넓히고, 창 · 팝업의 어두운 막은 화면 전체로.</summary>
        private static void Inner(RectTransform node, float left, float right, int depth)
        {
            if (depth > 3) return;
            for (int k = 0; k < node.childCount; k++)
            {
                if (!(node.GetChild(k) is RectTransform x) || !FullStretch(x) || x.name == "BackgroundArt" || x.name == "SafeBleed") continue;
                if (Modals.Contains(x.name)) { WidenSelf(x, left, right); continue; }
                if (!Exact(x)) continue;   // 크기가 다른 칸 안쪽은 건드리지 않음
                Widen(x.Find("BackgroundArt") as RectTransform, left, right);
                Inner(x, left, right, depth + 1);
            }
        }

        /// <summary>그 판 자체를 화면 전체로(안의 창은 화면 정가운데). 붙여 둔 SafeBleed 가 있으면 끔.</summary>
        private static void WidenSelf(RectTransform r, float left, float right)
        {
            r.offsetMin = new Vector2(-left, r.offsetMin.y);
            r.offsetMax = new Vector2(right, r.offsetMax.y);
            var b = r.Find("SafeBleed");
            if (b != null) b.gameObject.SetActive(false);
        }

        /// <summary>새 창을 띄우거나 화면을 바꾼 직후 바로 안전 영역을 맞춤(0.5초 뒤에 밀려 보이는 '움찔'이 없게).</summary>
        public static void RefreshSafeArea() => ApplySafeArea();

        private static void Widen(RectTransform art, float left, float right)
        {
            if (art == null || art.anchorMin != Vector2.zero || art.anchorMax != Vector2.one) return;
            art.offsetMin = new Vector2(-left, art.offsetMin.y);
            art.offsetMax = new Vector2(right, art.offsetMax.y);
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
