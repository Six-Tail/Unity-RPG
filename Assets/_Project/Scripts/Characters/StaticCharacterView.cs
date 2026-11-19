using UnityEngine;

namespace SubcultureRPG.Characters
{
    /// <summary>
    /// 5성 이하: 정적 일러스트(Sprite)를 SpriteRenderer로 표시.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class StaticCharacterView : MonoBehaviour, ICharacterView
    {
        private SpriteRenderer _renderer;

        private void Awake() => _renderer = GetComponent<SpriteRenderer>();

        public void Bind(CharacterData data)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _renderer.sprite = data.portrait;
        }

        public void PlayMotion(string key)
        {
            // 정적 일러스트는 코드 기반 간단 연출로 대체합니다.
            // TODO: 공격 시 살짝 전진/스케일 트윈(DOTween 등) 연결.
        }

        public void SetVisible(bool visible)
        {
            if (_renderer != null) _renderer.enabled = visible;
        }
    }
}
