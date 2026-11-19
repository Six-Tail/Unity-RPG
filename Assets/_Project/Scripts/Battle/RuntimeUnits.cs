using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Battle
{
    /// <summary>
    /// CharacterData로부터 전투 유닛(BattleUnitData)을 얻습니다.
    /// 저작된 유닛이 있으면 그걸, 없으면 성급 기반으로 런타임 생성 → 뽑은 캐릭터가 바로 전투 가능.
    /// (Meta에 의존하지 않도록 저작 유닛 조회는 델리게이트로 주입)
    /// </summary>
    public static class RuntimeUnits
    {
        public static BattleUnitData FromCharacter(CharacterData c, int level,
            Func<CharacterData, BattleUnitData> authoredLookup = null)
        {
            if (authoredLookup != null)
            {
                var authored = authoredLookup(c);
                if (authored != null) return authored;
            }
            return Generate(c, level);
        }

        /// <summary>성급 기반 기본 유닛 생성(기본공격 + 스킬 + 필살기 포함).</summary>
        public static BattleUnitData Generate(CharacterData c, int level)
        {
            int r = c != null ? (int)c.rarity : 3;      // 3..6
            float mul = 1f + 0.35f * (r - 3);           // 3성=1.0 … 6성≈2.05

            var u = ScriptableObject.CreateInstance<BattleUnitData>();
            u.character = c;
            u.displayName = c != null && !string.IsNullOrEmpty(c.displayName) ? c.displayName : "유닛";
            u.level = Mathf.Max(1, level);
            u.baseStats = new BattleStats
            {
                maxHP = Mathf.RoundToInt(800 * mul),
                atk = Mathf.RoundToInt(90 * mul),
                def = Mathf.RoundToInt(45 * mul),
                spd = 95 + 5 * (r - 3),
                critRate = 0.05f,
                critDmg = 0.5f
            };
            u.skills = new List<BattleSkill>
            {
                MakeSkill("기본 공격", SkillCategory.Basic, TargetType.SingleEnemy, 1.0f, 20),
                MakeSkill("스킬", SkillCategory.Skill, TargetType.SingleEnemy, 1.8f, 30),
                MakeSkill("필살기", SkillCategory.Ultimate, TargetType.AllEnemies, 3.2f, 0),
            };
            return u;
        }

        private static BattleSkill MakeSkill(string name, SkillCategory cat, TargetType target, float power, int energyGain)
        {
            var s = ScriptableObject.CreateInstance<BattleSkill>();
            s.skillName = name;
            s.category = cat;
            s.target = target;
            s.power = power;
            s.hits = 1;
            s.energyGain = energyGain;
            return s;
        }
    }
}
