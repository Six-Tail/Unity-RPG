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

        [Tooltip("대표 대사(6성 획득 연출 등에 표시). 비우면 역할별 기본 대사.")]
        [TextArea(1, 3)] public string signatureLine;

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

        /// <summary>대표 대사(비어 있으면 역할별 기본 대사).</summary>
        public string SignatureLine => !string.IsNullOrEmpty(signatureLine) ? signatureLine : role switch
        {
            Role.Tank => "내 뒤에 서. 아무것도 지나가지 못하게 할 테니까.",
            Role.Support => "준비는 끝났어. 이번엔 우리가 이길 차례야.",
            Role.Healer => "다치지 마. 끝까지 곁에서 지켜 줄게.",
            _ => "길을 열어 줄게. 내 칼끝이 닿는 곳까지.",
        };

        [Header("Live2D (6성만)")]
        [Tooltip("Cubism 모델을 프리팹으로 만들어 연결. 6성이 아니면 비워 둬도 됩니다.")]
        public GameObject live2DModelPrefab;

        [Tooltip("Live2D 대기 모션(반복 재생). 모델 폴더의 모션에서 자동 연결됩니다(이름에 idle 이 들어간 것 우선).")]
        public AnimationClip live2DIdleMotion;
    }
}
