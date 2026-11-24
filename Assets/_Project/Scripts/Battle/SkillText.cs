using System.Collections.Generic;

namespace SubcultureRPG.Battle
{
    /// <summary>스킬 설명 문구(전투 스킬 버튼 등).</summary>
    public static class SkillText
    {
        public static string Target(TargetType t) => t switch
        {
            TargetType.AllEnemies => "적 전체",
            TargetType.AllAllies => "아군 전체",
            TargetType.SingleAlly => "아군 1명",
            TargetType.Self => "자신",
            _ => "적 1명",
        };

        public static string Effect(StatusType t) => t switch
        {
            StatusType.AtkUp => "공격력 증가",
            StatusType.DefUp => "방어력 증가",
            StatusType.AtkDown => "공격력 감소",
            StatusType.DefDown => "방어력 감소",
            StatusType.Dot => "지속 피해",
            _ => "",
        };

        /// <summary>'적 1명 · 피해 180%' / '아군 1명 · 회복 200%' / '공격력 증가 25% (2턴)'.</summary>
        public static string Describe(BattleSkill s)
        {
            if (s == null) return "";
            var parts = new List<string>();
            if (s.IsHeal && s.TargetsAllies) parts.Add($"회복 {s.heal * 100f:0}%");
            else if (s.power > 0f) parts.Add($"피해 {s.power * 100f:0}%" + (s.hits > 1 ? $"×{s.hits}" : ""));
            if (s.applyEffect != StatusType.None)
            {
                string mag = s.applyEffect == StatusType.Dot ? $"{s.effectMagnitude * 100f:0}%" : $"{s.effectMagnitude * 100f:0}%";
                parts.Add($"{Effect(s.applyEffect)} {mag} ({s.effectDuration}턴)");
            }
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
