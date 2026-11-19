using UnityEngine;

namespace SubcultureRPG.Characters
{
    /// <summary>
    /// 6성: Live2D(Cubism) 모델을 표시.
    /// ⚠️ Live2D Cubism SDK를 아직 임포트하지 않아도 컴파일되도록 '스텁'으로 둡니다.
    ///    SDK 임포트 후 CubismModel / CubismMotionController 연결부(아래 TODO)를 채우세요.
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
