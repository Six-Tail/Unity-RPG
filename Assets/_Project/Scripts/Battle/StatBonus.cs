using System;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Battle
{
    /// <summary>
    /// 장비 등 외부에서 더해지는 능력치 묶음. 비율은 0.1 = 10%.
    /// 최종치 = 레벨 기준치 × (1 + %) + 고정치. 나머지는 전투 계산에 그대로 더해짐.
    /// </summary>
    [Serializable]
    public class StatBonus
    {
        [Header("고정치")]
        public float hp, atk, def, spd;

        [Header("비율 (0.1 = 10%)")]
        public float hpPct, atkPct, defPct;

        [Header("전투")]
        public float critRate, critDmg;
        [Tooltip("방어 관통: 대상 방어력의 이 비율만큼 무시(최대 80%).")] public float penetration;
        [Tooltip("모든 피해 증가.")] public float allDmg;
        [Tooltip("속성별 피해 증가(Element 순서: 물리·화염·얼음·번개·바람).")] public float[] elementDmg = new float[5];
        [Tooltip("브레이크 효율: 내구도 감소량·브레이크 추가 피해 증가.")] public float breakEffect;
        [Tooltip("에너지 회복 효율: 행동으로 얻는 에너지 증가.")] public float energyRegen;
        [Tooltip("치유량 증가.")] public float healBonus;
        [Tooltip("효과 명중: 적에게 거는 효과 확률 증가.")] public float effectHit;
        [Tooltip("효과 저항: 받는 해로운 효과 확률 감소.")] public float effectRes;
        [Tooltip("전투 시작 에너지.")] public int startEnergy;

        public float ElementDmg(Element e)
        {
            int i = (int)e;
            return elementDmg != null && i >= 0 && i < elementDmg.Length ? elementDmg[i] : 0f;
        }

        public void AddElementDmg(Element e, float v)
        {
            if (elementDmg == null || elementDmg.Length < 5) elementDmg = new float[5];
            elementDmg[(int)e] += v;
        }

        /// <summary>다른 묶음을 더함.</summary>
        public void Add(StatBonus o)
        {
            if (o == null) return;
            hp += o.hp; atk += o.atk; def += o.def; spd += o.spd;
            hpPct += o.hpPct; atkPct += o.atkPct; defPct += o.defPct;
            critRate += o.critRate; critDmg += o.critDmg;
            penetration += o.penetration; allDmg += o.allDmg;
            breakEffect += o.breakEffect; energyRegen += o.energyRegen; healBonus += o.healBonus;
            effectHit += o.effectHit; effectRes += o.effectRes; startEnergy += o.startEnergy;
            for (int i = 0; i < 5; i++) AddElementDmg((Element)i, o.ElementDmg((Element)i));
        }

        /// <summary>최종 HP/ATK/DEF (기준치 × (1+%) + 고정치).</summary>
        public int ApplyHP(int baseValue) => Mathf.Max(1, Mathf.RoundToInt(baseValue * (1f + hpPct) + hp));
        public int ApplyAtk(int baseValue) => Mathf.Max(0, Mathf.RoundToInt(baseValue * (1f + atkPct) + atk));
        public int ApplyDef(int baseValue) => Mathf.Max(0, Mathf.RoundToInt(baseValue * (1f + defPct) + def));
        public int ApplySpd(int baseValue) => Mathf.Max(1, baseValue + Mathf.RoundToInt(spd));
    }
}
