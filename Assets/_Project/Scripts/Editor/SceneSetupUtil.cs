#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 생성기 공용 새 씬: 빈 씬에 카메라(직교)와 AudioListener만 직접 만듭니다.
    /// UI는 Screen Space Overlay라 조명은 필요 없음. (오디오 모듈은 Live2D SDK 때문에 켜져 있음)
    /// </summary>
    public static class SceneSetupUtil
    {
        public static Scene NewSceneWithCamera()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);
            cam.orthographic = true;
            camGO.AddComponent<AudioListener>();   // 오디오 모듈을 켰으므로(Live2D SDK 필요) 리스너를 둠
            camGO.transform.position = new Vector3(0, 0, -10);
            return scene;
        }
    }
}
#endif
