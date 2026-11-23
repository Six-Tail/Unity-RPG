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

        public static Hit Compute(BattleUnit attacker, BattleUnit defender, BattleSkill skill, float roll01, BattleConfig cfg)
        {
            float raw = attacker.EffectiveAtk * Mathf.Max(0f, skill.power);
            float def = defender.EffectiveDef * (1f - attacker.penetration);          // 방어 관통
            float mitigation = def / (def + Mathf.Max(1f, cfg.defConstant));
            float afterDef = raw * (1f - mitigation);
            afterDef *= 1f + attacker.allDmg + attacker.ElementDmg(skill.element);    // 피해 증가 · 속성 피해

            bool weakness = defender.IsWeakTo(skill.element);
            if (weakness) afterDef *= Mathf.Max(1f, cfg.weaknessMultiplier);
            if (defender.isBroken) afterDef *= Mathf.Max(1f, cfg.breakVulnerability);

            bool crit = roll01 < attacker.critRate;
            float mult = crit ? 1f + attacker.critDmg : 1f;

            int dmg = Mathf.Max(1, Mathf.RoundToInt(afterDef * mult));
            return new Hit { damage = dmg, crit = crit, weakness = weakness };
        }
    }
}
