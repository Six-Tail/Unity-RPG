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

        [Tooltip("스킬 목록. [0]에 기본 공격을 권장(비우면 자동 생성).")]
        public List<BattleSkill> skills = new();

        public string DisplayName =>
            character != null && !string.IsNullOrEmpty(character.displayName)
                ? character.displayName
                : displayName;
    }
}
