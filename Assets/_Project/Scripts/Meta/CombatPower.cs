using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 전투력: 지금 육성 상태(레벨 · 돌파 · 스킬 · 성급/초월 · 장비 · 패시브) 그대로의 전투 유닛에서 계산한 한 줄 요약 수치.
    ///
    ///   기본  = 공격력×3 + 체력×0.2 + 방어력×2.5 + 속도×6
    ///   배율  = 1 + 치명 확률×치명 피해 + 방어 관통 + (모든 피해 + 자기 속성 피해)
    ///             + 브레이크 효율×0.2 + 치유량×0.3 + 효과 명중×0.2 + 효과 저항×0.2 + 조건부 피해 보정 평균
    ///   스킬  = 1 + 0.03 × (기본 공격 · 스킬 · 궁극기 실제 레벨 합 - 3)
    ///   전투력 = 기본 × 배율 × 스킬 × 3.5(표시 배율 Scale)
    /// 육성 화면 · 프로필(보유 캐릭터 합산)에 표시. 밸런스 수치가 아니라 성장 체감용 지표입니다.
    /// </summary>
    public static class CombatPower
    {
        private static BattleConfig _cfg;

        /// <summary>표시 배율(숫자 규모 조절). 태생 3★ Lv.1 ≈ 1만, 6★ Lv.80 풀 육성 ≈ 15 ~ 25만.</summary>
        public const float Scale = 3.5f;   // 능력치 규모(BattleStats.Scale = 3)를 올리면서 같은 크기가 되도록 맞춤

        public static int Of(BattleUnit u, int[] effectiveSkillLevels)
        {
            if (u == null) return 0;
            float basePower = u.atk * 3f + u.maxHP * 0.2f + u.def * 2.5f + u.spd * 6f;
            float mult = 1f + u.critRate * u.critDmg + u.penetration + u.allDmg + u.ElementDmg(u.element)
                         + u.breakEffect * 0.2f + u.healBonus * 0.3f + u.effectHit * 0.2f + u.effectRes * 0.2f
                         + (u.dmgBasic + u.dmgSkill + u.dmgUlt) / 3f
                         + (u.dmgVsBroken + u.dmgVsDebuffed + u.dmgVsLowHP) * 0.3f + u.dmgTakenDown * 0.5f;
            int sum = 3;
            if (effectiveSkillLevels != null) { sum = 0; foreach (int l in effectiveSkillLevels) sum += Mathf.Max(1, l); }
            float skill = 1f + 0.03f * Mathf.Max(0, sum - 3);
            return Mathf.RoundToInt(basePower * mult * skill * Scale);
        }

        /// <summary>보유 캐릭터 1명의 전투력(장비 포함). 보유하지 않았으면 0.</summary>
        public static int Of(CharacterData c, PlayerRoster roster, PlayerEquipment equipment)
        {
            if (c == null || roster == null) return 0;
            var o = roster.GetOwned(c);
            if (o == null) return 0;
            if (_cfg == null) _cfg = BattleConfig.CreateDefault();

            var levels = roster.EffectiveSkillLevels(c);
            var data = RuntimeUnits.FromCharacter(c, o.level, null, o.skillLevel, roster.StarOf(c), roster.TranscendOf(c), levels);
            if (equipment != null) data.bonus = equipment.BonusFor(c);
            var unit = new BattleUnit(data, Team.Ally, _cfg);
            int power = Of(unit, levels);

            // 계산용으로 만든 유닛·스킬 정리
            if (data.skills != null) foreach (var sk in data.skills) if (sk != null) Object.Destroy(sk);
            Object.Destroy(data);
            return power;
        }

        /// <summary>보유 캐릭터 전투력 합계.</summary>
        public static long Total(PlayerRoster roster, PlayerEquipment equipment)
        {
            long sum = 0;
            foreach (var kv in All(roster, equipment)) sum += kv.Value;
            return sum;
        }

        /// <summary>보유 캐릭터별 전투력(높은 순).</summary>
        public static List<KeyValuePair<CharacterData, int>> All(PlayerRoster roster, PlayerEquipment equipment)
        {
            var list = new List<KeyValuePair<CharacterData, int>>();
            if (roster == null || roster.Database == null) return list;
            foreach (var o in roster.Owned)
            {
                var c = roster.Database.GetCharacter(o.id);
                if (c != null) list.Add(new KeyValuePair<CharacterData, int>(c, Of(c, roster, equipment)));
            }
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            return list;
        }
    }
}
