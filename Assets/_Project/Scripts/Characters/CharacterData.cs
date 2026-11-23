using UnityEngine;

namespace SubcultureRPG.Characters
{
    /// <summary>
    /// 캐릭터 1명의 데이터. 메뉴: Create > SubcultureRPG > Character Data.
    /// 5성 이하는 portrait(정적 일러스트)를, 6성은 live2DModelPrefab을 채웁니다.
    /// </summary>
    [CreateAssetMenu(fileName = "Character_", menuName = "SubcultureRPG/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [Header("기본 정보")]
        public string id;
        public string displayName;
        public Rarity rarity = Rarity.R3;

        [Header("특성")]
        [Tooltip("속성. 전투에서 이 속성으로 공격하며, 적의 약점 판정에 쓰입니다.")]
        public Element element = Element.Physical;
        [Tooltip("역할(포지션). 생성 유닛의 스탯 분배와 스킬 구성이 달라집니다.")]
        public Role role = Role.Dealer;

        [Header("정적 일러스트 (5성 이하)")]
        [Tooltip("전투용 전신/반신 일러스트. AI 출력 PNG를 Sprite로 임포트해 연결.")]
        public Sprite portrait;

        [Tooltip("UI 아이콘/썸네일.")]
        public Sprite thumbnail;

        [Header("Live2D (6성만)")]
        [Tooltip("Cubism 모델을 프리팹으로 만들어 연결. 6성이 아니면 비워 둬도 됩니다.")]
        public GameObject live2DModelPrefab;
    }
}
