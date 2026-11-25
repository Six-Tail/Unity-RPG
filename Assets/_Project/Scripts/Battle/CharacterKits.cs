using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Battle
{
    /// <summary>스킬 1개의 설계값(레벨 1 기준). Build로 실제 BattleSkill을 만듭니다.</summary>
    public class SkillSpec
    {
        public string name;
        public TargetType target;
        public float power;            // 타격 1회 계수
        public int hits = 1;
        public int energy = -1;        // -1 = 종류별 기본값
        public int brk = -1;           // 브레이크 수치 합계(-1 = 종류별 기본값)
        public float heal;
        public StatusType effect;
        public float effMag;
        public int effTurns = 2;
        public float effChance = 1f;
        public float splash, lifesteal, advance;
        public bool cleanse;
        public StatusType selfEffect;
        public float selfMag;
        public int selfTurns = 2;

        public SkillSpec Eff(StatusType type, float mag, int turns = 2, float chance = 1f)
        { effect = type; effMag = mag; effTurns = turns; effChance = chance; return this; }
        public SkillSpec Heal(float x) { heal = x; return this; }
        public SkillSpec Splash(float x) { splash = x; return this; }
        public SkillSpec Leech(float x) { lifesteal = x; return this; }
        public SkillSpec Adv(float x) { advance = x; return this; }
        public SkillSpec Cleanse() { cleanse = true; return this; }
        public SkillSpec Self(StatusType type, float mag, int turns = 2) { selfEffect = type; selfMag = mag; selfTurns = turns; return this; }
        public SkillSpec Break(int total) { brk = total; return this; }
        public SkillSpec En(int e) { energy = e; return this; }

        private bool IsAttack => target == TargetType.SingleEnemy || target == TargetType.AllEnemies
                                 || target == TargetType.RandomEnemies || target == TargetType.Blast;

        /// <summary>
        /// 실제 스킬 생성. mul = 스킬 레벨 배율(피해·회복, 그리고 '효과가 본체인 스킬'의 효과 수치에 적용).
        /// extraHits = 초월 고유 능력의 타격 수 추가.
        /// </summary>
        public BattleSkill Build(SkillCategory cat, Element el, float mul, int extraHits)
        {
            var s = ScriptableObject.CreateInstance<BattleSkill>();
            s.skillName = name;
            s.category = cat;
            s.element = el;
            s.target = target;
            s.power = power * mul;
            s.hits = Mathf.Max(1, hits + (power > 0f ? extraHits : 0));
            s.energyGain = energy >= 0 ? energy : cat == SkillCategory.Basic ? 20 : cat == SkillCategory.Skill ? 30 : 0;
            int totalBreak = brk >= 0 ? brk : (IsAttack && power > 0f ? (cat == SkillCategory.Basic ? 10 : 20) : 0);
            s.breakDamage = totalBreak > 0 ? Mathf.Max(1, Mathf.RoundToInt(totalBreak / (float)Mathf.Max(1, hits))) : 0;   // 타격당
            s.heal = heal * mul;

            bool pure = power <= 0f && heal <= 0f;   // 효과가 본체인 스킬 → 효과 수치도 레벨로 오름
            s.applyEffect = effect;
            bool scale = effect == StatusType.Dot || effect == StatusType.Regen || (pure && !SkillText.NoMagnitude(effect));
            s.effectMagnitude = effMag * (scale ? mul : 1f);
            s.effectDuration = effTurns;
            s.effectChance = effChance;

            s.splash = splash;
            s.lifesteal = lifesteal;
            s.advance = advance;
            s.cleanse = cleanse;
            s.selfEffect = selfEffect;
            s.selfEffectMagnitude = selfMag * (pure && !SkillText.NoMagnitude(selfEffect) ? mul : 1f);
            s.selfEffectDuration = selfTurns;
            return s;
        }
    }

    /// <summary>캐릭터 1명의 전투 구성: 기본 공격 · 스킬 · 궁극기 · 고유 패시브 · 초월 고유 능력(+1 / +3 / +6).</summary>
    public class CharacterKit
    {
        public SkillSpec basic, skill, ultimate;
        public KitAbility passive, t1, t3, t6;

        public IEnumerable<KitAbility> TranscendAbilities()
        {
            yield return t1;
            yield return t3;
            yield return t6;
        }
    }

    /// <summary>
    /// 캐릭터별 전투 구성표. 캐릭터 id(c_violet …)로 찾고, 없으면 역할별 기본 구성.
    /// 수치는 스킬 Lv.1 기준이며 레벨당 +10%.
    ///
    /// 설계 원칙
    /// · 고유 패시브가 캐릭터의 핵심이고, 초월 고유 능력(+1 / +3 / +6)은 그 패시브를 강화(Up)하는 쪽이 중심
    /// · 태생 성급이 높을수록 패시브가 특별하고 초월 강화 폭이 큼
    ///   6★: 전용 메커니즘 + 초월마다 패시브 강화 · 5★: +3 · +6에서 강화 · 4★ / 3★: +6에서만 소폭 강화, 나머지는 작은 능력치
    /// </summary>
    public static partial class CharacterKits
    {
        private static Dictionary<string, CharacterKit> _kits;
        private static readonly Dictionary<Role, CharacterKit> _defaults = new();

        public static CharacterKit Get(CharacterData c)
        {
            _kits ??= BuildAll();
            if (c != null && !string.IsNullOrEmpty(c.id) && _kits.TryGetValue(c.id, out var kit)) return kit;
            Role role = c != null ? c.role : Role.Dealer;
            if (!_defaults.TryGetValue(role, out var d)) _defaults[role] = d = Default(role);
            return d;
        }

        /// <summary>지금 초월 단계의 고유 패시브 효과(초월 강화가 있으면 가장 높은 단계의 강화판).</summary>
        public static List<PassiveDef> CurrentPassive(CharacterData c, int transcend)
        {
            var kit = Get(c);
            var cur = kit.passive.parts;
            foreach (var a in kit.TranscendAbilities())
                if (a != null && a.unlock <= transcend && a.upgrade.Count > 0) cur = a.upgrade;
            return cur;
        }

        /// <summary>지금 초월 단계에서 켜져 있는 모든 패시브 효과(고유 패시브 현재판 + 해금된 초월 추가 효과).</summary>
        public static List<PassiveDef> ActivePassives(CharacterData c, int transcend)
        {
            var list = new List<PassiveDef>(CurrentPassive(c, transcend));
            foreach (var a in Get(c).TranscendAbilities())
                if (a != null && a.unlock <= transcend) list.AddRange(a.parts);
            return list;
        }

        /// <summary>효과 묶음의 설명 문장.</summary>
        public static string Describe(List<PassiveDef> parts)
        {
            var list = new List<string>();
            foreach (var p in parts) list.Add(PassiveText.Describe(p));
            return string.Join(" ", list);
        }

        // ---------------- 작성 도우미 ----------------

        private const TargetType One = TargetType.SingleEnemy, All = TargetType.AllEnemies, Rnd = TargetType.RandomEnemies,
            Blast = TargetType.Blast, Ally = TargetType.SingleAlly, Party = TargetType.AllAllies, Me = TargetType.Self;

        private static SkillSpec Atk(string name, TargetType t, float power, int hits = 1) =>
            new SkillSpec { name = name, target = t, power = power, hits = hits };

        private static SkillSpec Sup(string name, TargetType t) => new SkillSpec { name = name, target = t };

        /// <summary>상시 효과.</summary>
        private static PassiveDef S(PassiveEffect e, float v) =>
            new PassiveDef { trigger = PassiveTrigger.Static, effect = e, v = v };

        /// <summary>발동 효과.</summary>
        private static PassiveDef T(PassiveTrigger t, PassiveEffect e, float v = 0f, int limit = 0, float chance = 1f, float v2 = 0f) =>
            new PassiveDef { trigger = t, effect = e, v = v, v2 = v2, limit = limit, chance = chance };

        /// <summary>발동 시 상태 효과 부여.</summary>
        private static PassiveDef TS(PassiveTrigger t, PassiveEffect e, StatusType st, float mag, int turns, int limit = 0, float chance = 1f) =>
            new PassiveDef { trigger = t, effect = e, status = st, v = mag, turns = turns, limit = limit, chance = chance };

        /// <summary>연쇄 시전: 처치 시 스킬 재시전. first/second = 1·2번째 재시전의 피해 감소, count = 연쇄 최대 횟수.</summary>
        private static PassiveDef Re(float first, float second, int count) =>
            new PassiveDef { trigger = PassiveTrigger.OnKill, effect = PassiveEffect.Recast, v = first, v2 = second, limit = count };

        /// <summary>능력 1개(추가 효과들).</summary>
        private static KitAbility A(string name, params PassiveDef[] parts)
        {
            var a = new KitAbility { name = name };
            foreach (var p in parts) { p.name = name; a.parts.Add(p); }
            return a;
        }

        /// <summary>초월 고유 능력에 '패시브 강화판'을 붙임(고유 패시브가 이 효과들로 바뀜).</summary>
        private static KitAbility Up(this KitAbility a, params PassiveDef[] upgraded)
        {
            a.upgrade.AddRange(upgraded);
            return a;
        }

        private static CharacterKit K(SkillSpec basic, SkillSpec skill, SkillSpec ult, KitAbility passive, KitAbility t1, KitAbility t3, KitAbility t6)
        {
            passive.unlock = 0; t1.unlock = 1; t3.unlock = 3; t6.unlock = 6;
            // 강화판은 패시브 이름으로 발동 알림
            foreach (var a in new[] { t1, t3, t6 })
                foreach (var p in a.upgrade) p.name = passive.name;
            return new CharacterKit { basic = basic, skill = skill, ultimate = ult, passive = passive, t1 = t1, t3 = t3, t6 = t6 };
        }

        // 자주 쓰는 이름 줄임
        private const PassiveTrigger Start = PassiveTrigger.BattleStart, Turn = PassiveTrigger.TurnStart,
            AfBasic = PassiveTrigger.AfterBasic, AfSkill = PassiveTrigger.AfterSkill, AfUlt = PassiveTrigger.AfterUltimate,
            AfAtk = PassiveTrigger.AfterAttack, AfHeal = PassiveTrigger.AfterHeal, Kill = PassiveTrigger.OnKill,
            Crit = PassiveTrigger.OnCrit, Weak = PassiveTrigger.OnWeakHit, Hurt = PassiveTrigger.OnDamaged;

        // ---------------- 역할별 기본 구성(전용 구성이 없는 캐릭터) ----------------

        private static CharacterKit Default(Role role)
        {
            switch (role)
            {
                case Role.Tank:
                    return K(Atk("기본 공격", One, 0.9f).Break(15),
                        Sup("방어 태세", Me).Eff(StatusType.Taunt, 0f, 2).Self(StatusType.DefUp, 0.4f),
                        Atk("철벽 강타", All, 2.2f).Eff(StatusType.DefDown, 0.2f),
                        A("굳건함", T(Turn, PassiveEffect.HealSelf, 0.03f)),
                        A("단련", S(PassiveEffect.HpPct, 0.06f)),
                        A("불굴", S(PassiveEffect.DefPct, 0.08f)),
                        A("버팀목").Up(T(Turn, PassiveEffect.HealSelf, 0.04f)));
                case Role.Support:
                    return K(Atk("기본 공격", One, 0.9f),
                        Sup("전투 고무", Party).Eff(StatusType.AtkUp, 0.25f),
                        Atk("총공격 신호", All, 2.4f).Eff(StatusType.AtkDown, 0.2f),
                        A("보급", T(AfSkill, PassiveEffect.TeamEnergy, 5)),
                        A("기민함", S(PassiveEffect.SpdFlat, 4)),
                        A("지휘관의 직감", S(PassiveEffect.EffectHit, 0.10f)),
                        A("넉넉한 보급").Up(T(AfSkill, PassiveEffect.TeamEnergy, 7)));
                case Role.Healer:
                    return K(Atk("기본 공격", One, 0.8f),
                        Sup("치유", Ally).Heal(2.0f),
                        Sup("치유의 빛", Party).Heal(1.4f),
                        A("보살핌", T(Turn, PassiveEffect.HealLowest, 0.2f)),
                        A("정성", S(PassiveEffect.HealBonus, 0.08f)),
                        A("차분함", S(PassiveEffect.EffectRes, 0.10f)),
                        A("깊은 보살핌").Up(T(Turn, PassiveEffect.HealLowest, 0.26f)));
                default:
                    return K(Atk("기본 공격", One, 1.0f),
                        Atk("강타", One, 1.8f),
                        Atk("필살기", All, 3.2f),
                        A("집중", S(PassiveEffect.CritRate, 0.05f)),
                        A("단련", S(PassiveEffect.AtkPct, 0.05f)),
                        A("약점 간파", S(PassiveEffect.CritDmg, 0.10f)),
                        A("날 선 집중").Up(S(PassiveEffect.CritRate, 0.08f)));
            }
        }

        // ---------------- 캐릭터별 구성 ----------------

        private static Dictionary<string, CharacterKit> BuildAll() => new()
        {
            // =====================================================================
            // 6★ — 전용 메커니즘 패시브. 초월 +1 · +3 · +6 모두 패시브를 강화하고 추가 효과도 붙음
            // =====================================================================

            // 바이올렛 — 물리 딜러: 처치하면 스킬을 공짜로 다시 시전(연쇄). 초월할수록 연쇄의 피해 감소가 줄어듦.
            ["c_violet"] = K(
                Atk("그림자 베기", One, 0.5f, 2),
                Atk("무음의 연격", One, 0.62f, 3),
                Atk("칠흑의 종막", One, 1.1f, 5).Break(40),
                A("처형자", Re(0.20f, 0.40f, 2)),
                A("칼날 손질").Up(Re(0.15f, 0.30f, 2)),
                A("끊기지 않는 춤", T(Kill, PassiveEffect.Energy, 10)).Up(Re(0.10f, 0.20f, 2)),
                A("끝없는 밤", S(PassiveEffect.HitsUlt, 2)).Up(Re(0.05f, 0.10f, 3))),

            // 세라핀 — 화염 딜러: 공격할 때마다 화상을 묻히고, 디버프 걸린 적에게 더 큰 피해.
            ["c_seraphine"] = K(
                Atk("불씨", One, 1.0f),
                Atk("화염 폭발", Blast, 1.5f).Splash(0.5f).Eff(StatusType.Dot, 0.35f, 2),
                Atk("홍염의 심판", All, 1.5f, 2).Eff(StatusType.Dot, 0.5f, 3),
                A("잿불", TS(AfAtk, PassiveEffect.StatusTarget, StatusType.Dot, 0.20f, 2), S(PassiveEffect.DmgVsDebuffed, 0.20f)),
                A("발화").Up(TS(AfAtk, PassiveEffect.StatusTarget, StatusType.Dot, 0.25f, 2), S(PassiveEffect.DmgVsDebuffed, 0.28f)),
                A("연소 가속", T(AfUlt, PassiveEffect.SkillPoint, 1))
                    .Up(TS(AfAtk, PassiveEffect.StatusTarget, StatusType.Dot, 0.30f, 3), S(PassiveEffect.DmgVsDebuffed, 0.36f)),
                A("불사조", S(PassiveEffect.CritDmg, 0.30f))
                    .Up(TS(AfAtk, PassiveEffect.StatusTarget, StatusType.Dot, 0.40f, 3), S(PassiveEffect.DmgVsDebuffed, 0.50f))),

            // 시엘 — 얼음 딜러: 브레이크된 적을 부수고, 약점을 찌르면 에너지를 채움.
            ["c_ciel"] = K(
                Atk("서리 화살", One, 1.0f),
                Atk("빙창 난무", Rnd, 0.7f, 4).Eff(StatusType.SpdDown, 0.2f, 2),
                Atk("절대영도", All, 2.8f).Eff(StatusType.Stun, 0f, 1, 0.4f),
                A("서릿발", S(PassiveEffect.DmgVsBroken, 0.30f), T(Weak, PassiveEffect.Energy, 8, limit: 1)),
                A("결빙 감지").Up(S(PassiveEffect.DmgVsBroken, 0.38f), T(Weak, PassiveEffect.Energy, 10, limit: 1)),
                A("빙창 증식", S(PassiveEffect.HitsSkill, 1)).Up(S(PassiveEffect.DmgVsBroken, 0.46f), T(Weak, PassiveEffect.Energy, 12, limit: 1)),
                A("영구 동토", T(AfUlt, PassiveEffect.AdvanceSelf, 0.5f))
                    .Up(S(PassiveEffect.DmgVsBroken, 0.60f), T(Weak, PassiveEffect.Energy, 15, limit: 2))),

            // 아크 — 번개 탱커: 맞으면 반격. 초월할수록 반격이 세지고 횟수가 늘어남.
            ["c_ark"] = K(
                Atk("방패 강타", One, 0.9f).Break(15),
                Sup("수호 선언", Me).Eff(StatusType.Taunt, 0f, 2).Self(StatusType.DefUp, 0.4f),
                Atk("뇌광 방벽", All, 1.8f).Eff(StatusType.DefDown, 0.2f).Break(30),
                A("뇌전 반격", T(Hurt, PassiveEffect.FollowUp, 0.60f, limit: 2)),
                A("선봉", TS(Start, PassiveEffect.StatusSelf, StatusType.Taunt, 0f, 2)).Up(T(Hurt, PassiveEffect.FollowUp, 0.75f, limit: 2)),
                A("강철 심장", S(PassiveEffect.DmgTakenDown, 0.12f)).Up(T(Hurt, PassiveEffect.FollowUp, 0.90f, limit: 3)),
                A("부서지지 않는 방패", S(PassiveEffect.Survive, 0.30f)).Up(T(Hurt, PassiveEffect.FollowUp, 1.20f, limit: 4))),

            // 에스텔 — 화염 힐러: 기본 공격으로도 치유. +6에서 아군 전체 치유로 바뀜.
            ["c_estelle"] = K(
                Atk("성화", One, 0.8f),
                Sup("치유의 불꽃", Ally).Heal(2.0f).Eff(StatusType.Regen, 0.4f, 2),
                Sup("재생의 화염", Party).Heal(1.5f).Cleanse(),
                A("온기", T(AfBasic, PassiveEffect.HealLowest, 0.50f)),
                A("등불", T(Start, PassiveEffect.TeamEnergy, 15)).Up(T(AfBasic, PassiveEffect.HealLowest, 0.65f)),
                A("성녀의 손길", S(PassiveEffect.HealBonus, 0.20f)).Up(T(AfBasic, PassiveEffect.HealLowest, 0.80f)),
                A("부활의 불씨", TS(AfUlt, PassiveEffect.StatusTeam, StatusType.AtkUp, 0.25f, 2)).Up(T(AfBasic, PassiveEffect.HealTeam, 0.60f))),

            // 오로라 — 얼음 서포터: 스킬을 쓸 때마다 아군 에너지 보급.
            ["c_aurora"] = K(
                Atk("별빛", One, 0.9f),
                Sup("극광의 축복", Party).Eff(StatusType.AtkUp, 0.25f),
                Sup("오로라 커튼", Party).Eff(StatusType.CritUp, 0.25f, 3).Adv(0.3f),
                A("길잡이 별", T(AfSkill, PassiveEffect.TeamEnergy, 8)),
                A("새벽빛", T(Start, PassiveEffect.SkillPoint, 1)).Up(T(AfSkill, PassiveEffect.TeamEnergy, 10)),
                A("유성", S(PassiveEffect.SpdFlat, 12)).Up(T(AfSkill, PassiveEffect.TeamEnergy, 12)),
                A("밤을 걷는 빛", TS(AfUlt, PassiveEffect.StatusTeam, StatusType.SpdUp, 0.2f, 2))
                    .Up(T(AfSkill, PassiveEffect.TeamEnergy, 15), T(AfUlt, PassiveEffect.SkillPoint, 2))),

            // 카엘 — 바람 딜러: 전부 다단 히트, 치명타가 터지면 행동이 당겨짐.
            ["c_kael"] = K(
                Atk("질풍", One, 0.34f, 3),
                Atk("선풍 연참", Blast, 0.5f, 3).Splash(0.5f),
                Atk("폭풍의 눈", All, 0.8f, 4),
                A("순풍", T(Crit, PassiveEffect.AdvanceSelf, 0.15f, limit: 1)),
                A("바람의 발", S(PassiveEffect.SpdFlat, 10)).Up(T(Crit, PassiveEffect.AdvanceSelf, 0.20f, limit: 1)),
                A("칼바람", S(PassiveEffect.HitsBasic, 1)).Up(T(Crit, PassiveEffect.AdvanceSelf, 0.25f, limit: 1)),
                A("태풍의 화신", T(AfUlt, PassiveEffect.ExtraTurn, limit: 1))
                    .Up(T(Crit, PassiveEffect.AdvanceSelf, 0.35f, limit: 1), S(PassiveEffect.CritRate, 0.10f))),

            // =====================================================================
            // 5★ — 쓸 만한 발동형 패시브. +1은 능력치, +3 · +6에서 패시브 강화
            // =====================================================================

            // 그레이 — 물리 탱커: 도발 + 턴마다 회복.
            ["c_grey"] = K(
                Atk("철권", One, 0.9f).Break(15),
                Sup("강철 의지", Me).Eff(StatusType.Taunt, 0f, 2).Self(StatusType.DefUp, 0.5f),
                Atk("대지 분쇄", All, 2.0f).Eff(StatusType.DefDown, 0.2f).Break(40),
                A("버티기", T(Turn, PassiveEffect.HealSelf, 0.04f)),
                A("단련된 몸", S(PassiveEffect.HpPct, 0.10f)),
                A("투지").Up(T(Turn, PassiveEffect.HealSelf, 0.05f)),
                A("파쇄자", S(PassiveEffect.BreakEffect, 0.20f)).Up(T(Turn, PassiveEffect.HealSelf, 0.06f))),

            // 레나 — 번개 딜러: 연쇄 번개(무작위 연타), 스킬을 쓰면 에너지 충전.
            ["c_rena"] = K(
                Atk("전격", One, 1.0f),
                Atk("연쇄 번개", Rnd, 0.6f, 4),
                Atk("뇌신강림", One, 1.3f, 4).Eff(StatusType.Stun, 0f, 1, 0.4f).Break(40),
                A("과충전", T(AfSkill, PassiveEffect.Energy, 10)),
                A("예리한 전류", S(PassiveEffect.CritRate, 0.06f)),
                A("축전").Up(T(AfSkill, PassiveEffect.Energy, 13)),
                A("뇌신", S(PassiveEffect.HitsSkill, 1)).Up(T(AfSkill, PassiveEffect.Energy, 16))),

            // 루나 — 얼음 딜러: 단일 대상 한 방. 약해진 적을 마무리.
            ["c_luna"] = K(
                Atk("달빛 사격", One, 1.05f),
                Atk("빙월참", One, 2.0f).Eff(StatusType.DefDown, 0.15f),
                Atk("만월의 일격", One, 5.0f).Break(40),
                A("사냥감 표식", S(PassiveEffect.DmgVsLowHP, 0.20f)),
                A("명사수", S(PassiveEffect.CritDmg, 0.15f)),
                A("조준 보정").Up(S(PassiveEffect.DmgVsLowHP, 0.26f)),
                A("월식", T(Kill, PassiveEffect.Energy, 20)).Up(S(PassiveEffect.DmgVsLowHP, 0.32f))),

            // 리오 — 바람 서포터: 전투 시작부터 아군을 빠르게.
            ["c_rio"] = K(
                Atk("산들바람", One, 0.9f),
                Sup("순풍의 가호", Ally).Eff(StatusType.AtkUp, 0.2f).Adv(0.5f),
                Atk("태풍의 눈", All, 2.2f).Eff(StatusType.SpdDown, 0.2f),
                A("바람길", TS(Start, PassiveEffect.StatusTeam, StatusType.SpdUp, 0.15f, 2)),
                A("상승 기류", S(PassiveEffect.EnergyRegen, 0.10f)),
                A("순항").Up(TS(Start, PassiveEffect.StatusTeam, StatusType.SpdUp, 0.18f, 3)),
                A("폭풍 전야", T(AfUlt, PassiveEffect.AdvanceTeam, 0.15f)).Up(TS(Start, PassiveEffect.StatusTeam, StatusType.SpdUp, 0.22f, 3))),

            // 셀레네 — 얼음 힐러: 전체 회복 중심, 턴마다 자동 치유.
            ["c_selene"] = K(
                Atk("월광", One, 0.8f),
                Sup("달의 은총", Party).Heal(0.9f),
                Sup("초승달 성역", Party).Heal(1.3f).Eff(StatusType.DefUp, 0.25f),
                A("밀물", T(Turn, PassiveEffect.HealLowest, 0.30f)),
                A("은빛 기도", S(PassiveEffect.HealBonus, 0.10f)),
                A("만조").Up(T(Turn, PassiveEffect.HealLowest, 0.38f)),
                A("달의 요람", T(AfHeal, PassiveEffect.Energy, 6)).Up(T(Turn, PassiveEffect.HealLowest, 0.45f))),

            // 하루 — 화염 서포터: 치명 버프 + 방어 감소, 궁극기 뒤 공격력 응원.
            ["c_haru"] = K(
                Atk("불꽃 부채", One, 0.9f),
                Sup("투지의 북", Party).Eff(StatusType.CritUp, 0.15f),
                Atk("열풍 진군", All, 2.0f).Eff(StatusType.DefDown, 0.25f),
                A("응원", TS(AfUlt, PassiveEffect.StatusTeam, StatusType.AtkUp, 0.15f, 2)),
                A("타오르는 기세", T(Start, PassiveEffect.Energy, 20)),
                A("큰 북소리").Up(TS(AfUlt, PassiveEffect.StatusTeam, StatusType.AtkUp, 0.18f, 2)),
                A("축제의 밤", S(PassiveEffect.EffectHit, 0.20f)).Up(TS(AfUlt, PassiveEffect.StatusTeam, StatusType.AtkUp, 0.22f, 3))),

            // =====================================================================
            // 4★ — 단순한 발동형 패시브. +1 · +3은 작은 능력치, +6에서만 패시브 소폭 강화
            // =====================================================================

            // 미나 — 화염 딜러: 2연타 + 화상.
            ["c_mina"] = K(
                Atk("화염구", One, 1.0f),
                Atk("쌍화염", One, 0.95f, 2).Eff(StatusType.Dot, 0.3f, 2),
                Atk("대폭발", All, 3.0f),
                A("불티", T(Crit, PassiveEffect.Energy, 6, limit: 1)),
                A("화력 증강", S(PassiveEffect.DmgSkill, 0.10f)),
                A("집중 포화", S(PassiveEffect.CritRate, 0.06f)),
                A("번지는 불길").Up(T(Crit, PassiveEffect.Energy, 9, limit: 1))),

            // 벨라 — 번개 서포터: 방어·공격 감소 디버퍼.
            ["c_bella"] = K(
                Atk("스파크", One, 0.9f),
                Atk("감전 유도", One, 0.8f).Eff(StatusType.DefDown, 0.25f),
                Atk("뇌운", All, 2.2f).Eff(StatusType.AtkDown, 0.2f),
                A("잔류 전류", T(AfBasic, PassiveEffect.SkillPoint, 1, chance: 0.3f)),
                A("정밀 조준", S(PassiveEffect.EffectHit, 0.15f)),
                A("빠른 손", S(PassiveEffect.SpdFlat, 5)),
                A("과부하").Up(T(AfBasic, PassiveEffect.SkillPoint, 1, chance: 0.4f))),

            // 소라 — 얼음 탱커: 도발 + 때린 적을 느리게.
            ["c_sora"] = K(
                Atk("얼음 방패", One, 0.9f).Break(15),
                Sup("빙벽", Me).Eff(StatusType.Taunt, 0f, 2).Self(StatusType.DefUp, 0.5f),
                Atk("눈사태", All, 2.0f).Eff(StatusType.SpdDown, 0.2f),
                A("서리 갑옷", TS(Hurt, PassiveEffect.StatusTarget, StatusType.SpdDown, 0.2f, 1, chance: 0.3f)),
                A("두꺼운 얼음", S(PassiveEffect.DefPct, 0.10f)),
                A("단단한 몸", S(PassiveEffect.HpPct, 0.08f)),
                A("만년설").Up(TS(Hurt, PassiveEffect.StatusTarget, StatusType.SpdDown, 0.2f, 2, chance: 0.4f))),

            // 유나 — 바람 딜러: 화살 연사(무작위 연타·전체 4연타).
            ["c_yuna"] = K(
                Atk("바람 화살", One, 0.5f, 2),
                Atk("삼연사", Rnd, 0.65f, 3),
                Atk("화살비", All, 0.75f, 4),
                A("바람 읽기", T(Weak, PassiveEffect.AdvanceSelf, 0.10f, limit: 1)),
                A("가벼운 발", S(PassiveEffect.SpdFlat, 6)),
                A("매의 눈", S(PassiveEffect.CritRate, 0.06f)),
                A("순풍 타기").Up(T(Weak, PassiveEffect.AdvanceSelf, 0.15f, limit: 1))),

            // 코코 — 물리 탱커: 자기 회복 + 반격.
            ["c_coco"] = K(
                Atk("박치기", One, 0.9f).Break(20),
                Sup("웅크리기", Me).Heal(1.5f).Eff(StatusType.DefUp, 0.4f),
                Atk("지진", All, 2.0f).Eff(StatusType.Stun, 0f, 1, 0.25f).Break(30),
                A("되받아치기", T(Hurt, PassiveEffect.FollowUp, 0.50f, limit: 1)),
                A("든든한 체격", S(PassiveEffect.HpPct, 0.08f)),
                A("묵직한 일격", S(PassiveEffect.BreakEffect, 0.15f)),
                A("철옹성").Up(T(Hurt, PassiveEffect.FollowUp, 0.65f, limit: 1))),

            // 테오 — 번개 힐러: 회복하면서 행동을 당겨 줌.
            ["c_teo"] = K(
                Atk("정전기", One, 0.8f),
                Sup("전기 치료", Ally).Heal(2.0f).Adv(0.2f),
                Sup("활력 충전", Party).Heal(1.2f).Eff(StatusType.SpdUp, 0.15f),
                A("순환", T(AfHeal, PassiveEffect.Energy, 6)),
                A("의료 지식", S(PassiveEffect.HealBonus, 0.10f)),
                A("가벼운 몸", S(PassiveEffect.SpdFlat, 5)),
                A("재충전").Up(T(AfHeal, PassiveEffect.Energy, 9))),

            // =====================================================================
            // 3★ — 소박한 패시브. +1 · +3은 작은 능력치, +6에서만 패시브 소폭 강화
            // =====================================================================

            // 도도 — 얼음 딜러: 속도 감소, 브레이크된 적에게 강함.
            ["c_dodo"] = K(
                Atk("눈덩이", One, 1.0f),
                Atk("고드름", One, 1.7f).Eff(StatusType.SpdDown, 0.15f),
                Atk("눈보라", All, 2.8f),
                A("빈틈 노리기", S(PassiveEffect.DmgVsBroken, 0.15f)),
                A("단단한 눈", S(PassiveEffect.AtkPct, 0.05f)),
                A("날카로운 얼음", S(PassiveEffect.DmgSkill, 0.08f)),
                A("한겨울").Up(S(PassiveEffect.DmgVsBroken, 0.22f))),

            // 루루 — 번개 서포터: 공격력 버프 + SP 보급.
            ["c_ruru"] = K(
                Atk("찌릿", One, 0.9f),
                Sup("응원 전류", Party).Eff(StatusType.AtkUp, 0.2f),
                Atk("방전", All, 2.0f).Eff(StatusType.AtkDown, 0.15f),
                A("준비 완료", T(Start, PassiveEffect.SkillPoint, 1)),
                A("재빠름", S(PassiveEffect.SpdFlat, 4)),
                A("또랑또랑", S(PassiveEffect.EffectHit, 0.10f)),
                A("모두 힘내").Up(T(Start, PassiveEffect.SkillPoint, 1), T(Start, PassiveEffect.Energy, 15))),

            // 리리 — 바람 힐러: 단일 회복 + 재생.
            ["c_riri"] = K(
                Atk("산들", One, 0.8f),
                Sup("바람의 손길", Ally).Heal(1.8f),
                Sup("치유의 바람", Party).Heal(1.2f).Eff(StatusType.Regen, 0.25f, 2),
                A("살랑바람", T(Turn, PassiveEffect.HealLowest, 0.20f)),
                A("정성", S(PassiveEffect.HealBonus, 0.08f)),
                A("미리 준비", T(Start, PassiveEffect.Energy, 10)),
                A("포근한 바람").Up(T(Turn, PassiveEffect.HealLowest, 0.26f))),

            // 모모 — 화염 탱커: 도발, 맞을수록 에너지.
            ["c_momo"] = K(
                Atk("불주먹", One, 0.9f).Break(15),
                Sup("열혈", Me).Eff(StatusType.Taunt, 0f, 2).Self(StatusType.DefUp, 0.4f),
                Atk("화염 강타", All, 1.9f).Eff(StatusType.Dot, 0.3f, 2),
                A("근성", T(Hurt, PassiveEffect.Energy, 5, limit: 3)),
                A("튼튼", S(PassiveEffect.HpPct, 0.06f)),
                A("불굴의 의지", S(PassiveEffect.DefPct, 0.08f)),
                A("맞불").Up(T(Hurt, PassiveEffect.Energy, 7, limit: 3))),

            // 키키 — 바람 탱커: 아군 전체 방어 버프, 2연타 기본 공격.
            ["c_kiki"] = K(
                Atk("회전 차기", One, 0.45f, 2).Break(15),
                Sup("바람막이", Party).Eff(StatusType.DefUp, 0.25f),
                Atk("돌개바람", All, 1.9f).Eff(StatusType.AtkDown, 0.15f),
                A("날쌘돌이", T(Hurt, PassiveEffect.AdvanceSelf, 0.2f, chance: 0.3f)),
                A("단단한 다리", S(PassiveEffect.DefPct, 0.06f)),
                A("끈기", S(PassiveEffect.EffectRes, 0.12f)),
                A("바람처럼").Up(T(Hurt, PassiveEffect.AdvanceSelf, 0.2f, chance: 0.4f))),

            // 핀 — 물리 딜러: 3연 찌르기, 처치하면 에너지.
            ["c_pin"] = K(
                Atk("찌르기", One, 1.0f),
                Atk("연속 찌르기", One, 0.6f, 3),
                Atk("돌진", Blast, 2.6f).Splash(0.6f),
                A("기세", T(Kill, PassiveEffect.Energy, 20)),
                A("훈련의 성과", S(PassiveEffect.AtkPct, 0.05f)),
                A("빈틈 찌르기", S(PassiveEffect.Penetration, 0.06f)),
                A("오르는 기세").Up(T(Kill, PassiveEffect.Energy, 30))),
        };
    }
}
