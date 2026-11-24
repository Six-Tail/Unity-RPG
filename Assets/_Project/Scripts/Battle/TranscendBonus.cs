using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Battle
{
    /// <summary>
    /// 합성(성급)·초월 단계를 전투 유닛에 반영합니다. 규칙 수치는 <see cref="Transcendence"/>.
    /// · 능력치 배율(성급/승급 패널티/초월) · 스킬 계수. 초월 고유 능력(+1/+3/+6)은 CharacterKits, 스킬 레벨 +3(+2/+4/+5)은 Transcendence.SkillLevelBonus
    /// 저작 에셋을 건드리지 않도록 복제본에 적용합니다.
    /// </summary>
    public static class TranscendBonus
    {
        /// <summary>보정이 필요한지(태생 성급·초월 0이면 불필요).</summary>
        public static bool Needed(Rarity native, int star, int transcend) =>
            star > (int)native || transcend > 0;

        /// <summary>
        /// 보정 적용. inPlace=false면 유닛과 스킬을 복제해서 적용(저작 에셋 보호).
        /// </summary>
        public static BattleUnitData Apply(BattleUnitData src, Rarity native, int star, int transcend, Role role, bool inPlace)
        {
            if (src == null) return null;
            int n = (int)native;
            star = Mathf.Clamp(star, n, Transcendence.MaxStar);
            transcend = star >= Transcendence.MaxStar ? Mathf.Clamp(transcend, 0, Transcendence.MaxTranscend) : 0;

            var u = inPlace ? src : Object.Instantiate(src);
            if (!inPlace) u.name = src.name + $"_T{star}+{transcend}";

            // --- 능력치 ---
            float m = Transcendence.StatMultiplier(native, star, transcend);
            var s = u.baseStats ?? new BattleStats();
            float hpM = m, defM = m;

            s.maxHP = Mathf.RoundToInt(s.maxHP * hpM);
            s.hpGrowth = Mathf.RoundToInt(s.hpGrowth * hpM);
            s.atk = Mathf.RoundToInt(s.atk * m);
            s.atkGrowth = Mathf.RoundToInt(s.atkGrowth * m);
            s.def = Mathf.RoundToInt(s.def * defM);
            s.defGrowth = Mathf.RoundToInt(s.defGrowth * defM);
            s.spd += 5 * (star - n);                       // 성급 1단계당 속도 +5 (생성 규칙과 동일)

            u.baseStats = s;

            // --- 스킬 ---
            if (u.skills != null)
            {
                float k = Transcendence.SkillMultiplier(transcend);
                for (int i = 0; i < u.skills.Count; i++)
                {
                    var sk = u.skills[i];
                    if (sk == null) continue;
                    if (!inPlace) { sk = Object.Instantiate(sk); u.skills[i] = sk; }

                    sk.power *= k;
                    sk.heal *= k;
                    if (sk.TargetsAllies && sk.applyEffect != StatusType.None) sk.effectMagnitude *= k;

                }
            }
            return u;
        }
    }
}
