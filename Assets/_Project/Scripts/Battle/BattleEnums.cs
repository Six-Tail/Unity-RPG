namespace SubcultureRPG.Battle
{
    /// <summary>전투 진영.</summary>
    public enum Team
    {
        Ally,
        Enemy,
    }

    /// <summary>스킬 분류.</summary>
    public enum SkillCategory
    {
        Basic,     // 기본 공격(에너지 충전원)
        Skill,     // 일반 스킬
        Ultimate,  // 필살기(에너지 가득 필요, 사용 시 소모)
    }

    /// <summary>대상 지정 방식. (v1은 적 대상만 — 아군/버프/힐은 확장 포인트)</summary>
    public enum TargetType
    {
        SingleEnemy,
        AllEnemies,
    }
}
