#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 생성기 공용 새 씬. 유니티 기본 씬(DefaultGameObjects)은 카메라에 AudioListener를 붙이는데,
    /// 이 프로젝트는 내장 Audio 모듈이 꺼져 있어 씬을 열 때마다 "AudioListener component deleted" 경고가 납니다.
    /// → 빈 씬에 카메라(AudioListener 없음)만 직접 만듭니다. UI는 Screen Space Overlay라 조명은 필요 없음.
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
            camGO.transform.position = new Vector3(0, 0, -10);
            return scene;
        }
    }
}
#endif
