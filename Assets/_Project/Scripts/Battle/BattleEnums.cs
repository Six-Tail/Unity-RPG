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
        Basic,     // 기본 공격(에너지 충전원, SP +1)
        Skill,     // 일반 스킬(SP -1)
        Ultimate,  // 필살기(에너지 가득 필요)
    }

    /// <summary>대상 지정 방식.</summary>
    public enum TargetType
    {
        SingleEnemy,
        AllEnemies,
        SingleAlly,
        AllAllies,
        Self,
    }

    /// <summary>속성.</summary>
    public enum Element
    {
        Physical, // 물리
        Fire,     // 화염
        Ice,      // 얼음
        Lightning,// 번개
        Wind,     // 바람
    }

    /// <summary>버프/디버프/지속효과 종류.</summary>
    public enum StatusType
    {
        None,
        AtkUp,
        AtkDown,
        DefUp,
        DefDown,
        Dot, // 지속 피해
    }
}
