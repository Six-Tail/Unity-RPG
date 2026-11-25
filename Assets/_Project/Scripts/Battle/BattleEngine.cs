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
        // ---------------- 턴(라운드) ----------------
        // 살아 있는 모든 아군 · 적이 한 번 이상 행동하면 1턴이 지나감(빠른 유닛은 한 턴에 여러 번 행동할 수 있음).
        private readonly HashSet<BattleUnit> _actedThisRound = new();
        private bool _roundDone;

        /// <summary>지금 턴 번호(1부터).</summary>
        public int Round { get; private set; } = 1;

        /// <summary>턴이 바뀔 때(새 턴 번호).</summary>
        public event System.Action<int> RoundChanged;

        private void TrackRound(BattleUnit actor)
        {
            if (_roundDone)
            {
                _roundDone = false;
                _actedThisRound.Clear();
                Round++;
                RoundChanged?.Invoke(Round);
            }
            _actedThisRound.Add(actor);
            foreach (var u in AllUnits())
                if (u.IsAlive && !_actedThisRound.Contains(u)) return;
            _roundDone = true;   // 모두 행동함 → 다음 행동부터 새 턴
        }

        public BattleUnit NextActor()
        {
            BattleUnit actor = null;
            float min = float.MaxValue;
            foreach (var u in AllUnits())
                if (u.IsAlive && u.actionValue < min) { min = u.actionValue; actor = u; }
            if (actor == null) return null;

            foreach (var u in AllUnits())
                if (u.IsAlive) u.actionValue -= min;   // 시간 전진

            actor.actionValue = _cfg.baseActionValue / Mathf.Max(1, actor.EffectiveSpd); // 다음 차례 예약(속도 버프 반영)
            TrackRound(actor);
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

                sim[idx] = new KeyValuePair<BattleUnit, float>(cur.Key, _cfg.baseActionValue / Mathf.Max(1, cur.Key.EffectiveSpd));
                order.Add(cur.Key);
            }
            return order;
        }

        // ---------------- 턴 시작 처리 ----------------

        /// <summary>유닛 턴 시작: 브레이크 회복 + 도트/효과 틱. 반환: 이번 틱에 받은 도트 피해.</summary>
        public int StartTurn(BattleUnit actor)
        {
            actor.skipTurn = false;
            actor.recastPending = false;
            actor.recastChain = 0;
            actor.inExtraTurn = actor.extraQueued;
            actor.extraQueued = false;
            if (actor.inExtraTurn) return 0;   // 추가 행동: 효과 턴·지속 피해는 진행하지 않음

            actor.ResetPassiveUses();
            actor.RecoverBreak();
            bool stunned = actor.HasStatus(StatusType.Stun);
            int dot = actor.TickTurnStart(out int regen);
            if (regen > 0)
                _extras.Add(new ActionResult.TargetHit { target = actor, healed = regen, extra = true, label = "재생", source = actor });

            if (stunned && actor.IsAlive) { actor.skipTurn = true; actor.stunGuard = 2; }   // 기절 후 2턴은 다시 기절하지 않음
            else if (actor.stunGuard > 0) actor.stunGuard--;

            if (actor.IsAlive && !actor.skipTurn) Fire(actor, PassiveTrigger.TurnStart, null);
            return dot;
        }

        /// <summary>전투 시작 패시브 발동(컨트롤러가 OnBattleStart 알림 뒤에 한 번 호출).</summary>
        public void StartBattle()
        {
            if (_started) return;
            _started = true;
            foreach (var u in allies) Fire(u, PassiveTrigger.BattleStart, null);
            foreach (var u in enemies) Fire(u, PassiveTrigger.BattleStart, null);
        }

        private bool _started;

        // ---------------- 패시브 ----------------

        /// <summary>패시브가 발동했을 때(유닛, 효과). UI 알림용.</summary>
        public event Action<BattleUnit, PassiveDef> PassiveTriggered;

        /// <summary>행동 밖에서 생긴 부가 결과(턴 시작 회복 등)와 행동 중 쌓인 부가 결과.</summary>
        private readonly List<ActionResult.TargetHit> _extras = new();

        /// <summary>쌓인 부가 결과를 꺼냄(행동 밖: 턴 시작·전투 시작 뒤 컨트롤러가 호출).</summary>
        public List<ActionResult.TargetHit> DrainExtras()
        {
            var list = new List<ActionResult.TargetHit>(_extras);
            _extras.Clear();
            return list;
        }

        /// <summary>owner의 trigger 패시브를 모두 발동. other = 관련 유닛(공격 대상 · 피격 시 공격자).</summary>
        private void Fire(BattleUnit owner, PassiveTrigger trigger, BattleUnit other)
        {
            if (owner == null || !owner.IsAlive) return;
            for (int i = 0; i < owner.passives.Count; i++)
            {
                var p = owner.passives[i];
                if (p.trigger != trigger) continue;
                if (p.limit > 0 && owner.passiveUses[i] >= p.limit) continue;
                if (p.chance < 0.999f && _roll() >= p.chance) continue;
                if (!Execute(owner, p, other)) continue;
                owner.passiveUses[i]++;
                PassiveTriggered?.Invoke(owner, p);
            }
        }

        private bool ApplyStatus(BattleUnit owner, BattleUnit target, PassiveDef p)
        {
            if (target == null || !target.IsAlive) return false;
            float mag = p.status == StatusType.Dot || p.status == StatusType.Regen ? owner.EffectiveAtk * p.v : p.v;
            return target.ApplyEffect(p.status, mag, p.turns, owner);
        }

        /// <summary>회복 적용 + 기록(실제로 채워진 양) + 표시용 항목(계산된 양).</summary>
        private ActionResult.TargetHit HealEntry(BattleUnit source, BattleUnit target, int amount, string label)
        {
            int got = target.Heal(amount);
            source.record.healed += got;
            target.record.healReceived += got;
            return new ActionResult.TargetHit { target = target, healed = amount, extra = true, label = label, source = source };
        }

        /// <summary>피해 적용 + 기록 + 버티기 처리.</summary>
        private void Damage(BattleUnit source, BattleUnit target, int amount)
        {
            int real = target.TakeDamage(amount);   // 실제로 깎인 체력(남은 체력을 넘은 피해는 빼고 기록)
            if (source != null) source.record.dealt += real;
            target.record.taken += real;
            if (target.ConsumeSurvived(out int healed))
            {
                _extras.Add(new ActionResult.TargetHit { target = target, healed = healed, extra = true, label = target.surviveDef.name, source = target });
                target.record.healReceived += healed;
                PassiveTriggered?.Invoke(target, target.surviveDef);
            }
        }

        private bool Execute(BattleUnit owner, PassiveDef p, BattleUnit other)
        {
            switch (p.effect)
            {
                case PassiveEffect.ExtraTurn:
                    if (owner.inExtraTurn || owner.extraQueued) return false;   // 추가 행동 중에는 다시 얻지 못함
                    owner.extraQueued = true;
                    owner.actionValue = 0f;
                    return true;

                case PassiveEffect.Energy:
                    if (owner.UltimateReady) return false;
                    owner.GainEnergy(Mathf.RoundToInt(p.v));
                    return true;

                case PassiveEffect.TeamEnergy:
                    foreach (var u in Teammates(owner)) if (u.IsAlive) u.GainEnergy(Mathf.RoundToInt(p.v));
                    return true;

                case PassiveEffect.SkillPoint:
                    if (SkillPointsOf(owner.team) >= _cfg.maxSkillPoints) return false;
                    AddSkillPoints(owner.team, Mathf.RoundToInt(p.v));
                    return true;

                case PassiveEffect.HealSelf:
                {
                    if (owner.currentHP >= owner.maxHP) return false;
                    int amt = Mathf.Max(1, Mathf.RoundToInt(owner.maxHP * p.v));
                    _extras.Add(HealEntry(owner, owner, amt, p.name));
                    return true;
                }

                case PassiveEffect.HealLowest:
                {
                    var t = LowestHPRatio(Teammates(owner));
                    if (t == null || t.currentHP >= t.maxHP) return false;
                    int amt = Mathf.Max(1, Mathf.RoundToInt(owner.EffectiveAtk * p.v * (1f + owner.healBonus)));
                    _extras.Add(HealEntry(owner, t, amt, p.name));
                    return true;
                }

                case PassiveEffect.HealTeam:
                {
                    bool any = false;
                    int amt = Mathf.Max(1, Mathf.RoundToInt(owner.EffectiveAtk * p.v * (1f + owner.healBonus)));
                    foreach (var u in Teammates(owner))
                    {
                        if (!u.IsAlive || u.currentHP >= u.maxHP) continue;
                        _extras.Add(HealEntry(owner, u, amt, p.name));
                        any = true;
                    }
                    return any;
                }

                case PassiveEffect.StatusSelf:
                    return ApplyStatus(owner, owner, p);

                case PassiveEffect.StatusTeam:
                {
                    bool any = false;
                    foreach (var u in Teammates(owner)) any |= ApplyStatus(owner, u, p);
                    return any;
                }

                case PassiveEffect.StatusTarget:
                    return other != null && other.team != owner.team && ApplyStatus(owner, other, p);

                case PassiveEffect.StatusEnemies:
                {
                    bool any = false;
                    foreach (var u in Opponents(owner)) any |= ApplyStatus(owner, u, p);
                    return any;
                }

                case PassiveEffect.FollowUp:
                {
                    var t = other != null && other.IsAlive && other.team != owner.team ? other : LowestHP(Opponents(owner));
                    if (t == null) return false;
                    var hit = DamageCalculator.Compute(owner, t, p.v, owner.element, SkillCategory.Basic, _roll(), _cfg, false);
                    Damage(owner, t, hit.damage);
                    if (hit.crit) owner.record.crits++;
                    bool dead = !t.IsAlive;
                    if (dead) owner.record.kills++;
                    _extras.Add(new ActionResult.TargetHit
                    {
                        target = t, totalDamage = hit.damage, anyCrit = hit.crit, weakness = hit.weakness, defeated = dead,
                        extra = true, label = p.trigger == PassiveTrigger.OnDamaged ? "반격" : "추가 공격", source = owner,
                        hitDamages = new List<int> { hit.damage }, hitCrits = new List<bool> { hit.crit },
                    });
                    return true;
                }

                case PassiveEffect.AdvanceSelf:
                    owner.actionValue *= 1f - Mathf.Clamp01(p.v);
                    return true;

                case PassiveEffect.AdvanceTeam:
                    foreach (var u in Teammates(owner)) if (u.IsAlive) u.actionValue *= 1f - Mathf.Clamp01(p.v);
                    return true;

                case PassiveEffect.StackAtk:
                    if (owner.stackAtk >= p.v * p.v2 - 0.0001f) return false;
                    owner.stackAtk += p.v;
                    return true;

                case PassiveEffect.StackCrit:
                    if (owner.stackCrit >= p.v * p.v2 - 0.0001f) return false;
                    owner.stackCrit += p.v;
                    return true;
            }
            return false;   // 상시 효과는 유닛 생성 때 이미 반영
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
            bool attack = !skill.TargetsAllies;
            actor.record.actions++;

            // 연쇄 시전(처치 → 스킬 재시전): SP를 쓰지 않고, 재시전할수록 피해가 줄어듦
            bool isRecast = actor.recastPending;
            actor.recastPending = false;
            float actionMul = 1f;
            if (isRecast)
            {
                actor.recastChain++;
                actionMul = 1f - actor.RecastPenalty(actor.recastChain);
                result.recast = actor.recastChain;
            }
            result.recastMul = actionMul;

            // 대상별 결과(무작위 연타는 같은 대상이 여러 번 맞을 수 있어 대상 기준으로 모음)
            var order = new List<BattleUnit>();
            var acc = new Dictionary<BattleUnit, ActionResult.TargetHit>();
            ActionResult.TargetHit Get(BattleUnit t)
            {
                if (!acc.TryGetValue(t, out var th))
                {
                    th = new ActionResult.TargetHit { target = t, hitDamages = new List<int>(), hitCrits = new List<bool>() };
                    order.Add(t);
                }
                return th;
            }

            BattleUnit first = targets.Count > 0 ? targets[0] : null;   // 주 대상
            int hits = Mathf.Max(1, skill.hits);

            if (heal)
            {
                foreach (var target in targets)
                {
                    if (target == null || !target.IsAlive) continue;
                    var th = Get(target);
                    int amount = Mathf.Max(1, Mathf.RoundToInt(actor.EffectiveAtk * skill.heal * (1f + actor.healBonus)));
                    int got = target.Heal(amount);
                    actor.record.healed += got;
                    target.record.healReceived += got;
                    th.healed = amount;
                    acc[target] = th;
                }
            }
            else if (attack && skill.target == TargetType.RandomEnemies)
            {
                // 연타: 타격마다 살아 있는 적 중 무작위
                for (int h = 0; h < hits; h++)
                {
                    var t = RandomAlive(Opponents(actor));
                    if (t == null) break;
                    var th = Get(t);
                    Strike(actor, t, skill, actionMul, ref th);
                    acc[t] = th;
                }
                first = order.Count > 0 ? order[0] : first;
            }
            else if (attack)
            {
                foreach (var target in targets)
                {
                    if (target == null || !target.IsAlive) continue;
                    // 확산: 양옆은 splash 배율
                    float mul = skill.target == TargetType.Blast && target != first ? Mathf.Max(0f, skill.splash) : 1f;
                    var th = Get(target);
                    for (int h = 0; h < hits; h++)
                    {
                        if (!target.IsAlive) break;
                        Strike(actor, target, skill, mul * actionMul, ref th);
                    }
                    acc[target] = th;
                }
            }
            else
            {
                foreach (var target in targets)
                    if (target != null && target.IsAlive) acc[target] = Get(target);
            }

            long dealt = 0;
            bool anyKill = false, anyCrit = false, anyWeak = false, anyBroke = false;
            BattleUnit killed = null, broken = null;

            foreach (var target in order)
            {
                var th = acc[target];
                if (attack)
                {
                    th.defeated = !target.IsAlive;
                    if (th.defeated) { actor.record.kills++; anyKill = true; killed = target; }
                    anyCrit |= th.anyCrit;
                    anyWeak |= th.weakness;
                    if (th.broke) { anyBroke = true; broken = target; }
                    dealt += th.totalDamage;
                }

                // 상태효과 부여(디버프는 적 대상, 버프는 아군 대상)
                // 적에게 거는 효과: 효과 명중 ↑ · 대상 효과 저항 ↓
                float chance = target.team != actor.team
                    ? skill.effectChance * (1f + actor.effectHit) * (1f - target.effectRes)
                    : skill.effectChance;
                if (skill.applyEffect != StatusType.None && target.IsAlive && _roll() < chance)
                {
                    float mag = skill.applyEffect == StatusType.Dot || skill.applyEffect == StatusType.Regen
                        ? actor.EffectiveAtk * skill.effectMagnitude
                        : skill.effectMagnitude;
                    if (target.ApplyEffect(skill.applyEffect, mag, skill.effectDuration, actor))
                        th.appliedEffect = skill.applyEffect;
                }

                // 아군 대상: 행동 당김 · 디버프 해제
                if (skill.TargetsAllies && target.IsAlive)
                {
                    if (skill.advance > 0f) target.actionValue *= 1f - Mathf.Clamp01(skill.advance);
                    if (skill.cleanse) target.Cleanse();
                }

                result.hits.Add(th);
            }

            // 시전자 에너지 정산
            if (skill.IsUltimate) actor.ResetEnergy();
            else actor.GainEnergy(Mathf.RoundToInt(skill.energyGain * (1f + actor.energyRegen)));   // 에너지 회복 효율

            // 스킬 포인트: 기본 +1, 스킬 -1 (필살기는 변화 없음)
            if (skill.category == SkillCategory.Skill) { if (!isRecast) AddSkillPoints(actor.team, -1); }
            else if (skill.category == SkillCategory.Basic) AddSkillPoints(actor.team, +1);

            if (actor.IsAlive)
            {
                // 자신에게 거는 효과
                if (skill.selfEffect != StatusType.None)
                {
                    float mag = skill.selfEffect == StatusType.Dot || skill.selfEffect == StatusType.Regen
                        ? actor.EffectiveAtk * skill.selfEffectMagnitude
                        : skill.selfEffectMagnitude;
                    actor.ApplyEffect(skill.selfEffect, mag, skill.selfEffectDuration, actor);
                }

                // 흡혈
                if (attack && skill.lifesteal > 0f && dealt > 0)
                {
                    int amt = Mathf.Max(1, Mathf.RoundToInt(dealt * skill.lifesteal));
                    _extras.Add(HealEntry(actor, actor, amt, "흡혈"));
                }

                // 패시브(행동자)
                if (anyKill) Fire(actor, PassiveTrigger.OnKill, killed);
                if (anyCrit) Fire(actor, PassiveTrigger.OnCrit, first);
                if (anyBroke) Fire(actor, PassiveTrigger.OnBreak, broken);
                if (anyWeak) Fire(actor, PassiveTrigger.OnWeakHit, first);
                Fire(actor, skill.category == SkillCategory.Basic ? PassiveTrigger.AfterBasic
                    : skill.category == SkillCategory.Skill ? PassiveTrigger.AfterSkill : PassiveTrigger.AfterUltimate, first);
                if (attack && dealt > 0) Fire(actor, PassiveTrigger.AfterAttack, first);
                if (heal) Fire(actor, PassiveTrigger.AfterHeal, first);
            }

            // 패시브(맞은 쪽)
            if (attack)
                foreach (var target in order)
                    if (target.IsAlive && acc[target].totalDamage > 0) Fire(target, PassiveTrigger.OnDamaged, actor);

            // 연쇄 시전 예약: 처치했고 연쇄 횟수가 남았으면 한 번 더, 아니면 연쇄 끝(피해 감소 초기화)
            if (actor.recastDef != null)
            {
                if (actor.IsAlive && anyKill && !IsOver && actor.recastChain < Mathf.Max(0, actor.recastDef.limit) && RecastSkill(actor) != null)
                {
                    actor.recastPending = true;
                    PassiveTriggered?.Invoke(actor, actor.recastDef);
                }
                else actor.recastChain = 0;
            }

            result.energyAfter = actor.energy;

            // 부가 결과(추가 공격·반격·흡혈·패시브 회복)를 뒤에 붙임
            result.hits.AddRange(_extras);
            _extras.Clear();
            return result;
        }

        private static BattleSkill RecastSkill(BattleUnit actor)
        {
            foreach (var s in actor.skills)
                if (s.category == SkillCategory.Skill && !s.TargetsAllies && s.power > 0f) return s;
            return null;
        }

        /// <summary>
        /// 연쇄 시전이 예약돼 있으면 쓸 스킬과 대상(체력이 가장 낮은 적)을 돌려줌.
        /// 컨트롤러가 행동 직후 반복해서 확인 → ResolveAction을 다시 호출.
        /// </summary>
        public bool NextRecast(BattleUnit actor, out BattleSkill skill, out BattleUnit target)
        {
            skill = null; target = null;
            if (actor == null || !actor.recastPending) return false;
            if (!actor.IsAlive || IsOver) { actor.recastPending = false; actor.recastChain = 0; return false; }
            skill = RecastSkill(actor);
            target = LowestHP(Opponents(actor));
            if (skill == null || target == null) { actor.recastPending = false; actor.recastChain = 0; return false; }
            return true;
        }

        /// <summary>타격 1회: 피해 + 기록 + 브레이크 게이지.</summary>
        private void Strike(BattleUnit actor, BattleUnit target, BattleSkill skill, float mul, ref ActionResult.TargetHit th)
        {
            var hit = DamageCalculator.Compute(actor, target, skill, _roll(), _cfg, mul);
            if (skill.maxHpDamage > 0f)
            {
                // 최대 체력 비례 추가 피해(방어 무시 · 받는 피해 감소 적용)
                float pct = skill.maxHpDamage * mul / Mathf.Max(1, skill.hits);
                hit.damage += Mathf.RoundToInt(target.maxHP * pct * (1f - Mathf.Clamp(target.dmgTakenDown, 0f, 0.8f)));
            }
            Damage(actor, target, hit.damage);
            if (hit.crit) actor.record.crits++;
            th.hitDamages.Add(hit.damage);
            th.hitCrits.Add(hit.crit);
            target.GainEnergy(_cfg.energyOnHit);
            th.totalDamage += hit.damage;
            th.anyCrit |= hit.crit;
            th.weakness |= hit.weakness;

            // 모든 공격이 브레이크 게이지를 깎고, 약점 속성이면 훨씬 많이 깎음 → 0이 되면 브레이크
            float breakRate = hit.weakness ? _cfg.breakRateWeak : _cfg.breakRateNormal;
            int breakAmount = Mathf.RoundToInt(skill.breakDamage * (1f + actor.breakEffect) * Mathf.Max(0f, breakRate));   // 브레이크 효율
            if (target.IsAlive && target.ReduceToughness(breakAmount))
            {
                int bonus = Mathf.Max(1, Mathf.RoundToInt(hit.damage * Mathf.Max(0f, _cfg.breakBonus) * (1f + actor.breakEffect)));
                Damage(actor, target, bonus);
                actor.record.breaks++;
                th.breakBonusDamage += bonus;
                th.totalDamage += bonus;
                target.actionValue += _cfg.breakActionDelay; // 다음 행동 지연
                th.broke = true;
            }
        }

        /// <summary>도발 중인 상대가 있으면 단일 대상 공격은 그쪽으로.</summary>
        private BattleUnit Redirect(BattleUnit actor, BattleUnit target)
        {
            if (target != null && target.IsTaunting) return target;
            foreach (var u in Opponents(actor)) if (u.IsTaunting) return u;
            return target;
        }

        private BattleUnit RandomAlive(List<BattleUnit> list)
        {
            int n = 0;
            foreach (var u in list) if (u.IsAlive) n++;
            if (n == 0) return null;
            int pick = Mathf.Min(n - 1, Mathf.FloorToInt(_roll() * n));
            foreach (var u in list)
                if (u.IsAlive && pick-- == 0) return u;
            return null;
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

                case TargetType.RandomEnemies:
                    // 실제 대상은 타격마다 고름. 여기서는 살아 있는 적 1명만(대상 없음 방지)
                    { var t = FirstAlive(Opponents(actor)); if (t != null) list.Add(t); }
                    break;

                default: // SingleEnemy · Blast
                {
                    var opp = Opponents(actor);
                    BattleUnit main = primary != null && primary.IsAlive && primary.team != actor.team ? primary : FirstAlive(opp);
                    main = Redirect(actor, main);
                    if (main == null) break;
                    list.Add(main);
                    if (skill.target == TargetType.Blast)
                    {
                        int i = opp.IndexOf(main);
                        if (i - 1 >= 0 && opp[i - 1].IsAlive) list.Add(opp[i - 1]);
                        if (i + 1 < opp.Count && opp[i + 1].IsAlive) list.Add(opp[i + 1]);
                    }
                    break;
                }
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

            var target = Redirect(actor, LowestHP(Opponents(actor)));   // 도발 중인 상대 우선
            return (chosen, target);
        }

        /// <summary>
        /// 아군 자동 전투용 AI. 적 AI(PickAction)보다 영리하게:
        /// 위급한 아군이 있으면 회복 → 필살기 → (SP 여유 시) 버프/공격 스킬 → 기본 공격.
        /// 공격 대상은 스킬 속성 약점 + 브레이크 안 된 적 우선, 그다음 처치 가능/최저 체력.
        /// </summary>
        public (BattleSkill skill, BattleUnit target) PickAutoAction(BattleUnit actor)
        {
            var mates = Teammates(actor);
            var hurt = LowestHPRatio(mates);
            int hurtCount = 0;
            foreach (var u in mates) if (u.IsAlive && u.HPRatio < 0.6f) hurtCount++;
            int sp = SkillPointsOf(actor.team);

            // 1) 회복: 가장 약한 아군 HP < 50% (광역 회복은 60% 미만 2명 이상이면 우선)
            if (hurt != null && hurt.HPRatio < 0.5f)
            {
                BattleSkill single = null, aoe = null;
                foreach (var s in actor.skills)
                {
                    if (!s.IsHeal || !CanUse(actor, s)) continue;
                    if (s.target == TargetType.Self && hurt != actor) continue;   // 자기 회복은 자신이 위급할 때만
                    if (s.target == TargetType.AllAllies) { if (aoe == null || s.heal > aoe.heal) aoe = s; }
                    else if (single == null || s.heal > single.heal) single = s;
                }
                var heal = hurtCount >= 2 ? (aoe ?? single) : (single ?? aoe);
                if (heal != null) return (heal, heal.target == TargetType.SingleAlly ? hurt : null);
            }

            // 1-1) 자기 회복 스킬: 자신의 HP < 50%
            if (actor.HPRatio < 0.5f)
            {
                foreach (var s in actor.skills)
                    if (s.IsHeal && s.target == TargetType.Self && CanUse(actor, s)) return (s, null);
            }

            // 2) 필살기 (회복 전용 필살기는 팀이 멀쩡하면 아껴 둠)
            if (actor.UltimateReady && HasUltimate(actor, out var ult))
            {
                bool wasteful = ult.IsHeal && ult.power <= 0f && (hurt == null || hurt.HPRatio > 0.8f);
                if (!wasteful) return (ult, PickTarget(actor, ult));
            }

            // 3) 스킬 (SP 1 이상)
            if (sp >= 1)
            {
                BattleSkill best = null;
                float bestScore = 0f;
                foreach (var s in actor.skills)
                {
                    if (s.category != SkillCategory.Skill) continue;
                    float score = ScoreSkill(actor, s);
                    if (score > bestScore) { best = s; bestScore = score; }
                }
                // SP가 1뿐이면 확실히 이득일 때만 사용(나머지는 아군 차례 위해 적립)
                if (best != null && (sp >= 2 || bestScore >= 1.5f))
                    return (best, PickTarget(actor, best));
            }

            var basic = actor.BasicSkill;
            return (basic, PickTarget(actor, basic));
        }

        private float ScoreSkill(BattleUnit actor, BattleSkill s)
        {
            if (s.IsHeal && s.power <= 0f)
            {
                var hurt = LowestHPRatio(Teammates(actor));
                return hurt != null && hurt.HPRatio < 0.75f ? 2f : 0f;
            }
            if (s.TargetsAllies)
            {
                // 버프: 이미 같은 버프가 걸려 있으면 사용 안 함
                if (s.applyEffect == StatusType.None) return s.advance > 0f ? 1.6f : 0.5f;
                if (s.target == TargetType.Self) return HasEffect(actor, s.applyEffect) ? 0f : 1.6f;
                foreach (var u in Teammates(actor))
                    if (u.IsAlive && !HasEffect(u, s.applyEffect)) return 1.6f;
                return 0f;
            }
            // 공격: 피해 배율 + 약점 적 수 + 광역 가산
            float score = s.power * s.hits;
            int weak = 0, alive = 0;
            foreach (var e in Opponents(actor))
            {
                if (!e.IsAlive) continue;
                alive++;
                if (!e.isBroken && e.IsWeakTo(s.element)) weak++;
            }
            if (s.target == TargetType.AllEnemies) score *= Mathf.Max(1, alive) * 0.7f;
            if (weak > 0) score += 0.6f;
            return score;
        }

        /// <summary>스킬 대상 선택(광역·자기 대상은 null).</summary>
        private BattleUnit PickTarget(BattleUnit actor, BattleSkill s)
        {
            switch (s.target)
            {
                case TargetType.AllEnemies:
                case TargetType.AllAllies:
                case TargetType.Self:
                    return null;
                case TargetType.SingleAlly:
                    if (s.IsHeal) return LowestHPRatio(Teammates(actor));
                    // 버프: 효과가 없는 아군 중 공격력 높은 순
                    BattleUnit pick = null;
                    foreach (var u in Teammates(actor))
                    {
                        if (!u.IsAlive || (s.applyEffect != StatusType.None && HasEffect(u, s.applyEffect))) continue;
                        if (pick == null || u.atk > pick.atk) pick = u;
                    }
                    return pick ?? LowestHPRatio(Teammates(actor));
            }

            // 단일 적: 약점(미브레이크) > 한 방에 처치 가능 > 최저 체력
            BattleUnit best = null;
            float bestScore = float.MinValue;
            int estimate = Mathf.RoundToInt(actor.EffectiveAtk * s.power * s.hits);
            foreach (var e in Opponents(actor))
            {
                if (!e.IsAlive) continue;
                float score = 0f;
                if (!e.isBroken && e.HasBreakSystem && e.IsWeakTo(s.element)) score += 100f;
                if (e.isBroken) score += 20f;               // 브레이크 중 = 받는 피해 증가 구간
                if (e.currentHP <= estimate) score += 50f;  // 마무리
                score -= e.HPRatio * 10f;
                if (score > bestScore) { best = e; bestScore = score; }
            }
            return best;
        }

        private static bool HasEffect(BattleUnit u, StatusType t)
        {
            foreach (var e in u.effects) if (e.type == t && e.remaining > 0) return true;
            return false;
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
