using UnityEngine;

namespace SubcultureRPG
{
    /// <summary>
    /// 씬에 AudioListener 가 하나는 있도록 보장합니다.
    /// Live2D(Cubism SDK)가 오디오 모듈을 필요로 해 모듈을 켰는데, 리스너가 없으면 Unity 가 플레이 중 계속 경고를 냅니다.
    /// 예전에 만든 씬(카메라에 리스너 없음)도 그대로 쓰도록 실행 시 한 번 확인합니다.
    /// </summary>
    public static class AudioBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureListener()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;   // 클라우드 세이브를 받아 씬을 다시 열 때도
            Ensure();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Ensure();

        private static void Ensure()
        {
            if (Object.FindAnyObjectByType<AudioListener>() != null) return;
            var cam = Camera.main;
            var host = cam != null ? cam.gameObject : new GameObject("AudioListener");
            host.AddComponent<AudioListener>();
        }
    }
}
