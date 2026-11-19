using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubcultureRPG.Battle
{
    /// <summary>
    /// 턴제 전투 엔진(순수 로직). 속도 기반 행동 순서(Action Value)·행동 처리·간단 AI·승패.
    /// MonoBehaviour가 아니라 테스트가 쉽고, RNG 주입으로 결정적 검증이 가능합니다.
    /// 시간 흐름(연출·입력 대기)은 BattleController가 담당.
    /// </summary>
    public class BattleEngine
    {
        public readonly List<BattleUnit> allies = new();
        public readonly List<BattleUnit> enemies = new();

        private readonly BattleConfig _cfg;
        private readonly Func<float> _roll;

        private int _allySP;
        private int _enemySP;

        public bool IsOver => !AnyAlive(allies) || !AnyAlive(enemies);
        public Team? Winner => !IsOver ? (Team?)null : (AnyAlive(allies) ? Team.Ally : Team.Enemy);

        public BattleEngine(IEnumerable<BattleUnitData> allyData, IEnumerable<BattleUnitData> enemyData,
            BattleConfig cfg = null, Func<float> roll01 = null)
        {
            _cfg = cfg != null ? cfg : BattleConfig.CreateDefault();
            _roll = roll01 ?? (() => UnityEngine.Random.value);

            if (allyData != null)
                foreach (var d in allyData) if (d != null) allies.Add(new BattleUnit(d, Team.Ally, _cfg));
            if (enemyData != null)
                foreach (var d in enemyData) if (d != null) enemies.Add(new BattleUnit(d, Team.Enemy, _cfg));

            _allySP = Mathf.Clamp(_cfg.startSkillPoints, 0, _cfg.maxSkillPoints);
            _enemySP = _allySP;
        }

        public IEnumerable<BattleUnit> AllUnits()
        {
            foreach (var u in allies) yield return u;
            foreach (var u in enemies) yield return u;
        }

        public List<BattleUnit> Opponents(BattleUnit u) => u.team == Team.Ally ? enemies : allies;
        public List<BattleUnit> Teammates(BattleUnit u) => u.team == Team.Ally ? allies : enemies;

        // ---------------- 스킬 포인트 (팀 공유) ----------------

        public int MaxSkillPoints => _cfg.maxSkillPoints;
        public int AllySkillPoints => _allySP;
        public int EnemySkillPoints => _enemySP;
        public int SkillPointsOf(Team t) => t == Team.Ally ? _allySP : _enemySP;

        /// <summary>이 스킬을 지금 쓸 수 있는가(필살기=에너지 가득, 스킬=SP≥1, 기본=항상).</summary>
        public bool CanUse(BattleUnit actor, BattleSkill skill)
        {
            if (skill == null) return false;
            if (skill.category == SkillCategory.Ultimate) return actor.UltimateReady;
            if (skill.category == SkillCategory.Skill) return SkillPointsOf(actor.team) >= 1;
            return true;
        }

        private void AddSkillPoints(Team t, int delta)
        {
            if (t == Team.Ally) _allySP = Mathf.Clamp(_allySP + delta, 0, _cfg.maxSkillPoints);
            else _enemySP = Mathf.Clamp(_enemySP + delta, 0, _cfg.maxSkillPoints);
        }

        // ---------------- 행동 순서 (Action Value) ----------------

        /// <summary>
        /// 다음 행동 유닛을 반환하면서, 전체 시간을 전진시키고 해당 유닛의 다음 차례를 예약합니다.
        /// (가장 행동치가 낮은 유닛이 즉시 행동 → actionValue = base / SPD 로 재설정)
        /// </summary>
        public BattleUnit NextActor()
        {
            BattleUnit actor = null;
            float min = float.MaxValue;
            foreach (var u in AllUnits())
                if (u.IsAlive && u.actionValue < min) { min = u.actionValue; actor = u; }
            if (actor == null) return null;

            foreach (var u in AllUnits())
                if (u.IsAlive) u.actionValue -= min;   // 시간 전진

            actor.actionValue = _cfg.baseActionValue / Mathf.Max(1, actor.spd); // 다음 차례 예약
            return actor;
        }

        /// <summary>앞으로의 행동 순서를 미리보기(상태 변경 없음). UI 턴 순서 바에 사용.</summary>
        public List<BattleUnit> PreviewOrder(int count)
        {
            var sim = new List<KeyValuePair<BattleUnit, float>>();
            foreach (var u in AllUnits())
                if (u.IsAlive) sim.Add(new KeyValuePair<BattleUnit, float>(u, u.actionValue));

            var order = new List<BattleUnit>();
            for (int i = 0; i < count && sim.Count > 0; i++)
            {
                int idx = 0;
                for (int j = 1; j < sim.Count; j++)
                    if (sim[j].Value < sim[idx].Value) idx = j;

                var cur = sim[idx];
                float min = cur.Value;
                for (int j = 0; j < sim.Count; j++)
                    sim[j] = new KeyValuePair<BattleUnit, float>(sim[j].Key, sim[j].Value - min);

                sim[idx] = new KeyValuePair<BattleUnit, float>(cur.Key, _cfg.baseActionValue / Mathf.Max(1, cur.Key.spd));
                order.Add(cur.Key);
            }
            return order;
        }

        // ---------------- 턴 시작 처리 ----------------

        /// <summary>유닛 턴 시작: 브레이크 회복 + 도트/효과 틱. 반환: 이번 틱에 받은 도트 피해.</summary>
        public int StartTurn(BattleUnit actor)
        {
            actor.RecoverBreak();
            return actor.TickTurnStart();
        }

        // ---------------- 행동 처리 ----------------

        public ActionResult ResolveAction(BattleUnit actor, BattleSkill skill, BattleUnit primaryTarget)
        {
            if (skill == null) skill = actor.BasicSkill;
            var targets = ResolveTargets(actor, skill, primaryTarget);

            var result = new ActionResult
            {
                actor = actor,
                skill = skill,
                hits = new List<ActionResult.TargetHit>()
            };

            bool heal = skill.IsHeal && skill.TargetsAllies;

            foreach (var target in targets)
            {
                if (target == null || !target.IsAlive) continue;
                var th = new ActionResult.TargetHit { target = target };

                if (heal)
                {
                    int amount = Mathf.Max(1, Mathf.RoundToInt(actor.EffectiveAtk * skill.heal));
                    target.Heal(amount);
                    th.healed = amount;
                }
                else if (!skill.TargetsAllies)
                {
                    int total = 0;
                    bool anyCrit = false, anyWeak = false;
                    for (int h = 0; h < Mathf.Max(1, skill.hits); h++)
                    {
                        if (!target.IsAlive) break;
                        var hit = DamageCalculator.Compute(actor, target, skill, _roll(), _cfg);
                        target.TakeDamage(hit.damage);
                        target.GainEnergy(_cfg.energyOnHit);
                        total += hit.damage;
                        anyCrit |= hit.crit;
                        anyWeak |= hit.weakness;

                        // 약점 → 내구도 감소 → 브레이크
                        if (hit.weakness && target.ReduceToughness(skill.breakDamage))
                        {
                            int bonus = Mathf.Max(1, Mathf.RoundToInt(hit.damage * Mathf.Max(0f, _cfg.breakBonus)));
                            target.TakeDamage(bonus);
                            total += bonus;
                            target.actionValue += _cfg.breakActionDelay; // 다음 행동 지연
                            th.broke = true;
                        }
                    }
                    th.totalDamage = total;
                    th.anyCrit = anyCrit;
                    th.weakness = anyWeak;
                    th.defeated = !target.IsAlive;
                }

                // 상태효과 부여(디버프는 적 대상, 버프는 아군 대상)
                if (skill.applyEffect != StatusType.None && target.IsAlive && _roll() < skill.effectChance)
                {
                    float mag = skill.applyEffect == StatusType.Dot
                        ? actor.EffectiveAtk * skill.effectMagnitude
                        : skill.effectMagnitude;
                    target.ApplyEffect(skill.applyEffect, mag, skill.effectDuration);
                    th.appliedEffect = skill.applyEffect;
                }

                result.hits.Add(th);
            }

            // 시전자 에너지 정산
            if (skill.IsUltimate) actor.ResetEnergy();
            else actor.GainEnergy(skill.energyGain);
            result.energyAfter = actor.energy;

            // 스킬 포인트: 기본 +1, 스킬 -1 (필살기는 변화 없음)
            if (skill.category == SkillCategory.Skill) AddSkillPoints(actor.team, -1);
            else if (skill.category == SkillCategory.Basic) AddSkillPoints(actor.team, +1);

            return result;
        }

        private List<BattleUnit> ResolveTargets(BattleUnit actor, BattleSkill skill, BattleUnit primary)
        {
            var list = new List<BattleUnit>();

            switch (skill.target)
            {
                case TargetType.AllEnemies:
                    foreach (var u in Opponents(actor)) if (u.IsAlive) list.Add(u);
                    break;

                case TargetType.AllAllies:
                    foreach (var u in Teammates(actor)) if (u.IsAlive) list.Add(u);
                    break;

                case TargetType.Self:
                    list.Add(actor);
                    break;

                case TargetType.SingleAlly:
                    if (primary != null && primary.IsAlive && primary.team == actor.team) list.Add(primary);
                    else { var t = LowestHPRatio(Teammates(actor)); if (t != null) list.Add(t); }
                    break;

                default: // SingleEnemy
                    if (primary != null && primary.IsAlive && primary.team != actor.team) list.Add(primary);
                    else { var t = FirstAlive(Opponents(actor)); if (t != null) list.Add(t); }
                    break;
            }
            return list;
        }

        // ---------------- 간단 AI (적/자동 전투) ----------------

        /// <summary>행동 선택: 필살기 준비되면 우선, 아니면 가장 강한 스킬, 없으면 기본 공격. 대상은 최저 체력 적.</summary>
        public (BattleSkill skill, BattleUnit target) PickAction(BattleUnit actor)
        {
            BattleSkill chosen = actor.BasicSkill;

            if (actor.UltimateReady && HasUltimate(actor, out var ult))
            {
                chosen = ult;
            }
            else if (SkillPointsOf(actor.team) >= 1)
            {
                // SP가 있으면 가장 강한 스킬, 없으면 기본 공격(=SP 적립)
                float best = -1f;
                foreach (var s in actor.skills)
                    if (s.category == SkillCategory.Skill && s.power > best) { chosen = s; best = s.power; }
            }

            var target = LowestHP(Opponents(actor));
            return (chosen, target);
        }

        private static bool HasUltimate(BattleUnit actor, out BattleSkill ult)
        {
            foreach (var s in actor.skills)
                if (s.IsUltimate) { ult = s; return true; }
            ult = null;
            return false;
        }

        // ---------------- 헬퍼 ----------------

        private static bool AnyAlive(List<BattleUnit> list)
        {
            foreach (var u in list) if (u.IsAlive) return true;
            return false;
        }

        private static BattleUnit FirstAlive(List<BattleUnit> list)
        {
            foreach (var u in list) if (u.IsAlive) return u;
            return null;
        }

        public static BattleUnit LowestHP(List<BattleUnit> list)
        {
            BattleUnit best = null;
            foreach (var u in list)
                if (u.IsAlive && (best == null || u.currentHP < best.currentHP)) best = u;
            return best;
        }

        private static BattleUnit LowestHPRatio(List<BattleUnit> list)
        {
            BattleUnit best = null;
            foreach (var u in list)
                if (u.IsAlive && (best == null || u.HPRatio < best.HPRatio)) best = u;
            return best;
        }
    }
}
