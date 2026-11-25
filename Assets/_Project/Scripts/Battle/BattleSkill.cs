using UnityEngine;
using SubcultureRPG.Characters;

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

        [Header("속성 / 브레이크 / 회복")]
        public Element element = Element.Physical;
        [Tooltip("약점 적중 시 대상 내구도를 깎는 양(브레이크용).")]
        public int breakDamage = 10;
        [Tooltip("회복량(시전자 ATK 대비 배율). >0이고 대상이 아군이면 피해 대신 회복.")]
        public float heal = 0f;

        [Header("부여 효과 (선택)")]
        public StatusType applyEffect = StatusType.None;
        [Tooltip("세기. 버프/디버프=비율(0.3=30%), 도트=시전자 ATK 대비 배율.")]
        public float effectMagnitude = 0.3f;
        [Min(0)] public int effectDuration = 2;
        [Range(0f, 1f)] public float effectChance = 1f;

        [Header("추가 동작 (선택)")]
        [Tooltip("Blast: 양옆 대상에게 들어가는 피해 비율(0.5 = 50%).")]
        public float splash = 0f;
        [Tooltip("흡혈: 준 피해의 이 비율만큼 시전자 회복.")]
        public float lifesteal = 0f;
        [Tooltip("대상 최대 체력 비례 추가 피해(0.05 = 5%) — 방어 무시, 받는 피해 감소는 적용, 타격 수로 나눔. 주간 보스처럼 상대 육성도와 상관없이 위협이 필요한 적용.")]
        public float maxHpDamage = 0f;
        [Tooltip("아군 대상 스킬: 대상의 행동 게이지를 이 비율만큼 당김(0.5 = 절반).")]
        [Range(0f, 1f)] public float advance = 0f;
        [Tooltip("아군 대상 스킬: 대상의 디버프를 모두 해제.")]
        public bool cleanse = false;
        [Tooltip("사용 후 시전자 자신에게 거는 효과.")]
        public StatusType selfEffect = StatusType.None;
        public float selfEffectMagnitude = 0.2f;
        [Min(0)] public int selfEffectDuration = 2;

        public bool IsUltimate => category == SkillCategory.Ultimate;
        public bool IsHeal => heal > 0f;
        public bool TargetsAllies =>
            target == TargetType.SingleAlly || target == TargetType.AllAllies || target == TargetType.Self;

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
