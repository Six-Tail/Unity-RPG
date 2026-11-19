using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Battle
{
    /// <summary>
    /// 전투에 투입되는 유닛 정의(= 가챠 캐릭터 + 전투 스탯/스킬).
    /// 메뉴: Create > SubcultureRPG > Battle > Unit.
    /// character를 비우면 displayName으로 표시(스탯만 있는 적 등).
    /// </summary>
    [CreateAssetMenu(fileName = "Unit_", menuName = "SubcultureRPG/Battle/Unit")]
    public class BattleUnitData : ScriptableObject
    {
        [Tooltip("가챠 캐릭터 연결(외형/이름). 비우면 아래 displayName 사용.")]
        public CharacterData character;
        [Tooltip("character가 없을 때 쓸 이름.")]
        public string displayName = "적";

        [Min(1)] public int level = 1;
        public BattleStats baseStats = new();

        [Header("속성 / 약점")]
        public Element element = Element.Physical;
        [Tooltip("이 속성으로 맞으면 약점 적중(추가 피해 + 내구도 감소).")]
        public List<Element> weaknesses = new();
        [Tooltip("내구도. 약점 피해 누적으로 0이 되면 브레이크. 0이면 브레이크 시스템 없음.")]
        public int maxToughness = 0;

        [Tooltip("스킬 목록. [0]에 기본 공격을 권장(비우면 자동 생성).")]
        public List<BattleSkill> skills = new();

        public string DisplayName =>
            character != null && !string.IsNullOrEmpty(character.displayName)
                ? character.displayName
                : displayName;
    }
}
