using System.Collections.Generic;

namespace SubcultureRPG.Battle
{
    /// <summary>스킬 설명 문구(전투 스킬 버튼 · 육성 스킬 탭).</summary>
    public static class SkillText
    {
        public static string Target(TargetType t) => t switch
        {
            TargetType.AllEnemies => "적 전체",
            TargetType.AllAllies => "아군 전체",
            TargetType.SingleAlly => "아군 1명",
            TargetType.Self => "자신",
            TargetType.RandomEnemies => "무작위 적",
            TargetType.Blast => "적 1명 + 양옆",
            _ => "적 1명",
        };

        public static string Effect(StatusType t) => t switch
        {
            StatusType.AtkUp => "공격력 증가",
            StatusType.DefUp => "방어력 증가",
            StatusType.AtkDown => "공격력 감소",
            StatusType.DefDown => "방어력 감소",
            StatusType.SpdUp => "속도 증가",
            StatusType.SpdDown => "속도 감소",
            StatusType.CritUp => "치명타 확률 증가",
            StatusType.Dot => "지속 피해",
            StatusType.Regen => "재생",
            StatusType.Taunt => "도발",
            StatusType.Stun => "기절",
            _ => "",
        };

        /// <summary>수치가 없는 효과(도발·기절)인지.</summary>
        public static bool NoMagnitude(StatusType t) => t == StatusType.Taunt || t == StatusType.Stun;

        private static string EffectPart(StatusType t, float mag, int turns, float chance)
        {
            string c = chance < 0.999f ? $" {chance * 100f:0}%" : "";
            string m = NoMagnitude(t) ? "" : $" {mag * 100f:0}%";
            return $"{Effect(t)}{m}{c} ({turns}턴)";
        }

        /// <summary>'적 1명 · 피해 60%×3' / '아군 1명 · 회복 200%' / '공격력 증가 25% (2턴)'.</summary>
        public static string Describe(BattleSkill s)
        {
            if (s == null) return "";
            var parts = new List<string>();
            if (s.IsHeal && s.TargetsAllies) parts.Add($"회복 {s.heal * 100f:0}%");
            else if (s.power > 0f)
            {
                string p = $"피해 {s.power * 100f:0}%" + (s.hits > 1 ? $"×{s.hits}" : "");
                if (s.target == TargetType.Blast && s.splash > 0f) p += $" (양옆 {s.splash * 100f:0}%)";
                parts.Add(p);
            }
            if (s.applyEffect != StatusType.None) parts.Add(EffectPart(s.applyEffect, s.effectMagnitude, s.effectDuration, s.effectChance));
            if (s.selfEffect != StatusType.None) parts.Add("자신 " + EffectPart(s.selfEffect, s.selfEffectMagnitude, s.selfEffectDuration, 1f));
            if (s.advance > 0f) parts.Add($"행동 당김 {s.advance * 100f:0}%");
            if (s.lifesteal > 0f) parts.Add($"흡혈 {s.lifesteal * 100f:0}%");
            if (s.cleanse) parts.Add("디버프 해제");
            return $"{Target(s.target)} · " + (parts.Count > 0 ? string.Join(" · ", parts) : "보조");
        }

        /// <summary>비용·효과: 기본 = SP +1, 스킬 = SP -1, 필살기 = 에너지 가득.</summary>
        public static string Cost(BattleSkill s) => s == null ? "" : s.category switch
        {
            SkillCategory.Ultimate => "필살기 · 에너지 소모",
            SkillCategory.Skill => "SP -1",
            _ => "SP +1",
        };
    }
}
