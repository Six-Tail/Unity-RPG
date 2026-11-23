using UnityEngine;

namespace SubcultureRPG.Characters
{
    /// <summary>
    /// 6성: Live2D(Cubism) 모델을 월드에 직접 표시(월드 공간 연출용).
    /// 게임 화면(UI)에 띄우는 것은 <see cref="Live2DStage"/> + SubcultureRPG.UI.Live2DUI 가 맡습니다 — docs/live2d.md.
    /// ⚠️ Live2D Cubism SDK를 임포트하지 않아도 컴파일되도록 SDK 타입을 직접 쓰지 않습니다.
    ///    SDK: https://www.live2d.com/sdk/download/unity/  (개인·소규모 무료 라이선스)
    /// </summary>
    public class Live2DCharacterView : MonoBehaviour, ICharacterView
    {
        private GameObject _modelInstance;

        public void Bind(CharacterData data)
        {
            if (data.live2DModelPrefab == null)
            {
                Debug.LogWarning($"[Live2D] '{data.displayName}'의 live2DModelPrefab이 비어 있습니다.");
                return;
            }
            _modelInstance = Instantiate(data.live2DModelPrefab, transform);
        }

        public void PlayMotion(string key)
        {
            // TODO: Cubism SDK 임포트 후
            //       _modelInstance.GetComponent<CubismMotionController>().PlayAnimation(...) 연결.
        }

        public void SetVisible(bool visible)
        {
            if (_modelInstance != null) _modelInstance.SetActive(visible);
        }
    }
}
