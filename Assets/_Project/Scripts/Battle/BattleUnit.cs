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

        /// <summary>이번 전투 누적 기록(준/받은 피해·회복 등).</summary>
        public readonly BattleRecord record = new();

        // --- 패시브(캐릭터 고유 · 초월 고유 능력) ---
        public readonly List<PassiveDef> passives = new();
        public int[] passiveUses = new int[0];          // 자기 턴 사이 발동 횟수(턴 시작에 초기화)
        public float dmgBasic, dmgSkill, dmgUlt, dmgVsBroken, dmgVsDebuffed, dmgVsLowHP, dmgTakenDown;
        public float stackAtk, stackCrit;               // 전투 중 쌓이는 보너스
        public PassiveDef surviveDef;                   // 버티기 패시브(있으면)
        public PassiveDef recastDef;                    // 연쇄 시전 패시브(있으면)
        public bool recastPending;                      // 다음 행동이 연쇄 시전
        public int recastChain;                         // 이번 연쇄에서 다시 시전한 횟수

        /// <summary>연쇄 시전 n번째의 피해 감소 비율(1번째 v, 2번째부터 v2).</summary>
        public float RecastPenalty(int n) =>
            recastDef == null ? 0f : Mathf.Clamp01(n <= 1 ? recastDef.v : recastDef.v2);
        public bool surviveUsed;
        private int _surviveHealed;

        // --- 턴 상태 ---
        public bool skipTurn;       // 이번 턴 행동 불가(기절)
        public bool inExtraTurn;    // 지금 턴이 추가 행동
        public bool extraQueued;    // 추가 행동 예약됨
        public int stunGuard;       // 기절 면역 남은 턴(연속 기절 방지)
        public readonly List<StatusEffect> effects = new();

        // 장비 등 추가 능력(StatBonus)
        public float penetration, allDmg, breakEffect, energyRegen, healBonus, effectHit, effectRes;
        public readonly float[] elementDmg = new float[5];
        public float ElementDmg(Element e) => (int)e >= 0 && (int)e < elementDmg.Length ? elementDmg[(int)e] : 0f;

        public bool IsAlive => currentHP > 0;
        // 이름은 전투 시작 때 저장 — 전투가 끝나면 정리되는 임시 유닛 데이터(주간 보스 등)도 전투 기록 창에서 이름 · 그림이 나오게
        private readonly string _name;
        public string Name => !string.IsNullOrEmpty(_name) ? _name : source != null ? source.DisplayName : "???";
        public float HPRatio => maxHP > 0 ? (float)currentHP / maxHP : 0f;
        public float EnergyRatio => maxEnergy > 0 ? (float)energy / maxEnergy : 0f;
        public bool UltimateReady => energy >= maxEnergy;

        public BattleUnit(BattleUnitData data, Team team, BattleConfig cfg)
        {
            source = data;
            character = data.character;
            _name = data.DisplayName;
            this.team = team;
            level = Mathf.Max(1, data.level);

            var s = data.baseStats;
            var b = new StatBonus();
            b.Add(data.bonus);

            // 패시브: 상시 능력치는 보너스에 합치고, 조건부 피해 보정은 따로 보관
            if (data.passives != null)
                foreach (var p in data.passives)
                {
                    if (p == null) continue;
                    passives.Add(p);
                    if (p.effect == PassiveEffect.Recast) recastDef = p;
                    if (p.trigger != PassiveTrigger.Static) continue;
                    switch (p.effect)
                    {
                        case PassiveEffect.AtkPct: b.atkPct += p.v; break;
                        case PassiveEffect.HpPct: b.hpPct += p.v; break;
                        case PassiveEffect.DefPct: b.defPct += p.v; break;
                        case PassiveEffect.SpdFlat: b.spd += p.v; break;
                        case PassiveEffect.CritRate: b.critRate += p.v; break;
                        case PassiveEffect.CritDmg: b.critDmg += p.v; break;
                        case PassiveEffect.Penetration: b.penetration += p.v; break;
                        case PassiveEffect.BreakEffect: b.breakEffect += p.v; break;
                        case PassiveEffect.HealBonus: b.healBonus += p.v; break;
                        case PassiveEffect.EffectHit: b.effectHit += p.v; break;
                        case PassiveEffect.EffectRes: b.effectRes += p.v; break;
                        case PassiveEffect.EnergyRegen: b.energyRegen += p.v; break;
                        case PassiveEffect.DmgBasic: dmgBasic += p.v; break;
                        case PassiveEffect.DmgSkill: dmgSkill += p.v; break;
                        case PassiveEffect.DmgUlt: dmgUlt += p.v; break;
                        case PassiveEffect.DmgVsBroken: dmgVsBroken += p.v; break;
                        case PassiveEffect.DmgVsDebuffed: dmgVsDebuffed += p.v; break;
                        case PassiveEffect.DmgVsLowHP: dmgVsLowHP += p.v; break;
                        case PassiveEffect.DmgTakenDown: dmgTakenDown += p.v; break;
                        case PassiveEffect.Survive: surviveDef = p; break;
                    }
                }
            passiveUses = new int[passives.Count];
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
            // 목걸이 '속성 피해'는 이 유닛의 속성에 더함
            if ((int)data.element >= 0 && (int)data.element < elementDmg.Length) elementDmg[(int)data.element] += b.wearerElementDmg;

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

        /// <summary>피해를 받음. 돌려주는 값 = 실제로 깎인 체력(남은 체력을 넘은 피해는 뺌).</summary>
        public int TakeDamage(int amount)
        {
            amount = Mathf.Max(0, amount);
            int real = Mathf.Min(amount, Mathf.Max(0, currentHP));
            bool lethal = currentHP > 0 && currentHP - amount <= 0;
            currentHP = Mathf.Max(0, currentHP - amount);
            if (lethal && surviveDef != null && !surviveUsed)
            {
                // 버티기: 전투당 1회 체력 1로 살아남고 회복
                surviveUsed = true;
                currentHP = 1;
                int before = currentHP;
                Heal(Mathf.RoundToInt(maxHP * surviveDef.v));
                _surviveHealed = Mathf.Max(1, currentHP - before);
            }
            return real;
        }

        /// <summary>방금 버티기가 발동했으면 true(회복량 반환). 한 번 읽으면 초기화.</summary>
        public bool ConsumeSurvived(out int healed)
        {
            healed = _surviveHealed;
            _surviveHealed = 0;
            return healed > 0;
        }

        public void ResetPassiveUses()
        {
            for (int i = 0; i < passiveUses.Length; i++) passiveUses[i] = 0;
        }

        /// <summary>회복. 돌려주는 값 = 실제로 채워진 체력(가득 찬 체력을 넘은 양은 뺌).</summary>
        public int Heal(int amount)
        {
            int before = currentHP;
            currentHP = Mathf.Min(maxHP, currentHP + Mathf.Max(0, amount));
            return currentHP - before;
        }

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

        public int EffectiveAtk => Mathf.Max(0, Mathf.RoundToInt(atk * (1f + Modifier(StatusType.AtkUp) - Modifier(StatusType.AtkDown) + stackAtk)));
        public int EffectiveSpd => Mathf.Max(1, Mathf.RoundToInt(spd * Mathf.Max(0.2f, 1f + Modifier(StatusType.SpdUp) - Modifier(StatusType.SpdDown))));
        public float EffectiveCritRate => Mathf.Clamp01(critRate + Modifier(StatusType.CritUp) + stackCrit);

        public bool HasStatus(StatusType t)
        {
            foreach (var e in effects) if (e.type == t && e.remaining > 0) return true;
            return false;
        }

        public bool IsTaunting => IsAlive && HasStatus(StatusType.Taunt);

        /// <summary>디버프 전부 해제. 해제한 개수 반환.</summary>
        public int Cleanse() => effects.RemoveAll(e => e.IsDebuff);
        public int EffectiveDef => Mathf.Max(0, Mathf.RoundToInt(def * (1f + Modifier(StatusType.DefUp) - Modifier(StatusType.DefDown))));

        private float Modifier(StatusType t)
        {
            float sum = 0f;
            foreach (var e in effects) if (e.type == t) sum += e.magnitude;
            return sum;
        }

        public int BuffCount { get { int n = 0; foreach (var e in effects) if (e.IsBuff) n++; return n; } }
        public int DebuffCount { get { int n = 0; foreach (var e in effects) if (e.IsDebuff) n++; return n; } }

        /// <summary>효과 부여(같은 종류는 더 큰 수치·더 긴 턴으로 갱신). 기절 면역 중이면 false.</summary>
        public bool ApplyEffect(StatusType type, float magnitude, int duration, BattleUnit source = null)
        {
            if (type == StatusType.None || duration <= 0) return false;
            if (type == StatusType.Stun && stunGuard > 0) return false;
            var existing = effects.Find(e => e.type == type);
            if (existing != null)
            {
                existing.magnitude = Mathf.Max(existing.magnitude, magnitude);
                existing.remaining = Mathf.Max(existing.remaining, duration);
                if (source != null) existing.source = source;
            }
            else effects.Add(new StatusEffect(type, magnitude, duration) { source = source });
            return true;
        }

        /// <summary>턴 시작 처리: 도트 피해 + 재생 회복 + 효과 턴 감소/만료. 반환: 받은 도트 총량(regen = 회복량).</summary>
        public int TickTurnStart(out int regen)
        {
            regen = 0;
            int dot = 0;
            bool wasAlive = IsAlive;
            BattleUnit lastSource = null;
            foreach (var e in effects)
            {
                if (e.type != StatusType.Dot) continue;
                dot += Mathf.Max(0, Mathf.RoundToInt(e.magnitude));
                if (e.source != null) lastSource = e.source;
            }
            if (dot > 0)
            {
                int real = TakeDamage(dot);
                record.taken += real;
                // 실제로 깎인 양을 도트를 건 유닛들에게 비율대로 나눔
                foreach (var e in effects)
                {
                    if (e.type != StatusType.Dot || e.source == null) continue;
                    int d = Mathf.Max(0, Mathf.RoundToInt(e.magnitude));
                    e.source.record.dealt += (long)d * real / dot;
                }
                if (wasAlive && !IsAlive && lastSource != null) lastSource.record.kills++;
            }

            // 재생(살아 있을 때만)
            if (IsAlive)
            {
                int before = currentHP;
                foreach (var e in effects)
                {
                    if (e.type != StatusType.Regen) continue;
                    int h = Mathf.Max(0, Mathf.RoundToInt(e.magnitude));
                    int got = Heal(h);
                    if (e.source != null) e.source.record.healed += got;
                    record.healReceived += got;
                    regen += h;
                }
                if (currentHP == before && regen > 0 && before >= maxHP) regen = 0;   // 이미 가득이면 표시 안 함
            }

            for (int i = effects.Count - 1; i >= 0; i--)
            {
                effects[i].remaining--;
                if (effects[i].remaining <= 0) effects.RemoveAt(i);
            }
            return dot;
        }
    }
}
