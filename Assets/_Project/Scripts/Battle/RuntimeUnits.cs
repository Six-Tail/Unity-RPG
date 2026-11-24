using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Battle
{
    /// <summary>
    /// CharacterData로부터 전투 유닛(BattleUnitData)을 얻습니다.
    /// 저작된 유닛이 있으면 그걸, 없으면 성급·속성·역할 기반으로 런타임 생성 → 뽑은 캐릭터가 바로 전투 가능.
    /// (Meta에 의존하지 않도록 저작 유닛 조회는 델리게이트로 주입)
    /// </summary>
    public static class RuntimeUnits
    {
        /// <param name="star">현재 성급(합성 반영). 0이면 태생 성급.</param>
        /// <param name="transcend">초월 단계(6성일 때만 유효).</param>
        public static BattleUnitData FromCharacter(CharacterData c, int level,
            Func<CharacterData, BattleUnitData> authoredLookup = null, int skillLevel = 1,
            int star = 0, int transcend = 0, int[] skillLevels = null)
        {
            Rarity native = c != null ? c.rarity : Rarity.R3;
            if (star <= 0) star = (int)native;
            bool bonus = c != null && TranscendBonus.Needed(native, star, transcend);
            Role role = c != null ? c.role : Role.Dealer;

            if (authoredLookup != null)
            {
                var authored = authoredLookup(c);
                if (authored != null)
                    return bonus ? TranscendBonus.Apply(authored, native, star, transcend, role, inPlace: false) : authored;
            }

            var u = Generate(c, level, skillLevel, skillLevels, star >= Transcendence.MaxStar ? transcend : 0);
            return bonus ? TranscendBonus.Apply(u, native, star, transcend, role, inPlace: true) : u;
        }

        /// <summary>
        /// 유닛 생성. 능력치는 성급·역할 기반, 스킬·패시브는 캐릭터별 구성(<see cref="CharacterKits"/>).
        /// 스킬 레벨로 피해/회복/효과량을 스케일(+10%/Lv). transcend = 해금된 초월 고유 능력 반영.
        /// </summary>
        public static BattleUnitData Generate(CharacterData c, int level, int skillLevel = 1, int[] skillLevels = null, int transcend = 0)
        {
            int r = c != null ? (int)c.rarity : 3;      // 3..6
            float mul = 1f + 0.35f * (r - 3);           // 3성=1.0 … 6성≈2.05
            Element el = c != null ? c.element : Element.Physical;
            Role role = c != null ? c.role : Role.Dealer;
            var rs = RoleStats(role);

            var u = ScriptableObject.CreateInstance<BattleUnitData>();
            u.character = c;
            u.displayName = c != null && !string.IsNullOrEmpty(c.displayName) ? c.displayName : "유닛";
            u.level = Mathf.Max(1, level);
            u.element = el;

            int baseHP = Mathf.RoundToInt(800 * mul * rs.hp);
            int baseAtk = Mathf.RoundToInt(90 * mul * rs.atk);
            int baseDef = Mathf.RoundToInt(45 * mul * rs.def);
            u.baseStats = new BattleStats
            {
                maxHP = baseHP,
                atk = baseAtk,
                def = baseDef,
                spd = 95 + 5 * (r - 3) + rs.spd,
                critRate = role == Role.Dealer ? 0.12f : 0.05f,
                critDmg = 0.5f,
                hpGrowth = Mathf.RoundToInt(baseHP * 0.06f),   // 레벨당 +6%
                atkGrowth = Mathf.RoundToInt(baseAtk * 0.06f),
                defGrowth = Mathf.RoundToInt(baseDef * 0.06f)
            };

            // 스킬 레벨당 +10% (기본 공격·스킬·궁극기 각각)
            float Mul(int i) => 1f + 0.1f * Mathf.Max(0, (skillLevels != null && skillLevels.Length > i ? skillLevels[i] : skillLevel) - 1);
            // 캐릭터별 구성: 기본 공격 · 스킬 · 궁극기 + 패시브(기본 + 해금된 초월 고유 능력)
            var kit = CharacterKits.Get(c);
            var passives = CharacterKits.ActivePassives(c, transcend);
            int Extra(PassiveEffect e)
            {
                float n = 0f;
                foreach (var p in passives) if (p.trigger == PassiveTrigger.Static && p.effect == e) n += p.v;
                return Mathf.RoundToInt(n);
            }
            u.skills = new List<BattleSkill>
            {
                kit.basic.Build(SkillCategory.Basic, el, Mul(0), Extra(PassiveEffect.HitsBasic)),
                kit.skill.Build(SkillCategory.Skill, el, Mul(1), Extra(PassiveEffect.HitsSkill)),
                kit.ultimate.Build(SkillCategory.Ultimate, el, Mul(2), Extra(PassiveEffect.HitsUlt)),
            };
            u.passives = passives;
            return u;
        }

        // ---------------- 역할별 구성 ----------------

        private struct RoleMul { public float hp, atk, def; public int spd; }

        private static RoleMul RoleStats(Role role) => role switch
        {
            Role.Tank => new RoleMul { hp = 1.35f, atk = 0.8f, def = 1.45f, spd = -5 },
            Role.Support => new RoleMul { hp = 1.0f, atk = 0.95f, def = 1.0f, spd = 10 },
            Role.Healer => new RoleMul { hp = 1.05f, atk = 0.9f, def = 1.05f, spd = 0 },
            _ => new RoleMul { hp = 0.9f, atk = 1.25f, def = 0.9f, spd = 5 }, // 딜러
        };
    }
}
