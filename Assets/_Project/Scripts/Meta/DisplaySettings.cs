using System.Collections.Generic;
using UnityEngine;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// PC 화면 설정(설정 창 '화면' 줄): 전체 화면 / 창 모드 · 해상도(16:9 — 모니터보다 큰 것은 빼고 모니터 해상도는 늘 포함).
    /// 이 PC에만 저장(기기 설정 display.*). 게임을 켤 때 저장한 값으로 적용(<see cref="PlatformRuntime"/>). 모바일은 해당 없음.
    /// </summary>
    public static class DisplaySettings
    {
        private const string KMode = "display.mode", KW = "display.w", KH = "display.h";

        public static bool Supported => !Application.isMobilePlatform;

        private static readonly Vector2Int[] Common =
        {
            new(1280, 720), new(1366, 768), new(1600, 900), new(1920, 1080), new(2560, 1440), new(3200, 1800), new(3840, 2160),
        };

        /// <summary>모니터 해상도.</summary>
        public static Vector2Int Native
        {
            get
            {
                int w = Display.main != null ? Display.main.systemWidth : Screen.currentResolution.width;
                int h = Display.main != null ? Display.main.systemHeight : Screen.currentResolution.height;
                return new Vector2Int(Mathf.Max(640, w), Mathf.Max(360, h));
            }
        }

        public static List<Vector2Int> Resolutions()
        {
            var n = Native;
            var list = new List<Vector2Int>();
            foreach (var r in Common) if (r.x <= n.x && r.y <= n.y) list.Add(r);
            if (!list.Contains(n)) list.Add(n);
            list.Sort((a, b) => (a.x * a.y).CompareTo(b.x * b.y));
            return list;
        }

        public static bool Fullscreen => PlayerPrefs.GetInt(KMode, 1) == 1;

        /// <summary>고른 해상도(없으면 모니터 해상도).</summary>
        public static Vector2Int Resolution
        {
            get
            {
                int w = PlayerPrefs.GetInt(KW, 0), h = PlayerPrefs.GetInt(KH, 0);
                var n = Native;
                if (w <= 0 || h <= 0 || w > n.x || h > n.y) return n;
                return new Vector2Int(w, h);
            }
        }

        public static void SetFullscreen(bool on)
        {
            PlayerPrefs.SetInt(KMode, on ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
        }

        public static void SetResolution(Vector2Int r)
        {
            PlayerSettingsSave(r);
            Apply();
        }

        private static void PlayerSettingsSave(Vector2Int r)
        {
            PlayerPrefs.SetInt(KW, r.x); PlayerPrefs.SetInt(KH, r.y);
            PlayerPrefs.Save();
        }

        /// <summary>저장한 설정이 있으면 적용(처음 실행은 출시 설정의 기본값 = 전체 화면).</summary>
        public static void ApplySaved()
        {
            if (!Supported || Application.isEditor) return;
            if (!PlayerPrefs.HasKey(KMode) && !PlayerPrefs.HasKey(KW)) return;
            Apply();
        }

        public static void Apply()
        {
            if (!Supported || Application.isEditor) return;
            var r = Resolution;
            if (Fullscreen) Screen.SetResolution(r.x, r.y, FullScreenMode.FullScreenWindow);
            else
            {
                // 창 모드는 작업 표시줄 · 제목 줄이 들어가도록 모니터보다 한 단계 작게
                var n = Native;
                if (r.x >= n.x || r.y >= n.y)
                {
                    var list = Resolutions();
                    for (int i = list.Count - 1; i >= 0; i--) if (list[i].x < n.x && list[i].y < n.y) { r = list[i]; break; }
                }
                Screen.SetResolution(r.x, r.y, FullScreenMode.Windowed);
            }
        }
    }
}
