using UnityEngine;

namespace SubcultureRPG.Battle
{
    /// <summary>
    /// 스킬 1개(데이터). 메뉴: Create > SubcultureRPG > Battle > Skill.
    /// 피해 = 시전자 ATK × power (× hits). 방어/치명 보정은 DamageCalculator에서.
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_", menuName = "SubcultureRPG/Battle/Skill")]
    public class BattleSkill : ScriptableObject
    {
        public string skillName = "기본 공격";
        [TextArea] public string description;

        public SkillCategory category = SkillCategory.Basic;
        public TargetType target = TargetType.SingleEnemy;

        [Tooltip("ATK 대비 피해 배율 (1 = ATK 100%).")]
        public float power = 1.0f;
        [Min(1)] public int hits = 1;

        [Tooltip("사용 시 시전자가 얻는 에너지. (필살기는 사용 시 에너지 0으로 소모)")]
        public int energyGain = 20;

        public bool IsUltimate => category == SkillCategory.Ultimate;

        /// <summary>스킬이 하나도 없을 때 쓰는 런타임 기본 '기본 공격'.</summary>
        public static BattleSkill CreateDefaultBasic()
        {
            var s = CreateInstance<BattleSkill>();
            s.skillName = "기본 공격";
            s.category = SkillCategory.Basic;
            s.target = TargetType.SingleEnemy;
            s.power = 1.0f;
            s.hits = 1;
            s.energyGain = 20;
            return s;
        }
    }
}
