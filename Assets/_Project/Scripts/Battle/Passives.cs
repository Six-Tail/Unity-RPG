using System;
using System.Collections.Generic;

namespace SubcultureRPG.Battle
{
    /// <summary>패시브가 발동하는 때.</summary>
    public enum PassiveTrigger
    {
        Static,        // 상시(능력치·피해 보정)
        BattleStart,   // 전투 시작
        TurnStart,     // 자신의 턴 시작
        AfterBasic,    // 기본 공격 후
        AfterSkill,    // 스킬 사용 후
        AfterUltimate, // 궁극기 사용 후
        AfterAttack,   // 피해를 준 행동 후
        AfterHeal,     // 회복 스킬 사용 후
        OnKill,        // 적 처치
        OnCrit,        // 치명타 적중
        OnBreak,       // 적 브레이크
        OnWeakHit,     // 약점 적중
        OnDamaged,     // 피격(살아남았을 때)
    }

    /// <summary>패시브 효과. v/v2/status/turns의 뜻은 효과마다 다름(PassiveText 참고).</summary>
    public enum PassiveEffect
    {
        // ---- 발동형 ----
        ExtraTurn,      // 한 번 더 행동
        Energy,         // 자신 에너지 +v
        TeamEnergy,     // 아군 전체 에너지 +v
        SkillPoint,     // SP +v
        HealSelf,       // 최대 체력의 v 회복
        HealLowest,     // 체력이 가장 낮은 아군을 공격력의 v 회복
        HealTeam,       // 아군 전체를 공격력의 v 회복
        StatusSelf,     // 자신에게 status(v, turns)
        StatusTeam,     // 아군 전체에게
        StatusTarget,   // 대상(피격 발동이면 공격자)에게
        StatusEnemies,  // 적 전체에게
        FollowUp,       // 공격력의 v 추가 공격(피격 발동이면 반격)
        AdvanceSelf,    // 행동 게이지 v 당김
        AdvanceTeam,    // 아군 전체 행동 게이지 v 당김
        StackAtk,       // 공격력 +v (최대 v2중첩, 전투 종료까지)
        StackCrit,      // 치명타 확률 +v (최대 v2중첩)
        Recast,         // 처치 시 스킬을 SP 소모 없이 최저 체력 적에게 다시 시전. 피해 감소 v → v2, 최대 limit회

        // ---- 상시 ----
        AtkPct, HpPct, DefPct, SpdFlat, CritRate, CritDmg, Penetration, BreakEffect,
        HealBonus, EffectHit, EffectRes, EnergyRegen,
        DmgBasic, DmgSkill, DmgUlt,     // 종류별 피해 +v
        DmgVsBroken,    // 브레이크 상태의 적에게 피해 +v
        DmgVsDebuffed,  // 디버프에 걸린 적에게 피해 +v
        DmgVsLowHP,     // 체력 50% 미만인 적에게 피해 +v
        DmgTakenDown,   // 받는 피해 -v
        Survive,        // 전투당 1회 쓰러질 피해를 버티고 최대 체력의 v 회복
        HitsBasic, HitsSkill, HitsUlt,  // 타격 수 +v (유닛 생성 시 스킬에 반영)
    }

    /// <summary>패시브 효과 1개(데이터). 캐릭터 고유 패시브·초월 고유 능력이 이걸 1개 이상 가집니다.</summary>
    [Serializable]
    public class PassiveDef
    {
        public string name;         // 속한 능력 이름(발동 알림에 표시)
        public PassiveTrigger trigger;
        public PassiveEffect effect;
        public float v, v2;
        public StatusType status;
        public int turns;
        public float chance = 1f;
        public int limit;           // 자기 턴 사이 발동 횟수 제한(0 = 무제한)
    }

    /// <summary>캐릭터 고유 능력 1개(패시브 또는 초월 고유 능력) = 이름 + 효과 묶음.</summary>
    public class KitAbility
    {
        public string name;
        public int unlock;                      // 0 = 기본 패시브, 1/3/6 = 그 초월 단계에서 해금
        public List<PassiveDef> parts = new();
        /// <summary>초월 고유 능력 전용: 고유 패시브를 이 효과들로 바꿈(강화판). 비어 있으면 패시브는 그대로.</summary>
        public List<PassiveDef> upgrade = new();

        /// <summary>설명 문장(효과마다 한 문장). 패시브 강화가 있으면 '패시브 강화 → …'를 앞에.</summary>
        public string Desc
        {
            get
            {
                var list = new List<string>();
                if (upgrade.Count > 0)
                {
                    var up = new List<string>();
                    foreach (var p in upgrade) up.Add(PassiveText.Describe(p));
                    list.Add("<b>패시브 강화</b> → " + string.Join(" ", up));
                }
                foreach (var p in parts) list.Add(PassiveText.Describe(p));
                return string.Join(" ", list);
            }
        }
    }

    /// <summary>패시브 설명 문장 생성(수치가 데이터와 항상 일치하도록 자동 생성).</summary>
    public static class PassiveText
    {
        private static string Pct(float v) => $"{v * 100f:0.#}%";

        private static string Trigger(PassiveDef p) => p.trigger switch
        {
            PassiveTrigger.BattleStart => "전투 시작 시 ",
            PassiveTrigger.TurnStart => "자신의 턴이 시작될 때 ",
            PassiveTrigger.AfterBasic => "기본 공격 후 ",
            PassiveTrigger.AfterSkill => "스킬 사용 후 ",
            PassiveTrigger.AfterUltimate => "궁극기 사용 후 ",
            PassiveTrigger.AfterAttack => "공격 후 ",
            PassiveTrigger.AfterHeal => "회복 스킬 사용 후 ",
            PassiveTrigger.OnKill => "적을 처치하면 ",
            PassiveTrigger.OnCrit => "치명타가 적중하면 ",
            PassiveTrigger.OnBreak => "적을 브레이크시키면 ",
            PassiveTrigger.OnWeakHit => "약점을 공격하면 ",
            PassiveTrigger.OnDamaged => "공격을 받으면 ",
            _ => "",
        };

        public static string Describe(PassiveDef p)
        {
            if (p == null) return "";
            string chance = p.chance < 0.999f ? $"{Pct(p.chance)} 확률로 " : "";
            string limit = p.limit > 0 && p.trigger != PassiveTrigger.Static && p.trigger != PassiveTrigger.BattleStart
                && p.effect != PassiveEffect.Recast
                ? $" (턴당 {p.limit}회)" : "";
            return Trigger(p) + chance + Effect(p) + limit + ".";
        }

        private static string Effect(PassiveDef p)
        {
            bool reactive = p.trigger == PassiveTrigger.OnDamaged;
            string st = StatusText.Short(p.status, p.v, p.turns);
            return p.effect switch
            {
                PassiveEffect.ExtraTurn => "한 번 더 행동합니다",
                PassiveEffect.Energy => $"에너지 +{p.v:0}",
                PassiveEffect.TeamEnergy => $"아군 전체 에너지 +{p.v:0}",
                PassiveEffect.SkillPoint => $"SP +{p.v:0}",
                PassiveEffect.HealSelf => $"최대 체력의 {Pct(p.v)}를 회복합니다",
                PassiveEffect.HealLowest => $"체력이 가장 낮은 아군을 공격력의 {Pct(p.v)}만큼 회복합니다",
                PassiveEffect.HealTeam => $"아군 전체를 공격력의 {Pct(p.v)}만큼 회복합니다",
                PassiveEffect.StatusSelf => $"자신에게 {st}",
                PassiveEffect.StatusTeam => $"아군 전체에게 {st}",
                PassiveEffect.StatusTarget => (reactive ? "공격자에게 " : "대상에게 ") + st,
                PassiveEffect.StatusEnemies => $"적 전체에게 {st}",
                PassiveEffect.FollowUp => reactive
                    ? $"공격자에게 공격력의 {Pct(p.v)} 피해로 반격합니다"
                    : $"대상에게 공격력의 {Pct(p.v)} 추가 공격을 합니다",
                PassiveEffect.AdvanceSelf => $"자신의 행동 게이지를 {Pct(p.v)} 당깁니다",
                PassiveEffect.AdvanceTeam => $"아군 전체의 행동 게이지를 {Pct(p.v)} 당깁니다",
                PassiveEffect.StackAtk => $"공격력 +{Pct(p.v)} (최대 {p.v2:0}중첩, 전투 종료까지)",
                PassiveEffect.StackCrit => $"치명타 확률 +{Pct(p.v)} (최대 {p.v2:0}중첩, 전투 종료까지)",
                PassiveEffect.Recast => "스킬을 SP 소모 없이 체력이 가장 낮은 적에게 다시 시전합니다(연쇄 최대 " + p.limit + "회). "
                    + (p.v <= 0.0005f && p.v2 <= 0.0005f
                        ? "다시 시전해도 피해가 줄지 않습니다"
                        : $"다시 시전할 때마다 피해가 {Pct(p.v)} → {Pct(p.v2)} 줄어들고, 처치하지 못하면 원래대로 돌아옵니다"),

                PassiveEffect.AtkPct => $"공격력 +{Pct(p.v)}",
                PassiveEffect.HpPct => $"체력 +{Pct(p.v)}",
                PassiveEffect.DefPct => $"방어력 +{Pct(p.v)}",
                PassiveEffect.SpdFlat => $"속도 +{p.v:0}",
                PassiveEffect.CritRate => $"치명타 확률 +{Pct(p.v)}",
                PassiveEffect.CritDmg => $"치명타 피해 +{Pct(p.v)}",
                PassiveEffect.Penetration => $"방어 관통 +{Pct(p.v)}",
                PassiveEffect.BreakEffect => $"브레이크 효율 +{Pct(p.v)}",
                PassiveEffect.HealBonus => $"치유량 +{Pct(p.v)}",
                PassiveEffect.EffectHit => $"효과 명중 +{Pct(p.v)}",
                PassiveEffect.EffectRes => $"효과 저항 +{Pct(p.v)}",
                PassiveEffect.EnergyRegen => $"에너지 회복 효율 +{Pct(p.v)}",
                PassiveEffect.DmgBasic => $"기본 공격 피해 +{Pct(p.v)}",
                PassiveEffect.DmgSkill => $"스킬 피해 +{Pct(p.v)}",
                PassiveEffect.DmgUlt => $"궁극기 피해 +{Pct(p.v)}",
                PassiveEffect.DmgVsBroken => $"브레이크 상태의 적에게 주는 피해 +{Pct(p.v)}",
                PassiveEffect.DmgVsDebuffed => $"디버프에 걸린 적에게 주는 피해 +{Pct(p.v)}",
                PassiveEffect.DmgVsLowHP => $"체력 50% 미만인 적에게 주는 피해 +{Pct(p.v)}",
                PassiveEffect.DmgTakenDown => $"받는 피해 -{Pct(p.v)}",
                PassiveEffect.Survive => $"전투당 1회, 쓰러질 피해를 받아도 체력 1로 버티고 최대 체력의 {Pct(p.v)}를 회복합니다",
                PassiveEffect.HitsBasic => $"기본 공격 타격 수 +{p.v:0}",
                PassiveEffect.HitsSkill => $"스킬 타격 수 +{p.v:0}",
                PassiveEffect.HitsUlt => $"궁극기 타격 수 +{p.v:0}",
                _ => "",
            };
        }
    }
}
