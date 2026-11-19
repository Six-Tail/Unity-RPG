using UnityEngine;

namespace SubcultureRPG.Battle
{
    /// <summary>
    /// 피해 계산(순수 함수). 치명 판정 RNG는 주입된 roll[0,1)로 → 결정적 테스트 가능.
    /// 피해 = ATK × power × (1 - DEF/(DEF+K)) × (치명 시 1+critDmg), 최소 1.
    /// </summary>
    public static class DamageCalculator
    {
        public struct Hit
        {
            public int damage;
            public bool crit;
        }

        public static Hit Compute(BattleUnit attacker, BattleUnit defender, BattleSkill skill, float roll01, BattleConfig cfg)
        {
            float raw = attacker.atk * Mathf.Max(0f, skill.power);
            float mitigation = defender.def / (defender.def + Mathf.Max(1f, cfg.defConstant));
            float afterDef = raw * (1f - mitigation);

            bool crit = roll01 < attacker.critRate;
            float mult = crit ? 1f + attacker.critDmg : 1f;

            int dmg = Mathf.Max(1, Mathf.RoundToInt(afterDef * mult));
            return new Hit { damage = dmg, crit = crit };
        }
    }
}
