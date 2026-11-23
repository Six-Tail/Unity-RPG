using UnityEngine;

namespace SubcultureRPG.Characters
{
    /// <summary>
    /// (선택) Live2D 모델 프리팹에 붙이면 게임 화면에 잡히는 범위를 조정합니다.
    /// 붙이지 않으면 모델 전체가 보이도록 자동으로 맞춥니다.
    /// 예: 전신 모델을 상반신 위주로 → zoom 1.6, offset (0, 0.18)
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Characters/Live2D Framing")]
    public class Live2DFraming : MonoBehaviour
    {
        [Tooltip("확대 배율. 1 = 모델 전체, 2 = 두 배 확대.")]
        [Min(0.1f)] public float zoom = 1f;

        [Tooltip("보는 중심 이동(모델 키 대비). y 0.2 = 키의 20%만큼 위쪽을 봄.")]
        public Vector2 offset = Vector2.zero;
    }
}
