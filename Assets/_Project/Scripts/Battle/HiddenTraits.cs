using System.Collections.Generic;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Battle
{
    /// <summary>
    /// 숨겨진 특성: 캐릭터가 Lv.100(풀 초월 + 돌파 완료 후의 각성 최대 레벨)에 도달하면 개화하는 추가 패시브.
    /// 도달하기 어려운 만큼 효율이 높고, 태생 성급이 높을수록 강합니다.
    ///   6★ 판도를 바꾸는 효과(추가 행동 · 전투 시작 시 팀 전체 강화 · 큰 피해 배율)
    ///   5★ 강한 효과 3개 · 4★ 준수한 효과 3개 · 3★ 소박하지만 확실한 효과 2 ~ 3개
    /// 기존 패시브 틀(PassiveTrigger × PassiveEffect)을 그대로 쓰므로 전투 · 설명 문장 · 발동 알림이 자동으로 따라옵니다.
    /// 문서: docs/character-kits.md '숨겨진 특성'
    /// </summary>
    public static partial class CharacterKits
    {
        /// <summary>숨겨진 특성이 개화하는 레벨(Leveling.AwakenedMaxLevel 과 같음).</summary>
        public const int HiddenLevel = 100;

        private static Dictionary<string, KitAbility> _hidden;
        private static readonly Dictionary<Role, KitAbility> _hiddenDefaults = new();

        /// <summary>이 캐릭터의 숨겨진 특성(레벨과 상관없이 정의를 돌려줌).</summary>
        public static KitAbility Hidden(CharacterData c)
        {
            _hidden ??= BuildHidden();
            if (c != null && !string.IsNullOrEmpty(c.id) && _hidden.TryGetValue(c.id, out var a)) return a;
            Role role = c != null ? c.role : Role.Dealer;
            if (!_hiddenDefaults.TryGetValue(role, out var d)) _hiddenDefaults[role] = d = HiddenDefault(role);
            return d;
        }

        /// <summary>이 레벨에서 숨겨진 특성이 켜져 있는가.</summary>
        public static bool HiddenActive(int level) => level >= HiddenLevel;

        /// <summary>초월 고유 능력 + (Lv.100이면) 숨겨진 특성까지 포함한 모든 패시브 효과.</summary>
        public static List<PassiveDef> ActivePassives(CharacterData c, int transcend, int level)
        {
            var list = ActivePassives(c, transcend);
            if (HiddenActive(level)) list.AddRange(Hidden(c).parts);
            return list;
        }

        private static KitAbility H(string name, params PassiveDef[] parts)
        {
            var a = A(name, parts);
            a.unlock = HiddenLevel;
            return a;
        }

        // 전용 특성이 없는 캐릭터(역할별)
        private static KitAbility HiddenDefault(Role role) => role switch
        {
            Role.Tank => H("굳건한 의지", S(PassiveEffect.HpPct, 0.12f), S(PassiveEffect.DmgTakenDown, 0.08f)),
            Role.Support => H("전장의 지휘", T(Start, PassiveEffect.TeamEnergy, 10), S(PassiveEffect.SpdFlat, 6)),
            Role.Healer => H("자애", S(PassiveEffect.HealBonus, 0.15f), T(AfHeal, PassiveEffect.Energy, 8)),
            _ => H("한계 돌파", S(PassiveEffect.AtkPct, 0.12f), S(PassiveEffect.CritDmg, 0.2f)),
        };

        private static Dictionary<string, KitAbility> BuildHidden() => new()
        {
            // ===================== 6★ — 판도를 바꾸는 효과 =====================
            // 바이올렛(처형자 · 연쇄 시전): 처치하면 한 번 더 움직이고, 빈사 상태의 적을 확실히 끝냄
            ["c_violet"] = H("밤의 여왕",
                T(Kill, PassiveEffect.ExtraTurn, limit: 1),
                S(PassiveEffect.Penetration, 0.25f),
                S(PassiveEffect.DmgVsLowHP, 0.40f)),
            // 세라핀(화상 연계): 전투가 시작되자마자 적 전체가 불타고, 불타는 적에게 압도적
            ["c_seraphine"] = H("불멸의 불꽃",
                TS(Start, PassiveEffect.StatusEnemies, StatusType.Dot, 0.40f, 3),
                S(PassiveEffect.DmgVsDebuffed, 0.40f),
                T(Crit, PassiveEffect.Energy, 10, limit: 2)),
            // 시엘(브레이크 · 약점): 브레이크시키면 한 번 더 움직이고, 브레이크된 적을 얼려 부숨
            ["c_ciel"] = H("절대영도의 주인",
                T(PassiveTrigger.OnBreak, PassiveEffect.ExtraTurn, limit: 1),
                S(PassiveEffect.DmgVsBroken, 0.50f),
                S(PassiveEffect.Penetration, 0.20f)),
            // 아크(반격 탱커): 맞을수록 아군의 궁극기가 차오르고, 시작부터 팀 전체를 단단하게
            ["c_ark"] = H("천둥의 수호신",
                TS(Start, PassiveEffect.StatusTeam, StatusType.DefUp, 0.30f, 3),
                T(Hurt, PassiveEffect.TeamEnergy, 6, limit: 3),
                S(PassiveEffect.DmgTakenDown, 0.25f),
                S(PassiveEffect.HpPct, 0.25f)),
            // 에스텔(힐러): 자기 턴마다 아군 전체가 회복되고, 회복하면 아군 전체 공격력 상승
            ["c_estelle"] = H("꺼지지 않는 성화",
                T(Turn, PassiveEffect.HealTeam, 0.40f),
                TS(AfHeal, PassiveEffect.StatusTeam, StatusType.AtkUp, 0.20f, 2),
                S(PassiveEffect.HealBonus, 0.35f)),
            // 오로라(에너지 · SP 서포터): 시작부터 궁극기와 스킬을 쓸 수 있게 깔아 주고, 궁극기 후 아군 전체가 앞당겨 행동
            ["c_aurora"] = H("극광의 여신",
                T(Start, PassiveEffect.TeamEnergy, 30),
                T(Start, PassiveEffect.SkillPoint, 2),
                T(AfUlt, PassiveEffect.AdvanceTeam, 0.30f),
                S(PassiveEffect.SpdFlat, 15)),
            // 카엘(치명타 · 속도): 치명타가 폭발적으로 강해지고, 처치하면 한 번 더 움직임
            ["c_kael"] = H("태풍의 눈",
                T(Kill, PassiveEffect.ExtraTurn, limit: 1),
                S(PassiveEffect.CritRate, 0.20f),
                S(PassiveEffect.CritDmg, 0.50f)),

            // ===================== 5★ — 강한 효과 =====================
            ["c_grey"] = H("늑대왕의 혼",
                T(Hurt, PassiveEffect.FollowUp, 0.50f, limit: 2),
                S(PassiveEffect.HpPct, 0.20f),
                S(PassiveEffect.DmgTakenDown, 0.15f)),
            ["c_rena"] = H("뇌신의 창",
                S(PassiveEffect.DmgSkill, 0.30f),
                S(PassiveEffect.CritRate, 0.12f),
                T(Crit, PassiveEffect.Energy, 8, limit: 2)),
            ["c_luna"] = H("만월의 사수",
                S(PassiveEffect.DmgUlt, 0.40f),
                S(PassiveEffect.CritDmg, 0.40f),
                T(Kill, PassiveEffect.Energy, 30)),
            ["c_rio"] = H("바람의 지휘자",
                T(Turn, PassiveEffect.AdvanceTeam, 0.08f),
                S(PassiveEffect.EnergyRegen, 0.20f),
                S(PassiveEffect.SpdFlat, 10)),
            ["c_selene"] = H("달의 성녀",
                T(Turn, PassiveEffect.HealTeam, 0.25f),
                TS(AfHeal, PassiveEffect.StatusTeam, StatusType.DefUp, 0.20f, 2),
                S(PassiveEffect.HealBonus, 0.25f)),
            ["c_haru"] = H("축제의 불꽃",
                TS(Start, PassiveEffect.StatusTeam, StatusType.AtkUp, 0.20f, 3),
                T(AfUlt, PassiveEffect.SkillPoint, 2),
                S(PassiveEffect.EffectHit, 0.20f)),

            // ===================== 4★ — 준수한 효과 =====================
            ["c_mina"] = H("대마법사의 자질",
                S(PassiveEffect.DmgSkill, 0.20f),
                S(PassiveEffect.DmgUlt, 0.20f),
                S(PassiveEffect.CritRate, 0.08f)),
            ["c_bella"] = H("과부하 회로",
                T(Start, PassiveEffect.SkillPoint, 1),
                T(AfSkill, PassiveEffect.Energy, 8),
                S(PassiveEffect.EffectHit, 0.20f)),
            ["c_sora"] = H("만년설의 기사",
                S(PassiveEffect.DefPct, 0.20f),
                S(PassiveEffect.DmgTakenDown, 0.10f),
                T(Turn, PassiveEffect.HealSelf, 0.04f)),
            ["c_yuna"] = H("바람의 명사수",
                S(PassiveEffect.HitsBasic, 1),
                S(PassiveEffect.CritRate, 0.10f),
                T(Weak, PassiveEffect.Energy, 6, limit: 2)),
            ["c_coco"] = H("꺾이지 않는 방패",
                S(PassiveEffect.Survive, 0.25f),
                S(PassiveEffect.HpPct, 0.15f),
                S(PassiveEffect.BreakEffect, 0.15f)),
            ["c_teo"] = H("구명 전류",
                T(AfHeal, PassiveEffect.HealLowest, 0.40f),
                S(PassiveEffect.HealBonus, 0.18f),
                S(PassiveEffect.SpdFlat, 6)),

            // ===================== 3★ — 소박하지만 확실한 효과 =====================
            ["c_dodo"] = H("설원의 사냥꾼",
                S(PassiveEffect.CritDmg, 0.25f),
                S(PassiveEffect.DmgVsBroken, 0.15f)),
            ["c_ruru"] = H("번개 배달부",
                T(Start, PassiveEffect.TeamEnergy, 10),
                S(PassiveEffect.SpdFlat, 6)),
            ["c_riri"] = H("숲의 약사",
                S(PassiveEffect.HealBonus, 0.15f),
                T(AfHeal, PassiveEffect.Energy, 8)),
            ["c_momo"] = H("마을의 수호자",
                S(PassiveEffect.HpPct, 0.12f),
                S(PassiveEffect.DmgTakenDown, 0.08f)),
            ["c_kiki"] = H("길 위의 방패",
                S(PassiveEffect.DefPct, 0.12f),
                S(PassiveEffect.EffectRes, 0.20f),
                T(Turn, PassiveEffect.HealSelf, 0.03f)),
            ["c_pin"] = H("견습 기사의 각오",
                S(PassiveEffect.AtkPct, 0.12f),
                S(PassiveEffect.Penetration, 0.10f)),
        };
    }
}
