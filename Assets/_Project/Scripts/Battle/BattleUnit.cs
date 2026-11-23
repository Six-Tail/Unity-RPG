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

        // --- 속성 / 브레이크 / 상태효과 ---
        public readonly Element element;
        public readonly IReadOnlyList<Element> weaknesses;
        public int maxToughness;
        public int toughness;
        public bool isBroken;
        public readonly List<StatusEffect> effects = new();

        // 장비 등 추가 능력(StatBonus)
        public float penetration, allDmg, breakEffect, energyRegen, healBonus, effectHit, effectRes;
        public readonly float[] elementDmg = new float[5];
        public float ElementDmg(Element e) => (int)e >= 0 && (int)e < elementDmg.Length ? elementDmg[(int)e] : 0f;

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
            var b = data.bonus ?? new StatBonus();
            maxHP = b.ApplyHP(Mathf.Max(1, s.MaxHPAt(level)));
            currentHP = maxHP;
            atk = b.ApplyAtk(Mathf.Max(0, s.AtkAt(level)));
            def = b.ApplyDef(Mathf.Max(0, s.DefAt(level)));
            spd = b.ApplySpd(Mathf.Max(1, s.spd));
            critRate = Mathf.Clamp01(s.critRate + b.critRate);
            critDmg = Mathf.Max(0f, s.critDmg + b.critDmg);
            penetration = Mathf.Clamp(b.penetration, 0f, 0.8f);
            allDmg = b.allDmg;
            breakEffect = b.breakEffect;
            energyRegen = b.energyRegen;
            healBonus = b.healBonus;
            effectHit = b.effectHit;
            effectRes = Mathf.Clamp01(b.effectRes);
            for (int i = 0; i < elementDmg.Length; i++) elementDmg[i] = b.ElementDmg((Element)i);

            maxEnergy = Mathf.Max(1, cfg.maxEnergy);
            energy = Mathf.Clamp(data.startEnergy + b.startEnergy, 0, maxEnergy);

            // 스킬 목록(null 제거, 비면 기본 공격 자동 생성)
            var list = new List<BattleSkill>();
            if (data.skills != null)
                foreach (var sk in data.skills)
                    if (sk != null) list.Add(sk);
            if (list.Count == 0) list.Add(BattleSkill.CreateDefaultBasic());
            skills = list;

            // 속성 / 약점 / 내구도
            element = data.element;
            var weak = new List<Element>();
            if (data.weaknesses != null) weak.AddRange(data.weaknesses);
            weaknesses = weak;
            maxToughness = Mathf.Max(0, data.maxToughness);
            toughness = maxToughness;
            isBroken = false;

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

        // ---------------- 속성 / 브레이크 ----------------

        public bool HasBreakSystem => maxToughness > 0;
        public float ToughnessRatio => maxToughness > 0 ? (float)toughness / maxToughness : 0f;

        public bool IsWeakTo(Element e)
        {
            if (weaknesses == null) return false;
            foreach (var w in weaknesses) if (w == e) return true;
            return false;
        }

        /// <summary>내구도 감소. 방금 0이 되어 브레이크됐으면 true.</summary>
        public bool ReduceToughness(int amount)
        {
            if (!HasBreakSystem || isBroken || amount <= 0) return false;
            toughness = Mathf.Max(0, toughness - amount);
            if (toughness == 0) { isBroken = true; return true; }
            return false;
        }

        /// <summary>브레이크 회복(다음 자기 턴에).</summary>
        public void RecoverBreak()
        {
            if (isBroken) { isBroken = false; toughness = maxToughness; }
        }

        // ---------------- 상태효과(버프/디버프/도트) ----------------

        public int EffectiveAtk => Mathf.Max(0, Mathf.RoundToInt(atk * (1f + Modifier(StatusType.AtkUp) - Modifier(StatusType.AtkDown))));
        public int EffectiveDef => Mathf.Max(0, Mathf.RoundToInt(def * (1f + Modifier(StatusType.DefUp) - Modifier(StatusType.DefDown))));

        private float Modifier(StatusType t)
        {
            float sum = 0f;
            foreach (var e in effects) if (e.type == t) sum += e.magnitude;
            return sum;
        }

        public int BuffCount { get { int n = 0; foreach (var e in effects) if (e.IsBuff) n++; return n; } }
        public int DebuffCount { get { int n = 0; foreach (var e in effects) if (e.IsDebuff) n++; return n; } }

        public void ApplyEffect(StatusType type, float magnitude, int duration)
        {
            if (type == StatusType.None || duration <= 0) return;
            var existing = effects.Find(e => e.type == type);
            if (existing != null)
            {
                existing.magnitude = Mathf.Max(existing.magnitude, magnitude);
                existing.remaining = Mathf.Max(existing.remaining, duration);
            }
            else effects.Add(new StatusEffect(type, magnitude, duration));
        }

        /// <summary>턴 시작 처리: 도트 피해 + 효과 턴 감소/만료. 반환: 받은 도트 총량.</summary>
        public int TickTurnStart()
        {
            int dot = 0;
            foreach (var e in effects)
                if (e.type == StatusType.Dot) dot += Mathf.Max(0, Mathf.RoundToInt(e.magnitude));
            if (dot > 0) TakeDamage(dot);

            for (int i = effects.Count - 1; i >= 0; i--)
            {
                effects[i].remaining--;
                if (effects[i].remaining <= 0) effects.RemoveAt(i);
            }
            return dot;
        }
    }
}
