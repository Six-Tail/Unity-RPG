using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Battle
{
    /// <summary>
    /// 전투 중 유닛 1명의 '살아있는' 상태. (순수 로직, MonoBehaviour 아님)
    /// BattleUnitData를 레벨에 맞춰 해석해 현재 수치를 가집니다.
    /// </summary>
    public class BattleUnit
    {
        public readonly BattleUnitData source;
        public readonly CharacterData character;
        public readonly Team team;
        public readonly int level;
        public readonly IReadOnlyList<BattleSkill> skills;

        public int maxHP;
        public int currentHP;
        public int atk;
        public int def;
        public int spd;
        public float critRate;
        public float critDmg;

        public int energy;
        public readonly int maxEnergy;

        /// <summary>행동치(낮을수록 먼저 행동). 엔진이 관리.</summary>
        public float actionValue;

        public bool IsAlive => currentHP > 0;
        public string Name => source != null ? source.DisplayName : "???";
        public float HPRatio => maxHP > 0 ? (float)currentHP / maxHP : 0f;
        public float EnergyRatio => maxEnergy > 0 ? (float)energy / maxEnergy : 0f;
        public bool UltimateReady => energy >= maxEnergy;

        public BattleUnit(BattleUnitData data, Team team, BattleConfig cfg)
        {
            source = data;
            character = data.character;
            this.team = team;
            level = Mathf.Max(1, data.level);

            var s = data.baseStats;
            maxHP = Mathf.Max(1, s.MaxHPAt(level));
            currentHP = maxHP;
            atk = Mathf.Max(0, s.AtkAt(level));
            def = Mathf.Max(0, s.DefAt(level));
            spd = Mathf.Max(1, s.spd);
            critRate = Mathf.Clamp01(s.critRate);
            critDmg = Mathf.Max(0f, s.critDmg);

            maxEnergy = Mathf.Max(1, cfg.maxEnergy);
            energy = 0;

            // 스킬 목록(null 제거, 비면 기본 공격 자동 생성)
            var list = new List<BattleSkill>();
            if (data.skills != null)
                foreach (var sk in data.skills)
                    if (sk != null) list.Add(sk);
            if (list.Count == 0) list.Add(BattleSkill.CreateDefaultBasic());
            skills = list;

            actionValue = cfg.baseActionValue / spd;
        }

        public int TakeDamage(int amount)
        {
            amount = Mathf.Max(0, amount);
            currentHP = Mathf.Max(0, currentHP - amount);
            return amount;
        }

        public void Heal(int amount) => currentHP = Mathf.Min(maxHP, currentHP + Mathf.Max(0, amount));

        public void GainEnergy(int amount) => energy = Mathf.Clamp(energy + amount, 0, maxEnergy);

        public void ResetEnergy() => energy = 0;

        /// <summary>기본 공격(없으면 첫 스킬). 항상 하나는 보장됨.</summary>
        public BattleSkill BasicSkill
        {
            get
            {
                foreach (var s in skills)
                    if (s.category == SkillCategory.Basic) return s;
                return skills[0];
            }
        }
    }
}
