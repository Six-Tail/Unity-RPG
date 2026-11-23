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

            var u = Generate(c, level, skillLevel, skillLevels);
            return bonus ? TranscendBonus.Apply(u, native, star, transcend, role, inPlace: true) : u;
        }

        /// <summary>
        /// 성급·역할 기반 유닛 생성. 속성은 캐릭터 지정값.
        /// 스킬 레벨로 피해/회복/효과량을 스케일(+10%/Lv).
        /// </summary>
        public static BattleUnitData Generate(CharacterData c, int level, int skillLevel = 1, int[] skillLevels = null)
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
            u.skills = new List<BattleSkill>
            {
                MakeSkill("기본 공격", SkillCategory.Basic, el, TargetType.SingleEnemy, 1.0f * Mul(0), 20, 10),
                RoleSkill(role, el, Mul(1)),
                RoleUltimate(role, el, Mul(2)),
            };
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

        private static BattleSkill RoleSkill(Role role, Element el, float sMul)
        {
            switch (role)
            {
                case Role.Tank:
                    return WithEffect(MakeSkill("방어 태세", SkillCategory.Skill, el, TargetType.Self, 0f, 30, 0),
                        StatusType.DefUp, 0.5f * sMul, 2);
                case Role.Support:
                    return WithEffect(MakeSkill("전투 고무", SkillCategory.Skill, el, TargetType.AllAllies, 0f, 30, 0),
                        StatusType.AtkUp, 0.25f * sMul, 2);
                case Role.Healer:
                    return WithHeal(MakeSkill("치유", SkillCategory.Skill, el, TargetType.SingleAlly, 0f, 30, 0), 2.0f * sMul);
                default:
                    return MakeSkill("스킬", SkillCategory.Skill, el, TargetType.SingleEnemy, 1.8f * sMul, 30, 20);
            }
        }

        private static BattleSkill RoleUltimate(Role role, Element el, float sMul)
        {
            switch (role)
            {
                case Role.Tank:
                    return WithEffect(MakeSkill("철벽 강타", SkillCategory.Ultimate, el, TargetType.AllEnemies, 2.2f * sMul, 0, 20),
                        StatusType.DefDown, 0.2f, 2);
                case Role.Support:
                    return WithEffect(MakeSkill("총공격 신호", SkillCategory.Ultimate, el, TargetType.AllEnemies, 2.4f * sMul, 0, 20),
                        StatusType.AtkDown, 0.2f, 2);
                case Role.Healer:
                    return WithHeal(MakeSkill("치유의 빛", SkillCategory.Ultimate, el, TargetType.AllAllies, 0f, 0, 0), 1.4f * sMul);
                default:
                    return MakeSkill("필살기", SkillCategory.Ultimate, el, TargetType.AllEnemies, 3.2f * sMul, 0, 20);
            }
        }

        private static BattleSkill MakeSkill(string name, SkillCategory cat, Element element, TargetType target, float power, int energyGain, int breakDamage)
        {
            var s = ScriptableObject.CreateInstance<BattleSkill>();
            s.skillName = name;
            s.category = cat;
            s.element = element;
            s.target = target;
            s.power = power;
            s.hits = 1;
            s.energyGain = energyGain;
            s.breakDamage = breakDamage;
            return s;
        }

        private static BattleSkill WithEffect(BattleSkill s, StatusType type, float magnitude, int duration)
        {
            s.applyEffect = type;
            s.effectMagnitude = magnitude;
            s.effectDuration = duration;
            s.effectChance = 1f;
            return s;
        }

        private static BattleSkill WithHeal(BattleSkill s, float heal)
        {
            s.heal = heal;
            return s;
        }
    }
}
