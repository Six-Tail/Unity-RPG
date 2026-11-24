using UnityEngine;

namespace SubcultureRPG.Battle
{
    /// <summary>
    /// 피해 계산(순수 함수). 치명 판정 RNG는 주입된 roll[0,1)로 → 결정적 테스트 가능.
    /// 유효 스탯(버프/디버프 반영) + 약점/브레이크 보정 적용.
    /// </summary>
    public static class DamageCalculator
    {
        public struct Hit
        {
            public int damage;
            public bool crit;
            public bool weakness;
        }

        public static Hit Compute(BattleUnit attacker, BattleUnit defender, BattleSkill skill, float roll01, BattleConfig cfg, float powerMul = 1f) =>
            Compute(attacker, defender, skill.power * powerMul, skill.element, skill.category, roll01, cfg, true);

        /// <summary>
        /// 계수·속성 직접 지정(추가 공격·반격 등). categoryBonus = 종류별(기본/스킬/궁극기) 피해 증가 적용 여부.
        /// </summary>
        public static Hit Compute(BattleUnit attacker, BattleUnit defender, float power, SubcultureRPG.Characters.Element element,
            SkillCategory category, float roll01, BattleConfig cfg, bool categoryBonus)
        {
            float raw = attacker.EffectiveAtk * Mathf.Max(0f, power);
            float def = defender.EffectiveDef * (1f - attacker.penetration);          // 방어 관통
            float mitigation = def / (def + Mathf.Max(1f, cfg.defConstant) * BattleStats.Scale);
            float afterDef = raw * (1f - mitigation);

            // 피해 증가(합연산): 모든 피해 · 속성 피해 · 종류별 · 조건부(패시브)
            float bonus = attacker.allDmg + attacker.ElementDmg(element);
            if (categoryBonus)
                bonus += category == SkillCategory.Basic ? attacker.dmgBasic
                    : category == SkillCategory.Skill ? attacker.dmgSkill : attacker.dmgUlt;
            if (defender.isBroken) bonus += attacker.dmgVsBroken;
            if (defender.DebuffCount > 0) bonus += attacker.dmgVsDebuffed;
            if (defender.HPRatio < 0.5f) bonus += attacker.dmgVsLowHP;
            afterDef *= 1f + bonus;
            afterDef *= 1f - Mathf.Clamp(defender.dmgTakenDown, 0f, 0.8f);            // 받는 피해 감소

            bool weakness = defender.IsWeakTo(element);
            if (weakness) afterDef *= Mathf.Max(1f, cfg.weaknessMultiplier);
            if (defender.isBroken) afterDef *= Mathf.Max(1f, cfg.breakVulnerability);

            bool crit = roll01 < attacker.EffectiveCritRate;
            float mult = crit ? 1f + attacker.critDmg : 1f;

            int dmg = Mathf.Max(1, Mathf.RoundToInt(afterDef * mult));
            return new Hit { damage = dmg, crit = crit, weakness = weakness };
        }
    }
}
